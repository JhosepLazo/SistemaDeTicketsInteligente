/**
 * Archivo: InicioTIDAO.cs
 * Objetivo: Obtener desde SQL Server toda la información necesaria para construir el Inicio del operador TI.
 * Responsabilidad: Ejecutar el Stored Procedure del módulo Inicio TI y mapear sus conjuntos de resultados a DTO sin aplicar reglas de negocio.
 * Dependencias: ConexionSqlServer, Microsoft.Data.SqlClient, InicioTIDTO y dbo.Usp_TI_Obtener_InicioTI.
 * Flujo: InicioTIBLL -> InicioTIDAO -> Stored Procedure -> SQL Server.
 * Consideraciones: Realiza una sola llamada a base de datos, usa parámetros tipados y limita la consulta al área TI del operador autenticado.
 */

using System.Data;
using Microsoft.Data.SqlClient;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.DAO;

public sealed class InicioTIDAO
{
    private readonly ConexionSqlServer conexionSqlServer;

    public InicioTIDAO(ConexionSqlServer conexionSqlServer)
    {
        this.conexionSqlServer = conexionSqlServer;
    }

    public async Task<InicioTIRespuesta> ObtenerAsync(string usuario, string area, CancellationToken cancellationToken = default)
    {
        var respuesta = new InicioTIRespuesta();

        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Obtener_InicioTI", conexion) { CommandType = CommandType.StoredProcedure };
        comando.Parameters.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;
        comando.Parameters.Add("@cArea", SqlDbType.Char, 3).Value = area;

        await conexion.OpenAsync(cancellationToken);
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

        if (await lector.ReadAsync(cancellationToken))
        {
            respuesta.Resumen = new InicioTIResumen
            {
                Pendientes = LeerEntero(lector, "Pendientes"),
                PendientesDesdeAyer = LeerEntero(lector, "PendientesDesdeAyer"),
                EnAtencion = LeerEntero(lector, "EnAtencion"),
                EnProgresoHoy = LeerEntero(lector, "EnProgresoHoy"),
                RequierenAccion = LeerEntero(lector, "RequierenAccion"),
                SinAsignar = LeerEntero(lector, "SinAsignar"),
                PrioridadAlta = LeerEntero(lector, "PrioridadAlta"),
                TicketsActivos = LeerEntero(lector, "TicketsActivos"),
                MisAsignados = LeerEntero(lector, "MisAsignados")
            };
        }

        await lector.NextResultAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            respuesta.RequierenAtencion.Add(new InicioTITicketPrioritario
            {
                IncidenciaNumero = LeerCadena(lector, "IncidenciaNumero"),
                UsuarioSolicitante = LeerCadena(lector, "UsuarioSolicitante"),
                Titulo = LeerCadena(lector, "Titulo"),
                Estado = LeerCadena(lector, "Estado"),
                EstadoDescripcion = LeerCadena(lector, "EstadoDescripcion"),
                Prioridad = LeerEnteroNullable(lector, "Prioridad"),
                UsuarioTI = LeerCadena(lector, "UsuarioTI"),
                Responsable = LeerCadena(lector, "Responsable"),
                UltimaFechaModif = LeerFecha(lector, "UltimaFechaModif"),
                SlaMinutosRestantes = LeerEnteroNullable(lector, "SlaMinutosRestantes"),
                TipoAtencion = LeerCadena(lector, "TipoAtencion"),
                Accion = LeerCadena(lector, "Accion")
            });
        }

        await lector.NextResultAsync(cancellationToken);
        if (await lector.ReadAsync(cancellationToken))
        {
            respuesta.Recordatorios = new InicioTIReminder
            {
                AprobacionesPendientes = LeerEntero(lector, "AprobacionesPendientes"),
                SlaPorVencer = LeerEntero(lector, "SlaPorVencer"),
                TicketsReabiertos = LeerEntero(lector, "TicketsReabiertos")
            };
        }

        await lector.NextResultAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            respuesta.TicketsActivos.Add(new InicioTITicketActivo
            {
                IncidenciaNumero = LeerCadena(lector, "IncidenciaNumero"),
                UsuarioSolicitante = LeerCadena(lector, "UsuarioSolicitante"),
                Titulo = LeerCadena(lector, "Titulo"),
                Prioridad = LeerEnteroNullable(lector, "Prioridad"),
                AreaTI = LeerCadena(lector, "AreaTI"),
                GrupoSoporte = LeerCadena(lector, "GrupoSoporte"),
                Estado = LeerCadena(lector, "Estado"),
                EstadoDescripcion = LeerCadena(lector, "EstadoDescripcion"),
                UsuarioTI = LeerCadena(lector, "UsuarioTI"),
                Responsable = LeerCadena(lector, "Responsable"),
                UltimaFechaModif = LeerFecha(lector, "UltimaFechaModif")
            });
        }

        await lector.NextResultAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            respuesta.ActividadReciente.Add(new InicioTIActividad
            {
                IncidenciaNumero = LeerCadena(lector, "IncidenciaNumero"),
                Titulo = LeerCadena(lector, "Titulo"),
                Estado = LeerCadena(lector, "Estado"),
                EstadoDescripcion = LeerCadena(lector, "EstadoDescripcion"),
                Actor = LeerCadena(lector, "Actor"),
                FechaCambio = LeerFecha(lector, "FechaCambio")
            });
        }

        return respuesta;
    }

    private static string LeerCadena(SqlDataReader lector, string columna) => lector[columna] is DBNull ? string.Empty : lector[columna].ToString()?.Trim() ?? string.Empty;
    private static int LeerEntero(SqlDataReader lector, string columna) => lector[columna] is DBNull ? 0 : Convert.ToInt32(lector[columna]);
    private static int? LeerEnteroNullable(SqlDataReader lector, string columna) => lector[columna] is DBNull ? null : Convert.ToInt32(lector[columna]);
    private static DateTime LeerFecha(SqlDataReader lector, string columna) => lector[columna] is DBNull ? DateTime.MinValue : Convert.ToDateTime(lector[columna]);
}
