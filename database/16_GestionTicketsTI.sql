/*
	Archivo: 16_GestionTicketsTI.sql
	Objetivo: Crear los Stored Procedures requeridos por el módulo Gestión de Tickets para operadores TI.
	Responsabilidad: Consolidar bandeja, detalle técnico, clasificación, asignación, avances, solicitudes de información, resolución, No Procede, aprobaciones y descarga de evidencias.
	Dependencias: TI_Incidencia, maestros TI_*, TI_IncidenciaEstado, TI_IncidenciaAvance, TI_IncidenciaMensaje, TI_IncidenciaAdjunto, TI_IncidenciaDocumento, TI_SolicitudAprobacion, TI_Accion y TI_Auditoria.
	Orden: Ejecutar después de 15_MisTicketsUsuario.sql.
	Consideraciones: El operador se valida siempre desde la sesión/backend; ninguna operación ejecuta SQL libre ni cierra una incidencia sin validación del usuario cuando la solución fue enviada a confirmar.
*/

Use [SistemaTicketsInteligente]
Go

/* Ejemplo: Exec dbo.Usp_TI_Obtener_GestionTicketsTI 'TEC001', 'TIC' */
Create Or Alter Procedure dbo.Usp_TI_Obtener_GestionTicketsTI
/*================================================================================
Objetivo            : Obtener resumen, bandeja y catálogos operativos para Gestión de Tickets TI.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Primera versión del módulo Gestión de Tickets para operadores TI.
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3)
As
Begin
	Set NoCount On

	If Not Exists (
		Select 1
		From dbo.TI_Usuario as u
		Where u.Usuario = @cUsuario and u.Area = @cArea and u.Estado = 'A' and u.Perfil In ('TEC', 'SUP', 'ADM')
	) Throw 50200, 'El usuario autenticado no tiene acceso a Gestión de Tickets TI.', 1

	Select
		Pendientes = Sum(Case When i.Estado In ('NV', 'RC', 'PA', 'RA') Then 1 Else 0 End),
		EnAtencion = Sum(Case When i.Estado In ('DG', 'AU', 'EJ', 'ES') Then 1 Else 0 End),
		PorVencer = Sum(Case When i.Estado Not In ('RS', 'CA') and i.SlaObjetivoMinutos Is Not Null and DateAdd(minute, i.SlaObjetivoMinutos, i.FechaRegistro) Between SysDateTime() and DateAdd(hour, 24, SysDateTime()) Then 1 Else 0 End),
		Reabiertos = Sum(Case When i.Estado = 'RA' Then 1 Else 0 End),
		SinAsignar = Sum(Case When i.Estado Not In ('RS', 'CA') and i.UsuarioTI Is Null Then 1 Else 0 End),
		MisAsignados = Sum(Case When i.Estado Not In ('RS', 'CA') and i.UsuarioTI = @cUsuario Then 1 Else 0 End),
		PrioridadAlta = Sum(Case When i.Estado Not In ('RS', 'CA') and IsNull(i.Prioridad, 0) >= 4 Then 1 Else 0 End),
		AprobacionesPendientes = (
			Select Count(*)
			From dbo.TI_SolicitudAprobacion as s
			Where s.Estado = 'P'
		),
		Total = Count(*)
	From dbo.TI_Incidencia as i

	Select
		i.IncidenciaNumero,
		i.UsuarioSolicitante,
		Solicitante = us.NombreCompleto,
		i.Titulo,
		i.Detalle,
		i.AreaSolicitante,
		AreaDescripcion = ar.Descripcion,
		i.Linea,
		LineaDescripcion = l.Descripcion,
		i.Tipo,
		TipoDescripcion = t.Descripcion,
		i.Estado,
		EstadoDescripcion = e.Descripcion,
		i.Prioridad,
		i.Impacto,
		i.Complejidad,
		i.UsuarioTI,
		Responsable = IsNull(ut.NombreCompleto, 'Sin asignar'),
		i.FechaRegistro,
		i.UltimaFechaModif,
		SlaMinutosRestantes = Case When i.SlaObjetivoMinutos Is Null Then Null Else DateDiff(minute, SysDateTime(), DateAdd(minute, i.SlaObjetivoMinutos, i.FechaRegistro)) End,
		TieneAprobacionPendiente = Convert(bit, Case When Exists (Select 1 From dbo.TI_SolicitudAprobacion as sa Where sa.IncidenciaNumero = i.IncidenciaNumero and sa.Estado = 'P') Then 1 Else 0 End),
		Accion = Case When i.UsuarioTI Is Null and i.Estado Not In ('RS', 'CA') Then 'ASIGNAR' When i.Estado In ('RS', 'CA') Then 'VER_DETALLE' Else 'CONTINUAR' End
	From dbo.TI_Incidencia as i
	Inner Join dbo.TI_Usuario as us on us.Usuario = i.UsuarioSolicitante
	Inner Join dbo.TI_Area as ar on ar.Area = i.AreaSolicitante
	Inner Join dbo.TI_Linea as l on l.Linea = i.Linea
	Inner Join dbo.TI_Tipo as t on t.Tipo = i.Tipo
	Inner Join dbo.TI_Estado as e on e.Estado = i.Estado
	Left Join dbo.TI_Usuario as ut on ut.Usuario = i.UsuarioTI
	Order By Case When i.Estado In ('NV', 'RA', 'PA', 'RC') Then 0 Else 1 End, IsNull(i.Prioridad, 0) Desc, i.UltimaFechaModif Desc

	Select Codigo = Estado, Descripcion From dbo.TI_Estado Order By Orden
	Select Codigo = Area, Descripcion From dbo.TI_Area Where Estado = 'A' Order By Descripcion
	Select Codigo = Usuario, Descripcion = NombreCompleto From dbo.TI_Usuario Where Estado = 'A' and Area = @cArea and Perfil In ('TEC', 'SUP', 'ADM') Order By NombreCompleto
	Select Codigo = Linea, Descripcion, Area From dbo.TI_Linea Where Estado = 'A' Order By Descripcion
	Select Codigo = Item, Descripcion, Linea From dbo.TI_Item Where Estado = 'A' Order By Descripcion
	Select Codigo = Tipo, Descripcion From dbo.TI_Tipo Where Estado = 'A' Order By Descripcion
	Select Codigo = Categoria, Descripcion From dbo.TI_Categoria Where Estado = 'A' Order By Descripcion
	Select Codigo = SubTipo, Descripcion, Tipo, Categoria From dbo.TI_SubTipo Where Estado = 'A' Order By Tipo, Categoria, Descripcion
End
Go

/* Ejecuta Procedure */

