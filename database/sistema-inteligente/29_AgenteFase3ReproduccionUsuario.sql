/*
	Archivo: 29_AgenteFase3ReproduccionUsuario.sql
	Objetivo: Incorporar al usuario final en la observación: TI lo invita, el usuario consiente y reproduce el error desde su portal.
	Responsabilidad: Registrar la invitación y el consentimiento, recibir la evidencia Live del usuario y correlacionar su telemetría con la investigación.
	Dependencias: Requiere 28_AgenteFase2Integracion.sql.
	Orden: Ejecutar después de 28_AgenteFase2Integracion.sql.
	Consideraciones: El usuario solo puede aportar observaciones (fuente LIVE_USUARIO) mientras la investigación esté en observación y su invitación esté aceptada y vigente;
		nunca obtiene acceso al diagnóstico, al expediente ni a las decisiones de TI. La pantalla no se almacena.
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

If Col_Length('dbo.TI_AgenteSesion', 'UsuarioInvitado') Is Null
	Alter Table dbo.TI_AgenteSesion Add
		UsuarioInvitado				varchar(20)		Null,
		EstadoInvitacion			varchar(20)		Null,
		FechaInvitacion				datetime2(0)	Null,
		InvitacionExpira			datetime2(0)	Null,
		FechaRespuestaInvitacion	datetime2(0)	Null,
		ConsentimientoVersion		varchar(30)		Null
Go

If Object_Id('dbo.FK_TI_AgenteSesion_UsuarioInvitado', 'F') Is Null
	Alter Table dbo.TI_AgenteSesion With Check Add Constraint FK_TI_AgenteSesion_UsuarioInvitado Foreign Key (UsuarioInvitado) References dbo.TI_Usuario (Usuario)
Go

If Object_Id('dbo.CK_TI_AgenteSesion_EstadoInvitacion', 'C') Is Null
	-- PENDIENTE, ACEPTADA, RECHAZADA, CANCELADA (por TI) o FINALIZADA (el usuario terminó la reproducción).
	Alter Table dbo.TI_AgenteSesion With Check Add Constraint CK_TI_AgenteSesion_EstadoInvitacion
		Check (EstadoInvitacion Is Null or EstadoInvitacion In ('PENDIENTE','ACEPTADA','RECHAZADA','CANCELADA','FINALIZADA'))
Go

If Not Exists (Select 1 From sys.indexes Where object_id = Object_Id(N'dbo.TI_AgenteSesion') and name = N'IX_TI_AgenteSesion_UsuarioInvitado')
	-- Sin filtro a propósito: un índice filtrado obliga a que todo SP que modifique la tabla esté compilado con QUOTED_IDENTIFIER ON.
	Create Nonclustered Index IX_TI_AgenteSesion_UsuarioInvitado
		on dbo.TI_AgenteSesion (UsuarioInvitado, EstadoInvitacion)
Go

/* ============================== CONTEXTO ============================== */

-- dbo.Usp_TI_Agente_ObtenerContexto: la versión vigente está en 33_AgenteFase6Mejoras.sql (aquí había una versión anterior que ese script reemplaza).
Go

/* ============================== INVITACIÓN (TI) ============================== */

