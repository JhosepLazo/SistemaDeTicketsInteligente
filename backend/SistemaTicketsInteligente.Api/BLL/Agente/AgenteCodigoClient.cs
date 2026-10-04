/**
 * Archivo: AgenteCodigoClient.cs
 * Objetivo: Señalar en qué partes del código de este sistema aparecen las palabras de la incidencia (búsqueda estática).
 * Responsabilidad: Buscar los términos del problema y los procedimientos registrados por la telemetría en el backend, los scripts SQL
 *   y las pantallas, y devolver como máximo 20 referencias archivo:línea.
 * Dependencias: IWebHostEnvironment, IConfiguration (AgenteTI:RepositorioLectura) y ReplicaTecnicaBLL.RaizRepositorio.
 * Flujo: AsistenteTIBLL (investigación o botón Código) -> AgenteCodigoClient -> referencias CODIGO_ESTATICO del expediente.
 * Consideraciones: Solo lee archivos .cs, .sql y .tsx; no carga configuraciones, omite líneas con credenciales y nunca ejecuta código.
 *   Una referencia indica dónde aparece un texto, no que esa ruta se haya ejecutado.
 */

using System.Text.RegularExpressions;

namespace SistemaTicketsInteligente.Api.BLL.Agente;

public sealed class AgenteCodigoClient(IWebHostEnvironment environment, IConfiguration configuration)
{
    public List<AgenteTICodigoReferencia> Buscar(AgenteTIContextoInvestigacion contexto)
    {
        var raiz = configuration["AgenteTI:RepositorioLectura"];
        if (string.IsNullOrWhiteSpace(raiz)) raiz = ReplicaTecnicaBLL.RaizRepositorio(environment.ContentRootPath);
        if (string.IsNullOrWhiteSpace(raiz) || !Directory.Exists(raiz)) return [];
        var texto = contexto.Sesion.DescripcionInicial + " " + contexto.Sesion.ErrorObservado + " " + contexto.Ticket.Titulo;
        var tokens = Regex.Matches(texto, @"[A-Za-z_][A-Za-z0-9_]{4,}").Select(x => x.Value)
            .Where(x => !new[] { "usuario", "sistema", "error", "cuando", "tiene", "puede", "desde", "ticket", "problema" }.Contains(x.ToLowerInvariant()))
            .Concat(contexto.Eventos.Where(x => x.OrigenServidor).SelectMany(x => Regex.Matches(x.Contenido, @"Usp_[A-Za-z0-9_]+").Select(m => m.Value)))
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToArray();
        if (tokens.Length == 0) return [];
        var resultados = new List<AgenteTICodigoReferencia>();
        var opciones = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
        foreach (var carpeta in new[] { "backend/SistemaTicketsInteligente.Api/BLL", "backend/SistemaTicketsInteligente.Api/Controllers", "database/sistema-inteligente", "frontend/src" })
        {
            var ruta = Path.Combine(raiz, carpeta);
            if (!Directory.Exists(ruta)) continue;
            foreach (var archivo in Directory.EnumerateFiles(ruta, "*", opciones).Order(StringComparer.Ordinal).Take(300))
            {
                if (!new[] { ".cs", ".sql", ".tsx" }.Contains(Path.GetExtension(archivo)) || new FileInfo(archivo).Length > 256_000) continue;
                try
                {
                    var numero = 0;
                    foreach (var linea in File.ReadLines(archivo))
                    {
                        numero++;
                        if (!tokens.Any(t => linea.Contains(t, StringComparison.OrdinalIgnoreCase))) continue;
                        if (Regex.IsMatch(linea, "ApiKey|password|contrase|connectionstring|clave|secret|Bearer", RegexOptions.IgnoreCase)) continue;
                        resultados.Add(new(Path.GetRelativePath(raiz, archivo).Replace('\\', '/'), numero, linea.Trim()[..Math.Min(linea.Trim().Length, 300)]));
                        if (resultados.Count >= 20) return resultados;
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        return resultados;
    }
}
