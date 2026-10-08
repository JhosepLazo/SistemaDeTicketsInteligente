/*
	Archivo: 37_MetricasAgente.sql
	Objetivo: Medir el desempeño del agente y del flujo de tickets con los indicadores del plan de mejoras (Anexo B).
	Responsabilidad: Entregar a Reportes TI las métricas de investigaciones, aprobaciones, ejecuciones, clasificación, tiempos por tipo,
		fichas de requerimientos, utilidad del conocimiento y costo de las llamadas al modelo, y la comparación entre lo que propuso el
		agente y lo que TI registró al resolver (evidencia para decidir cuánta autonomía conceder).
	Dependencias: Requiere 36_ClasificacionFichaYGuias.sql.
	Orden: Ejecutar después de 36_ClasificacionFichaYGuias.sql.
	Consideraciones: Solo lectura. Los tickets importados del legado (canal LEGADO) no entran en los tiempos ni en las tasas, porque sus
		fechas no reflejan el proceso nuevo. La primera respuesta se mide hasta la asignación del ticket.
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

-- Usp_TI_Obtener_MetricasAgente 'TEC001', 'TIC', '2026-10-01', '2026-10-31'
Create Or Alter Procedure Usp_TI_Obtener_MetricasAgente
/*================================================================================
Objetivo        : Entregar los indicadores del agente y del flujo de tickets de un periodo.
Creado Por      : Jhosep S. Lazo
Fecha Creación  : Oct 2026
SP Anterior     :
Comentario Cambios  : Diez conjuntos: investigaciones, estados, aprobaciones, motivos de rechazo, ejecuciones, clasificación,
					  tickets por tipo, fichas REQ, conocimiento y llamadas al modelo.
================================================================================*/

@cUsuario	varchar(20),
@cArea		char(3),
@dDesde		date,
@dHasta		date

