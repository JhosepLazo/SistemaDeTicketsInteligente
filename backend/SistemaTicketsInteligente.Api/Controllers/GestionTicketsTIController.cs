/**
 * Archivo: GestionTicketsTIController.cs
 * Objetivo: Exponer la bandeja de tickets del operador TI y sus acciones.
 * Responsabilidad: Delegar consulta, clasificación (y su propuesta con IA), asignación, información, resolución, No Procede,
 *   aprobaciones, ficha y descargas en GestionTicketsTIBLL con la identidad y el área de la cookie.
 * Dependencias: GestionTicketsTIBLL y la autenticación por cookie.
 * Flujo: GestionTicketsTIPage -> GestionTicketsTIController -> GestionTicketsTIBLL.
 * Consideraciones: Solo perfiles TEC, SUP y ADM; los avances se registran en GestionOperativaTIController.
 */

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace SistemaTicketsInteligente.Api.Controllers;

[ApiController]
[Authorize(Roles = "TEC,SUP,ADM")]
[Route("api/gestion-tickets")]
public sealed class GestionTicketsTIController(GestionTicketsTIBLL gestion) : ControladorBase
{
    [HttpGet]
    public Task<IActionResult> Obtener(CancellationToken ct) => Responder(async () => Ok(await gestion.ObtenerAsync(Usuario, Area, ct)));

    [HttpGet("{incidenciaNumero}")]
    public Task<IActionResult> ObtenerDetalle(string incidenciaNumero, CancellationToken ct) =>
        Responder(async () => Ok(await gestion.ObtenerDetalleAsync(Usuario, Area, incidenciaNumero, ct)));

    [HttpPost("{incidenciaNumero}/clasificar")]
    public Task<IActionResult> Clasificar(string incidenciaNumero, ClasificarTicketTISolicitud s, CancellationToken ct) =>
        Ejecutar(() => gestion.ClasificarAsync(Usuario, Area, incidenciaNumero, s, ct));

    /// <summary>Historial de clasificaciones: propuestas de la IA (I) y clasificaciones aplicadas por TI (T).</summary>
    [HttpGet("{incidenciaNumero}/clasificaciones")]
    public Task<IActionResult> ObtenerClasificaciones(string incidenciaNumero, CancellationToken ct) =>
        Responder(async () => Ok(await gestion.ObtenerClasificacionesAsync(Usuario, Area, incidenciaNumero, ct)));

    /// <summary>La IA propone una clasificación; no cambia el ticket hasta que TI la aplique.</summary>
    [HttpPost("{incidenciaNumero}/clasificacion/proponer")]
    [EnableRateLimiting("Asistente")]
    public Task<IActionResult> ProponerClasificacion(string incidenciaNumero, CancellationToken ct) =>
        Responder(async () => Ok(await gestion.ProponerClasificacionAsync(Usuario, Area, incidenciaNumero, ct)));

    [HttpGet("{incidenciaNumero}/ficha")]
    public Task<IActionResult> ObtenerFicha(string incidenciaNumero, CancellationToken ct) =>
        Responder(async () => Ok(await gestion.ObtenerFichaAsync(Usuario, Area, incidenciaNumero, ct)));

    [HttpPost("{incidenciaNumero}/asignar")]
    public Task<IActionResult> Asignar(string incidenciaNumero, AsignarTicketTISolicitud s, CancellationToken ct) =>
        Ejecutar(() => gestion.AsignarAsync(Usuario, Area, incidenciaNumero, s, ct));

    [HttpPost("{incidenciaNumero}/solicitar-informacion")]
    public Task<IActionResult> SolicitarInformacion(string incidenciaNumero, SolicitarInformacionTISolicitud s, CancellationToken ct) =>
        Ejecutar(() => gestion.SolicitarInformacionAsync(Usuario, Area, incidenciaNumero, s, ct));

    [HttpPost("{incidenciaNumero}/resolver")]
    public Task<IActionResult> Resolver(string incidenciaNumero, ResolverTicketTISolicitud s, CancellationToken ct) =>
        Ejecutar(() => gestion.ResolverAsync(Usuario, Area, incidenciaNumero, s, ct));

    [HttpPost("{incidenciaNumero}/no-procede")]
    public Task<IActionResult> NoProcede(string incidenciaNumero, NoProcedeTicketTISolicitud s, CancellationToken ct) =>
        Ejecutar(() => gestion.NoProcedeAsync(Usuario, Area, incidenciaNumero, s, ct));

    [HttpPost("{incidenciaNumero}/aprobaciones/{secuencia:int}")]
    public Task<IActionResult> ResponderAprobacion(string incidenciaNumero, int secuencia, ResponderAprobacionTISolicitud s, CancellationToken ct) =>
        Ejecutar(() => gestion.ResponderAprobacionAsync(Usuario, Area, incidenciaNumero, secuencia, s, ct));

    [HttpGet("{incidenciaNumero}/adjuntos/{secuencia:int}")]
    public Task<IActionResult> DescargarAdjunto(string incidenciaNumero, int secuencia, CancellationToken ct) => Responder(async () =>
    {
        var archivo = await gestion.ObtenerArchivoAsync(Usuario, Area, incidenciaNumero, secuencia, ct);
        return Archivo(archivo.RutaFisica, archivo.TipoMime, archivo.NombreOriginal);
    });
}
