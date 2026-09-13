/*
 * Archivo: RecursosSoporteController.cs
 * Objetivo: Exponer formatos frecuentes y artículos de ayuda estática para usuarios autenticados.
 * Responsabilidad: Entregar recursos publicados y descargar formatos por código sin exponer rutas físicas.
 * Dependencias: RecursosSoporteBLL y autenticación por cookie.
 * Flujo: Frontend -> RecursosSoporteController -> RecursosSoporteBLL -> RecursosSoporteDAO -> SQL Server.
 * Consideraciones: No implementa IA; reutiliza conocimiento validado como autoservicio previo al Asistente TI.
 */

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaTicketsInteligente.Api.BLL;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/recursos-soporte")]
public sealed class RecursosSoporteController : ControllerBase
{
    private readonly RecursosSoporteBLL recursosSoporteBLL;

    public RecursosSoporteController(RecursosSoporteBLL recursosSoporteBLL)
    {
        this.recursosSoporteBLL = recursosSoporteBLL;
    }

    [HttpGet]
    public async Task<ActionResult<RecursosSoporteRespuesta>> Obtener(CancellationToken ct) => Ok(await recursosSoporteBLL.ObtenerAsync(ct));

    [HttpGet("formatos/{formatoCodigo}")]
    public async Task<IActionResult> DescargarFormato(string formatoCodigo, CancellationToken ct)
    {
        try
        {
            var archivo = await recursosSoporteBLL.ObtenerArchivoAsync(formatoCodigo, ct);
            return PhysicalFile(archivo.RutaArchivo, archivo.TipoMime, archivo.NombreOriginal, enableRangeProcessing: true);
        }
        catch (ArgumentException ex) { return BadRequest(new { mensaje = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { mensaje = ex.Message }); }
        catch (FileNotFoundException ex) { return NotFound(new { mensaje = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { mensaje = ex.Message }); }
    }
}
