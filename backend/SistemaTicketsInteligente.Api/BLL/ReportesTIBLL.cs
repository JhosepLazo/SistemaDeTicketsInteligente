/**
 * Archivo: ReportesTIBLL.cs
 * Objetivo: Calcular los reportes de gestión de TI para un rango de fechas y filtros.
 * Responsabilidad: Validar los filtros, leer indicadores, evolución, distribución, avances, tiempos y detalle exportable,
 *   y completar el resumen con el esfuerzo efectivo registrado.
 * Dependencias: BaseDatos (Usp_TI_Obtener_ReportesTI, Usp_TI_Obtener_EsfuerzoOperativoTI, Usp_TI_Obtener_MetricasAgente y
 *   Usp_TI_Obtener_ComparativoAgente).
 * Flujo: ReportesTIController -> ReportesTIBLL -> Stored Procedures -> ReportesTIDTO.
 * Consideraciones: El rango máximo es de 366 días; los filtros vacíos se envían como NULL (sin filtrar).
 */

namespace SistemaTicketsInteligente.Api.BLL;

public sealed class ReportesTIBLL(BaseDatos baseDatos)
{
    private static readonly HashSet<string> PrioridadesPermitidas = new(StringComparer.OrdinalIgnoreCase) { "ALTA", "MEDIA", "BAJA", "SIN" };