/* Ejemplo: Exec dbo.Usp_TI_Obtener_DetalleGestionTicketTI 'TEC001', 'TIC', 'INC-000001' */
Create Or Alter Procedure dbo.Usp_TI_Obtener_DetalleGestionTicketTI
/*================================================================================
Objetivo            : Obtener el detalle técnico y la trazabilidad completa de un ticket para el operador TI.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Devuelve cabecera, estados, avances, mensajes, adjuntos, documentos y aprobaciones.
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3),
	@cIncidenciaNumero varchar(12)
As
Begin
	Set NoCount On

	If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC', 'SUP', 'ADM'))
		Throw 50201, 'El usuario autenticado no tiene acceso al detalle técnico.', 1
	If Not Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero)
		Throw 50202, 'El ticket indicado no existe.', 1

	Select
		i.IncidenciaNumero, i.UsuarioSolicitante, Solicitante = us.NombreCompleto, CorreoSolicitante = IsNull(us.Correo, ''),
		i.AreaSolicitante, AreaSolicitanteDescripcion = ars.Descripcion, i.AreaTI, AreaTIDescripcion = IsNull(arti.Descripcion, ''),
		i.UsuarioTI, Responsable = IsNull(ut.NombreCompleto, 'Sin asignar'), i.UsuarioAsigno,
		i.Linea, LineaDescripcion = l.Descripcion, i.Item, ItemDescripcion = IsNull(it.Descripcion, ''),
		i.Tipo, TipoDescripcion = t.Descripcion, i.SubTipo, SubTipoDescripcion = IsNull(st.Descripcion, ''), i.Categoria, CategoriaDescripcion = IsNull(c.Descripcion, ''),
		i.Estado, EstadoDescripcion = e.Descripcion, i.AreaCausante, AreaCausanteDescripcion = IsNull(ac.Descripcion, ''),
		i.Titulo, i.Detalle, i.MensajeError, i.FechaRegistro, i.FechaAsignacion, i.FechaAtencion, i.FechaCierre,
		i.SlaObjetivoMinutos, i.Prioridad, i.Impacto, i.Complejidad, i.CanalRegistro, i.CausaRaiz, i.SolucionTecnica, i.RespuestaUsuario, i.TipoResolucion,
		i.Calificacion, i.ComentarioCalificacion, i.UltimaFechaModif,
		SlaMinutosRestantes = Case When i.SlaObjetivoMinutos Is Null Then Null Else DateDiff(minute, SysDateTime(), DateAdd(minute, i.SlaObjetivoMinutos, i.FechaRegistro)) End
	From dbo.TI_Incidencia as i
	Inner Join dbo.TI_Usuario as us on us.Usuario = i.UsuarioSolicitante
	Inner Join dbo.TI_Area as ars on ars.Area = i.AreaSolicitante
	Inner Join dbo.TI_Linea as l on l.Linea = i.Linea
	Inner Join dbo.TI_Tipo as t on t.Tipo = i.Tipo
	Inner Join dbo.TI_Estado as e on e.Estado = i.Estado
	Left Join dbo.TI_Area as arti on arti.Area = i.AreaTI
	Left Join dbo.TI_Usuario as ut on ut.Usuario = i.UsuarioTI
	Left Join dbo.TI_Item as it on it.Item = i.Item
	Left Join dbo.TI_SubTipo as st on st.Tipo = i.Tipo and st.SubTipo = i.SubTipo and st.Categoria = i.Categoria
	Left Join dbo.TI_Categoria as c on c.Categoria = i.Categoria
	Left Join dbo.TI_Area as ac on ac.Area = i.AreaCausante
	Where i.IncidenciaNumero = @cIncidenciaNumero

	Select h.Secuencia, h.Estado, EstadoDescripcion = e.Descripcion, Actor = IsNull(u.NombreCompleto, 'Sistema'), h.FechaCambio, h.Observacion
	From dbo.TI_IncidenciaEstado as h
	Inner Join dbo.TI_Estado as e on e.Estado = h.Estado
	Left Join dbo.TI_Usuario as u on u.Usuario = h.UsuarioCambio
	Where h.IncidenciaNumero = @cIncidenciaNumero
	Order By h.Secuencia

	Select av.Secuencia, av.UsuarioTI, Responsable = u.NombreCompleto, av.FechaAvance, av.Detalle, av.TiempoUtilizado, av.PorcentajeAvance
	From dbo.TI_IncidenciaAvance as av
	Inner Join dbo.TI_Usuario as u on u.Usuario = av.UsuarioTI
	Where av.IncidenciaNumero = @cIncidenciaNumero
	Order By av.Secuencia

	Select m.Secuencia, m.TipoAutor, Autor = Case When m.TipoAutor = 'S' Then 'Sistema' When m.TipoAutor = 'I' Then 'Asistente TI' Else IsNull(u.NombreCompleto, 'Usuario') End,
		m.Contenido, m.FechaMensaje, m.EsInterno
	From dbo.TI_IncidenciaMensaje as m
	Left Join dbo.TI_Usuario as u on u.Usuario = m.UsuarioAutor
	Where m.IncidenciaNumero = @cIncidenciaNumero
	Order By m.Secuencia

	Select ad.Secuencia, ad.MensajeSecuencia, ad.NombreOriginal, ad.TipoMime, ad.TamanoBytes, ad.FechaRegistro, ad.UsuarioRegistro
	From dbo.TI_IncidenciaAdjunto as ad
	Where ad.IncidenciaNumero = @cIncidenciaNumero
	Order By ad.Secuencia

	Select d.Secuencia, d.CompaniaSocio, d.TipoDocumento, d.NumeroDocumento, d.Descripcion
	From dbo.TI_IncidenciaDocumento as d
	Where d.IncidenciaNumero = @cIncidenciaNumero
	Order By d.Secuencia

	Select s.Secuencia, s.AccionCodigo, AccionNombre = a.Nombre, a.NivelRiesgo, s.Estado, s.Justificacion, s.ComentarioRespuesta,
		s.UsuarioSolicitante, Solicitante = us.NombreCompleto, s.UsuarioAprobador, Aprobador = IsNull(ua.NombreCompleto, ''), s.FechaSolicitud, s.FechaRespuesta
	From dbo.TI_SolicitudAprobacion as s
	Inner Join dbo.TI_Accion as a on a.AccionCodigo = s.AccionCodigo
	Inner Join dbo.TI_Usuario as us on us.Usuario = s.UsuarioSolicitante
	Left Join dbo.TI_Usuario as ua on ua.Usuario = s.UsuarioAprobador
	Where s.IncidenciaNumero = @cIncidenciaNumero
	Order By s.Secuencia Desc
End
Go

/* Ejecuta Procedure */

