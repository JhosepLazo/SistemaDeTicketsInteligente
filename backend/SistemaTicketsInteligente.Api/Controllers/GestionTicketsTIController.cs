/*
 * Archivo: GestionTicketsTIController.cs
 * Objetivo: Exponer los endpoints protegidos utilizados por el módulo Gestión de Tickets para operadores TI.
 * Responsabilidad: Obtener identidad/área desde la sesión, consultar bandeja/detalle y recibir únicamente las acciones operativas permitidas para el ciclo de atención.
 * Dependencias: GestionTicketsTIBLL, GestionTicketsTIDTO, autenticación por cookie y claims de ASP.NET Core.
 * Flujo: Frontend -> GestionTicketsTIController -> GestionTicketsTIBLL -> GestionTicketsTIDAO -> SQL Server.
 * Consideraciones: Nunca acepta usuario, área o perfil como datos confiables del frontend; la ejecución de acciones automáticas de negocio no forma parte de este módulo.
 */

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaTicketsInteligente.Api.BLL;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.Controllers;

[ApiController]
[Authorize(Roles = "TEC,SUP,ADM")]
[Route("api/gestion-tickets")]
public sealed class GestionTicketsTIController : ControllerBase
{
    private readonly GestionTicketsTIBLL gestionTicketsTIBLL;

    public GestionTicketsTIController(GestionTicketsTIBLL gestionTicketsTIBLL)
    {
        this.gestionTicketsTIBLL = gestionTicketsTIBLL;
    }

    [HttpGet]
    public async Task<ActionResult<GestionTicketsTIRespuesta>> Obtener(CancellationToken cancellationToken)
    {
        var identidad = ObtenerIdentidad();
        if (identidad is null) return Unauthorized();

        try
        {
            return Ok(await gestionTicketsTIBLL.ObtenerAsync(identidad.Value.Usuario, identidad.Value.Area, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { mensaje = ex.Message });
        }
    }

    [HttpGet("{incidenciaNumero}")]
    public async Task<ActionResult<GestionTicketTIDetalle>> ObtenerDetalle(string incidenciaNumero, CancellationToken cancellationToken)
    {
        var identidad = ObtenerIdentidad();
        if (identidad is null) return Unauthorized();

        try
        {
            return Ok(await gestionTicketsTIBLL.ObtenerDetalleAsync(identidad.Value.Usuario, identidad.Value.Area, incidenciaNumero, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { mensaje = ex.Message });
        }
    }

    [HttpPost("{incidenciaNumero}/clasificar")]
    public Task<IActionResult> Clasificar(string incidenciaNumero, ClasificarTicketTISolicitud solicitud, CancellationToken cancellationToken) =>
        EjecutarAccion(incidenciaNumero, (usuario, area) => gestionTicketsTIBLL.ClasificarAsync(usuario, area, incidenciaNumero, solicitud, cancellationToken));

    [HttpPost("{incidenciaNumero}/asignar")]
    public Task<IActionResult> Asignar(string incidenciaNumero, AsignarTicketTISolicitud solicitud, CancellationToken cancellationToken) =>
        EjecutarAccion(incidenciaNumero, (usuario, area) => gestionTicketsTIBLL.AsignarAsync(usuario, area, incidenciaNumero, solicitud, cancellationToken));

    [HttpPost("{incidenciaNumero}/avances")]
    public Task<IActionResult> RegistrarAvance(string incidenciaNumero, RegistrarAvanceTISolicitud solicitud, CancellationToken cancellationToken) =>
        EjecutarAccion(incidenciaNumero, (usuario, area) => gestionTicketsTIBLL.RegistrarAvanceAsync(usuario, area, incidenciaNumero, solicitud, cancellationToken));

    [HttpPost("{incidenciaNumero}/solicitar-informacion")]
    public Task<IActionResult> SolicitarInformacion(string incidenciaNumero, SolicitarInformacionTISolicitud solicitud, CancellationToken cancellationToken) =>
        EjecutarAccion(incidenciaNumero, (usuario, area) => gestionTicketsTIBLL.SolicitarInformacionAsync(usuario, area, incidenciaNumero, solicitud, cancellationToken));

    [HttpPost("{incidenciaNumero}/resolver")]
    public Task<IActionResult> Resolver(string incidenciaNumero, ResolverTicketTISolicitud solicitud, CancellationToken cancellationToken) =>
        EjecutarAccion(incidenciaNumero, (usuario, area) => gestionTicketsTIBLL.ResolverAsync(usuario, area, incidenciaNumero, solicitud, cancellationToken));

    [HttpPost("{incidenciaNumero}/no-procede")]
    public Task<IActionResult> NoProcede(string incidenciaNumero, NoProcedeTicketTISolicitud solicitud, CancellationToken cancellationToken) =>
        EjecutarAccion(incidenciaNumero, (usuario, area) => gestionTicketsTIBLL.NoProcedeAsync(usuario, area, incidenciaNumero, solicitud, cancellationToken));

    [HttpPost("{incidenciaNumero}/aprobaciones/{secuencia:int}")]
    public Task<IActionResult> ResponderAprobacion(string incidenciaNumero, int secuencia, ResponderAprobacionTISolicitud solicitud, CancellationToken cancellationToken) =>
        EjecutarAccion(incidenciaNumero, (usuario, area) => gestionTicketsTIBLL.ResponderAprobacionAsync(usuario, area, incidenciaNumero, secuencia, solicitud, cancellationToken));

    [HttpGet("{incidenciaNumero}/adjuntos/{secuencia:int}")]
    public async Task<IActionResult> DescargarAdjunto(string incidenciaNumero, int secuencia, CancellationToken cancellationToken)
    {
        var identidad = ObtenerIdentidad();
        if (identidad is null) return Unauthorized();

        try
        {
            var archivo = await gestionTicketsTIBLL.ObtenerArchivoAsync(identidad.Value.Usuario, identidad.Value.Area, incidenciaNumero, secuencia, cancellationToken);
            return PhysicalFile(archivo.RutaArchivo, archivo.TipoMime, archivo.NombreOriginal, enableRangeProcessing: true);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { mensaje = ex.Message });
        }
    }

    private async Task<IActionResult> EjecutarAccion(string incidenciaNumero, Func<string, string, Task> accion)
    {
        var identidad = ObtenerIdentidad();
        if (identidad is null) return Unauthorized();

        try
        {
            await accion(identidad.Value.Usuario, identidad.Value.Area);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { mensaje = ex.Message });
        }
    }

    private (string Usuario, string Area)? ObtenerIdentidad()
    {
        var usuario = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var area = User.FindFirst("Area")?.Value;
        return string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(area) ? null : (usuario, area);
    }
}
