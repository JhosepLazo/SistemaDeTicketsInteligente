/**
 * Archivo: ConocimientoDAO.cs
 * Objetivo: Leer el corpus de conocimiento autorizado y guardar sus vectores para la búsqueda semántica.
 * Responsabilidad: Ejecutar Usp_TI_Conocimiento_Corpus y Usp_TI_Conocimiento_GuardarVector.
 * Dependencias: ConexionSqlServer y Microsoft.Data.SqlClient.
 * Flujo: ConocimientoSemanticoBLL -> ConocimientoDAO -> Stored Procedures -> SQL Server.
 * Consideraciones: El filtro de visibilidad (colaborador o TI) lo aplica el procedimiento, no el backend.
 */

using System.Data;
using System.Runtime.InteropServices;
using Microsoft.Data.SqlClient;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.DAO;

public sealed class ConocimientoDAO
{
    private readonly ConexionSqlServer conexionSqlServer;

    public ConocimientoDAO(ConexionSqlServer conexionSqlServer)
    {
        this.conexionSqlServer = conexionSqlServer;
    }

    public async Task<List<ConocimientoItem>> ObtenerCorpusAsync(bool soloUsuario, string modelo, CancellationToken ct)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Conocimiento_Corpus", conexion) { CommandType = CommandType.StoredProcedure, CommandTimeout = 60 };
        comando.Parameters.Add("@lSoloUsuario", SqlDbType.Bit).Value = soloUsuario;
        comando.Parameters.Add("@cModelo", SqlDbType.VarChar, 60).Value = modelo;
        await conexion.OpenAsync(ct);
        await using var lector = await comando.ExecuteReaderAsync(ct);
        var corpus = new List<ConocimientoItem>();
        while (await lector.ReadAsync(ct))
        {
            float[]? vector = null;
            if (lector["Vector"] is byte[] bytes && bytes.Length % 4 == 0) vector = MemoryMarshal.Cast<byte, float>(bytes).ToArray();
            corpus.Add(new ConocimientoItem
            {
                Origen = Cadena(lector, "Origen"), Codigo = Cadena(lector, "Codigo"), Titulo = Cadena(lector, "Titulo"), Texto = Cadena(lector, "Texto"),
                Linea = Cadena(lector, "Linea"), Item = Cadena(lector, "Item"), Huella = Cadena(lector, "Huella"), Vector = vector
            });
        }
        return corpus;
    }

    public async Task GuardarVectorAsync(string origen, string codigo, string modelo, string huella, float[] vector, CancellationToken ct)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Conocimiento_GuardarVector", conexion) { CommandType = CommandType.StoredProcedure };
        comando.Parameters.Add("@cOrigen", SqlDbType.Char, 1).Value = origen;
        comando.Parameters.Add("@cCodigo", SqlDbType.VarChar, 20).Value = codigo;
        comando.Parameters.Add("@cModelo", SqlDbType.VarChar, 60).Value = modelo;
        comando.Parameters.Add("@cHuella", SqlDbType.Char, 64).Value = huella;
        comando.Parameters.Add("@nDimensiones", SqlDbType.SmallInt).Value = (short)vector.Length;
        comando.Parameters.Add("@bVector", SqlDbType.VarBinary, -1).Value = MemoryMarshal.AsBytes(vector.AsSpan()).ToArray();
        await conexion.OpenAsync(ct);
        await comando.ExecuteNonQueryAsync(ct);
    }

    private static string Cadena(SqlDataReader lector, string columna) => lector[columna] is DBNull ? string.Empty : lector[columna].ToString()?.Trim() ?? string.Empty;
}
