/**
 * Archivo: MisTicketsUsuarioDAO.cs
 * Objetivo: Ejecutar los Stored Procedures requeridos por el módulo Mis Tickets del usuario.
 * Responsabilidad: Consultar listado y detalle, registrar respuestas/adjuntos, validar soluciones, guardar calificaciones y localizar adjuntos autorizados.
 * Dependencias: ConexionSqlServer, Microsoft.Data.SqlClient, MisTicketsUsuarioDTO y procedimientos dbo.Usp_TI_* del módulo Mis Tickets.
 * Flujo: MisTicketsUsuarioBLL -> MisTicketsUsuarioDAO -> Stored Procedures -> SQL Server.
 * Consideraciones: Todas las consultas filtran por el usuario autenticado; responder una observación y registrar sus adjuntos se ejecuta dentro de una sola transacción SQL.
 */

using System.Data;
using Microsoft.Data.SqlClient;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.DAO;

public sealed class MisTicketsUsuarioDAO
{
    private readonly ConexionSqlServer conexionSqlServer;

    public MisTicketsUsuarioDAO(ConexionSqlServer conexionSqlServer)
    {
        this.conexionSqlServer = conexionSqlServer;
    }

    public async Task<MisTicketsUsuarioRespuesta> ObtenerAsync(string usuario, CancellationToken cancellationToken = default)
    {
        var respuesta = new MisTicketsUsuarioRespuesta();

        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Obtener_MisTicketsUsuario", conexion) { CommandType = CommandType.StoredProcedure };
        comando.Parameters.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;

        await conexion.OpenAsync(cancellationToken);
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

        if (await lector.ReadAsync(cancellationToken))
        {
            respuesta.Resumen = new MisTicketsUsuarioResumen
            {
                Activos = LeerEntero(lector, "Activos"),
                EnAtencion = LeerEntero(lector, "EnAtencion"),
                PendientesRespuesta = LeerEntero(lector, "PendientesRespuesta"),
                Resueltos30Dias = LeerEntero(lector, "Resueltos30Dias")
            };
        }

        await lector.NextResultAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            respuesta.Tickets.Add(new MisTicketsUsuarioItem
            {
                IncidenciaNumero = LeerCadena(lector, "IncidenciaNumero"),
                Titulo = LeerCadena(lector, "Titulo"),
                Detalle = LeerCadena(lector, "Detalle"),
                Estado = LeerCadena(lector, "Estado"),
                EstadoDescripcion = LeerCadena(lector, "EstadoDescripcion"),
                Responsable = LeerCadena(lector, "Responsable"),
                FechaRegistro = LeerFecha(lector, "FechaRegistro"),
                UltimaFechaModif = LeerFecha(lector, "UltimaFechaModif"),
                Prioridad = LeerEnteroNullable(lector, "Prioridad"),
                Accion = LeerCadena(lector, "Accion")
            });
        }

