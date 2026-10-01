/*
 * Archivo: RecursosSoporteDAO.cs
 * Objetivo: Consultar los recursos de soporte publicados para usuarios autenticados.
 * Responsabilidad: Mapear formatos y artículos visibles, y resolver archivos por código sin exponer rutas físicas al cliente.
 * Dependencias: ConexionSqlServer, Microsoft.Data.SqlClient y RecursosSoporteDTO.
 * Flujo: RecursosSoporteBLL -> RecursosSoporteDAO -> Stored Procedures -> SQL Server.
 * Consideraciones: Solo lee contenido marcado como activo/visible; no contiene lógica de IA.
 */

using System.Data;
using Microsoft.Data.SqlClient;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.DAO;

public sealed class RecursosSoporteDAO
{
    private readonly ConexionSqlServer conexionSqlServer;

    public RecursosSoporteDAO(ConexionSqlServer conexionSqlServer)
    {
        this.conexionSqlServer = conexionSqlServer;
    }

    public async Task<RecursosSoporteRespuesta> ObtenerAsync(CancellationToken ct = default)
    {
        var respuesta = new RecursosSoporteRespuesta();
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Obtener_RecursosSoporteUsuario", conexion) { CommandType = CommandType.StoredProcedure };
        await conexion.OpenAsync(ct);
        await using var lector = await comando.ExecuteReaderAsync(ct);

        while (await lector.ReadAsync(ct))
        {
            respuesta.Formatos.Add(new RecursoFormato
            {
                FormatoCodigo = LeerCadena(lector, "FormatoCodigo"), Titulo = LeerCadena(lector, "Titulo"), Descripcion = LeerCadena(lector, "Descripcion"),
                NombreOriginal = LeerCadena(lector, "NombreOriginal"), TipoMime = LeerCadena(lector, "TipoMime"), TipoTicket = LeerCadena(lector, "TipoTicket")
            });
        }

        await lector.NextResultAsync(ct);
        while (await lector.ReadAsync(ct))
        {
            respuesta.Articulos.Add(new RecursoArticulo
            {
                ConocimientoCodigo = LeerCadena(lector, "ConocimientoCodigo"), Titulo = LeerCadena(lector, "Titulo"), Problema = LeerCadena(lector, "Problema"),
                Sintomas = LeerCadena(lector, "Sintomas"), Solucion = LeerCadena(lector, "Solucion"), Procedimiento = LeerCadena(lector, "Procedimiento"), Tipo = LeerCadena(lector, "Tipo")
            });
        }

        return respuesta;
    }

    public async Task<RecursoArchivo?> ObtenerArchivoAsync(string formatoCodigo, CancellationToken ct = default)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Obtener_FormatoSoporteUsuario", conexion) { CommandType = CommandType.StoredProcedure };
        comando.Parameters.Add("@cFormatoCodigo", SqlDbType.VarChar, 20).Value = formatoCodigo;
        await conexion.OpenAsync(ct);
        await using var lector = await comando.ExecuteReaderAsync(ct);
        if (!await lector.ReadAsync(ct)) return null;

        return new RecursoArchivo { NombreOriginal = LeerCadena(lector, "NombreOriginal"), RutaArchivo = LeerCadena(lector, "RutaArchivo"), TipoMime = LeerCadena(lector, "TipoMime") };
    }

    private static string LeerCadena(SqlDataReader lector, string columna) => lector[columna] is DBNull ? string.Empty : lector[columna].ToString()?.Trim() ?? string.Empty;
}
