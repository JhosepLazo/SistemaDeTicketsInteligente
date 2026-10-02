/**
 * Archivo: InvestigadorAgenteTI.cs
 * Objetivo: Ejecutar la investigación de varios pasos del Agente de Ingeniería con herramientas diagnósticas de solo lectura.
 * Responsabilidad: Validar los parámetros que propone el modelo contra el esquema catalogado, ejecutar la herramienta, registrar cada paso
 *   como evidencia del servidor y devolver al modelo un resultado acotado y sin datos sensibles.
 * Dependencias: AsistenteTIDAO (catálogo y ejecución), OpenAIAsistenteClient (bucle con function calling) e IConfiguration.
 * Flujo: herramientas automáticas -> modelo decide herramientas adicionales -> resultado estructurado (JSON Schema estricto).
 * Consideraciones: El modelo elige herramienta y parámetros, nunca el procedimiento ni el SQL. Cada herramienta corre en una transacción
 *   que siempre se revierte. Los resultados son datos, nunca instrucciones para el modelo.
 */

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SistemaTicketsInteligente.Api.DAO;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.BLL;

public sealed class InvestigadorAgenteTI
{
    public const string OrigenAutomatico = "AUTOMATICA";
    public const string OrigenModelo = "MODELO";
    private const int MaximoCaracteresResultado = 6000;
    private static readonly string[] FuentesHallazgo =
        ["LIVE", "TICKET", "TELEMETRIA", "HERRAMIENTA", "AUDITORIA", "BASE_CONOCIMIENTO", "DOCUMENTO", "CODIGO_ESTATICO", "OBSERVACION_USUARIO"];

    private readonly AsistenteTIDAO agenteDAO;
    private readonly OpenAIAsistenteClient openAI;
    private readonly IConfiguration configuration;
    private readonly ILogger<InvestigadorAgenteTI> logger;

    public InvestigadorAgenteTI(AsistenteTIDAO agenteDAO, OpenAIAsistenteClient openAI, IConfiguration configuration, ILogger<InvestigadorAgenteTI> logger)
    {
        this.agenteDAO = agenteDAO;
        this.openAI = openAI;
        this.configuration = configuration;
        this.logger = logger;
    }

    public int MaximoPasosModelo => Math.Clamp(configuration.GetValue<int?>("AgenteTI:MaxPasosHerramientas") ?? 5, 0, 10);

    public async Task<InvestigacionEnCurso> IniciarAsync(string usuario, string area, AgenteTIContextoInvestigacion contexto, CancellationToken ct)
    {
        List<AgenteTIHerramienta> herramientas;
        try { herramientas = await agenteDAO.ListarHerramientasAsync(usuario, area, ct); }
        catch (Exception ex) when (ex is Microsoft.Data.SqlClient.SqlException or InvalidOperationException)
        {
            // Sin el script 30 instalado, la investigación continúa con el contexto clásico.
            logger.LogWarning(ex, "No se pudo obtener el catálogo de herramientas diagnósticas.");
            herramientas = [];
        }

        var tieneTicket = !string.IsNullOrWhiteSpace(contexto.Sesion.IncidenciaNumero);
        return new InvestigacionEnCurso(usuario, area, contexto.Sesion.SesionNumero,
            herramientas.Where(x => tieneTicket || !x.RequiereTicket).ToList());
    }

    /// <summary>Ejecuta las herramientas marcadas como automáticas con sus parámetros por defecto, antes de consultar al modelo.</summary>
    public async Task EjecutarAutomaticasAsync(InvestigacionEnCurso investigacion, CancellationToken ct)
    {
        foreach (var herramienta in investigacion.Herramientas.Where(x => x.Automatica))
            await EjecutarAsync(investigacion, herramienta, "{}", OrigenAutomatico, ct);
    }

