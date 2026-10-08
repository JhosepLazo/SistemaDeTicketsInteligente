/**
 * Archivo: InvestigadorAgenteTI.cs
 * Objetivo: Ejecutar la investigación de varios pasos del Agente de Ingeniería con herramientas diagnósticas de solo lectura.
 * Responsabilidad: Validar los parámetros que propone el modelo contra el esquema catalogado, ejecutar la herramienta, registrar cada paso
 *   como evidencia del servidor y devolver al modelo un resultado acotado y sin datos sensibles.
 * Dependencias: BaseDatos (Usp_TI_Agente_Herramientas, Usp_TI_Agente_RegistrarHerramienta y las herramientas dbo.Usp_TI_AgenteDiag_*),
 *   ConocimientoSemanticoBLL, ReplicaTecnicaBLL y AnalizadorGrabacionClient (herramientas internas),
 *   OpenAIAsistenteClient (bucle con function calling) e IConfiguration.
 * Flujo: herramientas automáticas -> modelo decide herramientas adicionales -> resultado estructurado (JSON Schema estricto).
 * Consideraciones: El modelo elige herramienta y parámetros, nunca el procedimiento ni el SQL. Cada herramienta corre en una transacción
 *   que siempre se revierte, con la identidad SQL de lectura del agente. Los resultados son datos, nunca instrucciones para el modelo.
 *   Todo pedido rechazado (herramienta inexistente, parámetros fuera del esquema o acción fuera del catálogo) queda en Rechazos para
 *   registrarlo en la traza: es una señal de revisión, por ejemplo ante una instrucción incrustada en los datos.
 */

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SistemaTicketsInteligente.Api.BLL.Agente;