/* Ejemplo: Exec dbo.Usp_TI_Clasificar_Ticket 'TEC001', 'TIC', 'INC-000002', 'CMP', 'REQUISICION', 'INC', 'PRC', 'PROCESO', 'CMP', 4, 4, 3, '00000000-0000-0000-0000-000000000001' */
Create Or Alter Procedure dbo.Usp_TI_Clasificar_Ticket
/*================================================================================
Objetivo            : Registrar o corregir la clasificación técnica de una incidencia.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Conserva la clasificación del sistema legado sin crear una pantalla separada de asignación.
================================================================================*/
	@cUsuario varchar(20), @cArea char(3), @cIncidenciaNumero varchar(12), @cLinea char(3), @cItem varchar(20),
	@cTipo char(3), @cSubTipo char(3), @cCategoria varchar(20), @cAreaCausante char(3) = Null,
	@nPrioridad int = Null, @nImpacto int = Null, @nComplejidad int = Null, @cIdCorrelacion uniqueidentifier
As
Begin
	Set NoCount On

	If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC', 'SUP', 'ADM')) Throw 50203, 'El operador TI no es válido.', 1
	If Not Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero and Estado Not In ('RS', 'CA')) Throw 50204, 'El ticket no existe o ya se encuentra cerrado.', 1
	If Not Exists (Select 1 From dbo.TI_Item Where Item = @cItem and Linea = @cLinea and Estado = 'A') Throw 50205, 'El item no corresponde a la línea seleccionada.', 1
	If Not Exists (Select 1 From dbo.TI_SubTipo Where Tipo = @cTipo and SubTipo = @cSubTipo and Categoria = @cCategoria and Estado = 'A') Throw 50206, 'La combinación de tipo, subtipo y categoría no es válida.', 1
	If Not Exists (Select 1 From dbo.TI_ItemCategoria Where Item = @cItem and Categoria = @cCategoria and Estado = 'A') Throw 50207, 'La categoría no está habilitada para el item seleccionado.', 1
	If @cAreaCausante Is Not Null and Not Exists (Select 1 From dbo.TI_Area Where Area = @cAreaCausante and Estado = 'A') Throw 50208, 'El área causante no es válida.', 1
	If @nPrioridad Is Not Null and @nPrioridad Not Between 1 and 5 Throw 50209, 'La prioridad debe estar entre 1 y 5.', 1
	If @nImpacto Is Not Null and @nImpacto Not Between 1 and 5 Throw 50210, 'El impacto debe estar entre 1 y 5.', 1
	If @nComplejidad Is Not Null and @nComplejidad Not Between 1 and 5 Throw 50211, 'La complejidad debe estar entre 1 y 5.', 1

	Update dbo.TI_Incidencia
	Set AreaTI = @cArea, Linea = @cLinea, Item = @cItem, Tipo = @cTipo, SubTipo = @cSubTipo, Categoria = @cCategoria,
		AreaCausante = @cAreaCausante, Prioridad = @nPrioridad, Impacto = @nImpacto, Complejidad = @nComplejidad,
		UltimoUsuario = @cUsuario, UltimaFechaModif = SysDateTime()
	Where IncidenciaNumero = @cIncidenciaNumero

	Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
	Values (@cIncidenciaNumero, @cUsuario, 'T', 'TI_Incidencia', @cIncidenciaNumero, 'CLASIFICAR_TICKET', 'EXITOSO', Null, @cIdCorrelacion, SysDateTime())
