/*
	Archivo: 30_AgenteFase4HerramientasDiagnostico.sql
	Objetivo: Convertir el diagnóstico en una investigación de varios pasos con herramientas catalogadas de solo lectura y simular cambios antes de ejecutarlos.
	Responsabilidad: Catalogar las herramientas diagnósticas (dbo.Usp_TI_AgenteDiag_*), registrar cada paso como evidencia del servidor,
		registrar las simulaciones transaccionales y aceptar los pasos que Live registra mediante funciones.
	Dependencias: Requiere 29_AgenteFase3ReproduccionUsuario.sql.
	Orden: Ejecutar después de 29_AgenteFase3ReproduccionUsuario.sql.
	Consideraciones: Las herramientas solo ejecutan SELECT y el backend las invoca dentro de una transacción que siempre revierte.
		El modelo elige herramienta y parámetros, pero nunca el procedimiento: el nombre sale de este catálogo y debe cumplir el prefijo dbo.Usp_TI_AgenteDiag_.
		La simulación ejecuta el mismo ejecutor de la acción catalogada y revierte siempre; no requiere aprobación porque no persiste cambios.
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

If Object_Id('dbo.TI_AgenteHerramienta', 'U') Is Null
Begin
	Create Table dbo.TI_AgenteHerramienta (
		HerramientaCodigo			varchar(40)		Not Null,
		Nombre						nvarchar(100)	Not Null,
		-- Se entrega al modelo: describe qué responde la herramienta y cuándo conviene usarla.
		Descripcion					nvarchar(800)	Not Null,
		Procedimiento				varchar(200)	Not Null,
		-- JSON Schema compatible con salidas estrictas: object, propiedades string/integer, todas requeridas.
		ParametrosEsquemaJson		nvarchar(max)	Not Null,
		-- Se ejecuta antes de consultar al modelo, con los parámetros por defecto.
		Automatica					bit				Not Null,
		RequiereTicket				bit				Not Null,
		MaximoFilas					int				Not Null,
		-- Lectura equivalente del catálogo de acciones (Tipo L), cuando existe.
		AccionCodigo				varchar(50)		Null,
		Estado						varchar(2)		Not Null,
		UltimoUsuario				varchar(20)		Null,
		UltimaFechaModif			datetime2(0)	Null,

		Constraint PK_TI_AgenteHerramienta Primary Key (HerramientaCodigo),
		Constraint FK_TI_AgenteHerramienta_Accion Foreign Key (AccionCodigo) References dbo.TI_Accion (AccionCodigo),
		Constraint CK_TI_AgenteHerramienta_Procedimiento Check (Procedimiento Like 'dbo.Usp_TI_AgenteDiag[_]%'),
		Constraint CK_TI_AgenteHerramienta_Esquema Check (IsJson(ParametrosEsquemaJson) = 1),
		Constraint CK_TI_AgenteHerramienta_MaximoFilas Check (MaximoFilas Between 1 and 200),
		Constraint CK_TI_AgenteHerramienta_Estado Check (Estado In ('A','I'))
	)
End
Go

/* ============================================================================
   Herramientas diagnósticas. Contrato común:
   - Parámetros: @cUsuario, @cArea, @nSesionNumero, @cParametrosJson.
   - Solo SELECT; un único conjunto de resultados.
   - La sesión debe pertenecer al operador y seguir en investigación.
   ============================================================================ */

