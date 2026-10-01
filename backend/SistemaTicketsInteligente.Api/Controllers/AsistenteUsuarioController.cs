/**
 * Archivo: AsistenteUsuarioController.cs
 * Objetivo: Exponer el Asistente TI para colaboradores autenticados.
 * Responsabilidad: Resolver la identidad desde la cookie, limitar solicitudes y delegar la orientación segura a la BLL.
 * Dependencias: AsistenteUsuarioBLL, autenticación y rate limiting de ASP.NET Core.
 * Flujo: React -> POST /api/asistente/usuario -> BLL -> conocimiento/tickets propios -> IA opcional.
 * Consideraciones: No acepta identidad ni perfil desde el cliente.
 */

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SistemaTicketsInteligente.Api.BLL;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.Controllers;

[ApiController]
[Authorize(Roles = "USR")]
[EnableRateLimiting("Asistente")]
[Route("api/asistente/usuario")]
public sealed class AsistenteUsuarioController : ControllerBase
{
    private readonly AsistenteUsuarioBLL asistenteUsuarioBLL;

    public AsistenteUsuarioController(AsistenteUsuarioBLL asistenteUsuarioBLL)
    {
        this.asistenteUsuarioBLL = asistenteUsuarioBLL;
    }

    [HttpPost("mensajes")]
    public async Task<ActionResult<AsistenteUsuarioRespuesta>> Responder(AsistenteUsuarioSolicitud solicitud, CancellationToken ct)
    {
        var usuario = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(usuario)) return Unauthorized();

        try { return Ok(await asistenteUsuarioBLL.ResponderAsync(usuario, solicitud, ct)); }
        catch (ArgumentException ex) { return BadRequest(new { mensaje = ex.Message }); }
    }
}
