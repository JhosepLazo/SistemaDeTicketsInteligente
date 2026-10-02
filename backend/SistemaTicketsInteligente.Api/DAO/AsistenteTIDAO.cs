/**
 * Archivo: AsistenteTIDAO.cs
 * Objetivo: Ejecutar los Stored Procedures del Agente de Ingeniería Autónomo.
 * Responsabilidad: Persistir sesiones/evidencias, recuperar contexto autorizado y ejecutar únicamente procedimientos previamente catalogados.
 * Dependencias: ConexionSqlServer, Microsoft.Data.SqlClient, AsistenteTIDTO y dbo.Usp_TI_Agente_*.
 * Flujo: AsistenteTIBLL -> AsistenteTIDAO -> Stored Procedures -> SQL Server.
 * Consideraciones: No ejecuta SQL generado por IA; un ejecutor de cambio debe provenir de la lista permitida TI_AgenteAccionEjecutor y cumplir el prefijo contractual.
 */

using System.Data;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.DAO;

public sealed class AsistenteTIDAO
{
    private static readonly Regex ProcedimientoPermitido = new(@"^dbo\.Usp_TI_AgenteAccion_[A-Za-z0-9_]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly ConexionSqlServer conexionSqlServer;

    public AsistenteTIDAO(ConexionSqlServer conexionSqlServer)
    {
        this.conexionSqlServer = conexionSqlServer;
    }

    public async Task<AgenteTISesion> CrearSesionAsync(string usuario, string area, CrearInvestigacionTISolicitud solicitud, Guid idCorrelacion, CancellationToken ct)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Agente_CrearSesion", conexion) { CommandType = CommandType.StoredProcedure };
        AgregarIdentidad(comando, usuario, area);
        comando.Parameters.Add("@cIncidenciaNumero", SqlDbType.VarChar, 12).Value = string.IsNullOrWhiteSpace(solicitud.IncidenciaNumero) ? DBNull.Value : solicitud.IncidenciaNumero.Trim().ToUpperInvariant();
        comando.Parameters.Add("@cDescripcion", SqlDbType.NVarChar, 1200).Value = solicitud.Descripcion.Trim();
        comando.Parameters.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = idCorrelacion;

        await conexion.OpenAsync(ct);
        try
        {
            await using var lector = await comando.ExecuteReaderAsync(ct);
            if (!await lector.ReadAsync(ct)) throw new InvalidOperationException("No fue posible crear la sesión de investigación.");
            return new AgenteTISesion
            {
                SesionNumero = LeerLong(lector, "SesionNumero"),
                IncidenciaNumero = LeerCadena(lector, "IncidenciaNumero"),
                IdCorrelacion = LeerGuid(lector, "IdCorrelacion"),
                DescripcionInicial = LeerCadena(lector, "DescripcionInicial"),
                Estado = LeerCadena(lector, "Estado"),
                FechaInicio = LeerFecha(lector, "FechaInicio")
            };
        }
        catch (SqlException ex) when (EsErrorFuncional(ex))
        {
            throw new InvalidOperationException(ex.Message, ex);
        }
    }

    public Task RegistrarEventoAsync(string usuario, long sesionNumero, RegistrarEventoAgenteTISolicitud solicitud, CancellationToken ct) =>
        EjecutarAsync("dbo.Usp_TI_Agente_RegistrarEvento", comando =>
        {
            comando.Parameters.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;
            comando.Parameters.Add("@nSesionNumero", SqlDbType.BigInt).Value = sesionNumero;
            comando.Parameters.Add("@cTipo", SqlDbType.VarChar, 40).Value = solicitud.Tipo.Trim();
            comando.Parameters.Add("@cFuente", SqlDbType.VarChar, 30).Value = solicitud.Fuente.Trim();
            comando.Parameters.Add("@cContenido", SqlDbType.NVarChar, -1).Value = solicitud.Contenido.Trim();
            comando.Parameters.Add("@cDatosJson", SqlDbType.NVarChar, -1).Value = string.IsNullOrWhiteSpace(solicitud.DatosJson) ? DBNull.Value : solicitud.DatosJson;
        }, ct);

