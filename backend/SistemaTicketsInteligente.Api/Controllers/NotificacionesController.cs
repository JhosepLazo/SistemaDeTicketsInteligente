/*
 * Archivo: NotificacionesController.cs
 * Objetivo: Exponer la campana de notificaciones para cualquier usuario autenticado.
 * Responsabilidad: Obtener la identidad desde la sesión y permitir consultar o marcar como leídas únicamente sus propias notificaciones.
 * Dependencias: NotificacionesBLL, autenticación por cookie y claims de ASP.NET Core.
 * Flujo: Frontend -> NotificacionesController -> NotificacionesBLL -> NotificacionesDAO -> SQL Server.
 * Consideraciones: No recibe el usuario desde el frontend y no sustituye la autorización de los módulos enlazados.
 */

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaTicketsInteligente.Api.BLL;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/notificaciones")]
public sealed class NotificacionesController : ControllerBase
{
    private readonly NotificacionesBLL notificacionesBLL;

    public NotificacionesController(NotificacionesBLL notificacionesBLL)
    {
        this.notificacionesBLL = notificacionesBLL;
    }

    [HttpGet]
    public async Task<ActionResult<NotificacionesRespuesta>> Obtener(CancellationToken cancellationToken)
    {
        var usuario = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(usuario)) return Unauthorized();
        return Ok(await notificacionesBLL.ObtenerAsync(usuario, cancellationToken));
    }

    [HttpPost("{notificacionNumero:long}/leida")]
    public async Task<IActionResult> MarcarLeida(long notificacionNumero, CancellationToken cancellationToken)
    {
        var usuario = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(usuario)) return Unauthorized();

        try
        {
            await notificacionesBLL.MarcarLeidaAsync(usuario, notificacionNumero, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }
}
