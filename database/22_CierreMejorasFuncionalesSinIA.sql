/*
	Archivo: 22_CierreMejorasFuncionalesSinIA.sql
	Objetivo: Cerrar las brechas operativas detectadas al validar las mejoras funcionales sin IA.
	Responsabilidad: Notificar avances visibles y respuestas del usuario, y exponer esfuerzo efectivo con los mismos filtros de Reportes TI.
	Dependencias: Requiere 19_MejorasFuncionalesSinIA.sql, 20_AjustesOperativosSinIA.sql y 21_CorreccionesCompatibilidadSinIA.sql.
	Orden: Ejecutar despues de 21_CorreccionesCompatibilidadSinIA.sql.
	Consideraciones: No agrega modulos ni automatizaciones inteligentes. Conserva los flujos y tablas existentes.
*/

Use [SistemaTicketsInteligente]
Go

If Not Exists (Select 1 From sys.indexes Where object_id = Object_Id('dbo.TI_IncidenciaAvance') and name = 'IX_TI_IncidenciaAvance_FechaUsuario')
	Create Index IX_TI_IncidenciaAvance_FechaUsuario
	On dbo.TI_IncidenciaAvance (FechaAvance, UsuarioTI, IncidenciaNumero)
	Include (TiempoUtilizado)
Go

