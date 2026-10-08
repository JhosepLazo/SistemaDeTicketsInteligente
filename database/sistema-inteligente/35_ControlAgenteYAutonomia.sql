/*
	Archivo: 35_ControlAgenteYAutonomia.sql
	Objetivo: Dar a TI el control operativo del agente y dejar preparada, con valores seguros, la autonomía para solicitudes.
	Responsabilidad: Crear los parámetros que TI cambia sin desplegar (TI_Parametro), la política de autonomía por tipo de ticket y
		acción (TI_PoliticaAutonomia) y los atributos que esa política necesita; ligar cada aprobación al diagnóstico que la originó y
		darle vigencia; recuperar las investigaciones automáticas que un reinicio dejó pendientes; cerrar las ejecuciones que quedaron
		interrumpidas; y cerrar, si TI lo configura, los tickets que esperan demasiado la validación del usuario.
	Dependencias: Requiere 34_DominiosYMaquinaEstados.sql.
	Orden: Ejecutar después de 34_DominiosYMaquinaEstados.sql.
	Consideraciones: Los valores iniciales conservan el comportamiento actual: modo ASISTIDO, todas las acciones con decisión de TI
		(política APROBACION), aprobaciones sin vencimiento y sin autocierre. Nada se ejecuta sin humano hasta que un ADM libere una
		acción de una solicitud (SOL) que no exija aprobación, sea reversible y no supere el techo de riesgo, y cambie el modo a
		AUTONOMO; la base vuelve a comprobar esas condiciones antes de preparar la ejecución.
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

/* ============================================================================
   Parámetros operativos
   ============================================================================ */

If Object_Id('dbo.TI_Parametro', 'U') Is Null
Begin
	Create Table dbo.TI_Parametro (
		Parametro					varchar(50)		Not Null,
		-- Vacío (Null) significa "sin valor": por ejemplo, aprobaciones sin vencimiento.
		Valor						nvarchar(200)	Null,
		Descripcion					nvarchar(500)	Not Null,
		UltimoUsuario				varchar(20)		Null,
		UltimaFechaModif			datetime2(0)	Null,

		Constraint PK_TI_Parametro Primary Key (Parametro)
	)
End
Go

Insert dbo.TI_Parametro (Parametro, Valor, Descripcion, UltimoUsuario, UltimaFechaModif)
Select v.Parametro, v.Valor, v.Descripcion, Null, SysDateTime()
From (Values
	('AGENTE_MODO', N'ASISTIDO', N'Interruptor del agente. APAGADO: no investiga ni ejecuta. SOMBRA: investiga y propone, pero no ejecuta. ASISTIDO: ejecuta solo con la decisión de TI y, si la acción lo exige, con la aprobación de otro operador. AUTONOMO: además ejecuta sin humano las acciones que la política libera para solicitudes.'),
	('AGENTE_RIESGO_MAXIMO_AUTONOMO', N'MUY_BAJO', N'Techo de riesgo de la ejecución autónoma: una acción con un nivel mayor nunca se ejecuta sin humano.'),
	('AGENTE_CONFIANZA_MINIMA_PROPUESTA', N'60', N'Confianza diagnóstica mínima (1 a 100) para que el agente proponga una acción del catálogo.'),
	('APROBACION_VIGENCIA_HORAS', Null, N'Horas que dura una aprobación concedida para ejecutar la acción del agente (1 a 720). Vacío: sin vencimiento.'),
	('TICKET_AUTOCIERRE_PV_DIAS', Null, N'Días que un ticket puede esperar la validación del usuario antes de cerrarse como resuelto (1 a 90). Vacío: nunca se cierra solo.')
) as v (Parametro, Valor, Descripcion)
Where Not Exists (Select 1 From dbo.TI_Parametro as p Where p.Parametro = v.Parametro)
Go

/* ============================================================================
   Atributos de acciones, aprobaciones y ejecuciones
   ============================================================================ */

-- Una acción irreversible nunca puede ejecutarse sin humano. Todas nacen como no reversibles hasta que TI lo confirme.
If Col_Length('dbo.TI_Accion', 'Reversible') Is Null
	Alter Table dbo.TI_Accion Add Reversible bit Not Null Constraint DF_TI_Accion_Reversible Default (0)
Go

-- Esquema de los parámetros del ejecutor: el backend valida contra él lo que propone el modelo, igual que con las herramientas.
If Col_Length('dbo.TI_AgenteAccionEjecutor', 'ParametrosEsquemaJson') Is Null
	Alter Table dbo.TI_AgenteAccionEjecutor Add ParametrosEsquemaJson nvarchar(max) Null
Go
If Not Exists (Select 1 From sys.check_constraints Where name = 'CK_TI_AgenteAccionEjecutor_Esquema')
	Alter Table dbo.TI_AgenteAccionEjecutor Add Constraint CK_TI_AgenteAccionEjecutor_Esquema Check (ParametrosEsquemaJson Is Null or IsJson(ParametrosEsquemaJson) = 1)
Go
Update dbo.TI_AgenteAccionEjecutor
Set ParametrosEsquemaJson = N'{"type":"object","properties":{"usuario":{"type":"string","description":"Usuario solicitante del ticket.","minLength":2,"maxLength":20}},"required":["usuario"],"additionalProperties":false}'
Where AccionCodigo = 'ACC-007' and ParametrosEsquemaJson Is Null
Update dbo.TI_AgenteAccionEjecutor
Set ParametrosEsquemaJson = N'{"type":"object","properties":{"incidenciaNumero":{"type":"string","description":"Ticket bloqueado en PA; si se omite se usa el ticket investigado.","minLength":10,"maxLength":12}},"additionalProperties":false}'
Where AccionCodigo = 'ACC-004' and ParametrosEsquemaJson Is Null
Go

-- La aprobación queda ligada al diagnóstico que la justificó y, si TI lo configura, vence.
If Col_Length('dbo.TI_SolicitudAprobacion', 'DiagnosticoSecuencia') Is Null
	Alter Table dbo.TI_SolicitudAprobacion Add DiagnosticoSecuencia int Null, FechaExpiracion datetime2(0) Null
Go
If Not Exists (Select 1 From sys.foreign_keys Where name = 'FK_TI_SolicitudAprobacion_Diagnostico')
	Alter Table dbo.TI_SolicitudAprobacion Add Constraint FK_TI_SolicitudAprobacion_Diagnostico
		Foreign Key (IncidenciaNumero, DiagnosticoSecuencia) References dbo.TI_IncidenciaDiagnostico (IncidenciaNumero, Secuencia)
Go
If Not Exists (Select 1 From sys.check_constraints Where name = 'CK_TI_SolicitudAprobacion_Expiracion')
	Alter Table dbo.TI_SolicitudAprobacion Add Constraint CK_TI_SolicitudAprobacion_Expiracion
		Check (FechaExpiracion Is Null or (Estado = 'A' and FechaExpiracion > FechaRespuesta))
Go

-- T: ejecutada por decisión de TI. I: ejecutada por el agente sin humano, porque la política lo permitió.
If Col_Length('dbo.TI_EjecucionAccion', 'TipoEjecutor') Is Null
	Alter Table dbo.TI_EjecucionAccion Add TipoEjecutor char(1) Not Null Constraint DF_TI_EjecucionAccion_TipoEjecutor Default ('T')
Go
If Not Exists (Select 1 From sys.check_constraints Where name = 'CK_TI_EjecucionAccion_TipoEjecutor')
	Alter Table dbo.TI_EjecucionAccion Add Constraint CK_TI_EjecucionAccion_TipoEjecutor Check (TipoEjecutor In ('T','I'))
Go

If Not Exists (Select 1 From sys.check_constraints Where name = 'CK_TI_AgenteSesion_Decision')
	Alter Table dbo.TI_AgenteSesion Add Constraint CK_TI_AgenteSesion_Decision
		Check (Decision Is Null or Decision In ('GRABAR_INFORMACION','REALIZAR_CAMBIO','CANCELAR','EJECUCION_AUTONOMA'))
Go

/* ============================================================================
   Política de autonomía
   ============================================================================ */

If Object_Id('dbo.TI_PoliticaAutonomia', 'U') Is Null
Begin
	Create Table dbo.TI_PoliticaAutonomia (
		Tipo						char(3)			Not Null,
		AccionCodigo				varchar(50)		Not Null,
		-- AUTONOMA: el agente ejecuta sin humano si se cumplen todas las condiciones. APROBACION: decide TI. PROHIBIDA: nunca se ejecuta.
		Modo						varchar(20)		Not Null,
		ConfianzaMinima				decimal(5,2)	Null,
		Estado						varchar(2)		Not Null,
		UltimoUsuario				varchar(20)		Null,
		UltimaFechaModif			datetime2(0)	Null,

		Constraint PK_TI_PoliticaAutonomia Primary Key (Tipo, AccionCodigo),
		Constraint FK_TI_PoliticaAutonomia_Tipo Foreign Key (Tipo) References dbo.TI_Tipo (Tipo),
		Constraint FK_TI_PoliticaAutonomia_Accion Foreign Key (AccionCodigo) References dbo.TI_Accion (AccionCodigo),
		Constraint CK_TI_PoliticaAutonomia_Modo Check (Modo In ('AUTONOMA','APROBACION','PROHIBIDA')),
		-- Solo una solicitud puede liberarse, y con una confianza mínima no inferior al umbral con que el agente propone acciones.
		Constraint CK_TI_PoliticaAutonomia_Autonoma Check ((Modo = 'AUTONOMA' and Tipo = 'SOL' and ConfianzaMinima Between 60 and 100)
			or (Modo <> 'AUTONOMA' and ConfianzaMinima Is Null)),
		Constraint CK_TI_PoliticaAutonomia_Estado Check (Estado In ('A','I'))
	)