Create Or Alter Procedure dbo.Usp_TI_AgenteDiag_HistorialTicket
/*================================================================================
Objetivo            : Reconstruir la línea de tiempo del ticket investigado: estados y avances técnicos.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : Herramienta de solo lectura del Agente de Ingeniería.
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3),
	@nSesionNumero bigint,
	@cParametrosJson nvarchar(max) = Null
As
Begin
	Set NoCount On
	Declare @cIncidencia varchar(12)
	Select @cIncidencia = IncidenciaNumero From dbo.TI_AgenteSesion
	Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario and AreaTI = @cArea and Estado In ('RECOPILANDO','OBSERVANDO','LISTO_INVESTIGAR')
	If @@RowCount = 0 Throw 50566, 'La investigación no pertenece al operador o ya no admite herramientas diagnósticas.', 1
	If @cIncidencia Is Null Throw 50567, 'La herramienta requiere un ticket vinculado a la investigación.', 1

	Select Top (60) Origen, Fecha, Estado, Usuario, Detalle
	From (
		Select 'TICKET' Origen, i.FechaRegistro Fecha, i.Estado, i.UsuarioTI Usuario,
			Concat(N'Estado actual ', i.Estado, N' · Prioridad ', i.Prioridad, N' · SLA ', i.SlaObjetivoMinutos, N' min · Asignado ', Convert(varchar(19), i.FechaAsignacion, 120),
				N' · Atención ', Convert(varchar(19), i.FechaAtencion, 120), N' · Cierre ', Convert(varchar(19), i.FechaCierre, 120)) Detalle
		From dbo.TI_Incidencia i Where i.IncidenciaNumero = @cIncidencia
		Union All
		Select 'ESTADO', e.FechaCambio, e.Estado, e.UsuarioCambio, Left(IsNull(e.Observacion, N''), 400)
		From dbo.TI_IncidenciaEstado e Where e.IncidenciaNumero = @cIncidencia
		Union All
		Select 'AVANCE', a.FechaAvance, Null, a.UsuarioTI, Left(Concat(a.Detalle, N' · Avance ', a.PorcentajeAvance, N'%'), 400)
		From dbo.TI_IncidenciaAvance a Where a.IncidenciaNumero = @cIncidencia
	) h
	Order By Fecha Desc
End
Go

Create Or Alter Procedure dbo.Usp_TI_AgenteDiag_AprobacionesEjecuciones
/*================================================================================
Objetivo            : Listar aprobaciones y ejecuciones de acciones del ticket investigado, con su resultado o error.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : Herramienta de solo lectura del Agente de Ingeniería.
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3),
	@nSesionNumero bigint,
	@cParametrosJson nvarchar(max) = Null
As
Begin
	Set NoCount On
	Declare @cIncidencia varchar(12)
	Select @cIncidencia = IncidenciaNumero From dbo.TI_AgenteSesion
	Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario and AreaTI = @cArea and Estado In ('RECOPILANDO','OBSERVANDO','LISTO_INVESTIGAR')
	If @@RowCount = 0 Throw 50566, 'La investigación no pertenece al operador o ya no admite herramientas diagnósticas.', 1
	If @cIncidencia Is Null Throw 50567, 'La herramienta requiere un ticket vinculado a la investigación.', 1

	Select Top (40) Registro, Secuencia, AccionCodigo, Estado, Usuario, Fecha, ParametrosJson, Resultado
	From (
		Select 'APROBACION' Registro, s.Secuencia, s.AccionCodigo, s.Estado, s.UsuarioSolicitante Usuario, s.FechaSolicitud Fecha,
			Left(s.ParametrosJson, 500) ParametrosJson, Left(Concat(N'Aprobador ', s.UsuarioAprobador, N' · ', s.ComentarioRespuesta), 500) Resultado
		From dbo.TI_SolicitudAprobacion s Where s.IncidenciaNumero = @cIncidencia
		Union All
		Select 'EJECUCION', e.Secuencia, e.AccionCodigo, e.Estado, e.UsuarioEjecutor, e.FechaInicio,
			Left(e.ParametrosJson, 500), Left(Coalesce(e.Error, e.ResultadoJson), 500)
		From dbo.TI_EjecucionAccion e Where e.IncidenciaNumero = @cIncidencia
	) r
	Order By Fecha Desc
End
Go

Create Or Alter Procedure dbo.Usp_TI_AgenteDiag_CuentaSolicitante
/*================================================================================
Objetivo            : Verificar el estado de la cuenta del solicitante del ticket y sus últimos cambios auditados.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : No devuelve clave, correo, teléfono ni documento: solo datos necesarios para diagnosticar accesos.
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3),
	@nSesionNumero bigint,
	@cParametrosJson nvarchar(max) = Null
As
Begin
	Set NoCount On
	Declare @cIncidencia varchar(12)
	Select @cIncidencia = IncidenciaNumero From dbo.TI_AgenteSesion
	Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario and AreaTI = @cArea and Estado In ('RECOPILANDO','OBSERVANDO','LISTO_INVESTIGAR')
	If @@RowCount = 0 Throw 50566, 'La investigación no pertenece al operador o ya no admite herramientas diagnósticas.', 1
	If @cIncidencia Is Null Throw 50567, 'La herramienta requiere un ticket vinculado a la investigación.', 1

	Select u.Usuario, u.Estado, u.Perfil, u.Area, u.AreaSolicitanteTicket, u.FuenteIdentidad, u.EstadoCorporativo,
		u.UltimaSincronizacion, u.UltimaFechaModif, u.UltimoUsuario,
		UltimosCambios = (
			Select Top (10) a.Fecha fecha, a.Evento evento, a.Resultado resultado, Left(a.DetalleJson, 300) detalle
			From dbo.TI_Auditoria a Where a.Entidad = 'TI_Usuario' and a.Registro = u.Usuario
			Order By a.Fecha Desc, a.AuditoriaNumero Desc
			For Json Path)
	From (
		Select t.Usuario, t.Estado, t.Perfil, t.Area, i.AreaSolicitante AreaSolicitanteTicket, t.FuenteIdentidad, t.EstadoCorporativo,
			t.UltimaSincronizacion, t.UltimaFechaModif, t.UltimoUsuario
		From dbo.TI_Incidencia i Join dbo.TI_Usuario t on t.Usuario = i.UsuarioSolicitante
		Where i.IncidenciaNumero = @cIncidencia
	) u
End
Go

Create Or Alter Procedure dbo.Usp_TI_AgenteDiag_TicketsSimilares
/*================================================================================
Objetivo            : Detectar incidencias recurrentes o masivas: tickets recientes con la misma clasificación o el mismo mensaje de error.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : Parámetro opcional {"dias": 1..90}; por defecto 30.
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3),
	@nSesionNumero bigint,
	@cParametrosJson nvarchar(max) = Null
As
Begin
	Set NoCount On
	Declare @cIncidencia varchar(12), @nDias int, @cLinea char(3), @cItem varchar(20), @cMensaje nvarchar(1000)
	Select @cIncidencia = IncidenciaNumero From dbo.TI_AgenteSesion
	Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario and AreaTI = @cArea and Estado In ('RECOPILANDO','OBSERVANDO','LISTO_INVESTIGAR')
	If @@RowCount = 0 Throw 50566, 'La investigación no pertenece al operador o ya no admite herramientas diagnósticas.', 1
	If @cIncidencia Is Null Throw 50567, 'La herramienta requiere un ticket vinculado a la investigación.', 1

	Set @nDias = IsNull(Try_Convert(int, Json_Value(IsNull(@cParametrosJson, '{}'), '$.dias')), 30)
	If @nDias Not Between 1 and 90 Throw 50568, 'El parámetro "dias" debe estar entre 1 y 90.', 1

	Select @cLinea = Linea, @cItem = Item, @cMensaje = NullIf(LTrim(RTrim(MensajeError)), N'') From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidencia

	Select Top (25) i.IncidenciaNumero, i.FechaRegistro, i.Estado, i.AreaSolicitante, i.Linea, i.Item,
		MismoMensajeError = Cast(Case When @cMensaje Is Not Null and i.MensajeError = @cMensaje Then 1 Else 0 End as bit),
		Left(i.Titulo, 150) Titulo, Left(i.CausaRaiz, 300) CausaRaiz, Left(i.SolucionTecnica, 300) SolucionTecnica
	From dbo.TI_Incidencia i
	Where i.IncidenciaNumero <> @cIncidencia and i.FechaRegistro >= DateAdd(day, -@nDias, SysDateTime())
		and ((@cMensaje Is Not Null and i.MensajeError = @cMensaje) or (i.Linea = @cLinea and (@cItem Is Null or i.Item = @cItem)))
	Order By MismoMensajeError Desc, i.FechaRegistro Desc
End
Go

Create Or Alter Procedure dbo.Usp_TI_AgenteDiag_BuscarError
/*================================================================================
Objetivo            : Buscar un texto de error en tickets de los últimos 90 días y agrupar dónde se repite.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : Parámetro {"texto": 5..200 caracteres}; los comodines de LIKE se escapan.
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3),
	@nSesionNumero bigint,
	@cParametrosJson nvarchar(max) = Null
As
Begin
	Set NoCount On
	Declare @cTexto nvarchar(200), @cPatron nvarchar(420)
	If Not Exists (Select 1 From dbo.TI_AgenteSesion
		Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario and AreaTI = @cArea and Estado In ('RECOPILANDO','OBSERVANDO','LISTO_INVESTIGAR'))
		Throw 50566, 'La investigación no pertenece al operador o ya no admite herramientas diagnósticas.', 1

	Set @cTexto = LTrim(RTrim(Json_Value(IsNull(@cParametrosJson, '{}'), '$.texto')))
	If Len(IsNull(@cTexto, N'')) Not Between 5 and 200 Throw 50569, 'El parámetro "texto" debe tener entre 5 y 200 caracteres.', 1
	Set @cPatron = N'%' + Replace(Replace(Replace(@cTexto, N'[', N'[[]'), N'%', N'[%]'), N'_', N'[_]') + N'%'

	Select Top (20) i.Linea, i.Item, i.Estado, i.AreaSolicitante, Tickets = Count(*), PrimerRegistro = Min(i.FechaRegistro), UltimoRegistro = Max(i.FechaRegistro),
		Ejemplo = Max(i.IncidenciaNumero), ConSolucion = Sum(Case When i.SolucionTecnica Is Not Null Then 1 Else 0 End)
	From dbo.TI_Incidencia i
	Where i.FechaRegistro >= DateAdd(day, -90, SysDateTime()) and (i.MensajeError Like @cPatron or i.Titulo Like @cPatron or i.Detalle Like @cPatron)
	Group By i.Linea, i.Item, i.Estado, i.AreaSolicitante
	Order By Tickets Desc, UltimoRegistro Desc
End
Go

Create Or Alter Procedure dbo.Usp_TI_AgenteDiag_BuscarConocimiento
/*================================================================================
Objetivo            : Buscar artículos activos de la base de conocimiento por texto libre.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : Parámetro {"termino": 3..80 caracteres}; complementa el ranking por clasificación del contexto.
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3),
	@nSesionNumero bigint,
	@cParametrosJson nvarchar(max) = Null
As
Begin
	Set NoCount On
	Declare @cTermino nvarchar(80), @cPatron nvarchar(260)
	If Not Exists (Select 1 From dbo.TI_AgenteSesion
		Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario and AreaTI = @cArea and Estado In ('RECOPILANDO','OBSERVANDO','LISTO_INVESTIGAR'))
		Throw 50566, 'La investigación no pertenece al operador o ya no admite herramientas diagnósticas.', 1

	Set @cTermino = LTrim(RTrim(Json_Value(IsNull(@cParametrosJson, '{}'), '$.termino')))
	If Len(IsNull(@cTermino, N'')) Not Between 3 and 80 Throw 50570, 'El parámetro "termino" debe tener entre 3 y 80 caracteres.', 1
	Set @cPatron = N'%' + Replace(Replace(Replace(@cTermino, N'[', N'[[]'), N'%', N'[%]'), N'_', N'[_]') + N'%'

	Select Top (10) k.ConocimientoCodigo, Left(k.Titulo, 200) Titulo, Left(k.MensajeError, 300) MensajeError,
		Left(k.Causa, 500) Causa, Left(k.Solucion, 600) Solucion, k.Linea, k.Item, k.IncidenciaOrigen
	From dbo.TI_BaseConocimiento k
	Where k.Estado = 'A' and (k.Titulo Like @cPatron or k.Problema Like @cPatron or k.Sintomas Like @cPatron or k.MensajeError Like @cPatron or k.Causa Like @cPatron)
	Order By k.FechaValidacion Desc, k.FechaCreacion Desc
End
Go

Create Or Alter Procedure dbo.Usp_TI_AgenteDiag_DocumentoRelacionado
/*================================================================================
Objetivo            : Encontrar otros tickets que reportaron problemas con el mismo documento empresarial.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : Parámetros {"tipoDocumento": 1..10, "numeroDocumento": 1..20}. No consulta tablas del ERP.
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3),
	@nSesionNumero bigint,
	@cParametrosJson nvarchar(max) = Null
As
Begin
	Set NoCount On
	Declare @cTipo varchar(10), @cNumero varchar(20)
	If Not Exists (Select 1 From dbo.TI_AgenteSesion
		Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario and AreaTI = @cArea and Estado In ('RECOPILANDO','OBSERVANDO','LISTO_INVESTIGAR'))
		Throw 50566, 'La investigación no pertenece al operador o ya no admite herramientas diagnósticas.', 1

	Set @cTipo = Upper(LTrim(RTrim(Json_Value(IsNull(@cParametrosJson, '{}'), '$.tipoDocumento'))))
	Set @cNumero = Upper(LTrim(RTrim(Json_Value(IsNull(@cParametrosJson, '{}'), '$.numeroDocumento'))))
	If NullIf(@cTipo, '') Is Null or NullIf(@cNumero, '') Is Null Throw 50571, 'Los parámetros "tipoDocumento" y "numeroDocumento" son obligatorios.', 1

	Select Top (25) d.IncidenciaNumero, d.CompaniaSocio, d.TipoDocumento, d.NumeroDocumento, Left(d.Descripcion, 200) Descripcion,
		i.Estado, i.FechaRegistro, Left(i.Titulo, 150) Titulo, Left(i.MensajeError, 300) MensajeError, Left(i.SolucionTecnica, 300) SolucionTecnica
	From dbo.TI_IncidenciaDocumento d Join dbo.TI_Incidencia i on i.IncidenciaNumero = d.IncidenciaNumero
	Where d.TipoDocumento = @cTipo and d.NumeroDocumento = @cNumero
	Order By i.FechaRegistro Desc
End
Go

/* ============================================================================
   Catálogo de herramientas
   ============================================================================ */

