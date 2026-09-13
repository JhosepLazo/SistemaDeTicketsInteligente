/*
 * Archivo: ReportesTIDTO.cs
 * Objetivo: Definir los contratos utilizados por el módulo Reportes para operadores TI.
 * Responsabilidad: Transportar filtros, indicadores, series, distribuciones, tiempos, tickets destacados y catálogos sin contener reglas de negocio.
 * Dependencias: Ninguna capa de infraestructura; solo tipos base de .NET.
 * Flujo: ReportesTIController <-> ReportesTIBLL <-> ReportesTIDAO <-> Stored Procedure de Reportes TI.
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
    public List<ReportesTIPrioridadTiempo> TiemposPorPrioridad { get; set; } = [];
    public List<ReportesTITicketDestacado> TicketsPrioridadAlta { get; set; } = [];
    public ReportesTICatalogos Catalogos { get; set; } = new();
    public List<ReportesTIDetalleExportacion> DetalleExportacion { get; set; } = [];
}
