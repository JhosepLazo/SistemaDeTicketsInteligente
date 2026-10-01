/*
 * Archivo: GestionOperativaTIController.cs
 * Objetivo: Exponer las mejoras operativas complementarias de Gestión de Tickets para TEC/SUP/ADM.
 * Responsabilidad: Consultar catálogos pequeños, registrar esfuerzo/área causante, solicitar aprobación y crear tickets a nombre de otro usuario desde mesa de ayuda.
 * Dependencias: GestionOperativaTIBLL, GestionOperativaTIDTO y autenticación por cookie.
 * Flujo: Frontend -> GestionOperativaTIController -> GestionOperativaTIBLL -> GestionOperativaTIDAO -> SQL Server.
 * Consideraciones: La identidad del operador se obtiene exclusivamente de la sesión; no implementa IA ni ejecución automática de acciones.
 */

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaTicketsInteligente.Api.BLL;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.Controllers;

[ApiController]
[Authorize(Roles = "TEC,SUP,ADM")]
[Route("api/gestion-operativa-ti")]
public sealed class GestionOperativaTIController : ControllerBase
{
    private readonly GestionOperativaTIBLL gestionOperativaTIBLL;

    public GestionOperativaTIController(GestionOperativaTIBLL gestionOperativaTIBLL)
    {
        this.gestionOperativaTIBLL = gestionOperativaTIBLL;
    }

    [HttpGet("datos")]
    public async Task<ActionResult<GestionOperativaTIDatos>> ObtenerDatos(CancellationToken ct)
    {
        var identidad = Identidad();
        if (identidad is null) return Unauthorized();
        try { return Ok(await gestionOperativaTIBLL.ObtenerDatosAsync(identidad.Value.Usuario, identidad.Value.Area, ct)); }
        catch (ArgumentException ex) { return BadRequest(new { mensaje = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { mensaje = ex.Message }); }
    }

    [HttpPost("tickets/{incidenciaNumero}/avances")]
    public Task<IActionResult> RegistrarAvance(string incidenciaNumero, RegistrarAvanceDetalladoSolicitud s, CancellationToken ct) =>
        Ejecutar((usuario, area) => gestionOperativaTIBLL.RegistrarAvanceAsync(usuario, area, incidenciaNumero, s, ct));

    [HttpPost("tickets/{incidenciaNumero}/solicitar-aprobacion")]
    public Task<IActionResult> SolicitarAprobacion(string incidenciaNumero, SolicitarAprobacionOperativaSolicitud s, CancellationToken ct) =>
        Ejecutar((usuario, area) => gestionOperativaTIBLL.SolicitarAprobacionAsync(usuario, area, incidenciaNumero, s, ct));

    [HttpPost("tickets/mesa-ayuda")]
    public async Task<IActionResult> CrearTicketPorUsuario(CrearTicketMesaAyudaSolicitud s, CancellationToken ct)
    {
        var identidad = Identidad();
        if (identidad is null) return Unauthorized();
        try { return Created("/api/gestion-tickets", await gestionOperativaTIBLL.CrearTicketPorUsuarioAsync(identidad.Value.Usuario, identidad.Value.Area, s, ct)); }
        catch (ArgumentException ex) { return BadRequest(new { mensaje = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { mensaje = ex.Message }); }
    }

    private async Task<IActionResult> Ejecutar(Func<string, string, Task> accion)
    {
        var identidad = Identidad();
        if (identidad is null) return Unauthorized();
        try { await accion(identidad.Value.Usuario, identidad.Value.Area); return NoContent(); }
        catch (ArgumentException ex) { return BadRequest(new { mensaje = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { mensaje = ex.Message }); }
    }

    private (string Usuario, string Area)? Identidad()
    {
        var usuario = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var area = User.FindFirst("Area")?.Value;
        return string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(area) ? null : (usuario, area);
    }
}