Merge dbo.TI_AgenteHerramienta As t
Using (Values
	('DIAG_HISTORIAL_TICKET', N'Historial del ticket',
		N'Devuelve la línea de tiempo del ticket investigado: estado actual, SLA, cambios de estado y avances técnicos registrados. Úsala para saber qué se hizo antes y desde cuándo está detenido.',
		'dbo.Usp_TI_AgenteDiag_HistorialTicket', N'{"type":"object","properties":{},"required":[],"additionalProperties":false}', 1, 1, 60),
	('DIAG_APROBACIONES_EJECUCIONES', N'Aprobaciones y ejecuciones',
		N'Devuelve las solicitudes de aprobación y las ejecuciones de acciones del ticket, con parámetros, resultado o error. Úsala para detectar acciones fallidas, rechazadas o pendientes.',
		'dbo.Usp_TI_AgenteDiag_AprobacionesEjecuciones', N'{"type":"object","properties":{},"required":[],"additionalProperties":false}', 1, 1, 40),
	('DIAG_CUENTA_SOLICITANTE', N'Cuenta del solicitante',
		N'Devuelve el estado de la cuenta del solicitante del ticket (estado, perfil, área, sincronización corporativa) y sus últimos cambios auditados. Úsala en problemas de acceso, login o permisos.',
		'dbo.Usp_TI_AgenteDiag_CuentaSolicitante', N'{"type":"object","properties":{},"required":[],"additionalProperties":false}', 1, 1, 1),
	('DIAG_TICKETS_SIMILARES', N'Tickets similares recientes',
		N'Devuelve tickets de los últimos N días con el mismo mensaje de error o la misma línea/ítem. Úsala para saber si el problema es recurrente o masivo y cómo se resolvió antes.',
		'dbo.Usp_TI_AgenteDiag_TicketsSimilares', N'{"type":"object","properties":{"dias":{"type":"integer","description":"Ventana en días hacia atrás (1 a 90).","minimum":1,"maximum":90}},"required":["dias"],"additionalProperties":false}', 1, 1, 25),
	('DIAG_BUSCAR_ERROR', N'Buscar texto de error',
		N'Busca un fragmento literal del mensaje de error en tickets de los últimos 90 días y agrupa por línea, ítem, estado y área. Úsala con el texto exacto observado en pantalla.',
		'dbo.Usp_TI_AgenteDiag_BuscarError', N'{"type":"object","properties":{"texto":{"type":"string","description":"Fragmento literal del mensaje de error (5 a 200 caracteres).","minLength":5,"maxLength":200}},"required":["texto"],"additionalProperties":false}', 0, 0, 20),
	('DIAG_BUSCAR_CONOCIMIENTO', N'Buscar en base de conocimiento',
		N'Busca artículos activos de la base de conocimiento por un término (error, módulo, proceso). Úsala cuando el conocimiento entregado en el contexto no cubre el síntoma.',
		'dbo.Usp_TI_AgenteDiag_BuscarConocimiento', N'{"type":"object","properties":{"termino":{"type":"string","description":"Término a buscar (3 a 80 caracteres).","minLength":3,"maxLength":80}},"required":["termino"],"additionalProperties":false}', 0, 0, 10),
	('DIAG_DOCUMENTO_RELACIONADO', N'Tickets del mismo documento',
		N'Devuelve otros tickets que reportaron el mismo documento empresarial (tipo y número). Úsala cuando el ticket o la reproducción mencionan un documento concreto.',
		'dbo.Usp_TI_AgenteDiag_DocumentoRelacionado', N'{"type":"object","properties":{"tipoDocumento":{"type":"string","description":"Tipo de documento, por ejemplo OC o REQ.","minLength":1,"maxLength":10},"numeroDocumento":{"type":"string","description":"Número del documento.","minLength":1,"maxLength":20}},"required":["tipoDocumento","numeroDocumento"],"additionalProperties":false}', 0, 0, 25)
) As s (HerramientaCodigo, Nombre, Descripcion, Procedimiento, ParametrosEsquemaJson, Automatica, RequiereTicket, MaximoFilas)
On t.HerramientaCodigo = s.HerramientaCodigo
When Matched Then Update Set Nombre = s.Nombre, Descripcion = s.Descripcion, Procedimiento = s.Procedimiento, ParametrosEsquemaJson = s.ParametrosEsquemaJson,
	Automatica = s.Automatica, RequiereTicket = s.RequiereTicket, MaximoFilas = s.MaximoFilas, UltimaFechaModif = SysDateTime()