        return respuesta;
    }

    public async Task<MisTicketsUsuarioDetalle?> ObtenerDetalleAsync(string usuario, string incidenciaNumero, CancellationToken cancellationToken = default)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Obtener_DetalleTicketUsuario", conexion) { CommandType = CommandType.StoredProcedure };
        comando.Parameters.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;
        comando.Parameters.Add("@cIncidenciaNumero", SqlDbType.VarChar, 12).Value = incidenciaNumero;

        await conexion.OpenAsync(cancellationToken);
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
        if (!await lector.ReadAsync(cancellationToken)) return null;

        var detalle = new MisTicketsUsuarioDetalle
        {
            IncidenciaNumero = LeerCadena(lector, "IncidenciaNumero"),
            Titulo = LeerCadena(lector, "Titulo"),
            Detalle = LeerCadena(lector, "Detalle"),
            MensajeError = LeerCadena(lector, "MensajeError"),
            Estado = LeerCadena(lector, "Estado"),
            EstadoDescripcion = LeerCadena(lector, "EstadoDescripcion"),
            Linea = LeerCadena(lector, "Linea"),
            LineaDescripcion = LeerCadena(lector, "LineaDescripcion"),
            Item = LeerCadena(lector, "Item"),
            ItemDescripcion = LeerCadena(lector, "ItemDescripcion"),
            Tipo = LeerCadena(lector, "Tipo"),
            TipoDescripcion = LeerCadena(lector, "TipoDescripcion"),
            AreaDescripcion = LeerCadena(lector, "AreaDescripcion"),
            Responsable = LeerCadena(lector, "Responsable"),
            FechaRegistro = LeerFecha(lector, "FechaRegistro"),
            FechaAsignacion = LeerFechaNullable(lector, "FechaAsignacion"),
            FechaAtencion = LeerFechaNullable(lector, "FechaAtencion"),
            FechaCierre = LeerFechaNullable(lector, "FechaCierre"),
            UltimaFechaModif = LeerFecha(lector, "UltimaFechaModif"),
            Prioridad = LeerEnteroNullable(lector, "Prioridad"),
            Calificacion = LeerByteNullable(lector, "Calificacion"),
            ComentarioCalificacion = LeerCadena(lector, "ComentarioCalificacion"),
            RespuestaUsuario = LeerCadena(lector, "RespuestaUsuario"),
            Accion = LeerCadena(lector, "Accion")
        };

        await lector.NextResultAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            detalle.HistorialEstados.Add(new MisTicketsUsuarioEstado
            {
                Secuencia = LeerEntero(lector, "Secuencia"),
                Estado = LeerCadena(lector, "Estado"),
                EstadoDescripcion = LeerCadena(lector, "EstadoDescripcion"),
                Actor = LeerCadena(lector, "Actor"),
                Fecha = LeerFecha(lector, "Fecha"),
                Observacion = LeerCadena(lector, "Observacion")
            });
        }

        await lector.NextResultAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            detalle.Avances.Add(new MisTicketsUsuarioAvance
            {
                Secuencia = LeerEntero(lector, "Secuencia"),
                Responsable = LeerCadena(lector, "Responsable"),
                Fecha = LeerFecha(lector, "Fecha"),
                Detalle = LeerCadena(lector, "Detalle"),
                PorcentajeAvance = LeerDecimalNullable(lector, "PorcentajeAvance")
            });
        }

        await lector.NextResultAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            detalle.Mensajes.Add(new MisTicketsUsuarioMensaje
            {
                Secuencia = LeerEntero(lector, "Secuencia"),
                TipoAutor = LeerCadena(lector, "TipoAutor"),
                Autor = LeerCadena(lector, "Autor"),
                Contenido = LeerCadena(lector, "Contenido"),
                Fecha = LeerFecha(lector, "Fecha")
            });
        }

        await lector.NextResultAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            detalle.Adjuntos.Add(new MisTicketsUsuarioAdjunto
            {
                Secuencia = LeerEntero(lector, "Secuencia"),
                MensajeSecuencia = LeerEnteroNullable(lector, "MensajeSecuencia"),
                NombreOriginal = LeerCadena(lector, "NombreOriginal"),
                TipoMime = LeerCadena(lector, "TipoMime"),
                TamanoBytes = LeerLong(lector, "TamanoBytes"),
                FechaRegistro = LeerFecha(lector, "FechaRegistro")
            });
        }

        await lector.NextResultAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            detalle.Documentos.Add(new MisTicketsUsuarioDocumento
            {
                Secuencia = LeerEntero(lector, "Secuencia"),
                CompaniaSocio = LeerCadena(lector, "CompaniaSocio"),
                TipoDocumento = LeerCadena(lector, "TipoDocumento"),
                NumeroDocumento = LeerCadena(lector, "NumeroDocumento"),
                Descripcion = LeerCadena(lector, "Descripcion")
            });
        }

        return detalle;
    }

    public async Task ResponderObservacionAsync(
        string usuario,
        string incidenciaNumero,
        string contenido,
        IReadOnlyCollection<MisTicketsUsuarioAdjuntoRegistro> adjuntos,
        Guid idCorrelacion,
        CancellationToken cancellationToken = default)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await conexion.OpenAsync(cancellationToken);
        await using var transaccion = (SqlTransaction)await conexion.BeginTransactionAsync(cancellationToken);

        try
        {
            var mensajeSecuencia = await RegistrarRespuestaAsync(conexion, transaccion, usuario, incidenciaNumero, contenido, idCorrelacion, cancellationToken);

            foreach (var adjunto in adjuntos)
            {
                await RegistrarAdjuntoMensajeAsync(conexion, transaccion, usuario, incidenciaNumero, mensajeSecuencia, adjunto, cancellationToken);
            }

            await transaccion.CommitAsync(cancellationToken);
        }
        catch (SqlException ex) when (ex.Number is >= 50100 and <= 50199)
        {
            await transaccion.RollbackAsync(cancellationToken);
            throw new InvalidOperationException(ex.Message, ex);
        }
        catch
        {
            await transaccion.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task ValidarSolucionAsync(string usuario, string incidenciaNumero, bool solucionada, string? comentario, Guid idCorrelacion, CancellationToken cancellationToken = default)
    {
        await EjecutarAccionAsync("dbo.Usp_TI_Validar_SolucionTicket", usuario, incidenciaNumero, comando =>
        {
            comando.Parameters.Add("@lSolucionada", SqlDbType.Bit).Value = solucionada;
            comando.Parameters.Add("@cComentario", SqlDbType.NVarChar, 1000).Value = string.IsNullOrWhiteSpace(comentario) ? DBNull.Value : comentario;
            comando.Parameters.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = idCorrelacion;
        }, cancellationToken);
    }

    public async Task CalificarAsync(string usuario, string incidenciaNumero, byte calificacion, string? comentario, Guid idCorrelacion, CancellationToken cancellationToken = default)
    {
        await EjecutarAccionAsync("dbo.Usp_TI_Calificar_TicketUsuario", usuario, incidenciaNumero, comando =>
        {
            comando.Parameters.Add("@nCalificacion", SqlDbType.TinyInt).Value = calificacion;
            comando.Parameters.Add("@cComentario", SqlDbType.NVarChar, 500).Value = string.IsNullOrWhiteSpace(comentario) ? DBNull.Value : comentario;
            comando.Parameters.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = idCorrelacion;
        }, cancellationToken);
    }

    public async Task<MisTicketsUsuarioArchivo?> ObtenerArchivoAsync(string usuario, string incidenciaNumero, int secuencia, CancellationToken cancellationToken = default)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Obtener_AdjuntoTicketUsuario", conexion) { CommandType = CommandType.StoredProcedure };
        comando.Parameters.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;
        comando.Parameters.Add("@cIncidenciaNumero", SqlDbType.VarChar, 12).Value = incidenciaNumero;
        comando.Parameters.Add("@nSecuencia", SqlDbType.Int).Value = secuencia;

        await conexion.OpenAsync(cancellationToken);
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
        if (!await lector.ReadAsync(cancellationToken)) return null;

        return new MisTicketsUsuarioArchivo
        {
            NombreOriginal = LeerCadena(lector, "NombreOriginal"),
            RutaArchivo = LeerCadena(lector, "RutaArchivo"),
            TipoMime = LeerCadena(lector, "TipoMime")
        };
    }

    private async Task EjecutarAccionAsync(string procedimiento, string usuario, string incidenciaNumero, Action<SqlCommand> configurar, CancellationToken cancellationToken)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand(procedimiento, conexion) { CommandType = CommandType.StoredProcedure };
        comando.Parameters.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;
        comando.Parameters.Add("@cIncidenciaNumero", SqlDbType.VarChar, 12).Value = incidenciaNumero;
        configurar(comando);

        await conexion.OpenAsync(cancellationToken);
        try
        {
            await comando.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException ex) when (ex.Number is >= 50100 and <= 50199)
        {
            throw new InvalidOperationException(ex.Message, ex);
        }
    }

    private static async Task<int> RegistrarRespuestaAsync(
        SqlConnection conexion,
        SqlTransaction transaccion,
        string usuario,
        string incidenciaNumero,
        string contenido,
        Guid idCorrelacion,
        CancellationToken cancellationToken)
    {
        await using var comando = new SqlCommand("dbo.Usp_TI_Responder_ObservacionTicket", conexion, transaccion) { CommandType = CommandType.StoredProcedure };
        comando.Parameters.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;
        comando.Parameters.Add("@cIncidenciaNumero", SqlDbType.VarChar, 12).Value = incidenciaNumero;
        comando.Parameters.Add("@cContenido", SqlDbType.NVarChar, -1).Value = contenido;
        comando.Parameters.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = idCorrelacion;

        var resultado = await comando.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(resultado);
    }

    private static async Task RegistrarAdjuntoMensajeAsync(
        SqlConnection conexion,
        SqlTransaction transaccion,
        string usuario,
        string incidenciaNumero,
        int mensajeSecuencia,
        MisTicketsUsuarioAdjuntoRegistro adjunto,
        CancellationToken cancellationToken)
    {
        await using var comando = new SqlCommand("dbo.Usp_TI_Registrar_AdjuntoMensajeUsuario", conexion, transaccion) { CommandType = CommandType.StoredProcedure };
        comando.Parameters.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;
        comando.Parameters.Add("@cIncidenciaNumero", SqlDbType.VarChar, 12).Value = incidenciaNumero;
        comando.Parameters.Add("@nMensajeSecuencia", SqlDbType.Int).Value = mensajeSecuencia;
        comando.Parameters.Add("@cNombreOriginal", SqlDbType.NVarChar, 260).Value = adjunto.NombreOriginal;
        comando.Parameters.Add("@cNombreArchivo", SqlDbType.NVarChar, 260).Value = adjunto.NombreArchivo;
        comando.Parameters.Add("@cRutaArchivo", SqlDbType.NVarChar, 1000).Value = adjunto.RutaArchivo;
        comando.Parameters.Add("@cTipoMime", SqlDbType.VarChar, 100).Value = adjunto.TipoMime;
        comando.Parameters.Add("@nTamanoBytes", SqlDbType.BigInt).Value = adjunto.TamanoBytes;
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string LeerCadena(SqlDataReader lector, string columna) => lector[columna] is DBNull ? string.Empty : lector[columna].ToString()?.Trim() ?? string.Empty;
    private static int LeerEntero(SqlDataReader lector, string columna) => lector[columna] is DBNull ? 0 : Convert.ToInt32(lector[columna]);
    private static int? LeerEnteroNullable(SqlDataReader lector, string columna) => lector[columna] is DBNull ? null : Convert.ToInt32(lector[columna]);
    private static long LeerLong(SqlDataReader lector, string columna) => lector[columna] is DBNull ? 0 : Convert.ToInt64(lector[columna]);
    private static byte? LeerByteNullable(SqlDataReader lector, string columna) => lector[columna] is DBNull ? null : Convert.ToByte(lector[columna]);
    private static decimal? LeerDecimalNullable(SqlDataReader lector, string columna) => lector[columna] is DBNull ? null : Convert.ToDecimal(lector[columna]);
    private static DateTime LeerFecha(SqlDataReader lector, string columna) => Convert.ToDateTime(lector[columna]);
    private static DateTime? LeerFechaNullable(SqlDataReader lector, string columna) => lector[columna] is DBNull ? null : Convert.ToDateTime(lector[columna]);
}