End
Go

-- Valor inicial: toda acción de ejecución requiere la decisión de TI en incidencias, solicitudes y requerimientos (comportamiento actual).
Insert dbo.TI_PoliticaAutonomia (Tipo, AccionCodigo, Modo, ConfianzaMinima, Estado, UltimoUsuario, UltimaFechaModif)
Select t.Tipo, a.AccionCodigo, 'APROBACION', Null, 'A', Null, SysDateTime()
From dbo.TI_Tipo as t
Cross Join dbo.TI_Accion as a
Where t.Tipo In ('INC','SOL','REQ') and a.Tipo = 'E'
	and Not Exists (Select 1 From dbo.TI_PoliticaAutonomia as p Where p.Tipo = t.Tipo and p.AccionCodigo = a.AccionCodigo)
Go

/* ============================================================================
   Consulta y administración (Configuración TI)
   ============================================================================ */

-- Usp_TI_Obtener_ParametrosAgente
Create Or Alter Procedure Usp_TI_Obtener_ParametrosAgente
/*================================================================================
Objetivo        : Entregar al backend los parámetros operativos vigentes del agente y de los tickets.
Creado Por      : Jhosep S. Lazo
Fecha Creación  : Oct 2026
SP Anterior     :
Comentario Cambios  : Uso interno del servidor (caché corta); no se expone en la API.
================================================================================*/

As
Begin
Set NoCount On

	Select Parametro, Valor From TI_Parametro

End
Go

/* Ejecuta Procedure */

-- Usp_TI_Obtener_ControlAgente 'TEC001', 'TIC'
Create Or Alter Procedure Usp_TI_Obtener_ControlAgente
/*================================================================================
Objetivo        : Mostrar a TI los parámetros del agente, la política de autonomía y el catálogo de acciones.
Creado Por      : Jhosep S. Lazo
Fecha Creación  : Oct 2026
SP Anterior     :
Comentario Cambios  : Lectura para cualquier operador TI; los cambios solo los hace un ADM.
================================================================================*/

@cUsuario	varchar(20),
@cArea		char(3)

