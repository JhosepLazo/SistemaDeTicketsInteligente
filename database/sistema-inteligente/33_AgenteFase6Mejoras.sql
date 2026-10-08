/*
	Archivo: 33_AgenteFase6Mejoras.sql
	Objetivo: Ajustes de la Fase 6 detectados al revisar los flujos completos.
	Responsabilidad: Permitir que el responsable TI del ticket consulte y tome la investigación del agente (antes solo podía un supervisor),
		y que los videos adjuntados al ticket pasen a la investigación para que el agente los analice.
	Dependencias: Requiere 32_AgenteFase6ReplicaTecnica.sql.
	Orden: Ejecutar después de 32_AgenteFase6ReplicaTecnica.sql.
	Consideraciones: Solo lectura de permisos y vinculación de evidencia; no modifica datos del negocio.
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

-- dbo.Usp_TI_Agente_ObtenerContexto: la versión vigente está en 36_ClasificacionFichaYGuias.sql (aquí había una versión anterior que ese script reemplaza).
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_ListarPorTicket
/*================================================================================
Objetivo            : Listar las investigaciones del agente asociadas a un ticket para mostrarlas en Gestión de Tickets.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : dbo.Usp_TI_Agente_ListarPorTicket (28_AgenteFase2Integracion.sql)
Comentario Cambios  : 02/10/2026 El responsable TI del ticket también puede abrir sus investigaciones.
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
		PuedeAbrir = Convert(bit, Case When s.UsuarioTI = @cUsuario or @cPerfil In ('SUP','ADM') or i.UsuarioTI = @cUsuario Then 1 Else 0 End)
	From dbo.TI_AgenteSesion s
	Inner Join dbo.TI_Usuario op on op.Usuario = s.UsuarioTI
	Inner Join dbo.TI_Incidencia i on i.IncidenciaNumero = s.IncidenciaNumero
	Where s.IncidenciaNumero = @cIncidenciaNumero
	Order By s.FechaInicio Desc
End
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_Reasignar
/*================================================================================
Objetivo            : Transferir una investigación activa a otro operador TI, o tomarla.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : dbo.Usp_TI_Agente_Reasignar (28_AgenteFase2Integracion.sql)
Comentario Cambios  : 02/10/2026 Además de supervisión, el responsable TI del ticket puede tomar la investigación (para sí mismo).
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3),
	@nSesionNumero bigint,
	@cNuevoUsuario varchar(20)
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Declare @cNuevaArea char(3), @cAnterior varchar(20), @cIncidencia varchar(12), @cCorrelacion uniqueidentifier, @lSupervisor bit,
		@cRuta varchar(250) = Concat('/asistente-ti?sesion=', @nSesionNumero), @dFecha datetime2(0) = SysDateTime()

	Select @lSupervisor = Case When Perfil In ('SUP','ADM') Then 1 Else 0 End
	From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM')
	If @lSupervisor Is Null Throw 50549, 'Solo un supervisor o el responsable del ticket puede reasignar o tomar investigaciones.', 1
	If @lSupervisor = 0 and (@cNuevoUsuario <> @cUsuario or Not Exists (Select 1 From dbo.TI_AgenteSesion s Join dbo.TI_Incidencia i on i.IncidenciaNumero = s.IncidenciaNumero
		Where s.SesionNumero = @nSesionNumero and i.UsuarioTI = @cUsuario))
		Throw 50549, 'Solo un supervisor o el responsable del ticket puede reasignar o tomar investigaciones.', 1
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

		If @cNuevoUsuario <> @cUsuario
			Exec dbo.Usp_TI_Registrar_Notificacion
				@cUsuario = @cNuevoUsuario, @cIncidenciaNumero = @cIncidencia, @cTipo = 'AGENTE_REASIGNADO',
				@cTitulo = N'Investigación asignada', @cMensaje = N'Un supervisor te asignó una investigación del Agente de Ingeniería.', @cRuta = @cRuta
		-- Quien tenía la investigación se entera de que otra persona la tomó.
		If @cAnterior <> @cUsuario and Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cAnterior and Estado = 'A')
			Exec dbo.Usp_TI_Registrar_Notificacion
				@cUsuario = @cAnterior, @cIncidenciaNumero = @cIncidencia, @cTipo = 'AGENTE_REASIGNADO',
				@cTitulo = N'Investigación transferida', @cMensaje = N'Una investigación del Agente de Ingeniería que tenías a cargo pasó a otro operador.', @cRuta = @cRuta

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

Create Or Alter Procedure dbo.Usp_TI_Agente_VincularGrabacionesTicket
/*================================================================================
Objetivo            : Pasar a la investigación los videos adjuntados al ticket (por ejemplo, una grabación que el usuario subió a mano).
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : Idempotente: un adjunto ya vinculado (misma ruta) no se vuelve a registrar.
================================================================================*/
	@cUsuario varchar(20),
	@nSesionNumero bigint
As
Begin
	Set NoCount On
	Set Xact_Abort On
	Declare @cIncidencia varchar(12), @nBase int, @dFecha datetime2(0) = SysDateTime()

	Begin Try
		Begin Transaction
		Select @cIncidencia = IncidenciaNumero From dbo.TI_AgenteSesion With (UpdLock, HoldLock)
		Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario and Estado In ('RECOPILANDO','OBSERVANDO','LISTO_INVESTIGAR')
		If @@RowCount = 0 or @cIncidencia Is Null
		Begin
			Commit Transaction
			Select 0 Vinculadas
			Return
		End

		Select @nBase = IsNull(Max(Secuencia), 0) From dbo.TI_AgenteEvento With (UpdLock, HoldLock) Where SesionNumero = @nSesionNumero
		Insert dbo.TI_AgenteEvento (SesionNumero, Secuencia, Tipo, Fuente, Contenido, DatosJson, Fecha, OrigenServidor)
		Select @nSesionNumero, @nBase + Row_Number() Over (Order By a.Secuencia), 'GRABACION_PANTALLA',
			Case When a.UsuarioRegistro = i.UsuarioSolicitante Then 'LIVE_USUARIO' Else 'LIVE' End,
			Concat(N'Video adjuntado al ticket: ', a.NombreOriginal, N'.'),
			(Select ruta = a.RutaArchivo, nombreOriginal = a.NombreOriginal, tipoMime = a.TipoMime, tamanoBytes = a.TamanoBytes, adjuntoSecuencia = a.Secuencia, usuario = a.UsuarioRegistro
				For Json Path, Without_Array_Wrapper), @dFecha, 1
		From dbo.TI_IncidenciaAdjunto a
		Join dbo.TI_Incidencia i on i.IncidenciaNumero = a.IncidenciaNumero
		Where a.IncidenciaNumero = @cIncidencia and a.TipoMime In ('video/webm','video/mp4')
			and Not Exists (Select 1 From dbo.TI_AgenteEvento e Where e.SesionNumero = @nSesionNumero and e.Tipo = 'GRABACION_PANTALLA'
				and Json_Value(e.DatosJson, '$.ruta') = a.RutaArchivo)
		Declare @nVinculadas int = @@RowCount
		Commit Transaction
		Select @nVinculadas Vinculadas
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go
