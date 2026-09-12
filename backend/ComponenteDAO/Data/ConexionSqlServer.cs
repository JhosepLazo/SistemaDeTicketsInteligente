/**
 * Archivo: ConexionSqlServer.cs
 * Objetivo: Proporcionar conexiones hacia SQL Server a partir de la configuración recibida por la aplicación.
 * Responsabilidad: Validar y conservar la cadena de conexión, y crear instancias SqlConnection cuando la capa DAO las solicite.
 * Dependencias: Microsoft.Data.SqlClient.
 * Flujo: Program.cs -> ConexionSqlServer -> AutenticacionDAO -> SQL Server.
 * Consideraciones: No abre conexiones ni ejecuta consultas; las credenciales no deben escribirse directamente dentro de esta clase.
 */

using Microsoft.Data.SqlClient;

namespace SistemaTicketsInteligente.Datos.ComponenteDAO.Data;

public sealed class ConexionSqlServer
{
    private readonly string cadenaConexion;

    public ConexionSqlServer(string cadenaConexion)
    {
        if (string.IsNullOrWhiteSpace(cadenaConexion)) throw new ArgumentException("La cadena de conexión no puede estar vacía.", nameof(cadenaConexion));
        this.cadenaConexion = cadenaConexion;
    }

    public SqlConnection CrearConexion() => new(cadenaConexion);
}