As
Begin
Set NoCount On

	If Not Exists (Select 1 From TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM'))
		Throw 50613, 'El operador TI no se encuentra habilitado.', 1

	Select Parametro, Valor = IsNull(Valor, N''), Descripcion, UltimoUsuario = IsNull(UltimoUsuario, ''), UltimaFechaModif
	From TI_Parametro
	Order By Parametro

	-- Una acción nueva sin fila de política se comporta como APROBACION.
	Select tipo.Tipo, TipoDescripcion = tipo.Descripcion, accion.AccionCodigo, AccionNombre = accion.Nombre,
		Modo = IsNull(politica.Modo, 'APROBACION'), politica.ConfianzaMinima, Estado = IsNull(politica.Estado, 'A'),
		Configurada = Convert(bit, Case When politica.Tipo Is Null Then 0 Else 1 End)
	From TI_Tipo as tipo
	Cross Join TI_Accion as accion
	Left Join TI_PoliticaAutonomia as politica on politica.Tipo = tipo.Tipo and politica.AccionCodigo = accion.AccionCodigo
	Where tipo.Tipo In ('INC','SOL','REQ') and accion.Tipo = 'E'
	Order By tipo.Tipo, accion.AccionCodigo

	Select accion.AccionCodigo, accion.Nombre, accion.Descripcion, accion.Tipo, accion.NivelRiesgo, accion.RequiereAprobacion, accion.Reversible, accion.Estado,
		TieneEjecutor = Convert(bit, Case When ejecutor.AccionCodigo Is Null Then 0 Else 1 End),
		Procedimiento = IsNull(ejecutor.Procedimiento, ''), MaximoFilas = IsNull(ejecutor.MaximoFilas, 0), ParametrosDescripcion = IsNull(ejecutor.ParametrosDescripcion, N'')
	From TI_Accion as accion
	Left Join TI_AgenteAccionEjecutor as ejecutor on ejecutor.AccionCodigo = accion.AccionCodigo and ejecutor.Estado = 'A'
	Order By accion.Tipo, accion.AccionCodigo

End
Go

/* Ejecuta Procedure */

-- Usp_TI_Guardar_ParametroTI 'ADM001', 'TIC', 'AGENTE_MODO', 'SOMBRA', '00000000-0000-0000-0000-000000000001'
Create Or Alter Procedure Usp_TI_Guardar_ParametroTI
/*================================================================================
Objetivo        : Cambiar un parámetro operativo del agente o de los tickets, con auditoría del valor anterior.
Creado Por      : Jhosep S. Lazo
Fecha Creación  : Oct 2026
SP Anterior     :
Comentario Cambios  : Solo ADM. Bajar el techo de riesgo devuelve a APROBACION las acciones liberadas que lo superen.
================================================================================*/

@cUsuario		varchar(20),
@cArea			char(3),
@cParametro		varchar(50),
@cValor			nvarchar(200),
@cIdCorrelacion	uniqueidentifier

As
Begin
Set NoCount On
Set Xact_Abort On

	Declare @cAnterior nvarchar(200), @nTecho int, @dFecha datetime2(0) = SysDateTime()

	If Not Exists (Select 1 From TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil = 'ADM')
		Throw 50614, 'Solo un administrador puede cambiar los parámetros del agente.', 1
	Set @cValor = NullIf(LTrim(RTrim(@cValor)), N'')
	Select @cAnterior = Valor From TI_Parametro Where Parametro = @cParametro
	If @@RowCount = 0 Throw 50615, 'El parámetro indicado no existe.', 1

	If (@cParametro = 'AGENTE_MODO' and IsNull(@cValor, N'') Not In ('APAGADO','SOMBRA','ASISTIDO','AUTONOMO'))
		or (@cParametro = 'AGENTE_RIESGO_MAXIMO_AUTONOMO' and IsNull(@cValor, N'') Not In ('MUY_BAJO','BAJO','MEDIO','ALTO','MUY_ALTO'))
		or (@cParametro = 'AGENTE_CONFIANZA_MINIMA_PROPUESTA' and IsNull(Try_Convert(int, @cValor), 0) Not Between 1 and 100)
		or (@cParametro = 'APROBACION_VIGENCIA_HORAS' and @cValor Is Not Null and IsNull(Try_Convert(int, @cValor), 0) Not Between 1 and 720)
		or (@cParametro = 'TICKET_AUTOCIERRE_PV_DIAS' and @cValor Is Not Null and IsNull(Try_Convert(int, @cValor), 0) Not Between 1 and 90)
		Throw 50616, 'El valor no es válido para el parámetro indicado.', 1

	Begin Transaction

	Update TI_Parametro Set Valor = @cValor, UltimoUsuario = @cUsuario, UltimaFechaModif = @dFecha Where Parametro = @cParametro

	If @cParametro = 'AGENTE_RIESGO_MAXIMO_AUTONOMO'
	Begin
		Set @nTecho = Case @cValor When 'MUY_BAJO' Then 1 When 'BAJO' Then 2 When 'MEDIO' Then 3 When 'ALTO' Then 4 When 'MUY_ALTO' Then 5 End
		Update politica Set Modo = 'APROBACION', ConfianzaMinima = Null, UltimoUsuario = @cUsuario, UltimaFechaModif = @dFecha
		From TI_PoliticaAutonomia as politica
		Inner Join TI_Accion as accion on accion.AccionCodigo = politica.AccionCodigo
		Where politica.Modo = 'AUTONOMA'
			and Case accion.NivelRiesgo When 'MUY_BAJO' Then 1 When 'BAJO' Then 2 When 'MEDIO' Then 3 When 'ALTO' Then 4 Else 5 End > @nTecho
	End

	Insert TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
	Values (Null, @cUsuario, 'T', 'TI_Parametro', @cParametro, 'CAMBIAR_PARAMETRO', 'EXITOSO',
		(Select anterior = @cAnterior, nuevo = @cValor For Json Path, Without_Array_Wrapper), @cIdCorrelacion, @dFecha)

	Commit Transaction

End
Go

/* Ejecuta Procedure */

-- Usp_TI_Guardar_PoliticaAutonomia 'ADM001', 'TIC', 'SOL', 'ACC-004', 'APROBACION', Null, 'A', '00000000-0000-0000-0000-000000000001'
Create Or Alter Procedure Usp_TI_Guardar_PoliticaAutonomia
/*================================================================================
Objetivo        : Definir, por tipo de ticket y acción, si el agente puede ejecutar sin humano, requiere a TI o nunca ejecuta.
Creado Por      : Jhosep S. Lazo
Fecha Creación  : Oct 2026
SP Anterior     :
Comentario Cambios  : Solo ADM. AUTONOMA exige una solicitud (SOL) y una acción activa, con ejecutor y esquema de parámetros,
					  sin aprobación previa, reversible y bajo el techo de riesgo vigente.
================================================================================*/

@cUsuario			varchar(20),
@cArea				char(3),
@cTipo				char(3),
@cAccionCodigo		varchar(50),
@cModo				varchar(20),
@nConfianzaMinima	decimal(5,2),
@cEstado			varchar(2),
@cIdCorrelacion		uniqueidentifier

As
Begin
Set NoCount On
Set Xact_Abort On

	Declare @cAnterior varchar(20), @lRequiereAprobacion bit, @lReversible bit, @cNivelRiesgo varchar(20), @cEstadoAccion varchar(2), @cTecho nvarchar(200), @dFecha datetime2(0) = SysDateTime()

	If Not Exists (Select 1 From TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil = 'ADM')
		Throw 50617, 'Solo un administrador puede cambiar la política de autonomía.', 1
	If Not Exists (Select 1 From TI_Tipo Where Tipo = @cTipo) Throw 50618, 'El tipo de ticket no existe.', 1
	Select @lRequiereAprobacion = RequiereAprobacion, @lReversible = Reversible, @cNivelRiesgo = NivelRiesgo, @cEstadoAccion = Estado
	From TI_Accion Where AccionCodigo = @cAccionCodigo and Tipo = 'E'
	If @@RowCount = 0 Throw 50619, 'La acción no existe o no es una acción de ejecución.', 1
	If @cModo Not In ('AUTONOMA','APROBACION','PROHIBIDA') or @cEstado Not In ('A','I') Throw 50620, 'El modo o el estado de la política no son válidos.', 1

	If @cModo = 'AUTONOMA'
	Begin
		Select @cTecho = Valor From TI_Parametro Where Parametro = 'AGENTE_RIESGO_MAXIMO_AUTONOMO'
		If @cTipo <> 'SOL' Throw 50621, 'Solo las solicitudes (SOL) pueden ejecutarse sin humano; las incidencias y los requerimientos siempre pasan por TI.', 1
		If @cEstadoAccion <> 'A' or @lRequiereAprobacion = 1 or @lReversible = 0
			Throw 50622, 'Para liberar la acción debe estar activa, no requerir aprobación y ser reversible.', 1
		If Not Exists (Select 1 From TI_AgenteAccionEjecutor Where AccionCodigo = @cAccionCodigo and Estado = 'A' and ParametrosEsquemaJson Is Not Null)
			Throw 50623, 'La acción no tiene un ejecutor activo con esquema de parámetros.', 1
		If Case @cNivelRiesgo When 'MUY_BAJO' Then 1 When 'BAJO' Then 2 When 'MEDIO' Then 3 When 'ALTO' Then 4 Else 5 End
			> IsNull(Case @cTecho When 'MUY_BAJO' Then 1 When 'BAJO' Then 2 When 'MEDIO' Then 3 When 'ALTO' Then 4 When 'MUY_ALTO' Then 5 End, 0)
			Throw 50624, 'El riesgo de la acción supera el techo de riesgo autónomo configurado.', 1
		If @nConfianzaMinima Is Null or @nConfianzaMinima Not Between 60 and 100 Throw 50625, 'Indica la confianza mínima del diagnóstico (60 a 100).', 1
	End
	Else Set @nConfianzaMinima = Null

	Begin Transaction

	Select @cAnterior = Modo From TI_PoliticaAutonomia With (UpdLock, HoldLock) Where Tipo = @cTipo and AccionCodigo = @cAccionCodigo
	If @@RowCount = 0
		Insert TI_PoliticaAutonomia (Tipo, AccionCodigo, Modo, ConfianzaMinima, Estado, UltimoUsuario, UltimaFechaModif)
		Values (@cTipo, @cAccionCodigo, @cModo, @nConfianzaMinima, @cEstado, @cUsuario, @dFecha)
	Else
		Update TI_PoliticaAutonomia Set Modo = @cModo, ConfianzaMinima = @nConfianzaMinima, Estado = @cEstado, UltimoUsuario = @cUsuario, UltimaFechaModif = @dFecha
		Where Tipo = @cTipo and AccionCodigo = @cAccionCodigo

	Insert TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
	Values (Null, @cUsuario, 'T', 'TI_PoliticaAutonomia', Concat(@cTipo, '/', @cAccionCodigo), 'CAMBIAR_POLITICA_AUTONOMIA', 'EXITOSO',
		(Select anterior = IsNull(@cAnterior, 'APROBACION'), nuevo = @cModo, confianzaMinima = @nConfianzaMinima, estado = @cEstado For Json Path, Without_Array_Wrapper),
		@cIdCorrelacion, @dFecha)

	Commit Transaction

End
Go

/* Ejecuta Procedure */

-- Usp_TI_Guardar_AccionCatalogo 'ADM001', 'TIC', 'ACC-004', 'MEDIO', 1, 0, 'A', '00000000-0000-0000-0000-000000000001'
Create Or Alter Procedure Usp_TI_Guardar_AccionCatalogo
/*================================================================================
Objetivo        : Ajustar el riesgo, la exigencia de aprobación, la reversibilidad y la vigencia de una acción del catálogo.
Creado Por      : Jhosep S. Lazo
Fecha Creación  : Oct 2026
SP Anterior     :
Comentario Cambios  : Solo ADM. Si la acción deja de cumplir las condiciones de autonomía, su política vuelve a APROBACION.
================================================================================*/

@cUsuario				varchar(20),
@cArea					char(3),
@cAccionCodigo			varchar(50),
@cNivelRiesgo			varchar(20),
@lRequiereAprobacion	bit,
@lReversible			bit,
@cEstado				varchar(2),
@cIdCorrelacion			uniqueidentifier

As
Begin
Set NoCount On
Set Xact_Abort On

	Declare @cAnterior nvarchar(400), @cTecho nvarchar(200), @dFecha datetime2(0) = SysDateTime()

	If Not Exists (Select 1 From TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil = 'ADM')
		Throw 50626, 'Solo un administrador puede modificar el catálogo de acciones.', 1
	If @cNivelRiesgo Not In ('MUY_BAJO','BAJO','MEDIO','ALTO','MUY_ALTO') or @cEstado Not In ('A','I') Throw 50627, 'El nivel de riesgo o el estado no son válidos.', 1
	Set @cAnterior = (Select nivelRiesgo = NivelRiesgo, requiereAprobacion = RequiereAprobacion, reversible = Reversible, estado = Estado
		From TI_Accion Where AccionCodigo = @cAccionCodigo For Json Path, Without_Array_Wrapper)
	If @cAnterior Is Null Throw 50628, 'La acción no existe.', 1
	Select @cTecho = Valor From TI_Parametro Where Parametro = 'AGENTE_RIESGO_MAXIMO_AUTONOMO'

	Begin Transaction

	Update TI_Accion
	Set NivelRiesgo = @cNivelRiesgo, RequiereAprobacion = @lRequiereAprobacion, Reversible = @lReversible, Estado = @cEstado,
		UltimoUsuario = @cUsuario, UltimaFechaModif = @dFecha
	Where AccionCodigo = @cAccionCodigo

	If @lRequiereAprobacion = 1 or @lReversible = 0 or @cEstado <> 'A'
		or Case @cNivelRiesgo When 'MUY_BAJO' Then 1 When 'BAJO' Then 2 When 'MEDIO' Then 3 When 'ALTO' Then 4 Else 5 End
			> IsNull(Case @cTecho When 'MUY_BAJO' Then 1 When 'BAJO' Then 2 When 'MEDIO' Then 3 When 'ALTO' Then 4 When 'MUY_ALTO' Then 5 End, 0)
		Update TI_PoliticaAutonomia Set Modo = 'APROBACION', ConfianzaMinima = Null, UltimoUsuario = @cUsuario, UltimaFechaModif = @dFecha
		Where AccionCodigo = @cAccionCodigo and Modo = 'AUTONOMA'

	Insert TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
	Values (Null, @cUsuario, 'T', 'TI_Accion', @cAccionCodigo, 'CAMBIAR_ACCION_CATALOGO', 'EXITOSO',
		(Select anterior = Json_Query(@cAnterior), nivelRiesgo = @cNivelRiesgo, requiereAprobacion = @lRequiereAprobacion, reversible = @lReversible, estado = @cEstado
			For Json Path, Without_Array_Wrapper), @cIdCorrelacion, @dFecha)

	Commit Transaction

End
Go

/* Ejecuta Procedure */

/* ============================================================================
   Decisión y ejecución
   ============================================================================ */

-- Usp_TI_Agente_DatosAutonomia 1
Create Or Alter Procedure Usp_TI_Agente_DatosAutonomia
/*================================================================================
Objetivo        : Reunir lo que el backend necesita para evaluar si una acción propuesta puede ejecutarse sin humano.
Creado Por      : Jhosep S. Lazo
Fecha Creación  : Oct 2026
SP Anterior     :
Comentario Cambios  : Uso interno del servidor. La evaluación la hace PoliticaAutonomia; PrepararCambio vuelve a comprobarla.
================================================================================*/

@nSesionNumero	bigint

As
Begin
Set NoCount On

	Select sesion.SesionNumero, sesion.UsuarioTI, sesion.AreaTI, EstadoSesion = sesion.Estado, sesion.Confianza, sesion.AccionCodigo,
		ParametrosJson = IsNull(sesion.ParametrosJson, N'{}'), SolicitudPendiente = Convert(bit, Case When sesion.SolicitudAprobacionSecuencia Is Null Then 0 Else 1 End),
		IncidenciaNumero = IsNull(ticket.IncidenciaNumero, ''), TipoTicket = IsNull(ticket.Tipo, ''), SubTipo = IsNull(ticket.SubTipo, ''), EstadoTicket = IsNull(ticket.Estado, ''),
		PerfilSolicitante = IsNull(solicitante.Perfil, ''),
		AccionTipo = IsNull(accion.Tipo, ''), AccionEstado = IsNull(accion.Estado, ''), RequiereAprobacion = IsNull(accion.RequiereAprobacion, 1),
		Reversible = IsNull(accion.Reversible, 0), NivelRiesgo = IsNull(accion.NivelRiesgo, ''),
		TieneEjecutor = Convert(bit, Case When ejecutor.AccionCodigo Is Null Then 0 Else 1 End), ParametrosEsquemaJson = IsNull(ejecutor.ParametrosEsquemaJson, N''),
		ModoPolitica = IsNull(politica.Modo, 'APROBACION'), politica.ConfianzaMinima, EstadoPolitica = IsNull(politica.Estado, 'A'),
		ModoAgente = IsNull((Select Valor From TI_Parametro Where Parametro = 'AGENTE_MODO'), N'ASISTIDO'),
		RiesgoMaximo = IsNull((Select Valor From TI_Parametro Where Parametro = 'AGENTE_RIESGO_MAXIMO_AUTONOMO'), N'')
	From TI_AgenteSesion as sesion
	Left Join TI_Incidencia as ticket on ticket.IncidenciaNumero = sesion.IncidenciaNumero
	Left Join TI_Usuario as solicitante on solicitante.Usuario = ticket.UsuarioSolicitante
	Left Join TI_Accion as accion on accion.AccionCodigo = sesion.AccionCodigo
	Left Join TI_AgenteAccionEjecutor as ejecutor on ejecutor.AccionCodigo = sesion.AccionCodigo and ejecutor.Estado = 'A'
	Left Join TI_PoliticaAutonomia as politica on politica.Tipo = ticket.Tipo and politica.AccionCodigo = sesion.AccionCodigo
	Where sesion.SesionNumero = @nSesionNumero

End
Go

/* Ejecuta Procedure */

Create Or Alter Procedure dbo.Usp_TI_Agente_PrepararCambio
/*================================================================================
Objetivo            : Validar la decisión REALIZAR CAMBIO, crear aprobación cuando corresponda y preparar únicamente un ejecutor catalogado.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 01/10/2026
SP Anterior         : dbo.Usp_TI_Agente_PrepararCambio (27_AgenteFase1Integracion.sql)
Comentario Cambios  : 02/10/2026 La aprobación del agente se comporta igual que la manual: bloquea el ticket en PA, registra historial,
					  notifica a los aprobadores y no se superpone con otra aprobación pendiente ni con un ticket en validación.
					  07/10/2026 Respeta el interruptor del agente (APAGADO y SOMBRA no ejecutan) y la política PROHIBIDA; liga la
					  aprobación al diagnóstico; una aprobación vencida se solicita de nuevo; entrega el esquema de parámetros del
					  ejecutor; con @lAutonoma = 1 prepara la ejecución sin humano solo si la base confirma todas las condiciones.
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3),
	@nSesionNumero bigint,
	@cClaveIdempotencia uniqueidentifier,
	@cIdCorrelacion uniqueidentifier,
	@lAutonoma bit = 0
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Declare @cIncidenciaNumero varchar(12), @cAccionCodigo varchar(50), @cAccionNombre nvarchar(100), @cParametrosJson nvarchar(max), @lRequiereAprobacion bit,
		@lReversible bit, @cNivelRiesgo varchar(20), @nSolicitudSecuencia int, @cEstadoSolicitud char(1), @dExpiracion datetime2(0), @cProcedimiento varchar(200),
		@nMaximoFilas int, @cEsquema nvarchar(max), @nEjecucionSecuencia int, @nEstado int, @nDiagnosticoSecuencia int, @nConfianza decimal(5,2),
		@cEstadoSesion varchar(30), @cTipoTicket char(3), @cSubTipo char(3), @cModoPolitica varchar(20), @nConfianzaMinima decimal(5,2),
		@cModoAgente nvarchar(200), @cTecho nvarchar(200),
		@cSesionCodigo varchar(12) = Concat('AGT-', Right(Concat('000000', @nSesionNumero), 6)), @dFecha datetime2(0) = SysDateTime()

	If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM')) Throw 50517, 'El operador TI no se encuentra habilitado.', 1

	Select @cModoAgente = Valor From dbo.TI_Parametro Where Parametro = 'AGENTE_MODO'
	Select @cTecho = Valor From dbo.TI_Parametro Where Parametro = 'AGENTE_RIESGO_MAXIMO_AUTONOMO'
	If IsNull(@cModoAgente, N'ASISTIDO') In (N'APAGADO', N'SOMBRA')
		Throw 50601, 'El agente está en modo APAGADO o SOMBRA: TI deshabilitó la ejecución de cambios.', 1
	If @lAutonoma = 1 and IsNull(@cModoAgente, N'') <> N'AUTONOMO' Throw 50602, 'El agente no está en modo AUTONOMO.', 1

	Begin Try
		Begin Transaction

		Select @cIncidenciaNumero = IncidenciaNumero, @cAccionCodigo = AccionCodigo, @cParametrosJson = ParametrosJson, @nSolicitudSecuencia = SolicitudAprobacionSecuencia,
			@nDiagnosticoSecuencia = DiagnosticoSecuencia, @nConfianza = Confianza, @cEstadoSesion = Estado
		From dbo.TI_AgenteSesion With (UpdLock, HoldLock)
		Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario and AreaTI = @cArea and IdCorrelacion = @cIdCorrelacion
			and Estado In ('PENDIENTE_TI','PENDIENTE_APROBACION','LISTO_EJECUCION','SIN_EJECUTOR')
		If @@RowCount = 0 Throw 50523, 'La investigación ya fue procesada o no admite esta ejecución.', 1
		If @cAccionCodigo Is Null Throw 50518, 'El diagnóstico no contiene una acción correctiva catalogada.', 1
		If @cIncidenciaNumero Is Null Throw 50519, 'Para ejecutar un cambio la investigación debe estar asociada a una incidencia.', 1
		If Not Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero and Estado Not In ('RS','CA','CF','NP')) Throw 50530, 'La incidencia ya está finalizada.', 1
		If Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero and Estado = 'PV') Throw 50538, 'El ticket espera la validación del usuario; no admite una nueva acción correctiva.', 1
		If Exists (Select 1 From dbo.TI_SolicitudAprobacion With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero and Estado = 'P' and Secuencia <> IsNull(@nSolicitudSecuencia, -1))
			Throw 50537, 'El ticket tiene otra aprobación pendiente. Resuélvela en Gestión de Tickets antes de continuar.', 1

		Select @cTipoTicket = Tipo, @cSubTipo = SubTipo From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero
		Select @cModoPolitica = Modo, @nConfianzaMinima = ConfianzaMinima From dbo.TI_PoliticaAutonomia Where Tipo = @cTipoTicket and AccionCodigo = @cAccionCodigo and Estado = 'A'
		If @cModoPolitica = 'PROHIBIDA' Throw 50603, 'La política de TI prohíbe ejecutar esta acción en este tipo de ticket.', 1

		Select @lRequiereAprobacion = RequiereAprobacion, @cAccionNombre = Nombre, @lReversible = Reversible, @cNivelRiesgo = NivelRiesgo
		From dbo.TI_Accion With (HoldLock) Where AccionCodigo = @cAccionCodigo and Tipo = 'E' and Estado = 'A'
		If @lRequiereAprobacion Is Null Throw 50520, 'La acción propuesta no está habilitada para ejecución.', 1

		-- La ejecución sin humano vuelve a comprobar aquí cada condición de la política: el backend no puede saltarse ninguna.
		If @lAutonoma = 1
		Begin
			If @cEstadoSesion <> 'PENDIENTE_TI' or @nSolicitudSecuencia Is Not Null Throw 50604, 'La investigación ya tiene una decisión en curso.', 1
			If @cTipoTicket <> 'SOL' or @cSubTipo Is Null Throw 50605, 'Solo una solicitud (SOL) clasificada por TI puede ejecutarse sin humano.', 1
			If IsNull(@cModoPolitica, '') <> 'AUTONOMA' Throw 50606, 'La política de TI no libera esta acción para ejecución autónoma.', 1
			If @lRequiereAprobacion = 1 or @lReversible = 0 Throw 50607, 'La acción requiere aprobación o no es reversible: no puede ejecutarse sin humano.', 1
			If Case @cNivelRiesgo When 'MUY_BAJO' Then 1 When 'BAJO' Then 2 When 'MEDIO' Then 3 When 'ALTO' Then 4 Else 5 End
				> IsNull(Case @cTecho When 'MUY_BAJO' Then 1 When 'BAJO' Then 2 When 'MEDIO' Then 3 When 'ALTO' Then 4 When 'MUY_ALTO' Then 5 End, 0)
				Throw 50608, 'El riesgo de la acción supera el techo de riesgo autónomo.', 1
			If IsNull(@nConfianza, 0) < IsNull(@nConfianzaMinima, 101) Throw 50609, 'La confianza del diagnóstico no alcanza el mínimo de la política.', 1
			If Not Exists (Select 1 From dbo.TI_Incidencia as ticket Inner Join dbo.TI_Usuario as solicitante on solicitante.Usuario = ticket.UsuarioSolicitante
				Where ticket.IncidenciaNumero = @cIncidenciaNumero and solicitante.Perfil = 'USR')
				Throw 50610, 'El solicitante no es elegible para una ejecución autónoma.', 1
		End

		If @lRequiereAprobacion = 1
		Begin
			-- Una aprobación concedida que ya venció deja de valer: se solicita de nuevo.
			If @nSolicitudSecuencia Is Not Null
			Begin
				Select @cEstadoSolicitud = Estado, @dExpiracion = FechaExpiracion From dbo.TI_SolicitudAprobacion With (UpdLock, HoldLock)
				Where IncidenciaNumero = @cIncidenciaNumero and Secuencia = @nSolicitudSecuencia and AccionCodigo = @cAccionCodigo
					and IsNull(ParametrosJson, '{}') = IsNull(@cParametrosJson, '{}')
				If @cEstadoSolicitud = 'A' and @dExpiracion < @dFecha Set @nSolicitudSecuencia = Null
			End

			If @nSolicitudSecuencia Is Null
			Begin
				Select @nSolicitudSecuencia = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_SolicitudAprobacion With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero
				Insert dbo.TI_SolicitudAprobacion (IncidenciaNumero, Secuencia, AccionCodigo, UsuarioSolicitante, UsuarioAprobador, ParametrosJson, Estado, Justificacion, ComentarioRespuesta, FechaSolicitud, FechaRespuesta, DiagnosticoSecuencia)
				Select @cIncidenciaNumero, @nSolicitudSecuencia, @cAccionCodigo, @cUsuario, Null, @cParametrosJson, 'P',
					Left(Concat(@cSesionCodigo, N' · ', Coalesce(NullIf(CausaProbable, ''), NullIf(SolucionPropuesta, ''), N'Acción propuesta por investigación autónoma.')), 1000), Null, @dFecha, Null,
					Case When Exists (Select 1 From dbo.TI_IncidenciaDiagnostico Where IncidenciaNumero = @cIncidenciaNumero and Secuencia = @nDiagnosticoSecuencia) Then @nDiagnosticoSecuencia End
				From dbo.TI_AgenteSesion Where SesionNumero = @nSesionNumero

				Update dbo.TI_Incidencia Set Estado = 'PA', UltimoUsuario = @cUsuario, UltimaFechaModif = @dFecha Where IncidenciaNumero = @cIncidenciaNumero and Estado <> 'PA'

				Select @nEstado = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_IncidenciaEstado With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero
				Insert dbo.TI_IncidenciaEstado (IncidenciaNumero, Secuencia, Estado, UsuarioCambio, FechaCambio, Observacion)
				Values (@cIncidenciaNumero, @nEstado, 'PA', @cUsuario, @dFecha, Concat(N'La acción ', @cAccionCodigo, N' propuesta por el Agente de Ingeniería (', @cSesionCodigo, N') espera aprobación; la atención queda bloqueada.'))

				Insert dbo.TI_Notificacion (Usuario, IncidenciaNumero, Tipo, Titulo, Mensaje, Ruta, Fecha)
				Select u.Usuario, @cIncidenciaNumero, 'APROBACION', N'Aprobación pendiente del agente',
					Left(Concat(N'El Agente de Ingeniería solicita aprobar ', @cAccionCodigo, N' · ', @cAccionNombre, N' en el ticket ', @cIncidenciaNumero, N'.'), 500), '/gestion-tickets', @dFecha
				From dbo.TI_Usuario as u
				Where u.Estado = 'A' and u.Perfil In ('TEC','SUP','ADM') and u.Usuario <> @cUsuario

				Update dbo.TI_AgenteSesion
				Set Estado = 'PENDIENTE_APROBACION', Decision = 'REALIZAR_CAMBIO', UsuarioDecision = @cUsuario, FechaDecision = @dFecha, SolicitudAprobacionSecuencia = @nSolicitudSecuencia
				Where SesionNumero = @nSesionNumero

				Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
				Values (@cIncidenciaNumero, @cUsuario, 'T', 'TI_SolicitudAprobacion', Concat(@cIncidenciaNumero, '/', @nSolicitudSecuencia), 'SOLICITUD_CAMBIO_AGENTE', 'PENDIENTE',
					(Select accionCodigo = @cAccionCodigo, diagnosticoSecuencia = @nDiagnosticoSecuencia For Json Path, Without_Array_Wrapper), @cIdCorrelacion, @dFecha)

				Commit Transaction
				Select Estado = 'PENDIENTE_APROBACION', Mensaje = 'La acción requiere aprobación de otro operador TI. El ticket quedó bloqueado y los aprobadores fueron notificados.', PuedeEjecutar = Convert(bit, 0), ProcedimientoEjecutor = Convert(varchar(200), ''), EjecucionSecuencia = Convert(int, Null), SolicitudAprobacionSecuencia = @nSolicitudSecuencia, ParametrosJson = IsNull(@cParametrosJson, '{}')
				Return
			End

			If @cEstadoSolicitud = 'P'
			Begin
				Commit Transaction
				Select Estado = 'PENDIENTE_APROBACION', Mensaje = 'La acción todavía espera aprobación de otro operador TI.', PuedeEjecutar = Convert(bit, 0), ProcedimientoEjecutor = Convert(varchar(200), ''), EjecucionSecuencia = Convert(int, Null), SolicitudAprobacionSecuencia = @nSolicitudSecuencia, ParametrosJson = IsNull(@cParametrosJson, '{}')
				Return
			End
			If @cEstadoSolicitud = 'R' Throw 50539, 'La acción fue rechazada. Graba la información o inicia una nueva investigación.', 1
			If IsNull(@cEstadoSolicitud, '') <> 'A' Throw 50521, 'No existe una aprobación válida para esta acción y sus parámetros.', 1
		End

		Select @cProcedimiento = Procedimiento, @nMaximoFilas = MaximoFilas, @cEsquema = ParametrosEsquemaJson From dbo.TI_AgenteAccionEjecutor With (HoldLock) Where AccionCodigo = @cAccionCodigo and Estado = 'A'
		If @cProcedimiento Is Null
		Begin
			If @lAutonoma = 1 Throw 50611, 'La acción no tiene un ejecutor autorizado; no puede ejecutarse sin humano.', 1
			Update dbo.TI_AgenteSesion Set Estado = 'SIN_EJECUTOR' Where SesionNumero = @nSesionNumero
			Commit Transaction
			Select Estado = 'SIN_EJECUTOR', Mensaje = 'La acción está aprobada, pero todavía no tiene un procedimiento ejecutor autorizado. El agente no realizará cambios hasta que TI configure ese contrato.', PuedeEjecutar = Convert(bit, 0), ProcedimientoEjecutor = Convert(varchar(200), ''), EjecucionSecuencia = Convert(int, Null), SolicitudAprobacionSecuencia = @nSolicitudSecuencia, ParametrosJson = IsNull(@cParametrosJson, '{}')
			Return
		End

		If @cProcedimiento Not Like 'dbo.Usp_TI_AgenteAccion[_]%' Throw 50522, 'El procedimiento ejecutor no cumple el contrato autorizado.', 1
		If @lAutonoma = 1 and @cEsquema Is Null Throw 50611, 'El ejecutor no declara el esquema de sus parámetros; no puede ejecutarse sin humano.', 1
		If Exists (Select 1 From dbo.TI_EjecucionAccion Where ClaveIdempotencia = @cClaveIdempotencia) Throw 50523, 'La solicitud de ejecución ya fue procesada.', 1

		Select @nEjecucionSecuencia = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_EjecucionAccion With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero
		Insert dbo.TI_EjecucionAccion (IncidenciaNumero, Secuencia, AccionCodigo, SolicitudSecuencia, UsuarioEjecutor, ClaveIdempotencia, ParametrosJson, ResultadoJson, Estado, FilasAfectadas, FechaInicio, FechaFin, Error, TipoEjecutor)
		Values (@cIncidenciaNumero, @nEjecucionSecuencia, @cAccionCodigo, @nSolicitudSecuencia, @cUsuario, @cClaveIdempotencia, @cParametrosJson, Null, 'PR', Null, @dFecha, Null, Null,
			Case When @lAutonoma = 1 Then 'I' Else 'T' End)

		Update dbo.TI_AgenteSesion
		Set Estado = 'EJECUTANDO', Decision = Case When @lAutonoma = 1 Then 'EJECUCION_AUTONOMA' Else 'REALIZAR_CAMBIO' End,
			UsuarioDecision = Case When @lAutonoma = 1 Then Null Else @cUsuario End, FechaDecision = @dFecha, EjecucionSecuencia = @nEjecucionSecuencia
		Where SesionNumero = @nSesionNumero

		If @lAutonoma = 1
			Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
			Values (@cIncidenciaNumero, Null, 'I', 'TI_EjecucionAccion', Concat(@cIncidenciaNumero, '/', @nEjecucionSecuencia), 'EJECUCION_AUTONOMA_PREPARADA', 'EN_CURSO',
				(Select accionCodigo = @cAccionCodigo, confianza = @nConfianza, confianzaMinima = @nConfianzaMinima, nivelRiesgo = @cNivelRiesgo, techo = @cTecho, responsable = @cUsuario
					For Json Path, Without_Array_Wrapper), @cIdCorrelacion, @dFecha)

		Commit Transaction
		Select Estado = 'LISTO_EJECUCION', Mensaje = 'La acción superó permisos, aprobación y catálogo de ejecutores.', PuedeEjecutar = Convert(bit, 1), ProcedimientoEjecutor = @cProcedimiento, EjecucionSecuencia = @nEjecucionSecuencia, SolicitudAprobacionSecuencia = @nSolicitudSecuencia, ParametrosJson = IsNull(@cParametrosJson, '{}'), MaximoFilas = @nMaximoFilas,
			ParametrosEsquemaJson = IsNull(@cEsquema, N'')
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go

Create Or Alter Procedure dbo.Usp_TI_Responder_AprobacionTicket
/*================================================================================
Objetivo            : Aprobar o rechazar una solicitud pendiente y desbloquear el flujo operativo.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : dbo.Usp_TI_Responder_AprobacionTicket (28_AgenteFase2Integracion.sql)
Comentario Cambios  : 02/10/2026 Tampoco puede aprobar el operador que hoy es responsable de la investigación del agente que originó la solicitud
					  (evita eludir la segregación reasignando la investigación).
					  07/10/2026 Si TI configuró APROBACION_VIGENCIA_HORAS, la aprobación concedida vence en ese plazo.
================================================================================*/
	@cUsuario varchar(20), @cArea char(3), @cIncidenciaNumero varchar(12), @nSecuencia int, @lAprobar bit, @cComentario nvarchar(1000), @cIdCorrelacion uniqueidentifier
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Begin Try
		Begin Transaction
		Declare @nEstado int, @cSolicitante varchar(20), @cMensajeNotificacion nvarchar(500), @cRuta varchar(250) = '/gestion-tickets', @nSesionAgente bigint, @cResponsableAgente varchar(20),
			@nVigenciaHoras int, @dFecha datetime2(0) = SysDateTime()

		If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Estado = 'A' and Perfil In ('TEC','SUP','ADM')) Throw 50230, 'Solo un operador TI activo puede responder esta aprobación.', 1
		Select @cSolicitante = UsuarioSolicitante From dbo.TI_SolicitudAprobacion With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero and Secuencia = @nSecuencia and Estado = 'P'
		If @cSolicitante Is Null Throw 50231, 'La solicitud de aprobación ya no se encuentra pendiente.', 1
		If @cSolicitante = @cUsuario Throw 50242, 'No puedes responder una aprobación que tú mismo solicitaste. Debe resolverla otro operador TI.', 1

		Select @nSesionAgente = SesionNumero, @cResponsableAgente = UsuarioTI From dbo.TI_AgenteSesion
		Where IncidenciaNumero = @cIncidenciaNumero and SolicitudAprobacionSecuencia = @nSecuencia and Estado = 'PENDIENTE_APROBACION'
		If @cResponsableAgente = @cUsuario Throw 50243, 'Eres el responsable actual de la investigación que propone esta acción; debe aprobarla otro operador TI.', 1
		If @lAprobar = 0 and NullIf(LTrim(RTrim(@cComentario)), '') Is Null Throw 50232, 'Indica el motivo del rechazo.', 1

		Set @nVigenciaHoras = Try_Convert(int, (Select Valor From dbo.TI_Parametro Where Parametro = 'APROBACION_VIGENCIA_HORAS'))

		Update dbo.TI_SolicitudAprobacion
		Set Estado = Case When @lAprobar = 1 Then 'A' Else 'R' End, UsuarioAprobador = @cUsuario,
			ComentarioRespuesta = NullIf(LTrim(RTrim(@cComentario)), ''), FechaRespuesta = @dFecha,
			FechaExpiracion = Case When @lAprobar = 1 and @nVigenciaHoras > 0 Then DateAdd(hour, @nVigenciaHoras, @dFecha) End
		Where IncidenciaNumero = @cIncidenciaNumero and Secuencia = @nSecuencia and Estado = 'P'

		Update dbo.TI_Incidencia Set Estado = 'DG', UltimoUsuario = @cUsuario, UltimaFechaModif = @dFecha Where IncidenciaNumero = @cIncidenciaNumero and Estado = 'PA'
		Select @nEstado = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_IncidenciaEstado With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero
		Insert dbo.TI_IncidenciaEstado (IncidenciaNumero, Secuencia, Estado, UsuarioCambio, FechaCambio, Observacion)
		Values (@cIncidenciaNumero, @nEstado, 'DG', @cUsuario, @dFecha, Case When @lAprobar = 1 Then N'La aprobación fue concedida; el ticket puede continuar.' Else N'La aprobación fue rechazada; el ticket vuelve a diagnóstico.' End)

		If @nSesionAgente Is Not Null Set @cRuta = Concat('/asistente-ti?sesion=', @nSesionAgente)

		Set @cMensajeNotificacion = Case
			When @nSesionAgente Is Not Null and @lAprobar = 1 and @nVigenciaHoras > 0 Then Concat(N'La acción del agente fue aprobada y vence en ', @nVigenciaHoras, N' hora(s). Abre la investigación para ejecutarla.')
			When @nSesionAgente Is Not Null and @lAprobar = 1 Then N'La acción del agente fue aprobada. Abre la investigación para ejecutarla.'
			When @lAprobar = 1 Then N'La solicitud fue aprobada y el ticket puede continuar.'
			Else N'La solicitud fue rechazada. Revisa el comentario registrado.' End
		-- Si la investigación fue reasignada, el aviso llega a su responsable actual.
		Exec dbo.Usp_TI_Registrar_Notificacion
			@cUsuario = @cSolicitante,
			@cIncidenciaNumero = @cIncidenciaNumero,
			@cTipo = 'APROBACION_RESPUESTA',
			@cTitulo = N'Respuesta de aprobación',
			@cMensaje = @cMensajeNotificacion,
			@cRuta = @cRuta
		If @cResponsableAgente Is Not Null and @cResponsableAgente <> @cSolicitante
			Exec dbo.Usp_TI_Registrar_Notificacion
				@cUsuario = @cResponsableAgente,
				@cIncidenciaNumero = @cIncidenciaNumero,
				@cTipo = 'APROBACION_RESPUESTA',
				@cTitulo = N'Respuesta de aprobación',
				@cMensaje = @cMensajeNotificacion,
				@cRuta = @cRuta

		Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
		Values (@cIncidenciaNumero, @cUsuario, 'T', 'TI_SolicitudAprobacion', Concat(@cIncidenciaNumero, '-', @nSecuencia), Case When @lAprobar = 1 Then 'APROBAR_ACCION' Else 'RECHAZAR_ACCION' End, 'EXITOSO',
			Case When @lAprobar = 1 and @nVigenciaHoras > 0 Then (Select vigenciaHoras = @nVigenciaHoras For Json Path, Without_Array_Wrapper) End, @cIdCorrelacion, @dFecha)

		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go

