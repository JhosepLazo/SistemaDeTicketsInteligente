/*
 * Archivo: ConfiguracionTIController.cs
 * Objetivo: Exponer la administración funcional mínima requerida por el sistema.
 * Responsabilidad: Permitir a SUP/ADM mantener catálogos, matriz, SLA, usuarios corporativos, formatos y publicación de conocimiento sin acceder directamente a la base de datos.
 * Dependencias: ConfiguracionTIBLL, ConfiguracionTIDTO, autenticación por cookie y claims de ASP.NET Core.
 * Flujo: Frontend -> ConfiguracionTIController -> ConfiguracionTIBLL -> DAO -> SQL Server / Spring.
 * Consideraciones: El módulo está restringido a SUP/ADM; no implementa funciones de IA ni permite eliminar registros históricos.
 */

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaTicketsInteligente.Api.BLL;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.Controllers;

[ApiController]
[Authorize(Roles = "SUP,ADM")]
[Route("api/configuracion-ti")]
public sealed class ConfiguracionTIController : ControllerBase
{
    private readonly ConfiguracionTIBLL configuracionTIBLL;

    public ConfiguracionTIController(ConfiguracionTIBLL configuracionTIBLL)
    {
        this.configuracionTIBLL = configuracionTIBLL;
    }

    [HttpGet]
    public Task<IActionResult> Obtener(CancellationToken ct) => EjecutarConRespuesta(async usuario => Ok(await configuracionTIBLL.ObtenerAsync(usuario, ct)));

    [HttpPost("areas")]
    public Task<IActionResult> GuardarArea(GuardarAreaTISolicitud s, CancellationToken ct) => Ejecutar(usuario => configuracionTIBLL.GuardarAreaAsync(usuario, s, ct));

    [HttpPost("lineas")]
    public Task<IActionResult> GuardarLinea(GuardarLineaTISolicitud s, CancellationToken ct) => Ejecutar(usuario => configuracionTIBLL.GuardarLineaAsync(usuario, s, ct));

    [HttpPost("items")]
    public Task<IActionResult> GuardarItem(GuardarItemTISolicitud s, CancellationToken ct) => Ejecutar(usuario => configuracionTIBLL.GuardarItemAsync(usuario, s, ct));

    [HttpPost("tipos")]
    public Task<IActionResult> GuardarTipo(GuardarTipoTISolicitud s, CancellationToken ct) => Ejecutar(usuario => configuracionTIBLL.GuardarTipoAsync(usuario, s, ct));

    [HttpPost("categorias")]
    public Task<IActionResult> GuardarCategoria(GuardarCategoriaTISolicitud s, CancellationToken ct) => Ejecutar(usuario => configuracionTIBLL.GuardarCategoriaAsync(usuario, s, ct));

    [HttpPost("subtipos")]
    public Task<IActionResult> GuardarSubTipo(GuardarSubTipoTISolicitud s, CancellationToken ct) => Ejecutar(usuario => configuracionTIBLL.GuardarSubTipoAsync(usuario, s, ct));

    [HttpPost("matriz")]
    public Task<IActionResult> GuardarMatriz(GuardarMatrizTISolicitud s, CancellationToken ct) => Ejecutar(usuario => configuracionTIBLL.GuardarMatrizAsync(usuario, s, ct));

    [HttpPost("sla")]
    public Task<IActionResult> GuardarSla(GuardarSlaTISolicitud s, CancellationToken ct) => Ejecutar(usuario => configuracionTIBLL.GuardarSlaAsync(usuario, s, ct));

    [HttpPost("usuarios/sincronizar")]
    public Task<IActionResult> SincronizarUsuario(SincronizarUsuarioCorporativoSolicitud s, CancellationToken ct) => Ejecutar(usuario => configuracionTIBLL.SincronizarUsuarioCorporativoAsync(usuario, s, ct));

    [HttpPost("cargos/sincronizar")]
    public Task<IActionResult> SincronizarCargos(CancellationToken ct) => EjecutarConRespuesta(async usuario => Ok(new { procesados = await configuracionTIBLL.SincronizarCargosCorporativosAsync(usuario, ct) }));

    [HttpPost("formatos")]
    [RequestSizeLimit(16 * 1024 * 1024)]
    public Task<IActionResult> GuardarFormato([FromForm] GuardarFormatoSoporteSolicitud s, CancellationToken ct) => Ejecutar(usuario => configuracionTIBLL.GuardarFormatoAsync(usuario, s, ct));

    [HttpPost("conocimiento/{conocimientoCodigo}/visibilidad")]
    public Task<IActionResult> ActualizarVisibilidad(string conocimientoCodigo, VisibilidadConocimientoSolicitud s, CancellationToken ct) => Ejecutar(usuario => configuracionTIBLL.ActualizarVisibilidadConocimientoAsync(usuario, conocimientoCodigo, s.VisibleUsuario, ct));

    private async Task<IActionResult> Ejecutar(Func<string, Task> accion)
    {
        var usuario = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(usuario)) return Unauthorized();

        try
        {
            await accion(usuario);
            return NoContent();
        }
        catch (ArgumentException ex) { return BadRequest(new { mensaje = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { mensaje = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { mensaje = ex.Message }); }
    }

    private async Task<IActionResult> EjecutarConRespuesta(Func<string, Task<IActionResult>> accion)
    {
        var usuario = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(usuario)) return Unauthorized();

        try { return await accion(usuario); }
        catch (ArgumentException ex) { return BadRequest(new { mensaje = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { mensaje = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { mensaje = ex.Message }); }
    }
}
