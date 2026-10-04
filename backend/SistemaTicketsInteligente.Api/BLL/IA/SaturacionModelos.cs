/**
 * Archivo: SaturacionModelos.cs
 * Objetivo: Elegir el siguiente modelo cuando el proveedor de IA está saturado, sin esperar en cada llamada a que el saturado falle.
 * Responsabilidad: Recordar por unos minutos qué modelos respondieron 503 (alta demanda) o 429 (cuota) y ordenarlos al final de la cadena.
 * Dependencias: Ninguna (estado en memoria del proceso).
 * Flujo: OpenAIAsistenteClient / AnalizadorGrabacionClient -> Ordenar -> intento -> Marcar o Liberar.
 * Consideraciones: En el nivel gratuito de Gemini cada modelo tiene su propia capacidad y cuota: si uno está saturado,
 *   otro suele responder. Un 503 puede tardar 30-70 s en llegar, por eso conviene no repetirlo durante unos minutos.
 */

using System.Collections.Concurrent;

namespace SistemaTicketsInteligente.Api.BLL.IA;

public static class SaturacionModelos
{
    private static readonly ConcurrentDictionary<string, DateTimeOffset> Saturados = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Modelos sin repetir, con los saturados recientemente al final (se usan solo si todos los demás fallan).</summary>
    public static IReadOnlyList<string> Ordenar(IEnumerable<string?> modelos)
    {
        var lista = modelos.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var ahora = DateTimeOffset.UtcNow;
        bool Saturado(string modelo) => Saturados.TryGetValue(modelo, out var hasta) && hasta > ahora;
        return lista.Where(x => !Saturado(x)).Concat(lista.Where(Saturado)).ToList();
    }

    public static void Marcar(string modelo, TimeSpan duracion) => Saturados[modelo] = DateTimeOffset.UtcNow + duracion;

    public static void Liberar(string modelo) => Saturados.TryRemove(modelo, out _);
}
