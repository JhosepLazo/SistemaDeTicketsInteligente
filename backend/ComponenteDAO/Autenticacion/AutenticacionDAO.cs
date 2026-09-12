using System.Data;
using Microsoft.Data.SqlClient;
using SistemaTicketsInteligente.Datos.ComponenteDAO.Data;
using SistemaTicketsInteligente.Entidades.ComponenteDTO.Autenticacion;

namespace SistemaTicketsInteligente.Datos.ComponenteDAO.Autenticacion;

public sealed class AutenticacionDAO
{
    private readonly ConexionSqlServer conexionSqlServer;

    public AutenticacionDAO(ConexionSqlServer conexionSqlServer)
    {
        this.conexionSqlServer = conexionSqlServer;
    }

    public async Task<UsuarioAutenticacion?> BuscarUsuarioAsync(
        string usuario,
        CancellationToken cancellationToken = default)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("Usp_TI_BuscarUsuarioAutenticacion", conexion)
        {
            CommandType = CommandType.StoredProcedure
        };

        comando.Parameters.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;

        await conexion.OpenAsync(cancellationToken);
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

        if (!await lector.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new UsuarioAutenticacion
        {
            Usuario = LeerCadena(lector, "Usuario"),
            NombreCompleto = LeerCadena(lector, "NombreCompleto"),
            ClaveHash = LeerCadena(lector, "ClaveHash"),
            Area = LeerCadena(lector, "Area"),
            Perfil = LeerCadena(lector, "Perfil"),
            EstadoUsuario = LeerCadena(lector, "EstadoUsuario"),
            EstadoPerfil = LeerCadena(lector, "EstadoPerfil")
        };
    }

    private static string LeerCadena(SqlDataReader lector, string columna)
    {
        var ordinal = lector.GetOrdinal(columna);
        return lector.IsDBNull(ordinal) ? string.Empty : lector.GetString(ordinal);
    }
}