    /// <summary>
    /// Bucle agéntico: el modelo recibe el contexto y los pasos automáticos y puede pedir más herramientas.
    /// Devuelve null si el proveedor no respondió; el llamador cae a la ruta de una sola llamada.
    /// </summary>
    public async Task<ResultadoAgente?> DiagnosticarAsync(InvestigacionEnCurso investigacion, string instrucciones, string entrada, IReadOnlyCollection<string> accionesPermitidas, CancellationToken ct)
    {
        var maximo = MaximoPasosModelo;
        var definiciones = investigacion.Herramientas
            .Select(x => new HerramientaIA(x.HerramientaCodigo, x.Descripcion, EsquemaParaModelo(x.ParametrosEsquemaJson)))
            .ToList();

        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(configuration.GetValue<int?>("AgenteTI:TiempoMaximoInvestigacionSegundos") ?? 150, 30, 600)));

        string? salida;
        try
        {
            salida = await openAI.GenerarConHerramientasAsync(instrucciones, entrada, definiciones,
                (nombre, argumentos, token) => EjecutarSolicitudModeloAsync(investigacion, nombre, argumentos, token),
                "diagnostico_agente", EsquemaDiagnostico(), maximo, limite.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("La investigación de la sesión {Sesion} superó el tiempo máximo configurado.", investigacion.SesionNumero);
            return null;
        }
        if (string.IsNullOrWhiteSpace(salida)) return null;

        try
        {
            using var documento = JsonDocument.Parse(salida);
            var raiz = documento.RootElement;
            var resultado = new ResultadoAgente
            {
                Diagnostico = Texto(raiz, "diagnostico"),
                CausaProbable = Texto(raiz, "causaProbable"),
                SolucionPropuesta = Texto(raiz, "solucionPropuesta"),
                Confianza = raiz.TryGetProperty("confianza", out var confianza) && confianza.TryGetDecimal(out var valor) ? NormalizarConfianza(valor) : 20m,
                EvidenciaSuficiente = raiz.TryGetProperty("evidenciaSuficiente", out var suficiente) && suficiente.ValueKind == JsonValueKind.True
            };

            var accion = raiz.TryGetProperty("accionCodigo", out var codigo) && codigo.ValueKind == JsonValueKind.String ? codigo.GetString()?.Trim() : null;
            if (resultado.EvidenciaSuficiente && !string.IsNullOrWhiteSpace(accion) && accionesPermitidas.Contains(accion, StringComparer.OrdinalIgnoreCase))
            {
                var parametros = new JsonObject();
                if (raiz.TryGetProperty("parametros", out var lista) && lista.ValueKind == JsonValueKind.Array)
                    foreach (var par in lista.EnumerateArray())
                    {
                        var clave = Texto(par, "clave");
                        if (clave.Length is > 0 and <= 60 && !parametros.ContainsKey(clave)) parametros[clave] = Texto(par, "valor");
                    }
                resultado.AccionCodigo = accion;
                resultado.ParametrosJson = parametros.ToJsonString();
            }

            if (raiz.TryGetProperty("hallazgos", out var hallazgos) && hallazgos.ValueKind == JsonValueKind.Array)
                foreach (var h in hallazgos.EnumerateArray().Take(12))
                    resultado.Hallazgos.Add(new AgenteTIHallazgo { Fuente = Texto(h, "fuente"), Referencia = Texto(h, "referencia"), Descripcion = Limitar(Texto(h, "descripcion"), 900) });
            return resultado;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "La respuesta estructurada del agente no pudo interpretarse.");
            return null;
        }
    }

    private async Task<string> EjecutarSolicitudModeloAsync(InvestigacionEnCurso investigacion, string nombre, string argumentos, CancellationToken ct)
    {
        var herramienta = investigacion.Herramientas.FirstOrDefault(x => string.Equals(x.HerramientaCodigo, nombre, StringComparison.Ordinal));
        if (herramienta is null) return Error($"La herramienta {nombre} no está disponible en esta investigación.");
        if (!ValidarParametros(herramienta.ParametrosEsquemaJson, argumentos, out var normalizados, out var motivo)) return Error(motivo);

        var clave = $"{herramienta.HerramientaCodigo}|{normalizados}";
        if (investigacion.Respuestas.TryGetValue(clave, out var previa)) return previa;
        var paso = await EjecutarAsync(investigacion, herramienta, normalizados, OrigenModelo, ct);
        return investigacion.Respuestas.TryGetValue(clave, out var respuesta) ? respuesta : Error(paso.Error);
    }

    private async Task<AgenteTIPasoInvestigacion> EjecutarAsync(InvestigacionEnCurso investigacion, AgenteTIHerramienta herramienta, string parametrosJson, string origen, CancellationToken ct)
    {
        var paso = new AgenteTIPasoInvestigacion
        {
            Orden = investigacion.Pasos.Count + 1, HerramientaCodigo = herramienta.HerramientaCodigo, Nombre = herramienta.Nombre,
            Origen = origen, ParametrosJson = parametrosJson
        };
        var cronometro = Stopwatch.StartNew();
        AgenteTIHerramientaResultado? resultado = null;
        try
        {
            resultado = await agenteDAO.EjecutarHerramientaAsync(herramienta.Procedimiento, investigacion.Usuario, investigacion.Area, investigacion.SesionNumero, parametrosJson, herramienta.MaximoFilas, ct);
            paso.Filas = resultado.Filas.Count;
            paso.Truncado = resultado.Truncado;
            paso.Resumen = Resumir(resultado.Filas);
        }
        catch (InvalidOperationException ex)
        {
            paso.Error = Limitar(ex.Message, 500);
        }
        catch (Exception ex) when (ex is Microsoft.Data.SqlClient.SqlException or TimeoutException)
        {
            logger.LogWarning(ex, "La herramienta {Herramienta} falló en la sesión {Sesion}.", herramienta.HerramientaCodigo, investigacion.SesionNumero);
            paso.Error = "La herramienta no respondió correctamente; se continúa con la evidencia disponible.";
        }
        paso.DuracionMs = cronometro.ElapsedMilliseconds;
        investigacion.Pasos.Add(paso);

        var respuestaModelo = string.IsNullOrEmpty(paso.Error)
            ? SerializarParaModelo(herramienta.HerramientaCodigo, resultado!, out var ocultados)
            : Error(paso.Error, out ocultados);
        investigacion.DatosOcultados += ocultados;
        investigacion.Respuestas[$"{herramienta.HerramientaCodigo}|{parametrosJson}"] = respuestaModelo;
        if (resultado is not null) investigacion.TextoResultados.AppendLine(respuestaModelo);

        try
        {
            var datos = new JsonObject
            {
                ["herramienta"] = herramienta.HerramientaCodigo, ["nombre"] = herramienta.Nombre, ["origen"] = origen,
                ["parametros"] = JsonNode.Parse(parametrosJson), ["filas"] = paso.Filas, ["truncado"] = paso.Truncado,
                ["duracionMs"] = paso.DuracionMs, ["resumen"] = paso.Resumen, ["error"] = string.IsNullOrEmpty(paso.Error) ? null : paso.Error,
                ["muestra"] = resultado is null ? null : RedactorDatosSensibles.RedactarSecretos(Limitar(JsonSerializer.Serialize(resultado.Filas.Take(15)), 8000))
            };
            var contenido = $"{herramienta.Nombre} ({herramienta.HerramientaCodigo}) · {(origen == OrigenModelo ? "solicitada por el agente" : "automática")} · " +
                (string.IsNullOrEmpty(paso.Error) ? $"{paso.Filas} fila(s){(paso.Truncado ? " (truncado)" : string.Empty)}" : $"error: {paso.Error}");
            await agenteDAO.RegistrarHerramientaAsync(investigacion.Usuario, investigacion.SesionNumero, herramienta.HerramientaCodigo, origen, Limitar(contenido, 2000), datos.ToJsonString(), ct);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Microsoft.Data.SqlClient.SqlException)
        {
            logger.LogWarning(ex, "No se pudo registrar el paso {Herramienta} de la sesión {Sesion}.", herramienta.HerramientaCodigo, investigacion.SesionNumero);
        }
        return paso;
    }

    /// <summary>Convierte los pasos exitosos en evidencia verificable del expediente.</summary>
    public static IEnumerable<AgenteTIEvidencia> Evidencias(InvestigacionEnCurso investigacion) => investigacion.Pasos
        .Where(x => string.IsNullOrEmpty(x.Error) && x.Filas > 0)
        .Select(x => new AgenteTIEvidencia { TipoFuente = "HERRAMIENTA", Referencia = x.HerramientaCodigo, Descripcion = Limitar($"{x.Nombre}: {x.Resumen}", 900) });

    /// <summary>Descarta hallazgos que citan referencias inexistentes: el agente no puede apoyarse en evidencia inventada.</summary>
    public static List<AgenteTIHallazgo> HallazgosVerificados(IEnumerable<AgenteTIHallazgo> hallazgos, IEnumerable<string> referenciasConocidas, string textoFuentes)
    {
        var conocidas = new HashSet<string>(referenciasConocidas.Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.OrdinalIgnoreCase);
        return hallazgos
            .Where(x => x.Referencia.Length is > 0 and <= 120 && x.Descripcion.Length > 0)
            .Where(x => conocidas.Contains(x.Referencia) || (x.Referencia.Length >= 4 && textoFuentes.Contains(x.Referencia, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public static string DescribirParaModelo(InvestigacionEnCurso investigacion)
    {
        var sb = new StringBuilder();
        sb.AppendLine("HERRAMIENTAS DE SOLO LECTURA EJECUTADAS AUTOMÁTICAMENTE (no las repitas con los mismos parámetros):");
        if (investigacion.Pasos.Count == 0) sb.AppendLine("- Ninguna.");
        foreach (var paso in investigacion.Pasos)
            sb.AppendLine($"- {paso.HerramientaCodigo} {paso.ParametrosJson}: {investigacion.Respuestas.GetValueOrDefault($"{paso.HerramientaCodigo}|{paso.ParametrosJson}", "{}")}");
        return sb.ToString();
    }

    private static string SerializarParaModelo(string codigo, AgenteTIHerramientaResultado resultado, out int ocultados)
    {
        // Se agregan filas mientras quepan: el modelo recibe datos completos por fila, nunca fragmentos cortados a la mitad.
        var filas = new JsonArray();
        var truncado = resultado.Truncado;
        foreach (var fila in resultado.Filas)
        {
            var nodo = JsonSerializer.SerializeToNode(fila);
            filas.Add(nodo);
            if (filas.ToJsonString().Length > MaximoCaracteresResultado)
            {
                filas.RemoveAt(filas.Count - 1);
                truncado = true;
                break;
            }
        }
        var json = new JsonObject { ["herramienta"] = codigo, ["filas"] = resultado.Filas.Count, ["truncado"] = truncado, ["datos"] = filas }.ToJsonString();
        return RedactorDatosSensibles.RedactarParaIA(json, out ocultados);
    }

    private static string Error(string mensaje) => Error(mensaje, out _);
    private static string Error(string mensaje, out int ocultados)
    {
        ocultados = 0;
        return new JsonObject { ["error"] = string.IsNullOrWhiteSpace(mensaje) ? "La herramienta no devolvió resultados." : mensaje }.ToJsonString();
    }

    private static string Resumir(List<Dictionary<string, object?>> filas)
    {
        if (filas.Count == 0) return "Sin registros.";
        var sb = new StringBuilder($"{filas.Count} registro(s). ");
        foreach (var fila in filas.Take(3))
        {
            sb.Append('[');
            sb.Append(string.Join("; ", fila.Where(x => x.Value is not null && x.Value.ToString()!.Length > 0).Take(6)
                .Select(x => $"{x.Key}={Limitar(Convert.ToString(x.Value, CultureInfo.InvariantCulture), 80)}")));
            sb.Append("] ");
        }
        return RedactorDatosSensibles.RedactarSecretos(Limitar(sb.ToString(), 600));
    }

    /// <summary>
    /// Valida los argumentos del modelo contra el esquema del catálogo: solo claves declaradas, todas las requeridas,
    /// tipos string/integer y sus límites. Devuelve un JSON normalizado con las claves en el orden del esquema.
    /// </summary>
    public static bool ValidarParametros(string esquemaJson, string argumentosJson, out string normalizados, out string motivo)
    {
        normalizados = "{}";
        motivo = string.Empty;
        JsonObject? esquema, argumentos;
        try
        {
            esquema = JsonNode.Parse(esquemaJson) as JsonObject;
            argumentos = JsonNode.Parse(string.IsNullOrWhiteSpace(argumentosJson) ? "{}" : argumentosJson) as JsonObject;
        }
        catch (JsonException)
        {
            motivo = "Los parámetros de la herramienta no son JSON válido.";
            return false;
        }
        if (esquema is null || argumentos is null)
        {
            motivo = "Los parámetros de la herramienta deben ser un objeto JSON.";
            return false;
        }

        var propiedades = esquema["properties"] as JsonObject ?? new JsonObject();
        var requeridos = (esquema["required"] as JsonArray)?.Select(x => x?.GetValue<string>()).OfType<string>().ToHashSet() ?? [];
        var desconocida = argumentos.Select(x => x.Key).FirstOrDefault(x => !propiedades.ContainsKey(x));
        if (desconocida is not null)
        {
            motivo = $"El parámetro \"{desconocida}\" no existe en la herramienta.";
            return false;
        }

        var salida = new JsonObject();
        foreach (var (nombre, definicionNodo) in propiedades)
        {
            var definicion = definicionNodo as JsonObject ?? new JsonObject();
            if (!argumentos.TryGetPropertyValue(nombre, out var valor) || valor is null)
            {
                if (requeridos.Contains(nombre))
                {
                    motivo = $"Falta el parámetro obligatorio \"{nombre}\".";
                    return false;
                }
                continue;
            }

            var tipo = definicion["type"]?.GetValue<string>();
            if (tipo == "string")
            {
                if (valor.GetValueKind() != JsonValueKind.String)
                {
                    motivo = $"El parámetro \"{nombre}\" debe ser texto.";
                    return false;
                }
                var texto = valor.GetValue<string>().Trim();
                var minimo = definicion["minLength"]?.GetValue<int>() ?? 0;
                var maximo = definicion["maxLength"]?.GetValue<int>() ?? 500;
                if (texto.Length < minimo || texto.Length > maximo)
                {
                    motivo = $"El parámetro \"{nombre}\" debe tener entre {minimo} y {maximo} caracteres.";
                    return false;
                }
                salida[nombre] = texto;
            }
            else if (tipo == "integer")
            {
                if (valor.GetValueKind() != JsonValueKind.Number || !valor.AsValue().TryGetValue<long>(out var numero))
                {
                    if (valor.GetValueKind() == JsonValueKind.Number && decimal.TryParse(valor.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var dec) && dec == decimal.Truncate(dec))
                        numero = (long)dec;
                    else
                    {
                        motivo = $"El parámetro \"{nombre}\" debe ser un número entero.";
                        return false;
                    }
                }
                var minimo = definicion["minimum"]?.GetValue<long>() ?? long.MinValue;
                var maximo = definicion["maximum"]?.GetValue<long>() ?? long.MaxValue;
                if (numero < minimo || numero > maximo)
                {
                    motivo = $"El parámetro \"{nombre}\" debe estar entre {minimo} y {maximo}.";
                    return false;
                }
                salida[nombre] = numero;
            }
            else
            {
                motivo = $"El tipo del parámetro \"{nombre}\" no está soportado.";
                return false;
            }
        }
        normalizados = salida.ToJsonString();
        return true;
    }

    /// <summary>Quita del esquema las palabras clave que el modo estricto del proveedor no admite; el servidor las sigue validando.</summary>
    private static string EsquemaParaModelo(string esquemaJson)
    {
        if (JsonNode.Parse(esquemaJson) is not JsonObject esquema) return "{\"type\":\"object\",\"properties\":{},\"required\":[],\"additionalProperties\":false}";
        if (esquema["properties"] is JsonObject propiedades)
            foreach (var (_, definicion) in propiedades)
                if (definicion is JsonObject objeto)
                {
                    objeto.Remove("minLength");
                    objeto.Remove("maxLength");
                }
        // En modo estricto todas las propiedades deben ser requeridas.
        esquema["required"] = new JsonArray((esquema["properties"] as JsonObject ?? new JsonObject()).Select(x => (JsonNode?)JsonValue.Create(x.Key)).ToArray());
        esquema["additionalProperties"] = false;
        return esquema.ToJsonString();
    }

    private static JsonObject EsquemaDiagnostico()
    {
        static JsonObject Cadena(string descripcion) => new() { ["type"] = "string", ["description"] = descripcion };
        static JsonArray Requeridos(params string[] nombres) => new(nombres.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray());

        return new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["required"] = Requeridos("diagnostico", "causaProbable", "solucionPropuesta", "confianza", "evidenciaSuficiente", "accionCodigo", "parametros", "hallazgos"),
            ["properties"] = new JsonObject
            {
                ["diagnostico"] = Cadena("Qué ocurre, explicado con la evidencia."),
                ["causaProbable"] = Cadena("Causa más probable; 'No determinada' si la evidencia no alcanza."),
                ["solucionPropuesta"] = Cadena("Pasos de solución para TI."),
                ["confianza"] = new JsonObject { ["type"] = "number", ["description"] = "Calidad del diagnóstico en porcentaje, de 0 a 100 (por ejemplo 85)." },
                ["evidenciaSuficiente"] = new JsonObject { ["type"] = "boolean", ["description"] = "true solo si la evidencia verificada sostiene la causa." },
                ["accionCodigo"] = new JsonObject { ["type"] = new JsonArray("string", "null"), ["description"] = "Código exacto de ACCIONES AUTORIZADAS o null." },
                ["parametros"] = new JsonObject
                {
                    ["type"] = "array",
                    ["description"] = "Parámetros de la acción, con valores tomados de la evidencia.",
                    ["items"] = new JsonObject
                    {
                        ["type"] = "object", ["additionalProperties"] = false, ["required"] = Requeridos("clave", "valor"),
                        ["properties"] = new JsonObject { ["clave"] = Cadena("Nombre del parámetro."), ["valor"] = Cadena("Valor del parámetro.") }
                    }
                },
                ["hallazgos"] = new JsonObject
                {
                    ["type"] = "array",
                    ["description"] = "Hechos que sostienen el diagnóstico, cada uno con la referencia exacta de su evidencia.",
                    ["items"] = new JsonObject
                    {
                        ["type"] = "object", ["additionalProperties"] = false, ["required"] = Requeridos("fuente", "referencia", "descripcion"),
                        ["properties"] = new JsonObject
                        {
                            ["fuente"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray(FuentesHallazgo.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()) },
                            ["referencia"] = Cadena("Código de herramienta, EVENTO-n, código KB, número de ticket o referencia de evidencia."),
                            ["descripcion"] = Cadena("Qué demuestra esa evidencia.")
                        }
                    }
                }
            }
        };
    }

    /// <summary>Algunos modelos expresan la confianza de 0 a 1 aunque se pida en porcentaje.</summary>
    public static decimal NormalizarConfianza(decimal valor) => Math.Clamp(valor is > 0m and <= 1m ? valor * 100m : valor, 0m, 100m);

    private static string Texto(JsonElement elemento, string propiedad) =>
        elemento.ValueKind == JsonValueKind.Object && elemento.TryGetProperty(propiedad, out var valor) && valor.ValueKind == JsonValueKind.String
            ? Limitar(valor.GetString(), 8000) : string.Empty;

    private static string Limitar(string? texto, int maximo)
    {
        var valor = texto?.Trim() ?? string.Empty;
        return valor.Length <= maximo ? valor : valor[..maximo];
    }
}

public sealed class InvestigacionEnCurso(string usuario, string area, long sesionNumero, List<AgenteTIHerramienta> herramientas)
{
    public string Usuario { get; } = usuario;
    public string Area { get; } = area;
    public long SesionNumero { get; } = sesionNumero;
    public List<AgenteTIHerramienta> Herramientas { get; } = herramientas;
    public List<AgenteTIPasoInvestigacion> Pasos { get; } = [];
    /// <summary>Respuesta entregada al modelo por herramienta y parámetros; evita repetir consultas idénticas.</summary>
    public Dictionary<string, string> Respuestas { get; } = new(StringComparer.Ordinal);
    public StringBuilder TextoResultados { get; } = new();
    public int DatosOcultados { get; set; }
}

public sealed class ResultadoAgente
{
    public string Diagnostico { get; set; } = string.Empty;
    public string CausaProbable { get; set; } = string.Empty;
    public string SolucionPropuesta { get; set; } = string.Empty;
    public decimal Confianza { get; set; }
    public bool EvidenciaSuficiente { get; set; }
    public string? AccionCodigo { get; set; }
    public string ParametrosJson { get; set; } = "{}";
    public List<AgenteTIHallazgo> Hallazgos { get; } = [];
}
