/**
 * Archivo: ReportesTIController.cs
 * Objetivo: Exponer los reportes de gestión de TI.
 * Responsabilidad: Recibir los filtros de la consulta y delegar el cálculo en ReportesTIBLL.
 * Dependencias: ReportesTIBLL y la autenticación por cookie.
 * Flujo: ReportesTIPage -> ReportesTIController -> ReportesTIBLL.
 * Consideraciones: Solo perfiles TEC, SUP y ADM.
 */

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SistemaTicketsInteligente.Api.Controllers;

[ApiController]
[Authorize(Roles = "TEC,SUP,ADM")]
[Route("api/reportes/ti")]
public sealed class ReportesTIController(ReportesTIBLL reportes) : ControladorBase
{
    [HttpGet]
    public Task<IActionResult> Obtener([FromQuery] ReportesTIFiltros filtros, CancellationToken ct) => Responder(async () => Ok(await reportes.ObtenerAsync(filtros, ct)));

    /// <summary>Indicadores del agente y comparación con TI para el periodo indicado.</summary>
    [HttpGet("agente")]
    public Task<IActionResult> ObtenerAgente([FromQuery] DateTime desde, [FromQuery] DateTime hasta, CancellationToken ct) =>
        Responder(async () => Ok(await reportes.ObtenerAgenteAsync(Usuario, Area, desde, hasta, ct)));
}
