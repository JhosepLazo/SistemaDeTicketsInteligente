/*
	Archivo: 15_MisTicketsUsuario.sql
	Objetivo: Crear los Stored Procedures requeridos por el módulo Mis Tickets para usuarios autenticados.
	Responsabilidad: Consultar resumen/listado/detalle y registrar únicamente las acciones que corresponden al usuario: responder observaciones, validar solución, reabrir, calificar y descargar adjuntos propios.
	Dependencias: TI_Incidencia, TI_IncidenciaEstado, TI_IncidenciaAvance, TI_IncidenciaMensaje, TI_IncidenciaAdjunto, TI_IncidenciaDocumento, TI_Usuario, TI_Area, TI_Linea, TI_Item, TI_Tipo, TI_Estado y TI_Auditoria.
	Orden: Ejecutar después de 14_NuevoTicketUsuario.sql.
	Consideraciones: Todas las operaciones validan UsuarioSolicitante; no permiten consultar ni modificar tickets de otro usuario y mantienen historial/auditoría de las acciones realizadas.
*/

Use [SistemaTicketsInteligente]
Go

/* Ejemplo: Exec dbo.Usp_TI_Obtener_MisTicketsUsuario 'USR001' */
Create Or Alter Procedure dbo.Usp_TI_Obtener_MisTicketsUsuario
/*================================================================================
Objetivo            : Obtener el resumen y listado de tickets pertenecientes al usuario autenticado.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Primera versión para la bandeja Mis Tickets del usuario.
================================================================================*/
	@cUsuario varchar(20)
As
Begin
	Set NoCount On

	Select
		Activos = Sum(Case When i.Estado Not In ('RS', 'CA') Then 1 Else 0 End),
		EnAtencion = Sum(Case When i.Estado In ('DG', 'EJ', 'ES', 'PA', 'AU') Then 1 Else 0 End),
		PendientesRespuesta = Sum(Case When i.Estado In ('RC', 'PV') Then 1 Else 0 End),
		Resueltos30Dias = Sum(Case When i.Estado = 'RS' and i.FechaCierre >= DateAdd(day, -30, SysDateTime()) Then 1 Else 0 End)
	From dbo.TI_Incidencia as i
	Where i.UsuarioSolicitante = @cUsuario

	Select
		i.IncidenciaNumero,
		i.Titulo,
		i.Detalle,
		i.Estado,
		EstadoDescripcion = e.Descripcion,
		Responsable = IsNull(u.NombreCompleto, 'Por asignar'),
		i.FechaRegistro,
		i.UltimaFechaModif,
		i.Prioridad,
		Accion = Case
			When i.Estado = 'RC' Then 'RESPONDER_OBSERVACION'
			When i.Estado = 'PV' Then 'CONFIRMAR_SOLUCION'
			When i.Estado = 'RS' and i.Calificacion Is Null Then 'CALIFICAR_ATENCION'
			Else 'VER_DETALLE'
		End
	From dbo.TI_Incidencia as i
	Inner Join dbo.TI_Estado as e on e.Estado = i.Estado
	Left Join dbo.TI_Usuario as u on u.Usuario = i.UsuarioTI
	Where i.UsuarioSolicitante = @cUsuario
	Order By Case When i.Estado In ('RC', 'PV') Then 0 Else 1 End, i.UltimaFechaModif Desc, i.FechaRegistro Desc
End
Go

/* Ejecuta Procedure */

/* Ejemplo: Exec dbo.Usp_TI_Obtener_DetalleTicketUsuario 'USR001', 'INC-000001' */
Create Or Alter Procedure dbo.Usp_TI_Obtener_DetalleTicketUsuario
/*================================================================================
Objetivo            : Obtener toda la información visible para el usuario sobre uno de sus tickets.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Devuelve cabecera, estados, avances, mensajes visibles, adjuntos y documentos relacionados.
================================================================================*/
	@cUsuario varchar(20),
	@cIncidenciaNumero varchar(12)
