/**
 * Archivo: ReproduccionUsuarioController.cs
 * Objetivo: Exponer al colaborador las invitaciones de TI para reproducir su error con pantalla y voz.
 * Responsabilidad: Delegar invitación, consentimiento, Live y evidencia en ReproduccionUsuarioBLL con la identidad de la sesión.
 * Dependencias: ReproduccionUsuarioBLL, autenticación por cookie y rate limiting.
 * Flujo: Portal del colaborador -> ReproduccionUsuarioController -> ReproduccionUsuarioBLL -> Stored Procedures Usp_TI_Reproduccion_*.
 * Consideraciones: Cualquier usuario autenticado puede llamar, pero los procedimientos solo responden si es el invitado de la investigación.
 */

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace SistemaTicketsInteligente.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("Agente")]
[Route("api/reproducciones")]
public sealed class ReproduccionUsuarioController(ReproduccionUsuarioBLL reproduccion) : ControladorBase
{
    [HttpGet]
    public Task<IActionResult> Listar(CancellationToken ct) => Responder(async () => Ok(await reproduccion.ListarAsync(Usuario, ct)));

    [HttpGet("{sesionNumero:long}")]
    public Task<IActionResult> Obtener(long sesionNumero, CancellationToken ct) => Responder(async () => Ok(await reproduccion.ObtenerAsync(Usuario, sesionNumero, ct)));

    [HttpPost("{sesionNumero:long}/responder")]
    public Task<IActionResult> ResponderInvitacion(long sesionNumero, ResponderReproduccionSolicitud solicitud, CancellationToken ct) =>
        Ejecutar(() => reproduccion.ResponderAsync(Usuario, sesionNumero, solicitud, ct));

    [HttpPost("{sesionNumero:long}/live/token")]
    public Task<IActionResult> CrearTokenLive(long sesionNumero, CancellationToken ct) =>
        Responder(async () => Ok(await reproduccion.CrearTokenAsync(Usuario, sesionNumero, ct)));

    [HttpPost("{sesionNumero:long}/eventos")]
    [EnableRateLimiting("AgenteEventos")]
    public Task<IActionResult> RegistrarEvento(long sesionNumero, RegistrarEventoReproduccionSolicitud solicitud, CancellationToken ct) =>
        Ejecutar(() => reproduccion.RegistrarEventoAsync(Usuario, sesionNumero, solicitud, ct));

    [HttpPost("{sesionNumero:long}/grabaciones")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public Task<IActionResult> SubirGrabacion(long sesionNumero, [FromForm] SubirGrabacionSolicitud solicitud, CancellationToken ct) =>
        Responder(async () =>
        {
            if (solicitud.Archivo is null) return BadRequest(new { mensaje = "Adjunta la grabación de la pantalla." });
            return Ok(new { eventoSecuencia = await reproduccion.SubirGrabacionAsync(Usuario, sesionNumero, solicitud.Archivo, solicitud.DuracionSegundos, ct) });
        });

    [HttpPost("{sesionNumero:long}/finalizar")]
    public Task<IActionResult> Finalizar(long sesionNumero, CancellationToken ct) =>
        Ejecutar(() => reproduccion.FinalizarAsync(Usuario, sesionNumero, ct));
}