Create Or Alter Procedure dbo.Usp_TI_Agente_InvitarUsuario
/*================================================================================
Objetivo            : Invitar al solicitante del ticket a reproducir el error desde su portal durante la observación.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : La invitación vence en 24 horas, se notifica al usuario y queda un mensaje visible en el ticket.
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3),
	@nSesionNumero bigint
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Declare @cIncidencia varchar(12), @cSolicitante varchar(20), @cCorrelacion uniqueidentifier, @nMensaje int,
		@cRuta varchar(250) = Concat('/reproducir?sesion=', @nSesionNumero), @dFecha datetime2(0) = SysDateTime(), @dExpira datetime2(0) = DateAdd(hour, 24, SysDateTime())

	If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM')) Throw 50554, 'El operador TI no se encuentra habilitado.', 1

	Begin Try
		Begin Transaction
		Select @cIncidencia = IncidenciaNumero, @cCorrelacion = IdCorrelacion
		From dbo.TI_AgenteSesion With (UpdLock, HoldLock)
		Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario and AreaTI = @cArea and Estado In ('RECOPILANDO','OBSERVANDO','LISTO_INVESTIGAR')
		If @@RowCount = 0 Throw 50555, 'Solo puedes invitar al usuario en una investigación propia que siga en etapa de observación.', 1
		If @cIncidencia Is Null Throw 50556, 'Vincula un ticket antes de invitar: la invitación se envía a su solicitante.', 1
		If Exists (Select 1 From dbo.TI_AgenteSesion Where SesionNumero = @nSesionNumero and EstadoInvitacion In ('PENDIENTE','ACEPTADA') and InvitacionExpira > @dFecha)
			Throw 50559, 'Ya existe una invitación vigente para esta investigación.', 1

		Select @cSolicitante = i.UsuarioSolicitante
		From dbo.TI_Incidencia i
		Inner Join dbo.TI_Usuario u on u.Usuario = i.UsuarioSolicitante and u.Estado = 'A'
		Where i.IncidenciaNumero = @cIncidencia and i.Estado Not In ('RS','CA','CF','NP')
		If @cSolicitante Is Null Throw 50557, 'El ticket está cerrado o su solicitante no tiene una cuenta activa.', 1
		If @cSolicitante = @cUsuario Throw 50558, 'Eres el solicitante del ticket: reproduce el error directamente con tu sesión Live.', 1

		Update dbo.TI_AgenteSesion
		Set UsuarioInvitado = @cSolicitante, EstadoInvitacion = 'PENDIENTE', FechaInvitacion = @dFecha, InvitacionExpira = @dExpira,
			FechaRespuestaInvitacion = Null, ConsentimientoVersion = Null
		Where SesionNumero = @nSesionNumero

		Select @nMensaje = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_IncidenciaMensaje With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidencia
		Insert dbo.TI_IncidenciaMensaje (IncidenciaNumero, Secuencia, UsuarioAutor, TipoAutor, Contenido, FechaMensaje, EsInterno)
		Values (@cIncidencia, @nMensaje, @cUsuario, 'T',
			N'TI te invita a mostrar el error en una sesión guiada de pantalla y voz. Puedes aceptarla o rechazarla desde la notificación o desde Inicio; vence en 24 horas y tú decides cuándo empezar y terminar.', @dFecha, 0)

		Exec dbo.Usp_TI_Registrar_Notificacion
			@cUsuario = @cSolicitante,
			@cIncidenciaNumero = @cIncidencia,
			@cTipo = 'REPRODUCCION',
			@cTitulo = N'TI te pide mostrar el error',
			@cMensaje = N'Acepta una sesión guiada para compartir tu pantalla y explicar el error. Tú decides cuándo empezar y terminar.',
			@cRuta = @cRuta

		Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
		Values (@cIncidencia, @cUsuario, 'T', 'TI_AgenteSesion', Convert(varchar(30), @nSesionNumero), 'INVITAR_REPRODUCCION', 'PENDIENTE',
			Concat('{"usuarioInvitado":"', @cSolicitante, '"}'), @cCorrelacion, @dFecha)
		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch

	Select UsuarioInvitado = @cSolicitante, NombreInvitado = (Select NombreCompleto From dbo.TI_Usuario Where Usuario = @cSolicitante), InvitacionExpira = @dExpira
End
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_CancelarInvitacion
/*================================================================================
Objetivo            : Retirar una invitación de reproducción todavía vigente.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : Desde ese momento el usuario ya no puede enviar evidencia ni obtener una sesión Live para la investigación.
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3),
	@nSesionNumero bigint
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Declare @cIncidencia varchar(12), @cInvitado varchar(20), @cCorrelacion uniqueidentifier, @dFecha datetime2(0) = SysDateTime()

	Begin Try
		Begin Transaction
		Select @cIncidencia = IncidenciaNumero, @cInvitado = UsuarioInvitado, @cCorrelacion = IdCorrelacion
		From dbo.TI_AgenteSesion With (UpdLock, HoldLock)
		Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario and AreaTI = @cArea and EstadoInvitacion In ('PENDIENTE','ACEPTADA')
		If @@RowCount = 0 Throw 50563, 'No existe una invitación vigente que puedas cancelar.', 1

		Update dbo.TI_AgenteSesion Set EstadoInvitacion = 'CANCELADA', FechaRespuestaInvitacion = @dFecha Where SesionNumero = @nSesionNumero

		Exec dbo.Usp_TI_Registrar_Notificacion
			@cUsuario = @cInvitado,
			@cIncidenciaNumero = @cIncidencia,
			@cTipo = 'REPRODUCCION',
			@cTitulo = N'Sesión de reproducción cancelada',
			@cMensaje = N'TI ya no necesita que muestres el error en una sesión guiada. Gracias por tu disposición.',
			@cRuta = '/mis-tickets'

		Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
		Values (@cIncidencia, @cUsuario, 'T', 'TI_AgenteSesion', Convert(varchar(30), @nSesionNumero), 'CANCELAR_INVITACION_REPRODUCCION', 'CANCELADA', Null, @cCorrelacion, @dFecha)
		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go

/* ============================== REPRODUCCIÓN (USUARIO FINAL) ============================== */

