/**
 * Archivo: ConfiguracionTIController.cs
 * Objetivo: Exponer la administración de los maestros de TI.
 * Responsabilidad: Delegar la consulta y el guardado de cada maestro, y la sincronización con Spring, en ConfiguracionTIBLL.
 * Dependencias: ConfiguracionTIBLL y la autenticación por cookie.
 * Flujo: ConfiguracionTIPage -> ConfiguracionTIController -> ConfiguracionTIBLL.
 * Consideraciones: El frontend solo muestra este módulo a SUP y ADM; los procedimientos aplican la autorización definitiva.
 */

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SistemaTicketsInteligente.Api.Controllers;

[ApiController]
[Authorize(Roles = "TEC,SUP,ADM")]
[Route("api/configuracion-ti")]
public sealed class ConfiguracionTIController(ConfiguracionTIBLL configuracion) : ControladorBase
{
    [HttpGet]
    public Task<IActionResult> Obtener(CancellationToken ct) => Responder(async () => Ok(await configuracion.ObtenerAsync(Usuario, ct)));

    [HttpPost("areas")]
    public Task<IActionResult> GuardarArea(GuardarAreaTISolicitud s, CancellationToken ct) => Ejecutar(() => configuracion.GuardarAreaAsync(Usuario, s, ct));

    [HttpPost("lineas")]
    public Task<IActionResult> GuardarLinea(GuardarLineaTISolicitud s, CancellationToken ct) => Ejecutar(() => configuracion.GuardarLineaAsync(Usuario, s, ct));

    [HttpPost("items")]
    public Task<IActionResult> GuardarItem(GuardarItemTISolicitud s, CancellationToken ct) => Ejecutar(() => configuracion.GuardarItemAsync(Usuario, s, ct));

    [HttpPost("tipos")]
    public Task<IActionResult> GuardarTipo(GuardarTipoTISolicitud s, CancellationToken ct) => Ejecutar(() => configuracion.GuardarTipoAsync(Usuario, s, ct));

    [HttpPost("categorias")]
    public Task<IActionResult> GuardarCategoria(GuardarCategoriaTISolicitud s, CancellationToken ct) => Ejecutar(() => configuracion.GuardarCategoriaAsync(Usuario, s, ct));

    [HttpPost("subtipos")]
    public Task<IActionResult> GuardarSubTipo(GuardarSubTipoTISolicitud s, CancellationToken ct) => Ejecutar(() => configuracion.GuardarSubTipoAsync(Usuario, s, ct));

    [HttpPost("matriz")]
    public Task<IActionResult> GuardarMatriz(GuardarMatrizTISolicitud s, CancellationToken ct) => Ejecutar(() => configuracion.GuardarMatrizAsync(Usuario, s, ct));

    [HttpPost("sla")]
    public Task<IActionResult> GuardarSla(GuardarSlaTISolicitud s, CancellationToken ct) => Ejecutar(() => configuracion.GuardarSlaAsync(Usuario, s, ct));

    [HttpPost("usuarios/sincronizar")]
    public Task<IActionResult> SincronizarUsuario(SincronizarUsuarioCorporativoSolicitud s, CancellationToken ct) =>
        Ejecutar(() => configuracion.SincronizarUsuarioCorporativoAsync(Usuario, s, ct));

    [HttpPost("cargos/sincronizar")]
    public Task<IActionResult> SincronizarCargos(CancellationToken ct) =>
        Responder(async () => Ok(new { procesados = await configuracion.SincronizarCargosCorporativosAsync(Usuario, ct) }));

    [HttpPost("formatos")]
    [RequestSizeLimit(16 * 1024 * 1024)]
    public Task<IActionResult> GuardarFormato([FromForm] GuardarFormatoSoporteSolicitud s, CancellationToken ct) =>
        Ejecutar(() => configuracion.GuardarFormatoAsync(Usuario, s, ct));

    [HttpPost("conocimiento/{conocimientoCodigo}/visibilidad")]
    public Task<IActionResult> ActualizarVisibilidad(string conocimientoCodigo, VisibilidadConocimientoSolicitud s, CancellationToken ct) =>
        Ejecutar(() => configuracion.ActualizarVisibilidadConocimientoAsync(Usuario, conocimientoCodigo, s.VisibleUsuario, ct));
}
