/**
 * Archivo: AlmacenGrabaciones.cs
 * Objetivo: Guardar las grabaciones de pantalla de las investigaciones y entregarlas de forma segura.
 * Responsabilidad: Validar que el archivo sea realmente un video WebM o MP4, guardarlo bajo uploads/incidencias/agente/{sesión},
 *   registrarlo como evidencia (y adjunto del ticket) y resolver su ruta sin permitir salir de esa carpeta.
 * Dependencias: BaseDatos (Usp_TI_Agente_RegistrarGrabacion) y Archivos.
 * Flujo: AsistenteTIBLL (observación TI) / ReproduccionUsuarioBLL (colaborador) -> RegistrarAsync -> evidencia GRABACION_PANTALLA.
 * Consideraciones: Las grabaciones viven dentro de uploads/incidencias para reutilizar la descarga segura de adjuntos del ticket.
 *   Si el registro en la base falla, el archivo guardado se borra.
 */

namespace SistemaTicketsInteligente.Api.BLL.Agente;

public sealed class AlmacenGrabaciones(BaseDatos baseDatos)
{
    public const long MaximoBytes = 45 * 1024 * 1024;

    /// <summary>Guarda la grabación y la registra en la investigación; devuelve la secuencia del evento creado.</summary>
    /// <param name="usuarioFinal">true si la grabó el colaborador invitado; false si es la observación del operador TI.</param>
    public async Task<int> RegistrarAsync(string usuario, long sesion, bool usuarioFinal, IFormFile archivo, int? duracionSegundos, CancellationToken ct)
    {
        if (archivo.Length <= 0) throw new ArgumentException("La grabación está vacía.");
        if (archivo.Length > MaximoBytes) throw new ArgumentException("La grabación no puede superar los 45 MB.");
        var (extension, tipoMime) = await Archivos.TipoVideoAsync(archivo, ct)
            ?? throw new ArgumentException("El archivo no es una grabación de video válida (WebM o MP4).");
        if (duracionSegundos is < 0 or > 7200) duracionSegundos = null;

        var guardada = await Archivos.GuardarAsync(archivo, $"{Archivos.Incidencias}/agente/{sesion}", extension, tipoMime, ct)
            with { NombreOriginal = $"grabacion-pantalla-{DateTime.Now:yyyyMMdd-HHmmss}{extension}" };
        try
        {
            return Convert.ToInt32(await baseDatos.EscalarAsync("dbo.Usp_TI_Agente_RegistrarGrabacion", p =>
            {
                p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;
                p.Add("@nSesionNumero", SqlDbType.BigInt).Value = sesion;
                p.Add("@lUsuarioFinal", SqlDbType.Bit).Value = usuarioFinal;
                Archivos.Parametros(p, guardada);
                p.Add("@nDuracionSegundos", SqlDbType.Int).Value = BaseDatos.Opcional(duracionSegundos);
            }, ct));
        }
        catch
        {
            Archivos.Eliminar(Archivos.RutaFisica(guardada.RutaArchivo));
            throw;
        }
    }

    /// <summary>Ruta física de una grabación registrada, solo si está dentro de uploads/incidencias y existe.</summary>
    public static string Resolver(string rutaRelativa)
    {
        var ruta = Archivos.RutaSegura(rutaRelativa, Archivos.Incidencias, "La ruta de la grabación no es válida.");
        return File.Exists(ruta) ? ruta : throw new KeyNotFoundException("La grabación ya no se encuentra disponible en el servidor.");
    }
}