Create Or Alter Procedure dbo.Usp_TI_Registrar_AvanceTicket
/*================================================================================
Objetivo            : Registrar un avance tecnico y avisar al usuario cuando el contenido sea visible para el.
Creado Por          : Jhosep S. Lazo
Fecha Creacion      : 13/09/2026
SP Anterior         : dbo.Usp_TI_Registrar_AvanceTicket
Comentario Cambios  : Vincula el avance visible con una notificacion accionable sin duplicar avances internos.
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3),
	@cIncidenciaNumero varchar(12),
	@cDetalle nvarchar(max),
	@lVisibleUsuario bit = 0,
	@nTiempoUtilizado decimal(8,2) = Null,
	@cAreaCausante char(3) = Null,
	@cIdCorrelacion uniqueidentifier
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Begin Try
		Begin Transaction

		Declare @nAvance int, @nMensaje int, @nEstado int, @cEstado char(2), @cUsuarioSolicitante varchar(20), @cMensajeNotificacion nvarchar(500), @dFecha datetime2(0) = SysDateTime()

		If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM')) Throw 50216, 'El operador TI no es valido.', 1
		If NullIf(LTrim(RTrim(@cDetalle)), '') Is Null Throw 50217, 'El detalle del avance no puede estar vacio.', 1
		If @nTiempoUtilizado Is Null or @nTiempoUtilizado <= 0 or @nTiempoUtilizado > 1440 Throw 50234, 'Registra el tiempo efectivo utilizado en minutos (1 a 1440).', 1
		If @cAreaCausante Is Null or Not Exists (Select 1 From dbo.TI_Area Where Area = @cAreaCausante and Estado = 'A') Throw 50235, 'Selecciona el area causante del avance.', 1

		Select @cEstado = Estado, @cUsuarioSolicitante = UsuarioSolicitante
		From dbo.TI_Incidencia With (UpdLock, HoldLock)
		Where IncidenciaNumero = @cIncidenciaNumero

		If @cEstado Is Null or @cEstado In ('RS','CA','PA') Throw 50218, 'El ticket no existe, esta cerrado o espera una aprobacion.', 1

		Select @nAvance = IsNull(Max(Secuencia), 0) + 1
		From dbo.TI_IncidenciaAvance With (UpdLock, HoldLock)
		Where IncidenciaNumero = @cIncidenciaNumero

		Insert dbo.TI_IncidenciaAvance (IncidenciaNumero, Secuencia, UsuarioTI, FechaAvance, Detalle, TiempoUtilizado, PorcentajeAvance, AreaCausante)
		Values (@cIncidenciaNumero, @nAvance, @cUsuario, @dFecha, LTrim(RTrim(@cDetalle)), @nTiempoUtilizado, Null, @cAreaCausante)

		If @lVisibleUsuario = 1
		Begin
			Select @nMensaje = IsNull(Max(Secuencia), 0) + 1
			From dbo.TI_IncidenciaMensaje With (UpdLock, HoldLock)
			Where IncidenciaNumero = @cIncidenciaNumero

			Insert dbo.TI_IncidenciaMensaje (IncidenciaNumero, Secuencia, UsuarioAutor, TipoAutor, Contenido, FechaMensaje, EsInterno)
			Values (@cIncidenciaNumero, @nMensaje, @cUsuario, 'T', LTrim(RTrim(@cDetalle)), @dFecha, 0)

			Set @cMensajeNotificacion = Concat(N'TI registro un nuevo avance visible en el ticket ', @cIncidenciaNumero, N'.')
			Exec dbo.Usp_TI_Registrar_Notificacion
				@cUsuario = @cUsuarioSolicitante,
				@cIncidenciaNumero = @cIncidenciaNumero,
				@cTipo = 'AVANCE_VISIBLE',
				@cTitulo = N'Nuevo avance de TI',
				@cMensaje = @cMensajeNotificacion,
				@cRuta = '/mis-tickets'
		End

		Update dbo.TI_Incidencia
		Set AreaCausante = @cAreaCausante,
			FechaAtencion = IsNull(FechaAtencion, @dFecha),
			Estado = Case When @cEstado In ('NV','RA','ES') Then 'DG' Else Estado End,
			UltimoUsuario = @cUsuario,
			UltimaFechaModif = @dFecha
		Where IncidenciaNumero = @cIncidenciaNumero

		If @cEstado In ('NV','RA','ES')
		Begin
			Select @nEstado = IsNull(Max(Secuencia), 0) + 1
			From dbo.TI_IncidenciaEstado With (UpdLock, HoldLock)
			Where IncidenciaNumero = @cIncidenciaNumero

			Insert dbo.TI_IncidenciaEstado (IncidenciaNumero, Secuencia, Estado, UsuarioCambio, FechaCambio, Observacion)
			Values (@cIncidenciaNumero, @nEstado, 'DG', @cUsuario, @dFecha, N'Se inicio o retomo la atencion tecnica del ticket.')
		End

		Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
		Values (@cIncidenciaNumero, @cUsuario, 'T', 'TI_IncidenciaAvance', Concat(@cIncidenciaNumero, '-', @nAvance), 'REGISTRAR_AVANCE', 'EXITOSO', Concat('{"minutos":', Convert(varchar(30), @nTiempoUtilizado), ',"areaCausante":"', @cAreaCausante, '"}'), @cIdCorrelacion, @dFecha)

		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go

Create Or Alter Trigger dbo.Tr_TI_Incidencia_NotificacionRespuestaUsuario
On dbo.TI_Incidencia
After Update
As
Begin
	Set NoCount On

	Insert dbo.TI_Notificacion (Usuario, IncidenciaNumero, Tipo, Titulo, Mensaje, Ruta, Fecha)
	Select i.UsuarioTI, i.IncidenciaNumero, 'RESPUESTA_USUARIO', N'El usuario respondio',
		Concat(N'El usuario envio la informacion solicitada para el ticket ', i.IncidenciaNumero, N'.'), '/gestion-tickets', SysDateTime()
	From inserted as i
	Inner Join deleted as d on d.IncidenciaNumero = i.IncidenciaNumero
	Where d.Estado = 'RC' and i.Estado = 'DG' and i.UsuarioTI Is Not Null

	Insert dbo.TI_Notificacion (Usuario, IncidenciaNumero, Tipo, Titulo, Mensaje, Ruta, Fecha)
	Select u.Usuario, i.IncidenciaNumero, 'RESPUESTA_USUARIO', N'El usuario respondio',
		Concat(N'El usuario envio la informacion solicitada para el ticket ', i.IncidenciaNumero, N'.'), '/gestion-tickets', SysDateTime()
	From inserted as i
	Inner Join deleted as d on d.IncidenciaNumero = i.IncidenciaNumero
	Inner Join dbo.TI_Usuario as u on u.Area = i.AreaTI and u.Estado = 'A' and u.Perfil In ('TEC','SUP','ADM')
	Where d.Estado = 'RC' and i.Estado = 'DG' and i.UsuarioTI Is Null
End
Go

Create Or Alter Trigger dbo.Tr_TI_SolicitudAprobacion_SeparacionFunciones
On dbo.TI_SolicitudAprobacion
After Update
As
Begin
	Set NoCount On

	If Exists (
		Select 1
		From inserted as i
		Inner Join deleted as d on d.IncidenciaNumero = i.IncidenciaNumero and d.Secuencia = i.Secuencia
		Where d.Estado = 'P' and i.Estado In ('A','R') and i.UsuarioAprobador = i.UsuarioSolicitante
	)
		Throw 50452, 'La persona que solicito la aprobacion no puede responder su propia solicitud.', 1
End
Go

Create Or Alter Procedure dbo.Usp_TI_Obtener_EsfuerzoOperativoTI
/*================================================================================
Objetivo            : Resumir el tiempo efectivo registrado por TI aplicando los filtros del reporte.
Creado Por          : Jhosep S. Lazo
Fecha Creacion      : 13/09/2026
SP Anterior         : dbo.Usp_TI_Obtener_EsfuerzoOperativoTI
Comentario Cambios  : El esfuerzo deja de quedar aislado y puede compararse con area, estado, prioridad, tipo y operador.
================================================================================*/
	@dFechaInicio date,
	@dFechaFin date,
	@cArea char(3) = Null,
	@cEstado char(2) = Null,
	@cPrioridad varchar(10) = Null,
	@cTipo char(3) = Null,
	@cUsuarioTI varchar(20) = Null
