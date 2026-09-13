/**
 * Archivo: MisTicketsUsuarioController.cs
 * Objetivo: Exponer los endpoints protegidos utilizados por el módulo Mis Tickets del usuario.
 * Responsabilidad: Obtener la identidad desde la sesión, consultar tickets/detalle y recibir las acciones que corresponden al usuario solicitante.
 * Dependencias: MisTicketsUsuarioBLL, MisTicketsUsuarioDTO, autenticación por cookie y claims de ASP.NET Core.
 * Flujo: Frontend -> MisTicketsUsuarioController -> MisTicketsUsuarioBLL -> MisTicketsUsuarioDAO -> SQL Server.
 * Consideraciones: Nunca recibe un usuario como parámetro confiable; todas las operaciones utilizan el NameIdentifier de la sesión y vuelven a validar propiedad en SQL Server.
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
public sealed class MisTicketsUsuarioController : ControllerBase
{
    private readonly MisTicketsUsuarioBLL misTicketsUsuarioBLL;

    public MisTicketsUsuarioController(MisTicketsUsuarioBLL misTicketsUsuarioBLL)
    {
        this.misTicketsUsuarioBLL = misTicketsUsuarioBLL;
    }

    [HttpGet]
    public async Task<ActionResult<MisTicketsUsuarioRespuesta>> Obtener(CancellationToken cancellationToken)
    {
        var usuario = ObtenerUsuario();
        if (usuario is null) return Unauthorized();
        return Ok(await misTicketsUsuarioBLL.ObtenerAsync(usuario, cancellationToken));
    }

    [HttpGet("{incidenciaNumero}")]
    public async Task<ActionResult<MisTicketsUsuarioDetalle>> ObtenerDetalle(string incidenciaNumero, CancellationToken cancellationToken)
    {
        var usuario = ObtenerUsuario();
        if (usuario is null) return Unauthorized();

        try
        {
            return Ok(await misTicketsUsuarioBLL.ObtenerDetalleAsync(usuario, incidenciaNumero, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }

    [HttpPost("{incidenciaNumero}/responder-observacion")]
    [RequestSizeLimit(55 * 1024 * 1024)]
    public async Task<IActionResult> ResponderObservacion(string incidenciaNumero, [FromForm] ResponderObservacionSolicitud solicitud, CancellationToken cancellationToken)
    {
        var usuario = ObtenerUsuario();
        if (usuario is null) return Unauthorized();

        try
        {
            await misTicketsUsuarioBLL.ResponderObservacionAsync(usuario, incidenciaNumero, solicitud, cancellationToken);
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

    [HttpPost("{incidenciaNumero}/validar-solucion")]
    public async Task<IActionResult> ValidarSolucion(string incidenciaNumero, ValidarSolucionSolicitud solicitud, CancellationToken cancellationToken)
    {
        var usuario = ObtenerUsuario();
        if (usuario is null) return Unauthorized();

        try
        {
            await misTicketsUsuarioBLL.ValidarSolucionAsync(usuario, incidenciaNumero, solicitud, cancellationToken);
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

    [HttpPost("{incidenciaNumero}/calificar")]
    public async Task<IActionResult> Calificar(string incidenciaNumero, CalificarTicketSolicitud solicitud, CancellationToken cancellationToken)
    {
        var usuario = ObtenerUsuario();
        if (usuario is null) return Unauthorized();

        try
        {
            await misTicketsUsuarioBLL.CalificarAsync(usuario, incidenciaNumero, solicitud, cancellationToken);
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

    [HttpGet("{incidenciaNumero}/adjuntos/{secuencia:int}")]
    public async Task<IActionResult> DescargarAdjunto(string incidenciaNumero, int secuencia, CancellationToken cancellationToken)
    {
        var usuario = ObtenerUsuario();
        if (usuario is null) return Unauthorized();

        try
        {
            var archivo = await misTicketsUsuarioBLL.ObtenerArchivoAsync(usuario, incidenciaNumero, secuencia, cancellationToken);
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
    }

    private string? ObtenerUsuario() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}
