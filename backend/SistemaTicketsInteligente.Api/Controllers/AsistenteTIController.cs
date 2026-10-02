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
[EnableRateLimiting("Agente")]
[Route("api/asistente/ti")]
public sealed class AsistenteTIController : ControllerBase
{
    private readonly AsistenteTIBLL asistente;

    public AsistenteTIController(AsistenteTIBLL asistente)
    {
        this.asistente = asistente;
    }

    [HttpPost("mensajes")]
    [EnableRateLimiting("Asistente")]
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
    [EnableRateLimiting("AgenteEventos")]
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
    [EnableRateLimiting("Asistente")]
    public Task<IActionResult> Investigar(long sesionNumero, CancellationToken ct) =>
        Ejecutar(async identidad => Ok(await asistente.InvestigarAsync(identidad.Usuario, identidad.Area, sesionNumero, ct)));

    [HttpGet("investigaciones/{sesionNumero:long}/diagnostico")]
    public Task<IActionResult> ObtenerDiagnostico(long sesionNumero, CancellationToken ct) =>
        Ejecutar(async identidad => Ok(await asistente.ObtenerDiagnosticoAsync(identidad.Usuario, identidad.Area, sesionNumero, ct)));

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

    [HttpGet("investigaciones")]
    public Task<IActionResult> Listar([FromQuery] string? alcance, CancellationToken ct) =>
        Ejecutar(async i => Ok(await asistente.ListarAsync(i.Usuario, i.Area, string.Equals(alcance, "todas", StringComparison.OrdinalIgnoreCase), ct)));

    [HttpGet("catalogos")]
    public Task<IActionResult> Catalogos(CancellationToken ct) => Ejecutar(async i => Ok(await asistente.ObtenerCatalogosAsync(i.Usuario, i.Area, ct)));

    [HttpGet("tickets/{incidenciaNumero}/investigaciones")]
    public Task<IActionResult> ListarPorTicket(string incidenciaNumero, CancellationToken ct) =>
        Ejecutar(async i => Ok(await asistente.ListarPorTicketAsync(i.Usuario, i.Area, incidenciaNumero, ct)));

    [HttpGet("investigaciones/{sesionNumero:long}/informe")]
    public Task<IActionResult> Informe(long sesionNumero, CancellationToken ct) =>
        Ejecutar(async i => Ok(await asistente.ObtenerInformeAsync(i.Usuario, i.Area, sesionNumero, ct)));

    [HttpPost("investigaciones/{sesionNumero:long}/reasignar")]
    public Task<IActionResult> Reasignar(long sesionNumero, ReasignarInvestigacionTISolicitud solicitud, CancellationToken ct) =>
        Ejecutar(async i => { await asistente.ReasignarAsync(i.Usuario, i.Area, sesionNumero, solicitud, ct); return NoContent(); });

    [HttpPost("investigaciones/{sesionNumero:long}/cancelar")]
    public Task<IActionResult> Cancelar(long sesionNumero, CancellationToken ct) => Ejecutar(async i => { await asistente.CancelarAsync(i.Usuario, i.Area, sesionNumero, ct); return NoContent(); });

    [HttpPost("investigaciones/{sesionNumero:long}/vincular")]
    public Task<IActionResult> Vincular(long sesionNumero, VincularIncidenciaTISolicitud solicitud, CancellationToken ct) => Ejecutar(async i => { await asistente.VincularAsync(i.Usuario, i.Area, sesionNumero, solicitud, ct); return NoContent(); });

    [HttpGet("investigaciones/{sesionNumero:long}/dry-run")]
    public Task<IActionResult> DryRun(long sesionNumero, CancellationToken ct) => Ejecutar(async i => Ok(await asistente.DryRunAsync(i.Usuario, i.Area, sesionNumero, ct)));

    [HttpGet("investigaciones/{sesionNumero:long}/codigo")]
    public Task<IActionResult> Codigo(long sesionNumero, CancellationToken ct) => Ejecutar(async i => Ok(await asistente.BuscarCodigoAsync(i.Usuario, i.Area, sesionNumero, ct)));

    [HttpPost("investigaciones/{sesionNumero:long}/validar-solucion")]
    public Task<IActionResult> ValidarSolucion(long sesionNumero, RealizarCambioAgenteTISolicitud solicitud, CancellationToken ct) => Ejecutar(async i => { await asistente.ValidarSolucionAsync(i.Usuario, i.Area, sesionNumero, solicitud, ct); return NoContent(); });

    [HttpPost("investigaciones/{sesionNumero:long}/conocimiento")]
    public Task<IActionResult> Conocimiento(long sesionNumero, CancellationToken ct) => Ejecutar(async i => Ok(new { conocimientoCodigo = await asistente.CrearBorradorAsync(i.Usuario, i.Area, sesionNumero, ct) }));

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
