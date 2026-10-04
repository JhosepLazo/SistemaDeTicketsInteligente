/*
	Archivo: 28_AgenteFase2Integracion.sql
	Objetivo: Integrar las investigaciones del agente con Gestión de Tickets y habilitar la supervisión de TI.
	Responsabilidad: Permitir que SUP/ADM consulten y reasignen investigaciones, exponer los expedientes del agente desde el ticket,
		entregar catálogos para cerrar el caso desde la consola y reforzar la segregación de funciones en aprobaciones y resoluciones.
	Dependencias: Requiere 27_AgenteFase1Integracion.sql.
	Orden: Ejecutar después de 27_AgenteFase1Integracion.sql.
	Consideraciones: La lectura de una investigación ajena es solo de consulta; todos los SP que modifican una sesión siguen exigiendo ser su operador.
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

/* ============================== CONSULTA Y SUPERVISIÓN ============================== */

Create Or Alter Procedure dbo.Usp_TI_Agente_ObtenerContexto
/*================================================================================
Objetivo            : Recuperar únicamente el contexto autorizado necesario para investigar una sesión.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 01/10/2026
SP Anterior         : dbo.Usp_TI_Agente_ObtenerContexto (27_AgenteFase1Integracion.sql)
Comentario Cambios  : 02/10/2026 SUP/ADM pueden consultar investigaciones de otros operadores en modo lectura (EsPropietario = 0).
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3),
	@nSesionNumero bigint
As
Begin
	Set NoCount On

	Declare @cPerfil char(3)
	Select @cPerfil = Perfil From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM')
	If @cPerfil Is Null Throw 50508, 'El operador TI no se encuentra habilitado.', 1
	If Not Exists (Select 1 From dbo.TI_AgenteSesion Where SesionNumero = @nSesionNumero and (UsuarioTI = @cUsuario or @cPerfil In ('SUP','ADM')))
		Throw 50509, 'La sesión de investigación no existe o no pertenece al operador.', 1

	Select
		s.SesionNumero, s.IncidenciaNumero, s.IdCorrelacion, s.DescripcionInicial, s.Estado, s.ResumenObservacion, s.ProcesoObservado, s.ErrorObservado, s.SolucionValidada, s.ConocimientoCodigo,
		s.Diagnostico, s.CausaProbable, s.SolucionPropuesta, s.Confianza, s.AccionCodigo, s.NivelRiesgo, s.ParametrosJson, s.Decision,
		s.SolicitudAprobacionSecuencia, s.FechaInicio, s.FechaDiagnostico, s.FechaDecision, s.EvidenciasJson, s.InformeMarkdown,
		InformeDisponible = Convert(bit, Case When NullIf(s.InformeMarkdown, '') Is Null Then 0 Else 1 End),
		s.UsuarioTI, NombreOperador = op.NombreCompleto, EsPropietario = Convert(bit, Case When s.UsuarioTI = @cUsuario Then 1 Else 0 End),
		i.Titulo, i.Detalle, i.MensajeError, i.Estado as EstadoIncidencia, i.Linea, i.Item, i.Tipo, i.SubTipo, i.Categoria, i.UsuarioSolicitante, i.FechaRegistro
	From dbo.TI_AgenteSesion s
	Inner Join dbo.TI_Usuario op on op.Usuario = s.UsuarioTI
	Left Join dbo.TI_Incidencia i on i.IncidenciaNumero = s.IncidenciaNumero
	Where s.SesionNumero = @nSesionNumero

	Select d.CompaniaSocio, d.TipoDocumento, d.NumeroDocumento, d.Descripcion
	From dbo.TI_IncidenciaDocumento d
	Inner Join dbo.TI_AgenteSesion s on s.IncidenciaNumero = d.IncidenciaNumero
	Where s.SesionNumero = @nSesionNumero
	Order By d.Secuencia

	Select Top (30) m.TipoAutor, Autor = IsNull(u.NombreCompleto, m.UsuarioAutor), m.Contenido, m.FechaMensaje, m.EsInterno
	From dbo.TI_IncidenciaMensaje m
	Inner Join dbo.TI_AgenteSesion s on s.IncidenciaNumero = m.IncidenciaNumero
	Left Join dbo.TI_Usuario u on u.Usuario = m.UsuarioAutor
	Where s.SesionNumero = @nSesionNumero
	Order By m.Secuencia Desc

	;With Contexto as (
		Select i.Linea, i.Item, i.Tipo, i.Categoria
		From dbo.TI_AgenteSesion s
		Left Join dbo.TI_Incidencia i on i.IncidenciaNumero = s.IncidenciaNumero
		Where s.SesionNumero = @nSesionNumero
	)
	Select Top (10) k.ConocimientoCodigo, k.Titulo, k.Problema, k.Sintomas, k.Causa, k.Solucion, k.Procedimiento,
		PuntajeContextual =
			Case When k.Item Is Not Null and k.Item = c.Item Then 5 Else 0 End +
			Case When k.Linea Is Not Null and k.Linea = c.Linea Then 3 Else 0 End +
			Case When k.Categoria Is Not Null and k.Categoria = c.Categoria Then 2 Else 0 End +
			Case When k.Tipo Is Not Null and k.Tipo = c.Tipo Then 1 Else 0 End
	From dbo.TI_BaseConocimiento k
	Cross Join Contexto c
	Where k.Estado = 'A'
		and (c.Linea Is Null or k.Linea Is Null or k.Linea = c.Linea)
	Order By PuntajeContextual Desc, k.FechaValidacion Desc, k.FechaCreacion Desc

	Select a.AccionCodigo, a.Nombre, a.Descripcion, a.Tipo, a.NivelRiesgo, a.RequiereAprobacion,
		TieneEjecutor = Convert(bit, Case When x.AccionCodigo Is Null Then 0 Else 1 End),
		ParametrosDescripcion = IsNull(x.ParametrosDescripcion, N'')
	From dbo.TI_Accion a
	Left Join dbo.TI_AgenteAccionEjecutor x on x.AccionCodigo = a.AccionCodigo and x.Estado = 'A'
	Where a.Estado = 'A'
	Order By a.Tipo, a.NivelRiesgo, a.Nombre

	Select Top (50) a.Entidad, a.Registro, a.Evento, a.Resultado, a.DetalleJson, a.IdCorrelacion, a.Fecha
	From dbo.TI_Auditoria a
	Inner Join dbo.TI_AgenteSesion s on s.IncidenciaNumero = a.IncidenciaNumero
	Where s.SesionNumero = @nSesionNumero
	Order By a.Fecha Desc, a.AuditoriaNumero Desc

	Select Secuencia, Tipo, Fuente, Contenido, DatosJson, Fecha, OrigenServidor
	From dbo.TI_AgenteEvento
	Where SesionNumero = @nSesionNumero
	Order By Secuencia
End
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_Listar
/*================================================================================
Objetivo            : Listar las investigaciones del operador o, para SUP/ADM, todas las investigaciones.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 01/10/2026
SP Anterior         : dbo.Usp_TI_Agente_Listar (26_AgenteDiagnosticoSeguro.sql)
Comentario Cambios  : 02/10/2026 Agrega el alcance de supervisión y el operador responsable de cada investigación.
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3),
	@lTodas bit = 0
As
Begin
	Set NoCount On

	Declare @cPerfil char(3)
	Select @cPerfil = Perfil From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM')
	If @cPerfil Is Null Throw 50500, 'El operador TI no se encuentra habilitado.', 1
	If @lTodas = 1 and @cPerfil Not In ('SUP','ADM') Throw 50548, 'Solo un supervisor o administrador puede consultar las investigaciones de todo el equipo.', 1

	Select Top (200) s.SesionNumero, IsNull(s.IncidenciaNumero, '') IncidenciaNumero, s.IdCorrelacion, s.DescripcionInicial, s.Estado, s.FechaInicio,
		s.SolucionValidada, IsNull(s.ConocimientoCodigo, '') ConocimientoCodigo, s.Confianza, IsNull(s.AccionCodigo, '') AccionCodigo,
		s.UsuarioTI, NombreOperador = op.NombreCompleto, EsPropietario = Convert(bit, Case When s.UsuarioTI = @cUsuario Then 1 Else 0 End)
	From dbo.TI_AgenteSesion s
	Inner Join dbo.TI_Usuario op on op.Usuario = s.UsuarioTI
	Where (@lTodas = 1 or (s.UsuarioTI = @cUsuario and s.AreaTI = @cArea))
	Order By s.FechaInicio Desc, s.SesionNumero Desc
End
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_Reasignar
/*================================================================================
Objetivo            : Transferir una investigación activa a otro operador TI (o tomarla) por decisión de un supervisor.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : No permite reasignar sesiones cerradas o en ejecución; notifica al nuevo responsable y deja auditoría.
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3),
	@nSesionNumero bigint,
	@cNuevoUsuario varchar(20)
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Declare @cNuevaArea char(3), @cAnterior varchar(20), @cIncidencia varchar(12), @cCorrelacion uniqueidentifier,
		@cRuta varchar(250) = Concat('/asistente-ti?sesion=', @nSesionNumero), @dFecha datetime2(0) = SysDateTime()

	If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('SUP','ADM'))
		Throw 50549, 'Solo un supervisor o administrador puede reasignar investigaciones.', 1
	Select @cNuevaArea = Area From dbo.TI_Usuario Where Usuario = @cNuevoUsuario and Estado = 'A' and Perfil In ('TEC','SUP','ADM')
	If @cNuevaArea Is Null Throw 50550, 'El nuevo responsable debe ser un operador TI activo.', 1

	Begin Try
		Begin Transaction
		Select @cAnterior = UsuarioTI, @cIncidencia = IncidenciaNumero, @cCorrelacion = IdCorrelacion
		From dbo.TI_AgenteSesion With (UpdLock, HoldLock)
		Where SesionNumero = @nSesionNumero and Estado In ('RECOPILANDO','OBSERVANDO','LISTO_INVESTIGAR','PENDIENTE_TI','PENDIENTE_APROBACION','SIN_EJECUTOR','ERROR_EJECUCION')
		If @@RowCount = 0 Throw 50551, 'La investigación no existe o su estado actual no permite reasignarla.', 1
		If @cAnterior = @cNuevoUsuario Throw 50552, 'La investigación ya pertenece a ese operador.', 1

		Update dbo.TI_AgenteSesion Set UsuarioTI = @cNuevoUsuario, AreaTI = @cNuevaArea Where SesionNumero = @nSesionNumero

		Exec dbo.Usp_TI_Registrar_Notificacion
			@cUsuario = @cNuevoUsuario,
			@cIncidenciaNumero = @cIncidencia,
			@cTipo = 'AGENTE_REASIGNADO',
			@cTitulo = N'Investigación asignada',
			@cMensaje = N'Un supervisor te asignó una investigación del Agente de Ingeniería.',
			@cRuta = @cRuta

		Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
		Values (@cIncidencia, @cUsuario, 'T', 'TI_AgenteSesion', Convert(varchar(30), @nSesionNumero), 'REASIGNAR_INVESTIGACION', 'EXITOSO',
			Concat('{"anterior":"', @cAnterior, '","nuevo":"', @cNuevoUsuario, '"}'), @cCorrelacion, @dFecha)
		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_Catalogos
/*================================================================================
Objetivo            : Entregar los catálogos que la consola del agente necesita para cerrar un caso o reasignarlo.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : Devuelve áreas activas (área causante) y operadores TI activos (reasignación).
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3)
As
Begin
	Set NoCount On

	If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM')) Throw 50500, 'El operador TI no se encuentra habilitado.', 1

	Select Codigo = Area, Descripcion From dbo.TI_Area Where Estado = 'A' Order By Descripcion
	Select Codigo = Usuario, Descripcion = Concat(NombreCompleto, ' · ', Perfil) From dbo.TI_Usuario Where Estado = 'A' and Perfil In ('TEC','SUP','ADM') Order By NombreCompleto
End
Go

/* ============================== EXPEDIENTES DESDE EL TICKET ============================== */