    public Task FinalizarObservacionAsync(string usuario, long sesionNumero, FinalizarObservacionAgenteTISolicitud solicitud, CancellationToken ct) =>
        EjecutarAsync("dbo.Usp_TI_Agente_FinalizarObservacion", comando =>
        {
            comando.Parameters.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;
            comando.Parameters.Add("@nSesionNumero", SqlDbType.BigInt).Value = sesionNumero;
            comando.Parameters.Add("@cResumenObservacion", SqlDbType.NVarChar, -1).Value = solicitud.ResumenObservacion.Trim();
            comando.Parameters.Add("@cProcesoObservado", SqlDbType.NVarChar, -1).Value = solicitud.ProcesoObservado.Trim();
            comando.Parameters.Add("@cErrorObservado", SqlDbType.NVarChar, 1000).Value = string.IsNullOrWhiteSpace(solicitud.ErrorObservado) ? DBNull.Value : solicitud.ErrorObservado.Trim();
        }, ct);

    public async Task<AgenteTIContextoInvestigacion> ObtenerContextoAsync(string usuario, string area, long sesionNumero, CancellationToken ct)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Agente_ObtenerContexto", conexion) { CommandType = CommandType.StoredProcedure };
        AgregarIdentidad(comando, usuario, area);
        comando.Parameters.Add("@nSesionNumero", SqlDbType.BigInt).Value = sesionNumero;

