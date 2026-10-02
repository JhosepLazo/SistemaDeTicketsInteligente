/**
 * Archivo: ReproduccionUsuarioController.cs
 * Objetivo: Exponer al usuario final las invitaciones de TI para reproducir su error con pantalla y voz.
 * Responsabilidad: Obtener la identidad desde la sesión y delegar invitación, consentimiento, Live y evidencia en ReproduccionUsuarioBLL.
 * Dependencias: ReproduccionUsuarioBLL, autenticación por cookie y rate limiting.
 * Flujo: Frontend (portal del usuario) -> Controller -> ReproduccionUsuarioBLL -> AsistenteTIDAO.
 * Consideraciones: Cualquier usuario autenticado puede llamar, pero los SP solo responden si es el invitado de la investigación.
 */

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SistemaTicketsInteligente.Api.BLL;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("Agente")]
[Route("api/reproducciones")]
public sealed class ReproduccionUsuarioController : ControllerBase
{
    private readonly ReproduccionUsuarioBLL reproduccion;

    public ReproduccionUsuarioController(ReproduccionUsuarioBLL reproduccion)
    {
        this.reproduccion = reproduccion;
    }

    [HttpGet]
    public Task<IActionResult> Listar(CancellationToken ct) => Ejecutar(async usuario => Ok(await reproduccion.ListarAsync(usuario, ct)));

    [HttpGet("{sesionNumero:long}")]
    public Task<IActionResult> Obtener(long sesionNumero, CancellationToken ct) => Ejecutar(async usuario => Ok(await reproduccion.ObtenerAsync(usuario, sesionNumero, ct)));

    [HttpPost("{sesionNumero:long}/responder")]
    public Task<IActionResult> Responder(long sesionNumero, ResponderReproduccionSolicitud solicitud, CancellationToken ct) =>
        Ejecutar(async usuario => { await reproduccion.ResponderAsync(usuario, sesionNumero, solicitud, ct); return NoContent(); });

    [HttpPost("{sesionNumero:long}/live/token")]
    public Task<IActionResult> CrearTokenLive(long sesionNumero, CancellationToken ct) =>
        Ejecutar(async usuario => Ok(await reproduccion.CrearTokenAsync(usuario, sesionNumero, ct)));

    [HttpPost("{sesionNumero:long}/eventos")]
    [EnableRateLimiting("AgenteEventos")]
    public Task<IActionResult> RegistrarEvento(long sesionNumero, RegistrarEventoReproduccionSolicitud solicitud, CancellationToken ct) =>
        Ejecutar(async usuario => { await reproduccion.RegistrarEventoAsync(usuario, sesionNumero, solicitud, ct); return NoContent(); });

    [HttpPost("{sesionNumero:long}/finalizar")]
    public Task<IActionResult> Finalizar(long sesionNumero, CancellationToken ct) =>
        Ejecutar(async usuario => { await reproduccion.FinalizarAsync(usuario, sesionNumero, ct); return NoContent(); });

    private async Task<IActionResult> Ejecutar(Func<string, Task<IActionResult>> accion)
    {
        var usuario = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(usuario)) return Unauthorized();
        try { return await accion(usuario); }
        catch (ArgumentException ex) { return BadRequest(new { mensaje = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { mensaje = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { mensaje = ex.Message }); }
    }
}
