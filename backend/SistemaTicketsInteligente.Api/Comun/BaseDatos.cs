/**
 * Archivo: BaseDatos.cs
 * Objetivo: Ejecutar los Stored Procedures del sistema con el mismo manejo de conexión y de errores en todos los módulos.
 * Responsabilidad: Abrir la conexión, ejecutar el procedimiento con sus parámetros tipados y convertir los errores de negocio
 *   (Throw 50000-50999 dentro del procedimiento) en InvalidOperationException, que el controlador devuelve como 409 con su mensaje.
 * Dependencias: Microsoft.Data.SqlClient y la cadena CnnSistemaTickets.
 * Flujo: BLL -> BaseDatos -> Stored Procedure -> filas (LecturaSql) o resultado.
 * Consideraciones: Cada BLL declara sus parámetros con tipo y largo exactos (un varchar no viaja como nvarchar) y lee cada
 *   columna por su nombre: una columna mal escrita falla de inmediato en vez de quedar vacía sin aviso.
 */

namespace SistemaTicketsInteligente.Api.Comun;

public sealed class BaseDatos
{
    private readonly string cadenaConexion;

    public BaseDatos(string cadenaConexion)
    {
        if (string.IsNullOrWhiteSpace(cadenaConexion)) throw new ArgumentException("La cadena de conexión no puede estar vacía.", nameof(cadenaConexion));
        this.cadenaConexion = PrepararCadena(cadenaConexion);
    }

    public SqlConnection CrearConexion() => new(cadenaConexion);

    /// <summary>Ejecuta un procedimiento que no devuelve filas.</summary>
    public Task EjecutarAsync(string procedimiento, Action<SqlParameterCollection> parametros, CancellationToken ct) =>
        UsarAsync(procedimiento, parametros, comando => comando.ExecuteNonQueryAsync(ct), ct);

    /// <summary>Ejecuta un procedimiento y devuelve el primer valor de la primera fila (por ejemplo, el código creado).</summary>
    public Task<object?> EscalarAsync(string procedimiento, Action<SqlParameterCollection> parametros, CancellationToken ct) =>
        UsarAsync(procedimiento, parametros, comando => comando.ExecuteScalarAsync(ct), ct);

    /// <summary>Ejecuta un procedimiento y entrega el lector para leer, en orden, cada conjunto de resultados.</summary>
    public Task<T> LeerAsync<T>(string procedimiento, Action<SqlParameterCollection> parametros, Func<SqlDataReader, Task<T>> leer,
        CancellationToken ct, int segundosMaximos = 30) =>
        UsarAsync(procedimiento, parametros, async comando =>
        {
            await using var lector = await comando.ExecuteReaderAsync(ct);
            return await leer(lector);
        }, ct, segundosMaximos);

    /// <summary>
    /// Varios procedimientos en una sola transacción (por ejemplo, el ticket y sus adjuntos): si uno falla se revierte todo.
    /// Dentro del trabajo, cada comando se crea con <see cref="Comando"/> sobre la conexión y la transacción recibidas.
    /// </summary>
    public async Task<T> TransaccionAsync<T>(Func<SqlConnection, SqlTransaction, Task<T>> trabajo, CancellationToken ct)
    {
        await using var conexion = CrearConexion();
        await conexion.OpenAsync(ct);
        await using var transaccion = (SqlTransaction)await conexion.BeginTransactionAsync(ct);
        try
        {
            var resultado = await trabajo(conexion, transaccion);
            await transaccion.CommitAsync(ct);
            return resultado;
        }
        catch (Exception ex)
        {
            // El procedimiento pudo haber revertido ya la transacción; se conserva la excepción que originó el fallo.
            try { if (transaccion.Connection is not null) await transaccion.RollbackAsync(CancellationToken.None); } catch (Exception) { }
            if (ex is SqlException sql && EsErrorDeNegocio(sql)) throw new InvalidOperationException(sql.Message, sql);
            throw;
        }
    }

    public static SqlCommand Comando(SqlConnection conexion, SqlTransaction? transaccion, string procedimiento, Action<SqlParameterCollection> parametros)
    {
        var comando = new SqlCommand(procedimiento, conexion, transaccion) { CommandType = CommandType.StoredProcedure };
        parametros(comando.Parameters);
        return comando;
    }

    /// <summary>Los procedimientos lanzan sus validaciones de negocio con Throw 50000-50999.</summary>
    public static bool EsErrorDeNegocio(SqlException ex) => ex.Number is >= 50000 and <= 50999;

    /// <summary>Texto opcional para un parámetro: vacío o solo espacios viaja como NULL.</summary>
    public static object Opcional(string? valor) => string.IsNullOrWhiteSpace(valor) ? DBNull.Value : valor;

    public static object Opcional<T>(T? valor) where T : struct => valor.HasValue ? valor.Value : DBNull.Value;

    public async Task VerificarAsync(CancellationToken ct = default)
    {
        await using var conexion = CrearConexion();
        await conexion.OpenAsync(ct);
        await using var comando = new SqlCommand("Select 1", conexion) { CommandTimeout = 10 };
        await comando.ExecuteScalarAsync(ct);
    }

    public static string PrepararCadena(string cadenaConexion)
    {
        var configuracion = new SqlConnectionStringBuilder(cadenaConexion);
        configuracion.ConnectTimeout = Math.Max(configuracion.ConnectTimeout, 30);
        configuracion.ConnectRetryCount = 3;
        configuracion.ConnectRetryInterval = 2;
        return configuracion.ConnectionString;
    }

    private async Task<T> UsarAsync<T>(string procedimiento, Action<SqlParameterCollection> parametros, Func<SqlCommand, Task<T>> ejecutar,
        CancellationToken ct, int segundosMaximos = 30)
    {
        await using var conexion = CrearConexion();
        await using var comando = Comando(conexion, null, procedimiento, parametros);
        comando.CommandTimeout = segundosMaximos;
        await conexion.OpenAsync(ct);
        try { return await ejecutar(comando); }
        catch (SqlException ex) when (EsErrorDeNegocio(ex)) { throw new InvalidOperationException(ex.Message, ex); }
    }
}
