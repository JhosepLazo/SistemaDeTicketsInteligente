/*
	Archivo: 31_AgenteFase5ConocimientoSemantico.sql
	Objetivo: Dar al Agente de Ingeniería y al asistente del colaborador búsqueda por significado sobre el conocimiento corporativo.
	Responsabilidad: Guardar los vectores (embeddings) de artículos y casos resueltos, entregar el corpus autorizado según el perfil
		y registrar la herramienta interna DIAG_CONOCIMIENTO_SEMANTICO en el catálogo del agente.
	Dependencias: Requiere 30_AgenteFase4HerramientasDiagnostico.sql.
	Orden: Ejecutar después de 30_AgenteFase4HerramientasDiagnostico.sql.
	Consideraciones: Los vectores se calculan en el backend con el proveedor de IA configurado y se guardan con la huella del texto,
		para recalcular solo lo que cambió. El colaborador solo accede a artículos activos visibles para usuarios; los casos resueltos
		(tickets de otras personas) solo forman parte del corpus de TI.
*/

Use [GestionSistemas]
Go

Set Xact_Abort On
Set Ansi_Nulls On
Set Ansi_Padding On
Set Ansi_Warnings On
Set ArithAbort On
Set Concat_Null_Yields_Null On
Set Quoted_Identifier On
Set Numeric_RoundAbort Off
Go

If Object_Id('dbo.TI_ConocimientoVector', 'U') Is Null
Begin
	Create Table dbo.TI_ConocimientoVector (
		-- K = artículo de la base de conocimiento, T = ticket resuelto.
		Origen						char(1)			Not Null,
		Codigo						varchar(20)		Not Null,
		Modelo						varchar(60)		Not Null,
		-- SHA-256 del texto vectorizado: si el artículo o ticket cambia, el vector se recalcula.
		Huella						char(64)		Not Null,
		Dimensiones					smallint		Not Null,
		-- float32 little-endian.
		Vector						varbinary(max)	Not Null,
		FechaGeneracion				datetime2(0)	Not Null,

		Constraint PK_TI_ConocimientoVector Primary Key (Origen, Codigo, Modelo),
		Constraint CK_TI_ConocimientoVector_Origen Check (Origen In ('K','T')),
		Constraint CK_TI_ConocimientoVector_Dimensiones Check (Dimensiones Between 64 and 4096)
	)
End
Go

/* Herramientas internas: se ejecutan en el backend (no son procedimientos), pero se catalogan igual para que TI pueda activarlas o no. */
If Col_Length('dbo.TI_AgenteHerramienta', 'Tipo') Is Null
	Alter Table dbo.TI_AgenteHerramienta Add Tipo varchar(10) Not Null Constraint DF_TI_AgenteHerramienta_Tipo Default ('SP')
Go
If Exists (Select 1 From sys.columns Where object_id = Object_Id('dbo.TI_AgenteHerramienta') and name = 'Procedimiento' and is_nullable = 0)
Begin
	Alter Table dbo.TI_AgenteHerramienta Drop Constraint CK_TI_AgenteHerramienta_Procedimiento
	Alter Table dbo.TI_AgenteHerramienta Alter Column Procedimiento varchar(200) Null
End
Go
If Not Exists (Select 1 From sys.check_constraints Where name = 'CK_TI_AgenteHerramienta_TipoProcedimiento')
	Alter Table dbo.TI_AgenteHerramienta Add Constraint CK_TI_AgenteHerramienta_TipoProcedimiento
		Check ((Tipo = 'SP' and Procedimiento Like 'dbo.Usp_TI_AgenteDiag[_]%') or (Tipo = 'INTERNA' and Procedimiento Is Null))
Go
If Exists (Select 1 From sys.check_constraints Where name = 'CK_TI_AgenteHerramienta_Procedimiento')
	Alter Table dbo.TI_AgenteHerramienta Drop Constraint CK_TI_AgenteHerramienta_Procedimiento
Go

If Not Exists (Select 1 From dbo.TI_AgenteHerramienta Where HerramientaCodigo = 'DIAG_CONOCIMIENTO_SEMANTICO')
	Insert dbo.TI_AgenteHerramienta (HerramientaCodigo, Nombre, Descripcion, Procedimiento, ParametrosEsquemaJson, Automatica, RequiereTicket, MaximoFilas, AccionCodigo, Estado, UltimoUsuario, UltimaFechaModif, Tipo)
	Values ('DIAG_CONOCIMIENTO_SEMANTICO', N'Casos y guías similares (búsqueda semántica)',
		N'Busca por significado, no por palabras exactas, en la base de conocimiento y en tickets resueltos con causa y solución registradas. Devuelve los casos más parecidos con su porcentaje de similitud. Úsala con una descripción del síntoma o del error para encontrar soluciones previas.',
		Null, N'{"type":"object","properties":{"consulta":{"type":"string","description":"Síntoma, proceso o mensaje de error a buscar (5 a 400 caracteres).","minLength":5,"maxLength":400}},"required":["consulta"],"additionalProperties":false}',
		1, 0, 8, Null, 'A', Null, SysDateTime(), 'INTERNA')
