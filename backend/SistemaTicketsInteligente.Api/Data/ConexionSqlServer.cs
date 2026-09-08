/**
    Archivo: ConexionSqlServer.cs
    Objetivo: Proporcionar el punto de creación de conexiones con SQL Server.
    Responsabilidad: Centralizar la construcción de instancias SqlConnection sin ejecutar consultas ni abrir la conexión.
    Dependencias: Microsoft.Data.SqlClient y la configuración de conexión existente del proyecto.
    Flujo: Repositorio -> ConexionSqlServer -> SQL Server.
    Consideraciones: Esta cabecera documenta el comportamiento existente; no modifica la conexión ni sus datos.
*/

using Microsoft.Data.SqlClient;

namespace SistemaTicketsInteligente.Api.Data;

public class ConexionSqlServer
{
    private const string cadenaConexion =
        "Server=localhost;Database=SistemaTicketsInteligente;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;";

    public SqlConnection CrearConexion()
    {
        return new SqlConnection(cadenaConexion);
    }
}