End
Go

/* Ejecuta Procedure */

/* Ejemplo: Exec dbo.Usp_TI_Asignar_Ticket 'TEC001', 'TIC', 'INC-000002', 'TEC001', '00000000-0000-0000-0000-000000000001' */
Create Or Alter Procedure dbo.Usp_TI_Asignar_Ticket
/*================================================================================
Objetivo            : Asignar o reasignar un ticket a un operador TI activo.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Registra responsable, actor que asigna, fechas, estado funcional e historial auditable.
================================================================================*/
	@cUsuario varchar(20), @cArea char(3), @cIncidenciaNumero varchar(12), @cUsuarioTI varchar(20), @cIdCorrelacion uniqueidentifier
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Begin Try
		Begin Transaction

		Declare @cEstadoActual char(2), @cEstadoNuevo char(2), @nSecuencia int, @dFecha datetime2(0) = SysDateTime()

		If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC', 'SUP', 'ADM')) Throw 50212, 'El operador TI no es válido.', 1
		If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuarioTI and Area = @cArea and Estado = 'A' and Perfil In ('TEC', 'SUP', 'ADM')) Throw 50213, 'El responsable seleccionado no es un operador TI activo.', 1

		Select @cEstadoActual = Estado From dbo.TI_Incidencia With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero
		If @cEstadoActual Is Null Throw 50214, 'El ticket indicado no existe.', 1
		If @cEstadoActual In ('RS', 'CA') Throw 50215, 'No se puede asignar un ticket cerrado.', 1

		Set @cEstadoNuevo = Case When @cEstadoActual In ('NV', 'RA', 'ES') Then 'DG' Else @cEstadoActual End

		Update dbo.TI_Incidencia
		Set AreaTI = @cArea, UsuarioTI = @cUsuarioTI, UsuarioAsigno = @cUsuario,
			FechaAsignacion = IsNull(FechaAsignacion, @dFecha), Estado = @cEstadoNuevo, UltimoUsuario = @cUsuario, UltimaFechaModif = @dFecha
		Where IncidenciaNumero = @cIncidenciaNumero

		If @cEstadoNuevo <> @cEstadoActual
		Begin
			Select @nSecuencia = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_IncidenciaEstado With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero
			Insert dbo.TI_IncidenciaEstado (IncidenciaNumero, Secuencia, Estado, UsuarioCambio, FechaCambio, Observacion)
			Values (@cIncidenciaNumero, @nSecuencia, @cEstadoNuevo, @cUsuario, @dFecha, N'Ticket asignado para diagnóstico.')
		End

		Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
		Values (@cIncidenciaNumero, @cUsuario, 'T', 'TI_Incidencia', @cIncidenciaNumero, 'ASIGNAR_TICKET', 'EXITOSO', Null, @cIdCorrelacion, @dFecha)

		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go

