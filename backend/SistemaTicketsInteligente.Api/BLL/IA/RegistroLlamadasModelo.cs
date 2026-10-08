/**
 * Archivo: RegistroLlamadasModelo.cs
 * Objetivo: Medir cada llamada al proveedor de IA (modelo, tokens, duración y resultado) durante una investigación del agente.
 * Responsabilidad: Abrir un registro por flujo asíncrono y acumular las llamadas que hace OpenAIAsistenteClient mientras esté abierto.
 * Dependencias: AsyncLocal (igual que TrazaAgente).
 * Flujo: AsistenteTIBLL.InvestigarAsync -> Iniciar() -> OpenAIAsistenteClient anota cada envío -> el BLL guarda LLAMADA_MODELO.
 * Consideraciones: Nunca guarda el contenido enviado ni la respuesta: solo metadatos para medir costo y latencia (Anexo B del plan).
 */

using System.Collections.Concurrent;

namespace SistemaTicketsInteligente.Api.BLL.IA;

public sealed class RegistroLlamadasModelo : IDisposable
{
    private static readonly AsyncLocal<RegistroLlamadasModelo?> Actual = new();
    private readonly RegistroLlamadasModelo? anterior;

    private RegistroLlamadasModelo()
    {
        anterior = Actual.Value;
        Actual.Value = this;
    }

    public ConcurrentQueue<LlamadaModelo> Llamadas { get; } = new();

    /// <summary>Abre un registro; las llamadas que se hagan en este flujo asíncrono quedan en él hasta Dispose.</summary>
    public static RegistroLlamadasModelo Iniciar() => new();

    /// <summary>Anota una llamada si hay un registro abierto; si no, no hace nada.</summary>
    public static void Anotar(LlamadaModelo llamada) => Actual.Value?.Llamadas.Enqueue(llamada);

    public void Dispose() => Actual.Value = anterior;
}

public sealed record LlamadaModelo(string Proveedor, string Modelo, int TokensEntrada, int TokensSalida, long DuracionMs, bool Exito, int EstadoHttp);
