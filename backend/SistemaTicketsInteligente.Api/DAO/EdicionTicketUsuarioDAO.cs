/*
 * Archivo: EdicionTicketUsuarioDAO.cs
 * Objetivo: Ejecutar la edición controlada de un ticket propio antes de su procesamiento técnico.
 * Responsabilidad: Invocar el Stored Procedure de edición con parámetros tipados y conservar la validación de propiedad/estado en SQL Server.
 * Dependencias: ConexionSqlServer, Microsoft.Data.SqlClient y EdicionTicketUsuarioDTO.
 * Flujo: EdicionTicketUsuarioBLL -> EdicionTicketUsuarioDAO -> dbo.Usp_TI_Editar_TicketUsuario -> SQL Server.
 * Consideraciones: No permite modificar clasificación técnica, prioridad, impacto, complejidad ni responsable TI.
 */

using System.Data;
using Microsoft.Data.SqlClient;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.DAO;

public sealed class EdicionTicketUsuarioDAO
{
    private readonly ConexionSqlServer conexionSqlServer;

    public EdicionTicketUsuarioDAO(ConexionSqlServer conexionSqlServer)
    {
        this.conexionSqlServer = conexionSqlServer;
    }

    public async Task EditarAsync(string usuario, string incidenciaNumero, EditarTicketUsuarioSolicitud solicitud, Guid idCorrelacion, CancellationToken ct = default)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Editar_TicketUsuario", conexion) { CommandType = CommandType.StoredProcedure };
        comando.Parameters.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;
        comando.Parameters.Add("@cIncidenciaNumero", SqlDbType.VarChar, 12).Value = incidenciaNumero;
        comando.Parameters.Add("@cLinea", SqlDbType.Char, 3).Value = solicitud.Linea;
        comando.Parameters.Add("@cTipo", SqlDbType.Char, 3).Value = solicitud.Tipo;
        comando.Parameters.Add("@cTitulo", SqlDbType.NVarChar, 250).Value = solicitud.Titulo;
        comando.Parameters.Add("@cDetalle", SqlDbType.NVarChar, -1).Value = solicitud.Detalle;
        comando.Parameters.Add("@cMensajeError", SqlDbType.NVarChar, 1000).Value = string.IsNullOrWhiteSpace(solicitud.MensajeError) ? DBNull.Value : solicitud.MensajeError;
        comando.Parameters.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = idCorrelacion;

        await conexion.OpenAsync(ct);
        try { await comando.ExecuteNonQueryAsync(ct); }
        catch (SqlException ex) when (ex.Number is >= 50430 and <= 50439) { throw new InvalidOperationException(ex.Message, ex); }
    }
}