As
Begin
	Set NoCount On

	If @dFechaInicio Is Null or @dFechaFin Is Null or @dFechaInicio > @dFechaFin Throw 50450, 'El rango de fechas no es valido.', 1
	If DateDiff(day, @dFechaInicio, @dFechaFin) > 365 Throw 50451, 'El rango maximo permitido es de 366 dias.', 1

	Select
		MinutosEfectivos = Cast(IsNull(Sum(av.TiempoUtilizado), 0) as decimal(18,2)),
		HorasEfectivas = Cast(IsNull(Sum(av.TiempoUtilizado), 0) / 60.0 as decimal(18,2)),
		TicketsConEsfuerzo = Count(Distinct av.IncidenciaNumero)
	From dbo.TI_IncidenciaAvance as av
	Inner Join dbo.TI_Incidencia as i on i.IncidenciaNumero = av.IncidenciaNumero
	Where av.FechaAvance >= @dFechaInicio and av.FechaAvance < DateAdd(day, 1, @dFechaFin)
		and (@cArea Is Null or @cArea = '' or i.AreaSolicitante = @cArea)
		and (@cEstado Is Null or @cEstado = '' or i.Estado = @cEstado)
		and (@cTipo Is Null or @cTipo = '' or i.Tipo = @cTipo)
		and (
			@cUsuarioTI Is Null or @cUsuarioTI = ''
			or (@cUsuarioTI = 'SIN_ASIGNAR' and i.UsuarioTI Is Null)
			or av.UsuarioTI = @cUsuarioTI
		)
		and (
			@cPrioridad Is Null or @cPrioridad = ''
			or (@cPrioridad = 'ALTA' and IsNull(i.Prioridad, 0) >= 4)
			or (@cPrioridad = 'MEDIA' and i.Prioridad = 3)
			or (@cPrioridad = 'BAJA' and i.Prioridad Between 1 and 2)
			or (@cPrioridad = 'SIN' and i.Prioridad Is Null)
		)
End
Go

/* Ejecuta Procedure */
