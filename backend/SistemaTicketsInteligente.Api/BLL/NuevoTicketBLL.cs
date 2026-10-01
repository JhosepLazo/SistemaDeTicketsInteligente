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

    private readonly NuevoTicketDAO nuevoTicketDAO;

    public NuevoTicketBLL(NuevoTicketDAO nuevoTicketDAO)
    {
        this.nuevoTicketDAO = nuevoTicketDAO;
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
                    var nombreArchivo = $"{Guid.NewGuid():N}{extension}";
                    var rutaFisica = Path.Combine(carpetaAdjuntos, nombreArchivo);

                    await using var destino = File.Create(rutaFisica);
                    await archivo.CopyToAsync(destino, cancellationToken);

                    adjuntos.Add(new NuevoTicketAdjuntoRegistro
                    {
                        NombreOriginal = Path.GetFileName(archivo.FileName),
                        NombreArchivo = nombreArchivo,
                        RutaArchivo = $"uploads/incidencias/{idCorrelacion:N}/{nombreArchivo}",
                        TipoMime = archivo.ContentType,
                        TamanoBytes = archivo.Length
                    });
                }
            }

            return await nuevoTicketDAO.CrearAsync(usuarioNormalizado, solicitud, adjuntos, idCorrelacion, cancellationToken);
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

        foreach (var archivo in adjuntos)
        {
            var nombre = Path.GetFileName(archivo.FileName);
            var extension = Path.GetExtension(nombre);
            if (archivo.Length <= 0) throw new ArgumentException($"El archivo '{nombre}' está vacío.");
            if (archivo.Length > MaximoBytesPorAdjunto) throw new ArgumentException($"El archivo '{nombre}' supera el límite de 10 MB.");
            if (!ExtensionesPermitidas.Contains(extension) || !TiposMimePermitidos.Contains(archivo.ContentType)) throw new ArgumentException($"El archivo '{nombre}' no tiene un formato permitido.");
        }
    }
}