When Not Matched Then Insert (HerramientaCodigo, Nombre, Descripcion, Procedimiento, ParametrosEsquemaJson, Automatica, RequiereTicket, MaximoFilas, AccionCodigo, Estado, UltimoUsuario, UltimaFechaModif)
	Values (s.HerramientaCodigo, s.Nombre, s.Descripcion, s.Procedimiento, s.ParametrosEsquemaJson, s.Automatica, s.RequiereTicket, s.MaximoFilas, Null, 'A', Null, SysDateTime());
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_Herramientas
/*================================================================================
Objetivo            : Entregar al backend el catálogo activo de herramientas diagnósticas.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : Solo se listan herramientas cuyo procedimiento está instalado.
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3)
As
Begin
	Set NoCount On
	If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM'))
		Throw 50500, 'El operador TI no se encuentra habilitado.', 1

	Select HerramientaCodigo, Nombre, Descripcion, Procedimiento, ParametrosEsquemaJson, Automatica, RequiereTicket, MaximoFilas
	From dbo.TI_AgenteHerramienta
	Where Estado = 'A' and Object_Id(Procedimiento, 'P') Is Not Null
	Order By Automatica Desc, HerramientaCodigo
End
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_RegistrarHerramienta
/*================================================================================
Objetivo            : Registrar un paso de investigación ejecutado con una herramienta diagnóstica.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : Evidencia del servidor (OrigenServidor = 1, fuente DIAGNOSTICO); el navegador no puede registrarla.
================================================================================*/
	@cUsuario varchar(20),
	@nSesionNumero bigint,
	@cHerramientaCodigo varchar(40),
	@cOrigen varchar(20),
	@cContenido nvarchar(2000),
	@cDatosJson nvarchar(max)
