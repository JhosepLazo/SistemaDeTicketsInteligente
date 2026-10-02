/**
 * Archivo: AsistenteTIController.cs
 * Objetivo: Exponer el Asistente TI y el flujo del Agente de Ingeniería Autónomo.
 * Responsabilidad: Obtener identidad desde sesión, recibir evidencia, iniciar Live, investigar, generar expediente y procesar decisiones autenticadas de TI.
 * Dependencias: AsistenteTIBLL, autenticación por cookie, rate limiting y DTO del agente.
 * Flujo: Frontend -> Controller -> AsistenteTIBLL -> DAO/proveedores autorizados.
 * Consideraciones: Usuario, área, permisos y aprobación nunca se aceptan como autoridad desde el navegador.
 */

using System.Security.Claims;
using System.Text;
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
        Ejecutar(async identidad => Ok(await asistente.ResponderAsync(identidad.Usuario, solicitud, ct)));

    [HttpPost("acciones/confirmar")]
    public Task<IActionResult> Confirmar(ConfirmarAccionTISolicitud solicitud, CancellationToken ct) =>
        Ejecutar(async identidad => Ok(await asistente.ConfirmarAsync(identidad.Usuario, solicitud, ct)));

    [HttpPost("investigaciones")]
    public Task<IActionResult> CrearInvestigacion(CrearInvestigacionTISolicitud solicitud, CancellationToken ct) =>
        Ejecutar(async identidad => Ok(await asistente.CrearInvestigacionAsync(identidad.Usuario, identidad.Area, solicitud, ct)));

    [HttpGet("investigaciones/{sesionNumero:long}")]
    public Task<IActionResult> ObtenerInvestigacion(long sesionNumero, CancellationToken ct) =>
        Ejecutar(async identidad => Ok(await asistente.ObtenerInvestigacionAsync(identidad.Usuario, identidad.Area, sesionNumero, ct)));

    [HttpPost("investigaciones/{sesionNumero:long}/eventos")]
    public Task<IActionResult> RegistrarEvento(long sesionNumero, RegistrarEventoAgenteTISolicitud solicitud, CancellationToken ct) =>
        Ejecutar(async identidad =>
        {
            await asistente.RegistrarEventoAsync(identidad.Usuario, sesionNumero, solicitud, ct);
            return NoContent();
        });

    [HttpPost("investigaciones/{sesionNumero:long}/observacion/finalizar")]
    public Task<IActionResult> FinalizarObservacion(long sesionNumero, FinalizarObservacionAgenteTISolicitud solicitud, CancellationToken ct) =>
        Ejecutar(async identidad =>
        {
            await asistente.FinalizarObservacionAsync(identidad.Usuario, sesionNumero, solicitud, ct);
            return NoContent();
        });

    [HttpPost("investigaciones/{sesionNumero:long}/live/token")]
    public Task<IActionResult> CrearTokenLive(long sesionNumero, CancellationToken ct) =>
        Ejecutar(async identidad => Ok(await asistente.CrearTokenLiveAsync(identidad.Usuario, identidad.Area, sesionNumero, ct)));

    [HttpPost("investigaciones/{sesionNumero:long}/analizar")]
    public Task<IActionResult> Investigar(long sesionNumero, CancellationToken ct) =>
        Ejecutar(async identidad => Ok(await asistente.InvestigarAsync(identidad.Usuario, identidad.Area, sesionNumero, ct)));

    [HttpPost("investigaciones/{sesionNumero:long}/grabar-informacion")]
    public Task<IActionResult> GrabarInformacion(long sesionNumero, CancellationToken ct) =>
        Ejecutar(async identidad =>
        {
            var contexto = await asistente.ObtenerInvestigacionAsync(identidad.Usuario, identidad.Area, sesionNumero, ct);
            var markdown = await asistente.GrabarInformacionAsync(identidad.Usuario, identidad.Area, sesionNumero, ct);
            var referencia = string.IsNullOrWhiteSpace(contexto.Sesion.IncidenciaNumero) ? $"AGT-{sesionNumero:000000}" : contexto.Sesion.IncidenciaNumero;
            return File(Encoding.UTF8.GetBytes(markdown), "text/markdown; charset=utf-8", $"{referencia}-investigacion.md");
        });

    [HttpPost("investigaciones/{sesionNumero:long}/realizar-cambio")]
    public Task<IActionResult> RealizarCambio(long sesionNumero, RealizarCambioAgenteTISolicitud solicitud, CancellationToken ct) =>
        Ejecutar(async identidad => Ok(await asistente.RealizarCambioAsync(identidad.Usuario, identidad.Area, sesionNumero, solicitud, ct)));

    private async Task<IActionResult> Ejecutar(Func<(string Usuario, string Area), Task<IActionResult>> accion)
    {
        var identidad = ObtenerIdentidad();
        if (identidad is null) return Unauthorized();
        try { return await accion(identidad.Value); }
        catch (ArgumentException ex) { return BadRequest(new { mensaje = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { mensaje = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { mensaje = ex.Message }); }
    }

    private (string Usuario, string Area)? ObtenerIdentidad()
    {
        var usuario = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var area = User.FindFirst("Area")?.Value;
        return string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(area) ? null : (usuario, area);
    }
}
