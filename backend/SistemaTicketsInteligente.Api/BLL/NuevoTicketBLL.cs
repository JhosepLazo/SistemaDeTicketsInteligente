/**
 * Archivo: NuevoTicketBLL.cs
 * Objetivo: Aplicar las reglas mínimas necesarias para cargar y registrar tickets creados por el usuario.
 * Responsabilidad: Validar campos, controlar evidencias permitidas, guardar físicamente los archivos y coordinar el registro transaccional mediante DAO.
 * Dependencias: NuevoTicketDAO, NuevoTicketDTO, ASP.NET Core y sistema de archivos local del backend.
 * Flujo: NuevoTicketController -> NuevoTicketBLL -> NuevoTicketDAO -> SQL Server.
 * Consideraciones: Máximo 5 adjuntos de 10 MB cada uno; solo se permiten imágenes, PDF y Excel. Si la persistencia falla, elimina los archivos guardados para evitar evidencias huérfanas.
 */

using Microsoft.AspNetCore.Http;
using SistemaTicketsInteligente.Api.DAO;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.BLL;

public sealed class NuevoTicketBLL
{
    private const int MaximoAdjuntos = 5;
    private const long MaximoBytesPorAdjunto = 10 * 1024 * 1024;
    private static readonly HashSet<string> ExtensionesPermitidas = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".pdf", ".xls", ".xlsx"
    };
    private static readonly HashSet<string> TiposMimePermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/webp", "application/pdf", "application/vnd.ms-excel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    };
    // La grabación de pantalla que el colaborador hizo con el Asistente TI viaja como adjunto de video.
    private const int MaximoVideos = 2;
    private const long MaximoBytesVideo = 40 * 1024 * 1024;

    private readonly NuevoTicketDAO nuevoTicketDAO;
    private readonly ColaAgenteTI cola;

    public NuevoTicketBLL(NuevoTicketDAO nuevoTicketDAO, ColaAgenteTI cola)
    {
        this.nuevoTicketDAO = nuevoTicketDAO;
        this.cola = cola;
    }

    public async Task<NuevoTicketDatosRespuesta> ObtenerDatosAsync(string usuario, CancellationToken cancellationToken = default)
    {
        var usuarioNormalizado = ValidarUsuario(usuario);
        return await nuevoTicketDAO.ObtenerDatosAsync(usuarioNormalizado, cancellationToken)
            ?? throw new InvalidOperationException("No fue posible obtener los datos del usuario autenticado.");
    }

    public async Task<NuevoTicketCreadoRespuesta> CrearAsync(
        string usuario,
        CrearNuevoTicketSolicitud solicitud,
        CancellationToken cancellationToken = default)
    {
        var usuarioNormalizado = ValidarUsuario(usuario);
        NormalizarYValidarSolicitud(solicitud);
        ValidarAdjuntos(solicitud.Adjuntos, solicitud.Tipo);

        var idCorrelacion = Guid.NewGuid();
        var carpetaAdjuntos = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "incidencias", idCorrelacion.ToString("N"));
        var adjuntos = new List<NuevoTicketAdjuntoRegistro>();

        try
        {
            if (solicitud.Adjuntos.Count > 0)
            {
                Directory.CreateDirectory(carpetaAdjuntos);

                foreach (var archivo in solicitud.Adjuntos)
                {
                    var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
                    if (EsVideo(archivo)) extension = await ExtensionVideoAsync(archivo, cancellationToken);
                    var nombreArchivo = $"{Guid.NewGuid():N}{extension}";
                    var rutaFisica = Path.Combine(carpetaAdjuntos, nombreArchivo);

                    await using var destino = File.Create(rutaFisica);
                    await archivo.CopyToAsync(destino, cancellationToken);

                    adjuntos.Add(new NuevoTicketAdjuntoRegistro
                    {
                        NombreOriginal = Path.GetFileName(archivo.FileName),
                        NombreArchivo = nombreArchivo,
                        RutaArchivo = $"uploads/incidencias/{idCorrelacion:N}/{nombreArchivo}",
                        TipoMime = EsVideo(archivo) ? (extension == ".mp4" ? "video/mp4" : "video/webm") : archivo.ContentType,
                        TamanoBytes = archivo.Length
                    });
                }
            }

            var creado = await nuevoTicketDAO.CrearAsync(usuarioNormalizado, solicitud, adjuntos, idCorrelacion, cancellationToken);
            // Si el colaborador mostró el error en pantalla, el agente investiga el ticket en segundo plano y avisa a TI.
            var evidencia = EvidenciaValida(solicitud.EvidenciaAsistenteJson);
            if (evidencia is not null) cola.Encolar(new TrabajoAgenteTI(null, creado.IncidenciaNumero, evidencia, $"ticket-{creado.IncidenciaNumero}"));
            return creado;
        }
        catch
        {
            try
            {
                if (Directory.Exists(carpetaAdjuntos)) Directory.Delete(carpetaAdjuntos, true);
            }
            catch
            {
                // La limpieza no debe ocultar el error que impidió registrar el ticket.
            }
            throw;
        }
    }

    private static bool EsVideo(IFormFile archivo) =>
        archivo.ContentType.StartsWith("video/webm", StringComparison.OrdinalIgnoreCase) || archivo.ContentType.StartsWith("video/mp4", StringComparison.OrdinalIgnoreCase);

    // La extensión se decide por la firma binaria del archivo, no por lo que declara el navegador.
    private static async Task<string> ExtensionVideoAsync(IFormFile archivo, CancellationToken ct)
    {
        var cabecera = new byte[12];
        await using var lectura = archivo.OpenReadStream();
        var leidos = await lectura.ReadAtLeastAsync(cabecera, cabecera.Length, throwOnEndOfStream: false, ct);
        if (leidos >= 4 && cabecera[0] == 0x1A && cabecera[1] == 0x45 && cabecera[2] == 0xDF && cabecera[3] == 0xA3) return ".webm";
        if (leidos >= 8 && cabecera[4] == (byte)'f' && cabecera[5] == (byte)'t' && cabecera[6] == (byte)'y' && cabecera[7] == (byte)'p') return ".mp4";
        throw new ArgumentException($"El archivo '{Path.GetFileName(archivo.FileName)}' no es una grabación de video válida.");
    }

    // Los secretos se ocultan en cada texto (no sobre el JSON completo, para no romper su estructura).
    private static string? EvidenciaValida(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 60000) return null;
        try
        {
            if (System.Text.Json.Nodes.JsonNode.Parse(json) is not System.Text.Json.Nodes.JsonObject evidencia) return null;
            return RedactarTextos(evidencia)!.ToJsonString();
        }
        catch (System.Text.Json.JsonException) { return null; }
    }

    private static System.Text.Json.Nodes.JsonNode? RedactarTextos(System.Text.Json.Nodes.JsonNode? nodo)
    {
        switch (nodo)
        {
            case System.Text.Json.Nodes.JsonObject objeto:
                foreach (var clave in objeto.Select(x => x.Key).ToList()) objeto[clave] = RedactarTextos(objeto[clave]?.DeepClone());
                return objeto;
            case System.Text.Json.Nodes.JsonArray lista:
                for (var i = 0; i < lista.Count; i++) lista[i] = RedactarTextos(lista[i]?.DeepClone());
                return lista;
            case System.Text.Json.Nodes.JsonValue valor when valor.TryGetValue<string>(out var texto):
                return System.Text.Json.Nodes.JsonValue.Create(RedactorDatosSensibles.RedactarSecretos(texto));
            default:
                return nodo;
        }
    }

    private static string ValidarUsuario(string usuario)
    {
        var usuarioNormalizado = usuario.Trim();
        if (usuarioNormalizado.Length == 0 || usuarioNormalizado.Length > 20) throw new ArgumentException("El usuario autenticado no es válido.", nameof(usuario));
        return usuarioNormalizado;
    }

    private static void NormalizarYValidarSolicitud(CrearNuevoTicketSolicitud solicitud)
    {
        solicitud.Linea = solicitud.Linea.Trim();
        solicitud.Tipo = solicitud.Tipo.Trim();
        solicitud.Titulo = solicitud.Titulo.Trim();
        solicitud.Detalle = solicitud.Detalle.Trim();
        solicitud.MensajeError = solicitud.MensajeError?.Trim();

        if (solicitud.Linea.Length != 3) throw new ArgumentException("Selecciona el sistema o módulo afectado.");
        if (solicitud.Tipo.Length != 3) throw new ArgumentException("Selecciona el tipo de ticket.");
        if (solicitud.Titulo.Length < 5 || solicitud.Titulo.Length > 250) throw new ArgumentException("El título debe contener entre 5 y 250 caracteres.");
        if (solicitud.Detalle.Length < 20 || solicitud.Detalle.Length > 1000) throw new ArgumentException("La descripción debe contener entre 20 y 1000 caracteres.");
        if ((solicitud.MensajeError?.Length ?? 0) > 1000) throw new ArgumentException("El mensaje de error no puede superar los 1000 caracteres.");
    }

    private static void ValidarAdjuntos(IReadOnlyCollection<IFormFile> adjuntos, string tipo)
    {
        if (adjuntos.Count > MaximoAdjuntos) throw new ArgumentException($"Puedes adjuntar como máximo {MaximoAdjuntos} archivos.");
        if (string.Equals(tipo, "REQ", StringComparison.OrdinalIgnoreCase) && adjuntos.Count == 0) throw new ArgumentException("Los requerimientos deben incluir al menos un archivo de sustento.");
        if (adjuntos.Count(EsVideo) > MaximoVideos) throw new ArgumentException($"Puedes adjuntar como máximo {MaximoVideos} grabaciones de pantalla.");
        if (adjuntos.Sum(x => x.Length) > 90L * 1024 * 1024) throw new ArgumentException("El total de archivos adjuntos no puede superar los 90 MB.");

        foreach (var archivo in adjuntos)
        {
            var nombre = Path.GetFileName(archivo.FileName);
            var extension = Path.GetExtension(nombre);
            if (archivo.Length <= 0) throw new ArgumentException($"El archivo '{nombre}' está vacío.");
            if (EsVideo(archivo))
            {
                if (archivo.Length > MaximoBytesVideo) throw new ArgumentException($"La grabación '{nombre}' supera el límite de 40 MB.");
                continue;
            }
            if (archivo.Length > MaximoBytesPorAdjunto) throw new ArgumentException($"El archivo '{nombre}' supera el límite de 10 MB.");
            if (!ExtensionesPermitidas.Contains(extension) || !TiposMimePermitidos.Contains(archivo.ContentType)) throw new ArgumentException($"El archivo '{nombre}' no tiene un formato permitido.");
        }
    }
}