/* Ejecuta Procedure */

/* Ejemplo: Exec dbo.Usp_TI_Registrar_AvanceTicket 'TEC001', 'TIC', 'INC-000002', N'Se validó información del documento.', 1, '00000000-0000-0000-0000-000000000001' */
Create Or Alter Procedure dbo.Usp_TI_Registrar_AvanceTicket
/*================================================================================
Objetivo            : Registrar un avance técnico y opcionalmente comunicarlo al usuario.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Elimina la necesidad de una pantalla independiente de avances y no exige porcentajes subjetivos.
================================================================================*/
	@cUsuario varchar(20), @cArea char(3), @cIncidenciaNumero varchar(12), @cDetalle nvarchar(max), @lVisibleUsuario bit = 0, @cIdCorrelacion uniqueidentifier
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Begin Try
		Begin Transaction

		Declare @nAvance int, @nMensaje int, @nEstado int, @cEstado char(2), @dFecha datetime2(0) = SysDateTime()

		If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC', 'SUP', 'ADM')) Throw 50216, 'El operador TI no es válido.', 1
		If NullIf(LTrim(RTrim(@cDetalle)), '') Is Null Throw 50217, 'El detalle del avance no puede estar vacío.', 1
		Select @cEstado = Estado From dbo.TI_Incidencia With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero
		If @cEstado Is Null or @cEstado In ('RS', 'CA') Throw 50218, 'El ticket no existe o ya se encuentra cerrado.', 1

		Select @nAvance = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_IncidenciaAvance With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero
		Insert dbo.TI_IncidenciaAvance (IncidenciaNumero, Secuencia, UsuarioTI, FechaAvance, Detalle, TiempoUtilizado, PorcentajeAvance)
		Values (@cIncidenciaNumero, @nAvance, @cUsuario, @dFecha, LTrim(RTrim(@cDetalle)), Null, Null)

		If @lVisibleUsuario = 1
		Begin
			Select @nMensaje = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_IncidenciaMensaje With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero
			Insert dbo.TI_IncidenciaMensaje (IncidenciaNumero, Secuencia, UsuarioAutor, TipoAutor, Contenido, FechaMensaje, EsInterno)
			Values (@cIncidenciaNumero, @nMensaje, @cUsuario, 'T', LTrim(RTrim(@cDetalle)), @dFecha, 0)
		End

		If @cEstado In ('NV', 'RA', 'ES')
		Begin
			Update dbo.TI_Incidencia Set Estado = 'DG', FechaAtencion = IsNull(FechaAtencion, @dFecha), UltimoUsuario = @cUsuario, UltimaFechaModif = @dFecha Where IncidenciaNumero = @cIncidenciaNumero
			Select @nEstado = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_IncidenciaEstado With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero
			Insert dbo.TI_IncidenciaEstado (IncidenciaNumero, Secuencia, Estado, UsuarioCambio, FechaCambio, Observacion)
			Values (@cIncidenciaNumero, @nEstado, 'DG', @cUsuario, @dFecha, N'Se inició la atención técnica del ticket.')
		End
		Else Update dbo.TI_Incidencia Set FechaAtencion = IsNull(FechaAtencion, @dFecha), UltimoUsuario = @cUsuario, UltimaFechaModif = @dFecha Where IncidenciaNumero = @cIncidenciaNumero

		Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
		Values (@cIncidenciaNumero, @cUsuario, 'T', 'TI_IncidenciaAvance', @cIncidenciaNumero, 'REGISTRAR_AVANCE', 'EXITOSO', Null, @cIdCorrelacion, @dFecha)

		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go

