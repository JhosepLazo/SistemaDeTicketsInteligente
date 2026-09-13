/**
 * Archivo: MisTicketsUsuarioBLL.cs
 * Objetivo: Aplicar las reglas funcionales del módulo Mis Tickets para el usuario autenticado.
 * Responsabilidad: Validar identidad y acciones, controlar adjuntos, coordinar persistencia de respuestas, confirmar/reabrir soluciones, registrar calificaciones y entregar archivos autorizados.
 * Dependencias: MisTicketsUsuarioDAO, MisTicketsUsuarioDTO y sistema de archivos local del backend.
 * Flujo: MisTicketsUsuarioController -> MisTicketsUsuarioBLL -> MisTicketsUsuarioDAO -> SQL Server.
 * Consideraciones: El usuario solo puede operar sobre sus propios tickets; una observación permite máximo 5 adjuntos de 10 MB y la reapertura exige explicar por qué la solución no funcionó.
 */

using SistemaTicketsInteligente.Api.DAO;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.BLL;

public sealed class MisTicketsUsuarioBLL
{
    private const int MaximoAdjuntos = 5;
    private const long MaximoBytesPorAdjunto = 10 * 1024 * 1024;
    private static readonly HashSet<string> ExtensionesPermitidas = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".pdf", ".xls", ".xlsx"
    };

    private static readonly HashSet<string> TiposMimePermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/webp", "application/pdf", "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/octet-stream"
    };

    private readonly MisTicketsUsuarioDAO misTicketsUsuarioDAO;

    public MisTicketsUsuarioBLL(MisTicketsUsuarioDAO misTicketsUsuarioDAO)
    {
        this.misTicketsUsuarioDAO = misTicketsUsuarioDAO;
    }

    public Task<MisTicketsUsuarioRespuesta> ObtenerAsync(string usuario, CancellationToken cancellationToken = default) =>
        misTicketsUsuarioDAO.ObtenerAsync(ValidarUsuario(usuario), cancellationToken);

    public async Task<MisTicketsUsuarioDetalle> ObtenerDetalleAsync(string usuario, string incidenciaNumero, CancellationToken cancellationToken = default)
    {
        var detalle = await misTicketsUsuarioDAO.ObtenerDetalleAsync(ValidarUsuario(usuario), ValidarIncidencia(incidenciaNumero), cancellationToken);
        return detalle ?? throw new KeyNotFoundException("El ticket no existe o no pertenece al usuario autenticado.");
    }

    public async Task ResponderObservacionAsync(
        string usuario,
        string incidenciaNumero,
        ResponderObservacionSolicitud solicitud,
        CancellationToken cancellationToken = default)
    {
        var usuarioNormalizado = ValidarUsuario(usuario);
        var incidenciaNormalizada = ValidarIncidencia(incidenciaNumero);
        var contenido = solicitud.Contenido.Trim();
        if (contenido.Length < 3 || contenido.Length > 2000) throw new ArgumentException("La respuesta debe contener entre 3 y 2000 caracteres.");
        ValidarAdjuntos(solicitud.Adjuntos);

        var idCorrelacion = Guid.NewGuid();
        var carpetaAdjuntos = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "incidencias", idCorrelacion.ToString("N"));
        var adjuntos = new List<MisTicketsUsuarioAdjuntoRegistro>();

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

                    adjuntos.Add(new MisTicketsUsuarioAdjuntoRegistro
                    {
                        NombreOriginal = Path.GetFileName(archivo.FileName),
                        NombreArchivo = nombreArchivo,
                        RutaArchivo = $"uploads/incidencias/{idCorrelacion:N}/{nombreArchivo}",
                        TipoMime = string.IsNullOrWhiteSpace(archivo.ContentType) ? "application/octet-stream" : archivo.ContentType,
                        TamanoBytes = archivo.Length
                    });
                }
            }

            await misTicketsUsuarioDAO.ResponderObservacionAsync(usuarioNormalizado, incidenciaNormalizada, contenido, adjuntos, idCorrelacion, cancellationToken);
        }
        catch
        {
            if (Directory.Exists(carpetaAdjuntos)) Directory.Delete(carpetaAdjuntos, true);
            throw;
        }
    }

    public Task ValidarSolucionAsync(
        string usuario,
        string incidenciaNumero,
        ValidarSolucionSolicitud solicitud,
        CancellationToken cancellationToken = default)
    {
        var comentario = solicitud.Comentario?.Trim();
        if ((comentario?.Length ?? 0) > 1000) throw new ArgumentException("El comentario no puede superar los 1000 caracteres.");
        if (!solicitud.Solucionada && string.IsNullOrWhiteSpace(comentario)) throw new ArgumentException("Indica qué problema continúa para poder reabrir el ticket.");

        return misTicketsUsuarioDAO.ValidarSolucionAsync(
            ValidarUsuario(usuario),
            ValidarIncidencia(incidenciaNumero),
            solicitud.Solucionada,
            comentario,
            Guid.NewGuid(),
            cancellationToken);
    }

    public Task CalificarAsync(
        string usuario,
        string incidenciaNumero,
        CalificarTicketSolicitud solicitud,
        CancellationToken cancellationToken = default)
    {
        if (solicitud.Calificacion is < 1 or > 5) throw new ArgumentException("La calificación debe estar entre 1 y 5 estrellas.");
        var comentario = solicitud.Comentario?.Trim();
        if ((comentario?.Length ?? 0) > 500) throw new ArgumentException("El comentario no puede superar los 500 caracteres.");

        return misTicketsUsuarioDAO.CalificarAsync(
            ValidarUsuario(usuario),
            ValidarIncidencia(incidenciaNumero),
            solicitud.Calificacion,
            comentario,
            Guid.NewGuid(),
            cancellationToken);
    }

    public async Task<MisTicketsUsuarioArchivo> ObtenerArchivoAsync(string usuario, string incidenciaNumero, int secuencia, CancellationToken cancellationToken = default)
    {
        if (secuencia <= 0) throw new ArgumentException("El adjunto indicado no es válido.");

        var archivo = await misTicketsUsuarioDAO.ObtenerArchivoAsync(ValidarUsuario(usuario), ValidarIncidencia(incidenciaNumero), secuencia, cancellationToken)
            ?? throw new KeyNotFoundException("El archivo no existe o no pertenece al ticket indicado.");

        var raizAdjuntos = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "uploads", "incidencias")) + Path.DirectorySeparatorChar;
        var rutaFisica = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), archivo.RutaArchivo.Replace('/', Path.DirectorySeparatorChar)));

        if (!rutaFisica.StartsWith(raizAdjuntos, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("La ruta del adjunto no es válida.");
        if (!File.Exists(rutaFisica)) throw new FileNotFoundException("El archivo asociado al ticket ya no se encuentra disponible.");

        archivo.RutaArchivo = rutaFisica;
        return archivo;
    }

    private static string ValidarUsuario(string usuario)
    {
        var valor = usuario.Trim();
        if (valor.Length == 0 || valor.Length > 20) throw new ArgumentException("El usuario autenticado no es válido.", nameof(usuario));
        return valor;
    }

    private static string ValidarIncidencia(string incidenciaNumero)
    {
        var valor = incidenciaNumero.Trim().ToUpperInvariant();
        if (valor.Length == 0 || valor.Length > 12) throw new ArgumentException("El número de ticket no es válido.", nameof(incidenciaNumero));
        return valor;
    }

    private static void ValidarAdjuntos(IReadOnlyCollection<IFormFile> adjuntos)
    {
        if (adjuntos.Count > MaximoAdjuntos) throw new ArgumentException($"Puedes adjuntar como máximo {MaximoAdjuntos} archivos.");

        foreach (var archivo in adjuntos)
        {
            var nombre = Path.GetFileName(archivo.FileName);
            var extension = Path.GetExtension(nombre);
            var tipoMime = string.IsNullOrWhiteSpace(archivo.ContentType) ? "application/octet-stream" : archivo.ContentType;

            if (archivo.Length <= 0) throw new ArgumentException($"El archivo '{nombre}' está vacío.");
            if (archivo.Length > MaximoBytesPorAdjunto) throw new ArgumentException($"El archivo '{nombre}' supera el límite de 10 MB.");
            if (!ExtensionesPermitidas.Contains(extension) || !TiposMimePermitidos.Contains(tipoMime)) throw new ArgumentException($"El archivo '{nombre}' no tiene un formato permitido.");
        }
    }
}
