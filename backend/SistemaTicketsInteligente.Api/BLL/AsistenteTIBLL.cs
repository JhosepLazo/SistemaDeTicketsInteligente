/**
 * Archivo: AsistenteTIBLL.cs
 * Objetivo: Orquestar el Asistente TI y el Agente de Ingeniería Autónomo.
 * Responsabilidad: Coordinar observación Live, contexto autorizado, investigación, diagnóstico, expediente Markdown y ejecución human-in-the-loop.
 * Dependencias: AsistenteTIDAO, ConfiguracionTIBLL, OpenAIAsistenteClient, GeminiLiveClient, Data Protection y MemoryCache.
 * Flujo: TI -> sesión/Live -> evidencia -> investigación -> informe -> decisión TI -> acción catalogada -> validación/auditoría.
 * Consideraciones: El modelo no ejecuta SQL, no concede permisos y no convierte texto libre o Markdown en una acción productiva.
 */

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using SistemaTicketsInteligente.Api.DAO;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.BLL;

public sealed class AsistenteTIBLL
{
    private static readonly object ConfirmacionLock = new();
    private static readonly IReadOnlyDictionary<string, string> Perfiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["USR"] = "Usuario", ["TEC"] = "Operador TI", ["SUP"] = "Supervisor", ["ADM"] = "Administrador"
    };

    private readonly ConfiguracionTIBLL configuracionTI;
    private readonly OpenAIAsistenteClient openAI;
    private readonly GeminiLiveClient geminiLive;
    private readonly AsistenteTIDAO agenteDAO;
    private readonly ITimeLimitedDataProtector protector;
    private readonly IMemoryCache cache;

    public AsistenteTIBLL(
        ConfiguracionTIBLL configuracionTI,
        OpenAIAsistenteClient openAI,
        GeminiLiveClient geminiLive,
        AsistenteTIDAO agenteDAO,
        IDataProtectionProvider dataProtection,
        IMemoryCache cache)
    {
        this.configuracionTI = configuracionTI;
        this.openAI = openAI;
        this.geminiLive = geminiLive;
        this.agenteDAO = agenteDAO;
        this.cache = cache;
        protector = dataProtection.CreateProtector("SistemaTickets.AsistenteTI.Acciones.v1").ToTimeLimitedDataProtector();
    }

    public async Task<AsistenteTIRespuesta> ResponderAsync(string operador, AsistenteTISolicitud solicitud, CancellationToken ct)
    {
        var mensaje = solicitud.Mensaje?.Trim() ?? string.Empty;
        if (mensaje.Length < 3) throw new ArgumentException("Describe brevemente la consulta o acción que necesitas.");
        if (mensaje.Length > 1200) throw new ArgumentException("La consulta no puede superar los 1200 caracteres.");
        solicitud.Historial ??= [];
        solicitud.Historial = solicitud.Historial.TakeLast(10).ToList();
        if (solicitud.Historial.Any(x => string.IsNullOrWhiteSpace(x.Contenido) || x.Contenido.Length > 1800 || (x.Rol != "usuario" && x.Rol != "asistente")))
            throw new ArgumentException("El historial de la conversación no es válido.");

        var configuracion = await configuracionTI.ObtenerAsync(operador, ct);
        var textoConversacion = string.Join(" ", solicitud.Historial.Where(x => x.Rol == "usuario").TakeLast(4).Select(x => x.Contenido).Append(mensaje));
        if (EsSolicitudUsuario(textoConversacion)) return PrepararUsuario(operador, textoConversacion, configuracion);

        var respuestaLocal = ResponderConConfiguracion(mensaje, configuracion);
        if (!openAI.EstaDisponible) return respuestaLocal;

        var instrucciones = """
            Eres el Asistente TI de Calimod. Responde en español profesional, claro y breve.
            Puedes explicar configuración autorizada y orientar al técnico para iniciar una investigación con el Agente de Ingeniería.
            Usa el contexto entregado como única fuente de datos internos. No inventes usuarios, áreas, métricas, tablas, procedimientos, diagnósticos ni acciones ejecutadas.
            Nunca solicites contraseñas ni secretos. Nunca entregues SQL correctivo ni afirmes haber modificado información.
            Las investigaciones operativas deben realizarse mediante una sesión del agente para conservar evidencia, correlación, informe Markdown y aprobación humana.
            La ejecución de cambios pertenece exclusivamente al backend mediante acciones catalogadas.
            """;
        var generada = await openAI.GenerarAsync(instrucciones, ConstruirContexto(mensaje, solicitud.Historial, configuracion), ct);
        if (string.IsNullOrWhiteSpace(generada)) return respuestaLocal;

        respuestaLocal.Respuesta = generada;
        respuestaLocal.Modo = "IA";
        return respuestaLocal;
    }

    public async Task<ConfirmarAccionTIRespuesta> ConfirmarAsync(string operador, ConfirmarAccionTISolicitud solicitud, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(solicitud.TokenConfirmacion)) throw new ArgumentException("La confirmación de la acción es obligatoria.");

        AccionFirmada accion;
        try
        {
            var contenido = protector.Unprotect(solicitud.TokenConfirmacion, out _);
            accion = JsonSerializer.Deserialize<AccionFirmada>(contenido) ?? throw new CryptographicException();
        }
        catch (CryptographicException)
        {
            throw new InvalidOperationException("La propuesta venció o no es válida. Solicita al asistente que la prepare nuevamente.");
        }

        if (!string.Equals(accion.Operador, operador, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("La propuesta pertenece a otra sesión de usuario.");
        if (!string.Equals(accion.Tipo, "SINCRONIZAR_USUARIO", StringComparison.Ordinal)) throw new InvalidOperationException("La acción solicitada no está habilitada.");

        var huella = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(solicitud.TokenConfirmacion)));
        var claveConfirmacion = $"asistente-ti:{huella}";
        lock (ConfirmacionLock)
        {
            if (cache.TryGetValue(claveConfirmacion, out _)) throw new InvalidOperationException("Esta acción ya fue procesada.");
            cache.Set(claveConfirmacion, true, TimeSpan.FromMinutes(15));
        }

        try
        {
            await configuracionTI.SincronizarUsuarioCorporativoAsync(operador, new SincronizarUsuarioCorporativoSolicitud
            {
                Usuario = accion.Usuario, Area = accion.Area, Perfil = accion.Perfil,
                Correo = string.IsNullOrWhiteSpace(accion.Correo) ? null : accion.Correo, Estado = accion.Estado
            }, ct);
        }
        catch
        {
            cache.Remove(claveConfirmacion);
            throw;
        }

        return new ConfirmarAccionTIRespuesta
        {
            Tipo = accion.Tipo, Registro = accion.Usuario,
            Mensaje = $"El usuario corporativo {accion.Usuario} fue sincronizado correctamente con el perfil {PerfilDescripcion(accion.Perfil)}."
        };
    }

    public async Task<AgenteTISesion> CrearInvestigacionAsync(string usuario, string area, CrearInvestigacionTISolicitud solicitud, CancellationToken ct)
    {
        solicitud.Descripcion = solicitud.Descripcion?.Trim() ?? string.Empty;
        solicitud.IncidenciaNumero = solicitud.IncidenciaNumero?.Trim().ToUpperInvariant();
        if (solicitud.Descripcion.Length < 5) throw new ArgumentException("Describe brevemente el problema que debe investigar el agente.");
        if (solicitud.Descripcion.Length > 1200) throw new ArgumentException("La descripción no puede superar los 1200 caracteres.");
        if (!string.IsNullOrWhiteSpace(solicitud.IncidenciaNumero) && !Regex.IsMatch(solicitud.IncidenciaNumero, @"^INC-\d{6}$"))
            throw new ArgumentException("La incidencia debe tener el formato INC-000000.");

        return await agenteDAO.CrearSesionAsync(usuario, area, solicitud, Guid.NewGuid(), ct);
    }

    public async Task<AgenteTILiveTokenRespuesta> CrearTokenLiveAsync(string usuario, string area, long sesionNumero, CancellationToken ct)
    {
        var contexto = await agenteDAO.ObtenerContextoAsync(usuario, area, sesionNumero, ct);
        if (contexto.Sesion.Estado is "INFORME_GRABADO" or "CAMBIO_VALIDADO" or "CANCELADO")
            throw new InvalidOperationException("La investigación ya está finalizada y no admite una nueva sesión Live.");
        return await geminiLive.CrearTokenAsync(ct);
    }

    public async Task RegistrarEventoAsync(string usuario, long sesionNumero, RegistrarEventoAgenteTISolicitud solicitud, CancellationToken ct)
    {
        solicitud.Tipo = solicitud.Tipo?.Trim() ?? string.Empty;
        solicitud.Fuente = solicitud.Fuente?.Trim() ?? string.Empty;
        solicitud.Contenido = solicitud.Contenido?.Trim() ?? string.Empty;
        if (solicitud.Tipo.Length is < 2 or > 40 || solicitud.Fuente.Length is < 2 or > 30) throw new ArgumentException("El tipo o la fuente del evento no son válidos.");
        if (solicitud.Contenido.Length is < 1 or > 12000) throw new ArgumentException("El contenido del evento no es válido.");
        if (!string.IsNullOrWhiteSpace(solicitud.DatosJson))
        {
            try { JsonDocument.Parse(solicitud.DatosJson); }
            catch (JsonException) { throw new ArgumentException("Los datos técnicos del evento no tienen formato JSON válido."); }
        }
        await agenteDAO.RegistrarEventoAsync(usuario, sesionNumero, solicitud, ct);
    }

    public Task FinalizarObservacionAsync(string usuario, long sesionNumero, FinalizarObservacionAgenteTISolicitud solicitud, CancellationToken ct)
    {
        solicitud.ResumenObservacion = Limitar(solicitud.ResumenObservacion, 12000);
        solicitud.ProcesoObservado = Limitar(solicitud.ProcesoObservado, 24000);
        solicitud.ErrorObservado = Limitar(solicitud.ErrorObservado, 1000);
        return agenteDAO.FinalizarObservacionAsync(usuario, sesionNumero, solicitud, ct);
    }

    public Task<AgenteTIContextoInvestigacion> ObtenerInvestigacionAsync(string usuario, string area, long sesionNumero, CancellationToken ct) =>
        agenteDAO.ObtenerContextoAsync(usuario, area, sesionNumero, ct);

    public async Task<AgenteTIDiagnosticoRespuesta> InvestigarAsync(string usuario, string area, long sesionNumero, CancellationToken ct)
    {
        var contexto = await agenteDAO.ObtenerContextoAsync(usuario, area, sesionNumero, ct);
        if (contexto.Sesion.Estado is "INFORME_GRABADO" or "CAMBIO_VALIDADO" or "CANCELADO") throw new InvalidOperationException("La investigación ya se encuentra finalizada.");
        if (contexto.Sesion.InformeDisponible && !string.IsNullOrWhiteSpace(contexto.Sesion.Diagnostico)) return ConstruirRespuestaPersistida(contexto);

        var evidencias = ConstruirEvidencias(contexto);
        var traza = ConstruirTraza(contexto);
        var limitacion = traza.Count == 0
            ? "No existe telemetría de código correlacionada para esta sesión. La pantalla y la conversación permiten reconstruir el proceso del usuario, pero el agente no inventará métodos, endpoints o Stored Procedures que no estén instrumentados."
            : string.Empty;

        var diagnostico = openAI.EstaDisponible
            ? await GenerarDiagnosticoIAAsync(contexto, evidencias, traza, ct)
            : DiagnosticoSinModelo(contexto);

        diagnostico.SesionNumero = sesionNumero;
        diagnostico.Estado = "PENDIENTE_TI";
        diagnostico.Evidencias = evidencias;
        diagnostico.TrazaTecnica = traza;
        diagnostico.Limitacion = limitacion;
        diagnostico.Confianza = AjustarConfianza(diagnostico.Confianza, evidencias, traza);
        diagnostico.InformeDisponible = true;
        ValidarAccionPropuesta(diagnostico, contexto);

        var parametrosJson = diagnostico.Accion?.ParametrosJson ?? string.Empty;
        var evidenciasJson = JsonSerializer.Serialize(evidencias, OpcionesJsonCamelCase);
        var informe = ConstruirInformeMarkdown(contexto, diagnostico);
        await agenteDAO.GuardarDiagnosticoAsync(usuario, sesionNumero, diagnostico, parametrosJson, evidenciasJson, informe, contexto.Sesion.IdCorrelacion, ct);
        return diagnostico;
    }

    public async Task<string> GrabarInformacionAsync(string usuario, string area, long sesionNumero, CancellationToken ct)
    {
        var contexto = await agenteDAO.ObtenerContextoAsync(usuario, area, sesionNumero, ct);
        if (!contexto.Sesion.InformeDisponible) throw new InvalidOperationException("Primero debe completarse la investigación y generar el informe técnico.");
        return await agenteDAO.GrabarInformacionAsync(usuario, sesionNumero, contexto.Sesion.IdCorrelacion, ct);
    }

    public async Task<AgenteTIDecisionRespuesta> RealizarCambioAsync(string usuario, string area, long sesionNumero, RealizarCambioAgenteTISolicitud solicitud, CancellationToken ct)
    {
        if (!solicitud.Confirmar) throw new ArgumentException("La ejecución requiere confirmación explícita de TI.");
        var contexto = await agenteDAO.ObtenerContextoAsync(usuario, area, sesionNumero, ct);
        if (!contexto.Sesion.InformeDisponible || string.IsNullOrWhiteSpace(contexto.Sesion.AccionCodigo)) throw new InvalidOperationException("El diagnóstico no contiene una acción correctiva catalogada.");
        if (string.IsNullOrWhiteSpace(contexto.Sesion.IncidenciaNumero)) throw new InvalidOperationException("La investigación debe estar asociada a una incidencia antes de ejecutar un cambio.");

        var preparacion = await agenteDAO.PrepararCambioAsync(usuario, area, sesionNumero, Guid.NewGuid(), contexto.Sesion.IdCorrelacion, ct);
        if (!preparacion.PuedeEjecutar)
            return new AgenteTIDecisionRespuesta
            {
                SesionNumero = sesionNumero, Estado = preparacion.Estado, Mensaje = preparacion.Mensaje,
                SolicitudAprobacionSecuencia = preparacion.SolicitudAprobacionSecuencia, Ejecutado = false
            };

        if (!preparacion.EjecucionSecuencia.HasValue) throw new InvalidOperationException("La ejecución no posee una secuencia de control válida.");

        try
        {
            var resultado = await agenteDAO.EjecutarProcedimientoControladoAsync(
                preparacion.ProcedimientoEjecutor, usuario, area, contexto.Sesion.IncidenciaNumero,
                preparacion.ParametrosJson, contexto.Sesion.IdCorrelacion, ct);

            if (!PostValidacionConfirmada(resultado.ResultadoJson))
            {
                await agenteDAO.FinalizarCambioAsync(usuario, sesionNumero, preparacion.EjecucionSecuencia.Value, false, resultado.ResultadoJson, resultado.FilasAfectadas,
                    "El ejecutor no confirmó la validación posterior requerida.", contexto.Sesion.IdCorrelacion, ct);
                return new AgenteTIDecisionRespuesta
                {
                    SesionNumero = sesionNumero, Estado = "ERROR_EJECUCION", Ejecutado = false,
                    Mensaje = "El procedimiento terminó, pero no confirmó sus postcondiciones. La incidencia permanece abierta para revisión de TI."
                };
            }

            await agenteDAO.FinalizarCambioAsync(usuario, sesionNumero, preparacion.EjecucionSecuencia.Value, true, resultado.ResultadoJson, resultado.FilasAfectadas, null, contexto.Sesion.IdCorrelacion, ct);
            return new AgenteTIDecisionRespuesta
            {
                SesionNumero = sesionNumero, Estado = "CAMBIO_VALIDADO", Ejecutado = true,
                Mensaje = "La acción autorizada fue ejecutada y el procedimiento confirmó la validación posterior. La trazabilidad quedó registrada."
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await agenteDAO.FinalizarCambioAsync(usuario, sesionNumero, preparacion.EjecucionSecuencia.Value, false, null, null, Limitar(ex.Message, 1800), contexto.Sesion.IdCorrelacion, ct);
            throw new InvalidOperationException("La acción controlada no pudo completarse. La ejecución fue registrada como error y la incidencia permanece abierta.", ex);
        }
    }

    private async Task<AgenteTIDiagnosticoRespuesta> GenerarDiagnosticoIAAsync(AgenteTIContextoInvestigacion contexto, List<AgenteTIEvidencia> evidencias, List<string> traza, CancellationToken ct)
    {
        var instrucciones = """
            Actúas como motor de diagnóstico del Agente de Ingeniería de Incidencias TI.
            Trabaja exclusivamente con la evidencia proporcionada. No inventes tablas, columnas, código fuente, endpoints, Stored Procedures, estados ni resultados de consultas.
            La pantalla describe lo que hizo o vio el usuario; la telemetría y auditoría describen lo que el sistema realmente registró. No mezcles ambos niveles como si fueran equivalentes.
            Si la evidencia es insuficiente o contradictoria, indícalo y no propongas una acción correctiva.
            Solo puedes proponer un accionCodigo que aparezca exactamente en ACCIONES AUTORIZADAS y cuyo tipo sea E. Nunca generes SQL.
            Devuelve exclusivamente un objeto JSON válido con: diagnostico, causaProbable, solucionPropuesta, confianza (0-100), accionCodigo (string o null) y parametros (objeto JSON).
            La confianza expresa calidad del diagnóstico, no autorización ni riesgo.
            """;

        var salida = await openAI.GenerarAsync(instrucciones, ConstruirContextoInvestigacion(contexto, evidencias, traza), ct);
        if (string.IsNullOrWhiteSpace(salida)) return DiagnosticoSinModelo(contexto);

        try
        {
            var json = ExtraerJson(salida);
            using var documento = JsonDocument.Parse(json);
            var raiz = documento.RootElement;
            var respuesta = new AgenteTIDiagnosticoRespuesta
            {
                Diagnostico = LeerTextoJson(raiz, "diagnostico", "La evidencia disponible no permite establecer un diagnóstico concluyente."),
                CausaProbable = LeerTextoJson(raiz, "causaProbable", "No determinada con la evidencia disponible."),
                SolucionPropuesta = LeerTextoJson(raiz, "solucionPropuesta", "Escalar a revisión técnica con el expediente recopilado."),
                Confianza = LeerDecimalJson(raiz, "confianza", 20m)
            };

            var accionCodigo = LeerTextoJson(raiz, "accionCodigo", string.Empty);
            if (!string.IsNullOrWhiteSpace(accionCodigo))
            {
                var accion = contexto.Acciones.FirstOrDefault(x => x.Tipo == "E" && string.Equals(x.AccionCodigo, accionCodigo, StringComparison.OrdinalIgnoreCase));
                if (accion is not null)
                {
                    var parametros = raiz.TryGetProperty("parametros", out var propiedadParametros) && propiedadParametros.ValueKind == JsonValueKind.Object
                        ? propiedadParametros.GetRawText() : "{}";
                    respuesta.Accion = new AgenteTIAccionPropuesta
                    {
                        AccionCodigo = accion.AccionCodigo, Nombre = accion.Nombre, NivelRiesgo = accion.NivelRiesgo,
                        RequiereAprobacion = accion.RequiereAprobacion, ParametrosJson = parametros
                    };
                }
            }
            return respuesta;
        }
        catch (JsonException)
        {
            return DiagnosticoSinModelo(contexto);
        }
    }

    private static AgenteTIDiagnosticoRespuesta DiagnosticoSinModelo(AgenteTIContextoInvestigacion contexto)
    {
        var error = !string.IsNullOrWhiteSpace(contexto.Sesion.ErrorObservado) ? contexto.Sesion.ErrorObservado : contexto.Ticket.MensajeError;
        return new AgenteTIDiagnosticoRespuesta
        {
            Diagnostico = string.IsNullOrWhiteSpace(error)
                ? "La sesión recopiló contexto, pero todavía no existe evidencia suficiente para determinar el punto técnico de falla."
                : $"El error observado fue registrado ({Limitar(error, 300)}), pero todavía no existe evidencia técnica suficiente para atribuir una causa raíz.",
            CausaProbable = "No determinada con evidencia verificable.",
            SolucionPropuesta = "Conservar el expediente y continuar con telemetría o herramientas diagnósticas autorizadas antes de modificar información.",
            Confianza = 20m
        };
    }

    private static List<AgenteTIEvidencia> ConstruirEvidencias(AgenteTIContextoInvestigacion contexto)
    {
        var evidencias = new List<AgenteTIEvidencia>();
        foreach (var evento in contexto.Eventos.Where(x => x.Tipo.Contains("ERROR", StringComparison.OrdinalIgnoreCase) || x.Tipo.Contains("TRAZA", StringComparison.OrdinalIgnoreCase)).TakeLast(5))
            evidencias.Add(new AgenteTIEvidencia { TipoFuente = evento.Fuente, Referencia = $"LIVE-{evento.Secuencia}", Descripcion = Limitar(evento.Contenido, 900) });

        foreach (var documento in contexto.Documentos.Take(5))
            evidencias.Add(new AgenteTIEvidencia { TipoFuente = "DOCUMENTO", Referencia = $"{documento.TipoDocumento}-{documento.NumeroDocumento}", Descripcion = Limitar(documento.Descripcion, 900) });

        foreach (var conocimiento in contexto.Conocimientos.Where(x => x.PuntajeContextual > 0).Take(3))
            evidencias.Add(new AgenteTIEvidencia { TipoFuente = "BASE_CONOCIMIENTO", Referencia = conocimiento.ConocimientoCodigo, Descripcion = Limitar($"{conocimiento.Titulo}: {conocimiento.Causa}", 900) });

        foreach (var auditoria in contexto.Auditoria.Take(5))
            evidencias.Add(new AgenteTIEvidencia { TipoFuente = "AUDITORIA", Referencia = auditoria.Evento, Descripcion = Limitar($"{auditoria.Resultado} · {auditoria.DetalleJson}", 900) });

        if (!string.IsNullOrWhiteSpace(contexto.Sesion.ErrorObservado))
            evidencias.Insert(0, new AgenteTIEvidencia { TipoFuente = "LIVE", Referencia = "ERROR_OBSERVADO", Descripcion = Limitar(contexto.Sesion.ErrorObservado, 900) });
        else if (!string.IsNullOrWhiteSpace(contexto.Ticket.MensajeError))
            evidencias.Insert(0, new AgenteTIEvidencia { TipoFuente = "TICKET", Referencia = contexto.Ticket.IncidenciaNumero, Descripcion = Limitar(contexto.Ticket.MensajeError, 900) });

        return evidencias.GroupBy(x => $"{x.TipoFuente}|{x.Referencia}|{x.Descripcion}").Select(x => x.First()).Take(15).ToList();
    }

    private static List<string> ConstruirTraza(AgenteTIContextoInvestigacion contexto) => contexto.Eventos
        .Where(x => x.Tipo.Contains("TRAZA", StringComparison.OrdinalIgnoreCase) || x.Fuente.Equals("TELEMETRIA", StringComparison.OrdinalIgnoreCase) || x.Fuente.Equals("CODIGO", StringComparison.OrdinalIgnoreCase))
        .Select(x => Limitar(x.Contenido, 1000)).Where(x => x.Length > 0).Take(20).ToList();

    private static decimal AjustarConfianza(decimal confianza, IReadOnlyCollection<AgenteTIEvidencia> evidencias, IReadOnlyCollection<string> traza)
    {
        confianza = Math.Clamp(confianza, 0m, 100m);
        if (evidencias.Count == 0) return Math.Min(confianza, 30m);
        if (evidencias.Count == 1) return Math.Min(confianza, 55m);
        if (traza.Count == 0) return Math.Min(confianza, 85m);
        return Math.Min(confianza, 95m);
    }

    private static void ValidarAccionPropuesta(AgenteTIDiagnosticoRespuesta diagnostico, AgenteTIContextoInvestigacion contexto)
    {
        if (diagnostico.Accion is null) return;
        var permitida = contexto.Acciones.FirstOrDefault(x => x.Tipo == "E" && string.Equals(x.AccionCodigo, diagnostico.Accion.AccionCodigo, StringComparison.OrdinalIgnoreCase));
        if (permitida is null || diagnostico.Confianza < 60m)
        {
            diagnostico.Accion = null;
            return;
        }
        diagnostico.Accion.Nombre = permitida.Nombre;
        diagnostico.Accion.NivelRiesgo = permitida.NivelRiesgo;
        diagnostico.Accion.RequiereAprobacion = permitida.RequiereAprobacion;
        if (!JsonValidoObjeto(diagnostico.Accion.ParametrosJson)) diagnostico.Accion.ParametrosJson = "{}";
    }

    private static AgenteTIDiagnosticoRespuesta ConstruirRespuestaPersistida(AgenteTIContextoInvestigacion contexto)
    {
        var accionCatalogo = contexto.Acciones.FirstOrDefault(x => string.Equals(x.AccionCodigo, contexto.Sesion.AccionCodigo, StringComparison.OrdinalIgnoreCase));
        return new AgenteTIDiagnosticoRespuesta
        {
            SesionNumero = contexto.Sesion.SesionNumero, Estado = contexto.Sesion.Estado, Diagnostico = contexto.Sesion.Diagnostico,
            CausaProbable = contexto.Sesion.CausaProbable, SolucionPropuesta = contexto.Sesion.SolucionPropuesta, Confianza = contexto.Sesion.Confianza ?? 0,
            Evidencias = ConstruirEvidencias(contexto), TrazaTecnica = ConstruirTraza(contexto), InformeDisponible = contexto.Sesion.InformeDisponible,
            Limitacion = ConstruirTraza(contexto).Count == 0 ? "No existe telemetría de código correlacionada para esta sesión; el agente no inventa el trazado interno." : string.Empty,
            Accion = accionCatalogo is null ? null : new AgenteTIAccionPropuesta
            {
                AccionCodigo = accionCatalogo.AccionCodigo, Nombre = accionCatalogo.Nombre, NivelRiesgo = accionCatalogo.NivelRiesgo,
                RequiereAprobacion = accionCatalogo.RequiereAprobacion, ParametrosJson = string.IsNullOrWhiteSpace(contexto.Sesion.ParametrosJson) ? "{}" : contexto.Sesion.ParametrosJson
            }
        };
    }

    private static string ConstruirContextoInvestigacion(AgenteTIContextoInvestigacion contexto, IEnumerable<AgenteTIEvidencia> evidencias, IEnumerable<string> traza)
    {
        var sb = new StringBuilder();
        sb.AppendLine("SESIÓN:");
        sb.AppendLine($"Descripción inicial: {Limitar(contexto.Sesion.DescripcionInicial, 1200)}");
        sb.AppendLine($"Proceso observado: {Limitar(contexto.Sesion.ProcesoObservado, 5000)}");
        sb.AppendLine($"Error observado: {Limitar(contexto.Sesion.ErrorObservado, 1000)}");
        if (!string.IsNullOrWhiteSpace(contexto.Ticket.IncidenciaNumero))
        {
            sb.AppendLine("TICKET:");
            sb.AppendLine($"{contexto.Ticket.IncidenciaNumero} | {contexto.Ticket.Titulo} | Estado={contexto.Ticket.Estado} | Línea={contexto.Ticket.Linea} | Item={contexto.Ticket.Item} | Tipo={contexto.Ticket.Tipo} | Categoría={contexto.Ticket.Categoria}");
            sb.AppendLine($"Detalle: {Limitar(contexto.Ticket.Detalle, 3000)}");
            sb.AppendLine($"Mensaje de error: {Limitar(contexto.Ticket.MensajeError, 1000)}");
        }
        sb.AppendLine("DOCUMENTOS:");
        foreach (var d in contexto.Documentos.Take(10)) sb.AppendLine($"- {d.CompaniaSocio} | {d.TipoDocumento} | {d.NumeroDocumento} | {Limitar(d.Descripcion, 500)}");
        sb.AppendLine("EVENTOS OBSERVADOS:");
        foreach (var e in contexto.Eventos.TakeLast(30)) sb.AppendLine($"- {e.Fecha:O} | {e.Fuente}/{e.Tipo} | {Limitar(e.Contenido, 700)}");
        sb.AppendLine("TRAZA TÉCNICA VERIFICADA:");
        foreach (var t in traza) sb.AppendLine($"- {t}");
        sb.AppendLine("CONOCIMIENTO AUTORIZADO:");
        foreach (var k in contexto.Conocimientos.Take(8)) sb.AppendLine($"- {k.ConocimientoCodigo} (score {k.PuntajeContextual}) | {k.Titulo} | Causa: {Limitar(k.Causa, 700)} | Solución: {Limitar(k.Solucion, 700)}");
        sb.AppendLine("AUDITORÍA:");
        foreach (var a in contexto.Auditoria.Take(20)) sb.AppendLine($"- {a.Fecha:O} | {a.Evento} | {a.Resultado} | {Limitar(a.DetalleJson, 700)}");
        sb.AppendLine("EVIDENCIAS CONSOLIDADAS:");
        foreach (var e in evidencias) sb.AppendLine($"- {e.TipoFuente} | {e.Referencia} | {e.Descripcion}");
        sb.AppendLine("ACCIONES AUTORIZADAS:");
        foreach (var a in contexto.Acciones.Where(x => x.Tipo == "E")) sb.AppendLine($"- {a.AccionCodigo} | {a.Nombre} | Riesgo={a.NivelRiesgo} | Aprobación={a.RequiereAprobacion}");
        return sb.ToString();
    }

    private static string ConstruirInformeMarkdown(AgenteTIContextoInvestigacion contexto, AgenteTIDiagnosticoRespuesta diagnostico)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Informe Técnico de Investigación Autónoma");
        sb.AppendLine();
        sb.AppendLine("## 1. Identificación");
        sb.AppendLine($"- **Sesión:** AGT-{contexto.Sesion.SesionNumero:000000}");
        sb.AppendLine($"- **Incidencia:** {(string.IsNullOrWhiteSpace(contexto.Sesion.IncidenciaNumero) ? "No asociada" : contexto.Sesion.IncidenciaNumero)}");
        sb.AppendLine($"- **Correlation ID:** `{contexto.Sesion.IdCorrelacion}`");
        sb.AppendLine($"- **Inicio:** {contexto.Sesion.FechaInicio:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("## 2. Problema investigado");
        sb.AppendLine(contexto.Sesion.DescripcionInicial);
        sb.AppendLine();
        sb.AppendLine("## 3. Reproducción observada");
        sb.AppendLine(string.IsNullOrWhiteSpace(contexto.Sesion.ProcesoObservado) ? "No se registró una sesión Live; la investigación se inició con la descripción disponible." : contexto.Sesion.ProcesoObservado);
        if (!string.IsNullOrWhiteSpace(contexto.Sesion.ErrorObservado)) sb.AppendLine($"\n**Error observado:** {contexto.Sesion.ErrorObservado}");
        sb.AppendLine();
        sb.AppendLine("## 4. Trazabilidad técnica");
        if (diagnostico.TrazaTecnica.Count == 0) sb.AppendLine("No existe telemetría de código correlacionada suficiente. No se infirieron métodos, endpoints ni Stored Procedures sin evidencia.");
        else foreach (var t in diagnostico.TrazaTecnica) sb.AppendLine($"- {t}");
        sb.AppendLine();
        sb.AppendLine("## 5. Evidencia analizada");
        if (diagnostico.Evidencias.Count == 0) sb.AppendLine("- No se recuperó evidencia verificable adicional.");
        else foreach (var e in diagnostico.Evidencias) sb.AppendLine($"- **{e.TipoFuente} · {e.Referencia}:** {e.Descripcion}");
        sb.AppendLine();
        sb.AppendLine("## 6. Diagnóstico");
        sb.AppendLine(diagnostico.Diagnostico);
        sb.AppendLine();
        sb.AppendLine("## 7. Causa probable");
        sb.AppendLine(diagnostico.CausaProbable);
        sb.AppendLine();
        sb.AppendLine("## 8. Solución propuesta");
        sb.AppendLine(diagnostico.SolucionPropuesta);
        sb.AppendLine();
        sb.AppendLine($"## 9. Confianza diagnóstica\n{diagnostico.Confianza:0.##}%");
        sb.AppendLine();
        sb.AppendLine("## 10. Acción controlada");
        if (diagnostico.Accion is null) sb.AppendLine("No se propone una acción automática. El caso debe continuar mediante revisión de TI.");
        else
        {
            sb.AppendLine($"- **Código:** {diagnostico.Accion.AccionCodigo}");
            sb.AppendLine($"- **Acción:** {diagnostico.Accion.Nombre}");
            sb.AppendLine($"- **Riesgo:** {diagnostico.Accion.NivelRiesgo}");
            sb.AppendLine($"- **Requiere aprobación:** {(diagnostico.Accion.RequiereAprobacion ? "Sí" : "No")}");
            sb.AppendLine($"- **Parámetros propuestos:** `{diagnostico.Accion.ParametrosJson}`");
        }
        sb.AppendLine();
        sb.AppendLine("## 11. Decisión requerida de TI");
        sb.AppendLine("- **GRABAR INFORMACIÓN:** conserva y descarga este expediente; el agente finaliza sin realizar cambios.");
        sb.AppendLine("- **REALIZAR CAMBIO:** solicita/valida aprobación y únicamente puede invocar una acción catalogada con ejecutor autorizado. Este Markdown nunca se ejecuta.");
        if (!string.IsNullOrWhiteSpace(diagnostico.Limitacion)) sb.AppendLine($"\n> Limitación: {diagnostico.Limitacion}");
        return sb.ToString();
    }

    private AsistenteTIRespuesta PrepararUsuario(string operador, string texto, ConfiguracionTIRespuesta configuracion)
    {
        var usuario = ExtraerUsuario(texto);
        var area = ExtraerArea(texto, configuracion.Areas);
        var perfil = ExtraerPerfil(texto);
        var correo = Regex.Match(texto, @"[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}", RegexOptions.IgnoreCase).Value;
        var faltantes = new List<string>();
        if (string.IsNullOrWhiteSpace(usuario)) faltantes.Add("usuario Spring");
        if (area is null) faltantes.Add("área");
        if (string.IsNullOrWhiteSpace(perfil)) faltantes.Add("perfil");

        if (faltantes.Count > 0)
            return new AsistenteTIRespuesta
            {
                Respuesta = $"Puedo preparar el alta corporativa, pero todavía necesito: {string.Join(", ", faltantes)}. Indícalos en un mensaje; el correo es opcional.",
                Fuentes = ["Maestros TI", "Identidad corporativa Spring"], Sugerencias = ["Ver áreas disponibles", "Ver perfiles disponibles"],
                Accion = new AsistenteTIAccion { Tipo = "SINCRONIZAR_USUARIO", Titulo = "Sincronizar usuario corporativo", Faltantes = faltantes }
            };

        var existente = configuracion.Usuarios.FirstOrDefault(x => string.Equals(x.Usuario, usuario, StringComparison.OrdinalIgnoreCase));
        var propuesta = new AsistenteTIUsuarioPropuesto
        {
            Usuario = usuario, Area = area!.Area, AreaDescripcion = area.Descripcion, Perfil = perfil,
            PerfilDescripcion = PerfilDescripcion(perfil), Correo = correo, Estado = "A", YaExiste = existente is not null
        };
        var expira = DateTimeOffset.UtcNow.AddMinutes(10);
        var token = protector.Protect(JsonSerializer.Serialize(new AccionFirmada("SINCRONIZAR_USUARIO", operador, propuesta.Usuario, propuesta.Area, propuesta.Perfil, propuesta.Correo, propuesta.Estado)), expira);

        return new AsistenteTIRespuesta
        {
            Respuesta = existente is null ? "Preparé el alta con los catálogos vigentes. Revisa los datos antes de confirmar; la identidad debe existir en Spring." : "Ese usuario ya está configurado. Si confirmas, se sincronizará nuevamente.",
            Fuentes = ["Maestros TI", "Identidad corporativa Spring"], Sugerencias = ["Ver áreas disponibles", "Ver perfiles disponibles"],
            Accion = new AsistenteTIAccion { Tipo = "SINCRONIZAR_USUARIO", Titulo = existente is null ? "Crear usuario corporativo" : "Actualizar usuario corporativo", TokenConfirmacion = token, ExpiraEn = expira, Usuario = propuesta }
        };
    }

    private static AsistenteTIRespuesta ResponderConConfiguracion(string mensaje, ConfiguracionTIRespuesta configuracion)
    {
        var texto = Normalizar(mensaje);
        string respuesta;
        if (texto.Contains("sla") || texto.Contains("tiempo objetivo") || texto.Contains("tiempo de atencion"))
            respuesta = $"SLA activos por prioridad:\n\n{string.Join("\n", configuracion.Sla.Where(x => x.Estado == "A").OrderByDescending(x => x.Prioridad).Select(x => $"Prioridad {x.Prioridad}: {FormatearDuracion(x.SlaObjetivoMinutos)}"))}";
        else if (texto.Contains("categoria")) respuesta = ResumirCatalogo("categorías activas", configuracion.Categorias.Where(x => x.Estado == "A").Select(x => $"{x.Categoria} · {x.Descripcion}"));
        else if (texto.Contains("tipo de ticket") || texto.Contains("tipos de ticket") || texto.Contains("tipologia")) respuesta = ResumirCatalogo("tipos de ticket activos", configuracion.Tipos.Where(x => x.Estado == "A").Select(x => $"{x.Tipo} · {x.Descripcion}"));
        else if (texto.Contains("linea")) respuesta = ResumirCatalogo("líneas activas", configuracion.Lineas.Where(x => x.Estado == "A").Select(x => $"{x.Linea} · {x.Descripcion} (area {x.Area})"));
        else if (texto.Contains("item") || texto.Contains("servicio")) respuesta = ResumirCatalogo("ítems o servicios activos", configuracion.Items.Where(x => x.Estado == "A").Select(x => $"{x.Item} · {x.Descripcion} (línea {x.Linea})"));
        else if (texto.Contains("conocimiento") || texto.Contains("articulo") || texto.Contains("guia")) respuesta = ResumirCatalogo("artículos activos", configuracion.Conocimientos.Where(x => x.Estado == "A").Select(x => $"{x.ConocimientoCodigo} · {x.Titulo}"));
        else if (texto.Contains("area")) respuesta = ResumirCatalogo("áreas activas", configuracion.Areas.Where(x => x.Estado == "A").Select(x => $"{x.Area} · {x.Descripcion}"));
        else if (texto.Contains("perfil") || texto.Contains("rol")) respuesta = "Perfiles habilitados:\n\nUSR · Usuario\nTEC · Operador TI\nSUP · Supervisor\nADM · Administrador";
        else respuesta = "Puedo responder consultas operativas o iniciar una investigación del Agente de Ingeniería. Para una incidencia real utiliza el flujo de investigación: observar/reproducir, analizar evidencia, generar expediente y decidir entre Grabar información o Realizar cambio.";

        return new AsistenteTIRespuesta { Respuesta = respuesta, Fuentes = ["Configuración vigente de Gestión TI"], Sugerencias = ["Iniciar investigación", "Ver SLA activos", "Ver áreas disponibles"] };
    }

    private static string ConstruirContexto(string mensaje, IEnumerable<AsistenteUsuarioMensaje> historial, ConfiguracionTIRespuesta configuracion)
    {
        var texto = new StringBuilder();
        texto.AppendLine("CONFIGURACIÓN AUTORIZADA:");
        texto.AppendLine($"Áreas activas: {string.Join("; ", configuracion.Areas.Where(x => x.Estado == "A").Select(x => $"{x.Area}={x.Descripcion}"))}");
        texto.AppendLine($"SLA activos: {string.Join("; ", configuracion.Sla.Where(x => x.Estado == "A").Select(x => $"P{x.Prioridad}={x.SlaObjetivoMinutos} minutos"))}");
        texto.AppendLine("CONVERSACIÓN:");
        foreach (var item in historial.TakeLast(6)) texto.AppendLine($"{item.Rol}: {item.Contenido}");
        texto.AppendLine($"usuario: {mensaje}");
        return texto.ToString();
    }

    private static bool EsSolicitudUsuario(string texto)
    {
        var normalizado = Normalizar(texto);
        return (normalizado.Contains("crear") || normalizado.Contains("crea ") || normalizado.Contains("registrar") || normalizado.Contains("agregar") || normalizado.Contains("anadir") || normalizado.Contains("sincronizar") || normalizado.Contains("dar de alta"))
            && (normalizado.Contains("usuario") || normalizado.Contains("cuenta"));
    }

    private static string ExtraerUsuario(string texto)
    {
        var match = Regex.Match(texto.ToUpperInvariant(), @"(?:USUARIO|CUENTA)\s+(?:SPRING\s+)?([A-Z0-9._-]{3,20})");
        if (!match.Success) return string.Empty;
        var valor = match.Groups[1].Value;
        return new[] { "CON", "DEL", "PARA", "EN", "AREA", "ÁREA", "PERFIL" }.Contains(valor) ? string.Empty : valor;
    }

    private static ConfiguracionTIArea? ExtraerArea(string texto, IEnumerable<ConfiguracionTIArea> areas)
    {
        var activas = areas.Where(x => x.Estado == "A").ToList();
        var match = Regex.Match(texto.ToUpperInvariant(), @"[ÁA]REA\s+(?:DE\s+)?([A-Z0-9]{1,3})\b");
        if (match.Success)
        {
            var porCodigo = activas.FirstOrDefault(x => string.Equals(x.Area, match.Groups[1].Value, StringComparison.OrdinalIgnoreCase));
            if (porCodigo is not null) return porCodigo;
        }
        var normalizado = Normalizar(texto);
        return activas.OrderByDescending(x => x.Descripcion.Length).FirstOrDefault(x => x.Descripcion.Length >= 4 && normalizado.Contains(Normalizar(x.Descripcion)));
    }

    private static string ExtraerPerfil(string texto)
    {
        var normalizado = Normalizar(texto);
        var codigo = Regex.Match(normalizado.ToUpperInvariant(), @"\b(USR|TEC|SUP|ADM)\b");
        if (codigo.Success) return codigo.Value;
        if (normalizado.Contains("administrador")) return "ADM";
        if (normalizado.Contains("supervisor")) return "SUP";
        if (normalizado.Contains("tecnico") || normalizado.Contains("operador ti")) return "TEC";
        if (normalizado.Contains("colaborador") || normalizado.Contains("usuario final") || Regex.IsMatch(normalizado, @"perfil\s+(?:de\s+)?(?:usuario|normal)")) return "USR";
        return string.Empty;
    }

    private static string ResumirCatalogo(string titulo, IEnumerable<string> valores)
    {
        var lista = valores.OrderBy(x => x).ToList();
        if (lista.Count == 0) return $"No hay registros para {titulo} en la configuración vigente.";
        const int maximo = 40;
        return $"Configuración vigente: {titulo}.\n\n{string.Join("\n", lista.Take(maximo))}{(lista.Count > maximo ? $"\n\nSe muestran {maximo} de {lista.Count} registros." : string.Empty)}";
    }

    private static string FormatearDuracion(int minutos)
    {
        if (minutos < 60) return $"{minutos} min";
        if (minutos % 1440 == 0) return $"{minutos / 1440} día(s)";
        if (minutos % 60 == 0) return $"{minutos / 60} h";
        return $"{minutos / 60} h {minutos % 60} min";
    }

    private static bool PostValidacionConfirmada(string resultadoJson)
    {
        if (string.IsNullOrWhiteSpace(resultadoJson)) return false;
        try
        {
            using var documento = JsonDocument.Parse(resultadoJson);
            return documento.RootElement.TryGetProperty("validacionPosterior", out var valor) && valor.ValueKind == JsonValueKind.True;
        }
        catch (JsonException) { return false; }
    }

    private static string ExtraerJson(string texto)
    {
        var limpio = texto.Trim();
        if (limpio.StartsWith("```", StringComparison.Ordinal))
        {
            var inicio = limpio.IndexOf('{');
            var fin = limpio.LastIndexOf('}');
            if (inicio >= 0 && fin > inicio) return limpio[inicio..(fin + 1)];
        }
        return limpio;
    }

    private static string LeerTextoJson(JsonElement raiz, string propiedad, string valorDefecto)
    {
        if (!raiz.TryGetProperty(propiedad, out var valor) || valor.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return valorDefecto;
        var texto = Limitar(valor.ValueKind == JsonValueKind.String ? valor.GetString() : valor.ToString(), 8000);
        return texto.Length > 0 ? texto : valorDefecto;
    }

    private static decimal LeerDecimalJson(JsonElement raiz, string propiedad, decimal valorDefecto)
    {
        if (!raiz.TryGetProperty(propiedad, out var valor)) return valorDefecto;
        if (valor.ValueKind == JsonValueKind.Number && valor.TryGetDecimal(out var numero)) return numero;
        return decimal.TryParse(valor.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var texto) ? texto : valorDefecto;
    }

    private static bool JsonValidoObjeto(string texto)
    {
        try { using var documento = JsonDocument.Parse(texto); return documento.RootElement.ValueKind == JsonValueKind.Object; }
        catch { return false; }
    }

    private static string Limitar(string? texto, int maximo)
    {
        var valor = texto?.Trim() ?? string.Empty;
        return valor.Length <= maximo ? valor : valor[..maximo];
    }

    private static string PerfilDescripcion(string perfil) => Perfiles.TryGetValue(perfil, out var descripcion) ? descripcion : perfil;

    private static string Normalizar(string texto)
    {
        var descompuesto = (texto ?? string.Empty).ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return new string(descompuesto.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).Normalize(NormalizationForm.FormC);
    }

    private static readonly JsonSerializerOptions OpcionesJsonCamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private sealed record AccionFirmada(string Tipo, string Operador, string Usuario, string Area, string Perfil, string Correo, string Estado);
}
