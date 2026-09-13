/**
 * Archivo: AutenticacionDAO.cs
 * Objetivo: Ejecutar las operaciones SQL necesarias para autenticar, sincronizar metadata corporativa y auditar accesos.
 * Responsabilidad: Consultar el usuario local, actualizar datos seguros provenientes de Spring y registrar eventos de autenticación sin aplicar reglas de negocio.
 * Dependencias: ConexionSqlServer, Microsoft.Data.SqlClient, DTO de autenticación, IdentidadCorporativaDTO y Stored Procedures de autenticación.
 * Flujo: AutenticacionBLL -> AutenticacionDAO -> Stored Procedures -> SQL Server.
 * Consideraciones: No valida contraseñas ni decide permisos; nunca recibe la contraseña corporativa y todos los valores se envían mediante parámetros tipados.
 */

using System.Data;
using Microsoft.Data.SqlClient;
using SistemaTicketsInteligente.Api.DTO;
using SistemaTicketsInteligente.Api.DTO.Autenticacion;

namespace SistemaTicketsInteligente.Api.DAO;

public sealed class AutenticacionDAO
{
    private readonly ConexionSqlServer conexionSqlServer;

    public AutenticacionDAO(ConexionSqlServer conexionSqlServer)
    {
        this.conexionSqlServer = conexionSqlServer;
    }

    public async Task<UsuarioAutenticacion?> BuscarUsuarioAsync(string usuario, CancellationToken cancellationToken = default)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Buscar_UsuarioAutenticacion", conexion) { CommandType = CommandType.StoredProcedure };
        comando.Parameters.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;

        await conexion.OpenAsync(cancellationToken);
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

        if (!await lector.ReadAsync(cancellationToken)) return null;

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

    public async Task SincronizarUsuarioCorporativoAsync(UsuarioCorporativo usuarioCorporativo, string usuarioModifica, CancellationToken cancellationToken = default)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Sincronizar_UsuarioCorporativo", conexion) { CommandType = CommandType.StoredProcedure };

        var cargo = usuarioCorporativo.Cargo.Trim().ToUpperInvariant();
        if (cargo.Length > 3) cargo = string.Empty;
        var estadoCorporativo = string.Join('/', new[] { usuarioCorporativo.Estado, usuarioCorporativo.EstadoEmpleado }.Where(x => !string.IsNullOrWhiteSpace(x)));

        comando.Parameters.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuarioCorporativo.Usuario;
        comando.Parameters.Add("@cNombreCompleto", SqlDbType.VarChar, 255).Value = usuarioCorporativo.NombreCompleto;
        comando.Parameters.Add("@cCargo", SqlDbType.Char, 3).Value = string.IsNullOrWhiteSpace(cargo) ? DBNull.Value : cargo;
        comando.Parameters.Add("@cDocumento", SqlDbType.VarChar, 20).Value = string.IsNullOrWhiteSpace(usuarioCorporativo.Documento) ? DBNull.Value : usuarioCorporativo.Documento;
        comando.Parameters.Add("@cEstadoCorporativo", SqlDbType.VarChar, 20).Value = string.IsNullOrWhiteSpace(estadoCorporativo) ? DBNull.Value : estadoCorporativo[..Math.Min(estadoCorporativo.Length, 20)];
        comando.Parameters.Add("@cUsuarioModifica", SqlDbType.VarChar, 20).Value = usuarioModifica;

        await conexion.OpenAsync(cancellationToken);
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RegistrarAuditoriaAsync(string? usuario, string registro, string evento, string resultado, Guid idCorrelacion, CancellationToken cancellationToken = default)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Registrar_AuditoriaAutenticacion", conexion) { CommandType = CommandType.StoredProcedure };

        comando.Parameters.Add("@cUsuario", SqlDbType.VarChar, 20).Value = (object?)usuario ?? DBNull.Value;
        comando.Parameters.Add("@cRegistro", SqlDbType.VarChar, 200).Value = registro;
        comando.Parameters.Add("@cEvento", SqlDbType.VarChar, 100).Value = evento;
        comando.Parameters.Add("@cResultado", SqlDbType.VarChar, 20).Value = resultado;
        comando.Parameters.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = idCorrelacion;

        await conexion.OpenAsync(cancellationToken);
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string LeerCadena(SqlDataReader lector, string columna) => lector[columna].ToString()?.Trim() ?? string.Empty;
}
