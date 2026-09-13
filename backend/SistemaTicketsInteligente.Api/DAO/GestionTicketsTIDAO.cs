/*
 * Archivo: GestionTicketsTIDAO.cs
 * Objetivo: Ejecutar los Stored Procedures requeridos por el módulo Gestión de Tickets para operadores TI.
 * Responsabilidad: Consultar bandeja/detalle técnico, mapear catálogos y ejecutar únicamente las operaciones controladas definidas para el ciclo de atención.
 * Dependencias: ConexionSqlServer, Microsoft.Data.SqlClient, GestionTicketsTIDTO y procedimientos dbo.Usp_TI_* de Gestión de Tickets TI.
 * Flujo: GestionTicketsTIBLL -> GestionTicketsTIDAO -> Stored Procedures -> SQL Server.
 * Consideraciones: No contiene reglas funcionales; usa parámetros tipados, una sola llamada para cargar la bandeja y convierte errores funcionales SQL 50200-50299 en errores controlados para el backend.
 */

using System.Data;
using Microsoft.Data.SqlClient;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.DAO;

public sealed class GestionTicketsTIDAO
{
    private readonly ConexionSqlServer conexionSqlServer;

    public GestionTicketsTIDAO(ConexionSqlServer conexionSqlServer)
    {
        this.conexionSqlServer = conexionSqlServer;
    }

    public async Task<GestionTicketsTIRespuesta> ObtenerAsync(string usuario, string area, CancellationToken cancellationToken = default)
    {
        var respuesta = new GestionTicketsTIRespuesta();

        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Obtener_GestionTicketsTI", conexion) { CommandType = CommandType.StoredProcedure };
        AgregarIdentidad(comando, usuario, area);

        await conexion.OpenAsync(cancellationToken);

        try
        {
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            if (await lector.ReadAsync(cancellationToken))
            {
                respuesta.Resumen = new GestionTicketsTIResumen
                {
                    Pendientes = LeerEntero(lector, "Pendientes"),
                    EnAtencion = LeerEntero(lector, "EnAtencion"),
                    PorVencer = LeerEntero(lector, "PorVencer"),
                    Reabiertos = LeerEntero(lector, "Reabiertos"),
                    SinAsignar = LeerEntero(lector, "SinAsignar"),
                    MisAsignados = LeerEntero(lector, "MisAsignados"),
                    PrioridadAlta = LeerEntero(lector, "PrioridadAlta"),
                    AprobacionesPendientes = LeerEntero(lector, "AprobacionesPendientes"),
                    Total = LeerEntero(lector, "Total")
                };
            }

            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken))
            {
                respuesta.Tickets.Add(new GestionTicketsTIItem
                {
                    IncidenciaNumero = LeerCadena(lector, "IncidenciaNumero"),
                    UsuarioSolicitante = LeerCadena(lector, "UsuarioSolicitante"),
                    Solicitante = LeerCadena(lector, "Solicitante"),
                    Titulo = LeerCadena(lector, "Titulo"),
                    Detalle = LeerCadena(lector, "Detalle"),
                    AreaSolicitante = LeerCadena(lector, "AreaSolicitante"),
                    AreaDescripcion = LeerCadena(lector, "AreaDescripcion"),
                    Linea = LeerCadena(lector, "Linea"),
                    LineaDescripcion = LeerCadena(lector, "LineaDescripcion"),
                    Tipo = LeerCadena(lector, "Tipo"),
                    TipoDescripcion = LeerCadena(lector, "TipoDescripcion"),
                    Estado = LeerCadena(lector, "Estado"),
                    EstadoDescripcion = LeerCadena(lector, "EstadoDescripcion"),
                    Prioridad = LeerEnteroNullable(lector, "Prioridad"),
                    Impacto = LeerEnteroNullable(lector, "Impacto"),
                    Complejidad = LeerEnteroNullable(lector, "Complejidad"),
                    UsuarioTI = LeerCadena(lector, "UsuarioTI"),
                    Responsable = LeerCadena(lector, "Responsable"),
                    FechaRegistro = LeerFecha(lector, "FechaRegistro"),
                    UltimaFechaModif = LeerFecha(lector, "UltimaFechaModif"),
                    SlaMinutosRestantes = LeerEnteroNullable(lector, "SlaMinutosRestantes"),
                    TieneAprobacionPendiente = LeerBooleano(lector, "TieneAprobacionPendiente"),
                    Accion = LeerCadena(lector, "Accion")
                });
            }

