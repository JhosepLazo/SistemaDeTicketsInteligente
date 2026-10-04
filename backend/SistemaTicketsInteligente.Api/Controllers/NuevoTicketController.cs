/**
 * Archivo: NuevoTicketController.cs
 * Objetivo: Exponer el registro de tickets del colaborador.
 * Responsabilidad: Entregar los datos del formulario y recibir el ticket con sus adjuntos y la evidencia del Asistente TI.
 * Dependencias: NuevoTicketBLL y la autenticación por cookie.
 * Flujo: NuevoTicketPage -> NuevoTicketController -> NuevoTicketBLL.
 * Consideraciones: El límite del formulario admite hasta 2 grabaciones de pantalla (40 MB c/u) además de los documentos.
 */

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SistemaTicketsInteligente.Api.Controllers;

[ApiController]
[Authorize(Roles = "USR")]
[Route("api/tickets/nuevo")]
public sealed class NuevoTicketController(NuevoTicketBLL nuevoTicket) : ControladorBase
{
    [HttpGet("datos")]
    public Task<IActionResult> ObtenerDatos(CancellationToken ct) => Responder(async () => Ok(await nuevoTicket.ObtenerDatosAsync(Usuario, ct)));

    [HttpPost]
    [RequestSizeLimit(100 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 100 * 1024 * 1024)]
    public Task<IActionResult> Crear([FromForm] CrearNuevoTicketSolicitud solicitud, CancellationToken ct) => Responder(async () =>
    {
        var creado = await nuevoTicket.CrearAsync(Usuario, solicitud, ct);
        return Created($"/api/tickets/{creado.IncidenciaNumero}", creado);
    });
}