/* ============================================================================
   Investigación automática que sobrevive a un reinicio de la API
   ============================================================================ */

-- Usp_TI_Agente_RegistrarEventoServidor 1, 'INVESTIGACION_AUTOMATICA_FIN', N'Investigación automática completada.', N'{"exito":true}'
Create Or Alter Procedure Usp_TI_Agente_RegistrarEventoServidor
/*================================================================================
Objetivo        : Registrar en la investigación un evento que solo produce el servidor (cola automática, llamadas al modelo, autonomía).
Creado Por      : Jhosep S. Lazo
Fecha Creación  : Oct 2026
SP Anterior     :
Comentario Cambios  : Uso interno del servidor (OrigenServidor = 1, fuente BACKEND); el navegador no puede registrarlos.
================================================================================*/

@nSesionNumero	bigint,
@cTipo			varchar(40),
@cContenido		nvarchar(2000),
@cDatosJson		nvarchar(max)

As
Begin
Set NoCount On
Set Xact_Abort On

	Declare @nSecuencia int

	If @cTipo Not In ('INVESTIGACION_AUTOMATICA','INVESTIGACION_AUTOMATICA_FIN','LLAMADA_MODELO','ACCION_RECHAZADA','EVALUACION_AUTONOMIA','EJECUCION_AUTONOMA')
		Throw 50629, 'El tipo de evento del servidor no es válido.', 1
	If @cDatosJson Is Not Null and IsJson(@cDatosJson) = 0 Throw 50630, 'Los datos del evento no tienen formato JSON válido.', 1

	Begin Transaction
	If Not Exists (Select 1 From TI_AgenteSesion With (UpdLock, HoldLock) Where SesionNumero = @nSesionNumero) Throw 50631, 'La investigación no existe.', 1
	Select @nSecuencia = IsNull(Max(Secuencia), 0) + 1 From TI_AgenteEvento With (UpdLock, HoldLock) Where SesionNumero = @nSesionNumero
	Insert TI_AgenteEvento (SesionNumero, Secuencia, Tipo, Fuente, Contenido, DatosJson, Fecha, OrigenServidor)
	Values (@nSesionNumero, @nSecuencia, @cTipo, 'BACKEND', Left(@cContenido, 2000), @cDatosJson, SysDateTime(), 1)
	Commit Transaction