public sealed class InvestigadorAgenteTI(BaseDatos baseDatos, ConocimientoSemanticoBLL conocimiento, ReplicaTecnicaBLL replica,
    AnalizadorGrabacionClient analizador, OpenAIAsistenteClient openAI, IConfiguration configuration, ILogger<InvestigadorAgenteTI> logger)
{
    public const string OrigenAutomatico = "AUTOMATICA";
    public const string OrigenModelo = "MODELO";
    public const string HerramientaSemantica = "DIAG_CONOCIMIENTO_SEMANTICO";
    public const string HerramientaGrabacion = "DIAG_ANALIZAR_GRABACION";
    private const int MaximoCaracteresResultado = 9000;
    // Sin escapar tildes ni eñes: el modelo lee "ó" y no "\u00F3" (menos tokens y búsquedas literales correctas).
    private static readonly JsonSerializerOptions JsonLegible = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string[] FuentesHallazgo =
        ["LIVE", "GRABACION", "TICKET", "TELEMETRIA", "HERRAMIENTA", "CODIGO_FUENTE", "BASE_DATOS", "AUDITORIA", "BASE_CONOCIMIENTO", "DOCUMENTO", "CODIGO_ESTATICO", "OBSERVACION_USUARIO"];
    // Solo procedimientos de diagnóstico con el prefijo contractual; el catálogo nunca puede apuntar a otro procedimiento.
    private static readonly Regex ProcedimientoDiagnostico = new(@"^dbo\.Usp_TI_AgenteDiag_[A-Za-z0-9_]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public int MaximoPasosModelo => Math.Clamp(configuration.GetValue<int?>("AgenteTI:MaxPasosHerramientas") ?? 8, 0, 15);

    /// <summary>Hay sistemas con código o base de datos configurados: el agente puede replicar técnicamente el proceso.</summary>
    public bool ReplicaDisponible => replica.HayCodigo || replica.HayBaseDatos;

    public async Task<InvestigacionEnCurso> IniciarAsync(string usuario, string area, AgenteTIContextoInvestigacion contexto, CancellationToken ct)
    {
        List<AgenteTIHerramienta> herramientas;
        try { herramientas = await ListarHerramientasAsync(usuario, area, ct); }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException)
        {
            // Sin el script 30 instalado, la investigación continúa con el contexto clásico.
            logger.LogWarning(ex, "No se pudo obtener el catálogo de herramientas diagnósticas.");
            herramientas = [];
        }

        var tieneTicket = !string.IsNullOrWhiteSpace(contexto.Sesion.IncidenciaNumero);
        var grabaciones = Grabaciones(contexto);
        // Las herramientas internas solo se ofrecen si su recurso existe (embeddings, grabación, código o base configurados).
        var disponibles = herramientas
            .Where(x => tieneTicket || !x.RequiereTicket)
            .Where(x => x.Tipo != "INTERNA" || HerramientaInternaDisponible(x.HerramientaCodigo, grabaciones.Count > 0))
            .ToList();
        var problema = string.Join(". ", new[]
        {
            contexto.Sesion.DescripcionInicial, contexto.Sesion.ErrorObservado, contexto.Ticket.Titulo, contexto.Ticket.MensajeError
        }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct());
        return new InvestigacionEnCurso(usuario, area, contexto.Sesion.SesionNumero, disponibles)
        {
            TextoProblema = Limitar(problema, 1500),
            Excluir = string.IsNullOrWhiteSpace(contexto.Sesion.IncidenciaNumero) ? [] : [contexto.Sesion.IncidenciaNumero],
            TextoError = Limitar(string.IsNullOrWhiteSpace(contexto.Sesion.ErrorObservado) ? contexto.Ticket.MensajeError : contexto.Sesion.ErrorObservado, 200),
            Grabaciones = grabaciones,
            SistemasTicket = replica.SistemasDeLinea(contexto.Ticket.Linea)
        };
    }

    private Task<List<AgenteTIHerramienta>> ListarHerramientasAsync(string usuario, string area, CancellationToken ct) =>
        baseDatos.LeerAsync("dbo.Usp_TI_Agente_Herramientas", p =>
        {
            p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;
            p.Add("@cArea", SqlDbType.Char, 3).Value = area;
        }, lector => lector.ListaAsync(f => new AgenteTIHerramienta
        {
            HerramientaCodigo = f.Texto("HerramientaCodigo"), Nombre = f.Texto("Nombre"), Descripcion = f.Texto("Descripcion"),
            Procedimiento = f.Texto("Procedimiento"), ParametrosEsquemaJson = f.Texto("ParametrosEsquemaJson"),
            Automatica = f.Booleano("Automatica"), RequiereTicket = f.Booleano("RequiereTicket"), MaximoFilas = f.Entero("MaximoFilas"),
            // Antes del script 31 el catálogo no tiene la columna Tipo: todas son procedimientos.
            Tipo = f.TieneColumna("Tipo") ? f.Texto("Tipo") : "SP"
        }, ct), ct);

    private bool HerramientaInternaDisponible(string codigo, bool hayGrabacion) => codigo switch
    {
        HerramientaSemantica => conocimiento.Disponible,
        HerramientaGrabacion => hayGrabacion && analizador.Disponible,
        "DIAG_CODIGO_BUSCAR" or "DIAG_CODIGO_LEER" => replica.HayCodigo,
        "DIAG_BD_BUSCAR" or "DIAG_BD_DEFINICION" or "DIAG_BD_ESTRUCTURA" => replica.HayBaseDatos,
        "DIAG_BD_CONSULTAR" => replica.HayConsultas,
        _ => false
    };

    private static List<GrabacionEvidencia> Grabaciones(AgenteTIContextoInvestigacion contexto)
    {
        var lista = new List<GrabacionEvidencia>();
        foreach (var evento in contexto.Eventos.Where(x => x.OrigenServidor && x.Tipo == "GRABACION_PANTALLA").OrderBy(x => x.Secuencia))
        {
            try
            {
                using var documento = JsonDocument.Parse(evento.DatosJson);
                var ruta = documento.RootElement.TryGetProperty("ruta", out var r) ? r.GetString() : null;
                var mime = documento.RootElement.TryGetProperty("tipoMime", out var m) ? m.GetString() : null;
                if (!string.IsNullOrWhiteSpace(ruta)) lista.Add(new GrabacionEvidencia(evento.Secuencia, ruta, string.IsNullOrWhiteSpace(mime) ? "video/webm" : mime, evento.Fuente == "LIVE_USUARIO"));
            }
            catch (JsonException) { /* un evento ilegible no impide la investigación */ }
        }
        return lista;
    }

    /// <summary>Ejecuta las herramientas marcadas como automáticas con sus parámetros por defecto, antes de consultar al modelo.</summary>
    public async Task EjecutarAutomaticasAsync(InvestigacionEnCurso investigacion, CancellationToken ct)
    {
        // Primero la grabación (puede revelar el texto exacto del error), al final la búsqueda en código y base con ese texto.
        foreach (var herramienta in investigacion.Herramientas.Where(x => x.Automatica).OrderBy(x => PrioridadAutomatica(x.HerramientaCodigo)))
        {
            if (herramienta.HerramientaCodigo is "DIAG_CODIGO_BUSCAR" or "DIAG_BD_BUSCAR")
            {
                // Solo en los sistemas asociados a la línea del ticket y si hay un mensaje de error que buscar.
                if (string.IsNullOrWhiteSpace(investigacion.TextoError)) continue;
                var validos = replica.CodigosPara(herramienta.HerramientaCodigo);
                foreach (var sistema in investigacion.SistemasTicket.Where(x => validos.Contains(x, StringComparer.OrdinalIgnoreCase)).Take(2))
                {
                    if (Desactivada(investigacion, herramienta.HerramientaCodigo)) break;
                    await EjecutarAsync(investigacion, herramienta, new JsonObject { ["sistema"] = sistema, ["texto"] = investigacion.TextoError }.ToJsonString(), OrigenAutomatico, ct);
                }
                continue;
            }
            await EjecutarAsync(investigacion, herramienta, "{}", OrigenAutomatico, ct);
        }
    }

    // Dos tiempos agotados (o cuatro errores seguidos) desactivan la herramienta en la investigación: con la base lenta,
    // el modelo repetía la misma búsqueda con variantes y gastaba todo el tiempo disponible.
    private const int LimiteFallosHerramienta = 4;
    private static readonly TimeSpan MargenRespuestaFinal = TimeSpan.FromSeconds(90);

    private static bool Desactivada(InvestigacionEnCurso investigacion, string codigo) =>
        investigacion.Fallos.GetValueOrDefault(codigo) >= LimiteFallosHerramienta;

    private static int PrioridadAutomatica(string codigo) => codigo switch
    {
        HerramientaGrabacion => 0,
        "DIAG_CODIGO_BUSCAR" or "DIAG_BD_BUSCAR" => 3,
        HerramientaSemantica => 2,
        _ => 1
    };

    /// <summary>
    /// Bucle agéntico: el modelo recibe el contexto y los pasos automáticos y puede pedir más herramientas.
    /// Devuelve null si el proveedor no respondió; el llamador cae a la ruta de una sola llamada.
    /// </summary>
    public async Task<ResultadoAgente?> DiagnosticarAsync(InvestigacionEnCurso investigacion, string instrucciones, string entrada, IReadOnlyCollection<string> accionesPermitidas, CancellationToken ct)
    {
        var maximo = MaximoPasosModelo;
        var definiciones = investigacion.Herramientas
            .Where(x => !Desactivada(investigacion, x.HerramientaCodigo))
            .Select(x => new HerramientaIA(x.HerramientaCodigo, x.Descripcion, EsquemaParaModelo(x.ParametrosEsquemaJson, replica.CodigosPara(x.HerramientaCodigo))))
            .ToList();

        // Al vencer el tiempo configurado ya no se piden herramientas y el modelo responde con lo reunido; la respuesta final tiene
        // un margen propio, para no perder toda la investigación por una última herramienta o un modelo lento.
        var tiempo = TimeSpan.FromSeconds(Math.Clamp(configuration.GetValue<int?>("AgenteTI:TiempoMaximoInvestigacionSegundos") ?? 240, 30, 600));
        using var cierre = new CancellationTokenSource(tiempo);
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(tiempo + MargenRespuestaFinal);

        string? salida;
        try
        {
            salida = await openAI.GenerarConHerramientasAsync(instrucciones, entrada, definiciones,
                (nombre, argumentos, token) => EjecutarSolicitudModeloAsync(investigacion, nombre, argumentos, token),
                "diagnostico_agente", EsquemaDiagnostico(), maximo, limite.Token, cierre.Token);
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
            if (!string.IsNullOrWhiteSpace(accion) && !accionesPermitidas.Contains(accion, StringComparer.OrdinalIgnoreCase))
                investigacion.Rechazos.Add($"El modelo propuso la acción {Limitar(accion, 60)}, que no pertenece a las acciones autorizadas.");
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
            resultado.AlternativasDescartadas.AddRange(Alternativas(raiz));
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
        if (herramienta is null)
        {
            investigacion.Rechazos.Add($"El modelo pidió la herramienta {Limitar(nombre, 60)}, que no está disponible en esta investigación.");
            return Error($"La herramienta {nombre} no está disponible en esta investigación.");
        }
        if (Desactivada(investigacion, herramienta.HerramientaCodigo))
            return Error($"La herramienta {nombre} quedó desactivada en esta investigación porque falló varias veces seguidas. No la vuelvas a pedir: usa otra herramienta o responde con la evidencia reunida.");
        if (!ValidarParametros(herramienta.ParametrosEsquemaJson, argumentos, out var normalizados, out var motivo))
        {
            investigacion.Rechazos.Add($"Parámetros rechazados para {herramienta.HerramientaCodigo}: {motivo}");
            return Error(motivo);
        }

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
        var tiempoAgotado = false;
        try
        {
            resultado = herramienta.Tipo == "INTERNA"
                ? await EjecutarInternaAsync(investigacion, herramienta, parametrosJson, ct)
                : await EjecutarProcedimientoAsync(investigacion, herramienta, parametrosJson, ct);
            paso.Filas = resultado.Filas.Count;
            paso.Truncado = resultado.Truncado;
            paso.Resumen = Resumir(resultado.Filas);
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
        {
            paso.Error = Limitar(ex.Message, 500);
        }
        // Tiempo agotado: el servidor no va a responder distinto a una variante del mismo pedido, así que se le dice al modelo que no insista.
        catch (Exception ex) when (ex is TimeoutException || (ex is OperationCanceledException && !ct.IsCancellationRequested))
        {
            logger.LogWarning("La herramienta {Herramienta} no respondió a tiempo en la sesión {Sesion}: {Motivo}", herramienta.HerramientaCodigo, investigacion.SesionNumero, ex.Message);
            tiempoAgotado = true;
            paso.Error = Limitar((ex is TimeoutException ? ex.Message : "La herramienta superó su tiempo máximo.")
                + " No la repitas con variantes del mismo texto; continúa con otras herramientas o con la evidencia reunida.", 500);
        }
        // Un paso que falla queda registrado con su motivo y la investigación sigue con el resto de la evidencia.
        catch (Exception ex) when (ex is SqlException or HttpRequestException or IOException or JsonException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "La herramienta {Herramienta} falló en la sesión {Sesion}.", herramienta.HerramientaCodigo, investigacion.SesionNumero);
            paso.Error = "La herramienta no respondió correctamente; se continúa con la evidencia disponible.";
        }
        paso.DuracionMs = cronometro.ElapsedMilliseconds;
        investigacion.Pasos.Add(paso);
        investigacion.Fallos[herramienta.HerramientaCodigo] = string.IsNullOrEmpty(paso.Error)
            ? 0 : investigacion.Fallos.GetValueOrDefault(herramienta.HerramientaCodigo) + (tiempoAgotado ? 2 : 1);

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
                ["muestra"] = resultado is null ? null : RedactorDatosSensibles.RedactarSecretos(Limitar(JsonSerializer.Serialize(resultado.Filas.Take(15), JsonLegible), 8000))
            };
            var contenido = $"{herramienta.Nombre} ({herramienta.HerramientaCodigo}) · {(origen == OrigenModelo ? "solicitada por el agente" : "automática")} · " +
                (string.IsNullOrEmpty(paso.Error) ? $"{paso.Filas} fila(s){(paso.Truncado ? " (truncado)" : string.Empty)}" : $"error: {paso.Error}");
            await baseDatos.EjecutarAsync("dbo.Usp_TI_Agente_RegistrarHerramienta", p =>
            {
                p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = investigacion.Usuario;
                p.Add("@nSesionNumero", SqlDbType.BigInt).Value = investigacion.SesionNumero;
                p.Add("@cHerramientaCodigo", SqlDbType.VarChar, 40).Value = herramienta.HerramientaCodigo;
                p.Add("@cOrigen", SqlDbType.VarChar, 20).Value = origen;
                p.Add("@cContenido", SqlDbType.NVarChar, 2000).Value = Limitar(contenido, 2000);
                p.Add("@cDatosJson", SqlDbType.NVarChar, -1).Value = datos.ToJsonString(JsonLegible);
            }, ct);
        }
        catch (Exception ex) when (ex is InvalidOperationException or SqlException)
        {
            logger.LogWarning(ex, "No se pudo registrar el paso {Herramienta} de la sesión {Sesion}.", herramienta.HerramientaCodigo, investigacion.SesionNumero);
        }
        return paso;
    }

    /// <summary>
    /// Ejecuta una herramienta diagnóstica del catálogo. Corre dentro de una transacción que siempre se revierte:
    /// aunque un procedimiento mal escrito intentara modificar datos, nada queda persistido.
    /// </summary>
    private async Task<AgenteTIHerramientaResultado> EjecutarProcedimientoAsync(InvestigacionEnCurso investigacion, AgenteTIHerramienta herramienta, string parametrosJson, CancellationToken ct)
    {
        if (!ProcedimientoDiagnostico.IsMatch(herramienta.Procedimiento)) throw new InvalidOperationException("La herramienta no pertenece a la lista permitida de diagnóstico.");
        var maximoFilas = Math.Clamp(herramienta.MaximoFilas, 1, 200);

        await using var conexion = baseDatos.CrearConexionAgenteLectura();
        await conexion.OpenAsync(ct);
        await using var transaccion = (SqlTransaction)await conexion.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await using var comando = BaseDatos.Comando(conexion, transaccion, herramienta.Procedimiento, p =>
        {
            p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = investigacion.Usuario;
            p.Add("@cArea", SqlDbType.Char, 3).Value = investigacion.Area;
            p.Add("@nSesionNumero", SqlDbType.BigInt).Value = investigacion.SesionNumero;
            p.Add("@cParametrosJson", SqlDbType.NVarChar, -1).Value = string.IsNullOrWhiteSpace(parametrosJson) ? "{}" : parametrosJson;
        });
        comando.CommandTimeout = 20;
        try
        {
            var resultado = new AgenteTIHerramientaResultado();
            await using (var lector = await comando.ExecuteReaderAsync(ct))
            {
                while (await lector.ReadAsync(ct))
                {
                    if (resultado.Filas.Count >= maximoFilas)
                    {
                        resultado.Truncado = true;
                        break;
                    }
                    var fila = new Dictionary<string, object?>(lector.FieldCount, StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < lector.FieldCount; i++) fila[lector.GetName(i)] = ValorHerramienta(lector.GetValue(i));
                    resultado.Filas.Add(fila);
                }
            }
            return resultado;
        }
        catch (SqlException ex) when (BaseDatos.EsErrorDeNegocio(ex))
        {
            throw new InvalidOperationException(ex.Message, ex);
        }
        finally
        {
            try { await transaccion.RollbackAsync(CancellationToken.None); } catch (Exception) { /* La conexión puede haberse cerrado. */ }
        }
    }

    // Valores legibles para el modelo y el expediente: fechas con formato fijo y sin binarios.
    private static object? ValorHerramienta(object valor) => valor switch
    {
        DBNull => null,
        DateTime fecha => fecha.ToString("yyyy-MM-dd HH:mm:ss"),
        DateTimeOffset fecha => fecha.ToString("yyyy-MM-dd HH:mm:ss zzz"),
        Guid guid => guid.ToString(),
        string texto => texto.Trim(),
        byte[] => "[binario omitido]",
        _ => valor
    };

    // Herramientas que no son procedimientos: hoy, la búsqueda semántica sobre conocimiento y casos resueltos (solo lectura).
    private async Task<AgenteTIHerramientaResultado> EjecutarInternaAsync(InvestigacionEnCurso investigacion, AgenteTIHerramienta herramienta, string parametrosJson, CancellationToken ct)
    {
        using var parametros = JsonDocument.Parse(parametrosJson);
        string Texto(string nombre) => parametros.RootElement.TryGetProperty(nombre, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()!.Trim() : string.Empty;
        int Entero(string nombre, int defecto) => parametros.RootElement.TryGetProperty(nombre, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : defecto;

        switch (herramienta.HerramientaCodigo)
        {
            case HerramientaGrabacion: return await AnalizarGrabacionAsync(investigacion, ct);
            case "DIAG_CODIGO_BUSCAR": return await Task.Run(() => replica.BuscarCodigo(Texto("sistema"), Texto("texto"), herramienta.MaximoFilas), ct);
            case "DIAG_CODIGO_LEER": return await Task.Run(() => replica.LeerCodigo(Texto("sistema"), Texto("archivo"), Math.Max(1, Entero("desde", 1))), ct);
            case "DIAG_BD_BUSCAR": return await replica.BuscarEnBaseDatosAsync(Texto("sistema"), Texto("texto"), herramienta.MaximoFilas, ct);
            case "DIAG_BD_DEFINICION": return await replica.DefinicionAsync(Texto("sistema"), Texto("objeto"), Math.Max(1, Entero("desde", 1)), ct);
            case "DIAG_BD_ESTRUCTURA": return await replica.EstructuraAsync(Texto("sistema"), Texto("tabla"), ct);
            case "DIAG_BD_CONSULTAR": return await replica.ConsultarAsync(Texto("sistema"), Texto("sql"), herramienta.MaximoFilas, ct);
            case HerramientaSemantica: break;
            default: throw new InvalidOperationException("La herramienta interna no está implementada en este servidor.");
        }
        if (!conocimiento.Disponible) throw new InvalidOperationException("La búsqueda semántica no está disponible con el proveedor de IA configurado.");

        var consulta = Texto("consulta");
        if (consulta.Length == 0) consulta = investigacion.TextoProblema;
        if (consulta.Trim().Length < 5) throw new InvalidOperationException("No hay una descripción del problema suficiente para buscar casos similares.");

        var similares = await conocimiento.BuscarAsync(consulta, soloUsuario: false, herramienta.MaximoFilas, investigacion.Excluir, ct);
        return new AgenteTIHerramientaResultado
        {
            Filas = similares.Select(x => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Fuente"] = x.Origen == "K" ? "Guía de conocimiento" : "Ticket resuelto",
                ["Codigo"] = x.Codigo, ["Titulo"] = x.Titulo, ["Similitud"] = $"{x.Similitud:0.#}%", ["Contenido"] = x.Extracto
            }).ToList()
        };
    }

    // El agente "mira" la grabación: pasos con su segundo, datos usados y el error exacto, que además alimenta la búsqueda en código y base.
    private async Task<AgenteTIHerramientaResultado> AnalizarGrabacionAsync(InvestigacionEnCurso investigacion, CancellationToken ct)
    {
        var grabacion = investigacion.Grabaciones.LastOrDefault(x => x.UsuarioFinal) ?? investigacion.Grabaciones.LastOrDefault()
            ?? throw new InvalidOperationException("La investigación no tiene grabaciones de pantalla.");
        var analisis = await analizador.AnalizarAsync(AlmacenGrabaciones.Resolver(grabacion.Ruta), grabacion.TipoMime, investigacion.TextoProblema, ct);
        static string Valor(JsonNode? nodo) => nodo is null ? string.Empty : nodo.GetValueKind() == JsonValueKind.String ? nodo.GetValue<string>().Trim() : nodo.ToJsonString();
        Dictionary<string, object?> Fila(string tipo, string? segundo, string pantalla, string accion, string detalle) => new(StringComparer.OrdinalIgnoreCase)
        {
            ["Tipo"] = tipo, ["Segundo"] = segundo, ["Pantalla"] = Limitar(pantalla, 120), ["Accion"] = Limitar(accion, 300), ["Detalle"] = Limitar(detalle, 400)
        };

        var resultado = new AgenteTIHerramientaResultado();
        resultado.Filas.Add(Fila("RESUMEN", null, Valor(analisis["sistema"]), string.Empty, Valor(analisis["resumen"])));
        var error = Valor(analisis["errorExacto"]);
        if (error.Length > 0)
        {
            resultado.Filas.Add(Fila("ERROR", null, string.Empty, string.Empty, error));
            if (string.IsNullOrWhiteSpace(investigacion.TextoError)) investigacion.TextoError = Limitar(error, 200);
        }
        if (analisis["pasos"] is JsonArray pasos)
            foreach (var paso in pasos.Take(25))
                resultado.Filas.Add(Fila("PASO", Valor(paso?["segundo"]), Valor(paso?["pantalla"]), Valor(paso?["accion"]), Valor(paso?["datos"])));
        if (analisis["datosClave"] is JsonArray datos)
            foreach (var dato in datos.Take(10))
                resultado.Filas.Add(Fila("DATO", null, Valor(dato?["dato"]), string.Empty, Valor(dato?["valor"])));
        return resultado;
    }

    /// <summary>Convierte los pasos exitosos en evidencia verificable del expediente.</summary>
    public static IEnumerable<AgenteTIEvidencia> Evidencias(InvestigacionEnCurso investigacion) => investigacion.Pasos
        .Where(x => string.IsNullOrEmpty(x.Error) && x.Filas > 0)
        .Select(x => new AgenteTIEvidencia { TipoFuente = "HERRAMIENTA", Referencia = x.HerramientaCodigo, Descripcion = Limitar($"{x.Nombre}: {x.Resumen}", 900) });

    /// <summary>Descarta hallazgos que citan referencias inexistentes: el agente no puede apoyarse en evidencia inventada.</summary>
    public static List<AgenteTIHallazgo> HallazgosVerificados(IEnumerable<AgenteTIHallazgo> hallazgos, IEnumerable<string> referenciasConocidas, string textoFuentes)
    {
        var conocidas = new HashSet<string>(referenciasConocidas.Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.OrdinalIgnoreCase);
        static string SinLinea(string referencia) => Regex.Replace(referencia.Trim(), @"[:#]\s*(?:l[ií]nea\s*)?\d+$", string.Empty, RegexOptions.IgnoreCase);
        return hallazgos
            .Where(x => x.Referencia.Length is > 0 and <= 200 && x.Descripcion.Length > 0)
            .Where(x => conocidas.Contains(x.Referencia)
                || (x.Referencia.Length >= 4 && textoFuentes.Contains(x.Referencia, StringComparison.OrdinalIgnoreCase))
                // "archivo.cs:120" u "objeto:30": basta que el archivo u objeto aparezca en la evidencia.
                || (SinLinea(x.Referencia).Length >= 4 && textoFuentes.Contains(SinLinea(x.Referencia), StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public string DescribirSistemas(InvestigacionEnCurso investigacion)
    {
        var disponibles = replica.Disponibles;
        if (disponibles.Count == 0) return string.Empty;
        var sb = new StringBuilder();
        sb.AppendLine("SISTEMAS INVESTIGABLES (código fuente y base de datos de solo lectura):");
        foreach (var sistema in disponibles)
            sb.AppendLine($"- {sistema.Codigo}: {sistema.Nombre} · código fuente: {(sistema.TieneCodigo ? "sí" : "no")} · base de datos: {(sistema.TieneBaseDatos ? "sí" : "no")} · consultas SELECT: {(sistema.PermiteConsultas ? "sí" : "no")}");
        sb.AppendLine(investigacion.SistemasTicket.Count > 0
            ? $"La línea del ticket corresponde a: {string.Join(", ", investigacion.SistemasTicket)}."
            : "La línea del ticket no está asociada a un sistema investigable; usa uno solo si la evidencia indica que el problema ocurre en él.");
        return sb.ToString();
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
            if (filas.ToJsonString(JsonLegible).Length > MaximoCaracteresResultado)
            {
                filas.RemoveAt(filas.Count - 1);
                truncado = true;
                break;
            }
        }
        var json = new JsonObject { ["herramienta"] = codigo, ["filas"] = resultado.Filas.Count, ["truncado"] = truncado, ["datos"] = filas };
        // Una búsqueda vacía también es un hallazgo; sin esta nota el modelo probaba variantes del mismo mensaje una y otra vez.
        if (resultado.Filas.Count == 0 && NotaSinResultados(codigo) is { } nota) json["nota"] = nota;
        return RedactorDatosSensibles.RedactarParaIA(json.ToJsonString(JsonLegible), out ocultados);
    }

    private static string? NotaSinResultados(string codigo) => codigo switch
    {
        "DIAG_BD_BUSCAR" => "El texto, completo y sin sus datos variables, no aparece en procedimientos, vistas, funciones ni triggers de esa base: "
            + "probablemente lo genera la aplicación u otro sistema. Prueba DIAG_CODIGO_BUSCAR o concluye con la evidencia; no repitas con variantes del mismo mensaje.",
        "DIAG_CODIGO_BUSCAR" => "El texto, completo y sin sus datos variables, no aparece en el código fuente de ese sistema: "
            + "probablemente lo genera la base de datos u otro sistema. Prueba DIAG_BD_BUSCAR o concluye con la evidencia; no repitas con variantes del mismo mensaje.",
        _ => null
    };

    private static string Error(string mensaje) => Error(mensaje, out _);
    private static string Error(string mensaje, out int ocultados)
    {
        ocultados = 0;
        return new JsonObject { ["error"] = string.IsNullOrWhiteSpace(mensaje) ? "La herramienta no devolvió resultados." : mensaje }.ToJsonString(JsonLegible);
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

    /// <summary>Quita del esquema las palabras clave que el modo estricto del proveedor no admite; el servidor las sigue validando.
    /// El parámetro "sistema" se limita a los sistemas configurados para esa herramienta.</summary>
    private static string EsquemaParaModelo(string esquemaJson, IReadOnlyList<string> sistemas)
    {
        if (JsonNode.Parse(esquemaJson) is not JsonObject esquema) return "{\"type\":\"object\",\"properties\":{},\"required\":[],\"additionalProperties\":false}";
        if (esquema["properties"] is JsonObject propiedades)
            foreach (var (nombre, definicion) in propiedades)
                if (definicion is JsonObject objeto)
                {
                    objeto.Remove("minLength");
                    objeto.Remove("maxLength");
                    if (nombre == "sistema" && sistemas.Count > 0) objeto["enum"] = new JsonArray(sistemas.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray());
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
            ["required"] = Requeridos("diagnostico", "causaProbable", "solucionPropuesta", "confianza", "evidenciaSuficiente", "accionCodigo", "parametros", "hallazgos", "alternativasDescartadas"),
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
                },
                ["alternativasDescartadas"] = new JsonObject
                {
                    ["type"] = "array",
                    ["description"] = "Otras causas que consideraste y por qué la evidencia las descarta.",
                    ["items"] = new JsonObject
                    {
                        ["type"] = "object", ["additionalProperties"] = false, ["required"] = Requeridos("causa", "motivo"),
                        ["properties"] = new JsonObject { ["causa"] = Cadena("Causa considerada."), ["motivo"] = Cadena("Evidencia que la descarta.") }
                    }
                }
            }
        };
    }

    /// <summary>Lee las causas descartadas de la respuesta estructurada (diagnóstico con herramientas o de una sola llamada).</summary>
    public static IEnumerable<AgenteTIAlternativa> Alternativas(JsonElement raiz)
    {
        if (!raiz.TryGetProperty("alternativasDescartadas", out var lista) || lista.ValueKind != JsonValueKind.Array) yield break;
        foreach (var alternativa in lista.EnumerateArray().Take(6))
        {
            var causa = Limitar(Texto(alternativa, "causa"), 400);
            if (causa.Length > 0) yield return new AgenteTIAlternativa { Causa = causa, Motivo = Limitar(Texto(alternativa, "motivo"), 600) };
        }
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
    /// <summary>Fallos seguidos por herramienta (un tiempo agotado cuenta doble); al llegar al límite la herramienta se desactiva.</summary>
    public Dictionary<string, int> Fallos { get; } = new(StringComparer.Ordinal);
    public StringBuilder TextoResultados { get; } = new();
    public int DatosOcultados { get; set; }
    /// <summary>Descripción del problema para la búsqueda semántica automática.</summary>
    public string TextoProblema { get; init; } = string.Empty;
    /// <summary>Códigos que no deben aparecer como casos similares (el propio ticket).</summary>
    public IReadOnlyCollection<string> Excluir { get; init; } = [];
    /// <summary>Mensaje de error exacto; si no se conocía, lo aporta el análisis de la grabación.</summary>
    public string TextoError { get; set; } = string.Empty;
    public IReadOnlyList<GrabacionEvidencia> Grabaciones { get; init; } = [];
    /// <summary>Sistemas investigables asociados a la línea del ticket.</summary>
    public IReadOnlyList<string> SistemasTicket { get; init; } = [];
    /// <summary>Pedidos del modelo que el servidor rechazó (herramientas, parámetros o acciones fuera del catálogo).</summary>
    public List<string> Rechazos { get; } = [];
}

public sealed record GrabacionEvidencia(int EventoSecuencia, string Ruta, string TipoMime, bool UsuarioFinal);

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
    public List<AgenteTIAlternativa> AlternativasDescartadas { get; } = [];
}
