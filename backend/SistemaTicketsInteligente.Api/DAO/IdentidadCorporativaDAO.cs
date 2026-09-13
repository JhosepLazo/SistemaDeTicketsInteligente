/*
 * Archivo: IdentidadCorporativaDAO.cs
 * Objetivo: Consultar y autenticar usuarios contra el origen corporativo Spring cuando la integración se encuentre habilitada.
 * Responsabilidad: Ejecutar únicamente los Stored Procedures corporativos ya utilizados por el sistema legado y mapear datos seguros de identidad/cargo.
 * Dependencias: Microsoft.Data.SqlClient, IConfiguration y IdentidadCorporativaDTO.
 * Flujo: AutenticacionBLL / ConfiguracionTIBLL -> IdentidadCorporativaDAO -> Spring SQL Server.
 * Consideraciones: La contraseña solo se envía al procedimiento corporativo de autenticación; nunca se registra, devuelve ni persiste en la base local. Los parámetros se asignan por posición porque los nombres internos del legado no forman parte del contrato actual.
 */

using System.Data;
using Microsoft.Data.SqlClient;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.DAO;

public sealed class IdentidadCorporativaDAO
{
    private readonly string? cadenaConexion;
    private readonly bool habilitada;

    public IdentidadCorporativaDAO(IConfiguration configuration)
    {
        habilitada = configuration.GetValue<bool>("IdentidadCorporativa:Habilitada");
        cadenaConexion = configuration.GetConnectionString("CnnSpring");
    }

    public bool Disponible => habilitada && !string.IsNullOrWhiteSpace(cadenaConexion);

    public async Task<bool> AutenticarAsync(string usuario, string contrasena, CancellationToken cancellationToken = default)
    {
        if (!Disponible) throw new InvalidOperationException("La identidad corporativa no se encuentra configurada.");

        await using var conexion = new SqlConnection(cadenaConexion);
        await conexion.OpenAsync(cancellationToken);
        var resultado = await EjecutarEscalarPorPosicionAsync(conexion, "spUsuarioComprobarSpring", [usuario, contrasena], cancellationToken);
        return resultado is not null && resultado is not DBNull && Convert.ToInt32(resultado) != 0;
    }

    public async Task<UsuarioCorporativo?> ObtenerUsuarioAsync(string usuario, CancellationToken cancellationToken = default)
    {
        if (!Disponible) throw new InvalidOperationException("La identidad corporativa no se encuentra configurada.");

        await using var conexion = new SqlConnection(cadenaConexion);
        await conexion.OpenAsync(cancellationToken);
        await using var comando = await CrearComandoPorPosicionAsync(conexion, "Usp_Inc_SelectUsuarioByUsuario", [usuario], cancellationToken);
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
        if (!await lector.ReadAsync(cancellationToken)) return null;

        return new UsuarioCorporativo
        {
            Usuario = LeerCadena(lector, "Usuario"),
            NombreCompleto = LeerCadena(lector, "Nombre"),
            Cargo = LeerCadena(lector, "CodigoCargo"),
            Documento = LeerCadena(lector, "Documento"),
            Estado = LeerCadena(lector, "Estado"),
            EstadoEmpleado = LeerCadena(lector, "EstadoEmpleado"),
            Correo = LeerCadena(lector, "Correo")
        };
    }

    public async Task<List<CargoCorporativo>> ObtenerCargosAsync(CancellationToken cancellationToken = default)
    {
        if (!Disponible) throw new InvalidOperationException("La identidad corporativa no se encuentra configurada.");

        var cargos = new List<CargoCorporativo>();
        await using var conexion = new SqlConnection(cadenaConexion);
        await conexion.OpenAsync(cancellationToken);
        await using var comando = await CrearComandoPorPosicionAsync(conexion, "Usp_Inc_SelectAllCargos", [], cancellationToken);
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

        while (await lector.ReadAsync(cancellationToken))
        {
            cargos.Add(new CargoCorporativo
            {
                Cargo = LeerCadena(lector, "CodigoPuesto"),
                Descripcion = LeerCadena(lector, "Descripcion")
            });
        }

        return cargos;
    }

    private static async Task<object?> EjecutarEscalarPorPosicionAsync(SqlConnection conexion, string procedimiento, IReadOnlyList<object?> valores, CancellationToken cancellationToken)
    {
        await using var comando = await CrearComandoPorPosicionAsync(conexion, procedimiento, valores, cancellationToken);
        return await comando.ExecuteScalarAsync(cancellationToken);
    }

    private static Task<SqlCommand> CrearComandoPorPosicionAsync(SqlConnection conexion, string procedimiento, IReadOnlyList<object?> valores, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var comando = new SqlCommand(procedimiento, conexion) { CommandType = CommandType.StoredProcedure };
        SqlCommandBuilder.DeriveParameters(comando);

        var parametrosEntrada = comando.Parameters.Cast<SqlParameter>()
            .Where(x => x.Direction is ParameterDirection.Input or ParameterDirection.InputOutput)
            .ToList();

        if (parametrosEntrada.Count != valores.Count)
        {
            comando.Dispose();
            throw new InvalidOperationException($"El procedimiento corporativo '{procedimiento}' no conserva el contrato esperado.");
        }

        for (var indice = 0; indice < valores.Count; indice++)
            parametrosEntrada[indice].Value = valores[indice] ?? DBNull.Value;

        return Task.FromResult(comando);
    }

    private static string LeerCadena(SqlDataReader lector, string columna)
    {
        for (var indice = 0; indice < lector.FieldCount; indice++)
            if (string.Equals(lector.GetName(indice), columna, StringComparison.OrdinalIgnoreCase))
                return lector.IsDBNull(indice) ? string.Empty : lector.GetValue(indice).ToString()?.Trim() ?? string.Empty;

        return string.Empty;
    }
}