As
Begin
	Set NoCount On
	Set Xact_Abort On

	If Not Exists (Select 1 From dbo.TI_AgenteHerramienta Where HerramientaCodigo = @cHerramientaCodigo) Throw 50572, 'La herramienta diagnóstica no pertenece al catálogo.', 1
	If @cOrigen Not In ('AUTOMATICA','MODELO') Throw 50573, 'El origen del paso de investigación no es válido.', 1
	If IsNull(IsJson(@cDatosJson), 0) <> 1 Throw 50506, 'Los datos técnicos del evento no tienen formato JSON válido.', 1

	Begin Try
		Begin Transaction
		If Not Exists (Select 1 From dbo.TI_AgenteSesion With (UpdLock, HoldLock)
			Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario and Estado In ('RECOPILANDO','OBSERVANDO','LISTO_INVESTIGAR'))
			Throw 50566, 'La investigación no pertenece al operador o ya no admite herramientas diagnósticas.', 1

		Declare @nSecuencia int
		Select @nSecuencia = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_AgenteEvento With (UpdLock, HoldLock) Where SesionNumero = @nSesionNumero
		Insert dbo.TI_AgenteEvento (SesionNumero, Secuencia, Tipo, Fuente, Contenido, DatosJson, Fecha, OrigenServidor)
		Values (@nSesionNumero, @nSecuencia, 'HERRAMIENTA_DIAGNOSTICO', 'DIAGNOSTICO', Left(@cContenido, 2000), @cDatosJson, SysDateTime(), 1)
		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go

