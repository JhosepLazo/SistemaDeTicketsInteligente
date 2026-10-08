/**
 * Archivo: AsistenteTIBLL.Decision.cs
 * Objetivo: Aplicar la decisión de TI sobre el diagnóstico: grabar la información o realizar el cambio propuesto, y cerrar el caso.
 * Responsabilidad: Grabar el expediente, comprobar precondiciones (dry-run), simular la acción en una transacción revertida, ejecutarla
 *   con aprobación y postcondiciones verificadas, validar la solución y convertir la investigación en un borrador de conocimiento.
 *   En modo AUTONOMO, ejecutar sin humano solo lo que PoliticaAutonomia libera, con una simulación inmediata antes.
 * Dependencias: BaseDatos (Usp_TI_Agente_GrabarInformacion, DryRun, ObtenerEjecutorSimulacion, RegistrarSimulacion, PrepararCambio,
 *   FinalizarCambio, ValidarSolucion, CrearBorrador, DatosAutonomia y los ejecutores catalogados dbo.Usp_TI_AgenteAccion_*),
 *   ControlAgenteTI, PoliticaAutonomia e IConfiguration.
 * Flujo: decisión TI -> preparación (aprobación e idempotencia en SQL) -> ejecutor en transacción -> postcondiciones -> auditoría.
 * Consideraciones: Solo se ejecutan procedimientos del catálogo con el prefijo contractual; jamás SQL generado por IA. El ejecutor debe
 *   devolver una única fila con validacionPosterior=true y no superar el máximo de filas autorizado; si no, se revierte todo.
 *   Con AgenteTI:SoloDiagnostico=true (entorno Diagnostico) o con el agente en modo APAGADO o SOMBRA, la ejecución queda deshabilitada.
 *   Los ejecutores corren con la identidad SQL de escritura del agente y sus parámetros se validan contra el esquema del catálogo.
 */

using System.Text.Json;
using System.Text.RegularExpressions;

namespace SistemaTicketsInteligente.Api.BLL.Agente;