As
Begin
	Set NoCount On

	Select
		i.IncidenciaNumero,
		i.Titulo,
		i.Detalle,
		i.MensajeError,
		i.Estado,
		EstadoDescripcion = e.Descripcion,
		i.Linea,
		LineaDescripcion = l.Descripcion,
		i.Item,
		ItemDescripcion = IsNull(it.Descripcion, ''),
		i.Tipo,
		TipoDescripcion = t.Descripcion,
		AreaDescripcion = a.Descripcion,
		Responsable = IsNull(u.NombreCompleto, 'Por asignar'),
		i.FechaRegistro,
		i.FechaAsignacion,
		i.FechaAtencion,
		i.FechaCierre,
		i.UltimaFechaModif,
		i.Prioridad,
		i.Calificacion,
		i.ComentarioCalificacion,
		i.RespuestaUsuario,
		Accion = Case
			When i.Estado = 'RC' Then 'RESPONDER_OBSERVACION'
			When i.Estado = 'PV' Then 'CONFIRMAR_SOLUCION'
			When i.Estado = 'RS' and i.Calificacion Is Null Then 'CALIFICAR_ATENCION'
			Else 'VER_DETALLE'
		End
	From dbo.TI_Incidencia as i
	Inner Join dbo.TI_Estado as e on e.Estado = i.Estado
	Inner Join dbo.TI_Linea as l on l.Linea = i.Linea
	Inner Join dbo.TI_Tipo as t on t.Tipo = i.Tipo
	Inner Join dbo.TI_Area as a on a.Area = i.AreaSolicitante
	Left Join dbo.TI_Item as it on it.Item = i.Item
	Left Join dbo.TI_Usuario as u on u.Usuario = i.UsuarioTI
	Where i.IncidenciaNumero = @cIncidenciaNumero and i.UsuarioSolicitante = @cUsuario

	Select
		h.Secuencia,
		h.Estado,
		EstadoDescripcion = e.Descripcion,
		Actor = IsNull(u.NombreCompleto, 'Sistema'),
		Fecha = h.FechaCambio,
		h.Observacion
	From dbo.TI_IncidenciaEstado as h
	Inner Join dbo.TI_Estado as e on e.Estado = h.Estado
	Left Join dbo.TI_Usuario as u on u.Usuario = h.UsuarioCambio
	Where h.IncidenciaNumero = @cIncidenciaNumero
		and Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero and UsuarioSolicitante = @cUsuario)
	Order By h.Secuencia

	Select
		av.Secuencia,
		Responsable = u.NombreCompleto,
		Fecha = av.FechaAvance,
		av.Detalle,
		av.PorcentajeAvance
	From dbo.TI_IncidenciaAvance as av
	Inner Join dbo.TI_Usuario as u on u.Usuario = av.UsuarioTI
	Where av.IncidenciaNumero = @cIncidenciaNumero
		and Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero and UsuarioSolicitante = @cUsuario)
	Order By av.Secuencia

	Select
		m.Secuencia,
		m.TipoAutor,
		Autor = Case When m.TipoAutor = 'S' Then 'Sistema' When m.TipoAutor = 'I' Then 'Asistente TI' Else IsNull(u.NombreCompleto, 'Usuario') End,
		m.Contenido,
		Fecha = m.FechaMensaje
	From dbo.TI_IncidenciaMensaje as m
	Left Join dbo.TI_Usuario as u on u.Usuario = m.UsuarioAutor
	Where m.IncidenciaNumero = @cIncidenciaNumero and m.EsInterno = 0
		and Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero and UsuarioSolicitante = @cUsuario)
	Order By m.Secuencia

	Select
		ad.Secuencia,
		ad.MensajeSecuencia,
		ad.NombreOriginal,
		ad.TipoMime,
		ad.TamanoBytes,
		ad.FechaRegistro
	From dbo.TI_IncidenciaAdjunto as ad
	Left Join dbo.TI_IncidenciaMensaje as m on m.IncidenciaNumero = ad.IncidenciaNumero and m.Secuencia = ad.MensajeSecuencia
	Where ad.IncidenciaNumero = @cIncidenciaNumero and (ad.MensajeSecuencia Is Null or m.EsInterno = 0)
		and Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero and UsuarioSolicitante = @cUsuario)
	Order By ad.Secuencia

	Select d.Secuencia, d.CompaniaSocio, d.TipoDocumento, d.NumeroDocumento, d.Descripcion
	From dbo.TI_IncidenciaDocumento as d
	Where d.IncidenciaNumero = @cIncidenciaNumero
		and Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero and UsuarioSolicitante = @cUsuario)
	Order By d.Secuencia
End
Go

/* Ejecuta Procedure */

