/**
 * Archivo: ReportesTIDTO.cs
 * Objetivo: Definir los contratos utilizados por el módulo Reportes para operadores TI.
 * Responsabilidad: Transportar filtros, indicadores, series, distribuciones, tiempos, tickets destacados y catálogos sin contener reglas de negocio.
 * Dependencias: Ninguna capa de infraestructura; solo tipos base de .NET.
 * Flujo: ReportesTIController <-> ReportesTIBLL <-> Stored Procedure de Reportes TI.
 * Consideraciones: Las métricas comparativas conservan los valores del período actual y del período anterior equivalente para que el frontend presente variaciones sin inventar datos.
 */

namespace SistemaTicketsInteligente.Api.DTO;

public sealed class ReportesTIFiltros
{
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public string? Area { get; set; }
    public string? Estado { get; set; }
    public string? Prioridad { get; set; }
    public string? Tipo { get; set; }
    public string? UsuarioTI { get; set; }
}

public sealed class ReportesTIResumen
{
    public int Total { get; set; }
    public int Resueltos { get; set; }
    public int Reabiertos { get; set; }
    public decimal? TiempoPromedioHoras { get; set; }
    public decimal? Satisfaccion { get; set; }
    public decimal? CumplimientoSla { get; set; }
    public int TotalAnterior { get; set; }
    public int ResueltosAnterior { get; set; }
    public decimal? TiempoPromedioHorasAnterior { get; set; }
    public decimal? SatisfaccionAnterior { get; set; }
    public decimal HorasEfectivas { get; set; }
    public int TicketsConEsfuerzo { get; set; }
}

public sealed class ReportesTIEvolucion
{
    public DateTime Fecha { get; set; }
    public int Cantidad { get; set; }
}

public sealed class ReportesTIEstado
{
    public string Estado { get; set; } = string.Empty;
    public string EstadoDescripcion { get; set; } = string.Empty;
    public int Cantidad { get; set; }
}

public sealed class ReportesTIArea
{
    public string Area { get; set; } = string.Empty;
    public string AreaDescripcion { get; set; } = string.Empty;
    public int Cantidad { get; set; }
}

