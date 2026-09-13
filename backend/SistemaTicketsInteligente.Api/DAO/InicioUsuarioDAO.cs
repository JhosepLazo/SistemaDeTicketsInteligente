/**
 * Archivo: InicioUsuarioDAO.cs
 * Objetivo: Obtener desde SQL Server toda la información necesaria para construir el Inicio del usuario.
 * Responsabilidad: Ejecutar el Stored Procedure del módulo Inicio y mapear sus conjuntos de resultados a DTO sin aplicar reglas de negocio.
 * Dependencias: ConexionSqlServer, Microsoft.Data.SqlClient, InicioUsuarioDTO y dbo.Usp_TI_Obtener_InicioUsuario.
 * Flujo: InicioUsuarioBLL -> InicioUsuarioDAO -> Stored Procedure -> SQL Server.
 * Consideraciones: Realiza una sola llamada a base de datos, usa parámetros tipados y solo consulta información perteneciente al usuario autenticado.
 */

using System.Data;
using Microsoft.Data.SqlClient;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.DAO;

public sealed class InicioUsuarioDAO
{
    private readonly ConexionSqlServer conexionSqlServer;

    public InicioUsuarioDAO(ConexionSqlServer conexionSqlServer)
    {
        this.conexionSqlServer = conexionSqlServer;
    }

    public async Task<InicioUsuarioRespuesta> ObtenerAsync(string usuario, CancellationToken cancellationToken = default)
    {
        var respuesta = new InicioUsuarioRespuesta();

        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Obtener_InicioUsuario", conexion) { CommandType = CommandType.StoredProcedure };
        comando.Parameters.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;

        await conexion.OpenAsync(cancellationToken);
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

        if (await lector.ReadAsync(cancellationToken))
        {
            respuesta.Resumen = new InicioUsuarioResumen
            {
                TicketsActivos = LeerEntero(lector, "TicketsActivos"),
                EnAtencion = LeerEntero(lector, "EnAtencion"),
                RequierenAtencion = LeerEntero(lector, "RequierenAtencion"),
                Resueltos30Dias = LeerEntero(lector, "Resueltos30Dias"),
                PendientesCalificacion = LeerEntero(lector, "PendientesCalificacion")
            };
        }

        await lector.NextResultAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            respuesta.RequierenAtencion.Add(new InicioUsuarioTicket
            {
                IncidenciaNumero = LeerCadena(lector, "IncidenciaNumero"),
                Titulo = LeerCadena(lector, "Titulo"),
                Estado = LeerCadena(lector, "Estado"),
                EstadoDescripcion = LeerCadena(lector, "EstadoDescripcion"),
                Accion = LeerCadena(lector, "Accion"),
                UltimaFechaModif = LeerFecha(lector, "UltimaFechaModif")
            });
        }

        await lector.NextResultAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            respuesta.TicketsRecientes.Add(new InicioUsuarioTicket
            {
                IncidenciaNumero = LeerCadena(lector, "IncidenciaNumero"),
                Titulo = LeerCadena(lector, "Titulo"),
                Estado = LeerCadena(lector, "Estado"),
                EstadoDescripcion = LeerCadena(lector, "EstadoDescripcion"),
                Responsable = LeerCadena(lector, "Responsable"),
                UltimaFechaModif = LeerFecha(lector, "UltimaFechaModif")
            });
        }

        await lector.NextResultAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            respuesta.AccionesPendientes.Add(new InicioUsuarioAccionPendiente
            {
                IncidenciaNumero = LeerCadena(lector, "IncidenciaNumero"),
                Titulo = LeerCadena(lector, "Titulo"),
                TipoAccion = LeerCadena(lector, "TipoAccion"),
                Descripcion = LeerCadena(lector, "Descripcion"),
                UltimaFechaModif = LeerFecha(lector, "UltimaFechaModif")
            });
        }

        await lector.NextResultAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            respuesta.ActividadReciente.Add(new InicioUsuarioActividad
            {
                IncidenciaNumero = LeerCadena(lector, "IncidenciaNumero"),
                Titulo = LeerCadena(lector, "Titulo"),
                EstadoDescripcion = LeerCadena(lector, "EstadoDescripcion"),
                Actor = LeerCadena(lector, "Actor"),
                FechaCambio = LeerFecha(lector, "FechaCambio")
            });
        }

        return respuesta;
    }

    private static string LeerCadena(SqlDataReader lector, string columna) => lector[columna].ToString()?.Trim() ?? string.Empty;
    private static int LeerEntero(SqlDataReader lector, string columna) => lector[columna] is DBNull ? 0 : Convert.ToInt32(lector[columna]);
    private static DateTime LeerFecha(SqlDataReader lector, string columna) => lector[columna] is DBNull ? DateTime.MinValue : Convert.ToDateTime(lector[columna]);
}
