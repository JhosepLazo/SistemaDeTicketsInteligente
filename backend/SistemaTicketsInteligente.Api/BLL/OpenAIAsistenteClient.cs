/**
 * Archivo: OpenAIAsistenteClient.cs
 * Objetivo: Consumir un proveedor de IA compatible con OpenAI desde el servidor.
 * Responsabilidad: Enviar únicamente el prompt y contexto previamente autorizados, y devolver texto sin exponer credenciales al navegador.
 * Dependencias: HttpClient, IConfiguration y System.Text.Json.
 * Flujo: Asistente BLL -> proveedor (OpenAI Responses, Groq Responses o Gemini Chat Completions) -> texto de respuesta.
 * Consideraciones: El proveedor sale de AsistenteIA:Proveedor (OpenAI, Gemini o Groq); si no se indica, se usa el primero que tenga clave:
 *   OPENAI_API_KEY, GEMINI_API_KEY o GROQ_API_KEY. Gemini y Groq tienen nivel gratuito para pruebas. Nunca se registra el contenido enviado.
 *   El chat usa un modelo rápido (ModeloChat) y el diagnóstico el modelo completo; los embeddings alimentan la búsqueda semántica.
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

    /// <summary>Texto para conversación: modelo rápido y un tiempo corto por modelo, para pasar pronto al respaldo si uno está saturado.</summary>
    public Task<string?> GenerarAsync(string instrucciones, string entrada, CancellationToken cancellationToken) =>
        GenerarTextoAsync(instrucciones, entrada, MaxTokensChat(), null, null, SegundosPorModelo("SegundosPorModeloChat", 25), cancellationToken);

    /// <summary>
    /// Respuesta que se interpretará como JSON. Reserva al menos <paramref name="maxTokens"/>: los modelos con razonamiento gastan
    /// parte del límite pensando y un JSON cortado no sirve. Con <paramref name="esquema"/>, el proveedor garantiza la estructura.
    /// </summary>
    public Task<string?> GenerarJsonAsync(string instrucciones, string entrada, CancellationToken cancellationToken,
        int maxTokens = 2000, string? nombreEsquema = null, JsonObject? esquema = null) =>
        GenerarTextoAsync(instrucciones, entrada, Math.Clamp(Math.Max(MaxTokensChat(), maxTokens), 300, 12000), nombreEsquema, esquema,
            SegundosPorModelo("SegundosPorModelo", 60), cancellationToken);

    private async Task<string?> GenerarTextoAsync(string instrucciones, string entrada, int maxTokens, string? nombreEsquema, JsonObject? esquema,
        TimeSpan tiempoPorModelo, CancellationToken cancellationToken)
    {
        var proveedor = ResolverProveedor();
        if (proveedor is null) return null;

        JsonObject cuerpo;
        if (proveedor.UsaResponses)
        {
            cuerpo = new JsonObject { ["model"] = proveedor.ModeloChat, ["instructions"] = instrucciones, ["input"] = entrada, ["max_output_tokens"] = maxTokens };
            if (proveedor.SoportaEstado) cuerpo["store"] = false;
            if (esquema is not null) cuerpo["text"] = FormatoResponses(nombreEsquema ?? "respuesta", esquema);
        }
        else
        {
            cuerpo = CuerpoChat(proveedor, new JsonArray(Mensaje("system", instrucciones), Mensaje("user", entrada)), maxTokens);
            cuerpo["model"] = proveedor.ModeloChat;
            if (esquema is not null) cuerpo["response_format"] = FormatoChat(nombreEsquema ?? "respuesta", esquema);
        }

        try
        {
            var respuesta = await EnviarAsync(proveedor, cuerpo, cancellationToken, null, Candidatos(proveedor, proveedor.ModeloChat), tiempoPorModelo);
            if (respuesta is null) return null;
            var texto = proveedor.UsaResponses ? TextoResponses(respuesta.Documento) : TextoChat(respuesta.Documento);
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
    /// y termina con un JSON que cumple <paramref name="esquemaSalida"/>. Cuando se cancela <paramref name="cierre"/> ya no se
    /// ejecutan herramientas y se pide la respuesta final con lo reunido; <paramref name="cancellationToken"/> es el límite duro.
    /// Devuelve null si el proveedor falla, para que el llamador use su ruta sin herramientas.
    /// </summary>
    public async Task<string?> GenerarConHerramientasAsync(
        string instrucciones, string entrada, IReadOnlyList<HerramientaIA> herramientas,
        Func<string, string, CancellationToken, Task<string>> ejecutarHerramienta,
        string nombreEsquema, JsonObject esquemaSalida, int maximoLlamadas, CancellationToken cancellationToken, CancellationToken cierre = default)
    {
        var proveedor = ResolverProveedor();
        if (proveedor is null) return null;

        try
        {
            return proveedor.UsaResponses
                ? await BucleResponsesAsync(proveedor, instrucciones, entrada, herramientas, ejecutarHerramienta, nombreEsquema, esquemaSalida, maximoLlamadas, cancellationToken, cierre)
                : await BucleChatAsync(proveedor, instrucciones, entrada, herramientas, ejecutarHerramienta, nombreEsquema, esquemaSalida, maximoLlamadas, cancellationToken, cierre);
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

    public bool SoportaEmbeddings => ResolverProveedor()?.ModeloEmbeddings is not null;

    /// <summary>Identifica el modelo de embeddings activo; los vectores de modelos distintos no son comparables.</summary>
    public string ModeloEmbeddingsActivo => ResolverProveedor()?.ModeloEmbeddings ?? string.Empty;

    public const int DimensionesEmbedding = 768;

    /// <summary>Calcula embeddings de varios textos (en lotes). Devuelve null si el proveedor no los soporta o falla.</summary>
    public async Task<List<float[]>?> GenerarEmbeddingsAsync(IReadOnlyList<string> textos, CancellationToken ct)
    {
        var proveedor = ResolverProveedor();
        if (proveedor?.ModeloEmbeddings is null || textos.Count == 0) return null;
        var vectores = new List<float[]>(textos.Count);
        try
        {
            foreach (var lote in textos.Chunk(32))
            {
                var cuerpo = new JsonObject
                {
                    ["model"] = proveedor.ModeloEmbeddings,
                    ["input"] = new JsonArray(lote.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()),
                    ["dimensions"] = DimensionesEmbedding
                };
                var respuesta = await EnviarAsync(proveedor, cuerpo, ct, "/embeddings", [proveedor.ModeloEmbeddings]);
                if (respuesta?.Documento["data"] is not JsonArray datos || datos.Count != lote.Length) return null;
                foreach (var dato in datos.OrderBy(x => x?["index"]?.GetValue<int>() ?? 0))
                {
                    if (dato?["embedding"] is not JsonArray numeros || numeros.Count != DimensionesEmbedding) return null;
                    vectores.Add(numeros.Select(x => x!.GetValue<float>()).ToArray());
                }
            }
            return vectores;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or FormatException
            || (ex is OperationCanceledException && !ct.IsCancellationRequested))
        {
            logger.LogWarning(ex, "No fue posible calcular embeddings con {Proveedor}.", proveedor.Nombre);
            return null;
        }
    }

    // Responses API (OpenAI y Groq).
    private async Task<string?> BucleResponsesAsync(
        Proveedor proveedor, string instrucciones, string entrada, IReadOnlyList<HerramientaIA> herramientas,
        Func<string, string, CancellationToken, Task<string>> ejecutarHerramienta,
        string nombreEsquema, JsonObject esquemaSalida, int maximoLlamadas, CancellationToken ct, CancellationToken cierre)
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
            var permitirHerramientas = llamadas < maximoLlamadas && definiciones.Count > 0 && !cierre.IsCancellationRequested;
            var cuerpo = new JsonObject
            {
                ["model"] = proveedor.Modelo, ["instructions"] = instrucciones, ["input"] = conversacion.DeepClone(), ["max_output_tokens"] = maxTokens,
                ["text"] = FormatoResponses(nombreEsquema, esquemaSalida)
            };
            if (proveedor.SoportaEstado) cuerpo["store"] = false;
            if (definiciones.Count > 0)
            {
                cuerpo["tools"] = definiciones.DeepClone();
                cuerpo["tool_choice"] = permitirHerramientas ? "auto" : "none";
                cuerpo["parallel_tool_calls"] = false;
            }
            if (razonamiento) cuerpo["include"] = new JsonArray("reasoning.encrypted_content");

            var enviada = await EnviarAsync(proveedor, cuerpo, ct, null, Candidatos(proveedor, proveedor.Modelo), SegundosPorModelo("SegundosPorModelo", 75));
            var documento = enviada?.Documento;
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
                var resultado = llamadas <= maximoLlamadas && !cierre.IsCancellationRequested ? await ejecutarHerramienta(nombre, argumentos, ct) : LimiteAlcanzado;
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
        string nombreEsquema, JsonObject esquemaSalida, int maximoLlamadas, CancellationToken ct, CancellationToken cierre)
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
        // Si el modelo principal está saturado y responde otro, la conversación sigue con ese (conserva su hilo de razonamiento).
        var modeloVigente = proveedor.Modelo;

        while (llamadas < maximoLlamadas && definiciones.Count > 0 && !cierre.IsCancellationRequested)
        {
            var cuerpo = CuerpoChat(proveedor, mensajes.DeepClone().AsArray(), maxTokens);
            cuerpo["tools"] = definiciones.DeepClone();
            cuerpo["tool_choice"] = "auto";

            var enviada = await EnviarAsync(proveedor, cuerpo, ct, null, Candidatos(proveedor, modeloVigente), SegundosPorModelo("SegundosPorModelo", 75));
            if (enviada is null) return null;
            modeloVigente = enviada.Modelo;
            if (enviada.Documento["choices"]?[0]?["message"] is not JsonObject mensaje) return null;
            if (mensaje["tool_calls"] is not JsonArray llamadasModelo || llamadasModelo.Count == 0) break;

            // El mensaje vuelve completo: Gemini exige recibir de nuevo su firma de razonamiento junto a cada llamada.
            mensajes.Add(mensaje.DeepClone());
            foreach (var llamada in llamadasModelo)
            {
                llamadas++;
                var nombre = llamada?["function"]?["name"]?.GetValue<string>() ?? string.Empty;
                var argumentos = llamada?["function"]?["arguments"]?.GetValue<string>() ?? "{}";
                var resultado = llamadas <= maximoLlamadas && !cierre.IsCancellationRequested ? await ejecutarHerramienta(nombre, argumentos, ct) : LimiteAlcanzado;
                mensajes.Add(new JsonObject { ["role"] = "tool", ["tool_call_id"] = llamada?["id"]?.GetValue<string>() ?? string.Empty, ["name"] = nombre, ["content"] = resultado });
            }
        }

        mensajes.Add(Mensaje("user", "Con la evidencia reunida, responde ahora únicamente con el objeto JSON del esquema solicitado."));
        var final = CuerpoChat(proveedor, mensajes, maxTokens);
        final["response_format"] = FormatoChat(nombreEsquema, esquemaSalida);
        var respuesta = await EnviarAsync(proveedor, final, ct, null, Candidatos(proveedor, modeloVigente), SegundosPorModelo("SegundosPorModelo", 75));
        var texto = respuesta is null ? null : TextoChat(respuesta.Documento);
        return string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
    }

    private const string LimiteAlcanzado = "{\"error\":\"Se alcanzó el límite de pasos o de tiempo de la investigación. Responde con la evidencia reunida.\"}";

    private sealed record RespuestaProveedor(JsonNode Documento, string Modelo);

    /// <summary>El modelo pedido y, después, los de respaldo; los saturados recientemente van al final.</summary>
    private static IReadOnlyList<string> Candidatos(Proveedor proveedor, string principal) => SaturacionModelos.Ordenar(new[] { principal }.Concat(proveedor.Respaldos));

    /// <summary>
    /// Envía la solicitud recorriendo la cadena de modelos: ante 503 (alta demanda), 500/502/504 (falla transitoria), 404 (modelo no disponible)
    /// o 400 (opción que ese modelo no admite) pasa al siguiente; ante 429 (cuota por minuto) espera y reintenta, y si persiste también pasa
    /// al siguiente (la cuota es por modelo). Con <paramref name="tiempoPorModelo"/>, un modelo que tarda más de eso se da por saturado.
    /// </summary>
    private async Task<RespuestaProveedor?> EnviarAsync(Proveedor proveedor, JsonObject cuerpo, CancellationToken ct, string? rutaRelativa,
        IReadOnlyList<string?> candidatos, TimeSpan? tiempoPorModelo = null)
    {
        var ruta = proveedor.BaseUrl + (rutaRelativa ?? (proveedor.UsaResponses ? "/responses" : "/chat/completions"));
        foreach (var modelo in candidatos.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!))
        {
            cuerpo["model"] = modelo;
            for (var intento = 0; intento < 3; intento++)
            {
                // Un modelo saturado puede tardar casi un minuto en contestar 503: con límite propio se pasa antes al siguiente.
                using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
                if (tiempoPorModelo is { } tiempo) limite.CancelAfter(tiempo);
                try
                {
                    using var solicitud = new HttpRequestMessage(HttpMethod.Post, ruta);
                    solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", proveedor.ApiKey);
                    solicitud.Content = new StringContent(cuerpo.ToJsonString(), Encoding.UTF8, "application/json");

                    using var respuesta = await httpClient.SendAsync(solicitud, HttpCompletionOption.ResponseHeadersRead, limite.Token);
                    var estado = (int)respuesta.StatusCode;
                    if (estado is 400 or 404 or 500 or 502 or 503 or 504)
                    {
                        if (estado >= 500) SaturacionModelos.Marcar(modelo, TimeSpan.FromMinutes(estado == 503 ? 3 : 1));
                        if (estado == 400)
                        {
                            var motivo = await respuesta.Content.ReadAsStringAsync(limite.Token);
                            logger.LogWarning("El modelo {Modelo} de {Proveedor} rechazó la solicitud (400): {Detalle}", modelo, proveedor.Nombre, motivo.Length > 500 ? motivo[..500] : motivo);
                        }
                        else logger.LogInformation("El modelo {Modelo} de {Proveedor} no está disponible ({Estado}); se prueba el siguiente.", modelo, proveedor.Nombre, estado);
                        break;
                    }
                    if (respuesta.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        if (intento == 2)
                        {
                            SaturacionModelos.Marcar(modelo, TimeSpan.FromMinutes(1));
                            break;
                        }
                        var espera = respuesta.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(10);
                        logger.LogInformation("El proveedor de IA {Proveedor} limitó la frecuencia de {Modelo}; se reintenta en {Segundos} s.", proveedor.Nombre, modelo, (int)espera.TotalSeconds);
                        await Task.Delay(espera > TimeSpan.FromSeconds(20) ? TimeSpan.FromSeconds(20) : espera, ct);
                        continue;
                    }
                    if (!respuesta.IsSuccessStatusCode)
                    {
                        var detalle = await respuesta.Content.ReadAsStringAsync(limite.Token);
                        logger.LogWarning("El proveedor de IA {Proveedor} respondió con estado {Estado} ({Modelo}): {Detalle}", proveedor.Nombre, estado, modelo, detalle.Length > 500 ? detalle[..500] : detalle);
                        return null;
                    }

                    await using var contenido = await respuesta.Content.ReadAsStreamAsync(limite.Token);
                    var documento = await JsonNode.ParseAsync(contenido, cancellationToken: limite.Token);
                    if (documento is null) return null;
                    SaturacionModelos.Liberar(modelo);
                    return new RespuestaProveedor(documento, modelo);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    // Venció el tiempo de este modelo (o el del HttpClient): se marca como saturado y se prueba el siguiente.
                    SaturacionModelos.Marcar(modelo, TimeSpan.FromMinutes(2));
                    logger.LogInformation("El modelo {Modelo} de {Proveedor} no respondió a tiempo; se prueba el siguiente.", modelo, proveedor.Nombre);
                    break;
                }
            }
        }
        logger.LogWarning("Ningún modelo de {Proveedor} respondió; se usa la ruta sin IA.", proveedor.Nombre);
        return null;
    }

    private static JsonObject FormatoResponses(string nombre, JsonObject esquema) =>
        new() { ["format"] = new JsonObject { ["type"] = "json_schema", ["name"] = nombre, ["schema"] = esquema.DeepClone(), ["strict"] = true } };

    private static JsonObject FormatoChat(string nombre, JsonObject esquema) =>
        new() { ["type"] = "json_schema", ["json_schema"] = new JsonObject { ["name"] = nombre, ["schema"] = esquema.DeepClone(), ["strict"] = true } };

    private int MaxTokensChat() => Math.Clamp(configuration.GetValue<int?>("AsistenteIA:MaxTokens") ?? 1800, 300, 3000);

    private TimeSpan SegundosPorModelo(string clave, int porDefecto) =>
        TimeSpan.FromSeconds(Math.Clamp(configuration.GetValue<int?>($"AsistenteIA:{clave}") ?? porDefecto, 5, 300));

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
            "OPENAI" when !string.IsNullOrWhiteSpace(openAI) => new Proveedor("OpenAI", "https://api.openai.com/v1", openAI, Modelo("Modelo", "gpt-5-mini"), true, true, [])
                { ModeloChat = Modelo("ModeloChat", Modelo("Modelo", "gpt-5-mini")), ModeloEmbeddings = Modelo("ModeloEmbeddings", "text-embedding-3-small") },
            "GEMINI" when !string.IsNullOrWhiteSpace(gemini) => new Proveedor("Gemini", "https://generativelanguage.googleapis.com/v1beta/openai", gemini, Modelo("ModeloGemini", "gemini-3.5-flash"), false, false, RespaldosGemini(configuration))
                { ModeloChat = Modelo("ModeloGeminiChat", "gemini-3.1-flash-lite"), ModeloEmbeddings = Modelo("ModeloGeminiEmbeddings", "gemini-embedding-2") },
            // Groq no ofrece embeddings: la búsqueda semántica queda desactivada y se usa la búsqueda por palabras.
            "GROQ" when !string.IsNullOrWhiteSpace(groq) => new Proveedor("Groq", "https://api.groq.com/openai/v1", groq, Modelo("ModeloGroq", "openai/gpt-oss-120b"), true, false, [])
                { ModeloChat = Modelo("ModeloGroqChat", Modelo("ModeloGroq", "openai/gpt-oss-120b")) },
            _ => null
        };
    }

    /// <summary>Modelos Gemini gratuitos que se prueban cuando el principal está saturado (AsistenteIA:ModelosGeminiRespaldo).</summary>
    public static IReadOnlyList<string> RespaldosGemini(IConfiguration configuration)
    {
        var configurados = configuration.GetSection("AsistenteIA:ModelosGeminiRespaldo").Get<string[]>() ?? [];
        var uno = configuration["AsistenteIA:ModeloGeminiRespaldo"];
        var lista = configurados.Length > 0 ? configurados : ["gemini-3.8-flash", "gemini-3-flash-preview", "gemini-3.5-flash", "gemini-3.5-flash-lite", "gemini-3.1-flash-lite"];
        return lista.Concat(string.IsNullOrWhiteSpace(uno) ? [] : [uno]).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private string Modelo(string clave, string porDefecto)
    {
        var modelo = configuration[$"AsistenteIA:{clave}"]?.Trim();
        return string.IsNullOrWhiteSpace(modelo) ? porDefecto : modelo;
    }

    /// <param name="UsaResponses">true: Responses API (OpenAI, Groq); false: Chat Completions (Gemini).</param>
    /// <param name="SoportaEstado">Acepta store/include (solo OpenAI).</param>
    /// <param name="Respaldos">Modelos alternos cuando el principal está saturado (503) o sin cuota (429), habitual en niveles gratuitos.</param>
    private sealed record Proveedor(string Nombre, string BaseUrl, string ApiKey, string Modelo, bool UsaResponses, bool SoportaEstado, IReadOnlyList<string> Respaldos)
    {
        /// <summary>Modelo para conversación: prioriza latencia.</summary>
        public string ModeloChat { get; init; } = Modelo;
        public string? ModeloEmbeddings { get; init; }
    }
}

public sealed record HerramientaIA(string Nombre, string Descripcion, string EsquemaParametrosJson);
