/**
 * Archivo: NuevoTicketDAO.cs
 * Objetivo: Ejecutar los Stored Procedures requeridos para cargar y registrar el módulo Nuevo Ticket.
 * Responsabilidad: Consultar datos del formulario y persistir la incidencia con la metadata de sus adjuntos dentro de una misma transacción SQL.
 * Dependencias: ConexionSqlServer, Microsoft.Data.SqlClient, NuevoTicketDTO y procedimientos dbo.Usp_TI_Obtener_DatosNuevoTicket, dbo.Usp_TI_Registrar_Incidencia y dbo.Usp_TI_Registrar_IncidenciaAdjunto.
 * Flujo: NuevoTicketBLL -> NuevoTicketDAO -> Stored Procedures -> SQL Server.
 * Consideraciones: No contiene reglas de negocio; utiliza parámetros tipados y una transacción para evitar registrar metadata de adjuntos parcialmente. Si SQL Server ya revirtió la transacción, conserva el error original.
 */

using System.Data;
using Microsoft.Data.SqlClient;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.DAO;

public sealed class NuevoTicketDAO
{
    private readonly ConexionSqlServer conexionSqlServer;

    public NuevoTicketDAO(ConexionSqlServer conexionSqlServer)
    {
        this.conexionSqlServer = conexionSqlServer;
    }

    public async Task<NuevoTicketDatosRespuesta?> ObtenerDatosAsync(string usuario, CancellationToken cancellationToken = default)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Obtener_DatosNuevoTicket", conexion) { CommandType = CommandType.StoredProcedure };
        comando.Parameters.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;

        await conexion.OpenAsync(cancellationToken);
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

        if (!await lector.ReadAsync(cancellationToken)) return null;

        var respuesta = new NuevoTicketDatosRespuesta
        {
            Usuario = LeerCadena(lector, "Usuario"),
            NombreCompleto = LeerCadena(lector, "NombreCompleto"),
            Area = LeerCadena(lector, "Area"),
            AreaDescripcion = LeerCadena(lector, "AreaDescripcion")
        };

        await lector.NextResultAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            respuesta.Lineas.Add(new NuevoTicketCatalogoItem
            {
                Codigo = LeerCadena(lector, "Codigo"),
                Descripcion = LeerCadena(lector, "Descripcion")
            });
        }

        await lector.NextResultAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            respuesta.Tipos.Add(new NuevoTicketCatalogoItem
            {
                Codigo = LeerCadena(lector, "Codigo"),
                Descripcion = LeerCadena(lector, "Descripcion")
            });
        }

        return respuesta;
    }

    public async Task<NuevoTicketCreadoRespuesta> CrearAsync(
        string usuario,
        CrearNuevoTicketSolicitud solicitud,
        IReadOnlyCollection<NuevoTicketAdjuntoRegistro> adjuntos,
        Guid idCorrelacion,
        CancellationToken cancellationToken = default)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await conexion.OpenAsync(cancellationToken);
        await using var transaccion = (SqlTransaction)await conexion.BeginTransactionAsync(cancellationToken);

        try
        {
            var creado = await RegistrarIncidenciaAsync(conexion, transaccion, usuario, solicitud, idCorrelacion, cancellationToken);

            foreach (var adjunto in adjuntos)
            {
                await RegistrarAdjuntoAsync(conexion, transaccion, creado.IncidenciaNumero, usuario, adjunto, cancellationToken);
            }

            await transaccion.CommitAsync(cancellationToken);
            return creado;
        }
        catch
        {
            try
            {
                if (transaccion.Connection is not null) await transaccion.RollbackAsync(CancellationToken.None);
            }
            catch
            {
                // El procedimiento puede haber revertido toda la transacción; se conserva la excepción que originó el fallo.
            }

            throw;
        }
    }

    private static async Task<NuevoTicketCreadoRespuesta> RegistrarIncidenciaAsync(
        SqlConnection conexion,
        SqlTransaction transaccion,
        string usuario,
        CrearNuevoTicketSolicitud solicitud,
        Guid idCorrelacion,
        CancellationToken cancellationToken)
    {
        await using var comando = new SqlCommand("dbo.Usp_TI_Registrar_Incidencia", conexion, transaccion) { CommandType = CommandType.StoredProcedure };
        comando.Parameters.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;
        comando.Parameters.Add("@cLinea", SqlDbType.Char, 3).Value = solicitud.Linea;
        comando.Parameters.Add("@cTipo", SqlDbType.Char, 3).Value = solicitud.Tipo;
        comando.Parameters.Add("@cTitulo", SqlDbType.NVarChar, 250).Value = solicitud.Titulo;
        comando.Parameters.Add("@cDetalle", SqlDbType.NVarChar, -1).Value = solicitud.Detalle;
        comando.Parameters.Add("@cMensajeError", SqlDbType.NVarChar, 1000).Value = string.IsNullOrWhiteSpace(solicitud.MensajeError) ? DBNull.Value : solicitud.MensajeError;
        comando.Parameters.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = idCorrelacion;

        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
        if (!await lector.ReadAsync(cancellationToken)) throw new InvalidOperationException("No se obtuvo el número de la incidencia registrada.");

        return new NuevoTicketCreadoRespuesta
        {
            IncidenciaNumero = LeerCadena(lector, "IncidenciaNumero"),
            FechaRegistro = Convert.ToDateTime(lector["FechaRegistro"])
        };
    }

    private static async Task RegistrarAdjuntoAsync(
        SqlConnection conexion,
        SqlTransaction transaccion,
        string incidenciaNumero,
        string usuario,
        NuevoTicketAdjuntoRegistro adjunto,
        CancellationToken cancellationToken)
    {
        await using var comando = new SqlCommand("dbo.Usp_TI_Registrar_IncidenciaAdjunto", conexion, transaccion) { CommandType = CommandType.StoredProcedure };
        comando.Parameters.Add("@cIncidenciaNumero", SqlDbType.VarChar, 12).Value = incidenciaNumero;
        comando.Parameters.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;
        comando.Parameters.Add("@cNombreOriginal", SqlDbType.NVarChar, 260).Value = adjunto.NombreOriginal;
        comando.Parameters.Add("@cNombreArchivo", SqlDbType.NVarChar, 260).Value = adjunto.NombreArchivo;
        comando.Parameters.Add("@cRutaArchivo", SqlDbType.NVarChar, 1000).Value = adjunto.RutaArchivo;
        comando.Parameters.Add("@cTipoMime", SqlDbType.VarChar, 100).Value = adjunto.TipoMime;
        comando.Parameters.Add("@nTamanoBytes", SqlDbType.BigInt).Value = adjunto.TamanoBytes;
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string LeerCadena(SqlDataReader lector, string columna) => lector[columna].ToString()?.Trim() ?? string.Empty;
}