Create Or Alter Procedure dbo.Usp_TI_Agente_ListarPorTicket
/*================================================================================
Objetivo            : Listar las investigaciones del agente asociadas a un ticket para mostrarlas en Gestión de Tickets.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : Cualquier operador TI activo puede consultar el resumen; abrir la sesión sigue sujeto a propiedad o supervisión.
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3),
	@cIncidenciaNumero varchar(12)
As
Begin
	Set NoCount On

	Declare @cPerfil char(3)
	Select @cPerfil = Perfil From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM')
	If @cPerfil Is Null Throw 50500, 'El operador TI no se encuentra habilitado.', 1

	Select s.SesionNumero, s.Estado, s.FechaInicio, s.FechaDiagnostico, s.Confianza, Diagnostico = Left(IsNull(s.Diagnostico, N''), 500),
		AccionCodigo = IsNull(s.AccionCodigo, ''), s.UsuarioTI, NombreOperador = op.NombreCompleto,
		InformeDisponible = Convert(bit, Case When NullIf(s.InformeMarkdown, '') Is Null Then 0 Else 1 End),
		PuedeAbrir = Convert(bit, Case When s.UsuarioTI = @cUsuario or @cPerfil In ('SUP','ADM') Then 1 Else 0 End)
	From dbo.TI_AgenteSesion s
	Inner Join dbo.TI_Usuario op on op.Usuario = s.UsuarioTI
	Where s.IncidenciaNumero = @cIncidenciaNumero
	Order By s.FechaInicio Desc
End
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_ObtenerInforme
/*================================================================================
Objetivo            : Obtener el expediente Markdown de una investigación asociada a un ticket.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : Lectura para cualquier operador TI activo; el expediente forma parte del historial técnico del ticket.
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3),
	@nSesionNumero bigint
As
Begin
	Set NoCount On

	If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM')) Throw 50500, 'El operador TI no se encuentra habilitado.', 1

	Select InformeMarkdown, IncidenciaNumero = IsNull(IncidenciaNumero, '')
	From dbo.TI_AgenteSesion
	Where SesionNumero = @nSesionNumero and NullIf(InformeMarkdown, '') Is Not Null
		and (IncidenciaNumero Is Not Null or UsuarioTI = @cUsuario)
	If @@RowCount = 0 Throw 50553, 'La investigación no tiene un expediente disponible para consulta.', 1
End
Go

/* ============================== SEGREGACIÓN DE FUNCIONES ============================== */

