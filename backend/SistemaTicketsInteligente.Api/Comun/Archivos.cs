/**
 * Archivo: Archivos.cs
 * Objetivo: Guardar y entregar los archivos que suben los usuarios (adjuntos, grabaciones y formatos) de forma segura.
 * Responsabilidad: Guardar cada archivo con un nombre aleatorio, reconocer los videos por su firma binaria y resolver rutas
 *   sin permitir salir de la carpeta autorizada.
 * Dependencias: IFormFile y el sistema de archivos local (carpeta uploads/ junto a la API).
 * Flujo: BLL -> GuardarAsync -> Stored Procedure registra la ruta relativa; descarga: SP devuelve la ruta -> RutaSegura -> PhysicalFile.
 * Consideraciones: La base guarda rutas relativas; RutaSegura impide que una ruta manipulada lea archivos fuera de uploads/.
 */

namespace SistemaTicketsInteligente.Api.Comun;

/// <summary>Archivo ya guardado en disco, con los datos que se registran en la base.</summary>
public sealed record ArchivoGuardado(string NombreOriginal, string NombreArchivo, string RutaArchivo, string TipoMime, long TamanoBytes);

/// <summary>Archivo listo para entregar al navegador: la ruta física ya fue validada.</summary>
public sealed record ArchivoDescarga(string NombreOriginal, string RutaFisica, string TipoMime);

public static class Archivos
{
    public const string Incidencias = "uploads/incidencias";
    public const string Formatos = "uploads/formatos";

    /// <summary>Guarda un archivo subido dentro de <paramref name="carpetaRelativa"/> con un nombre aleatorio.</summary>
    public static async Task<ArchivoGuardado> GuardarAsync(IFormFile archivo, string carpetaRelativa, string extension, string tipoMime,
        CancellationToken ct, string prefijoNombre = "")
    {
        var carpeta = RutaFisica(carpetaRelativa);
        Directory.CreateDirectory(carpeta);
        var nombreArchivo = $"{prefijoNombre}{Guid.NewGuid():N}{extension}";
        await using (var destino = File.Create(Path.Combine(carpeta, nombreArchivo))) await archivo.CopyToAsync(destino, ct);
        return new ArchivoGuardado(Path.GetFileName(archivo.FileName), nombreArchivo, $"{carpetaRelativa}/{nombreArchivo}", tipoMime, archivo.Length);
    }

    /// <summary>Parámetros con los que los procedimientos registran un archivo guardado (adjunto, adjunto de mensaje o grabación).</summary>
    public static void Parametros(SqlParameterCollection p, ArchivoGuardado archivo)
    {
        p.Add("@cNombreOriginal", SqlDbType.NVarChar, 260).Value = archivo.NombreOriginal;
        p.Add("@cNombreArchivo", SqlDbType.NVarChar, 260).Value = archivo.NombreArchivo;
        p.Add("@cRutaArchivo", SqlDbType.NVarChar, 1000).Value = archivo.RutaArchivo;
        p.Add("@cTipoMime", SqlDbType.VarChar, 100).Value = archivo.TipoMime;
        p.Add("@nTamanoBytes", SqlDbType.BigInt).Value = archivo.TamanoBytes;
    }

    /// <summary>
    /// Arma la descarga desde la fila que devuelve el procedimiento (NombreOriginal, RutaArchivo, TipoMime),
    /// validando que la ruta esté dentro de la carpeta permitida y que el archivo todavía exista.
    /// </summary>
    public static ArchivoDescarga Descarga(SqlDataReader fila, string carpetaPermitida, string mensajeRutaInvalida, string mensajeNoExiste)
    {
        var ruta = RutaSegura(fila.Texto("RutaArchivo"), carpetaPermitida, mensajeRutaInvalida);
        if (!File.Exists(ruta)) throw new FileNotFoundException(mensajeNoExiste);
        return new ArchivoDescarga(fila.Texto("NombreOriginal"), ruta, fila.Texto("TipoMime"));
    }

    /// <summary>Ruta física de un archivo registrado, solo si queda dentro de <paramref name="carpetaPermitida"/>.</summary>
    public static string RutaSegura(string rutaRelativa, string carpetaPermitida, string mensajeRutaInvalida)
    {
        var raiz = RutaFisica(carpetaPermitida) + Path.DirectorySeparatorChar;
        var ruta = RutaFisica(rutaRelativa);
        if (!ruta.StartsWith(raiz, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException(mensajeRutaInvalida);
        return ruta;
    }

    public static string RutaFisica(string rutaRelativa) =>
        Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), rutaRelativa.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>Reconoce un video por su contenido (WebM empieza con 1A 45 DF A3; MP4 trae "ftyp" desde el byte 4), no por su nombre.</summary>
    public static async Task<(string Extension, string TipoMime)?> TipoVideoAsync(IFormFile archivo, CancellationToken ct)
    {
        var cabecera = new byte[12];
        await using var lectura = archivo.OpenReadStream();
        var leidos = await lectura.ReadAtLeastAsync(cabecera, cabecera.Length, throwOnEndOfStream: false, ct);
        if (leidos >= 4 && cabecera[0] == 0x1A && cabecera[1] == 0x45 && cabecera[2] == 0xDF && cabecera[3] == 0xA3) return (".webm", "video/webm");
        if (leidos >= 8 && cabecera[4] == (byte)'f' && cabecera[5] == (byte)'t' && cabecera[6] == (byte)'y' && cabecera[7] == (byte)'p') return (".mp4", "video/mp4");
        return null;
    }

    /// <summary>Borra el archivo o la carpeta de una operación que falló; la limpieza nunca oculta el error original.</summary>
    public static void Eliminar(string ruta)
    {
        try
        {
            if (Directory.Exists(ruta)) Directory.Delete(ruta, true);
            else if (File.Exists(ruta)) File.Delete(ruta);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
