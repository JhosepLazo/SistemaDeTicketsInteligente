/**
 * Archivo: LecturaSql.cs
 * Objetivo: Leer columnas y conjuntos de resultados de un procedimiento con los mismos valores por defecto en todo el sistema.
 * Responsabilidad: Convertir cada columna al tipo de C# (texto recortado, números, fechas, nulos) y recorrer los conjuntos en orden.
 * Dependencias: SqlDataReader.
 * Flujo: BaseDatos.LeerAsync -> lector.FilaAsync / lector.ListaAsync -> DTO de respuesta.
 * Consideraciones: Un NULL se lee como texto vacío, 0 o el valor nulo del tipo; los textos se recortan porque muchas columnas son char.
 */

namespace SistemaTicketsInteligente.Api.Comun;

public static class LecturaSql
{
    public static string Texto(this SqlDataReader lector, string columna) =>
        lector[columna] is DBNull ? string.Empty : lector[columna].ToString()?.Trim() ?? string.Empty;

    public static int Entero(this SqlDataReader lector, string columna) => lector[columna] is DBNull ? 0 : Convert.ToInt32(lector[columna]);
    public static int? EnteroNulo(this SqlDataReader lector, string columna) => lector[columna] is DBNull ? null : Convert.ToInt32(lector[columna]);
    public static long Largo(this SqlDataReader lector, string columna) => lector[columna] is DBNull ? 0 : Convert.ToInt64(lector[columna]);
    public static byte? ByteNulo(this SqlDataReader lector, string columna) => lector[columna] is DBNull ? null : Convert.ToByte(lector[columna]);
    public static decimal? DecimalNulo(this SqlDataReader lector, string columna) => lector[columna] is DBNull ? null : Convert.ToDecimal(lector[columna]);
    public static bool Booleano(this SqlDataReader lector, string columna) => lector[columna] is not DBNull && Convert.ToBoolean(lector[columna]);
    public static DateTime Fecha(this SqlDataReader lector, string columna) => lector[columna] is DBNull ? DateTime.MinValue : Convert.ToDateTime(lector[columna]);
    public static DateTime? FechaNula(this SqlDataReader lector, string columna) => lector[columna] is DBNull ? null : Convert.ToDateTime(lector[columna]);
    public static Guid Identificador(this SqlDataReader lector, string columna) => lector[columna] is DBNull ? Guid.Empty : (Guid)lector[columna];

    /// <summary>Indica si el conjunto actual trae la columna; permite convivir con una versión anterior del procedimiento.</summary>
    public static bool TieneColumna(this SqlDataReader lector, string columna)
    {
        for (var i = 0; i < lector.FieldCount; i++)
            if (string.Equals(lector.GetName(i), columna, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>Lee todas las filas del conjunto actual y pasa al siguiente conjunto de resultados.</summary>
    public static async Task<List<T>> ListaAsync<T>(this SqlDataReader lector, Func<SqlDataReader, T> fila, CancellationToken ct)
    {
        var lista = new List<T>();
        while (await lector.ReadAsync(ct)) lista.Add(fila(lector));
        await lector.NextResultAsync(ct);
        return lista;
    }

    /// <summary>Lee la primera fila del conjunto actual (null si no hay) y pasa al siguiente conjunto de resultados.</summary>
    public static async Task<T?> FilaAsync<T>(this SqlDataReader lector, Func<SqlDataReader, T> fila, CancellationToken ct) where T : class
    {
        var resultado = await lector.ReadAsync(ct) ? fila(lector) : null;
        await lector.NextResultAsync(ct);
        return resultado;
    }
}