Create Or Alter Procedure dbo.Usp_TI_Reproduccion_Listar
/*================================================================================
Objetivo            : Listar las invitaciones de reproducción vigentes del usuario autenticado.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : Solo expone datos del ticket del propio usuario y el nombre del operador; nunca el diagnóstico interno.
================================================================================*/
	@cUsuario varchar(20),
	@nSesionNumero bigint = Null
As
Begin
	Set NoCount On

	Select s.SesionNumero, s.IncidenciaNumero, TituloTicket = i.Titulo, OperadorTI = op.NombreCompleto,
		s.EstadoInvitacion, s.FechaInvitacion, s.InvitacionExpira
	From dbo.TI_AgenteSesion s
	Inner Join dbo.TI_Incidencia i on i.IncidenciaNumero = s.IncidenciaNumero
	Inner Join dbo.TI_Usuario op on op.Usuario = s.UsuarioTI
	Where s.UsuarioInvitado = @cUsuario and i.UsuarioSolicitante = @cUsuario
		and s.EstadoInvitacion In ('PENDIENTE','ACEPTADA') and s.InvitacionExpira > SysDateTime()
		and s.Estado In ('RECOPILANDO','OBSERVANDO','LISTO_INVESTIGAR')
		and (@nSesionNumero Is Null or s.SesionNumero = @nSesionNumero)
	Order By s.FechaInvitacion Desc
End
Go

Create Or Alter Procedure dbo.Usp_TI_Reproduccion_Responder
/*================================================================================
Objetivo            : Registrar la aceptación (con consentimiento explícito) o el rechazo de una invitación de reproducción.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : El consentimiento queda versionado en la sesión, como evento de la investigación y en auditoría.
================================================================================*/
	@cUsuario varchar(20),
	@nSesionNumero bigint,
	@lAceptar bit,
	@cConsentimientoVersion varchar(30) = Null,
	@cMotivo nvarchar(500) = Null
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Declare @cIncidencia varchar(12), @cResponsable varchar(20), @cCorrelacion uniqueidentifier, @nSecuencia int, @cContenido nvarchar(1000),
		@cRuta varchar(250) = Concat('/asistente-ti?sesion=', @nSesionNumero), @dFecha datetime2(0) = SysDateTime()

	If @lAceptar = 1 and NullIf(LTrim(RTrim(@cConsentimientoVersion)), '') Is Null Throw 50561, 'Debes aceptar el consentimiento para compartir tu pantalla.', 1

	Begin Try
		Begin Transaction
		Select @cIncidencia = IncidenciaNumero, @cResponsable = UsuarioTI, @cCorrelacion = IdCorrelacion
		From dbo.TI_AgenteSesion With (UpdLock, HoldLock)
		Where SesionNumero = @nSesionNumero and UsuarioInvitado = @cUsuario and EstadoInvitacion = 'PENDIENTE' and InvitacionExpira > @dFecha
			and Estado In ('RECOPILANDO','OBSERVANDO','LISTO_INVESTIGAR')
		If @@RowCount = 0 Throw 50560, 'La invitación no existe, venció o ya fue respondida.', 1

		Update dbo.TI_AgenteSesion
		Set EstadoInvitacion = Case When @lAceptar = 1 Then 'ACEPTADA' Else 'RECHAZADA' End, FechaRespuestaInvitacion = @dFecha,
			ConsentimientoVersion = Case When @lAceptar = 1 Then LTrim(RTrim(@cConsentimientoVersion)) End
		Where SesionNumero = @nSesionNumero

		Set @cContenido = Case When @lAceptar = 1
			Then Concat(N'El usuario aceptó reproducir el error y otorgó su consentimiento (', LTrim(RTrim(@cConsentimientoVersion)), N').')
			Else Concat(N'El usuario rechazó la sesión de reproducción.', Case When NullIf(LTrim(RTrim(@cMotivo)), '') Is Null Then N'' Else Concat(N' Motivo: ', Left(LTrim(RTrim(@cMotivo)), 500)) End) End
		Select @nSecuencia = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_AgenteEvento With (UpdLock, HoldLock) Where SesionNumero = @nSesionNumero
		Insert dbo.TI_AgenteEvento (SesionNumero, Secuencia, Tipo, Fuente, Contenido, DatosJson, Fecha, OrigenServidor)
		Values (@nSesionNumero, @nSecuencia, Case When @lAceptar = 1 Then 'CONSENTIMIENTO_USUARIO' Else 'RECHAZO_USUARIO' End, 'USUARIO_FINAL', @cContenido, Null, @dFecha, 0)

		Exec dbo.Usp_TI_Registrar_Notificacion
			@cUsuario = @cResponsable,
			@cIncidenciaNumero = @cIncidencia,
			@cTipo = 'REPRODUCCION',
			@cTitulo = N'Respuesta del usuario',
			@cMensaje = @cContenido,
			@cRuta = @cRuta

		Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
		Values (@cIncidencia, @cUsuario, 'U', 'TI_AgenteSesion', Convert(varchar(30), @nSesionNumero), 'CONSENTIMIENTO_REPRODUCCION',
			Case When @lAceptar = 1 Then 'ACEPTADO' Else 'RECHAZADO' End,
			Case When @lAceptar = 1 Then Concat('{"version":"', LTrim(RTrim(@cConsentimientoVersion)), '"}') End, @cCorrelacion, @dFecha)
		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go

