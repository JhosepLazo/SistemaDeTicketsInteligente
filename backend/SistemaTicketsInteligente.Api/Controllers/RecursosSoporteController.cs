/**
 * Archivo: RecursosSoporteController.cs
 * Objetivo: Exponer los formatos y artículos de ayuda que el colaborador consulta al registrar un ticket.
 * Responsabilidad: Delegar la consulta y la descarga de formatos en RecursosSoporteBLL.
 * Dependencias: RecursosSoporteBLL y la autenticación por cookie.
 * Flujo: NuevoTicketPage -> RecursosSoporteController -> RecursosSoporteBLL.
 * Consideraciones: Las descargas se hacen por código de formato; el frontend nunca conoce rutas físicas.
 */

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SistemaTicketsInteligente.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/recursos-soporte")]
public sealed class RecursosSoporteController(RecursosSoporteBLL recursos) : ControladorBase
{
    [HttpGet]
    public Task<IActionResult> Obtener(CancellationToken ct) => Responder(async () => Ok(await recursos.ObtenerAsync(ct)));

    [HttpGet("formatos/{formatoCodigo}")]
    public Task<IActionResult> DescargarFormato(string formatoCodigo, CancellationToken ct) => Responder(async () =>
    {
        var archivo = await recursos.ObtenerArchivoAsync(formatoCodigo, ct);
        return Archivo(archivo.RutaFisica, archivo.TipoMime, archivo.NombreOriginal);
    });
}