        await conexion.OpenAsync(ct);
        try
        {
            await using var lector = await comando.ExecuteReaderAsync(ct);
            if (!await lector.ReadAsync(ct)) throw new KeyNotFoundException("No se encontró la sesión de investigación.");

            var contexto = new AgenteTIContextoInvestigacion
            {
                Sesion = new AgenteTISesion
                {
                    SolucionValidada = LeerBooleano(lector, "SolucionValidada"),
                    ConocimientoCodigo = LeerCadena(lector, "ConocimientoCodigo"),
                    SesionNumero = LeerLong(lector, "SesionNumero"),
                    IncidenciaNumero = LeerCadena(lector, "IncidenciaNumero"),
                    IdCorrelacion = LeerGuid(lector, "IdCorrelacion"),
                    DescripcionInicial = LeerCadena(lector, "DescripcionInicial"),
                    Estado = LeerCadena(lector, "Estado"),
                    ResumenObservacion = LeerCadena(lector, "ResumenObservacion"),
                    ProcesoObservado = LeerCadena(lector, "ProcesoObservado"),
                    ErrorObservado = LeerCadena(lector, "ErrorObservado"),
                    Diagnostico = LeerCadena(lector, "Diagnostico"),
                    CausaProbable = LeerCadena(lector, "CausaProbable"),
                    SolucionPropuesta = LeerCadena(lector, "SolucionPropuesta"),
                    Confianza = LeerDecimalNullable(lector, "Confianza"),
                    AccionCodigo = LeerCadena(lector, "AccionCodigo"),
                    NivelRiesgo = LeerCadena(lector, "NivelRiesgo"),
                    ParametrosJson = LeerCadena(lector, "ParametrosJson"),
                    Decision = LeerCadena(lector, "Decision"),
                    SolicitudAprobacionSecuencia = LeerEnteroNullable(lector, "SolicitudAprobacionSecuencia"),
                    FechaInicio = LeerFecha(lector, "FechaInicio"),
                    FechaDiagnostico = LeerFechaNullable(lector, "FechaDiagnostico"),
                    FechaDecision = LeerFechaNullable(lector, "FechaDecision"),
                    InformeDisponible = LeerBooleano(lector, "InformeDisponible"),
                    UsuarioTI = LeerCadena(lector, "UsuarioTI"),
                    NombreOperador = LeerCadena(lector, "NombreOperador"),
                    EsPropietario = LeerBooleano(lector, "EsPropietario"),
                    EvidenciasJson = LeerCadena(lector, "EvidenciasJson"),
                    InformeMarkdown = LeerCadena(lector, "InformeMarkdown")
                },
                Ticket = new AgenteTITicketContexto
                {
                    IncidenciaNumero = LeerCadena(lector, "IncidenciaNumero"),
                    Titulo = LeerCadena(lector, "Titulo"),
                    Detalle = LeerCadena(lector, "Detalle"),
                    MensajeError = LeerCadena(lector, "MensajeError"),
                    Estado = LeerCadena(lector, "EstadoIncidencia"),
                    Linea = LeerCadena(lector, "Linea"),
                    Item = LeerCadena(lector, "Item"),
                    Tipo = LeerCadena(lector, "Tipo"),
                    SubTipo = LeerCadena(lector, "SubTipo"),
                    Categoria = LeerCadena(lector, "Categoria"),
                    UsuarioSolicitante = LeerCadena(lector, "UsuarioSolicitante"),
                    FechaRegistro = LeerFechaNullable(lector, "FechaRegistro")
                }
            };

            await lector.NextResultAsync(ct);
            while (await lector.ReadAsync(ct))
                contexto.Documentos.Add(new AgenteTIDocumento
                {
                    CompaniaSocio = LeerCadena(lector, "CompaniaSocio"), TipoDocumento = LeerCadena(lector, "TipoDocumento"),
                    NumeroDocumento = LeerCadena(lector, "NumeroDocumento"), Descripcion = LeerCadena(lector, "Descripcion")
                });

            await lector.NextResultAsync(ct);
            while (await lector.ReadAsync(ct))
                contexto.Mensajes.Add(new AgenteTIMensaje
                {
                    TipoAutor = LeerCadena(lector, "TipoAutor"), Autor = LeerCadena(lector, "Autor"), Contenido = LeerCadena(lector, "Contenido"),
                    FechaMensaje = LeerFecha(lector, "FechaMensaje"), EsInterno = LeerBooleano(lector, "EsInterno")
                });

            await lector.NextResultAsync(ct);
            while (await lector.ReadAsync(ct))
                contexto.Conocimientos.Add(new AgenteTIConocimiento
                {
                    ConocimientoCodigo = LeerCadena(lector, "ConocimientoCodigo"), Titulo = LeerCadena(lector, "Titulo"),
                    Problema = LeerCadena(lector, "Problema"), Sintomas = LeerCadena(lector, "Sintomas"), Causa = LeerCadena(lector, "Causa"),
                    Solucion = LeerCadena(lector, "Solucion"), Procedimiento = LeerCadena(lector, "Procedimiento"), PuntajeContextual = LeerEntero(lector, "PuntajeContextual")
                });

            await lector.NextResultAsync(ct);
            while (await lector.ReadAsync(ct))
                contexto.Acciones.Add(new AgenteTIAccionDisponible
                {
                    AccionCodigo = LeerCadena(lector, "AccionCodigo"), Nombre = LeerCadena(lector, "Nombre"), Descripcion = LeerCadena(lector, "Descripcion"),
                    Tipo = LeerCadena(lector, "Tipo"), NivelRiesgo = LeerCadena(lector, "NivelRiesgo"), RequiereAprobacion = LeerBooleano(lector, "RequiereAprobacion"),
                    TieneEjecutor = LeerBooleano(lector, "TieneEjecutor"), ParametrosDescripcion = LeerCadena(lector, "ParametrosDescripcion")
                });

            await lector.NextResultAsync(ct);
            while (await lector.ReadAsync(ct))
                contexto.Auditoria.Add(new AgenteTIAuditoria
                {
                    Entidad = LeerCadena(lector, "Entidad"), Registro = LeerCadena(lector, "Registro"), Evento = LeerCadena(lector, "Evento"),
                    Resultado = LeerCadena(lector, "Resultado"), DetalleJson = LeerCadena(lector, "DetalleJson"), IdCorrelacion = LeerGuid(lector, "IdCorrelacion"), Fecha = LeerFecha(lector, "Fecha")
                });

            await lector.NextResultAsync(ct);
            while (await lector.ReadAsync(ct))
                contexto.Eventos.Add(new AgenteTIEvento
                {
                    OrigenServidor = LeerBooleano(lector, "OrigenServidor"),
                    Secuencia = LeerEntero(lector, "Secuencia"), Tipo = LeerCadena(lector, "Tipo"), Fuente = LeerCadena(lector, "Fuente"),
                    Contenido = LeerCadena(lector, "Contenido"), DatosJson = LeerCadena(lector, "DatosJson"), Fecha = LeerFecha(lector, "Fecha")
                });

            return contexto;
        }
        catch (SqlException ex) when (EsErrorFuncional(ex))
        {
            throw new InvalidOperationException(ex.Message, ex);
        }
    }

    public Task GuardarDiagnosticoAsync(string usuario, long sesionNumero, AgenteTIDiagnosticoRespuesta diagnostico, string parametrosJson, string evidenciasJson, string informeMarkdown, Guid idCorrelacion, CancellationToken ct) =>
        EjecutarAsync("dbo.Usp_TI_Agente_GuardarDiagnostico", comando =>
        {
            comando.Parameters.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;
            comando.Parameters.Add("@nSesionNumero", SqlDbType.BigInt).Value = sesionNumero;
            comando.Parameters.Add("@cDiagnostico", SqlDbType.NVarChar, -1).Value = diagnostico.Diagnostico;
            comando.Parameters.Add("@cCausaProbable", SqlDbType.NVarChar, -1).Value = diagnostico.CausaProbable;
            comando.Parameters.Add("@cSolucionPropuesta", SqlDbType.NVarChar, -1).Value = diagnostico.SolucionPropuesta;
            comando.Parameters.Add("@nConfianza", SqlDbType.Decimal).Value = diagnostico.Confianza;
            comando.Parameters["@nConfianza"].Precision = 5;
            comando.Parameters["@nConfianza"].Scale = 2;
            comando.Parameters.Add("@cAccionCodigo", SqlDbType.VarChar, 50).Value = diagnostico.Accion is null ? DBNull.Value : diagnostico.Accion.AccionCodigo;
            comando.Parameters.Add("@cNivelRiesgo", SqlDbType.VarChar, 20).Value = diagnostico.Accion is null ? DBNull.Value : diagnostico.Accion.NivelRiesgo;
            comando.Parameters.Add("@cParametrosJson", SqlDbType.NVarChar, -1).Value = string.IsNullOrWhiteSpace(parametrosJson) ? DBNull.Value : parametrosJson;
            comando.Parameters.Add("@cEvidenciasJson", SqlDbType.NVarChar, -1).Value = string.IsNullOrWhiteSpace(evidenciasJson) ? DBNull.Value : evidenciasJson;
            comando.Parameters.Add("@cInformeMarkdown", SqlDbType.NVarChar, -1).Value = informeMarkdown;
            comando.Parameters.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = idCorrelacion;
        }, ct);

    public async Task<string> GrabarInformacionAsync(string usuario, long sesionNumero, Guid idCorrelacion, CancellationToken ct)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Agente_GrabarInformacion", conexion) { CommandType = CommandType.StoredProcedure };
        comando.Parameters.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;
        comando.Parameters.Add("@nSesionNumero", SqlDbType.BigInt).Value = sesionNumero;
        comando.Parameters.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = idCorrelacion;
        await conexion.OpenAsync(ct);
        try
        {
            var resultado = await comando.ExecuteScalarAsync(ct);
            return resultado?.ToString() ?? throw new InvalidOperationException("El informe técnico no se encuentra disponible.");
        }
        catch (SqlException ex) when (EsErrorFuncional(ex))
        {
            throw new InvalidOperationException(ex.Message, ex);
        }
    }

    public async Task<AgenteTIPreparacionCambio> PrepararCambioAsync(string usuario, string area, long sesionNumero, Guid claveIdempotencia, Guid idCorrelacion, CancellationToken ct)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Agente_PrepararCambio", conexion) { CommandType = CommandType.StoredProcedure };
        AgregarIdentidad(comando, usuario, area);
        comando.Parameters.Add("@nSesionNumero", SqlDbType.BigInt).Value = sesionNumero;
        comando.Parameters.Add("@cClaveIdempotencia", SqlDbType.UniqueIdentifier).Value = claveIdempotencia;
        comando.Parameters.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = idCorrelacion;

        await conexion.OpenAsync(ct);
        try
        {
            await using var lector = await comando.ExecuteReaderAsync(ct);
            if (!await lector.ReadAsync(ct)) throw new InvalidOperationException("No fue posible preparar la acción controlada.");
            return new AgenteTIPreparacionCambio
            {
                MaximoFilas = LeerBooleano(lector, "PuedeEjecutar") ? LeerEntero(lector, "MaximoFilas") : 0,
                Estado = LeerCadena(lector, "Estado"), Mensaje = LeerCadena(lector, "Mensaje"), PuedeEjecutar = LeerBooleano(lector, "PuedeEjecutar"),
                ProcedimientoEjecutor = LeerCadena(lector, "ProcedimientoEjecutor"), EjecucionSecuencia = LeerEnteroNullable(lector, "EjecucionSecuencia"),
                SolicitudAprobacionSecuencia = LeerEnteroNullable(lector, "SolicitudAprobacionSecuencia"), ParametrosJson = LeerCadena(lector, "ParametrosJson")
            };
        }
        catch (SqlException ex) when (EsErrorFuncional(ex))
        {
            throw new InvalidOperationException(ex.Message, ex);
        }
    }

    public async Task<AgenteTIEjecucionResultado> EjecutarProcedimientoControladoAsync(string procedimiento, string usuario, string area, string incidenciaNumero, string parametrosJson, Guid idCorrelacion, int maximoFilas, CancellationToken ct)
    {
        if (!ProcedimientoPermitido.IsMatch(procedimiento)) throw new InvalidOperationException("El procedimiento ejecutor no pertenece a la lista permitida del agente.");

        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand(procedimiento, conexion) { CommandType = CommandType.StoredProcedure, CommandTimeout = 60 };
        AgregarIdentidad(comando, usuario, area);
        comando.Parameters.Add("@cIncidenciaNumero", SqlDbType.VarChar, 12).Value = incidenciaNumero;
        comando.Parameters.Add("@cParametrosJson", SqlDbType.NVarChar, -1).Value = string.IsNullOrWhiteSpace(parametrosJson) ? "{}" : parametrosJson;
        comando.Parameters.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = idCorrelacion;

        await conexion.OpenAsync(ct);
        await using var transaccion = (SqlTransaction)await conexion.BeginTransactionAsync(ct);
        comando.Transaction = transaccion;
        try
        {
            AgenteTIEjecucionResultado resultado;
            await using (var lector = await comando.ExecuteReaderAsync(ct))
            {
                if (!await lector.ReadAsync(ct)) throw new InvalidOperationException("El ejecutor no devolvió sus postcondiciones.");
                resultado = new() { ResultadoJson = LeerCadena(lector, "ResultadoJson"), FilasAfectadas = LeerEntero(lector, "FilasAfectadas") };
                if (lector["FilasAfectadas"] is DBNull || await lector.ReadAsync(ct)) throw new InvalidOperationException("El contrato de resultado del ejecutor no es válido.");
            }
            using var json = JsonDocument.Parse(resultado.ResultadoJson);
            if (json.RootElement.ValueKind != JsonValueKind.Object || !json.RootElement.TryGetProperty("validacionPosterior", out var validacion) || validacion.ValueKind != JsonValueKind.True)
                throw new InvalidOperationException("No se confirmaron las postcondiciones; el cambio se revierte.");
            if (maximoFilas is < 1 or > 1000 || resultado.FilasAfectadas < 0 || resultado.FilasAfectadas > maximoFilas)
                throw new InvalidOperationException("El ejecutor excedió el límite de filas autorizado; el cambio se revierte.");
            await transaccion.CommitAsync(ct);
            return resultado;
        }
        catch (Exception ex)
        {
            try { await transaccion.RollbackAsync(CancellationToken.None); } catch (Exception) { /* La conexion puede haberse cerrado. */ }
            // El ejecutor rechaza precondiciones con errores funcionales; su mensaje explica a TI por qué no se aplicó el cambio.
            if (ex is SqlException sql && sql.Number is >= 50500 and <= 50599) throw new InvalidOperationException(sql.Message, sql);
            throw;
        }
    }

    public Task FinalizarCambioAsync(string usuario, long sesionNumero, int ejecucionSecuencia, bool exito, string? resultadoJson, int? filasAfectadas, string? error, Guid idCorrelacion, CancellationToken ct) =>
        EjecutarAsync("dbo.Usp_TI_Agente_FinalizarCambio", comando =>
        {
            comando.Parameters.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;
            comando.Parameters.Add("@nSesionNumero", SqlDbType.BigInt).Value = sesionNumero;
            comando.Parameters.Add("@nEjecucionSecuencia", SqlDbType.Int).Value = ejecucionSecuencia;
            comando.Parameters.Add("@lExito", SqlDbType.Bit).Value = exito;
            comando.Parameters.Add("@cResultadoJson", SqlDbType.NVarChar, -1).Value = string.IsNullOrWhiteSpace(resultadoJson) ? DBNull.Value : resultadoJson;
            comando.Parameters.Add("@nFilasAfectadas", SqlDbType.Int).Value = filasAfectadas.HasValue ? filasAfectadas.Value : DBNull.Value;
            comando.Parameters.Add("@cError", SqlDbType.NVarChar, 2000).Value = string.IsNullOrWhiteSpace(error) ? DBNull.Value : error;
            comando.Parameters.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = idCorrelacion;
        }, ct);

    public Task CancelarAsync(string usuario, string area, long sesion, CancellationToken ct) =>
        EjecutarAsync("dbo.Usp_TI_Agente_Cancelar", c => AgregarSesion(c, usuario, area, sesion), ct);

    public async Task<Guid?> ResolverCorrelacionAsync(string usuario, string area, long sesion, CancellationToken ct)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Agente_Correlacion", conexion) { CommandType = CommandType.StoredProcedure };
        AgregarSesion(comando, usuario, area, sesion);
        await conexion.OpenAsync(ct);
        return await comando.ExecuteScalarAsync(ct) is Guid valor ? valor : null;
    }

    public Task VincularAsync(string usuario, string area, long sesion, string incidencia, CancellationToken ct) =>
        EjecutarAsync("dbo.Usp_TI_Agente_Vincular", c =>
        {
            AgregarSesion(c, usuario, area, sesion);
            c.Parameters.Add("@cIncidenciaNumero", SqlDbType.VarChar, 12).Value = incidencia;
        }, ct);

    public Task ValidarSolucionAsync(string usuario, string area, long sesion, CancellationToken ct) =>
        EjecutarAsync("dbo.Usp_TI_Agente_ValidarSolucion", c => AgregarSesion(c, usuario, area, sesion), ct);

    public Task RegistrarTelemetriaAsync(string usuario, long sesion, string contenido, string json, CancellationToken ct) =>
        EjecutarAsync("dbo.Usp_TI_Agente_Telemetria", c =>
        {
            c.Parameters.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;
            c.Parameters.Add("@nSesionNumero", SqlDbType.BigInt).Value = sesion;
            c.Parameters.Add("@cContenido", SqlDbType.NVarChar, -1).Value = contenido;
            c.Parameters.Add("@cDatosJson", SqlDbType.NVarChar, -1).Value = json;
        }, ct);

    public async Task<List<AgenteTISesion>> ListarAsync(string usuario, string area, bool todas, CancellationToken ct)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Agente_Listar", conexion) { CommandType = CommandType.StoredProcedure };
        AgregarIdentidad(comando, usuario, area);
        comando.Parameters.Add("@lTodas", SqlDbType.Bit).Value = todas;
        await conexion.OpenAsync(ct);
        try
        {
            await using var lector = await comando.ExecuteReaderAsync(ct);
            var sesiones = new List<AgenteTISesion>();
            while (await lector.ReadAsync(ct)) sesiones.Add(new AgenteTISesion
            {
                SesionNumero = LeerLong(lector, "SesionNumero"), IncidenciaNumero = LeerCadena(lector, "IncidenciaNumero"),
                IdCorrelacion = LeerGuid(lector, "IdCorrelacion"), Estado = LeerCadena(lector, "Estado"),
                DescripcionInicial = LeerCadena(lector, "DescripcionInicial"), FechaInicio = LeerFecha(lector, "FechaInicio"),
                SolucionValidada = LeerBooleano(lector, "SolucionValidada"), ConocimientoCodigo = LeerCadena(lector, "ConocimientoCodigo"),
                Confianza = LeerDecimalNullable(lector, "Confianza"), AccionCodigo = LeerCadena(lector, "AccionCodigo"),
                UsuarioTI = LeerCadena(lector, "UsuarioTI"), NombreOperador = LeerCadena(lector, "NombreOperador"), EsPropietario = LeerBooleano(lector, "EsPropietario")
            });
            return sesiones;
        }
        catch (SqlException ex) when (EsErrorFuncional(ex))
        {
            throw new InvalidOperationException(ex.Message, ex);
        }
    }

    public Task ReasignarAsync(string usuario, string area, long sesion, string nuevoUsuario, CancellationToken ct) =>
        EjecutarAsync("dbo.Usp_TI_Agente_Reasignar", c =>
        {
            AgregarSesion(c, usuario, area, sesion);
            c.Parameters.Add("@cNuevoUsuario", SqlDbType.VarChar, 20).Value = nuevoUsuario;
        }, ct);

    public async Task<AgenteTICatalogos> ObtenerCatalogosAsync(string usuario, string area, CancellationToken ct)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Agente_Catalogos", conexion) { CommandType = CommandType.StoredProcedure };
        AgregarIdentidad(comando, usuario, area);
        await conexion.OpenAsync(ct);
        try
        {
            await using var lector = await comando.ExecuteReaderAsync(ct);
            var catalogos = new AgenteTICatalogos();
            while (await lector.ReadAsync(ct)) catalogos.Areas.Add(new(LeerCadena(lector, "Codigo"), LeerCadena(lector, "Descripcion")));
            await lector.NextResultAsync(ct);
            while (await lector.ReadAsync(ct)) catalogos.Operadores.Add(new(LeerCadena(lector, "Codigo"), LeerCadena(lector, "Descripcion")));
            return catalogos;
        }
        catch (SqlException ex) when (EsErrorFuncional(ex))
        {
            throw new InvalidOperationException(ex.Message, ex);
        }
    }

    public async Task<List<AgenteTIInvestigacionTicket>> ListarPorTicketAsync(string usuario, string area, string incidenciaNumero, CancellationToken ct)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Agente_ListarPorTicket", conexion) { CommandType = CommandType.StoredProcedure };
        AgregarIdentidad(comando, usuario, area);
        comando.Parameters.Add("@cIncidenciaNumero", SqlDbType.VarChar, 12).Value = incidenciaNumero;
        await conexion.OpenAsync(ct);
        try
        {
            await using var lector = await comando.ExecuteReaderAsync(ct);
            var investigaciones = new List<AgenteTIInvestigacionTicket>();
            while (await lector.ReadAsync(ct)) investigaciones.Add(new AgenteTIInvestigacionTicket
            {
                SesionNumero = LeerLong(lector, "SesionNumero"), Estado = LeerCadena(lector, "Estado"), FechaInicio = LeerFecha(lector, "FechaInicio"),
                FechaDiagnostico = LeerFechaNullable(lector, "FechaDiagnostico"), Confianza = LeerDecimalNullable(lector, "Confianza"),
                Diagnostico = LeerCadena(lector, "Diagnostico"), AccionCodigo = LeerCadena(lector, "AccionCodigo"), UsuarioTI = LeerCadena(lector, "UsuarioTI"),
                NombreOperador = LeerCadena(lector, "NombreOperador"), InformeDisponible = LeerBooleano(lector, "InformeDisponible"), PuedeAbrir = LeerBooleano(lector, "PuedeAbrir")
            });
            return investigaciones;
        }
        catch (SqlException ex) when (EsErrorFuncional(ex))
        {
            throw new InvalidOperationException(ex.Message, ex);
        }
    }

    public async Task<AgenteTIInforme> ObtenerInformeAsync(string usuario, string area, long sesion, CancellationToken ct)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Agente_ObtenerInforme", conexion) { CommandType = CommandType.StoredProcedure };
        AgregarSesion(comando, usuario, area, sesion);
        await conexion.OpenAsync(ct);
        try
        {
            await using var lector = await comando.ExecuteReaderAsync(ct);
            if (!await lector.ReadAsync(ct)) throw new KeyNotFoundException("La investigación no tiene un expediente disponible.");
            return new AgenteTIInforme(LeerCadena(lector, "InformeMarkdown"), LeerCadena(lector, "IncidenciaNumero"));
        }
        catch (SqlException ex) when (EsErrorFuncional(ex))
        {
            throw new InvalidOperationException(ex.Message, ex);
        }
    }

    public async Task<List<AgenteTIComprobacion>> DryRunAsync(string usuario, string area, long sesion, CancellationToken ct)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Agente_DryRun", conexion) { CommandType = CommandType.StoredProcedure };
        AgregarSesion(comando, usuario, area, sesion);
        await conexion.OpenAsync(ct);
        await using var lector = await comando.ExecuteReaderAsync(ct);
        var valores = new List<AgenteTIComprobacion>();
        while (await lector.ReadAsync(ct)) valores.Add(new(LeerCadena(lector, "Codigo"), LeerCadena(lector, "Descripcion"), LeerCadena(lector, "Resultado")));
        return valores;
    }

    public async Task<string> CrearBorradorAsync(string usuario, string area, long sesion, CancellationToken ct)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Agente_CrearBorrador", conexion) { CommandType = CommandType.StoredProcedure };
        AgregarSesion(comando, usuario, area, sesion);
        await conexion.OpenAsync(ct);
        return (await comando.ExecuteScalarAsync(ct))?.ToString() ?? throw new InvalidOperationException("No se pudo crear el borrador.");
    }

    private static void AgregarSesion(SqlCommand comando, string usuario, string area, long sesion)
    {
        AgregarIdentidad(comando, usuario, area);
        comando.Parameters.Add("@nSesionNumero", SqlDbType.BigInt).Value = sesion;
    }

    private async Task EjecutarAsync(string procedimiento, Action<SqlCommand> configurar, CancellationToken ct)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand(procedimiento, conexion) { CommandType = CommandType.StoredProcedure };
        configurar(comando);
        await conexion.OpenAsync(ct);
        try
        {
            await comando.ExecuteNonQueryAsync(ct);
        }
        catch (SqlException ex) when (EsErrorFuncional(ex))
        {
            throw new InvalidOperationException(ex.Message, ex);
        }
    }

    private static void AgregarIdentidad(SqlCommand comando, string usuario, string area)
    {
        comando.Parameters.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;
        comando.Parameters.Add("@cArea", SqlDbType.Char, 3).Value = area;
    }

    private static bool EsErrorFuncional(SqlException ex) => ex.Number is >= 50500 and <= 50599;
    private static string LeerCadena(SqlDataReader lector, string columna) => lector[columna] is DBNull ? string.Empty : lector[columna].ToString()?.Trim() ?? string.Empty;
    private static bool LeerBooleano(SqlDataReader lector, string columna) => lector[columna] is not DBNull && Convert.ToBoolean(lector[columna]);
    private static int LeerEntero(SqlDataReader lector, string columna) => lector[columna] is DBNull ? 0 : Convert.ToInt32(lector[columna]);
    private static int? LeerEnteroNullable(SqlDataReader lector, string columna) => lector[columna] is DBNull ? null : Convert.ToInt32(lector[columna]);
    private static long LeerLong(SqlDataReader lector, string columna) => lector[columna] is DBNull ? 0 : Convert.ToInt64(lector[columna]);
    private static Guid LeerGuid(SqlDataReader lector, string columna) => lector[columna] is DBNull ? Guid.Empty : (Guid)lector[columna];
    private static decimal? LeerDecimalNullable(SqlDataReader lector, string columna) => lector[columna] is DBNull ? null : Convert.ToDecimal(lector[columna]);
    private static DateTime LeerFecha(SqlDataReader lector, string columna) => Convert.ToDateTime(lector[columna]);
    private static DateTime? LeerFechaNullable(SqlDataReader lector, string columna) => lector[columna] is DBNull ? null : Convert.ToDateTime(lector[columna]);
}
