/**
 * Endpoint del Asistente TI con respuestas operativas y acciones confirmables.
 */

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SistemaTicketsInteligente.Api.BLL;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.Controllers;

[ApiController]
[Authorize(Roles = "TEC,SUP,ADM")]
[EnableRateLimiting("Asistente")]
[Route("api/asistente/ti")]
public sealed class AsistenteTIController : ControllerBase
{
    private readonly AsistenteTIBLL asistente;

    public AsistenteTIController(AsistenteTIBLL asistente)
    {
        this.asistente = asistente;
    }

    [HttpPost("mensajes")]
    public Task<IActionResult> Responder(AsistenteTISolicitud solicitud, CancellationToken ct) =>
        Ejecutar(async operador => Ok(await asistente.ResponderAsync(operador, solicitud, ct)));

    [HttpPost("acciones/confirmar")]
    public Task<IActionResult> Confirmar(ConfirmarAccionTISolicitud solicitud, CancellationToken ct) =>
        Ejecutar(async operador => Ok(await asistente.ConfirmarAsync(operador, solicitud, ct)));

    private async Task<IActionResult> Ejecutar(Func<string, Task<IActionResult>> accion)
    {
        var operador = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(operador)) return Unauthorized();
        try { return await accion(operador); }
        catch (ArgumentException ex) { return BadRequest(new { mensaje = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { mensaje = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { mensaje = ex.Message }); }
    }
}
