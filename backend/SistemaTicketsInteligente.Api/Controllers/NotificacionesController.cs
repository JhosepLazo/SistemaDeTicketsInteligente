/**
 * Archivo: NotificacionesController.cs
 * Objetivo: Exponer la campana de notificaciones del usuario autenticado.
 * Responsabilidad: Delegar la consulta de avisos y la marca de lectura en NotificacionesBLL.
 * Dependencias: NotificacionesBLL y la autenticación por cookie.
 * Flujo: NotificacionesCampana -> NotificacionesController -> NotificacionesBLL.
 * Consideraciones: La ruta de cada aviso es orientativa; cada módulo destino mantiene su propia autorización.
 */

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SistemaTicketsInteligente.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/notificaciones")]
public sealed class NotificacionesController(NotificacionesBLL notificaciones) : ControladorBase
{
    [HttpGet]
    public Task<IActionResult> Obtener(CancellationToken ct) => Responder(async () => Ok(await notificaciones.ObtenerAsync(Usuario, ct)));

    [HttpPost("{notificacionNumero:long}/leida")]
    public Task<IActionResult> MarcarLeida(long notificacionNumero, CancellationToken ct) =>
        Ejecutar(() => notificaciones.MarcarLeidaAsync(Usuario, notificacionNumero, ct));
}