Create Or Alter Procedure dbo.Usp_TI_Responder_AprobacionTicket
/*================================================================================
Objetivo            : Aprobar o rechazar una solicitud pendiente y desbloquear el flujo operativo.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : dbo.Usp_TI_Responder_AprobacionTicket (27_AgenteFase1Integracion.sql)
Comentario Cambios  : 02/10/2026 Tampoco puede aprobar el operador que hoy es responsable de la investigación del agente que originó la solicitud
					  (evita eludir la segregación reasignando la investigación).
================================================================================*/
	@cUsuario varchar(20), @cArea char(3), @cIncidenciaNumero varchar(12), @nSecuencia int, @lAprobar bit, @cComentario nvarchar(1000), @cIdCorrelacion uniqueidentifier
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Begin Try
		Begin Transaction
		Declare @nEstado int, @cSolicitante varchar(20), @cMensajeNotificacion nvarchar(500), @cRuta varchar(250) = '/gestion-tickets', @nSesionAgente bigint, @cResponsableAgente varchar(20), @dFecha datetime2(0) = SysDateTime()

		If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Estado = 'A' and Perfil In ('TEC','SUP','ADM')) Throw 50230, 'Solo un operador TI activo puede responder esta aprobación.', 1
		Select @cSolicitante = UsuarioSolicitante From dbo.TI_SolicitudAprobacion With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero and Secuencia = @nSecuencia and Estado = 'P'
		If @cSolicitante Is Null Throw 50231, 'La solicitud de aprobación ya no se encuentra pendiente.', 1
		If @cSolicitante = @cUsuario Throw 50242, 'No puedes responder una aprobación que tú mismo solicitaste. Debe resolverla otro operador TI.', 1

		Select @nSesionAgente = SesionNumero, @cResponsableAgente = UsuarioTI From dbo.TI_AgenteSesion
		Where IncidenciaNumero = @cIncidenciaNumero and SolicitudAprobacionSecuencia = @nSecuencia and Estado = 'PENDIENTE_APROBACION'
		If @cResponsableAgente = @cUsuario Throw 50243, 'Eres el responsable actual de la investigación que propone esta acción; debe aprobarla otro operador TI.', 1
		If @lAprobar = 0 and NullIf(LTrim(RTrim(@cComentario)), '') Is Null Throw 50232, 'Indica el motivo del rechazo.', 1

		Update dbo.TI_SolicitudAprobacion
		Set Estado = Case When @lAprobar = 1 Then 'A' Else 'R' End, UsuarioAprobador = @cUsuario,
			ComentarioRespuesta = NullIf(LTrim(RTrim(@cComentario)), ''), FechaRespuesta = @dFecha
		Where IncidenciaNumero = @cIncidenciaNumero and Secuencia = @nSecuencia and Estado = 'P'

		Update dbo.TI_Incidencia Set Estado = 'DG', UltimoUsuario = @cUsuario, UltimaFechaModif = @dFecha Where IncidenciaNumero = @cIncidenciaNumero and Estado = 'PA'
		Select @nEstado = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_IncidenciaEstado With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero
		Insert dbo.TI_IncidenciaEstado (IncidenciaNumero, Secuencia, Estado, UsuarioCambio, FechaCambio, Observacion)
		Values (@cIncidenciaNumero, @nEstado, 'DG', @cUsuario, @dFecha, Case When @lAprobar = 1 Then N'La aprobación fue concedida; el ticket puede continuar.' Else N'La aprobación fue rechazada; el ticket vuelve a diagnóstico.' End)

		If @nSesionAgente Is Not Null Set @cRuta = Concat('/asistente-ti?sesion=', @nSesionAgente)

		Set @cMensajeNotificacion = Case
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
		Values (@cIncidenciaNumero, @cUsuario, 'T', 'TI_SolicitudAprobacion', Concat(@cIncidenciaNumero, '-', @nSecuencia), Case When @lAprobar = 1 Then 'APROBAR_ACCION' Else 'RECHAZAR_ACCION' End, 'EXITOSO', Null, @cIdCorrelacion, @dFecha)

		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go