As
Begin
Set NoCount On

	Declare @dInicio datetime2(0) = @dDesde, @dFin datetime2(0) = DateAdd(day, 1, @dHasta)

	If Not Exists (Select 1 From TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM'))
		Throw 50670, 'El operador TI no es válido.', 1
	If @dDesde Is Null or @dHasta Is Null or @dDesde > @dHasta or DateDiff(day, @dDesde, @dHasta) > 366
		Throw 50671, 'Indica un periodo válido de hasta un año.', 1

	-- 1. Investigaciones del periodo.
	Select Total = Count(*),
		Automaticas = IsNull(Sum(Case When automatica.SesionNumero Is Not Null Then 1 Else 0 End), 0),
		ConDiagnostico = IsNull(Sum(Case When sesion.FechaDiagnostico Is Not Null Then 1 Else 0 End), 0),
		ConAccionPropuesta = IsNull(Sum(Case When sesion.AccionCodigo Is Not Null Then 1 Else 0 End), 0),
		SolucionValidada = IsNull(Sum(Case When sesion.SolucionValidada = 1 Then 1 Else 0 End), 0),
		Canceladas = IsNull(Sum(Case When sesion.Estado = 'CANCELADO' Then 1 Else 0 End), 0),
		ConfianzaPromedio = Convert(decimal(5,2), Avg(sesion.Confianza)),
		MinutosPromedioDiagnostico = Avg(Convert(decimal(12,2), DateDiff(second, sesion.FechaInicio, sesion.FechaDiagnostico)) / 60)
	From TI_AgenteSesion as sesion
	Left Join (Select Distinct SesionNumero From TI_AgenteEvento Where Tipo = 'INVESTIGACION_AUTOMATICA') as automatica on automatica.SesionNumero = sesion.SesionNumero
	Where sesion.FechaInicio >= @dInicio and sesion.FechaInicio < @dFin

	-- 2. Investigaciones por estado.
	Select Estado, Cantidad = Count(*)
	From TI_AgenteSesion
	Where FechaInicio >= @dInicio and FechaInicio < @dFin
	Group By Estado
	Order By Cantidad Desc

	-- 3. Aprobaciones solicitadas en el periodo, del agente o manuales.
	Select Origen = Case When agente.SesionNumero Is Null Then 'MANUAL' Else 'AGENTE' End,
		Solicitadas = Count(*),
		Aprobadas = Sum(Case When aprobacion.Estado = 'A' Then 1 Else 0 End),
		Rechazadas = Sum(Case When aprobacion.Estado = 'R' Then 1 Else 0 End),
		Canceladas = Sum(Case When aprobacion.Estado = 'C' Then 1 Else 0 End),
		Pendientes = Sum(Case When aprobacion.Estado = 'P' Then 1 Else 0 End),
		Vencidas = Sum(Case When aprobacion.Estado = 'A' and aprobacion.FechaExpiracion < SysDateTime() Then 1 Else 0 End),
		HorasPromedioRespuesta = Avg(Convert(decimal(12,2), DateDiff(minute, aprobacion.FechaSolicitud, aprobacion.FechaRespuesta)) / 60)
	From TI_SolicitudAprobacion as aprobacion
	Left Join TI_AgenteSesion as agente on agente.IncidenciaNumero = aprobacion.IncidenciaNumero and agente.SolicitudAprobacionSecuencia = aprobacion.Secuencia
	Where aprobacion.FechaSolicitud >= @dInicio and aprobacion.FechaSolicitud < @dFin
	Group By Case When agente.SesionNumero Is Null Then 'MANUAL' Else 'AGENTE' End

	-- 4. Motivos de rechazo más frecuentes.
	Select Top (10) Motivo = Left(aprobacion.ComentarioRespuesta, 300), Cantidad = Count(*)
	From TI_SolicitudAprobacion as aprobacion
	Where aprobacion.Estado = 'R' and aprobacion.FechaRespuesta >= @dInicio and aprobacion.FechaRespuesta < @dFin
	Group By Left(aprobacion.ComentarioRespuesta, 300)
	Order By Cantidad Desc

	-- 5. Ejecuciones de acciones: decididas por TI (T) o autónomas (I), por resultado.
	Select TipoEjecutor, Estado, Cantidad = Count(*), FilasAfectadas = IsNull(Sum(FilasAfectadas), 0)
	From TI_EjecucionAccion
	Where FechaInicio >= @dInicio and FechaInicio < @dFin
	Group By TipoEjecutor, Estado
	Order By TipoEjecutor, Estado

	-- 6. Exactitud de la clasificación propuesta: cada propuesta se compara con la clasificación que TI aplicó después.
	Select Propuestas = Count(*),
		Comparadas = IsNull(Sum(Case When aplicada.Secuencia Is Not Null Then 1 Else 0 End), 0),
		CoincideTipo = IsNull(Sum(Case When aplicada.Secuencia Is Not Null and aplicada.Tipo = propuesta.Tipo Then 1 Else 0 End), 0),
		CoincideSubTipo = IsNull(Sum(Case When aplicada.Secuencia Is Not Null and aplicada.Tipo = propuesta.Tipo and aplicada.SubTipo = propuesta.SubTipo
			and aplicada.Categoria = propuesta.Categoria Then 1 Else 0 End), 0),
		CoincideItem = IsNull(Sum(Case When aplicada.Secuencia Is Not Null and aplicada.Linea = propuesta.Linea and aplicada.Item = propuesta.Item Then 1 Else 0 End), 0),
		ConfianzaPromedio = Convert(decimal(5,2), Avg(propuesta.Confianza))
	From TI_IncidenciaClasificacion as propuesta
	Outer Apply (
		Select Top (1) siguiente.Secuencia, siguiente.Linea, siguiente.Item, siguiente.Tipo, siguiente.SubTipo, siguiente.Categoria
		From TI_IncidenciaClasificacion as siguiente
		Where siguiente.IncidenciaNumero = propuesta.IncidenciaNumero and siguiente.Origen = 'T' and siguiente.Secuencia > propuesta.Secuencia
		Order By siguiente.Secuencia
	) as aplicada
	Where propuesta.Origen = 'I' and propuesta.FechaClasificacion >= @dInicio and propuesta.FechaClasificacion < @dFin

	-- 7. Tickets registrados en el periodo, por tipo (sin los importados del legado).
	Select ticket.Tipo, TipoDescripcion = tipo.Descripcion, Total = Count(*),
		Resueltos = Sum(Case When ticket.Estado = 'RS' Then 1 Else 0 End),
		Reabiertos = Sum(Case When reapertura.IncidenciaNumero Is Not Null Then 1 Else 0 End),
		DesdeAsistente = Sum(Case When ticket.CanalRegistro = 'ASISTENTE' Then 1 Else 0 End),
		MinutosPromedioPrimeraRespuesta = Avg(Convert(decimal(12,2), DateDiff(minute, ticket.FechaRegistro, ticket.FechaAsignacion))),
		HorasPromedioResolucion = Avg(Case When ticket.Estado = 'RS' Then Convert(decimal(12,2), DateDiff(minute, ticket.FechaRegistro, ticket.FechaCierre)) / 60 End),
		CalificacionPromedio = Convert(decimal(4,2), Avg(Convert(decimal(4,2), ticket.Calificacion)))
	From TI_Incidencia as ticket
	Inner Join TI_Tipo as tipo on tipo.Tipo = ticket.Tipo
	Left Join (Select Distinct IncidenciaNumero From TI_IncidenciaEstado Where Estado = 'RA') as reapertura on reapertura.IncidenciaNumero = ticket.IncidenciaNumero
	Where ticket.CanalRegistro <> 'LEGADO' and ticket.FechaRegistro >= @dInicio and ticket.FechaRegistro < @dFin
	Group By ticket.Tipo, tipo.Descripcion
	Order By Total Desc

	-- 8. Fichas de requerimientos: completas al registrar y devueltas a recopilación por TI.
	Select Requerimientos = Count(*),
		ConFichaCompleta = IsNull(Sum(Case When faltantes.Cantidad = 0 Then 1 Else 0 End), 0),
		DevueltosRecopilacion = IsNull(Sum(Case When devuelto.IncidenciaNumero Is Not Null Then 1 Else 0 End), 0)
	From TI_Incidencia as ticket
	Outer Apply (
		Select Cantidad = Count(*)
		From TI_PlantillaCampo as campo
		Where campo.Tipo = 'REQ' and campo.Estado = 'A' and campo.Obligatorio = 1
			and Not Exists (Select 1 From TI_IncidenciaDato as dato Where dato.IncidenciaNumero = ticket.IncidenciaNumero and dato.Campo = campo.Campo)
	) as faltantes
	Left Join (Select Distinct IncidenciaNumero From TI_IncidenciaEstado Where Estado = 'RC') as devuelto on devuelto.IncidenciaNumero = ticket.IncidenciaNumero
	Where ticket.Tipo = 'REQ' and ticket.CanalRegistro <> 'LEGADO' and ticket.FechaRegistro >= @dInicio and ticket.FechaRegistro < @dFin

	-- 9. Utilidad del conocimiento: veces que un artículo fue evidencia de un diagnóstico y cuántos de esos tickets se resolvieron sin reabrirse.
	Select Top (20) articulo.ConocimientoCodigo, articulo.Titulo,
		VecesEvidencia = Count(*),
		TicketsResueltosSinReapertura = Count(Distinct Case When ticket.Estado = 'RS' and reapertura.IncidenciaNumero Is Null Then ticket.IncidenciaNumero End)
	From TI_IncidenciaDiagnosticoEvidencia as evidencia
	Inner Join TI_IncidenciaDiagnostico as diagnostico on diagnostico.IncidenciaNumero = evidencia.IncidenciaNumero and diagnostico.Secuencia = evidencia.DiagnosticoSecuencia
	Inner Join TI_BaseConocimiento as articulo on articulo.ConocimientoCodigo = evidencia.Referencia
	Inner Join TI_Incidencia as ticket on ticket.IncidenciaNumero = evidencia.IncidenciaNumero
	Left Join (Select Distinct IncidenciaNumero From TI_IncidenciaEstado Where Estado = 'RA') as reapertura on reapertura.IncidenciaNumero = ticket.IncidenciaNumero
	Where evidencia.TipoFuente = 'BASE_CONOCIMIENTO' and diagnostico.FechaDiagnostico >= @dInicio and diagnostico.FechaDiagnostico < @dFin
	Group By articulo.ConocimientoCodigo, articulo.Titulo
	Order By VecesEvidencia Desc

	-- 10. Costo y latencia de las llamadas al modelo registradas por el agente.
	Select Modelo = IsNull(Json_Value(DatosJson, '$.modelo'), ''), Llamadas = Count(*),
		TokensEntrada = IsNull(Sum(Try_Convert(bigint, Json_Value(DatosJson, '$.tokensEntrada'))), 0),
		TokensSalida = IsNull(Sum(Try_Convert(bigint, Json_Value(DatosJson, '$.tokensSalida'))), 0),
		DuracionPromedioMs = Avg(Try_Convert(decimal(12,2), Json_Value(DatosJson, '$.duracionMs'))),
		Fallidas = Sum(Case When Json_Value(DatosJson, '$.exito') = 'false' Then 1 Else 0 End)
	From TI_AgenteEvento
	Where Tipo = 'LLAMADA_MODELO' and Fecha >= @dInicio and Fecha < @dFin
	Group By IsNull(Json_Value(DatosJson, '$.modelo'), '')
	Order By Llamadas Desc

End
Go

/* Ejecuta Procedure */

-- Usp_TI_Obtener_ComparativoAgente 'TEC001', 'TIC', '2026-10-01', '2026-10-31'
Create Or Alter Procedure Usp_TI_Obtener_ComparativoAgente
/*================================================================================
Objetivo        : Comparar, ticket por ticket, el diagnóstico del agente con la causa raíz y la solución que TI registró.
Creado Por      : Jhosep S. Lazo
Fecha Creación  : Oct 2026
SP Anterior     :
Comentario Cambios  : Es la vista del modo sombra: el agente propone sin ejecutar y TI mide cuánto acierta antes de darle autonomía.
================================================================================*/

@cUsuario	varchar(20),
@cArea		char(3),
@dDesde		date,
@dHasta		date

As
Begin
Set NoCount On

	If Not Exists (Select 1 From TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM'))
		Throw 50670, 'El operador TI no es válido.', 1
	If @dDesde Is Null or @dHasta Is Null or @dDesde > @dHasta or DateDiff(day, @dDesde, @dHasta) > 366
		Throw 50671, 'Indica un periodo válido de hasta un año.', 1

	Select Top (200) sesion.SesionNumero, sesion.IncidenciaNumero, ticket.Titulo, EstadoTicket = ticket.Estado, EstadoSesion = sesion.Estado,
		CausaAgente = IsNull(sesion.CausaProbable, N''), sesion.Confianza, AccionCodigo = IsNull(sesion.AccionCodigo, ''), Decision = IsNull(sesion.Decision, ''),
		sesion.SolucionValidada, CausaRaizTI = IsNull(ticket.CausaRaiz, N''), SolucionTI = IsNull(ticket.SolucionTecnica, N''),
		TipoResolucion = IsNull(ticket.TipoResolucion, ''), Reabierto = Convert(bit, Case When reapertura.IncidenciaNumero Is Null Then 0 Else 1 End),
		sesion.FechaDiagnostico
	From TI_AgenteSesion as sesion
	Inner Join TI_Incidencia as ticket on ticket.IncidenciaNumero = sesion.IncidenciaNumero
	Left Join (Select Distinct IncidenciaNumero From TI_IncidenciaEstado Where Estado = 'RA') as reapertura on reapertura.IncidenciaNumero = ticket.IncidenciaNumero
	Where sesion.FechaDiagnostico >= @dDesde and sesion.FechaDiagnostico < DateAdd(day, 1, Convert(datetime2(0), @dHasta))
	Order By sesion.FechaDiagnostico Desc

End
Go

/* Ejecuta Procedure */
