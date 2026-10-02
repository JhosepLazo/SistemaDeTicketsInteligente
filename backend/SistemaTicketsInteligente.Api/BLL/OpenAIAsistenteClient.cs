/**
 * Archivo: OpenAIAsistenteClient.cs
 * Objetivo: Consumir un proveedor de IA compatible con OpenAI desde el servidor.
 * Responsabilidad: Enviar únicamente el prompt y contexto previamente autorizados, y devolver texto sin exponer credenciales al navegador.
 * Dependencias: HttpClient, IConfiguration y System.Text.Json.
 * Flujo: Asistente BLL -> proveedor (OpenAI Responses, Groq Responses o Gemini Chat Completions) -> texto de respuesta.
 * Consideraciones: El proveedor sale de AsistenteIA:Proveedor (OpenAI, Gemini o Groq); si no se indica, se usa el primero que tenga clave:
 *   OPENAI_API_KEY, GEMINI_API_KEY o GROQ_API_KEY. Gemini y Groq tienen nivel gratuito para pruebas. Nunca se registra el contenido enviado.
 */

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

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

    public bool EstaDisponible => ResolverProveedor() is not null;

    /// <summary>Nombre del proveedor activo (para registros y el expediente), o vacío si no hay ninguno configurado.</summary>
    public string ProveedorActivo => ResolverProveedor()?.Nombre ?? string.Empty;

    public async Task<string?> GenerarAsync(string instrucciones, string entrada, CancellationToken cancellationToken)
    {
        var proveedor = ResolverProveedor();
        if (proveedor is null) return null;
        var maxTokens = Math.Clamp(configuration.GetValue<int?>("AsistenteIA:MaxTokens") ?? 1800, 300, 3000);

        JsonObject cuerpo;
        if (proveedor.UsaResponses)
        {
            cuerpo = new JsonObject { ["model"] = proveedor.Modelo, ["instructions"] = instrucciones, ["input"] = entrada, ["max_output_tokens"] = maxTokens };
            if (proveedor.SoportaEstado) cuerpo["store"] = false;
        }
        else
        {
            cuerpo = CuerpoChat(proveedor, new JsonArray(Mensaje("system", instrucciones), Mensaje("user", entrada)), maxTokens);
        }

        try
        {
            var documento = await EnviarAsync(proveedor, cuerpo, cancellationToken);
            if (documento is null) return null;
            var texto = proveedor.UsaResponses ? TextoResponses(documento) : TextoChat(documento);
            return string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("El proveedor de IA {Proveedor} superó el tiempo de espera configurado.", proveedor.Nombre);
            return null;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "No fue posible comunicarse con el proveedor de IA {Proveedor}.", proveedor.Nombre);
            return null;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            logger.LogWarning(ex, "El proveedor de IA {Proveedor} devolvió una respuesta no reconocida.", proveedor.Nombre);
            return null;
        }
    }

    /// <summary>
    /// Bucle agéntico: el modelo puede llamar herramientas (function calling) hasta <paramref name="maximoLlamadas"/> veces
    /// y termina con un JSON que cumple <paramref name="esquemaSalida"/>.
    /// Devuelve null si el proveedor falla, para que el llamador use su ruta sin herramientas.
    /// </summary>
    public async Task<string?> GenerarConHerramientasAsync(
        string instrucciones, string entrada, IReadOnlyList<HerramientaIA> herramientas,
        Func<string, string, CancellationToken, Task<string>> ejecutarHerramienta,
        string nombreEsquema, JsonObject esquemaSalida, int maximoLlamadas, CancellationToken cancellationToken)
    {
        var proveedor = ResolverProveedor();
        if (proveedor is null) return null;

        try
        {
            return proveedor.UsaResponses
                ? await BucleResponsesAsync(proveedor, instrucciones, entrada, herramientas, ejecutarHerramienta, nombreEsquema, esquemaSalida, maximoLlamadas, cancellationToken)
                : await BucleChatAsync(proveedor, instrucciones, entrada, herramientas, ejecutarHerramienta, nombreEsquema, esquemaSalida, maximoLlamadas, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("El proveedor de IA {Proveedor} superó el tiempo de espera durante la investigación con herramientas.", proveedor.Nombre);
            return null;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "No fue posible comunicarse con el proveedor de IA {Proveedor} durante la investigación con herramientas.", proveedor.Nombre);
            return null;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            logger.LogWarning(ex, "El proveedor de IA {Proveedor} devolvió una respuesta no reconocida durante la investigación con herramientas.", proveedor.Nombre);
            return null;
        }
    }

    // Responses API (OpenAI y Groq).
    private async Task<string?> BucleResponsesAsync(
        Proveedor proveedor, string instrucciones, string entrada, IReadOnlyList<HerramientaIA> herramientas,
        Func<string, string, CancellationToken, Task<string>> ejecutarHerramienta,
        string nombreEsquema, JsonObject esquemaSalida, int maximoLlamadas, CancellationToken ct)
    {
        var maxTokens = MaxTokensDiagnostico();
        // Con store=false, los modelos de razonamiento de OpenAI necesitan recibir de vuelta su razonamiento cifrado entre llamadas.
        var razonamiento = proveedor.SoportaEstado && (proveedor.Modelo.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase) || proveedor.Modelo.StartsWith('o'));

        var definiciones = new JsonArray();
        foreach (var herramienta in herramientas)
            definiciones.Add(new JsonObject
            {
                ["type"] = "function", ["name"] = herramienta.Nombre, ["description"] = herramienta.Descripcion,
                ["parameters"] = JsonNode.Parse(herramienta.EsquemaParametrosJson), ["strict"] = true
            });

        var conversacion = new JsonArray { new JsonObject { ["role"] = "user", ["content"] = entrada } };
        var llamadas = 0;

        // Una vuelta más que llamadas permitidas: la última obliga a responder sin herramientas.
        for (var vuelta = 0; vuelta <= maximoLlamadas + 1; vuelta++)
        {
            var permitirHerramientas = llamadas < maximoLlamadas && definiciones.Count > 0;
            var cuerpo = new JsonObject
            {
                ["model"] = proveedor.Modelo, ["instructions"] = instrucciones, ["input"] = conversacion.DeepClone(), ["max_output_tokens"] = maxTokens,
                ["text"] = new JsonObject { ["format"] = new JsonObject { ["type"] = "json_schema", ["name"] = nombreEsquema, ["schema"] = esquemaSalida.DeepClone(), ["strict"] = true } }
            };
            if (proveedor.SoportaEstado) cuerpo["store"] = false;
            if (definiciones.Count > 0)
            {
                cuerpo["tools"] = definiciones.DeepClone();
                cuerpo["tool_choice"] = permitirHerramientas ? "auto" : "none";
                cuerpo["parallel_tool_calls"] = false;
            }
            if (razonamiento) cuerpo["include"] = new JsonArray("reasoning.encrypted_content");

            var documento = await EnviarAsync(proveedor, cuerpo, ct);
            if (documento?["output"] is not JsonArray salida) return null;

            var pendientes = new List<(string CallId, string Nombre, string Argumentos)>();
            foreach (var elemento in salida)
            {
                if (elemento is null) continue;
                var tipo = elemento["type"]?.GetValue<string>();
                // Razonamiento y llamadas vuelven tal cual a la conversación; así el modelo conserva su hilo.
                if (tipo == "function_call" || (tipo == "reasoning" && razonamiento)) conversacion.Add(elemento.DeepClone());
                if (tipo == "function_call")
                    pendientes.Add((elemento["call_id"]?.GetValue<string>() ?? string.Empty, elemento["name"]?.GetValue<string>() ?? string.Empty, elemento["arguments"]?.GetValue<string>() ?? "{}"));
            }

            if (pendientes.Count == 0)
            {
                var texto = TextoResponses(documento);
                return string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
            }

            foreach (var (callId, nombre, argumentos) in pendientes)
            {
                llamadas++;
                var resultado = llamadas <= maximoLlamadas ? await ejecutarHerramienta(nombre, argumentos, ct) : LimiteAlcanzado;
                conversacion.Add(new JsonObject { ["type"] = "function_call_output", ["call_id"] = callId, ["output"] = resultado });
            }
        }
        logger.LogWarning("La investigación con herramientas no produjo una respuesta final.");
        return null;
    }

    // Chat Completions (Gemini). Las herramientas y el formato estructurado se piden en llamadas separadas:
    // primero el modelo investiga con herramientas y al final se le exige el JSON con el esquema.
    private async Task<string?> BucleChatAsync(
        Proveedor proveedor, string instrucciones, string entrada, IReadOnlyList<HerramientaIA> herramientas,
        Func<string, string, CancellationToken, Task<string>> ejecutarHerramienta,
        string nombreEsquema, JsonObject esquemaSalida, int maximoLlamadas, CancellationToken ct)
    {
        var maxTokens = MaxTokensDiagnostico();
        var definiciones = new JsonArray();
        foreach (var herramienta in herramientas)
            definiciones.Add(new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject { ["name"] = herramienta.Nombre, ["description"] = herramienta.Descripcion, ["parameters"] = JsonNode.Parse(herramienta.EsquemaParametrosJson) }
            });

        var mensajes = new JsonArray(Mensaje("system", instrucciones), Mensaje("user", entrada));
        var llamadas = 0;

        while (llamadas < maximoLlamadas && definiciones.Count > 0)
        {
            var cuerpo = CuerpoChat(proveedor, mensajes.DeepClone().AsArray(), maxTokens);
            cuerpo["tools"] = definiciones.DeepClone();
            cuerpo["tool_choice"] = "auto";

            var documento = await EnviarAsync(proveedor, cuerpo, ct);
            if (documento?["choices"]?[0]?["message"] is not JsonObject mensaje) return null;
            if (mensaje["tool_calls"] is not JsonArray llamadasModelo || llamadasModelo.Count == 0) break;

            // El mensaje vuelve completo: Gemini exige recibir de nuevo su firma de razonamiento junto a cada llamada.
            mensajes.Add(mensaje.DeepClone());
            foreach (var llamada in llamadasModelo)
            {
                llamadas++;
                var nombre = llamada?["function"]?["name"]?.GetValue<string>() ?? string.Empty;
                var argumentos = llamada?["function"]?["arguments"]?.GetValue<string>() ?? "{}";
                var resultado = llamadas <= maximoLlamadas ? await ejecutarHerramienta(nombre, argumentos, ct) : LimiteAlcanzado;
                mensajes.Add(new JsonObject { ["role"] = "tool", ["tool_call_id"] = llamada?["id"]?.GetValue<string>() ?? string.Empty, ["name"] = nombre, ["content"] = resultado });
            }
        }

        mensajes.Add(Mensaje("user", "Con la evidencia reunida, responde ahora únicamente con el objeto JSON del esquema solicitado."));
        var final = CuerpoChat(proveedor, mensajes, maxTokens);
        final["response_format"] = new JsonObject
        {
            ["type"] = "json_schema",
            ["json_schema"] = new JsonObject { ["name"] = nombreEsquema, ["schema"] = esquemaSalida.DeepClone(), ["strict"] = true }
        };
        var respuesta = await EnviarAsync(proveedor, final, ct);
        var texto = respuesta is null ? null : TextoChat(respuesta);
        return string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
    }

    private const string LimiteAlcanzado = "{\"error\":\"Se alcanzó el límite de pasos de investigación. Responde con la evidencia reunida.\"}";

    private async Task<JsonNode?> EnviarAsync(Proveedor proveedor, JsonObject cuerpo, CancellationToken ct)
    {
        var ruta = proveedor.BaseUrl + (proveedor.UsaResponses ? "/responses" : "/chat/completions");
        // Los niveles gratuitos limitan solicitudes por minuto (429) y a veces saturan un modelo (503):
        // ante 429 se espera lo indicado y se reintenta; ante 503 se pasa una vez al modelo de respaldo.
        var cambioModelo = false;
        for (var intento = 0; intento < 3; intento++)
        {
            using var solicitud = new HttpRequestMessage(HttpMethod.Post, ruta);
            solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", proveedor.ApiKey);
            solicitud.Content = new StringContent(cuerpo.ToJsonString(), Encoding.UTF8, "application/json");

            using var respuesta = await httpClient.SendAsync(solicitud, HttpCompletionOption.ResponseHeadersRead, ct);
            if (respuesta.StatusCode == HttpStatusCode.ServiceUnavailable && !cambioModelo && !string.IsNullOrWhiteSpace(proveedor.ModeloRespaldo)
                && cuerpo["model"]?.GetValue<string>() != proveedor.ModeloRespaldo)
            {
                logger.LogInformation("El modelo {Modelo} de {Proveedor} está saturado; se usa {Respaldo}.", cuerpo["model"]?.GetValue<string>(), proveedor.Nombre, proveedor.ModeloRespaldo);
                cuerpo["model"] = proveedor.ModeloRespaldo;
                cambioModelo = true;
                continue;
            }
            if (respuesta.StatusCode == HttpStatusCode.TooManyRequests && intento < 2)
            {
                var espera = respuesta.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(10);
                logger.LogInformation("El proveedor de IA {Proveedor} limitó la frecuencia; se reintenta en {Segundos} s.", proveedor.Nombre, (int)espera.TotalSeconds);
                await Task.Delay(espera > TimeSpan.FromSeconds(20) ? TimeSpan.FromSeconds(20) : espera, ct);
                continue;
            }
            if (!respuesta.IsSuccessStatusCode)
            {
                var detalle = await respuesta.Content.ReadAsStringAsync(ct);
                logger.LogWarning("El proveedor de IA {Proveedor} respondió con estado {Estado}: {Detalle}", proveedor.Nombre, (int)respuesta.StatusCode, detalle.Length > 500 ? detalle[..500] : detalle);
                return null;
            }

            await using var contenido = await respuesta.Content.ReadAsStreamAsync(ct);
            return await JsonNode.ParseAsync(contenido, cancellationToken: ct);
        }
        return null;
    }

    private static JsonObject CuerpoChat(Proveedor proveedor, JsonArray mensajes, int maxTokens)
    {
        var cuerpo = new JsonObject { ["model"] = proveedor.Modelo, ["messages"] = mensajes, ["max_tokens"] = maxTokens };
        // Los modelos Gemini 3 razonan por defecto con esfuerzo alto; "low" basta para soporte TI y reduce latencia y cuota.
        if (proveedor.Nombre == "Gemini") cuerpo["reasoning_effort"] = "low";
        return cuerpo;
    }

    private static JsonObject Mensaje(string rol, string contenido) => new() { ["role"] = rol, ["content"] = contenido };

    private static string? TextoResponses(JsonNode documento)
    {
        if (documento["output"] is not JsonArray salida) return null;
        var partes = new List<string>();
        foreach (var elemento in salida)
            if (elemento?["type"]?.GetValue<string>() == "message" && elemento["content"] is JsonArray bloques)
                foreach (var bloque in bloques)
                    if (bloque?["type"]?.GetValue<string>() == "output_text" && bloque["text"]?.GetValue<string>() is { Length: > 0 } texto)
                        partes.Add(texto.Trim());
        return partes.Count == 0 ? null : string.Join("\n", partes);
    }

    private static string? TextoChat(JsonNode documento) => documento["choices"]?[0]?["message"]?["content"]?.GetValue<string>();

    private int MaxTokensDiagnostico() => Math.Clamp(configuration.GetValue<int?>("AgenteTI:MaxTokensDiagnostico") ?? 4000, 1000, 12000);

    private Proveedor? ResolverProveedor()
    {
        var elegido = configuration["AsistenteIA:Proveedor"]?.Trim();
        var openAI = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? configuration["AsistenteIA:ApiKey"];
        var gemini = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? configuration["AsistenteLive:ApiKey"];
        var groq = Environment.GetEnvironmentVariable("GROQ_API_KEY") ?? configuration["AsistenteIA:GroqApiKey"];

        if (string.IsNullOrWhiteSpace(elegido))
            elegido = !string.IsNullOrWhiteSpace(openAI) ? "OpenAI" : !string.IsNullOrWhiteSpace(gemini) ? "Gemini" : !string.IsNullOrWhiteSpace(groq) ? "Groq" : null;

        return elegido?.ToUpperInvariant() switch
        {
            "OPENAI" when !string.IsNullOrWhiteSpace(openAI) => new Proveedor("OpenAI", "https://api.openai.com/v1", openAI, Modelo("Modelo", "gpt-5-mini"), true, true, null),
            "GEMINI" when !string.IsNullOrWhiteSpace(gemini) => new Proveedor("Gemini", "https://generativelanguage.googleapis.com/v1beta/openai", gemini, Modelo("ModeloGemini", "gemini-3.5-flash"), false, false, Modelo("ModeloGeminiRespaldo", "gemini-3.1-flash-lite")),
            "GROQ" when !string.IsNullOrWhiteSpace(groq) => new Proveedor("Groq", "https://api.groq.com/openai/v1", groq, Modelo("ModeloGroq", "openai/gpt-oss-120b"), true, false, null),
            _ => null
        };
    }

    private string Modelo(string clave, string porDefecto)
    {
        var modelo = configuration[$"AsistenteIA:{clave}"]?.Trim();
        return string.IsNullOrWhiteSpace(modelo) ? porDefecto : modelo;
    }

    /// <param name="UsaResponses">true: Responses API (OpenAI, Groq); false: Chat Completions (Gemini).</param>
    /// <param name="SoportaEstado">Acepta store/include (solo OpenAI).</param>
    /// <param name="ModeloRespaldo">Modelo alterno cuando el principal está saturado (503), habitual en niveles gratuitos.</param>
    private sealed record Proveedor(string Nombre, string BaseUrl, string ApiKey, string Modelo, bool UsaResponses, bool SoportaEstado, string? ModeloRespaldo);
}

public sealed record HerramientaIA(string Nombre, string Descripcion, string EsquemaParametrosJson);
