/**
 * Archivo: ConexionSqlServer.cs
 * Objetivo: Proporcionar conexiones hacia SQL Server a partir de la configuración recibida por la aplicación.
 * Responsabilidad: Validar y conservar la cadena de conexión, y crear instancias SqlConnection cuando la capa DAO las solicite.
 * Dependencias: Microsoft.Data.SqlClient.
 * Flujo: Program.cs -> ConexionSqlServer -> DAO -> SQL Server.
 * Consideraciones: No abre conexiones ni ejecuta consultas; las credenciales no deben escribirse directamente dentro de esta clase.
 */

using Microsoft.Data.SqlClient;

namespace SistemaTicketsInteligente.Api.DAO;

public sealed class ConexionSqlServer
{
    private readonly string cadenaConexion;

    public ConexionSqlServer(string cadenaConexion)
    {
        if (string.IsNullOrWhiteSpace(cadenaConexion)) throw new ArgumentException("La cadena de conexión no puede estar vacía.", nameof(cadenaConexion));
        this.cadenaConexion = PrepararCadena(cadenaConexion);
    }

    public SqlConnection CrearConexion() => new(cadenaConexion);

    public async Task VerificarAsync(CancellationToken cancellationToken = default)
    {
        await using var conexion = CrearConexion();
        await conexion.OpenAsync(cancellationToken);
        await using var comando = new SqlCommand("Select 1", conexion) { CommandTimeout = 10 };
        await comando.ExecuteScalarAsync(cancellationToken);
    }

    public static string PrepararCadena(string cadenaConexion)
    {
        var configuracion = new SqlConnectionStringBuilder(cadenaConexion);
        configuracion.ConnectTimeout = Math.Max(configuracion.ConnectTimeout, 30);
        configuracion.ConnectRetryCount = 3;
        configuracion.ConnectRetryInterval = 2;
        return configuracion.ConnectionString;
    }
}
