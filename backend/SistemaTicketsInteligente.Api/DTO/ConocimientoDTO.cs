/**
 * Archivo: ConocimientoDTO.cs
 * Objetivo: Definir los contratos de la búsqueda semántica de conocimiento.
 * Responsabilidad: Transportar el corpus autorizado con su vector y los resultados ordenados por similitud.
 * Dependencias: Ninguna.
 * Flujo: ConocimientoDAO -> ConocimientoSemanticoBLL -> asistentes (TI y colaborador).
 * Consideraciones: El texto del corpus nunca se devuelve completo al navegador; solo títulos, códigos y extractos.
 */

namespace SistemaTicketsInteligente.Api.DTO;

public sealed class ConocimientoItem
{
    /// <summary>K = artículo de la base de conocimiento, T = ticket resuelto.</summary>
    public string Origen { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Texto { get; set; } = string.Empty;
    public string Linea { get; set; } = string.Empty;
    public string Item { get; set; } = string.Empty;
    public string Huella { get; set; } = string.Empty;
    public float[]? Vector { get; set; }
}

public sealed class ConocimientoSimilar
{
    public string Origen { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    /// <summary>Similitud coseno expresada de 0 a 100.</summary>
    public decimal Similitud { get; set; }
    public string Extracto { get; set; } = string.Empty;
}
