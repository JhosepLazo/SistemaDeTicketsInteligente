/**
 * Archivo: IdentidadCorporativa.cs
 * Objetivo: Validar credenciales y consultar usuarios y cargos en el sistema corporativo Spring.
 * Responsabilidad: Invocar los procedimientos corporativos existentes sin copiar contraseñas ni modificar Spring.
 * Dependencias: IConfiguration (IdentidadCorporativa:Habilitada y la cadena CnnSpring) y Microsoft.Data.SqlClient.
 * Flujo: AutenticacionBLL / ConfiguracionTIBLL -> IdentidadCorporativa -> procedimientos de Spring.
 * Consideraciones: Los procedimientos corporativos se llaman por posición de parámetros (su contrato no usa nombres propios);
 *   si su firma cambia, se informa en lugar de enviar valores a parámetros equivocados.
 */

namespace SistemaTicketsInteligente.Api.Comun;

public sealed class IdentidadCorporativa
{
    private readonly string? cadenaConexion;
    private readonly bool habilitada;

    public IdentidadCorporativa(IConfiguration configuration)
    {
        habilitada = configuration.GetValue<bool>("IdentidadCorporativa:Habilitada");
        var cadenaConfigurada = configuration.GetConnectionString("CnnSpring");
        cadenaConexion = string.IsNullOrWhiteSpace(cadenaConfigurada) ? null : BaseDatos.PrepararCadena(cadenaConfigurada);
    }

    public bool Disponible => habilitada && !string.IsNullOrWhiteSpace(cadenaConexion);

    public async Task<bool> AutenticarAsync(string usuario, string contrasena, CancellationToken ct = default)
    {
        await using var conexion = await AbrirAsync(ct);
        await using var comando = CrearComandoPorPosicion(conexion, "spUsuarioComprobarSpring", [usuario, contrasena], ct);
        var resultado = await comando.ExecuteScalarAsync(ct);
        return resultado is not null && resultado is not DBNull && Convert.ToInt32(resultado) != 0;
    }

    public async Task<UsuarioCorporativo?> ObtenerUsuarioAsync(string usuario, CancellationToken ct = default)
    {
        await using var conexion = await AbrirAsync(ct);
        await using var comando = CrearComandoPorPosicion(conexion, "Usp_Inc_SelectUsuarioByUsuario", [usuario], ct);
        await using var lector = await comando.ExecuteReaderAsync(ct);
        if (!await lector.ReadAsync(ct)) return null;
        return new UsuarioCorporativo
        {
            Usuario = Columna(lector, "Usuario"), NombreCompleto = Columna(lector, "Nombre"), Cargo = Columna(lector, "CodigoCargo"),
            Documento = Columna(lector, "Documento"), Estado = Columna(lector, "Estado"), EstadoEmpleado = Columna(lector, "EstadoEmpleado"),
            Correo = Columna(lector, "Correo")
        };
    }

    public async Task<List<CargoCorporativo>> ObtenerCargosAsync(CancellationToken ct = default)
    {
        await using var conexion = await AbrirAsync(ct);
        await using var comando = CrearComandoPorPosicion(conexion, "Usp_Inc_SelectAllCargos", [], ct);
        await using var lector = await comando.ExecuteReaderAsync(ct);
        return await lector.ListaAsync(f => new CargoCorporativo { Cargo = Columna(f, "CodigoPuesto"), Descripcion = Columna(f, "Descripcion") }, ct);
    }

    private async Task<SqlConnection> AbrirAsync(CancellationToken ct)
    {
        if (!Disponible) throw new InvalidOperationException("La identidad corporativa no se encuentra configurada.");
        var conexion = new SqlConnection(cadenaConexion);
        await conexion.OpenAsync(ct);
        return conexion;
    }

    private static SqlCommand CrearComandoPorPosicion(SqlConnection conexion, string procedimiento, IReadOnlyList<object?> valores, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var comando = new SqlCommand(procedimiento, conexion) { CommandType = CommandType.StoredProcedure };
        SqlCommandBuilder.DeriveParameters(comando);
        var entrada = comando.Parameters.Cast<SqlParameter>().Where(x => x.Direction is ParameterDirection.Input or ParameterDirection.InputOutput).ToList();
        if (entrada.Count != valores.Count)
        {
            comando.Dispose();
            throw new InvalidOperationException($"El procedimiento corporativo '{procedimiento}' no conserva el contrato esperado.");
        }
        for (var i = 0; i < valores.Count; i++) entrada[i].Value = valores[i] ?? DBNull.Value;
        return comando;
    }

    // Spring no garantiza las mayúsculas de sus columnas: se busca sin distinguirlas y una columna ausente se lee vacía.
    private static string Columna(SqlDataReader lector, string columna)
    {
        for (var i = 0; i < lector.FieldCount; i++)
            if (string.Equals(lector.GetName(i), columna, StringComparison.OrdinalIgnoreCase))
                return lector.IsDBNull(i) ? string.Empty : lector.GetValue(i).ToString()?.Trim() ?? string.Empty;
        return string.Empty;
    }
}