Create Or Alter Procedure dbo.Usp_TI_Resolver_TicketTI
/*================================================================================
Objetivo            : Registrar la resolución técnica y enviar el ticket a validación del usuario.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : dbo.Usp_TI_Resolver_TicketTI (16_GestionTicketsTI.sql)
Comentario Cambios  : 02/10/2026 Un ticket bloqueado en PA (aprobación pendiente) no puede resolverse hasta responder la aprobación.
================================================================================*/
	@cUsuario varchar(20), @cArea char(3), @cIncidenciaNumero varchar(12), @cCausaRaiz nvarchar(max), @cSolucion nvarchar(max),
	@cRespuestaUsuario nvarchar(max), @cTipoResolucion varchar(20), @cIdCorrelacion uniqueidentifier
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Begin Try
		Begin Transaction
		Declare @nMensaje int, @nEstado int, @dFecha datetime2(0) = SysDateTime()

		If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC', 'SUP', 'ADM')) Throw 50222, 'El operador TI no es válido.', 1
		If Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero and Estado = 'PA') Throw 50244, 'El ticket tiene una aprobación pendiente; resuélvela antes de enviar la solución.', 1
		If Not Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero and Estado Not In ('RS', 'CA', 'PV')) Throw 50223, 'El ticket no admite resolución en su estado actual.', 1
		If NullIf(LTrim(RTrim(@cCausaRaiz)), '') Is Null Throw 50224, 'Registra la causa raíz validada.', 1
		If NullIf(LTrim(RTrim(@cSolucion)), '') Is Null Throw 50225, 'Registra la solución aplicada.', 1
		If NullIf(LTrim(RTrim(@cRespuestaUsuario)), '') Is Null Throw 50226, 'Registra la respuesta que recibirá el usuario.', 1

		Update dbo.TI_Incidencia
		Set Estado = 'PV', CausaRaiz = LTrim(RTrim(@cCausaRaiz)), SolucionTecnica = LTrim(RTrim(@cSolucion)), RespuestaUsuario = LTrim(RTrim(@cRespuestaUsuario)),
			TipoResolucion = NullIf(LTrim(RTrim(@cTipoResolucion)), ''), FechaAtencion = IsNull(FechaAtencion, @dFecha), UltimoUsuario = @cUsuario, UltimaFechaModif = @dFecha
		Where IncidenciaNumero = @cIncidenciaNumero

		Select @nMensaje = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_IncidenciaMensaje With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero
		Insert dbo.TI_IncidenciaMensaje (IncidenciaNumero, Secuencia, UsuarioAutor, TipoAutor, Contenido, FechaMensaje, EsInterno)
		Values (@cIncidenciaNumero, @nMensaje, @cUsuario, 'T', LTrim(RTrim(@cRespuestaUsuario)), @dFecha, 0)

		Select @nEstado = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_IncidenciaEstado With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero
		Insert dbo.TI_IncidenciaEstado (IncidenciaNumero, Secuencia, Estado, UsuarioCambio, FechaCambio, Observacion)
		Values (@cIncidenciaNumero, @nEstado, 'PV', @cUsuario, @dFecha, N'Solución técnica registrada; pendiente de validación del usuario.')

		Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
		Values (@cIncidenciaNumero, @cUsuario, 'T', 'TI_Incidencia', @cIncidenciaNumero, 'ENVIAR_A_VALIDACION', 'EXITOSO', Null, @cIdCorrelacion, @dFecha)
		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go

/* Ejecuta Procedure
Exec dbo.Usp_TI_Agente_Listar @cUsuario = 'SUP001', @cArea = 'TIC', @lTodas = 1;
Exec dbo.Usp_TI_Agente_ListarPorTicket @cUsuario = 'TEC001', @cArea = 'TIC', @cIncidenciaNumero = 'INC-000001';
*/