/* ============================================================================
   Simulación de cambios
   ============================================================================ */

Create Or Alter Procedure dbo.Usp_TI_Agente_ObtenerEjecutorSimulacion
/*================================================================================
Objetivo            : Entregar al backend el ejecutor y los parámetros de la acción propuesta para simularla.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : Los parámetros salen de la sesión (los mismos que se aprobarían); el navegador no los envía.
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3),
	@nSesionNumero bigint
As
Begin
	Set NoCount On
	Declare @cIncidencia varchar(12), @cAccion varchar(50), @cParametros nvarchar(max), @cEstado varchar(30)

	Select @cIncidencia = IncidenciaNumero, @cAccion = AccionCodigo, @cParametros = ParametrosJson, @cEstado = Estado
	From dbo.TI_AgenteSesion Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario and AreaTI = @cArea
	If @@RowCount = 0 Throw 50503, 'La sesión de investigación no existe o no pertenece al operador.', 1
	If @cEstado Not In ('PENDIENTE_TI','PENDIENTE_APROBACION','LISTO_EJECUCION','ERROR_EJECUCION')
		Throw 50574, 'La simulación solo está disponible con un diagnóstico pendiente de decisión.', 1
	If @cAccion Is Null or @cIncidencia Is Null Throw 50575, 'El diagnóstico no propone una acción catalogada sobre un ticket vinculado.', 1

	Select @cIncidencia IncidenciaNumero, @cAccion AccionCodigo, IsNull(@cParametros, N'{}') ParametrosJson, x.Procedimiento, x.MaximoFilas
	From dbo.TI_AgenteAccionEjecutor x
	Join dbo.TI_Accion a on a.AccionCodigo = x.AccionCodigo and a.Tipo = 'E' and a.Estado = 'A'
	Where x.AccionCodigo = @cAccion and x.Estado = 'A' and Object_Id(x.Procedimiento, 'P') Is Not Null
	If @@RowCount = 0 Throw 50576, 'La acción propuesta no tiene un ejecutor autorizado instalado; no puede simularse.', 1
End
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_RegistrarSimulacion
/*================================================================================
Objetivo            : Registrar el resultado de una simulación transaccional (siempre revertida) de la acción propuesta.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : Deja evidencia y auditoría; el dry-run la considera vigente solo si los parámetros no cambiaron.
================================================================================*/
	@cUsuario varchar(20),
	@nSesionNumero bigint,
	@lExito bit,
	@cParametrosJson nvarchar(max),
	@cResultadoJson nvarchar(max) = Null,
	@nFilasAfectadas int = Null,
	@cError nvarchar(2000) = Null
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Declare @cIncidencia varchar(12), @cAccion varchar(50), @cCorrelacion uniqueidentifier, @nSecuencia int, @dFecha datetime2(0) = SysDateTime(), @cDatos nvarchar(max)

	Begin Try
		Begin Transaction
		Select @cIncidencia = IncidenciaNumero, @cAccion = AccionCodigo, @cCorrelacion = IdCorrelacion
		From dbo.TI_AgenteSesion With (UpdLock, HoldLock)
		Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario and Estado In ('PENDIENTE_TI','PENDIENTE_APROBACION','LISTO_EJECUCION','ERROR_EJECUCION')
		If @@RowCount = 0 Throw 50574, 'La simulación solo está disponible con un diagnóstico pendiente de decisión.', 1

		Set @cDatos = (Select accionCodigo = @cAccion, exito = @lExito, filasAfectadas = @nFilasAfectadas, parametrosJson = @cParametrosJson,
			resultado = Json_Query(Case When IsJson(@cResultadoJson) = 1 Then @cResultadoJson End), error = @cError, revertida = Cast(1 as bit)
			For Json Path, Without_Array_Wrapper)

		Select @nSecuencia = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_AgenteEvento With (UpdLock, HoldLock) Where SesionNumero = @nSesionNumero
		Insert dbo.TI_AgenteEvento (SesionNumero, Secuencia, Tipo, Fuente, Contenido, DatosJson, Fecha, OrigenServidor)
		Values (@nSesionNumero, @nSecuencia, 'SIMULACION_CAMBIO', 'BACKEND',
			Case When @lExito = 1 Then Concat(N'Simulación de ', @cAccion, N' exitosa: ', IsNull(@nFilasAfectadas, 0), N' fila(s) afectada(s); la transacción fue revertida.')
				Else Left(Concat(N'Simulación de ', @cAccion, N' rechazada: ', @cError), 2000) End,
			@cDatos, @dFecha, 1)

		Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
		Values (@cIncidencia, @cUsuario, 'T', 'TI_AgenteSesion', Convert(varchar(30), @nSesionNumero), 'SIMULAR_CAMBIO_AGENTE',
			Case When @lExito = 1 Then 'SIMULADO_OK' Else 'SIMULADO_ERROR' End, @cDatos, @cCorrelacion, @dFecha)
		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_DryRun
	@cUsuario varchar(20), @cArea char(3), @nSesionNumero bigint
