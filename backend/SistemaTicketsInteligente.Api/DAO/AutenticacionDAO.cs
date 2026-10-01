/**
 * Archivo: AutenticacionDAO.cs
 * Objetivo: Ejecutar las operaciones SQL necesarias para autenticar y auditar accesos.
 * Responsabilidad: Consultar el usuario mediante Stored Procedure y registrar eventos de autenticación sin aplicar reglas de negocio.
 * Dependencias: ConexionSqlServer, Microsoft.Data.SqlClient, ILogger, DTO de autenticación y Stored Procedures de autenticación.
 * Flujo: AutenticacionBLL -> AutenticacionDAO -> Stored Procedures -> SQL Server.
 * Consideraciones: No valida contraseñas ni decide permisos; todos los valores se envían mediante parámetros tipados.
 */

using System.Data;
using Microsoft.Data.SqlClient;
using SistemaTicketsInteligente.Api.DTO.Autenticacion;

namespace SistemaTicketsInteligente.Api.DAO;

public sealed class AutenticacionDAO
{
    private readonly ConexionSqlServer conexionSqlServer;
    private readonly ILogger<AutenticacionDAO> logger;

    public AutenticacionDAO(ConexionSqlServer conexionSqlServer, ILogger<AutenticacionDAO> logger)
    {
        this.conexionSqlServer = conexionSqlServer;
        this.logger = logger;
    }

    public async Task<UsuarioAutenticacion?> BuscarUsuarioAsync(string usuario, CancellationToken cancellationToken = default)
    {
        try
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
        catch (SqlException ex)
        {
            logger.LogError(
                ex,
                "Error SQL al consultar autenticación del usuario {Usuario}. Código SQL {CodigoSql}",
                usuario,
                ex.Number);
            throw;
        }
    }

    public async Task RegistrarAuditoriaAsync(string? usuario, string registro, string evento, string resultado, Guid idCorrelacion, CancellationToken cancellationToken = default)
    {
        try
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
        catch (SqlException ex)
        {
            logger.LogError(
                ex,
                "Error SQL al registrar auditoría {Evento} para usuario {Usuario}. Código SQL {CodigoSql}",
                evento,
                usuario ?? "SIN_USUARIO",
                ex.Number);
            throw;
        }
    }

    private static string LeerCadena(SqlDataReader lector, string columna) => lector[columna].ToString()?.Trim() ?? string.Empty;
}