-- dbo.Usp_TI_Reproduccion_RegistrarEvento: la versión vigente está en 30_AgenteFase4HerramientasDiagnostico.sql (aquí había una versión anterior que ese script reemplaza).
Go

-- dbo.Usp_TI_Reproduccion_Finalizar: la versión vigente está en 35_ControlAgenteYAutonomia.sql (aquí había una versión anterior que ese script reemplaza).
Go

/* ============================== CORRELACIÓN Y TELEMETRÍA ============================== */

Create Or Alter Procedure dbo.Usp_TI_Agente_Correlacion
/*================================================================================
Objetivo            : Resolver el CorrelationId de una investigación para instrumentar las solicitudes del navegador.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 01/10/2026
SP Anterior         : dbo.Usp_TI_Agente_Correlacion (26_AgenteDiagnosticoSeguro.sql)
Comentario Cambios  : 02/10/2026 También correlaciona al usuario invitado mientras su invitación esté aceptada y vigente.
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3),
	@nSesionNumero bigint
As
Begin
	Set NoCount On

	Select s.IdCorrelacion
	From dbo.TI_AgenteSesion s
	Where s.SesionNumero = @nSesionNumero and s.Estado In ('RECOPILANDO','OBSERVANDO','LISTO_INVESTIGAR')
		and (
			(s.UsuarioTI = @cUsuario and s.AreaTI = @cArea
				and Exists (Select 1 From dbo.TI_Usuario u Where u.Usuario = @cUsuario and u.Estado = 'A' and u.Perfil In ('TEC','SUP','ADM')))
			or (s.UsuarioInvitado = @cUsuario and s.EstadoInvitacion = 'ACEPTADA' and s.InvitacionExpira > SysDateTime())
		)
End
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_Telemetria
/*================================================================================
Objetivo            : Registrar una traza técnica del servidor correlacionada con la investigación.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 01/10/2026
SP Anterior         : dbo.Usp_TI_Agente_Telemetria (26_AgenteDiagnosticoSeguro.sql)
Comentario Cambios  : 02/10/2026 Acepta trazas originadas por el usuario invitado durante su reproducción.
================================================================================*/
	@cUsuario varchar(20),
	@nSesionNumero bigint,
	@cContenido nvarchar(max),
	@cDatosJson nvarchar(max)
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Begin Try
		Begin Transaction
		If Not Exists (Select 1 From dbo.TI_AgenteSesion With (UpdLock, HoldLock)
			Where SesionNumero = @nSesionNumero and Estado In ('RECOPILANDO','OBSERVANDO','LISTO_INVESTIGAR')
				and (UsuarioTI = @cUsuario or (UsuarioInvitado = @cUsuario and EstadoInvitacion = 'ACEPTADA' and InvitacionExpira > SysDateTime())))
		Begin
			Commit Transaction
			Return
		End
		Declare @nSecuencia int
		Select @nSecuencia = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_AgenteEvento With (UpdLock, HoldLock) Where SesionNumero = @nSesionNumero
		Insert dbo.TI_AgenteEvento (SesionNumero, Secuencia, Tipo, Fuente, Contenido, DatosJson, Fecha, OrigenServidor)
		Values (@nSesionNumero, @nSecuencia, 'TRAZA_BACKEND', 'TELEMETRIA',
			Left(Concat(Case When Exists (Select 1 From dbo.TI_AgenteSesion Where SesionNumero = @nSesionNumero and UsuarioInvitado = @cUsuario and UsuarioTI <> @cUsuario) Then N'[usuario] ' Else N'' End, @cContenido), 12000),
			@cDatosJson, SysDateTime(), 1)
		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go

/* Ejecuta Procedure
Exec dbo.Usp_TI_Agente_InvitarUsuario @cUsuario = 'TEC001', @cArea = 'TIC', @nSesionNumero = 1;
Exec dbo.Usp_TI_Reproduccion_Listar @cUsuario = 'USR001';
*/