            await lector.NextResultAsync(cancellationToken);
            await CargarCatalogoAsync(lector, respuesta.Catalogos.Estados, cancellationToken);
            await lector.NextResultAsync(cancellationToken);
            await CargarCatalogoAsync(lector, respuesta.Catalogos.Areas, cancellationToken);
            await lector.NextResultAsync(cancellationToken);
            await CargarCatalogoAsync(lector, respuesta.Catalogos.Operadores, cancellationToken);

            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken))
            {
                respuesta.Catalogos.Lineas.Add(new GestionTicketsTILinea
                {
                    Codigo = LeerCadena(lector, "Codigo"),
                    Descripcion = LeerCadena(lector, "Descripcion"),
                    Area = LeerCadena(lector, "Area")
                });
            }

            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken))
            {
                respuesta.Catalogos.Items.Add(new GestionTicketsTIItemCatalogo
                {
                    Codigo = LeerCadena(lector, "Codigo"),
                    Descripcion = LeerCadena(lector, "Descripcion"),
                    Linea = LeerCadena(lector, "Linea")
                });
            }

            await lector.NextResultAsync(cancellationToken);
            await CargarCatalogoAsync(lector, respuesta.Catalogos.Tipos, cancellationToken);
            await lector.NextResultAsync(cancellationToken);
            await CargarCatalogoAsync(lector, respuesta.Catalogos.Categorias, cancellationToken);

            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken))
            {
                respuesta.Catalogos.SubTipos.Add(new GestionTicketsTISubTipo
                {
                    Codigo = LeerCadena(lector, "Codigo"),
                    Descripcion = LeerCadena(lector, "Descripcion"),
                    Tipo = LeerCadena(lector, "Tipo"),
                    Categoria = LeerCadena(lector, "Categoria")
                });
            }
        }
        catch (SqlException ex) when (EsErrorFuncional(ex))
        {
            throw new InvalidOperationException(ex.Message, ex);
        }

        return respuesta;
    }

    public async Task<GestionTicketTIDetalle?> ObtenerDetalleAsync(string usuario, string area, string incidenciaNumero, CancellationToken cancellationToken = default)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Obtener_DetalleGestionTicketTI", conexion) { CommandType = CommandType.StoredProcedure };
        AgregarIdentidad(comando, usuario, area);
        comando.Parameters.Add("@cIncidenciaNumero", SqlDbType.VarChar, 12).Value = incidenciaNumero;

        await conexion.OpenAsync(cancellationToken);

        try
        {
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
            if (!await lector.ReadAsync(cancellationToken)) return null;

            var detalle = new GestionTicketTIDetalle
            {
                IncidenciaNumero = LeerCadena(lector, "IncidenciaNumero"),
                UsuarioSolicitante = LeerCadena(lector, "UsuarioSolicitante"),
                Solicitante = LeerCadena(lector, "Solicitante"),
                CorreoSolicitante = LeerCadena(lector, "CorreoSolicitante"),
                AreaSolicitante = LeerCadena(lector, "AreaSolicitante"),
                AreaSolicitanteDescripcion = LeerCadena(lector, "AreaSolicitanteDescripcion"),
                AreaTI = LeerCadena(lector, "AreaTI"),
                AreaTIDescripcion = LeerCadena(lector, "AreaTIDescripcion"),
                UsuarioTI = LeerCadena(lector, "UsuarioTI"),
                Responsable = LeerCadena(lector, "Responsable"),
                UsuarioAsigno = LeerCadena(lector, "UsuarioAsigno"),
                Linea = LeerCadena(lector, "Linea"),
                LineaDescripcion = LeerCadena(lector, "LineaDescripcion"),
                Item = LeerCadena(lector, "Item"),
                ItemDescripcion = LeerCadena(lector, "ItemDescripcion"),
                Tipo = LeerCadena(lector, "Tipo"),
                TipoDescripcion = LeerCadena(lector, "TipoDescripcion"),
                SubTipo = LeerCadena(lector, "SubTipo"),
                SubTipoDescripcion = LeerCadena(lector, "SubTipoDescripcion"),
                Categoria = LeerCadena(lector, "Categoria"),
                CategoriaDescripcion = LeerCadena(lector, "CategoriaDescripcion"),
                Estado = LeerCadena(lector, "Estado"),
                EstadoDescripcion = LeerCadena(lector, "EstadoDescripcion"),
                AreaCausante = LeerCadena(lector, "AreaCausante"),
                AreaCausanteDescripcion = LeerCadena(lector, "AreaCausanteDescripcion"),
                Titulo = LeerCadena(lector, "Titulo"),
                Detalle = LeerCadena(lector, "Detalle"),
                MensajeError = LeerCadena(lector, "MensajeError"),
                FechaRegistro = LeerFecha(lector, "FechaRegistro"),
                FechaAsignacion = LeerFechaNullable(lector, "FechaAsignacion"),
                FechaAtencion = LeerFechaNullable(lector, "FechaAtencion"),
                FechaCierre = LeerFechaNullable(lector, "FechaCierre"),
                SlaObjetivoMinutos = LeerEnteroNullable(lector, "SlaObjetivoMinutos"),
                Prioridad = LeerEnteroNullable(lector, "Prioridad"),
                Impacto = LeerEnteroNullable(lector, "Impacto"),
                Complejidad = LeerEnteroNullable(lector, "Complejidad"),
                CanalRegistro = LeerCadena(lector, "CanalRegistro"),
                CausaRaiz = LeerCadena(lector, "CausaRaiz"),
                SolucionTecnica = LeerCadena(lector, "SolucionTecnica"),
                RespuestaUsuario = LeerCadena(lector, "RespuestaUsuario"),
                TipoResolucion = LeerCadena(lector, "TipoResolucion"),
                Calificacion = LeerByteNullable(lector, "Calificacion"),
                ComentarioCalificacion = LeerCadena(lector, "ComentarioCalificacion"),
                UltimaFechaModif = LeerFecha(lector, "UltimaFechaModif"),
                SlaMinutosRestantes = LeerEnteroNullable(lector, "SlaMinutosRestantes")
            };

            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken))
            {
                detalle.HistorialEstados.Add(new GestionTicketTIEstado
                {
                    Secuencia = LeerEntero(lector, "Secuencia"),
                    Estado = LeerCadena(lector, "Estado"),
                    EstadoDescripcion = LeerCadena(lector, "EstadoDescripcion"),
                    Actor = LeerCadena(lector, "Actor"),
                    FechaCambio = LeerFecha(lector, "FechaCambio"),
                    Observacion = LeerCadena(lector, "Observacion")
                });
            }

            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken))
            {
                detalle.Avances.Add(new GestionTicketTIAvance
                {
                    Secuencia = LeerEntero(lector, "Secuencia"),
                    UsuarioTI = LeerCadena(lector, "UsuarioTI"),
                    Responsable = LeerCadena(lector, "Responsable"),
                    FechaAvance = LeerFecha(lector, "FechaAvance"),
                    Detalle = LeerCadena(lector, "Detalle"),
                    TiempoUtilizado = LeerDecimalNullable(lector, "TiempoUtilizado"),
                    PorcentajeAvance = LeerDecimalNullable(lector, "PorcentajeAvance")
                });
            }

            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken))
            {
                detalle.Mensajes.Add(new GestionTicketTIMensaje
                {
                    Secuencia = LeerEntero(lector, "Secuencia"),
                    TipoAutor = LeerCadena(lector, "TipoAutor"),
                    Autor = LeerCadena(lector, "Autor"),
                    Contenido = LeerCadena(lector, "Contenido"),
                    FechaMensaje = LeerFecha(lector, "FechaMensaje"),
                    EsInterno = LeerBooleano(lector, "EsInterno")
                });
            }

            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken))
            {
                detalle.Adjuntos.Add(new GestionTicketTIAdjunto
                {
                    Secuencia = LeerEntero(lector, "Secuencia"),
                    MensajeSecuencia = LeerEnteroNullable(lector, "MensajeSecuencia"),
                    NombreOriginal = LeerCadena(lector, "NombreOriginal"),
                    TipoMime = LeerCadena(lector, "TipoMime"),
                    TamanoBytes = LeerLong(lector, "TamanoBytes"),
                    FechaRegistro = LeerFecha(lector, "FechaRegistro"),
                    UsuarioRegistro = LeerCadena(lector, "UsuarioRegistro")
                });
            }

            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken))
            {
                detalle.Documentos.Add(new GestionTicketTIDocumento
                {
                    Secuencia = LeerEntero(lector, "Secuencia"),
                    CompaniaSocio = LeerCadena(lector, "CompaniaSocio"),
                    TipoDocumento = LeerCadena(lector, "TipoDocumento"),
                    NumeroDocumento = LeerCadena(lector, "NumeroDocumento"),
                    Descripcion = LeerCadena(lector, "Descripcion")
                });
            }

            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken))
            {
                detalle.Aprobaciones.Add(new GestionTicketTIAprobacion
                {
                    Secuencia = LeerEntero(lector, "Secuencia"),
                    AccionCodigo = LeerCadena(lector, "AccionCodigo"),
                    AccionNombre = LeerCadena(lector, "AccionNombre"),
                    NivelRiesgo = LeerCadena(lector, "NivelRiesgo"),
                    Estado = LeerCadena(lector, "Estado"),
                    Justificacion = LeerCadena(lector, "Justificacion"),
                    ComentarioRespuesta = LeerCadena(lector, "ComentarioRespuesta"),
                    UsuarioSolicitante = LeerCadena(lector, "UsuarioSolicitante"),
                    Solicitante = LeerCadena(lector, "Solicitante"),
                    UsuarioAprobador = LeerCadena(lector, "UsuarioAprobador"),
                    Aprobador = LeerCadena(lector, "Aprobador"),
                    FechaSolicitud = LeerFecha(lector, "FechaSolicitud"),
                    FechaRespuesta = LeerFechaNullable(lector, "FechaRespuesta")
                });
            }

            return detalle;
        }
        catch (SqlException ex) when (EsErrorFuncional(ex))
        {
            throw new InvalidOperationException(ex.Message, ex);
        }
    }

    public Task ClasificarAsync(string usuario, string area, string incidenciaNumero, ClasificarTicketTISolicitud solicitud, Guid idCorrelacion, CancellationToken cancellationToken = default) =>
        EjecutarAccionAsync("dbo.Usp_TI_Clasificar_Ticket", usuario, area, incidenciaNumero, comando =>
        {
            comando.Parameters.Add("@cLinea", SqlDbType.Char, 3).Value = solicitud.Linea;
            comando.Parameters.Add("@cItem", SqlDbType.VarChar, 20).Value = solicitud.Item;
            comando.Parameters.Add("@cTipo", SqlDbType.Char, 3).Value = solicitud.Tipo;
            comando.Parameters.Add("@cSubTipo", SqlDbType.Char, 3).Value = solicitud.SubTipo;
            comando.Parameters.Add("@cCategoria", SqlDbType.VarChar, 20).Value = solicitud.Categoria;
            comando.Parameters.Add("@cAreaCausante", SqlDbType.Char, 3).Value = string.IsNullOrWhiteSpace(solicitud.AreaCausante) ? DBNull.Value : solicitud.AreaCausante;
            comando.Parameters.Add("@nPrioridad", SqlDbType.Int).Value = solicitud.Prioridad.HasValue ? solicitud.Prioridad.Value : DBNull.Value;
            comando.Parameters.Add("@nImpacto", SqlDbType.Int).Value = solicitud.Impacto.HasValue ? solicitud.Impacto.Value : DBNull.Value;
            comando.Parameters.Add("@nComplejidad", SqlDbType.Int).Value = solicitud.Complejidad.HasValue ? solicitud.Complejidad.Value : DBNull.Value;
            comando.Parameters.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = idCorrelacion;
        }, cancellationToken);

    public Task AsignarAsync(string usuario, string area, string incidenciaNumero, string usuarioTI, Guid idCorrelacion, CancellationToken cancellationToken = default) =>
        EjecutarAccionAsync("dbo.Usp_TI_Asignar_Ticket", usuario, area, incidenciaNumero, comando =>
        {
            comando.Parameters.Add("@cUsuarioTI", SqlDbType.VarChar, 20).Value = usuarioTI;
            comando.Parameters.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = idCorrelacion;
        }, cancellationToken);

    public Task RegistrarAvanceAsync(string usuario, string area, string incidenciaNumero, string detalle, bool visibleUsuario, Guid idCorrelacion, CancellationToken cancellationToken = default) =>
        EjecutarAccionAsync("dbo.Usp_TI_Registrar_AvanceTicket", usuario, area, incidenciaNumero, comando =>
        {
            comando.Parameters.Add("@cDetalle", SqlDbType.NVarChar, -1).Value = detalle;
            comando.Parameters.Add("@lVisibleUsuario", SqlDbType.Bit).Value = visibleUsuario;
            comando.Parameters.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = idCorrelacion;
        }, cancellationToken);

    public Task SolicitarInformacionAsync(string usuario, string area, string incidenciaNumero, string mensaje, Guid idCorrelacion, CancellationToken cancellationToken = default) =>
        EjecutarAccionAsync("dbo.Usp_TI_Solicitar_InformacionTicket", usuario, area, incidenciaNumero, comando =>
        {
            comando.Parameters.Add("@cMensaje", SqlDbType.NVarChar, 1000).Value = mensaje;
            comando.Parameters.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = idCorrelacion;
        }, cancellationToken);

    public Task ResolverAsync(string usuario, string area, string incidenciaNumero, ResolverTicketTISolicitud solicitud, Guid idCorrelacion, CancellationToken cancellationToken = default) =>
        EjecutarAccionAsync("dbo.Usp_TI_Resolver_TicketTI", usuario, area, incidenciaNumero, comando =>
        {
            comando.Parameters.Add("@cCausaRaiz", SqlDbType.NVarChar, -1).Value = solicitud.CausaRaiz;
            comando.Parameters.Add("@cSolucion", SqlDbType.NVarChar, -1).Value = solicitud.Solucion;
            comando.Parameters.Add("@cRespuestaUsuario", SqlDbType.NVarChar, -1).Value = solicitud.RespuestaUsuario;
            comando.Parameters.Add("@cTipoResolucion", SqlDbType.VarChar, 20).Value = solicitud.TipoResolucion;
            comando.Parameters.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = idCorrelacion;
        }, cancellationToken);

    public Task NoProcedeAsync(string usuario, string area, string incidenciaNumero, string motivo, Guid idCorrelacion, CancellationToken cancellationToken = default) =>
        EjecutarAccionAsync("dbo.Usp_TI_NoProcede_Ticket", usuario, area, incidenciaNumero, comando =>
        {
            comando.Parameters.Add("@cMotivo", SqlDbType.NVarChar, 1000).Value = motivo;
            comando.Parameters.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = idCorrelacion;
        }, cancellationToken);

    public Task ResponderAprobacionAsync(string usuario, string area, string incidenciaNumero, int secuencia, bool aprobar, string? comentario, Guid idCorrelacion, CancellationToken cancellationToken = default) =>
        EjecutarAccionAsync("dbo.Usp_TI_Responder_AprobacionTicket", usuario, area, incidenciaNumero, comando =>
        {
            comando.Parameters.Add("@nSecuencia", SqlDbType.Int).Value = secuencia;
            comando.Parameters.Add("@lAprobar", SqlDbType.Bit).Value = aprobar;
            comando.Parameters.Add("@cComentario", SqlDbType.NVarChar, 1000).Value = string.IsNullOrWhiteSpace(comentario) ? DBNull.Value : comentario;
            comando.Parameters.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = idCorrelacion;
        }, cancellationToken);

    public async Task<GestionTicketTIArchivo?> ObtenerArchivoAsync(string usuario, string area, string incidenciaNumero, int secuencia, CancellationToken cancellationToken = default)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Obtener_AdjuntoTicketTI", conexion) { CommandType = CommandType.StoredProcedure };
        AgregarIdentidad(comando, usuario, area);
        comando.Parameters.Add("@cIncidenciaNumero", SqlDbType.VarChar, 12).Value = incidenciaNumero;
        comando.Parameters.Add("@nSecuencia", SqlDbType.Int).Value = secuencia;

        await conexion.OpenAsync(cancellationToken);

        try
        {
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
            if (!await lector.ReadAsync(cancellationToken)) return null;

            return new GestionTicketTIArchivo
            {
                NombreOriginal = LeerCadena(lector, "NombreOriginal"),
                RutaArchivo = LeerCadena(lector, "RutaArchivo"),
                TipoMime = LeerCadena(lector, "TipoMime")
            };
        }
        catch (SqlException ex) when (EsErrorFuncional(ex))
        {
            throw new InvalidOperationException(ex.Message, ex);
        }
    }

    private async Task EjecutarAccionAsync(string procedimiento, string usuario, string area, string incidenciaNumero, Action<SqlCommand> configurar, CancellationToken cancellationToken)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand(procedimiento, conexion) { CommandType = CommandType.StoredProcedure };
        AgregarIdentidad(comando, usuario, area);
        comando.Parameters.Add("@cIncidenciaNumero", SqlDbType.VarChar, 12).Value = incidenciaNumero;
        configurar(comando);

        await conexion.OpenAsync(cancellationToken);

        try
        {
            await comando.ExecuteNonQueryAsync(cancellationToken);
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

    private static async Task CargarCatalogoAsync(SqlDataReader lector, ICollection<GestionTicketsTICatalogoItem> destino, CancellationToken cancellationToken)
    {
        while (await lector.ReadAsync(cancellationToken))
        {
            destino.Add(new GestionTicketsTICatalogoItem
            {
                Codigo = LeerCadena(lector, "Codigo"),
                Descripcion = LeerCadena(lector, "Descripcion")
            });
        }
    }

    private static bool EsErrorFuncional(SqlException ex) => ex.Number is >= 50200 and <= 50299;
    private static string LeerCadena(SqlDataReader lector, string columna) => lector[columna] is DBNull ? string.Empty : lector[columna].ToString()?.Trim() ?? string.Empty;
    private static int LeerEntero(SqlDataReader lector, string columna) => lector[columna] is DBNull ? 0 : Convert.ToInt32(lector[columna]);
    private static int? LeerEnteroNullable(SqlDataReader lector, string columna) => lector[columna] is DBNull ? null : Convert.ToInt32(lector[columna]);
    private static long LeerLong(SqlDataReader lector, string columna) => lector[columna] is DBNull ? 0 : Convert.ToInt64(lector[columna]);
    private static byte? LeerByteNullable(SqlDataReader lector, string columna) => lector[columna] is DBNull ? null : Convert.ToByte(lector[columna]);
    private static decimal? LeerDecimalNullable(SqlDataReader lector, string columna) => lector[columna] is DBNull ? null : Convert.ToDecimal(lector[columna]);
    private static bool LeerBooleano(SqlDataReader lector, string columna) => lector[columna] is not DBNull && Convert.ToBoolean(lector[columna]);
    private static DateTime LeerFecha(SqlDataReader lector, string columna) => Convert.ToDateTime(lector[columna]);
    private static DateTime? LeerFechaNullable(SqlDataReader lector, string columna) => lector[columna] is DBNull ? null : Convert.ToDateTime(lector[columna]);
}
