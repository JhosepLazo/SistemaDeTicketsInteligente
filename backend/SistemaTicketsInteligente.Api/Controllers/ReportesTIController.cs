/*
 * Archivo: ReportesTIController.cs
 * Objetivo: Exponer la consulta protegida del módulo Reportes para operadores TI autenticados.
 * Responsabilidad: Recibir filtros de consulta, delegar validación y procesamiento a la BLL y devolver respuestas HTTP claras.
 * Dependencias: ReportesTIBLL, ReportesTIDTO y autenticación por cookie de ASP.NET Core.
 * Flujo: Frontend -> ReportesTIController -> ReportesTIBLL -> ReportesTIDAO -> SQL Server.
 * Consideraciones: El endpoint es solo de lectura y está restringido a perfiles TI; no programa reportes ni envía información fuera del sistema.
 */

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaTicketsInteligente.Api.BLL;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.Controllers;

[ApiController]
[Authorize(Roles = "TEC,SUP,ADM")]
[Route("api/reportes/ti")]
public sealed class ReportesTIController : ControllerBase
{
    private readonly ReportesTIBLL reportesTIBLL;

    public ReportesTIController(ReportesTIBLL reportesTIBLL)
    {
        this.reportesTIBLL = reportesTIBLL;
    }

    [HttpGet]
    public async Task<ActionResult<ReportesTIRespuesta>> Obtener([FromQuery] ReportesTIFiltros filtros, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await reportesTIBLL.ObtenerAsync(filtros, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { mensaje = ex.Message });
        }
    }
}
