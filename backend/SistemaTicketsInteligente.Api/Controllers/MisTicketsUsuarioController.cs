/**
 * Archivo: MisTicketsUsuarioController.cs
 * Objetivo: Exponer al colaborador el seguimiento de sus propios tickets.
 * Responsabilidad: Delegar consulta, respuesta a TI, validación de la solución, calificación, descarga de adjuntos y edición temprana.
 * Dependencias: MisTicketsUsuarioBLL y la autenticación por cookie.
 * Flujo: MisTicketsUsuarioPage -> MisTicketsUsuarioController -> MisTicketsUsuarioBLL.
 * Consideraciones: El usuario siempre sale de la cookie; los procedimientos rechazan tickets ajenos.
 */

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SistemaTicketsInteligente.Api.Controllers;

[ApiController]
[Authorize(Roles = "USR")]
[Route("api/mis-tickets")]
public sealed class MisTicketsUsuarioController(MisTicketsUsuarioBLL misTickets) : ControladorBase
{
    [HttpGet]
    public Task<IActionResult> Obtener(CancellationToken ct) => Responder(async () => Ok(await misTickets.ObtenerAsync(Usuario, ct)));

    [HttpGet("{incidenciaNumero}")]
    public Task<IActionResult> ObtenerDetalle(string incidenciaNumero, CancellationToken ct) =>
        Responder(async () => Ok(await misTickets.ObtenerDetalleAsync(Usuario, Area, incidenciaNumero, ct)));

    [HttpPost("{incidenciaNumero}/responder-observacion")]
    [RequestSizeLimit(55 * 1024 * 1024)]
    public Task<IActionResult> ResponderObservacion(string incidenciaNumero, [FromForm] ResponderObservacionSolicitud solicitud, CancellationToken ct) =>
        Ejecutar(() => misTickets.ResponderObservacionAsync(Usuario, incidenciaNumero, solicitud, ct));

    [HttpPost("{incidenciaNumero}/validar-solucion")]
    public Task<IActionResult> ValidarSolucion(string incidenciaNumero, ValidarSolucionSolicitud solicitud, CancellationToken ct) =>
        Ejecutar(() => misTickets.ValidarSolucionAsync(Usuario, incidenciaNumero, solicitud, ct));

    [HttpPost("{incidenciaNumero}/calificar")]
    public Task<IActionResult> Calificar(string incidenciaNumero, CalificarTicketSolicitud solicitud, CancellationToken ct) =>
        Ejecutar(() => misTickets.CalificarAsync(Usuario, incidenciaNumero, solicitud, ct));

    [HttpGet("{incidenciaNumero}/adjuntos/{secuencia:int}")]
    public Task<IActionResult> DescargarAdjunto(string incidenciaNumero, int secuencia, CancellationToken ct) => Responder(async () =>
    {
        var archivo = await misTickets.ObtenerArchivoAsync(Usuario, incidenciaNumero, secuencia, ct);
        return Archivo(archivo.RutaFisica, archivo.TipoMime, archivo.NombreOriginal);
    });

    [HttpPost("{incidenciaNumero}/editar")]
    public Task<IActionResult> Editar(string incidenciaNumero, EditarTicketUsuarioSolicitud solicitud, CancellationToken ct) =>
        Ejecutar(() => misTickets.EditarAsync(Usuario, incidenciaNumero, solicitud, ct));
}
