/**
 * Archivo: AsistenteUsuarioController.cs
 * Objetivo: Exponer el Asistente TI para colaboradores autenticados.
 * Responsabilidad: Limitar solicitudes y delegar la orientación segura, la sesión Live y el borrador del ticket en AsistenteUsuarioBLL.
 * Dependencias: AsistenteUsuarioBLL, autenticación y rate limiting de ASP.NET Core.
 * Flujo: React -> /api/asistente/usuario -> AsistenteUsuarioBLL -> conocimiento/tickets propios -> IA opcional.
 * Consideraciones: No acepta identidad ni perfil desde el cliente.
 */

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace SistemaTicketsInteligente.Api.Controllers;

[ApiController]
[Authorize(Roles = "USR")]
[EnableRateLimiting("Asistente")]
[Route("api/asistente/usuario")]
public sealed class AsistenteUsuarioController(AsistenteUsuarioBLL asistente) : ControladorBase
{
    [HttpPost("mensajes")]
    public Task<IActionResult> EnviarMensaje(AsistenteUsuarioSolicitud solicitud, CancellationToken ct) =>
        Responder(async () => Ok(await asistente.ResponderAsync(Usuario, solicitud, ct)));

    /// <summary>Token Live para que el colaborador muestre el error compartiendo su pantalla.</summary>
    [HttpPost("live/token")]
    public Task<IActionResult> CrearTokenLive(LiveUsuarioSolicitud solicitud, CancellationToken ct) =>
        Responder(async () =>
        {
            // Superar el límite de sesiones por hora no es un conflicto: el colaborador debe esperar (429).
            try { return Ok(await asistente.CrearTokenLiveAsync(Usuario, solicitud, ct)); }
            catch (InvalidOperationException ex) { return StatusCode(StatusCodes.Status429TooManyRequests, new { mensaje = ex.Message }); }
        });

    /// <summary>Convierte la evidencia mostrada en pantalla en un borrador de ticket (título, descripción y mensaje de error).</summary>
    [HttpPost("evidencia/borrador")]
    public Task<IActionResult> PrepararBorrador(EvidenciaReproduccionUsuarioSolicitud solicitud, CancellationToken ct) =>
        Responder(async () => Ok(await asistente.PrepararBorradorAsync(solicitud, ct)));
}
