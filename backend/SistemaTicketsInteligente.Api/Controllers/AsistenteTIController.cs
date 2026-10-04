/**
 * Archivo: AsistenteTIController.cs
 * Objetivo: Exponer el Asistente TI y el flujo del Agente de Ingeniería Autónomo.
 * Responsabilidad: Recibir evidencia, iniciar Live, investigar, entregar el expediente y procesar las decisiones de TI
 *   con la identidad de la sesión autenticada.
 * Dependencias: AsistenteTIBLL, autenticación por cookie y rate limiting.
 * Flujo: Frontend -> AsistenteTIController -> AsistenteTIBLL -> Stored Procedures / proveedores de IA autorizados.
 * Consideraciones: Usuario, área, permisos y aprobación nunca se aceptan como autoridad desde el navegador.
 */

using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace SistemaTicketsInteligente.Api.Controllers;

[ApiController]
[Authorize(Roles = "TEC,SUP,ADM")]
[EnableRateLimiting("Agente")]
[Route("api/asistente/ti")]
public sealed class AsistenteTIController(AsistenteTIBLL asistente) : ControladorBase
{
    [HttpPost("mensajes")]
    [EnableRateLimiting("Asistente")]
    public Task<IActionResult> EnviarMensaje(AsistenteTISolicitud solicitud, CancellationToken ct) =>
        Responder(async () => Ok(await asistente.ResponderAsync(Usuario, Area, solicitud, ct)));

    [HttpPost("acciones/confirmar")]
    public Task<IActionResult> Confirmar(ConfirmarAccionTISolicitud solicitud, CancellationToken ct) =>
        Responder(async () => Ok(await asistente.ConfirmarAsync(Usuario, solicitud, ct)));

    [HttpGet("catalogos")]
    public Task<IActionResult> Catalogos(CancellationToken ct) =>
        Responder(async () => Ok(await asistente.ObtenerCatalogosAsync(Usuario, Area, ct)));

    [HttpGet("tickets/{incidenciaNumero}/investigaciones")]
    public Task<IActionResult> ListarPorTicket(string incidenciaNumero, CancellationToken ct) =>
        Responder(async () => Ok(await asistente.ListarPorTicketAsync(Usuario, Area, incidenciaNumero, ct)));

    // Sesión de investigación y evidencia.

    [HttpGet("investigaciones")]
    public Task<IActionResult> Listar([FromQuery] string? alcance, CancellationToken ct) =>
        Responder(async () => Ok(await asistente.ListarAsync(Usuario, Area, string.Equals(alcance, "todas", StringComparison.OrdinalIgnoreCase), ct)));

    [HttpPost("investigaciones")]
    public Task<IActionResult> CrearInvestigacion(CrearInvestigacionTISolicitud solicitud, CancellationToken ct) =>
        Responder(async () => Ok(await asistente.CrearInvestigacionAsync(Usuario, Area, solicitud, ct)));

    [HttpGet("investigaciones/{sesionNumero:long}")]
    public Task<IActionResult> ObtenerInvestigacion(long sesionNumero, CancellationToken ct) =>
        Responder(async () => Ok(await asistente.ObtenerInvestigacionAsync(Usuario, Area, sesionNumero, ct)));

    [HttpPost("investigaciones/{sesionNumero:long}/live/token")]
    public Task<IActionResult> CrearTokenLive(long sesionNumero, CancellationToken ct) =>
        Responder(async () => Ok(await asistente.CrearTokenLiveAsync(Usuario, Area, sesionNumero, ct)));

    [HttpPost("investigaciones/{sesionNumero:long}/eventos")]
    [EnableRateLimiting("AgenteEventos")]
    public Task<IActionResult> RegistrarEvento(long sesionNumero, RegistrarEventoAgenteTISolicitud solicitud, CancellationToken ct) =>
        Ejecutar(() => asistente.RegistrarEventoAsync(Usuario, sesionNumero, solicitud, ct));

    [HttpPost("investigaciones/{sesionNumero:long}/observacion/finalizar")]
    public Task<IActionResult> FinalizarObservacion(long sesionNumero, FinalizarObservacionAgenteTISolicitud solicitud, CancellationToken ct) =>
        Ejecutar(() => asistente.FinalizarObservacionAsync(Usuario, sesionNumero, solicitud, ct));