/* Ejecuta Procedure */

/* Ejemplo: Exec dbo.Usp_TI_Solicitar_InformacionTicket 'TEC001', 'TIC', 'INC-000002', N'Adjunta una captura completa e indica el documento relacionado.', '00000000-0000-0000-0000-000000000001' */
Create Or Alter Procedure dbo.Usp_TI_Solicitar_InformacionTicket
/*================================================================================
Objetivo            : Solicitar información adicional al usuario y dejar el ticket en recopilación.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Sustituye el estado legado Observado por un flujo explícito de recopilación.
================================================================================*/
	@cUsuario varchar(20), @cArea char(3), @cIncidenciaNumero varchar(12), @cMensaje nvarchar(1000), @cIdCorrelacion uniqueidentifier
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Begin Try
		Begin Transaction

		Declare @nMensaje int, @nEstado int, @dFecha datetime2(0) = SysDateTime()
		If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC', 'SUP', 'ADM')) Throw 50219, 'El operador TI no es válido.', 1
		If NullIf(LTrim(RTrim(@cMensaje)), '') Is Null Throw 50220, 'Indica qué información necesita el usuario.', 1
		If Not Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero and Estado Not In ('RS', 'CA', 'PV')) Throw 50221, 'El ticket no admite una solicitud de información en su estado actual.', 1

		Select @nMensaje = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_IncidenciaMensaje With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero
		Insert dbo.TI_IncidenciaMensaje (IncidenciaNumero, Secuencia, UsuarioAutor, TipoAutor, Contenido, FechaMensaje, EsInterno)
		Values (@cIncidenciaNumero, @nMensaje, @cUsuario, 'T', LTrim(RTrim(@cMensaje)), @dFecha, 0)

		Update dbo.TI_Incidencia Set Estado = 'RC', UltimoUsuario = @cUsuario, UltimaFechaModif = @dFecha Where IncidenciaNumero = @cIncidenciaNumero
		Select @nEstado = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_IncidenciaEstado With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero
		Insert dbo.TI_IncidenciaEstado (IncidenciaNumero, Secuencia, Estado, UsuarioCambio, FechaCambio, Observacion)
		Values (@cIncidenciaNumero, @nEstado, 'RC', @cUsuario, @dFecha, LTrim(RTrim(@cMensaje)))

		Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
		Values (@cIncidenciaNumero, @cUsuario, 'T', 'TI_Incidencia', @cIncidenciaNumero, 'SOLICITAR_INFORMACION', 'EXITOSO', Null, @cIdCorrelacion, @dFecha)
		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go