End
Go

/* Ejecuta Procedure */

-- Usp_TI_Agente_PrepararInvestigacionAutomatica 'INC-000006', N'{"pasos":["Abrí la pantalla"],"mensajeError":"Error"}', 'TEC001', '00000000-0000-0000-0000-000000000001'
Create Or Alter Procedure Usp_TI_Agente_PrepararInvestigacionAutomatica
/*================================================================================
Objetivo        : Crear, en una sola transacción, la investigación de un ticket registrado con la evidencia mostrada al Asistente TI.
Creado Por      : Jhosep S. Lazo
Fecha Creación  : Oct 2026
SP Anterior     :
Comentario Cambios  : Reemplaza la cola en memoria como fuente de verdad: la evidencia queda guardada aunque la API se reinicie,
					  y la marca INVESTIGACION_AUTOMATICA permite retomar la investigación pendiente.
================================================================================*/

@cIncidenciaNumero	varchar(12),
@cEvidenciaJson		nvarchar(max),
@cUsuarioPreferido	varchar(20),
@cIdCorrelacion		uniqueidentifier

As
Begin
Set NoCount On
Set Xact_Abort On

	Declare @cUsuario varchar(20), @cArea char(3), @nSesionNumero bigint, @dFecha datetime2(0) = SysDateTime()

	If Not Exists (Select 1 From TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero) Throw 50632, 'La incidencia indicada no existe.', 1
	If IsNull(IsJson(@cEvidenciaJson), 0) <> 1 Throw 50585, 'La evidencia del colaborador no tiene formato JSON válido.', 1

	-- La regla de quién responde por la investigación vive en un solo procedimiento (responsable del ticket, operador configurado,
	-- supervisor del área de la línea).
	Declare @tOperador Table (Usuario varchar(20), Area char(3))
	Insert @tOperador (Usuario, Area)
	Exec dbo.Usp_TI_Agente_ResolverOperadorAutomatico @cIncidenciaNumero = @cIncidenciaNumero, @cUsuarioPreferido = @cUsuarioPreferido
	Select Top (1) @cUsuario = Usuario, @cArea = Area From @tOperador
	If @cUsuario Is Null Throw 50633, 'No hay un operador TI activo para la investigación automática.', 1

	Begin Transaction

	Insert TI_AgenteSesion (IncidenciaNumero, UsuarioTI, AreaTI, IdCorrelacion, DescripcionInicial, Estado, FechaInicio)
	Values (@cIncidenciaNumero, @cUsuario, @cArea, @cIdCorrelacion,
		Concat(N'Investigación automática: el colaborador mostró el error en pantalla al Asistente TI antes de registrar el ticket ', @cIncidenciaNumero, N'.'), 'RECOPILANDO', @dFecha)
	Set @nSesionNumero = Scope_Identity()

	Insert TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
	Values (@cIncidenciaNumero, @cUsuario, 'S', 'TI_AgenteSesion', Convert(varchar(30), @nSesionNumero), 'INICIAR_INVESTIGACION_AUTOMATICA', 'EXITOSO', Null, @cIdCorrelacion, @dFecha)

	Exec dbo.Usp_TI_Agente_ImportarEvidenciaTicket @cUsuario = @cUsuario, @nSesionNumero = @nSesionNumero, @cEvidenciaJson = @cEvidenciaJson

	Exec Usp_TI_Agente_RegistrarEventoServidor @nSesionNumero = @nSesionNumero, @cTipo = 'INVESTIGACION_AUTOMATICA',
		@cContenido = N'Investigación automática encolada: el ticket llegó con la evidencia mostrada al Asistente TI.', @cDatosJson = N'{"origen":"ticket"}'

	Commit Transaction

	Select SesionNumero = @nSesionNumero, UsuarioTI = @cUsuario, AreaTI = @cArea

