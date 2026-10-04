/*
	Archivo: 16_GestionTicketsTI.sql
	Objetivo: Crear los Stored Procedures requeridos por el módulo Gestión de Tickets para operadores TI.
	Responsabilidad: Consolidar bandeja, detalle técnico, clasificación, asignación, avances, solicitudes de información, resolución, No Procede, aprobaciones y descarga de evidencias.
	Dependencias: TI_Incidencia, maestros TI_*, TI_IncidenciaEstado, TI_IncidenciaAvance, TI_IncidenciaMensaje, TI_IncidenciaAdjunto, TI_IncidenciaDocumento, TI_SolicitudAprobacion, TI_Accion y TI_Auditoria.
	Orden: Ejecutar después de 15_MisTicketsUsuario.sql.
	Consideraciones: El operador se valida siempre desde la sesión/backend; ninguna operación ejecuta SQL libre ni cierra una incidencia sin validación del usuario cuando la solución fue enviada a confirmar.
*/

Use [GestionSistemas]
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

-- dbo.Usp_TI_Obtener_DetalleGestionTicketTI: la versión vigente está en 27_AgenteFase1Integracion.sql (aquí había una versión anterior que ese script reemplaza).
Go

-- dbo.Usp_TI_Clasificar_Ticket: la versión vigente está en 19_MejorasFuncionalesSinIA.sql (aquí había una versión anterior que ese script reemplaza).
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

-- dbo.Usp_TI_Registrar_AvanceTicket: la versión vigente está en 22_CierreMejorasFuncionalesSinIA.sql (aquí había una versión anterior que ese script reemplaza).
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

-- dbo.Usp_TI_Resolver_TicketTI: la versión vigente está en 28_AgenteFase2Integracion.sql (aquí había una versión anterior que ese script reemplaza).
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

-- dbo.Usp_TI_Responder_AprobacionTicket: la versión vigente está en 28_AgenteFase2Integracion.sql (aquí había una versión anterior que ese script reemplaza).
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