/* Ejecuta Procedure */

/* Ejemplo: Exec dbo.Usp_TI_Resolver_TicketTI 'TEC001', 'TIC', 'INC-000002', N'Causa validada.', N'Solución aplicada.', N'Vuelve a realizar el proceso y confirma el resultado.', 'CORRECCION', '00000000-0000-0000-0000-000000000001' */
Create Or Alter Procedure dbo.Usp_TI_Resolver_TicketTI
/*================================================================================
Objetivo            : Registrar la resolución técnica y enviar el ticket a validación del usuario.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : TI no cierra directamente el caso; conserva Pendiente de validación hasta confirmación del usuario.
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

/* Ejecuta Procedure */

/* Ejemplo: Exec dbo.Usp_TI_NoProcede_Ticket 'TEC001', 'TIC', 'INC-000002', N'Corresponde a una mejora funcional y debe registrarse como requerimiento.', '00000000-0000-0000-0000-000000000001' */
Create Or Alter Procedure dbo.Usp_TI_NoProcede_Ticket
/*================================================================================
Objetivo            : Rechazar o cancelar justificadamente un ticket que no procede como incidencia atendible.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Conserva la capacidad No Procede del sistema legado con motivo obligatorio y trazabilidad.
================================================================================*/
	@cUsuario varchar(20), @cArea char(3), @cIncidenciaNumero varchar(12), @cMotivo nvarchar(1000), @cIdCorrelacion uniqueidentifier
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Begin Try
		Begin Transaction
		Declare @nMensaje int, @nEstado int, @dFecha datetime2(0) = SysDateTime()

		If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC', 'SUP', 'ADM')) Throw 50227, 'El operador TI no es válido.', 1
		If Not Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero and Estado Not In ('RS', 'CA')) Throw 50228, 'El ticket no existe o ya se encuentra cerrado.', 1
		If NullIf(LTrim(RTrim(@cMotivo)), '') Is Null Throw 50229, 'El motivo de No Procede es obligatorio.', 1

		Update dbo.TI_Incidencia Set Estado = 'CA', RespuestaUsuario = LTrim(RTrim(@cMotivo)), FechaCierre = @dFecha, UltimoUsuario = @cUsuario, UltimaFechaModif = @dFecha Where IncidenciaNumero = @cIncidenciaNumero

		Select @nMensaje = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_IncidenciaMensaje With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero
		Insert dbo.TI_IncidenciaMensaje (IncidenciaNumero, Secuencia, UsuarioAutor, TipoAutor, Contenido, FechaMensaje, EsInterno)
		Values (@cIncidenciaNumero, @nMensaje, @cUsuario, 'T', LTrim(RTrim(@cMotivo)), @dFecha, 0)

		Select @nEstado = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_IncidenciaEstado With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero
		Insert dbo.TI_IncidenciaEstado (IncidenciaNumero, Secuencia, Estado, UsuarioCambio, FechaCambio, Observacion)
		Values (@cIncidenciaNumero, @nEstado, 'CA', @cUsuario, @dFecha, LTrim(RTrim(@cMotivo)))

		Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
		Values (@cIncidenciaNumero, @cUsuario, 'T', 'TI_Incidencia', @cIncidenciaNumero, 'NO_PROCEDE', 'EXITOSO', Null, @cIdCorrelacion, @dFecha)
		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go

