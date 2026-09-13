/*
 * Archivo: NotificacionesDAO.cs
 * Objetivo: Ejecutar las operaciones SQL de consulta y lectura de notificaciones del usuario autenticado.
 * Responsabilidad: Mapear resultados de los Stored Procedures sin aplicar reglas de presentación ni autorización funcional.
 * Dependencias: ConexionSqlServer, Microsoft.Data.SqlClient y NotificacionesDTO.
 * Flujo: NotificacionesBLL -> NotificacionesDAO -> SQL Server.
 * Consideraciones: El usuario siempre se envía como parámetro tipado y una notificación solo puede marcarse desde su propietario.
 */

using System.Data;
using Microsoft.Data.SqlClient;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.DAO;

public sealed class NotificacionesDAO
{
    private readonly ConexionSqlServer conexionSqlServer;

    public NotificacionesDAO(ConexionSqlServer conexionSqlServer)
    {
        this.conexionSqlServer = conexionSqlServer;
    }

    public async Task<NotificacionesRespuesta> ObtenerAsync(string usuario, CancellationToken cancellationToken = default)
    {
        var respuesta = new NotificacionesRespuesta();
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Obtener_Notificaciones", conexion) { CommandType = CommandType.StoredProcedure };
        comando.Parameters.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;

        await conexion.OpenAsync(cancellationToken);
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

        if (await lector.ReadAsync(cancellationToken)) respuesta.NoLeidas = Convert.ToInt32(lector["NoLeidas"]);
        await lector.NextResultAsync(cancellationToken);

        while (await lector.ReadAsync(cancellationToken))
        {
            respuesta.Notificaciones.Add(new NotificacionItem
            {
                NotificacionNumero = Convert.ToInt64(lector["NotificacionNumero"]),
                IncidenciaNumero = LeerCadena(lector, "IncidenciaNumero"),
                Tipo = LeerCadena(lector, "Tipo"),
                Titulo = LeerCadena(lector, "Titulo"),
                Mensaje = LeerCadena(lector, "Mensaje"),
                Ruta = LeerCadena(lector, "Ruta"),
                Leida = Convert.ToBoolean(lector["Leida"]),
                Fecha = Convert.ToDateTime(lector["Fecha"])
            });
        }

        return respuesta;
    }

    public async Task MarcarLeidaAsync(string usuario, long notificacionNumero, CancellationToken cancellationToken = default)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Marcar_NotificacionLeida", conexion) { CommandType = CommandType.StoredProcedure };
        comando.Parameters.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;
        comando.Parameters.Add("@nNotificacionNumero", SqlDbType.BigInt).Value = notificacionNumero;
        await conexion.OpenAsync(cancellationToken);
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string LeerCadena(SqlDataReader lector, string columna) => lector[columna] is DBNull ? string.Empty : lector[columna].ToString()?.Trim() ?? string.Empty;
}