/* Ejemplo: Exec dbo.Usp_TI_Responder_ObservacionTicket 'USR001', 'INC-000002', N'Adjunto la evidencia solicitada.', '00000000-0000-0000-0000-000000000001' */
Create Or Alter Procedure dbo.Usp_TI_Responder_ObservacionTicket
/*================================================================================
Objetivo            : Registrar la respuesta del usuario a una observación y devolver el ticket a diagnóstico.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Inserta mensaje visible, cambia RC -> DG, registra historial y auditoría.
================================================================================*/
	@cUsuario varchar(20),
	@cIncidenciaNumero varchar(12),
	@cContenido nvarchar(max),
	@cIdCorrelacion uniqueidentifier
As
Begin
	Set NoCount On

	Declare @nMensajeSecuencia int, @nEstadoSecuencia int, @dFecha datetime2(0) = SysDateTime()

	If Not Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero and UsuarioSolicitante = @cUsuario)
		Throw 50100, 'El ticket no existe o no pertenece al usuario autenticado.', 1
	If Not Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero and UsuarioSolicitante = @cUsuario and Estado = 'RC')
		Throw 50101, 'El ticket ya no se encuentra esperando información del usuario.', 1
	If NullIf(LTrim(RTrim(@cContenido)), '') Is Null Throw 50102, 'La respuesta no puede estar vacía.', 1

	Select @nMensajeSecuencia = IsNull(Max(Secuencia), 0) + 1
	From dbo.TI_IncidenciaMensaje With (UpdLock, HoldLock)
	Where IncidenciaNumero = @cIncidenciaNumero

	Insert dbo.TI_IncidenciaMensaje (IncidenciaNumero, Secuencia, UsuarioAutor, TipoAutor, Contenido, FechaMensaje, EsInterno)
	Values (@cIncidenciaNumero, @nMensajeSecuencia, @cUsuario, 'U', LTrim(RTrim(@cContenido)), @dFecha, 0)

	Update dbo.TI_Incidencia
	Set Estado = 'DG', UltimoUsuario = @cUsuario, UltimaFechaModif = @dFecha
	Where IncidenciaNumero = @cIncidenciaNumero and UsuarioSolicitante = @cUsuario

	Select @nEstadoSecuencia = IsNull(Max(Secuencia), 0) + 1
	From dbo.TI_IncidenciaEstado With (UpdLock, HoldLock)
	Where IncidenciaNumero = @cIncidenciaNumero

	Insert dbo.TI_IncidenciaEstado (IncidenciaNumero, Secuencia, Estado, UsuarioCambio, FechaCambio, Observacion)
	Values (@cIncidenciaNumero, @nEstadoSecuencia, 'DG', @cUsuario, @dFecha, N'El usuario respondió la solicitud de información adicional.')

	Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
	Values (@cIncidenciaNumero, @cUsuario, 'U', 'TI_IncidenciaMensaje', @cIncidenciaNumero, 'RESPONDER_OBSERVACION', 'EXITOSO', Null, @cIdCorrelacion, @dFecha)

	Select @nMensajeSecuencia as MensajeSecuencia
End
Go

/* Ejecuta Procedure */

/* Ejemplo: Exec dbo.Usp_TI_Registrar_AdjuntoMensajeUsuario 'USR001', 'INC-000002', 1, 'captura.png', 'archivo.png', 'uploads/incidencias/archivo.png', 'image/png', 10000 */
Create Or Alter Procedure dbo.Usp_TI_Registrar_AdjuntoMensajeUsuario
/*================================================================================
Objetivo            : Registrar metadata de un adjunto enviado por el usuario dentro de un mensaje de su ticket.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : dbo.Usp_TI_Registrar_IncidenciaAdjunto
Comentario Cambios  : Vincula la evidencia a MensajeSecuencia y valida propiedad/autoria antes de insertar.
================================================================================*/
	@cUsuario varchar(20),
	@cIncidenciaNumero varchar(12),
	@nMensajeSecuencia int,
	@cNombreOriginal nvarchar(260),
	@cNombreArchivo nvarchar(260),
	@cRutaArchivo nvarchar(1000),
	@cTipoMime varchar(100),
	@nTamanoBytes bigint