Else
	Update dbo.TI_AgenteHerramienta
	Set Nombre = N'Casos y guías similares (búsqueda semántica)',
		Descripcion = N'Busca por significado, no por palabras exactas, en la base de conocimiento y en tickets resueltos con causa y solución registradas. Devuelve los casos más parecidos con su porcentaje de similitud. Úsala con una descripción del síntoma o del error para encontrar soluciones previas.',
		ParametrosEsquemaJson = N'{"type":"object","properties":{"consulta":{"type":"string","description":"Síntoma, proceso o mensaje de error a buscar (5 a 400 caracteres).","minLength":5,"maxLength":400}},"required":["consulta"],"additionalProperties":false}',
		UltimaFechaModif = SysDateTime()
	Where HerramientaCodigo = 'DIAG_CONOCIMIENTO_SEMANTICO'
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_Herramientas
/*================================================================================
Objetivo            : Entregar al backend el catálogo activo de herramientas diagnósticas.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : 30_AgenteFase4HerramientasDiagnostico.sql
Comentario Cambios  : 02/10/2026 · Incluye herramientas internas (Tipo INTERNA), que el backend ejecuta sin procedimiento.
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3)
As
Begin
	Set NoCount On
	If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM'))
		Throw 50500, 'El operador TI no se encuentra habilitado.', 1

	Select HerramientaCodigo, Nombre, Descripcion, IsNull(Procedimiento, '') Procedimiento, ParametrosEsquemaJson, Automatica, RequiereTicket, MaximoFilas, Tipo
	From dbo.TI_AgenteHerramienta
	Where Estado = 'A' and (Tipo = 'INTERNA' or Object_Id(Procedimiento, 'P') Is Not Null)
	Order By Automatica Desc, HerramientaCodigo
End
Go

Create Or Alter Procedure dbo.Usp_TI_Conocimiento_Corpus
/*================================================================================
Objetivo            : Entregar el conocimiento que puede buscarse por significado, con su vector guardado si existe.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : @lSoloUsuario = 1 limita a artículos activos visibles para colaboradores; TI recibe además los tickets resueltos.
================================================================================*/
	@lSoloUsuario bit,
	@cModelo varchar(60)
As
Begin
	Set NoCount On

	Select c.Origen, c.Codigo, c.Titulo, c.Texto, c.Linea, c.Item, v.Huella, v.Dimensiones, v.Vector
	From (
		Select 'K' Origen, k.ConocimientoCodigo Codigo, k.Titulo, k.Linea, k.Item,
			Left(Concat(k.Titulo, N'. Problema: ', k.Problema, N'. Síntomas: ', k.Sintomas, N'. Error: ', k.MensajeError,
				N'. Causa: ', k.Causa, N'. Solución: ', k.Solucion, N'. Procedimiento: ', k.Procedimiento), 4000) Texto
		From dbo.TI_BaseConocimiento k
		Where k.Estado = 'A' and (@lSoloUsuario = 0 or k.VisibleUsuario = 1)
		Union All
		Select 'T', i.IncidenciaNumero, i.Titulo, i.Linea, i.Item,
			Left(Concat(i.Titulo, N'. Error: ', i.MensajeError, N'. Detalle: ', Left(i.Detalle, 800),
				N'. Causa raíz: ', i.CausaRaiz, N'. Solución aplicada: ', i.SolucionTecnica), 4000)
		From dbo.TI_Incidencia i
		Where @lSoloUsuario = 0 and NullIf(LTrim(RTrim(i.SolucionTecnica)), N'') Is Not Null
			and i.FechaRegistro >= DateAdd(year, -2, SysDateTime())
	) c
	Left Join dbo.TI_ConocimientoVector v on v.Origen = c.Origen and v.Codigo = c.Codigo and v.Modelo = @cModelo
End
Go

Create Or Alter Procedure dbo.Usp_TI_Conocimiento_GuardarVector
/*================================================================================
Objetivo            : Guardar o actualizar el vector de un artículo o caso resuelto.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : Idempotente por (Origen, Codigo, Modelo).
================================================================================*/
	@cOrigen char(1),
	@cCodigo varchar(20),
	@cModelo varchar(60),
	@cHuella char(64),
	@nDimensiones smallint,
	@bVector varbinary(max)
As
Begin
	Set NoCount On
	Set Xact_Abort On
	If @cOrigen Not In ('K','T') Throw 50577, 'El origen del conocimiento no es válido.', 1
	If DataLength(@bVector) <> @nDimensiones * 4 Throw 50578, 'El vector no coincide con sus dimensiones.', 1

	Begin Try
		Begin Transaction
		Update dbo.TI_ConocimientoVector With (UpdLock, HoldLock)
		Set Huella = @cHuella, Dimensiones = @nDimensiones, Vector = @bVector, FechaGeneracion = SysDateTime()
		Where Origen = @cOrigen and Codigo = @cCodigo and Modelo = @cModelo
		If @@RowCount = 0
			Insert dbo.TI_ConocimientoVector (Origen, Codigo, Modelo, Huella, Dimensiones, Vector, FechaGeneracion)
			Values (@cOrigen, @cCodigo, @cModelo, @cHuella, @nDimensiones, @bVector, SysDateTime())
		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go