public sealed partial class AsistenteTIBLL
{
    private static readonly Regex EjecutorPermitido = new(@"^dbo\.Usp_TI_AgenteAccion_[A-Za-z0-9_]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Conserva el expediente y finaliza la investigación sin cambios; devuelve el informe Markdown para descargarlo.</summary>
    public async Task<string> GrabarInformacionAsync(string usuario, string area, long sesionNumero, CancellationToken ct)
    {
        var contexto = await ObtenerContextoAsync(usuario, area, sesionNumero, ct);
        if (!contexto.Sesion.InformeDisponible) throw new InvalidOperationException("Primero debe completarse la investigación y generar el informe técnico.");
        var informe = await baseDatos.EscalarAsync("dbo.Usp_TI_Agente_GrabarInformacion", p =>
        {
            Sesion(p, usuario, sesionNumero);
            p.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = contexto.Sesion.IdCorrelacion;
        }, ct);
        return informe?.ToString() ?? throw new InvalidOperationException("El informe técnico no se encuentra disponible.");
    }

    /// <summary>Comprobaciones previas de la acción propuesta, sin ejecutarla.</summary>
    public async Task<List<AgenteTIComprobacion>> DryRunAsync(string usuario, string area, long sesion, CancellationToken ct)
    {
        await ObtenerContextoAsync(usuario, area, sesion, ct);
        return await baseDatos.LeerAsync("dbo.Usp_TI_Agente_DryRun", p => Sesion(p, usuario, area, sesion),
            lector => lector.ListaAsync(f => new AgenteTIComprobacion(f.Texto("Codigo"), f.Texto("Descripcion"), f.Texto("Resultado")), ct), ct);
    }

    /// <summary>
    /// Ejecuta la acción propuesta con sus parámetros reales dentro de una transacción que siempre se revierte.
    /// Comprueba precondiciones, postcondiciones y filas afectadas sin persistir nada; no requiere aprobación.
    /// </summary>
    public async Task<AgenteTISimulacionRespuesta> SimularCambioAsync(string usuario, string area, long sesionNumero, CancellationToken ct)
    {
        var contexto = await ObtenerPropiaAsync(usuario, area, sesionNumero, ct);
        var ejecutor = await baseDatos.LeerAsync("dbo.Usp_TI_Agente_ObtenerEjecutorSimulacion", p => Sesion(p, usuario, area, sesionNumero),
            lector => lector.FilaAsync(f => new AgenteTIEjecutorSimulacion
            {
                IncidenciaNumero = f.Texto("IncidenciaNumero"), AccionCodigo = f.Texto("AccionCodigo"), ParametrosJson = f.Texto("ParametrosJson"),
                Procedimiento = f.Texto("Procedimiento"), MaximoFilas = f.Entero("MaximoFilas")
            }, ct), ct)
            ?? throw new InvalidOperationException("La acción propuesta no tiene un ejecutor autorizado instalado; no puede simularse.");

        var respuesta = new AgenteTISimulacionRespuesta { SesionNumero = sesionNumero, AccionCodigo = ejecutor.AccionCodigo, ParametrosJson = ejecutor.ParametrosJson };
        try
        {
            var resultado = await EjecutarEjecutorAsync(ejecutor.Procedimiento, usuario, area, ejecutor.IncidenciaNumero,
                ejecutor.ParametrosJson, contexto.Sesion.IdCorrelacion, ejecutor.MaximoFilas, simular: true, ct);
            respuesta.Exito = true;
            respuesta.FilasAfectadas = resultado.FilasAfectadas;
            respuesta.ResultadoJson = resultado.ResultadoJson;
            respuesta.Mensaje = $"La acción se ejecutaría correctamente: {resultado.FilasAfectadas} fila(s) afectada(s) y postcondiciones confirmadas. La transacción fue revertida; no se modificó información.";
        }
        catch (InvalidOperationException ex)
        {
            respuesta.Exito = false;
            respuesta.Mensaje = $"La acción no podría aplicarse: {Limitar(ex.Message, 1500)} La transacción fue revertida.";
        }
        catch (SqlException ex)
        {
            logger.LogWarning(ex, "La simulación de la sesión {Sesion} falló por un error técnico.", sesionNumero);
            respuesta.Exito = false;
            respuesta.Mensaje = "La simulación falló por un error técnico del ejecutor. La transacción fue revertida; revisa el procedimiento antes de ejecutar.";
        }

        await baseDatos.EjecutarAsync("dbo.Usp_TI_Agente_RegistrarSimulacion", p =>
        {
            Sesion(p, usuario, sesionNumero);
            p.Add("@lExito", SqlDbType.Bit).Value = respuesta.Exito;
            p.Add("@cParametrosJson", SqlDbType.NVarChar, -1).Value = ejecutor.ParametrosJson;
            p.Add("@cResultadoJson", SqlDbType.NVarChar, -1).Value = BaseDatos.Opcional(respuesta.Exito ? respuesta.ResultadoJson : null);
            p.Add("@nFilasAfectadas", SqlDbType.Int).Value = BaseDatos.Opcional(respuesta.FilasAfectadas);
            p.Add("@cError", SqlDbType.NVarChar, 2000).Value = BaseDatos.Opcional(respuesta.Exito ? null : respuesta.Mensaje);
        }, ct);
        return respuesta;
    }

    public async Task<AgenteTIDecisionRespuesta> RealizarCambioAsync(string usuario, string area, long sesionNumero, RealizarCambioAgenteTISolicitud solicitud, CancellationToken ct)
    {
        if (!solicitud.Confirmar) throw new ArgumentException("La ejecución requiere confirmación explícita de TI.");
        return await EjecutarCambioAsync(usuario, area, sesionNumero, autonoma: false, ct);
    }

    /// <summary>
    /// Ejecución sin humano (plan de mejoras §3.3): solo si PoliticaAutonomia no deja ningún motivo; las precondiciones se confirman con una
    /// simulación revertida inmediatamente antes y PrepararCambio vuelve a comprobar todo en la base. Fuera del modo AUTONOMO no hace nada;
    /// si algo no se cumple, el caso queda para la decisión de TI, como siempre.
    /// </summary>
    private async Task EjecutarSiPoliticaPermiteAsync(string usuario, string area, long sesion, CancellationToken ct)
    {
        var datos = await baseDatos.LeerAsync("dbo.Usp_TI_Agente_DatosAutonomia", p => p.Add("@nSesionNumero", SqlDbType.BigInt).Value = sesion,
            lector => lector.FilaAsync(f => new AgenteTIDatosAutonomia
            {
                SesionNumero = f.Largo("SesionNumero"), UsuarioTI = f.Texto("UsuarioTI"), AreaTI = f.Texto("AreaTI"), EstadoSesion = f.Texto("EstadoSesion"),
                Confianza = f.DecimalNulo("Confianza"), AccionCodigo = f.Texto("AccionCodigo"), ParametrosJson = f.Texto("ParametrosJson"),
                SolicitudPendiente = f.Booleano("SolicitudPendiente"), IncidenciaNumero = f.Texto("IncidenciaNumero"), TipoTicket = f.Texto("TipoTicket"),
                SubTipo = f.Texto("SubTipo"), EstadoTicket = f.Texto("EstadoTicket"), PerfilSolicitante = f.Texto("PerfilSolicitante"),
                AccionTipo = f.Texto("AccionTipo"), AccionEstado = f.Texto("AccionEstado"), RequiereAprobacion = f.Booleano("RequiereAprobacion"),
                Reversible = f.Booleano("Reversible"), NivelRiesgo = f.Texto("NivelRiesgo"), TieneEjecutor = f.Booleano("TieneEjecutor"),
                ParametrosEsquemaJson = f.Texto("ParametrosEsquemaJson"), ModoPolitica = f.Texto("ModoPolitica"), ConfianzaMinima = f.DecimalNulo("ConfianzaMinima"),
                EstadoPolitica = f.Texto("EstadoPolitica"), ModoAgente = f.Texto("ModoAgente"), RiesgoMaximo = f.Texto("RiesgoMaximo")
            }, ct), ct);
        if (datos is null || datos.ModoAgente != "AUTONOMO" || string.IsNullOrWhiteSpace(datos.AccionCodigo)) return;

        var evaluacion = PoliticaAutonomia.Evaluar(datos);
        await RegistrarEventoServidorAsync(sesion, "EVALUACION_AUTONOMIA",
            evaluacion.Permitida
                ? $"La política de TI libera {datos.AccionCodigo} para ejecución autónoma; se simula antes de ejecutar."
                : $"{datos.AccionCodigo} queda para la decisión de TI: {string.Join(" ", evaluacion.Motivos)}",
            new { accionCodigo = datos.AccionCodigo, permitida = evaluacion.Permitida, motivos = evaluacion.Motivos }, ct);
        if (!evaluacion.Permitida) return;

        try
        {
            var simulacion = await SimularCambioAsync(usuario, area, sesion, ct);
            if (!simulacion.Exito)
            {
                await RegistrarEventoServidorAsync(sesion, "EJECUCION_AUTONOMA", $"No se ejecutó: la simulación previa no confirmó las precondiciones. {simulacion.Mensaje}", new { ejecutado = false }, ct);
                return;
            }
            var resultado = await EjecutarCambioAsync(usuario, area, sesion, autonoma: true, ct);
            await RegistrarEventoServidorAsync(sesion, "EJECUCION_AUTONOMA", resultado.Mensaje, new { ejecutado = resultado.Ejecutado, estado = resultado.Estado }, ct);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("La ejecución autónoma de la sesión {Sesion} no se aplicó: {Motivo}", sesion, ex.Message);
            await RegistrarEventoServidorAsync(sesion, "EJECUCION_AUTONOMA", $"La ejecución autónoma no se aplicó: {ex.Message}", new { ejecutado = false }, CancellationToken.None);
        }
    }

    private async Task<AgenteTIDecisionRespuesta> EjecutarCambioAsync(string usuario, string area, long sesionNumero, bool autonoma, CancellationToken ct)
    {
        if (configuration.GetValue<bool>("AgenteTI:SoloDiagnostico")) throw new InvalidOperationException("Este entorno es de diagnóstico: la ejecución de cambios está deshabilitada.");
        if ((await control.ObtenerAsync(ct)).EjecucionDeshabilitada)
            throw new InvalidOperationException("El agente está en modo APAGADO o SOMBRA: TI deshabilitó la ejecución de cambios. Puedes grabar la información.");
        var contexto = await ObtenerPropiaAsync(usuario, area, sesionNumero, ct);
        if (!contexto.Sesion.InformeDisponible || string.IsNullOrWhiteSpace(contexto.Sesion.AccionCodigo)) throw new InvalidOperationException("El diagnóstico no contiene una acción correctiva catalogada.");
        if (string.IsNullOrWhiteSpace(contexto.Sesion.IncidenciaNumero)) throw new InvalidOperationException("La investigación debe estar asociada a una incidencia antes de ejecutar un cambio.");
        var correlacion = contexto.Sesion.IdCorrelacion;

        // El procedimiento decide si se puede ejecutar ya o si primero hace falta la aprobación de otro operador.
        var preparacion = await baseDatos.LeerAsync("dbo.Usp_TI_Agente_PrepararCambio", p =>
        {
            Sesion(p, usuario, area, sesionNumero);
            p.Add("@cClaveIdempotencia", SqlDbType.UniqueIdentifier).Value = correlacion;
            p.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = correlacion;
            p.Add("@lAutonoma", SqlDbType.Bit).Value = autonoma;
        }, lector => lector.FilaAsync(f => new AgenteTIPreparacionCambio
        {
            MaximoFilas = f.Booleano("PuedeEjecutar") ? f.Entero("MaximoFilas") : 0,
            ParametrosEsquemaJson = f.TieneColumna("ParametrosEsquemaJson") ? f.Texto("ParametrosEsquemaJson") : string.Empty,
            Estado = f.Texto("Estado"), Mensaje = f.Texto("Mensaje"), PuedeEjecutar = f.Booleano("PuedeEjecutar"),
            ProcedimientoEjecutor = f.Texto("ProcedimientoEjecutor"), EjecucionSecuencia = f.EnteroNulo("EjecucionSecuencia"),
            SolicitudAprobacionSecuencia = f.EnteroNulo("SolicitudAprobacionSecuencia"), ParametrosJson = f.Texto("ParametrosJson")
        }, ct), ct) ?? throw new InvalidOperationException("No fue posible preparar la acción controlada.");

        if (!preparacion.PuedeEjecutar)
            return new AgenteTIDecisionRespuesta
            {
                SesionNumero = sesionNumero, Estado = preparacion.Estado, Mensaje = preparacion.Mensaje,
                SolicitudAprobacionSecuencia = preparacion.SolicitudAprobacionSecuencia, Ejecutado = false
            };

        var ejecucion = preparacion.EjecucionSecuencia ?? throw new InvalidOperationException("La ejecución no posee una secuencia de control válida.");

        // Deja en la auditoría de la sesión el resultado de la ejecución: éxito con sus filas o el motivo del fallo.
        Task RegistrarResultadoAsync(bool exito, AgenteTIEjecucionResultado? resultado, string? error, CancellationToken token) =>
            baseDatos.EjecutarAsync("dbo.Usp_TI_Agente_FinalizarCambio", p =>
            {
                Sesion(p, usuario, sesionNumero);
                p.Add("@nEjecucionSecuencia", SqlDbType.Int).Value = ejecucion;
                p.Add("@lExito", SqlDbType.Bit).Value = exito;
                p.Add("@cResultadoJson", SqlDbType.NVarChar, -1).Value = BaseDatos.Opcional(resultado?.ResultadoJson);
                p.Add("@nFilasAfectadas", SqlDbType.Int).Value = BaseDatos.Opcional(resultado?.FilasAfectadas);
                p.Add("@cError", SqlDbType.NVarChar, 2000).Value = BaseDatos.Opcional(error);
                p.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = correlacion;
            }, token);

        try
        {
            // Los parámetros aprobados deben cumplir el esquema del ejecutor: el modelo nunca decide la forma de lo que se ejecuta.
            if (!string.IsNullOrWhiteSpace(preparacion.ParametrosEsquemaJson)
                && !InvestigadorAgenteTI.ValidarParametros(preparacion.ParametrosEsquemaJson, preparacion.ParametrosJson, out _, out var motivoEsquema))
                throw new InvalidOperationException($"Los parámetros no cumplen el esquema del ejecutor: {motivoEsquema}");
            // El ejecutor solo confirma la transacción si verificó sus postcondiciones (validacionPosterior = true).
            var resultado = await EjecutarEjecutorAsync(preparacion.ProcedimientoEjecutor, usuario, area, contexto.Sesion.IncidenciaNumero,
                preparacion.ParametrosJson, correlacion, preparacion.MaximoFilas, simular: false, ct);
            await RegistrarResultadoAsync(true, resultado, null, ct);
            return new AgenteTIDecisionRespuesta
            {
                SesionNumero = sesionNumero, Estado = "CAMBIO_VALIDADO", Ejecutado = true,
                Mensaje = autonoma
                    ? "El agente ejecutó la acción que la política de TI libera y el procedimiento confirmó la validación posterior. TI debe confirmar la solución con el usuario."
                    : "La acción autorizada fue ejecutada y el procedimiento confirmó la validación posterior. La trazabilidad quedó registrada."
            };
        }
        catch (Exception ex)
        {
            using var limpieza = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var error = ex is InvalidOperationException ? Limitar(ex.Message, 1500) : ex.GetType().Name + ": resultado pendiente de revisión TI";
            try { await RegistrarResultadoAsync(false, null, error, limpieza.Token); }
            catch (Exception registroError) { logger.LogError(registroError, "No se pudo registrar el resultado de la ejecución de sesión {Sesion}.", sesionNumero); }
            if (ex is OperationCanceledException) throw;
            var motivo = ex is InvalidOperationException ? $" Motivo: {ex.Message}" : string.Empty;
            throw new InvalidOperationException($"La ejecución no se aplicó y requiere revisión de TI.{motivo} No la repitas sin verificar el resultado y la auditoría.", ex);
        }
    }

    /// <summary>TI confirma que la solución funcionó; cierra el ciclo de la investigación.</summary>
    public async Task ValidarSolucionAsync(string usuario, string area, long sesion, RealizarCambioAgenteTISolicitud solicitud, CancellationToken ct)
    {
        if (!solicitud.Confirmar) throw new ArgumentException("TI debe confirmar que verificó la solución.");
        await ObtenerContextoAsync(usuario, area, sesion, ct);
        await baseDatos.EjecutarAsync("dbo.Usp_TI_Agente_ValidarSolucion", p => Sesion(p, usuario, area, sesion), ct);
    }

    /// <summary>Convierte la investigación en un borrador de la base de conocimiento; devuelve su código.</summary>
    public async Task<string> CrearBorradorAsync(string usuario, string area, long sesion, CancellationToken ct)
    {
        await ObtenerContextoAsync(usuario, area, sesion, ct);
        var codigo = await baseDatos.EscalarAsync("dbo.Usp_TI_Agente_CrearBorrador", p => Sesion(p, usuario, area, sesion), ct);
        return codigo?.ToString() ?? throw new InvalidOperationException("No se pudo crear el borrador.");
    }

    /// <summary>
    /// Ejecuta un procedimiento del catálogo dentro de una transacción. Se confirma solo si devuelve una única fila con
    /// validacionPosterior=true y sin exceder el máximo de filas; al simular se revierte siempre.
    /// </summary>
    private async Task<AgenteTIEjecucionResultado> EjecutarEjecutorAsync(string procedimiento, string usuario, string area, string incidenciaNumero,
        string parametrosJson, Guid idCorrelacion, int maximoFilas, bool simular, CancellationToken ct)
    {
        if (!EjecutorPermitido.IsMatch(procedimiento)) throw new InvalidOperationException("El procedimiento ejecutor no pertenece a la lista permitida del agente.");

        await using var conexion = baseDatos.CrearConexionAgenteEscritura();
        await conexion.OpenAsync(ct);
        await using var transaccion = (SqlTransaction)await conexion.BeginTransactionAsync(ct);
        await using var comando = BaseDatos.Comando(conexion, transaccion, procedimiento, p =>
        {
            Identidad(p, usuario, area);
            p.Add("@cIncidenciaNumero", SqlDbType.VarChar, 12).Value = incidenciaNumero;
            p.Add("@cParametrosJson", SqlDbType.NVarChar, -1).Value = string.IsNullOrWhiteSpace(parametrosJson) ? "{}" : parametrosJson;
            p.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = idCorrelacion;
        });
        comando.CommandTimeout = 60;
        try
        {
            AgenteTIEjecucionResultado resultado;
            await using (var lector = await comando.ExecuteReaderAsync(ct))
            {
                if (!await lector.ReadAsync(ct)) throw new InvalidOperationException("El ejecutor no devolvió sus postcondiciones.");
                resultado = new() { ResultadoJson = lector.Texto("ResultadoJson"), FilasAfectadas = lector.Entero("FilasAfectadas") };
                if (lector["FilasAfectadas"] is DBNull || await lector.ReadAsync(ct)) throw new InvalidOperationException("El contrato de resultado del ejecutor no es válido.");
            }
            using var json = JsonDocument.Parse(resultado.ResultadoJson);
            if (json.RootElement.ValueKind != JsonValueKind.Object || !json.RootElement.TryGetProperty("validacionPosterior", out var validacion) || validacion.ValueKind != JsonValueKind.True)
                throw new InvalidOperationException("No se confirmaron las postcondiciones; el cambio se revierte.");
            if (maximoFilas is < 1 or > 1000 || resultado.FilasAfectadas < 0 || resultado.FilasAfectadas > maximoFilas)
                throw new InvalidOperationException("El ejecutor excedió el límite de filas autorizado; el cambio se revierte.");
            if (simular) await transaccion.RollbackAsync(ct);
            else await transaccion.CommitAsync(ct);
            return resultado;
        }
        catch (Exception ex)
        {
            try { await transaccion.RollbackAsync(CancellationToken.None); } catch (Exception) { /* La conexión puede haberse cerrado. */ }
            // El ejecutor rechaza precondiciones con errores de negocio; su mensaje explica a TI por qué no se aplicó el cambio.
            if (ex is SqlException sql && BaseDatos.EsErrorDeNegocio(sql)) throw new InvalidOperationException(sql.Message, sql);
            throw;
        }
    }
}