    /// <summary>Indicadores del agente (Anexo B del plan de mejoras) y la comparación de su diagnóstico con lo que registró TI.</summary>
    public async Task<MetricasAgenteTIRespuesta> ObtenerAgenteAsync(string usuario, string area, DateTime desde, DateTime hasta, CancellationToken ct)
    {
        var (usuarioValido, areaValida) = (Validacion.Usuario(usuario), Validacion.Area(area));
        if (desde.Date > hasta.Date || (hasta.Date - desde.Date).TotalDays > 366) throw new ArgumentException("Indica un periodo válido de hasta un año.");
        void Parametros(SqlParameterCollection p)
        {
            p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuarioValido;
            p.Add("@cArea", SqlDbType.Char, 3).Value = areaValida;
            p.Add("@dDesde", SqlDbType.Date).Value = desde.Date;
            p.Add("@dHasta", SqlDbType.Date).Value = hasta.Date;
        }
        static ConteoTI Conteo(SqlDataReader f, string columna) => new() { Valor = f.Texto(columna), Cantidad = f.Entero("Cantidad") };
        var respuesta = await baseDatos.LeerAsync("dbo.Usp_TI_Obtener_MetricasAgente", Parametros, async lector => new MetricasAgenteTIRespuesta
        {
            Investigaciones = await lector.FilaAsync(f => new MetricasInvestigacionesTI
            {
                Total = f.Entero("Total"), Automaticas = f.Entero("Automaticas"), ConDiagnostico = f.Entero("ConDiagnostico"),
                ConAccionPropuesta = f.Entero("ConAccionPropuesta"), SolucionValidada = f.Entero("SolucionValidada"), Canceladas = f.Entero("Canceladas"),
                ConfianzaPromedio = f.DecimalNulo("ConfianzaPromedio"), MinutosPromedioDiagnostico = f.DecimalNulo("MinutosPromedioDiagnostico")
            }, ct) ?? new(),
            EstadosInvestigacion = await lector.ListaAsync(f => Conteo(f, "Estado"), ct),
            Aprobaciones = await lector.ListaAsync(f => new MetricasAprobacionesTI
            {
                Origen = f.Texto("Origen"), Solicitadas = f.Entero("Solicitadas"), Aprobadas = f.Entero("Aprobadas"), Rechazadas = f.Entero("Rechazadas"),
                Canceladas = f.Entero("Canceladas"), Pendientes = f.Entero("Pendientes"), Vencidas = f.Entero("Vencidas"),
                HorasPromedioRespuesta = f.DecimalNulo("HorasPromedioRespuesta")
            }, ct),
            MotivosRechazo = await lector.ListaAsync(f => Conteo(f, "Motivo"), ct),
            Ejecuciones = await lector.ListaAsync(f => new MetricaEjecucionTI
            {
                TipoEjecutor = f.Texto("TipoEjecutor"), Estado = f.Texto("Estado"), Cantidad = f.Entero("Cantidad"), FilasAfectadas = f.Entero("FilasAfectadas")
            }, ct),
            Clasificacion = await lector.FilaAsync(f => new MetricasClasificacionTI
            {
                Propuestas = f.Entero("Propuestas"), Comparadas = f.Entero("Comparadas"), CoincideTipo = f.Entero("CoincideTipo"),
                CoincideSubTipo = f.Entero("CoincideSubTipo"), CoincideItem = f.Entero("CoincideItem"), ConfianzaPromedio = f.DecimalNulo("ConfianzaPromedio")
            }, ct) ?? new(),
            Tipos = await lector.ListaAsync(f => new MetricaTipoTicketTI
            {
                Tipo = f.Texto("Tipo"), TipoDescripcion = f.Texto("TipoDescripcion"), Total = f.Entero("Total"), Resueltos = f.Entero("Resueltos"),
                Reabiertos = f.Entero("Reabiertos"), DesdeAsistente = f.Entero("DesdeAsistente"),
                MinutosPromedioPrimeraRespuesta = f.DecimalNulo("MinutosPromedioPrimeraRespuesta"), HorasPromedioResolucion = f.DecimalNulo("HorasPromedioResolucion"),
                CalificacionPromedio = f.DecimalNulo("CalificacionPromedio")
            }, ct),
            Fichas = await lector.FilaAsync(f => new MetricasFichaTI
            {
                Requerimientos = f.Entero("Requerimientos"), ConFichaCompleta = f.Entero("ConFichaCompleta"), DevueltosRecopilacion = f.Entero("DevueltosRecopilacion")
            }, ct) ?? new(),
            Conocimiento = await lector.ListaAsync(f => new MetricaConocimientoTI
            {
                ConocimientoCodigo = f.Texto("ConocimientoCodigo"), Titulo = f.Texto("Titulo"), VecesEvidencia = f.Entero("VecesEvidencia"),
                TicketsResueltosSinReapertura = f.Entero("TicketsResueltosSinReapertura")
            }, ct),
            Modelos = await lector.ListaAsync(f => new MetricaModeloTI
            {
                Modelo = f.Texto("Modelo"), Llamadas = f.Entero("Llamadas"), TokensEntrada = f.Largo("TokensEntrada"), TokensSalida = f.Largo("TokensSalida"),
                DuracionPromedioMs = f.DecimalNulo("DuracionPromedioMs"), Fallidas = f.Entero("Fallidas")
            }, ct)
        }, ct);
        respuesta.Comparativo = await baseDatos.LeerAsync("dbo.Usp_TI_Obtener_ComparativoAgente", Parametros, lector => lector.ListaAsync(f => new ComparativoAgenteTI
        {
            SesionNumero = f.Largo("SesionNumero"), IncidenciaNumero = f.Texto("IncidenciaNumero"), Titulo = f.Texto("Titulo"), EstadoTicket = f.Texto("EstadoTicket"),
            EstadoSesion = f.Texto("EstadoSesion"), CausaAgente = f.Texto("CausaAgente"), Confianza = f.DecimalNulo("Confianza"), AccionCodigo = f.Texto("AccionCodigo"),
            Decision = f.Texto("Decision"), SolucionValidada = f.Booleano("SolucionValidada"), CausaRaizTI = f.Texto("CausaRaizTI"), SolucionTI = f.Texto("SolucionTI"),
            TipoResolucion = f.Texto("TipoResolucion"), Reabierto = f.Booleano("Reabierto"), FechaDiagnostico = f.FechaNula("FechaDiagnostico")
        }, ct), ct);
        return respuesta;
    }

