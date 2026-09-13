/*
 * Archivo: ReportesTIBLL.cs
 * Objetivo: Aplicar las validaciones funcionales mínimas del módulo Reportes para operadores TI.
 * Responsabilidad: Normalizar filtros, validar rango de fechas y delegar la consulta consolidada al DAO.
 * Dependencias: ReportesTIDAO y ReportesTIDTO.
 * Flujo: ReportesTIController -> ReportesTIBLL -> ReportesTIDAO -> SQL Server.
 * Consideraciones: El rango máximo se limita a 366 días para evitar consultas excesivas desde la interfaz y mantener el módulo simple y predecible.
 */

using SistemaTicketsInteligente.Api.DAO;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.BLL;

public sealed class ReportesTIBLL
{
    private static readonly HashSet<string> PrioridadesPermitidas = new(StringComparer.OrdinalIgnoreCase)
    {
        "ALTA", "MEDIA", "BAJA", "SIN"
    };

    private readonly ReportesTIDAO reportesTIDAO;

    public ReportesTIBLL(ReportesTIDAO reportesTIDAO)
    {
        this.reportesTIDAO = reportesTIDAO;
    }

    public Task<ReportesTIRespuesta> ObtenerAsync(ReportesTIFiltros filtros, CancellationToken cancellationToken = default)
    {
        NormalizarYValidar(filtros);
        return reportesTIDAO.ObtenerAsync(filtros, cancellationToken);
    }

    private static void NormalizarYValidar(ReportesTIFiltros filtros)
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

    private static string? Normalizar(string? valor)
    {
        var normalizado = valor?.Trim();
        return string.IsNullOrWhiteSpace(normalizado) ? null : normalizado;
    }
}
