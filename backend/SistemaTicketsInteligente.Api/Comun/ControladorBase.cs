/**
 * Archivo: ControladorBase.cs
 * Objetivo: Concentrar lo que todos los controladores necesitan: la identidad de la cookie y la traducción de errores a HTTP.
 * Responsabilidad: Exponer el usuario y el área autenticados y convertir los errores de validación y de negocio en 400, 404 o 409
 *   con un mensaje para la pantalla.
 * Dependencias: ASP.NET Core MVC y los claims creados por AutenticacionController.
 * Flujo: Frontend -> Controller (hereda de ControladorBase) -> BLL -> respuesta o error con { mensaje }.
 * Consideraciones: Los errores de SQL que no son de negocio siguen al manejador global de Program.cs (503 o 500), sin detalles internos.
 */

using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace SistemaTicketsInteligente.Api.Comun;

public abstract class ControladorBase : ControllerBase
{
    /// <summary>Usuario autenticado (claim NameIdentifier de la cookie).</summary>
    protected string Usuario => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

    /// <summary>Área del usuario autenticado.</summary>
    protected string Area => User.FindFirst("Area")?.Value ?? string.Empty;

    /// <summary>Ejecuta la acción y devuelve su respuesta; los errores de validación y de negocio vuelven como 400, 404 o 409.</summary>
    protected async Task<IActionResult> Responder(Func<Task<IActionResult>> accion)
    {
        if (string.IsNullOrWhiteSpace(Usuario) || string.IsNullOrWhiteSpace(Area)) return Unauthorized();
        try { return await accion(); }
        catch (ArgumentException ex) { return BadRequest(new { mensaje = ex.Message }); }
        catch (Exception ex) when (ex is KeyNotFoundException or FileNotFoundException) { return NotFound(new { mensaje = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { mensaje = ex.Message }); }
    }

    /// <summary>Igual que <see cref="Responder"/>, para acciones que no devuelven contenido (204).</summary>
    protected Task<IActionResult> Ejecutar(Func<Task> accion) => Responder(async () =>
    {
        await accion();
        return NoContent();
    });

    /// <summary>Entrega un archivo guardado; el navegador puede reproducir videos y adelantar (rangos habilitados).</summary>
    protected IActionResult Archivo(string rutaFisica, string tipoMime, string? nombreDescarga = null) =>
        PhysicalFile(rutaFisica, tipoMime, nombreDescarga, enableRangeProcessing: true);
}
