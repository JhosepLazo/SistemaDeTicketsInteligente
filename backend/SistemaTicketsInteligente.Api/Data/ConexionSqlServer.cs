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