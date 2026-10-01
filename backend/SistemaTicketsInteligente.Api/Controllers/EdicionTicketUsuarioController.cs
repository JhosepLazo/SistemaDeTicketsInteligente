/*
 * Archivo: EdicionTicketUsuarioController.cs
 * Objetivo: Exponer la edición controlada de tickets propios antes de su procesamiento técnico.
 * Responsabilidad: Resolver el usuario desde la sesión y recibir únicamente los campos descriptivos permitidos.
 * Dependencias: EdicionTicketUsuarioBLL, EdicionTicketUsuarioDTO y autenticación por cookie.
 * Flujo: Frontend -> EdicionTicketUsuarioController -> EdicionTicketUsuarioBLL -> EdicionTicketUsuarioDAO -> SQL Server.
 * Consideraciones: Solo aplica a perfil USR; propiedad y estado se vuelven a validar en SQL Server.
 */

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaTicketsInteligente.Api.BLL;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.Controllers;

[ApiController]
[Authorize(Roles = "USR")]
[Route("api/mis-tickets")]
public sealed class EdicionTicketUsuarioController : ControllerBase
{
    private readonly EdicionTicketUsuarioBLL edicionTicketUsuarioBLL;

    public EdicionTicketUsuarioController(EdicionTicketUsuarioBLL edicionTicketUsuarioBLL)
    {
        this.edicionTicketUsuarioBLL = edicionTicketUsuarioBLL;
    }

    [HttpPost("{incidenciaNumero}/editar")]
    public async Task<IActionResult> Editar(string incidenciaNumero, EditarTicketUsuarioSolicitud solicitud, CancellationToken ct)
    {
        var usuario = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(usuario)) return Unauthorized();

        try
        {
            await edicionTicketUsuarioBLL.EditarAsync(usuario, incidenciaNumero, solicitud, ct);
            return NoContent();
        }
        catch (ArgumentException ex) { return BadRequest(new { mensaje = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { mensaje = ex.Message }); }
    }
}