As
Begin
	Set NoCount On
	-- Solo SELECT: no invoca ejecutores, crea aprobaciones ni cambia estados.
	Declare @cIncidencia varchar(12), @cAccion varchar(50), @cParametros nvarchar(max), @nSolicitud int
	Select @cIncidencia = IncidenciaNumero, @cAccion = AccionCodigo, @cParametros = ParametrosJson, @nSolicitud = SolicitudAprobacionSecuencia
	From dbo.TI_AgenteSesion Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario and AreaTI = @cArea
	If @@RowCount = 0 Throw 50503, 'La investigacion no pertenece al operador.', 1
	Select 'IDENTIDAD' Codigo, 'Operador TI habilitado' Descripcion,
		Case When Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM')) Then 'OK' Else 'ERROR' End Resultado
	Union All Select 'TICKET', 'Ticket vinculado y existente', Case When Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidencia) Then 'OK' Else 'PENDIENTE' End
	Union All Select 'ESTADO', 'Ticket admite intervencion', Case When Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidencia and Estado Not In ('RS','CA','CF','NP')) Then 'OK' Else 'PENDIENTE' End
	Union All Select 'ACCION', 'Accion correctiva activa', Case When Exists (Select 1 From dbo.TI_Accion Where AccionCodigo = @cAccion and Tipo = 'E' and Estado = 'A') Then 'OK' Else 'PENDIENTE' End
	Union All Select 'PARAMETROS', 'Parametros estructurados validos', Case When IsJson(@cParametros) = 1 and Left(LTrim(@cParametros), 1) = '{' Then 'OK' Else 'PENDIENTE' End
	Union All Select 'APROBACION', 'Aprobacion formal para esta accion y parametros', Case When Exists (Select 1 From dbo.TI_Accion Where AccionCodigo = @cAccion and RequiereAprobacion = 0)
		or Exists (Select 1 From dbo.TI_SolicitudAprobacion Where IncidenciaNumero = @cIncidencia and Secuencia = @nSolicitud and AccionCodigo = @cAccion and Estado = 'A' and IsNull(ParametrosJson, '{}') = IsNull(@cParametros, '{}')) Then 'OK' Else 'PENDIENTE' End
	Union All Select 'EJECUTOR', 'Procedimiento autorizado instalado', Case When Exists (Select 1 From dbo.TI_AgenteAccionEjecutor Where AccionCodigo = @cAccion and Estado = 'A' and Object_Id(Procedimiento, 'P') Is Not Null) Then 'OK' Else 'PENDIENTE' End
	-- La simulación más reciente decide: si falló o usó otros parámetros, deja de valer.
	-- Coalesce y no IsNull: IsNull tomaría el largo de 'OK'/'ERROR' y truncaría 'PENDIENTE'.
	Union All Select 'SIMULACION', 'Simulacion transaccional exitosa con los mismos parametros',
		Coalesce((Select Top (1) Case When Json_Value(e.DatosJson, '$.exito') = 'true' and Json_Value(e.DatosJson, '$.accionCodigo') = @cAccion
				and Json_Value(e.DatosJson, '$.parametrosJson') = IsNull(@cParametros, N'{}') Then 'OK' Else 'ERROR' End
			From dbo.TI_AgenteEvento e Where e.SesionNumero = @nSesionNumero and e.Tipo = 'SIMULACION_CAMBIO' and e.OrigenServidor = 1
			Order By e.Secuencia Desc), 'PENDIENTE')
	Union All Select 'ERP', 'Validaciones internas ERP: requieren contrato diagnostico del propietario', 'NO_DISPONIBLE'
End
Go

/* ============================================================================
   Live con funciones: el modelo registra cada paso que observa (PASO_OBSERVADO)
   ============================================================================ */

Create Or Alter Procedure dbo.Usp_TI_Agente_RegistrarEvento
/*================================================================================
Objetivo            : Registrar evidencias observadas durante la sesión Live o aportadas por el operador TI.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 01/10/2026
SP Anterior         : 25_AsistenteIngenieriaAutonomo.sql
Comentario Cambios  : 02/10/2026 · Admite PASO_OBSERVADO, registrado por la función Live registrar_paso.
================================================================================*/
	@cUsuario varchar(20),
	@nSesionNumero bigint,
	@cTipo varchar(40),
	@cFuente varchar(30),
	@cContenido nvarchar(max),
	@cDatosJson nvarchar(max) = Null