    [HttpPost("investigaciones/{sesionNumero:long}/grabaciones")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public Task<IActionResult> SubirGrabacion(long sesionNumero, [FromForm] SubirGrabacionSolicitud solicitud, CancellationToken ct) =>
        Responder(async () =>
        {
            if (solicitud.Archivo is null) return BadRequest(new { mensaje = "Adjunta la grabación de la pantalla." });
            return Ok(new { eventoSecuencia = await asistente.SubirGrabacionAsync(Usuario, Area, sesionNumero, solicitud.Archivo, solicitud.DuracionSegundos, ct) });
        });

    // Sin nombre de descarga: el navegador la reproduce en la consola y puede adelantar.
    [HttpGet("investigaciones/{sesionNumero:long}/grabaciones/{eventoSecuencia:int}")]
    public Task<IActionResult> VerGrabacion(long sesionNumero, int eventoSecuencia, CancellationToken ct) =>
        Responder(async () =>
        {
            var grabacion = await asistente.ObtenerGrabacionAsync(Usuario, Area, sesionNumero, eventoSecuencia, ct);
            return Archivo(grabacion.Ruta, grabacion.TipoMime);
        });

    [HttpPost("investigaciones/{sesionNumero:long}/reabrir")]
    public Task<IActionResult> ReabrirObservacion(long sesionNumero, CancellationToken ct) =>
        Ejecutar(() => asistente.ReabrirObservacionAsync(Usuario, Area, sesionNumero, ct));

    [HttpGet("investigaciones/{sesionNumero:long}/codigo")]
    public Task<IActionResult> Codigo(long sesionNumero, CancellationToken ct) =>
        Responder(async () => Ok(await asistente.BuscarCodigoAsync(Usuario, Area, sesionNumero, ct)));

    [HttpPost("investigaciones/{sesionNumero:long}/vincular")]
    public Task<IActionResult> Vincular(long sesionNumero, VincularIncidenciaTISolicitud solicitud, CancellationToken ct) =>
        Ejecutar(() => asistente.VincularAsync(Usuario, Area, sesionNumero, solicitud, ct));

    [HttpPost("investigaciones/{sesionNumero:long}/reasignar")]
    public Task<IActionResult> Reasignar(long sesionNumero, ReasignarInvestigacionTISolicitud solicitud, CancellationToken ct) =>
        Ejecutar(() => asistente.ReasignarAsync(Usuario, Area, sesionNumero, solicitud, ct));

    [HttpPost("investigaciones/{sesionNumero:long}/invitacion")]
    public Task<IActionResult> InvitarUsuario(long sesionNumero, CancellationToken ct) =>
        Responder(async () => Ok(await asistente.InvitarUsuarioAsync(Usuario, Area, sesionNumero, ct)));

    [HttpPost("investigaciones/{sesionNumero:long}/invitacion/cancelar")]
    public Task<IActionResult> CancelarInvitacion(long sesionNumero, CancellationToken ct) =>
        Ejecutar(() => asistente.CancelarInvitacionAsync(Usuario, Area, sesionNumero, ct));

    [HttpPost("investigaciones/{sesionNumero:long}/cancelar")]
    public Task<IActionResult> Cancelar(long sesionNumero, CancellationToken ct) =>
        Ejecutar(() => asistente.CancelarAsync(Usuario, Area, sesionNumero, ct));

    // Investigación y diagnóstico.

    [HttpPost("investigaciones/{sesionNumero:long}/analizar")]
    [EnableRateLimiting("Asistente")]
    public Task<IActionResult> Investigar(long sesionNumero, CancellationToken ct) =>
        Responder(async () => Ok(await asistente.InvestigarAsync(Usuario, Area, sesionNumero, ct)));

    [HttpGet("investigaciones/{sesionNumero:long}/diagnostico")]
    public Task<IActionResult> ObtenerDiagnostico(long sesionNumero, CancellationToken ct) =>
        Responder(async () => Ok(await asistente.ObtenerDiagnosticoAsync(Usuario, Area, sesionNumero, ct)));

    [HttpGet("investigaciones/{sesionNumero:long}/informe")]
    public Task<IActionResult> Informe(long sesionNumero, CancellationToken ct) =>
        Responder(async () => Ok(await asistente.ObtenerInformeAsync(Usuario, Area, sesionNumero, ct)));

    // Decisión de TI.

    [HttpPost("investigaciones/{sesionNumero:long}/grabar-informacion")]
    public Task<IActionResult> GrabarInformacion(long sesionNumero, CancellationToken ct) =>
        Responder(async () =>
        {
            var contexto = await asistente.ObtenerInvestigacionAsync(Usuario, Area, sesionNumero, ct);
            var markdown = await asistente.GrabarInformacionAsync(Usuario, Area, sesionNumero, ct);
            var referencia = string.IsNullOrWhiteSpace(contexto.Sesion.IncidenciaNumero) ? $"AGT-{sesionNumero:000000}" : contexto.Sesion.IncidenciaNumero;
            return File(Encoding.UTF8.GetBytes(markdown), "text/markdown; charset=utf-8", $"{referencia}-investigacion.md");
        });

    [HttpGet("investigaciones/{sesionNumero:long}/dry-run")]
    public Task<IActionResult> DryRun(long sesionNumero, CancellationToken ct) =>
        Responder(async () => Ok(await asistente.DryRunAsync(Usuario, Area, sesionNumero, ct)));

    [HttpPost("investigaciones/{sesionNumero:long}/simular-cambio")]
    public Task<IActionResult> SimularCambio(long sesionNumero, CancellationToken ct) =>
        Responder(async () => Ok(await asistente.SimularCambioAsync(Usuario, Area, sesionNumero, ct)));

    [HttpPost("investigaciones/{sesionNumero:long}/realizar-cambio")]
    public Task<IActionResult> RealizarCambio(long sesionNumero, RealizarCambioAgenteTISolicitud solicitud, CancellationToken ct) =>
        Responder(async () => Ok(await asistente.RealizarCambioAsync(Usuario, Area, sesionNumero, solicitud, ct)));

    [HttpPost("investigaciones/{sesionNumero:long}/validar-solucion")]
    public Task<IActionResult> ValidarSolucion(long sesionNumero, RealizarCambioAgenteTISolicitud solicitud, CancellationToken ct) =>
        Ejecutar(() => asistente.ValidarSolucionAsync(Usuario, Area, sesionNumero, solicitud, ct));

    [HttpPost("investigaciones/{sesionNumero:long}/conocimiento")]
    public Task<IActionResult> Conocimiento(long sesionNumero, CancellationToken ct) =>
        Responder(async () => Ok(new { conocimientoCodigo = await asistente.CrearBorradorAsync(Usuario, Area, sesionNumero, ct) }));
}