End
Go

/* Ejecuta Procedure */

Create Or Alter Procedure dbo.Usp_TI_Reproduccion_Finalizar
/*================================================================================
Objetivo            : Cerrar la participación del usuario en la reproducción y avisar al responsable TI.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : dbo.Usp_TI_Reproduccion_Finalizar (29_AgenteFase3ReproduccionUsuario.sql)
Comentario Cambios  : 07/10/2026 Registra en la misma transacción la marca INVESTIGACION_AUTOMATICA: si la API se reinicia antes de
					  investigar, el servidor retoma la investigación pendiente.
================================================================================*/
	@cUsuario varchar(20),
	@nSesionNumero bigint
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Declare @cIncidencia varchar(12), @cResponsable varchar(20), @cCorrelacion uniqueidentifier, @nSecuencia int,
		@cRuta varchar(250) = Concat('/asistente-ti?sesion=', @nSesionNumero), @dFecha datetime2(0) = SysDateTime()

	Begin Try
		Begin Transaction
		Select @cIncidencia = IncidenciaNumero, @cResponsable = UsuarioTI, @cCorrelacion = IdCorrelacion
		From dbo.TI_AgenteSesion With (UpdLock, HoldLock)
		Where SesionNumero = @nSesionNumero and UsuarioInvitado = @cUsuario and EstadoInvitacion = 'ACEPTADA'
		If @@RowCount = 0 Throw 50562, 'La sesión de reproducción ya no está activa.', 1

		Update dbo.TI_AgenteSesion Set EstadoInvitacion = 'FINALIZADA' Where SesionNumero = @nSesionNumero

		Select @nSecuencia = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_AgenteEvento With (UpdLock, HoldLock) Where SesionNumero = @nSesionNumero
		Insert dbo.TI_AgenteEvento (SesionNumero, Secuencia, Tipo, Fuente, Contenido, DatosJson, Fecha, OrigenServidor)
		Values (@nSesionNumero, @nSecuencia, 'FIN_LIVE', 'LIVE_USUARIO', N'El usuario finalizó la reproducción guiada desde su portal.', Null, @dFecha, 0)

		Exec dbo.Usp_TI_Agente_RegistrarEventoServidor @nSesionNumero = @nSesionNumero, @cTipo = 'INVESTIGACION_AUTOMATICA',
			@cContenido = N'Investigación automática encolada: el usuario terminó de reproducir el error.', @cDatosJson = N'{"origen":"reproduccion"}'

		Exec dbo.Usp_TI_Registrar_Notificacion
			@cUsuario = @cResponsable,
			@cIncidenciaNumero = @cIncidencia,
			@cTipo = 'REPRODUCCION',
			@cTitulo = N'Reproducción terminada',
			@cMensaje = N'El usuario terminó de mostrar el error. Revisa su evidencia y continúa la investigación.',
			@cRuta = @cRuta

		Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
		Values (@cIncidencia, @cUsuario, 'U', 'TI_AgenteSesion', Convert(varchar(30), @nSesionNumero), 'FINALIZAR_REPRODUCCION_USUARIO', 'FINALIZADA', Null, @cCorrelacion, @dFecha)
		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go

