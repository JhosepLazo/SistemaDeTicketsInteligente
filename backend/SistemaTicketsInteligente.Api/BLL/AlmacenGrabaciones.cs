/**
 * Archivo: AlmacenGrabaciones.cs
 * Objetivo: Guardar y localizar las grabaciones de pantalla de las reproducciones.
 * Responsabilidad: Validar que el archivo sea realmente un video WebM/MP4 (firma binaria, no solo extensión), guardarlo bajo
 *   uploads/incidencias/agente/{sesión} y resolver rutas sin permitir salir de esa carpeta.
 * Dependencias: IFormFile y el sistema de archivos local.
 * Flujo: Controller -> BLL -> GuardarAsync -> SP Usp_TI_Agente_RegistrarGrabacion (adjunto + evidencia).
 * Consideraciones: Las grabaciones viven dentro de uploads/incidencias para reutilizar la descarga segura de adjuntos del ticket.
 */

namespace SistemaTicketsInteligente.Api.BLL;

public static class AlmacenGrabaciones
{
    public const long MaximoBytes = 45 * 1024 * 1024;

    public sealed record Guardada(string NombreOriginal, string NombreArchivo, string RutaRelativa, string TipoMime, long TamanoBytes, string RutaFisica);

    public static async Task<Guardada> GuardarAsync(IFormFile archivo, long sesionNumero, CancellationToken ct)
    {
        if (archivo.Length <= 0) throw new ArgumentException("La grabación está vacía.");
        if (archivo.Length > MaximoBytes) throw new ArgumentException("La grabación no puede superar los 45 MB.");

        var cabecera = new byte[12];
        await using (var lectura = archivo.OpenReadStream())
        {
            var leidos = 0;
            while (leidos < cabecera.Length)
            {
                var n = await lectura.ReadAsync(cabecera.AsMemory(leidos), ct);
                if (n == 0) break;
                leidos += n;
            }
        }
        // WebM/Matroska empieza con 1A 45 DF A3; MP4 tiene "ftyp" en el byte 4.
        var (tipoMime, extension) = cabecera[0] == 0x1A && cabecera[1] == 0x45 && cabecera[2] == 0xDF && cabecera[3] == 0xA3 ? ("video/webm", ".webm")
            : cabecera[4] == (byte)'f' && cabecera[5] == (byte)'t' && cabecera[6] == (byte)'y' && cabecera[7] == (byte)'p' ? ("video/mp4", ".mp4")
            : throw new ArgumentException("El archivo no es una grabación de video válida (WebM o MP4).");

        var carpetaRelativa = $"uploads/incidencias/agente/{sesionNumero}";
        var carpeta = Path.Combine(Directory.GetCurrentDirectory(), carpetaRelativa.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(carpeta);
        var nombreArchivo = $"{Guid.NewGuid():N}{extension}";
        var rutaFisica = Path.Combine(carpeta, nombreArchivo);
        await using (var destino = File.Create(rutaFisica)) await archivo.CopyToAsync(destino, ct);

        var nombreOriginal = $"grabacion-pantalla-{DateTime.Now:yyyyMMdd-HHmmss}{extension}";
        return new Guardada(nombreOriginal, nombreArchivo, $"{carpetaRelativa}/{nombreArchivo}", tipoMime, archivo.Length, rutaFisica);
    }

    /// <summary>Ruta física de una grabación registrada, solo si está dentro de uploads/incidencias y existe.</summary>
    public static string Resolver(string rutaRelativa)
    {
        var raiz = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "uploads", "incidencias")) + Path.DirectorySeparatorChar;
        var ruta = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), rutaRelativa.Replace('/', Path.DirectorySeparatorChar)));
        if (!ruta.StartsWith(raiz, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("La ruta de la grabación no es válida.");
        if (!File.Exists(ruta)) throw new KeyNotFoundException("La grabación ya no se encuentra disponible en el servidor.");
        return ruta;
    }

    public static void Eliminar(string rutaFisica)
    {
        try { if (File.Exists(rutaFisica)) File.Delete(rutaFisica); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* la limpieza no debe ocultar el error original */ }
    }
}
