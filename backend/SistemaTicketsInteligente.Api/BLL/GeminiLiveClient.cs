/**
 * Archivo: GeminiLiveClient.cs
 * Objetivo: Emitir tokens efímeros para sesiones multimodales del Agente de Ingeniería.
 * Responsabilidad: Mantener la clave de Gemini exclusivamente en backend y entregar al navegador un token de un solo uso y corta duración.
 * Dependencias: HttpClient, IConfiguration y Gemini AuthTokenService.
 * Flujo: AsistenteTIBLL -> GeminiLiveClient -> Gemini API -> token efímero -> navegador -> Live API.
 * Consideraciones: El token Live no concede permisos de negocio; toda acción correctiva sigue validándose exclusivamente en el backend local.
 */

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.BLL;

public sealed class GeminiLiveClient
{
    private const string UrlTokens = "https://generativelanguage.googleapis.com/v1beta/auth_tokens";
    private const string UrlWebSocket = "wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContentConstrained";
    private readonly HttpClient httpClient;
    private readonly IConfiguration configuration;
    private readonly ILogger<GeminiLiveClient> logger;

    public GeminiLiveClient(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiLiveClient> logger)
    {
        this.httpClient = httpClient;
        this.configuration = configuration;
        this.logger = logger;
    }

    public bool EstaDisponible => !string.IsNullOrWhiteSpace(ObtenerApiKey());

    public async Task<AgenteTILiveTokenRespuesta> CrearTokenAsync(CancellationToken ct)
    {
        var apiKey = ObtenerApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
            return new AgenteTILiveTokenRespuesta
            {
                Disponible = false,
                Mensaje = "La asistencia Live no está configurada. Define GEMINI_API_KEY en el servidor; la investigación por texto continúa disponible."
            };

        var ahora = DateTimeOffset.UtcNow;
        var expira = ahora.AddMinutes(30);
        var nuevaSesionExpira = ahora.AddMinutes(1);
        var modelo = configuration["AsistenteLive:Modelo"]?.Trim();
        if (string.IsNullOrWhiteSpace(modelo)) modelo = "gemini-3.8-live";

        using var solicitud = new HttpRequestMessage(HttpMethod.Post, UrlTokens);
        solicitud.Headers.Add("x-goog-api-key", apiKey);
        solicitud.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        solicitud.Content = new StringContent(JsonSerializer.Serialize(new
        {
            uses = 1,
            expireTime = expira.ToString("O"),
            newSessionExpireTime = nuevaSesionExpira.ToString("O")
        }), Encoding.UTF8, "application/json");

        try
        {
            using var respuesta = await httpClient.SendAsync(solicitud, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!respuesta.IsSuccessStatusCode)
            {
                logger.LogWarning("Gemini no pudo emitir el token Live. Estado {Estado}.", (int)respuesta.StatusCode);
                return new AgenteTILiveTokenRespuesta { Disponible = false, Mensaje = "Gemini Live no pudo iniciar una sesión segura en este momento." };
            }

            await using var contenido = await respuesta.Content.ReadAsStreamAsync(ct);
            using var documento = await JsonDocument.ParseAsync(contenido, cancellationToken: ct);
            if (!documento.RootElement.TryGetProperty("name", out var nombre) || string.IsNullOrWhiteSpace(nombre.GetString()))
                return new AgenteTILiveTokenRespuesta { Disponible = false, Mensaje = "Gemini Live devolvió un token no reconocido." };

            return new AgenteTILiveTokenRespuesta
            {
                Disponible = true,
                Token = nombre.GetString()!,
                Modelo = modelo,
                WebSocketUrl = UrlWebSocket,
                ExpiraEn = expira,
                InstruccionSistema = """
                    Eres el observador Live del Agente de Ingeniería de Incidencias TI. Conversa en español profesional y breve.
                    Tu objetivo durante esta etapa es observar la pantalla compartida y ayudar al usuario a reproducir exactamente el proceso hasta que aparezca el error.
                    Identifica únicamente lo que realmente puedas observar o lo que el usuario confirme: sistema, módulo, secuencia de pasos, documento, botón o acción ejecutada, mensaje de error y resultado visible.
                    No inventes datos internos, tablas, métodos, endpoints, Stored Procedures ni causas raíz a partir de la pantalla.
                    No solicites contraseñas, secretos ni datos personales innecesarios. Si aparecen, pide al usuario ocultarlos antes de continuar.
                    No propongas ni ejecutes cambios productivos durante Live. Cuando el error sea visible, indica claramente que el error fue observado y que la investigación técnica continuará con evidencia y fuentes autorizadas.
                    """,
                Mensaje = "Sesión Live autorizada mediante token efímero de un solo uso."
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Gemini Live superó el tiempo de espera al emitir el token.");
            return new AgenteTILiveTokenRespuesta { Disponible = false, Mensaje = "Gemini Live no respondió dentro del tiempo esperado." };
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "No fue posible solicitar el token efímero de Gemini Live.");
            return new AgenteTILiveTokenRespuesta { Disponible = false, Mensaje = "No fue posible establecer la sesión Live con el proveedor multimodal." };
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Gemini Live devolvió una respuesta no reconocida al emitir el token.");
            return new AgenteTILiveTokenRespuesta { Disponible = false, Mensaje = "Gemini Live devolvió una respuesta no reconocida." };
        }
    }

    private string? ObtenerApiKey() =>
        Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? configuration["AsistenteLive:ApiKey"];
}
