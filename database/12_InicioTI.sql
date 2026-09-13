/*
	Archivo: 12_InicioTI.sql
	Objetivo: Crear la consulta consolidada que alimenta el módulo Inicio para operadores TI autenticados.
	Responsabilidad: Obtener resumen operativo, tickets prioritarios, recordatorios, tickets activos y actividad reciente del área TI en una sola ejecución.
	Dependencias: TI_Incidencia, TI_IncidenciaEstado, TI_Estado, TI_Usuario y TI_SolicitudAprobacion.
	Orden: Ejecutar después de 11_InicioUsuario.sql.
	Consideraciones: Solo realiza lectura; limita la información al área TI del operador autenticado e incluye tickets todavía sin área asignada para que puedan ser atendidos.
*/

Use [SistemaTicketsInteligente]
Go

-- Exec dbo.Usp_TI_Obtener_InicioTI 'TEC001', 'TIC'
Create Procedure dbo.Usp_TI_Obtener_InicioTI
/*================================================================================
Objetivo            : Obtener toda la información necesaria para el Inicio del operador TI en una sola llamada.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 12/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Primera versión del dashboard operativo para TI.
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3)
As
Begin
	Set NoCount On

	Declare @dAhora datetime2(0) = SysDateTime()
	Declare @dInicioHoy datetime2(0) = Convert(date, @dAhora)
	Declare @dInicioAyer datetime2(0) = DateAdd(Day, -1, @dInicioHoy)

	Select
		Pendientes = IsNull(Sum(Case When i.Estado In ('NV', 'RC', 'PA', 'RA') Then 1 Else 0 End), 0),
		PendientesDesdeAyer = IsNull(Sum(Case When i.Estado In ('NV', 'RC', 'PA', 'RA') and i.FechaRegistro >= @dInicioAyer Then 1 Else 0 End), 0),
		EnAtencion = IsNull(Sum(Case When i.Estado In ('DG', 'EJ', 'ES', 'PV') Then 1 Else 0 End), 0),
		EnProgresoHoy = IsNull(Sum(Case When i.Estado In ('DG', 'EJ', 'ES', 'PV') and i.UltimaFechaModif >= @dInicioHoy Then 1 Else 0 End), 0),
		RequierenAccion = IsNull(Sum(Case When i.Estado Not In ('RS', 'CA') and (
			i.UsuarioTI Is Null or i.Estado In ('ES', 'RA', 'PA') or
			(i.SlaObjetivoMinutos Is Not Null and DateAdd(Minute, i.SlaObjetivoMinutos, i.FechaRegistro) Between @dAhora and DateAdd(Hour, 4, @dAhora))
		) Then 1 Else 0 End), 0),
		SinAsignar = IsNull(Sum(Case When i.Estado Not In ('RS', 'CA') and i.UsuarioTI Is Null Then 1 Else 0 End), 0),
		PrioridadAlta = IsNull(Sum(Case When i.Estado Not In ('RS', 'CA') and IsNull(i.Prioridad, 0) >= 4 Then 1 Else 0 End), 0),
		TicketsActivos = IsNull(Sum(Case When i.Estado Not In ('RS', 'CA') Then 1 Else 0 End), 0),
		MisAsignados = IsNull(Sum(Case When i.Estado Not In ('RS', 'CA') and i.UsuarioTI = @cUsuario Then 1 Else 0 End), 0)
	From dbo.TI_Incidencia as i
	Where i.AreaTI = @cArea or i.AreaTI Is Null

	Select Top (3)
		i.IncidenciaNumero,
		UsuarioSolicitante = us.NombreCompleto,
		i.Titulo,
		i.Estado,
		EstadoDescripcion = e.Descripcion,
		i.Prioridad,
		i.UsuarioTI,
		Responsable = IsNull(ut.NombreCompleto, 'Sin asignar'),
		i.UltimaFechaModif,
		SlaMinutosRestantes = Case When i.SlaObjetivoMinutos Is Null Then Null Else DateDiff(Minute, @dAhora, DateAdd(Minute, i.SlaObjetivoMinutos, i.FechaRegistro)) End,
		TipoAtencion = Case
			When i.UsuarioTI Is Null Then 'SIN_ASIGNAR'
			When i.Estado = 'RA' Then 'REABIERTO'
			When i.Estado = 'ES' Then 'ESCALADO'
			When i.SlaObjetivoMinutos Is Not Null and DateAdd(Minute, i.SlaObjetivoMinutos, i.FechaRegistro) Between @dAhora and DateAdd(Hour, 4, @dAhora) Then 'SLA_POR_VENCER'
			When i.Estado = 'PA' Then 'APROBACION'
			Else 'REVISAR'
		End,
		Accion = Case When i.UsuarioTI Is Null Then 'ASIGNAR' When i.Estado = 'RA' Then 'CONTINUAR' Else 'REVISAR' End
	From dbo.TI_Incidencia as i
	Inner Join dbo.TI_Estado as e on e.Estado = i.Estado
	Inner Join dbo.TI_Usuario as us on us.Usuario = i.UsuarioSolicitante
	Left Join dbo.TI_Usuario as ut on ut.Usuario = i.UsuarioTI
	Where (i.AreaTI = @cArea or i.AreaTI Is Null)
		and i.Estado Not In ('RS', 'CA')
		and (
			i.UsuarioTI Is Null or i.Estado In ('ES', 'RA', 'PA') or
			(i.SlaObjetivoMinutos Is Not Null and DateAdd(Minute, i.SlaObjetivoMinutos, i.FechaRegistro) Between @dAhora and DateAdd(Hour, 4, @dAhora))
		)
	Order By
		Case When i.UsuarioTI Is Null Then 0 When i.Estado = 'RA' Then 1 When i.Estado = 'ES' Then 2 When i.Estado = 'PA' Then 3 Else 4 End,
		IsNull(i.Prioridad, 0) Desc,
		i.UltimaFechaModif Desc

	Select
		AprobacionesPendientes = (
			Select Count(1)
			From dbo.TI_SolicitudAprobacion as sa
			Inner Join dbo.TI_Incidencia as ia on ia.IncidenciaNumero = sa.IncidenciaNumero
			Where sa.Estado = 'P' and (ia.AreaTI = @cArea or ia.AreaTI Is Null)
		),
		SlaPorVencer = (
			Select Count(1)
			From dbo.TI_Incidencia as isla
			Where (isla.AreaTI = @cArea or isla.AreaTI Is Null)
				and isla.Estado Not In ('RS', 'CA')
				and isla.SlaObjetivoMinutos Is Not Null
				and DateAdd(Minute, isla.SlaObjetivoMinutos, isla.FechaRegistro) Between @dAhora and DateAdd(Hour, 4, @dAhora)
		),
		TicketsReabiertos = (
			Select Count(1)
			From dbo.TI_Incidencia as ira
			Where (ira.AreaTI = @cArea or ira.AreaTI Is Null) and ira.Estado = 'RA'
		)

	Select Top (20)
		i.IncidenciaNumero,
		UsuarioSolicitante = us.NombreCompleto,
		i.Titulo,
		i.Prioridad,
		i.Estado,
		EstadoDescripcion = e.Descripcion,
		i.UsuarioTI,
		Responsable = IsNull(ut.NombreCompleto, 'Sin asignar'),
		i.UltimaFechaModif
	From dbo.TI_Incidencia as i
	Inner Join dbo.TI_Estado as e on e.Estado = i.Estado
	Inner Join dbo.TI_Usuario as us on us.Usuario = i.UsuarioSolicitante
	Left Join dbo.TI_Usuario as ut on ut.Usuario = i.UsuarioTI
	Where (i.AreaTI = @cArea or i.AreaTI Is Null) and i.Estado Not In ('RS', 'CA')
	Order By Case When i.UsuarioTI Is Null Then 0 Else 1 End, IsNull(i.Prioridad, 0) Desc, i.UltimaFechaModif Desc

	Select Top (7)
		i.IncidenciaNumero,
		i.Titulo,
		ie.Estado,
		EstadoDescripcion = e.Descripcion,
		Actor = IsNull(u.NombreCompleto, 'Sistema'),
		ie.FechaCambio
	From dbo.TI_IncidenciaEstado as ie
	Inner Join dbo.TI_Incidencia as i on i.IncidenciaNumero = ie.IncidenciaNumero
	Inner Join dbo.TI_Estado as e on e.Estado = ie.Estado
	Left Join dbo.TI_Usuario as u on u.Usuario = ie.UsuarioCambio
	Where i.AreaTI = @cArea or i.AreaTI Is Null
	Order By ie.FechaCambio Desc
End
Go

/* Ejecuta Procedure */
