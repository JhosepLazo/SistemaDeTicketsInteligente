/*
	Archivo: 18_ReportesTI.sql
	Objetivo: Crear los Stored Procedures requeridos por el módulo Reportes para operadores TI.
	Responsabilidad: Consolidar indicadores, evolución, distribución, tiempos y detalle exportable de incidencias usando filtros operativos homogéneos.
	Dependencias: TI_Incidencia, TI_Usuario, TI_Area, TI_Estado y TI_Tipo.
	Orden: Ejecutar después de 17_BaseConocimientoTI.sql.
	Consideraciones: El módulo trabaja únicamente con datos reales del sistema; no genera métricas ficticias y limita el rango consultable a un máximo de 366 días desde backend.
*/

Use [GestionSistemas]
Go

/* Ejemplo: Exec dbo.Usp_TI_Obtener_ReportesTI '2026-09-01', '2026-09-30', Null, Null, Null, Null, Null */
Create Or Alter Procedure dbo.Usp_TI_Obtener_ReportesTI
/*================================================================================
Objetivo            : Obtener en una sola llamada los datos necesarios para el tablero de Reportes TI.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Primera versión con filtros por fecha, área, estado, prioridad, tipo y responsable TI.
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

	If @dFechaInicio Is Null or @dFechaFin Is Null Throw 50300, 'El rango de fechas es obligatorio.', 1
	If @dFechaInicio > @dFechaFin Throw 50301, 'La fecha inicial no puede ser mayor que la fecha final.', 1
	If DateDiff(day, @dFechaInicio, @dFechaFin) > 365 Throw 50302, 'El rango máximo permitido es de 366 días.', 1

	Declare @dFechaFinExclusiva date = DateAdd(day, 1, @dFechaFin)
	Declare @nDias int = DateDiff(day, @dFechaInicio, @dFechaFin) + 1
	Declare @dFechaInicioAnterior date = DateAdd(day, -@nDias, @dFechaInicio)
	Declare @dFechaFinAnterior date = @dFechaInicio

	Select
		i.IncidenciaNumero,
		i.UsuarioSolicitante,
		Solicitante = us.NombreCompleto,
		i.AreaSolicitante,
		AreaDescripcion = a.Descripcion,
		i.Tipo,
		TipoDescripcion = t.Descripcion,
		i.Estado,
		EstadoDescripcion = e.Descripcion,
		i.Prioridad,
		i.UsuarioTI,
		Responsable = IsNull(ut.NombreCompleto, 'Sin asignar'),
		i.Titulo,
		i.FechaRegistro,
		i.FechaCierre,
		i.SlaObjetivoMinutos,
		i.Calificacion,
		i.UltimaFechaModif
	Into #Tickets
	From dbo.TI_Incidencia as i
	Inner Join dbo.TI_Usuario as us on us.Usuario = i.UsuarioSolicitante
	Inner Join dbo.TI_Area as a on a.Area = i.AreaSolicitante
	Inner Join dbo.TI_Tipo as t on t.Tipo = i.Tipo
	Inner Join dbo.TI_Estado as e on e.Estado = i.Estado
	Left Join dbo.TI_Usuario as ut on ut.Usuario = i.UsuarioTI
	Where i.FechaRegistro >= @dFechaInicio and i.FechaRegistro < @dFechaFinExclusiva
		and (@cArea Is Null or @cArea = '' or i.AreaSolicitante = @cArea)
		and (@cEstado Is Null or @cEstado = '' or i.Estado = @cEstado)
		and (@cTipo Is Null or @cTipo = '' or i.Tipo = @cTipo)
		and (
			@cUsuarioTI Is Null or @cUsuarioTI = ''
			or (@cUsuarioTI = 'SIN_ASIGNAR' and i.UsuarioTI Is Null)
			or i.UsuarioTI = @cUsuarioTI
		)
		and (
			@cPrioridad Is Null or @cPrioridad = ''
			or (@cPrioridad = 'ALTA' and IsNull(i.Prioridad, 0) >= 4)
			or (@cPrioridad = 'MEDIA' and i.Prioridad = 3)
			or (@cPrioridad = 'BAJA' and i.Prioridad Between 1 and 2)
			or (@cPrioridad = 'SIN' and i.Prioridad Is Null)
		)

	Select
		i.Estado,
		i.Prioridad,
		i.FechaRegistro,
		i.FechaCierre,
		i.SlaObjetivoMinutos,
		i.Calificacion
	Into #TicketsAnterior
	From dbo.TI_Incidencia as i
	Where i.FechaRegistro >= @dFechaInicioAnterior and i.FechaRegistro < @dFechaFinAnterior
		and (@cArea Is Null or @cArea = '' or i.AreaSolicitante = @cArea)
		and (@cEstado Is Null or @cEstado = '' or i.Estado = @cEstado)
		and (@cTipo Is Null or @cTipo = '' or i.Tipo = @cTipo)
		and (
			@cUsuarioTI Is Null or @cUsuarioTI = ''
			or (@cUsuarioTI = 'SIN_ASIGNAR' and i.UsuarioTI Is Null)
			or i.UsuarioTI = @cUsuarioTI
		)
		and (
			@cPrioridad Is Null or @cPrioridad = ''
			or (@cPrioridad = 'ALTA' and IsNull(i.Prioridad, 0) >= 4)
			or (@cPrioridad = 'MEDIA' and i.Prioridad = 3)
			or (@cPrioridad = 'BAJA' and i.Prioridad Between 1 and 2)
			or (@cPrioridad = 'SIN' and i.Prioridad Is Null)
		)

	Select
		Total = Count(1),
		Resueltos = IsNull(Sum(Case When Estado = 'RS' Then 1 Else 0 End), 0),
		Reabiertos = IsNull(Sum(Case When Estado = 'RA' Then 1 Else 0 End), 0),
		TiempoPromedioHoras = Cast(Avg(Case When FechaCierre Is Not Null Then DateDiff(minute, FechaRegistro, FechaCierre) / 60.0 End) as decimal(10,2)),
		Satisfaccion = Cast(Avg(Convert(decimal(10,2), Calificacion)) as decimal(10,2)),
		CumplimientoSla = Cast(100.0 * Avg(Case When FechaCierre Is Null or SlaObjetivoMinutos Is Null Then Null When DateDiff(minute, FechaRegistro, FechaCierre) <= SlaObjetivoMinutos Then 1.0 Else 0.0 End) as decimal(10,2)),
		TotalAnterior = (Select Count(1) From #TicketsAnterior),
		ResueltosAnterior = (Select IsNull(Sum(Case When Estado = 'RS' Then 1 Else 0 End), 0) From #TicketsAnterior),
		TiempoPromedioHorasAnterior = (Select Cast(Avg(Case When FechaCierre Is Not Null Then DateDiff(minute, FechaRegistro, FechaCierre) / 60.0 End) as decimal(10,2)) From #TicketsAnterior),
		SatisfaccionAnterior = (Select Cast(Avg(Convert(decimal(10,2), Calificacion)) as decimal(10,2)) From #TicketsAnterior)
	From #Tickets

	Select Fecha = Convert(date, FechaRegistro), Cantidad = Count(1)
	From #Tickets
	Group By Convert(date, FechaRegistro)
	Order By Fecha

	Select Estado, EstadoDescripcion, Cantidad = Count(1)
	From #Tickets
	Group By Estado, EstadoDescripcion
	Order By Cantidad Desc, EstadoDescripcion

	Select Area = AreaSolicitante, AreaDescripcion, Cantidad = Count(1)
	From #Tickets
	Group By AreaSolicitante, AreaDescripcion
	Order By Cantidad Desc, AreaDescripcion

	;With AvanceAcumulado As (
		Select av.IncidenciaNumero,
			AvancesRegistrados = Count(1),
			MinutosRegistrados = Cast(IsNull(Sum(av.TiempoUtilizado), 0) as decimal(18,2))
		From dbo.TI_IncidenciaAvance as av
		Inner Join #Tickets as t on t.IncidenciaNumero = av.IncidenciaNumero
		Group By av.IncidenciaNumero
	), UltimoAvance As (
		Select av.IncidenciaNumero, av.PorcentajeAvance, av.FechaAvance,
			Fila = Row_Number() Over (Partition By av.IncidenciaNumero Order By av.FechaAvance Desc, av.Secuencia Desc)
		From dbo.TI_IncidenciaAvance as av
		Inner Join #Tickets as t on t.IncidenciaNumero = av.IncidenciaNumero
	)
	Select t.IncidenciaNumero, t.Titulo, t.AreaDescripcion, t.Estado, t.EstadoDescripcion, t.Responsable,
		PorcentajeAvance = u.PorcentajeAvance,
		FechaUltimoAvance = u.FechaAvance,
		AvancesRegistrados = IsNull(a.AvancesRegistrados, 0),
		MinutosRegistrados = IsNull(a.MinutosRegistrados, 0)
	From #Tickets as t
	Left Join AvanceAcumulado as a on a.IncidenciaNumero = t.IncidenciaNumero
	Left Join UltimoAvance as u on u.IncidenciaNumero = t.IncidenciaNumero and u.Fila = 1
	Order By Case When u.FechaAvance Is Null Then 1 Else 0 End, u.FechaAvance Desc, t.FechaRegistro Desc

	;With AvanceAcumulado As (
		Select av.IncidenciaNumero,
			AvancesRegistrados = Count(1),
			MinutosRegistrados = Cast(IsNull(Sum(av.TiempoUtilizado), 0) as decimal(18,2)),
			PorcentajePromedio = Cast(Avg(av.PorcentajeAvance) as decimal(10,2))
		From dbo.TI_IncidenciaAvance as av
		Inner Join #Tickets as t on t.IncidenciaNumero = av.IncidenciaNumero
		Group By av.IncidenciaNumero
	)
	Select
		Usuario = IsNull(NullIf(t.UsuarioTI, ''), 'SIN_ASIGNAR'),
		Responsable = t.Responsable,
		TicketsAsignados = Count(1),
		TicketsResueltos = Sum(Case When t.Estado = 'RS' Then 1 Else 0 End),
		TicketsEnCurso = Sum(Case When t.Estado Not In ('RS', 'CA') Then 1 Else 0 End),
		AvancesRegistrados = IsNull(Sum(a.AvancesRegistrados), 0),
		MinutosRegistrados = Cast(IsNull(Sum(a.MinutosRegistrados), 0) as decimal(18,2)),
		PorcentajePromedio = Cast(Avg(a.PorcentajePromedio) as decimal(10,2))
	From #Tickets as t
	Left Join AvanceAcumulado as a on a.IncidenciaNumero = t.IncidenciaNumero
	Group By IsNull(NullIf(t.UsuarioTI, ''), 'SIN_ASIGNAR'), t.Responsable
	Order By TicketsAsignados Desc, t.Responsable

	Select
		p.PrioridadNombre as Prioridad,
		Tickets = Count(1),
		TiempoPromedioHoras = Cast(Avg(Case When p.FechaCierre Is Not Null Then DateDiff(minute, p.FechaRegistro, p.FechaCierre) / 60.0 End) as decimal(10,2)),
		TiempoMasRapidoHoras = Cast(Min(Case When p.FechaCierre Is Not Null Then DateDiff(minute, p.FechaRegistro, p.FechaCierre) / 60.0 End) as decimal(10,2)),
		TiempoMasLargoHoras = Cast(Max(Case When p.FechaCierre Is Not Null Then DateDiff(minute, p.FechaRegistro, p.FechaCierre) / 60.0 End) as decimal(10,2))
	From (
		Select
			PrioridadNombre = Case When Prioridad >= 4 Then 'Alta' When Prioridad = 3 Then 'Media' When Prioridad Between 1 and 2 Then 'Baja' Else 'Sin asignar' End,
			FechaRegistro,
			FechaCierre
		From #Tickets
	) as p
	Group By p.PrioridadNombre
	Order By Case p.PrioridadNombre When 'Alta' Then 1 When 'Media' Then 2 When 'Baja' Then 3 Else 4 End

	Select Top (8)
		IncidenciaNumero, Titulo, AreaDescripcion, Estado, EstadoDescripcion, Prioridad, Responsable, FechaRegistro,
		TiempoAbiertoHoras = DateDiff(hour, FechaRegistro, IsNull(FechaCierre, SysDateTime()))
	From #Tickets
	Where IsNull(Prioridad, 0) >= 4
	Order By Prioridad Desc, FechaRegistro Desc

	Select Codigo = Area, Descripcion
	From dbo.TI_Area
	Where Estado = 'A'
	Order By Descripcion

	Select Codigo = Estado, Descripcion
	From dbo.TI_Estado
	Order By Orden, Descripcion

	Select Codigo = Tipo, Descripcion
	From dbo.TI_Tipo
	Where Estado = 'A'
	Order By Descripcion

	Select Codigo = Usuario, Descripcion = NombreCompleto
	From dbo.TI_Usuario
	Where Estado = 'A' and Perfil In ('TEC', 'SUP', 'ADM')
	Order By NombreCompleto

	Select
		IncidenciaNumero,
		Solicitante,
		Titulo,
		AreaDescripcion,
		TipoDescripcion,
		EstadoDescripcion,
		Prioridad,
		Responsable,
		FechaRegistro,
		FechaCierre,
		Calificacion
	From #Tickets
	Order By FechaRegistro Desc, IncidenciaNumero Desc
End
Go

/* Ejecuta Procedure */