/* Ejecuta Procedure */

/* Ejemplo: Exec dbo.Usp_TI_Responder_AprobacionTicket 'TEC001', 'TIC', 'INC-000001', 1, 1, N'Aprobado para continuar con el flujo controlado.', '00000000-0000-0000-0000-000000000001' */
Create Or Alter Procedure dbo.Usp_TI_Responder_AprobacionTicket
/*================================================================================
Objetivo            : Aprobar o rechazar una solicitud pendiente sin ejecutar automáticamente la acción asociada.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : La decisión queda registrada; la ejecución controlada pertenece al futuro motor de acciones y no se adelanta en este módulo.
================================================================================*/
	@cUsuario varchar(20), @cArea char(3), @cIncidenciaNumero varchar(12), @nSecuencia int, @lAprobar bit, @cComentario nvarchar(1000), @cIdCorrelacion uniqueidentifier
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Begin Try
		Begin Transaction
		Declare @nEstado int, @dFecha datetime2(0) = SysDateTime()

		If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC', 'SUP', 'ADM')) Throw 50230, 'El operador TI no es válido.', 1
		If Not Exists (Select 1 From dbo.TI_SolicitudAprobacion Where IncidenciaNumero = @cIncidenciaNumero and Secuencia = @nSecuencia and Estado = 'P') Throw 50231, 'La solicitud de aprobación ya no se encuentra pendiente.', 1
		If @lAprobar = 0 and NullIf(LTrim(RTrim(@cComentario)), '') Is Null Throw 50232, 'Indica el motivo del rechazo.', 1

		Update dbo.TI_SolicitudAprobacion
		Set Estado = Case When @lAprobar = 1 Then 'A' Else 'R' End, UsuarioAprobador = @cUsuario, ComentarioRespuesta = NullIf(LTrim(RTrim(@cComentario)), ''), FechaRespuesta = @dFecha
		Where IncidenciaNumero = @cIncidenciaNumero and Secuencia = @nSecuencia and Estado = 'P'

		If Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero and Estado = 'PA')
		Begin
			Update dbo.TI_Incidencia Set Estado = 'DG', UltimoUsuario = @cUsuario, UltimaFechaModif = @dFecha Where IncidenciaNumero = @cIncidenciaNumero
			Select @nEstado = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_IncidenciaEstado With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero
			Insert dbo.TI_IncidenciaEstado (IncidenciaNumero, Secuencia, Estado, UsuarioCambio, FechaCambio, Observacion)
			Values (@cIncidenciaNumero, @nEstado, 'DG', @cUsuario, @dFecha, Case When @lAprobar = 1 Then N'Acción aprobada; pendiente de ejecución controlada.' Else N'Acción rechazada; el ticket continúa en diagnóstico.' End)
		End

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

/* Ejecuta Procedure */

/* Ejemplo: Exec dbo.Usp_TI_Obtener_AdjuntoTicketTI 'TEC001', 'TIC', 'INC-000001', 1 */
Create Or Alter Procedure dbo.Usp_TI_Obtener_AdjuntoTicketTI
/*================================================================================
Objetivo            : Obtener la ruta física autorizada de una evidencia asociada a un ticket para su descarga por TI.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Valida perfil TI antes de exponer metadata del archivo solicitado.
================================================================================*/
	@cUsuario varchar(20), @cArea char(3), @cIncidenciaNumero varchar(12), @nSecuencia int
As
Begin
	Set NoCount On

	If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC', 'SUP', 'ADM')) Throw 50233, 'El operador TI no es válido.', 1

	Select ad.NombreOriginal, ad.RutaArchivo, ad.TipoMime
	From dbo.TI_IncidenciaAdjunto as ad
	Where ad.IncidenciaNumero = @cIncidenciaNumero and ad.Secuencia = @nSecuencia
End
Go

/* Ejecuta Procedure */