public sealed class ReportesTIAvance
{
    public string IncidenciaNumero { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string AreaDescripcion { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string EstadoDescripcion { get; set; } = string.Empty;
    public string Responsable { get; set; } = string.Empty;
    public decimal? PorcentajeAvance { get; set; }
    public DateTime? FechaUltimoAvance { get; set; }
    public int AvancesRegistrados { get; set; }
    public decimal MinutosRegistrados { get; set; }
}

public sealed class ReportesTIUsuario
{
    public string Usuario { get; set; } = string.Empty;
    public string Responsable { get; set; } = string.Empty;
    public int TicketsAsignados { get; set; }
    public int TicketsResueltos { get; set; }
    public int TicketsEnCurso { get; set; }
    public int AvancesRegistrados { get; set; }
    public decimal MinutosRegistrados { get; set; }
    public decimal? PorcentajePromedio { get; set; }
}

public sealed class ReportesTIPrioridadTiempo
{
    public string Prioridad { get; set; } = string.Empty;
    public int Tickets { get; set; }
    public decimal? TiempoPromedioHoras { get; set; }
    public decimal? TiempoMasRapidoHoras { get; set; }
    public decimal? TiempoMasLargoHoras { get; set; }
}

public sealed class ReportesTITicketDestacado
{
    public string IncidenciaNumero { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string AreaDescripcion { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string EstadoDescripcion { get; set; } = string.Empty;
    public int? Prioridad { get; set; }
    public string Responsable { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; }
    public int TiempoAbiertoHoras { get; set; }
}

public sealed class ReportesTIDetalleExportacion
{
    public string IncidenciaNumero { get; set; } = string.Empty;
    public string Solicitante { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string AreaDescripcion { get; set; } = string.Empty;
    public string TipoDescripcion { get; set; } = string.Empty;
    public string EstadoDescripcion { get; set; } = string.Empty;
    public int? Prioridad { get; set; }
    public string Responsable { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; }
    public DateTime? FechaCierre { get; set; }
    public byte? Calificacion { get; set; }
}

public sealed class ReportesTICatalogo
{
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
}

public sealed class ReportesTICatalogos
{
    public List<ReportesTICatalogo> Areas { get; set; } = [];
    public List<ReportesTICatalogo> Estados { get; set; } = [];
    public List<ReportesTICatalogo> Tipos { get; set; } = [];
    public List<ReportesTICatalogo> Operadores { get; set; } = [];
}

public sealed class ReportesTIRespuesta
{
    public ReportesTIResumen Resumen { get; set; } = new();
    public List<ReportesTIEvolucion> Evolucion { get; set; } = [];
    public List<ReportesTIEstado> Estados { get; set; } = [];
    public List<ReportesTIArea> Areas { get; set; } = [];
    public List<ReportesTIAvance> Avances { get; set; } = [];
    public List<ReportesTIUsuario> AvancePorUsuario { get; set; } = [];
    public List<ReportesTIPrioridadTiempo> TiemposPorPrioridad { get; set; } = [];
    public List<ReportesTITicketDestacado> TicketsPrioridadAlta { get; set; } = [];
    public ReportesTICatalogos Catalogos { get; set; } = new();
    public List<ReportesTIDetalleExportacion> DetalleExportacion { get; set; } = [];
}

/// <summary>Indicadores del agente y del flujo de tickets (Anexo B del plan de mejoras) y comparación agente frente a TI.</summary>
public sealed class MetricasAgenteTIRespuesta
{
    public MetricasInvestigacionesTI Investigaciones { get; set; } = new();
    public List<ConteoTI> EstadosInvestigacion { get; set; } = [];
    public List<MetricasAprobacionesTI> Aprobaciones { get; set; } = [];
    public List<ConteoTI> MotivosRechazo { get; set; } = [];
    public List<MetricaEjecucionTI> Ejecuciones { get; set; } = [];
    public MetricasClasificacionTI Clasificacion { get; set; } = new();
    public List<MetricaTipoTicketTI> Tipos { get; set; } = [];
    public MetricasFichaTI Fichas { get; set; } = new();
    public List<MetricaConocimientoTI> Conocimiento { get; set; } = [];
    public List<MetricaModeloTI> Modelos { get; set; } = [];
    public List<ComparativoAgenteTI> Comparativo { get; set; } = [];
}

public sealed class MetricasInvestigacionesTI
{
    public int Total { get; set; }
    public int Automaticas { get; set; }
    public int ConDiagnostico { get; set; }
    public int ConAccionPropuesta { get; set; }
    public int SolucionValidada { get; set; }
    public int Canceladas { get; set; }
    public decimal? ConfianzaPromedio { get; set; }
    public decimal? MinutosPromedioDiagnostico { get; set; }
}

public sealed class ConteoTI
{
    public string Valor { get; set; } = string.Empty;
    public int Cantidad { get; set; }
}

public sealed class MetricasAprobacionesTI
{
    /// <summary>AGENTE o MANUAL.</summary>
    public string Origen { get; set; } = string.Empty;
    public int Solicitadas { get; set; }
    public int Aprobadas { get; set; }
    public int Rechazadas { get; set; }
    public int Canceladas { get; set; }
    public int Pendientes { get; set; }
    public int Vencidas { get; set; }
    public decimal? HorasPromedioRespuesta { get; set; }
}

public sealed class MetricaEjecucionTI
{
    /// <summary>T: decidida por TI. I: autónoma.</summary>
    public string TipoEjecutor { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public int FilasAfectadas { get; set; }
}

public sealed class MetricasClasificacionTI
{
    public int Propuestas { get; set; }
    public int Comparadas { get; set; }
    public int CoincideTipo { get; set; }
    public int CoincideSubTipo { get; set; }
    public int CoincideItem { get; set; }
    public decimal? ConfianzaPromedio { get; set; }
}

public sealed class MetricaTipoTicketTI
{
    public string Tipo { get; set; } = string.Empty;
    public string TipoDescripcion { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Resueltos { get; set; }
    public int Reabiertos { get; set; }
    public int DesdeAsistente { get; set; }
    public decimal? MinutosPromedioPrimeraRespuesta { get; set; }
    public decimal? HorasPromedioResolucion { get; set; }
    public decimal? CalificacionPromedio { get; set; }
}

public sealed class MetricasFichaTI
{
    public int Requerimientos { get; set; }
    public int ConFichaCompleta { get; set; }
    public int DevueltosRecopilacion { get; set; }
}

public sealed class MetricaConocimientoTI
{
    public string ConocimientoCodigo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public int VecesEvidencia { get; set; }
    public int TicketsResueltosSinReapertura { get; set; }
}

public sealed class MetricaModeloTI
{
    public string Modelo { get; set; } = string.Empty;
    public int Llamadas { get; set; }
    public long TokensEntrada { get; set; }
    public long TokensSalida { get; set; }
    public decimal? DuracionPromedioMs { get; set; }
    public int Fallidas { get; set; }
}

/// <summary>Lo que propuso el agente frente a lo que TI registró al resolver (vista del modo sombra).</summary>
public sealed class ComparativoAgenteTI
{
    public long SesionNumero { get; set; }
    public string IncidenciaNumero { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string EstadoTicket { get; set; } = string.Empty;
    public string EstadoSesion { get; set; } = string.Empty;
    public string CausaAgente { get; set; } = string.Empty;
    public decimal? Confianza { get; set; }
    public string AccionCodigo { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public bool SolucionValidada { get; set; }
    public string CausaRaizTI { get; set; } = string.Empty;
    public string SolucionTI { get; set; } = string.Empty;
    public string TipoResolucion { get; set; } = string.Empty;
    public bool Reabierto { get; set; }
    public DateTime? FechaDiagnostico { get; set; }
}
