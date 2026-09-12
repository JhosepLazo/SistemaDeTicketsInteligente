using Microsoft.Data.SqlClient;

namespace SistemaTicketsInteligente.Datos.ComponenteDAO.Data;

public sealed class ConexionSqlServer
{
    private readonly string cadenaConexion;

    public ConexionSqlServer(string cadenaConexion)
    {
        if (string.IsNullOrWhiteSpace(cadenaConexion))
        {
            throw new ArgumentException("La cadena de conexión no puede estar vacía.", nameof(cadenaConexion));
        }

        this.cadenaConexion = cadenaConexion;
    }

    public SqlConnection CrearConexion() => new(cadenaConexion);
}