As
Begin
	Set NoCount On

	Declare @nSecuencia int, @dFecha datetime2(0) = SysDateTime()

	If Not Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero and UsuarioSolicitante = @cUsuario)
		Throw 50103, 'El ticket no pertenece al usuario autenticado.', 1
	If Not Exists (
		Select 1 From dbo.TI_IncidenciaMensaje
		Where IncidenciaNumero = @cIncidenciaNumero and Secuencia = @nMensajeSecuencia and UsuarioAutor = @cUsuario and TipoAutor = 'U'
	) Throw 50104, 'El mensaje indicado no pertenece al usuario autenticado.', 1

	Select @nSecuencia = IsNull(Max(Secuencia), 0) + 1
	From dbo.TI_IncidenciaAdjunto With (UpdLock, HoldLock)
	Where IncidenciaNumero = @cIncidenciaNumero

	Insert dbo.TI_IncidenciaAdjunto (
		IncidenciaNumero, Secuencia, MensajeSecuencia, UsuarioRegistro, NombreOriginal, NombreArchivo, RutaArchivo, TipoMime, TamanoBytes, FechaRegistro
	)
	Values (
		@cIncidenciaNumero, @nSecuencia, @nMensajeSecuencia, @cUsuario, @cNombreOriginal, @cNombreArchivo, @cRutaArchivo, @cTipoMime, @nTamanoBytes, @dFecha
	)
End
Go

/* Ejecuta Procedure */

/* Ejemplo: Exec dbo.Usp_TI_Validar_SolucionTicket 'USR001', 'INC-000001', 1, N'La solución funcionó.', '00000000-0000-0000-0000-000000000001' */
Create Or Alter Procedure dbo.Usp_TI_Validar_SolucionTicket
/*================================================================================
Objetivo            : Confirmar una solución o reabrir el ticket cuando el problema continúa.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Sustituye la confirmación separada del sistema legado por una acción contextual y auditable.
================================================================================*/
	@cUsuario varchar(20),
	@cIncidenciaNumero varchar(12),
	@lSolucionada bit,
	@cComentario nvarchar(1000) = Null,
	@cIdCorrelacion uniqueidentifier
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Declare @cEstado char(2) = Case When @lSolucionada = 1 Then 'RS' Else 'RA' End
	Declare @cMensaje nvarchar(1000), @nEstadoSecuencia int, @nMensajeSecuencia int, @dFecha datetime2(0) = SysDateTime()

	If Not Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero and UsuarioSolicitante = @cUsuario)
		Throw 50105, 'El ticket no existe o no pertenece al usuario autenticado.', 1
	If Not Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero and UsuarioSolicitante = @cUsuario and Estado = 'PV')
		Throw 50106, 'El ticket ya no se encuentra pendiente de validación.', 1
	If @lSolucionada = 0 and NullIf(LTrim(RTrim(@cComentario)), '') Is Null
		Throw 50107, 'Indica qué problema continúa para poder reabrir el ticket.', 1

	Set @cMensaje = Case
		When @lSolucionada = 1 Then IsNull(NullIf(LTrim(RTrim(@cComentario)), ''), N'El usuario confirmó que la solución funciona correctamente.')
		Else LTrim(RTrim(@cComentario))
	End

	Begin Transaction
	Begin Try
		Update dbo.TI_Incidencia
		Set Estado = @cEstado,
			FechaCierre = Case When @lSolucionada = 1 Then @dFecha Else Null End,
			RespuestaUsuario = @cMensaje,
			UltimoUsuario = @cUsuario,
			UltimaFechaModif = @dFecha
		Where IncidenciaNumero = @cIncidenciaNumero and UsuarioSolicitante = @cUsuario

		Select @nMensajeSecuencia = IsNull(Max(Secuencia), 0) + 1
		From dbo.TI_IncidenciaMensaje With (UpdLock, HoldLock)
		Where IncidenciaNumero = @cIncidenciaNumero

		Insert dbo.TI_IncidenciaMensaje (IncidenciaNumero, Secuencia, UsuarioAutor, TipoAutor, Contenido, FechaMensaje, EsInterno)
		Values (@cIncidenciaNumero, @nMensajeSecuencia, @cUsuario, 'U', @cMensaje, @dFecha, 0)

		Select @nEstadoSecuencia = IsNull(Max(Secuencia), 0) + 1
		From dbo.TI_IncidenciaEstado With (UpdLock, HoldLock)
		Where IncidenciaNumero = @cIncidenciaNumero

		Insert dbo.TI_IncidenciaEstado (IncidenciaNumero, Secuencia, Estado, UsuarioCambio, FechaCambio, Observacion)
		Values (
			@cIncidenciaNumero,
			@nEstadoSecuencia,
			@cEstado,
			@cUsuario,
			@dFecha,
			Case When @lSolucionada = 1 Then N'El usuario confirmó la solución.' Else N'El usuario indicó que el problema continúa y reabrió el ticket.' End
		)

		Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
		Values (
			@cIncidenciaNumero,
			@cUsuario,
			'U',
			'TI_Incidencia',
			@cIncidenciaNumero,
			Case When @lSolucionada = 1 Then 'CONFIRMAR_SOLUCION' Else 'REABRIR_TICKET' End,
			'EXITOSO',
			Null,
			@cIdCorrelacion,
			@dFecha
		)

		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go

