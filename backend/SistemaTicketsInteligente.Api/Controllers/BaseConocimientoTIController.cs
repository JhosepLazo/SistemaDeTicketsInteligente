/**
 * Archivo: BaseConocimientoTIController.cs
 * Objetivo: Exponer los endpoints protegidos del módulo Base de Conocimiento para operadores TI autenticados.
 * Responsabilidad: Obtener la identidad desde la sesión, delegar consultas y mantenimiento a la BLL y traducir errores funcionales a respuestas HTTP claras.
 * Dependencias: BaseConocimientoTIBLL, BaseConocimientoTIDTO, autenticación por cookie y claims de ASP.NET Core.
 * Flujo: Frontend -> BaseConocimientoTIController -> BaseConocimientoTIBLL -> BaseConocimientoTIDAO -> SQL Server.
 * Consideraciones: Nunca recibe un usuario como parámetro confiable; todas las escrituras usan el NameIdentifier de la sesión autenticada.
 */

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaTicketsInteligente.Api.BLL;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.Controllers;

[ApiController]
[Authorize(Roles = "TEC,SUP,ADM")]
[Route("api/base-conocimiento")]
public sealed class BaseConocimientoTIController : ControllerBase
{
    private readonly BaseConocimientoTIBLL baseConocimientoTIBLL;

    public BaseConocimientoTIController(BaseConocimientoTIBLL baseConocimientoTIBLL)
    {
        this.baseConocimientoTIBLL = baseConocimientoTIBLL;
    }

    [HttpGet]
    public async Task<ActionResult<BaseConocimientoTIRespuesta>> Obtener(CancellationToken cancellationToken) =>
        Ok(await baseConocimientoTIBLL.ObtenerAsync(cancellationToken));

    [HttpGet("{codigo}")]
    public async Task<ActionResult<BaseConocimientoTIDetalle>> ObtenerDetalle(string codigo, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await baseConocimientoTIBLL.ObtenerDetalleAsync(codigo, cancellationToken));
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

    [HttpPost]
    public async Task<ActionResult<BaseConocimientoTICreadoRespuesta>> Crear(GuardarBaseConocimientoTISolicitud solicitud, CancellationToken cancellationToken)
    {
        var usuario = ObtenerUsuario();
        if (usuario is null) return Unauthorized();

        try
        {
            var creado = await baseConocimientoTIBLL.CrearAsync(usuario, solicitud, cancellationToken);
            return CreatedAtAction(nameof(ObtenerDetalle), new { codigo = creado.ConocimientoCodigo }, creado);
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

    [HttpPut("{codigo}")]
    public async Task<IActionResult> Actualizar(string codigo, GuardarBaseConocimientoTISolicitud solicitud, CancellationToken cancellationToken)
    {
        var usuario = ObtenerUsuario();
        if (usuario is null) return Unauthorized();

        try
        {
            await baseConocimientoTIBLL.ActualizarAsync(usuario, codigo, solicitud, cancellationToken);
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

    [HttpPost("{codigo}/enviar-validacion")]
    public Task<IActionResult> EnviarValidacion(string codigo, CancellationToken cancellationToken) =>
        EjecutarEstado(codigo, baseConocimientoTIBLL.EnviarValidacionAsync, cancellationToken);

    [HttpPost("{codigo}/validar")]
    public Task<IActionResult> Validar(string codigo, CancellationToken cancellationToken) =>
        EjecutarEstado(codigo, baseConocimientoTIBLL.ValidarAsync, cancellationToken);

    [HttpPost("{codigo}/inactivar")]
    public Task<IActionResult> Inactivar(string codigo, CancellationToken cancellationToken) =>
        EjecutarEstado(codigo, baseConocimientoTIBLL.InactivarAsync, cancellationToken);

    private async Task<IActionResult> EjecutarEstado(
        string codigo,
        Func<string, string, CancellationToken, Task> accion,
        CancellationToken cancellationToken)
    {
        var usuario = ObtenerUsuario();
        if (usuario is null) return Unauthorized();

        try
        {
            await accion(usuario, codigo, cancellationToken);
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

    private string? ObtenerUsuario() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}