As
Begin
	Set NoCount On
	Set Xact_Abort On

	If Not Exists (Select 1 From dbo.TI_AgenteSesion Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario) Throw 50503, 'La sesión de investigación no existe o no pertenece al operador.', 1
	If Exists (Select 1 From dbo.TI_AgenteSesion Where SesionNumero = @nSesionNumero and Estado In ('INFORME_GRABADO','CAMBIO_VALIDADO','CANCELADO')) Throw 50504, 'La investigación ya se encuentra finalizada.', 1
	If NullIf(LTrim(RTrim(@cTipo)), '') Is Null or NullIf(LTrim(RTrim(@cFuente)), '') Is Null or NullIf(LTrim(RTrim(@cContenido)), '') Is Null Throw 50505, 'El evento de investigación no contiene información suficiente.', 1
	If @cDatosJson Is Not Null and IsJson(@cDatosJson) = 0 Throw 50506, 'Los datos técnicos del evento no tienen formato JSON válido.', 1
	If Upper(@cFuente) Not In ('LIVE','USUARIO') or Upper(@cTipo) Not In ('INICIO_LIVE','TRANSCRIPCION_USUARIO','TRANSCRIPCION_AGENTE','ERROR_OBSERVADO','PASO_OBSERVADO','FIN_LIVE','NOTA_USUARIO')
		Throw 50526, 'La evidencia del navegador no puede declararse telemetría o código del servidor.', 1

	Declare @nSecuencia int, @dFecha datetime2(0) = SysDateTime()

	Begin Transaction
	If Not Exists (Select 1 From dbo.TI_AgenteSesion With (UpdLock, HoldLock) Where SesionNumero = @nSesionNumero and Estado In ('RECOPILANDO','OBSERVANDO','LISTO_INVESTIGAR'))
		Throw 50527, 'La investigación ya no admite nuevas observaciones.', 1
	Select @nSecuencia = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_AgenteEvento With (UpdLock, HoldLock) Where SesionNumero = @nSesionNumero
	Insert dbo.TI_AgenteEvento (SesionNumero, Secuencia, Tipo, Fuente, Contenido, DatosJson, Fecha)
	Values (@nSesionNumero, @nSecuencia, Upper(LTrim(RTrim(@cTipo))), Upper(LTrim(RTrim(@cFuente))), @cContenido, @cDatosJson, @dFecha)

	Update dbo.TI_AgenteSesion
	Set Estado = Case When Estado = 'RECOPILANDO' Then 'OBSERVANDO' Else Estado End
	Where SesionNumero = @nSesionNumero
	Commit Transaction
End
Go

Create Or Alter Procedure dbo.Usp_TI_Reproduccion_RegistrarEvento
/*================================================================================
Objetivo            : Registrar la evidencia Live aportada por el usuario invitado.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : 29_AgenteFase3ReproduccionUsuario.sql
Comentario Cambios  : Admite PASO_OBSERVADO (función Live registrar_paso). Solo fuente LIVE_USUARIO; nunca telemetría, código ni decisiones.
================================================================================*/
	@cUsuario varchar(20),
	@nSesionNumero bigint,
	@cTipo varchar(40),
	@cContenido nvarchar(max)
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Declare @nSecuencia int
	Set @cTipo = Upper(LTrim(RTrim(@cTipo)))
	If @cTipo Not In ('INICIO_LIVE','FIN_LIVE','TRANSCRIPCION_USUARIO','TRANSCRIPCION_AGENTE','ERROR_OBSERVADO','PASO_OBSERVADO','NOTA_USUARIO')
		Throw 50564, 'El tipo de evidencia no es válido para una reproducción del usuario.', 1
	If NullIf(LTrim(RTrim(@cContenido)), '') Is Null Throw 50565, 'La evidencia no contiene información.', 1

	Begin Try
		Begin Transaction
		If Not Exists (Select 1 From dbo.TI_AgenteSesion With (UpdLock, HoldLock)
			Where SesionNumero = @nSesionNumero and UsuarioInvitado = @cUsuario and EstadoInvitacion = 'ACEPTADA' and InvitacionExpira > SysDateTime()
				and Estado In ('RECOPILANDO','OBSERVANDO','LISTO_INVESTIGAR'))
			Throw 50562, 'La sesión de reproducción ya no está activa.', 1

		Select @nSecuencia = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_AgenteEvento With (UpdLock, HoldLock) Where SesionNumero = @nSesionNumero
		Insert dbo.TI_AgenteEvento (SesionNumero, Secuencia, Tipo, Fuente, Contenido, DatosJson, Fecha, OrigenServidor)
		Values (@nSesionNumero, @nSecuencia, @cTipo, 'LIVE_USUARIO', Left(@cContenido, 12000), Null, SysDateTime(), 0)

		Update dbo.TI_AgenteSesion Set Estado = 'OBSERVANDO' Where SesionNumero = @nSesionNumero and Estado = 'RECOPILANDO'
		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go