-- Usp_TI_Agente_ListarInvestigacionesPendientes 10
Create Or Alter Procedure Usp_TI_Agente_ListarInvestigacionesPendientes
/*================================================================================
Objetivo        : Listar las investigaciones automáticas encoladas que nadie terminó (por ejemplo, por un reinicio de la API).
Creado Por      : Jhosep S. Lazo
Fecha Creación  : Oct 2026
SP Anterior     :
Comentario Cambios  : Uso interno del servidor. Pendiente: la última marca es INVESTIGACION_AUTOMATICA (sin _FIN posterior), la
					  investigación sigue investigable sin informe y la marca tiene más de @nMinutos minutos.
================================================================================*/

@nMinutos	int

As
Begin
Set NoCount On

	Select sesion.SesionNumero
	From TI_AgenteSesion as sesion
	Cross Apply (
		Select Top (1) evento.Tipo, evento.Fecha
		From TI_AgenteEvento as evento
		Where evento.SesionNumero = sesion.SesionNumero and evento.Tipo In ('INVESTIGACION_AUTOMATICA','INVESTIGACION_AUTOMATICA_FIN')
		Order By evento.Secuencia Desc
	) as marca
	Where sesion.Estado In ('RECOPILANDO','OBSERVANDO','LISTO_INVESTIGAR') and NullIf(sesion.InformeMarkdown, N'') Is Null
		and marca.Tipo = 'INVESTIGACION_AUTOMATICA' and marca.Fecha < DateAdd(minute, -@nMinutos, SysDateTime())
	Order By sesion.SesionNumero

End
Go

/* Ejecuta Procedure */

-- Usp_TI_Agente_ReconciliarEjecuciones 15
Create Or Alter Procedure Usp_TI_Agente_ReconciliarEjecuciones
/*================================================================================
Objetivo        : Cerrar como error las ejecuciones que quedaron en proceso sin resultado (por ejemplo, la API se detuvo).
Creado Por      : Jhosep S. Lazo
Fecha Creación  : Oct 2026
SP Anterior     :
Comentario Cambios  : Uso interno del servidor. No vuelve a ejecutar nada: marca la ejecución y la investigación con error, deja una
					  nota interna en el ticket y avisa al responsable para que verifique el estado real antes de cualquier reintento.
================================================================================*/

