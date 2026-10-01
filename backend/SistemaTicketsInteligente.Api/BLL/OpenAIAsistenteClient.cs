/**
 * Archivo: OpenAIAsistenteClient.cs
 * Objetivo: Consumir la Responses API de OpenAI desde el servidor.
 * Responsabilidad: Enviar únicamente el prompt y contexto previamente autorizados, y devolver texto sin exponer credenciales al navegador.
 * Dependencias: HttpClient, IConfiguration y System.Text.Json.
 * Flujo: AsistenteUsuarioBLL -> OpenAI Responses API -> texto de respuesta.
 * Consideraciones: La clave se lee desde OPENAI_API_KEY o AsistenteIA:ApiKey; nunca se registra el contenido enviado.
 */

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SistemaTicketsInteligente.Api.BLL;

public sealed class OpenAIAsistenteClient
{
    private readonly HttpClient httpClient;
    private readonly IConfiguration configuration;
    private readonly ILogger<OpenAIAsistenteClient> logger;

    public OpenAIAsistenteClient(HttpClient httpClient, IConfiguration configuration, ILogger<OpenAIAsistenteClient> logger)
    {
        this.httpClient = httpClient;
        this.configuration = configuration;
        this.logger = logger;
    }

    public bool EstaDisponible => !string.IsNullOrWhiteSpace(ObtenerApiKey());

    public async Task<string?> GenerarAsync(string instrucciones, string entrada, CancellationToken cancellationToken)
    {
        var apiKey = ObtenerApiKey();
        if (string.IsNullOrWhiteSpace(apiKey)) return null;

        var modelo = configuration["AsistenteIA:Modelo"]?.Trim();
        if (string.IsNullOrWhiteSpace(modelo)) modelo = "gpt-5-mini";
        var maxTokens = configuration.GetValue<int?>("AsistenteIA:MaxTokens") ?? 700;
        maxTokens = Math.Clamp(maxTokens, 200, 1200);

        using var solicitud = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        solicitud.Content = new StringContent(JsonSerializer.Serialize(new
        {
            model = modelo,
            instructions = instrucciones,
            input = entrada,
            max_output_tokens = maxTokens,
            store = false
        }), Encoding.UTF8, "application/json");

        try
        {
            using var respuesta = await httpClient.SendAsync(solicitud, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!respuesta.IsSuccessStatusCode)
            {
                logger.LogWarning("El proveedor de IA respondió con estado {Estado}.", (int)respuesta.StatusCode);
                return null;
            }

            await using var contenido = await respuesta.Content.ReadAsStreamAsync(cancellationToken);
            using var documento = await JsonDocument.ParseAsync(contenido, cancellationToken: cancellationToken);
            if (!documento.RootElement.TryGetProperty("output", out var salida)) return null;

            var partes = new List<string>();
            foreach (var elemento in salida.EnumerateArray())
            {
                if (!elemento.TryGetProperty("content", out var bloques)) continue;
                foreach (var bloque in bloques.EnumerateArray())
                {
                    if (bloque.TryGetProperty("type", out var tipo) && tipo.GetString() == "output_text" &&
                        bloque.TryGetProperty("text", out var texto) && !string.IsNullOrWhiteSpace(texto.GetString()))
                        partes.Add(texto.GetString()!.Trim());
                }
            }

            return partes.Count == 0 ? null : string.Join("\n", partes);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("El proveedor de IA superó el tiempo de espera configurado.");
            return null;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "No fue posible comunicarse con el proveedor de IA.");
            return null;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "El proveedor de IA devolvió una respuesta no reconocida.");
            return null;
        }
    }

    private string? ObtenerApiKey() =>
        Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? configuration["AsistenteIA:ApiKey"];
}
