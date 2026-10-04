/**
 * Archivo: BaseConocimientoTIController.cs
 * Objetivo: Exponer al operador TI la Base de Conocimiento y el ciclo de vida de sus artículos.
 * Responsabilidad: Delegar consulta, creación, edición, envío a validación, validación e inactivación en BaseConocimientoTIBLL.
 * Dependencias: BaseConocimientoTIBLL y la autenticación por cookie.
 * Flujo: BaseConocimientoTIPage -> BaseConocimientoTIController -> BaseConocimientoTIBLL.
 * Consideraciones: Solo perfiles TEC, SUP y ADM.
 */

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SistemaTicketsInteligente.Api.Controllers;

[ApiController]
[Authorize(Roles = "TEC,SUP,ADM")]
[Route("api/base-conocimiento")]
public sealed class BaseConocimientoTIController(BaseConocimientoTIBLL conocimiento) : ControladorBase
{
    [HttpGet]
    public Task<IActionResult> Obtener(CancellationToken ct) => Responder(async () => Ok(await conocimiento.ObtenerAsync(ct)));

    [HttpGet("{codigo}")]
    public Task<IActionResult> ObtenerDetalle(string codigo, CancellationToken ct) => Responder(async () => Ok(await conocimiento.ObtenerDetalleAsync(codigo, ct)));

    [HttpPost]
    public Task<IActionResult> Crear(GuardarBaseConocimientoTISolicitud solicitud, CancellationToken ct) => Responder(async () =>
    {
        var creado = await conocimiento.CrearAsync(Usuario, solicitud, ct);
        return CreatedAtAction(nameof(ObtenerDetalle), new { codigo = creado.ConocimientoCodigo }, creado);
    });

    [HttpPut("{codigo}")]
    public Task<IActionResult> Actualizar(string codigo, GuardarBaseConocimientoTISolicitud solicitud, CancellationToken ct) =>
        Ejecutar(() => conocimiento.ActualizarAsync(Usuario, codigo, solicitud, ct));

    [HttpPost("{codigo}/enviar-validacion")]
    public Task<IActionResult> EnviarValidacion(string codigo, CancellationToken ct) => Ejecutar(() => conocimiento.EnviarValidacionAsync(Usuario, codigo, ct));

    [HttpPost("{codigo}/validar")]
    public Task<IActionResult> Validar(string codigo, CancellationToken ct) => Ejecutar(() => conocimiento.ValidarAsync(Usuario, codigo, ct));

    [HttpPost("{codigo}/inactivar")]
    public Task<IActionResult> Inactivar(string codigo, CancellationToken ct) => Ejecutar(() => conocimiento.InactivarAsync(Usuario, codigo, ct));
}