    public async Task<ReportesTIRespuesta> ObtenerAsync(ReportesTIFiltros filtros, CancellationToken ct)
    {
        Validar(filtros);
        var respuesta = await baseDatos.LeerAsync("dbo.Usp_TI_Obtener_ReportesTI", p => Filtros(p, filtros), async lector =>
        {
            var r = new ReportesTIRespuesta();
            r.Resumen = await lector.FilaAsync(f => new ReportesTIResumen
            {
                Total = f.Entero("Total"), Resueltos = f.Entero("Resueltos"), Reabiertos = f.Entero("Reabiertos"), TiempoPromedioHoras = f.DecimalNulo("TiempoPromedioHoras"),
                Satisfaccion = f.DecimalNulo("Satisfaccion"), CumplimientoSla = f.DecimalNulo("CumplimientoSla"), TotalAnterior = f.Entero("TotalAnterior"),
                ResueltosAnterior = f.Entero("ResueltosAnterior"), TiempoPromedioHorasAnterior = f.DecimalNulo("TiempoPromedioHorasAnterior"),
                SatisfaccionAnterior = f.DecimalNulo("SatisfaccionAnterior")
            }, ct) ?? new();
            r.Evolucion = await lector.ListaAsync(f => new ReportesTIEvolucion { Fecha = f.Fecha("Fecha"), Cantidad = f.Entero("Cantidad") }, ct);
            r.Estados = await lector.ListaAsync(f => new ReportesTIEstado { Estado = f.Texto("Estado"), EstadoDescripcion = f.Texto("EstadoDescripcion"), Cantidad = f.Entero("Cantidad") }, ct);
            r.Areas = await lector.ListaAsync(f => new ReportesTIArea { Area = f.Texto("Area"), AreaDescripcion = f.Texto("AreaDescripcion"), Cantidad = f.Entero("Cantidad") }, ct);
            r.Avances = await lector.ListaAsync(f => new ReportesTIAvance
            {
                IncidenciaNumero = f.Texto("IncidenciaNumero"), Titulo = f.Texto("Titulo"), AreaDescripcion = f.Texto("AreaDescripcion"), Estado = f.Texto("Estado"),
                EstadoDescripcion = f.Texto("EstadoDescripcion"), Responsable = f.Texto("Responsable"), PorcentajeAvance = f.DecimalNulo("PorcentajeAvance"),
                FechaUltimoAvance = f.FechaNula("FechaUltimoAvance"), AvancesRegistrados = f.Entero("AvancesRegistrados"),
                MinutosRegistrados = f.DecimalNulo("MinutosRegistrados") ?? 0
            }, ct);
            r.AvancePorUsuario = await lector.ListaAsync(f => new ReportesTIUsuario
            {
                Usuario = f.Texto("Usuario"), Responsable = f.Texto("Responsable"), TicketsAsignados = f.Entero("TicketsAsignados"), TicketsResueltos = f.Entero("TicketsResueltos"),
                TicketsEnCurso = f.Entero("TicketsEnCurso"), AvancesRegistrados = f.Entero("AvancesRegistrados"), MinutosRegistrados = f.DecimalNulo("MinutosRegistrados") ?? 0,
                PorcentajePromedio = f.DecimalNulo("PorcentajePromedio")
            }, ct);
            r.TiemposPorPrioridad = await lector.ListaAsync(f => new ReportesTIPrioridadTiempo
            {
                Prioridad = f.Texto("Prioridad"), Tickets = f.Entero("Tickets"), TiempoPromedioHoras = f.DecimalNulo("TiempoPromedioHoras"),
                TiempoMasRapidoHoras = f.DecimalNulo("TiempoMasRapidoHoras"), TiempoMasLargoHoras = f.DecimalNulo("TiempoMasLargoHoras")
            }, ct);
            r.TicketsPrioridadAlta = await lector.ListaAsync(f => new ReportesTITicketDestacado
            {
                IncidenciaNumero = f.Texto("IncidenciaNumero"), Titulo = f.Texto("Titulo"), AreaDescripcion = f.Texto("AreaDescripcion"), Estado = f.Texto("Estado"),
                EstadoDescripcion = f.Texto("EstadoDescripcion"), Prioridad = f.EnteroNulo("Prioridad"), Responsable = f.Texto("Responsable"),
                FechaRegistro = f.Fecha("FechaRegistro"), TiempoAbiertoHoras = f.Entero("TiempoAbiertoHoras")
            }, ct);
            r.Catalogos.Areas = await lector.ListaAsync(Catalogo, ct);
            r.Catalogos.Estados = await lector.ListaAsync(Catalogo, ct);
            r.Catalogos.Tipos = await lector.ListaAsync(Catalogo, ct);
            r.Catalogos.Operadores = await lector.ListaAsync(Catalogo, ct);
            r.DetalleExportacion = await lector.ListaAsync(f => new ReportesTIDetalleExportacion
            {
                IncidenciaNumero = f.Texto("IncidenciaNumero"), Solicitante = f.Texto("Solicitante"), Titulo = f.Texto("Titulo"), AreaDescripcion = f.Texto("AreaDescripcion"),
                TipoDescripcion = f.Texto("TipoDescripcion"), EstadoDescripcion = f.Texto("EstadoDescripcion"), Prioridad = f.EnteroNulo("Prioridad"),
                Responsable = f.Texto("Responsable"), FechaRegistro = f.Fecha("FechaRegistro"), FechaCierre = f.FechaNula("FechaCierre"), Calificacion = f.ByteNulo("Calificacion")
            }, ct);
            return r;
        }, ct);

        // El esfuerzo efectivo (minutos registrados en los avances) se calcula aparte con los mismos filtros.
        var esfuerzo = await baseDatos.LeerAsync("dbo.Usp_TI_Obtener_EsfuerzoOperativoTI", p => Filtros(p, filtros),
            lector => lector.FilaAsync(f => new { Horas = f.DecimalNulo("HorasEfectivas") ?? 0, Tickets = f.Entero("TicketsConEsfuerzo") }, ct), ct);
        if (esfuerzo is not null)
        {
            respuesta.Resumen.HorasEfectivas = esfuerzo.Horas;
            respuesta.Resumen.TicketsConEsfuerzo = esfuerzo.Tickets;
        }
        return respuesta;
    }

