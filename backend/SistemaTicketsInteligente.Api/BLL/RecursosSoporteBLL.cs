/*
 * Archivo: RecursosSoporteBLL.cs
 * Objetivo: Aplicar validaciones mínimas al acceso de formatos y artículos publicados.
 * Responsabilidad: Consultar recursos y resolver descargas únicamente dentro de la carpeta controlada uploads/formatos.
 * Dependencias: RecursosSoporteDAO y RecursosSoporteDTO.
 * Flujo: RecursosSoporteController -> RecursosSoporteBLL -> RecursosSoporteDAO -> SQL Server.
 * Consideraciones: El cliente nunca recibe rutas físicas; las descargas se resuelven por código autorizado.
 */

using SistemaTicketsInteligente.Api.DAO;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.BLL;

public sealed class RecursosSoporteBLL
{
    private readonly RecursosSoporteDAO recursosSoporteDAO;

    public RecursosSoporteBLL(RecursosSoporteDAO recursosSoporteDAO)
    {
        this.recursosSoporteDAO = recursosSoporteDAO;
    }

    public Task<RecursosSoporteRespuesta> ObtenerAsync(CancellationToken ct = default) => recursosSoporteDAO.ObtenerAsync(ct);

    public async Task<RecursoArchivo> ObtenerArchivoAsync(string formatoCodigo, CancellationToken ct = default)
    {
        var codigo = formatoCodigo.Trim().ToUpperInvariant();
        if (codigo.Length == 0 || codigo.Length > 20) throw new ArgumentException("El formato indicado no es válido.");

        var archivo = await recursosSoporteDAO.ObtenerArchivoAsync(codigo, ct) ?? throw new KeyNotFoundException("El formato ya no se encuentra disponible.");
        var raiz = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "uploads", "formatos"));
        var ruta = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), archivo.RutaArchivo.Replace('/', Path.DirectorySeparatorChar)));

        if (!ruta.StartsWith(raiz + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("La ruta del formato no es válida.");
        if (!File.Exists(ruta)) throw new FileNotFoundException("El archivo del formato no se encuentra disponible.");

        archivo.RutaArchivo = ruta;
        return archivo;
    }
}
