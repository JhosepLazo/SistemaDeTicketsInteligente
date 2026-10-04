/**
 * Archivo: GestionOperativaTIController.cs
 * Objetivo: Exponer al operador TI el registro de avances con esfuerzo, las solicitudes de aprobación y la mesa de ayuda.
 * Responsabilidad: Delegar en GestionOperativaTIBLL con la identidad y el área de la cookie.
 * Dependencias: GestionOperativaTIBLL y la autenticación por cookie.
 * Flujo: GestionTicketsTIPage / AsistenteTIPage -> GestionOperativaTIController -> GestionOperativaTIBLL.
 * Consideraciones: Solo perfiles TEC, SUP y ADM.
 */

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SistemaTicketsInteligente.Api.Controllers;

[ApiController]
[Authorize(Roles = "TEC,SUP,ADM")]
[Route("api/gestion-operativa-ti")]
public sealed class GestionOperativaTIController(GestionOperativaTIBLL gestion) : ControladorBase
{
    [HttpGet("datos")]
    public Task<IActionResult> ObtenerDatos(CancellationToken ct) => Responder(async () => Ok(await gestion.ObtenerDatosAsync(Usuario, Area, ct)));

    [HttpPost("tickets/{incidenciaNumero}/avances")]
    public Task<IActionResult> RegistrarAvance(string incidenciaNumero, RegistrarAvanceDetalladoSolicitud s, CancellationToken ct) =>
        Ejecutar(() => gestion.RegistrarAvanceAsync(Usuario, Area, incidenciaNumero, s, ct));

    [HttpPost("tickets/{incidenciaNumero}/solicitar-aprobacion")]
    public Task<IActionResult> SolicitarAprobacion(string incidenciaNumero, SolicitarAprobacionOperativaSolicitud s, CancellationToken ct) =>
        Ejecutar(() => gestion.SolicitarAprobacionAsync(Usuario, Area, incidenciaNumero, s, ct));

    [HttpPost("tickets/mesa-ayuda")]
    public Task<IActionResult> CrearTicketPorUsuario(CrearTicketMesaAyudaSolicitud s, CancellationToken ct) =>
        Responder(async () => Created("/api/gestion-tickets", await gestion.CrearTicketPorUsuarioAsync(Usuario, Area, s, ct)));
}