@nMinutos	int

As
Begin
Set NoCount On
Set Xact_Abort On

	Declare @tPendientes table (IncidenciaNumero varchar(12), Secuencia int, AccionCodigo varchar(50), UsuarioEjecutor varchar(20), SesionNumero bigint, IdCorrelacion uniqueidentifier)
	Declare @cMensaje nvarchar(2000) = N'La ejecución quedó interrumpida sin confirmar su resultado (por ejemplo, la API se detuvo). El cambio pudo aplicarse o no: verifica el estado real antes de cualquier nuevo intento.',
		@dFecha datetime2(0) = SysDateTime(), @cIncidencia varchar(12), @cUsuario varchar(20), @nSesion bigint, @cNota nvarchar(max)

	Begin Transaction

	Insert @tPendientes (IncidenciaNumero, Secuencia, AccionCodigo, UsuarioEjecutor, SesionNumero, IdCorrelacion)
	Select ejecucion.IncidenciaNumero, ejecucion.Secuencia, ejecucion.AccionCodigo, ejecucion.UsuarioEjecutor, sesion.SesionNumero, IsNull(sesion.IdCorrelacion, ejecucion.ClaveIdempotencia)
	From TI_EjecucionAccion as ejecucion With (UpdLock, HoldLock)
	Left Join TI_AgenteSesion as sesion on sesion.IncidenciaNumero = ejecucion.IncidenciaNumero and sesion.EjecucionSecuencia = ejecucion.Secuencia
	Where ejecucion.Estado = 'PR' and ejecucion.FechaInicio < DateAdd(minute, -@nMinutos, @dFecha)

	Update ejecucion Set Estado = 'ER', FechaFin = @dFecha, Error = @cMensaje
	From TI_EjecucionAccion as ejecucion
	Inner Join @tPendientes as pendiente on pendiente.IncidenciaNumero = ejecucion.IncidenciaNumero and pendiente.Secuencia = ejecucion.Secuencia

	Update sesion Set Estado = 'ERROR_EJECUCION'
	From TI_AgenteSesion as sesion
	Inner Join @tPendientes as pendiente on pendiente.SesionNumero = sesion.SesionNumero
	Where sesion.Estado = 'EJECUTANDO'

	Insert TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
	Select IncidenciaNumero, Null, 'S', 'TI_EjecucionAccion', Concat(IncidenciaNumero, '/', Secuencia), 'RECONCILIAR_EJECUCION', 'ERROR',
		(Select accionCodigo = AccionCodigo, minutos = @nMinutos For Json Path, Without_Array_Wrapper), IdCorrelacion, @dFecha
	From @tPendientes

	Insert TI_Notificacion (Usuario, IncidenciaNumero, Tipo, Titulo, Mensaje, Ruta, Fecha)
	Select pendiente.UsuarioEjecutor, pendiente.IncidenciaNumero, 'AGENTE', N'Ejecución interrumpida',
		Left(Concat(N'La acción ', pendiente.AccionCodigo, N' del ticket ', pendiente.IncidenciaNumero, N' no confirmó su resultado. Verifica el estado real antes de reintentar.'), 500),
		Case When pendiente.SesionNumero Is Null Then '/gestion-tickets' Else Concat('/asistente-ti?sesion=', pendiente.SesionNumero) End, @dFecha
	From @tPendientes as pendiente
	Inner Join TI_Usuario as usuario on usuario.Usuario = pendiente.UsuarioEjecutor and usuario.Estado = 'A'

	-- La nota interna conserva la trazabilidad en el ticket (una por ejecución).
	While Exists (Select 1 From @tPendientes)
	Begin
		Select Top (1) @cIncidencia = IncidenciaNumero, @cUsuario = UsuarioEjecutor, @nSesion = SesionNumero From @tPendientes Order By IncidenciaNumero
		Set @cNota = Concat(N'Agente de Ingeniería', Case When @nSesion Is Not Null Then Concat(N' · AGT-', Right(Concat('000000', @nSesion), 6)) End, N' · ', @cMensaje)
		Exec dbo.Usp_TI_Agente_RegistrarNotaTicket @cUsuario, @cIncidencia, @cNota
		Delete @tPendientes Where IncidenciaNumero = @cIncidencia and IsNull(SesionNumero, -1) = IsNull(@nSesion, -1)
	End

	Commit Transaction

End
Go

/* Ejecuta Procedure */

/* ============================================================================
   Autocierre opcional de tickets en validación
   ============================================================================ */

-- Usp_TI_Cerrar_TicketsSinValidacion 15
Create Or Alter Procedure Usp_TI_Cerrar_TicketsSinValidacion
/*================================================================================
Objetivo        : Cerrar como resueltos los tickets que esperan la validación del usuario más días de los que TI configuró.
Creado Por      : Jhosep S. Lazo
Fecha Creación  : Oct 2026
SP Anterior     :
Comentario Cambios  : Uso interno del servidor; solo se invoca si TICKET_AUTOCIERRE_PV_DIAS tiene valor. El usuario recibe el aviso y
					  el mensaje visible; el historial y la auditoría registran al sistema como actor.
================================================================================*/

@nDias	int

As
Begin
Set NoCount On
Set Xact_Abort On

	Declare @tVencidos table (IncidenciaNumero varchar(12) Primary Key, UsuarioSolicitante varchar(20), Responsable varchar(20))
	Declare @cMensaje nvarchar(500) = Concat(N'El ticket se cerró como resuelto porque no hubo respuesta a la solución en ', @nDias, N' día(s). Si el problema continúa, registra un ticket nuevo.'),
		@dFecha datetime2(0) = SysDateTime(), @cIncidencia varchar(12), @nMensaje int, @nEstado int

	If @nDias Is Null or @nDias Not Between 1 and 90 Throw 50634, 'El plazo de autocierre debe estar entre 1 y 90 días.', 1

	Begin Transaction

	Insert @tVencidos (IncidenciaNumero, UsuarioSolicitante, Responsable)
	Select ticket.IncidenciaNumero, ticket.UsuarioSolicitante, IsNull(ticket.UsuarioTI, ticket.UsuarioSolicitante)
	From TI_Incidencia as ticket With (UpdLock, HoldLock)
	Where ticket.Estado = 'PV'
		and (Select Max(historial.FechaCambio) From TI_IncidenciaEstado as historial Where historial.IncidenciaNumero = ticket.IncidenciaNumero and historial.Estado = 'PV')
			< DateAdd(day, -@nDias, @dFecha)

	While Exists (Select 1 From @tVencidos)
	Begin
		Select Top (1) @cIncidencia = IncidenciaNumero From @tVencidos Order By IncidenciaNumero

		Update ticket Set Estado = 'RS', FechaCierre = @dFecha, RespuestaUsuario = @cMensaje, UltimoUsuario = vencido.Responsable, UltimaFechaModif = @dFecha
		From TI_Incidencia as ticket
		Inner Join @tVencidos as vencido on vencido.IncidenciaNumero = ticket.IncidenciaNumero
		Where ticket.IncidenciaNumero = @cIncidencia and ticket.Estado = 'PV'

		Select @nEstado = IsNull(Max(Secuencia), 0) + 1 From TI_IncidenciaEstado With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidencia
		Insert TI_IncidenciaEstado (IncidenciaNumero, Secuencia, Estado, UsuarioCambio, FechaCambio, Observacion)
		Values (@cIncidencia, @nEstado, 'RS', Null, @dFecha, Concat(N'Cierre automático: sin validación del usuario en ', @nDias, N' día(s).'))

		Select @nMensaje = IsNull(Max(Secuencia), 0) + 1 From TI_IncidenciaMensaje With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidencia
		Insert TI_IncidenciaMensaje (IncidenciaNumero, Secuencia, UsuarioAutor, TipoAutor, Contenido, FechaMensaje, EsInterno)
		Values (@cIncidencia, @nMensaje, Null, 'S', @cMensaje, @dFecha, 0)

		Insert TI_Notificacion (Usuario, IncidenciaNumero, Tipo, Titulo, Mensaje, Ruta, Fecha)
		Select usuario.Usuario, @cIncidencia, 'CIERRE_AUTOMATICO', N'Ticket cerrado sin validación', Left(Concat(@cIncidencia, N': ', @cMensaje), 500), '/mis-tickets', @dFecha
		From @tVencidos as vencido
		Inner Join TI_Usuario as usuario on usuario.Usuario = vencido.UsuarioSolicitante and usuario.Estado = 'A'
		Where vencido.IncidenciaNumero = @cIncidencia

		Insert TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
		Values (@cIncidencia, Null, 'S', 'TI_Incidencia', @cIncidencia, 'CIERRE_AUTOMATICO_VALIDACION', 'EXITOSO', (Select dias = @nDias For Json Path, Without_Array_Wrapper), NewId(), @dFecha)

		Delete @tVencidos Where IncidenciaNumero = @cIncidencia
	End

	Commit Transaction

End
Go

/* Ejecuta Procedure */
