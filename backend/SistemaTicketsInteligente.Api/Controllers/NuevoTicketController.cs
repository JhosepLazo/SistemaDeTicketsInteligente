/**
 * Archivo: NuevoTicketController.cs
 * Objetivo: Exponer los endpoints protegidos utilizados por el módulo Nuevo Ticket del usuario.
 * Responsabilidad: Obtener la identidad desde la sesión, entregar datos iniciales del formulario y recibir el registro multipart de una nueva incidencia.
 * Dependencias: NuevoTicketBLL, NuevoTicketDTO, autenticación por cookie y claims de ASP.NET Core.
 * Flujo: Frontend -> NuevoTicketController -> NuevoTicketBLL -> NuevoTicketDAO -> SQL Server.
 * Consideraciones: Nunca recibe el usuario ni el área como datos confiables desde el frontend; ambos se resuelven desde la sesión autenticada y la base de datos.
 */

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaTicketsInteligente.Api.BLL;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.Controllers;

[ApiController]
[Authorize(Roles = "USR")]
[Route("api/tickets/nuevo")]
public sealed class NuevoTicketController : ControllerBase
{
    private readonly NuevoTicketBLL nuevoTicketBLL;

    public NuevoTicketController(NuevoTicketBLL nuevoTicketBLL)
    {
        this.nuevoTicketBLL = nuevoTicketBLL;
    }

    [HttpGet("datos")]
    public async Task<ActionResult<NuevoTicketDatosRespuesta>> ObtenerDatos(CancellationToken cancellationToken)
    {
        var usuario = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(usuario)) return Unauthorized();

        return Ok(await nuevoTicketBLL.ObtenerDatosAsync(usuario, cancellationToken));
    }

    [HttpPost]
    [RequestSizeLimit(55 * 1024 * 1024)]
    public async Task<ActionResult<NuevoTicketCreadoRespuesta>> Crear([FromForm] CrearNuevoTicketSolicitud solicitud, CancellationToken cancellationToken)
    {
        var usuario = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(usuario)) return Unauthorized();

        try
        {
            var creado = await nuevoTicketBLL.CrearAsync(usuario, solicitud, cancellationToken);
            return Created($"/api/tickets/{creado.IncidenciaNumero}", creado);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }
}