/* Ejecuta Procedure */

/* Ejemplo: Exec dbo.Usp_TI_Calificar_TicketUsuario 'USR001', 'INC-000001', 5, N'Buena atención.', '00000000-0000-0000-0000-000000000001' */
Create Or Alter Procedure dbo.Usp_TI_Calificar_TicketUsuario
/*================================================================================
Objetivo            : Registrar la calificación final del usuario sobre un ticket resuelto.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Integra la encuesta del sistema legado dentro del detalle del ticket.
================================================================================*/
	@cUsuario varchar(20),
	@cIncidenciaNumero varchar(12),
	@nCalificacion tinyint,
	@cComentario nvarchar(500) = Null,
	@cIdCorrelacion uniqueidentifier
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Declare @dFecha datetime2(0) = SysDateTime()

	If @nCalificacion Not Between 1 and 5 Throw 50108, 'La calificación debe estar entre 1 y 5.', 1
	If Not Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero and UsuarioSolicitante = @cUsuario)
		Throw 50109, 'El ticket no existe o no pertenece al usuario autenticado.', 1
	If Not Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero and UsuarioSolicitante = @cUsuario and Estado = 'RS')
		Throw 50110, 'Solo puedes calificar tickets resueltos.', 1
	If Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero and UsuarioSolicitante = @cUsuario and Calificacion Is Not Null)
		Throw 50111, 'Este ticket ya fue calificado.', 1

	Begin Transaction
	Begin Try
		Update dbo.TI_Incidencia
		Set Calificacion = @nCalificacion,
			ComentarioCalificacion = NullIf(LTrim(RTrim(@cComentario)), ''),
			UltimoUsuario = @cUsuario,
			UltimaFechaModif = @dFecha
		Where IncidenciaNumero = @cIncidenciaNumero and UsuarioSolicitante = @cUsuario

		Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
		Values (@cIncidenciaNumero, @cUsuario, 'U', 'TI_Incidencia', @cIncidenciaNumero, 'CALIFICAR_ATENCION', 'EXITOSO', Null, @cIdCorrelacion, @dFecha)

		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go

/* Ejecuta Procedure */

/* Ejemplo: Exec dbo.Usp_TI_Obtener_AdjuntoTicketUsuario 'USR001', 'INC-000001', 1 */
Create Or Alter Procedure dbo.Usp_TI_Obtener_AdjuntoTicketUsuario
/*================================================================================
Objetivo            : Obtener la ubicación de un adjunto solo cuando pertenece a un ticket del usuario autenticado.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Primera versión para descarga controlada de evidencias desde Mis Tickets.
================================================================================*/
	@cUsuario varchar(20),
	@cIncidenciaNumero varchar(12),
	@nSecuencia int
As
Begin
	Set NoCount On

	Select ad.NombreOriginal, ad.RutaArchivo, ad.TipoMime
	From dbo.TI_IncidenciaAdjunto as ad
	Inner Join dbo.TI_Incidencia as i on i.IncidenciaNumero = ad.IncidenciaNumero
	Left Join dbo.TI_IncidenciaMensaje as m on m.IncidenciaNumero = ad.IncidenciaNumero and m.Secuencia = ad.MensajeSecuencia
	Where ad.IncidenciaNumero = @cIncidenciaNumero and ad.Secuencia = @nSecuencia and i.UsuarioSolicitante = @cUsuario
		and (ad.MensajeSecuencia Is Null or m.EsInterno = 0)
End
Go

/* Ejecuta Procedure */