    private static void Filtros(SqlParameterCollection p, ReportesTIFiltros f)
    {
        p.Add("@dFechaInicio", SqlDbType.Date).Value = f.FechaInicio.Date;
        p.Add("@dFechaFin", SqlDbType.Date).Value = f.FechaFin.Date;
        p.Add("@cArea", SqlDbType.Char, 3).Value = BaseDatos.Opcional(f.Area);
        p.Add("@cEstado", SqlDbType.Char, 2).Value = BaseDatos.Opcional(f.Estado);
        p.Add("@cPrioridad", SqlDbType.VarChar, 10).Value = BaseDatos.Opcional(f.Prioridad);
        p.Add("@cTipo", SqlDbType.Char, 3).Value = BaseDatos.Opcional(f.Tipo);
        p.Add("@cUsuarioTI", SqlDbType.VarChar, 20).Value = BaseDatos.Opcional(f.UsuarioTI);
    }

    private static void Validar(ReportesTIFiltros filtros)
    {
        filtros.FechaInicio = filtros.FechaInicio.Date;
        filtros.FechaFin = filtros.FechaFin.Date;
        filtros.Area = Normalizar(filtros.Area);
        filtros.Estado = Normalizar(filtros.Estado);
        filtros.Prioridad = Normalizar(filtros.Prioridad)?.ToUpperInvariant();
        filtros.Tipo = Normalizar(filtros.Tipo);
        filtros.UsuarioTI = Normalizar(filtros.UsuarioTI);
        if (filtros.FechaInicio == default || filtros.FechaFin == default) throw new ArgumentException("Selecciona el rango de fechas del reporte.");
        if (filtros.FechaInicio > filtros.FechaFin) throw new ArgumentException("La fecha inicial no puede ser mayor que la fecha final.");
        if ((filtros.FechaFin - filtros.FechaInicio).TotalDays > 365) throw new ArgumentException("El rango máximo permitido es de 366 días.");
        if (filtros.Area is { Length: > 0 } && filtros.Area.Length != 3) throw new ArgumentException("El área seleccionada no es válida.");
        if (filtros.Estado is { Length: > 0 } && filtros.Estado.Length != 2) throw new ArgumentException("El estado seleccionado no es válido.");
        if (filtros.Tipo is { Length: > 0 } && filtros.Tipo.Length != 3) throw new ArgumentException("El tipo seleccionado no es válido.");
        if (filtros.UsuarioTI is { Length: > 20 }) throw new ArgumentException("El responsable seleccionado no es válido.");
        if (filtros.Prioridad is not null && !PrioridadesPermitidas.Contains(filtros.Prioridad)) throw new ArgumentException("La prioridad seleccionada no es válida.");
    }

    private static string? Normalizar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static ReportesTICatalogo Catalogo(SqlDataReader f) => new() { Codigo = f.Texto("Codigo"), Descripcion = f.Texto("Descripcion") };
}
