/*
 * Archivo: GestionTicketsTIBLL.cs
 * Objetivo: Aplicar las reglas funcionales mínimas del módulo Gestión de Tickets para operadores TI.
 * Responsabilidad: Validar identidad, datos de clasificación y acciones del ciclo de atención antes de delegar la persistencia al DAO.
 * Dependencias: GestionTicketsTIDAO, GestionTicketsTIDTO y sistema de archivos local para entregar evidencias autorizadas.
 * Flujo: GestionTicketsTIController -> GestionTicketsTIBLL -> GestionTicketsTIDAO -> SQL Server.
 * Consideraciones: Mantiene validaciones simples y legibles; las relaciones entre maestros, estados y permisos definitivos se vuelven a validar en SQL Server.
 */

using SistemaTicketsInteligente.Api.DAO;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.BLL;

public sealed class GestionTicketsTIBLL
{
    private readonly GestionTicketsTIDAO gestionTicketsTIDAO;

    public GestionTicketsTIBLL(GestionTicketsTIDAO gestionTicketsTIDAO)
    {
        this.gestionTicketsTIDAO = gestionTicketsTIDAO;
    }

    public Task<GestionTicketsTIRespuesta> ObtenerAsync(string usuario, string area, CancellationToken cancellationToken = default) =>
        gestionTicketsTIDAO.ObtenerAsync(ValidarUsuario(usuario), ValidarArea(area), cancellationToken);

    public async Task<GestionTicketTIDetalle> ObtenerDetalleAsync(string usuario, string area, string incidenciaNumero, CancellationToken cancellationToken = default)
    {
        var detalle = await gestionTicketsTIDAO.ObtenerDetalleAsync(ValidarUsuario(usuario), ValidarArea(area), ValidarIncidencia(incidenciaNumero), cancellationToken);
        return detalle ?? throw new KeyNotFoundException("El ticket indicado no existe.");
    }

    public Task ClasificarAsync(string usuario, string area, string incidenciaNumero, ClasificarTicketTISolicitud solicitud, CancellationToken cancellationToken = default)
    {
        solicitud.Linea = solicitud.Linea.Trim().ToUpperInvariant();
        solicitud.Item = solicitud.Item.Trim().ToUpperInvariant();
        solicitud.Tipo = solicitud.Tipo.Trim().ToUpperInvariant();
        solicitud.SubTipo = solicitud.SubTipo.Trim().ToUpperInvariant();
        solicitud.Categoria = solicitud.Categoria.Trim().ToUpperInvariant();
        solicitud.AreaCausante = NormalizarOpcional(solicitud.AreaCausante)?.ToUpperInvariant();

        if (solicitud.Linea.Length != 3) throw new ArgumentException("Selecciona una línea válida.");
        if (solicitud.Item.Length == 0 || solicitud.Item.Length > 20) throw new ArgumentException("Selecciona un item válido.");
        if (solicitud.Tipo.Length != 3) throw new ArgumentException("Selecciona un tipo válido.");
        if (solicitud.SubTipo.Length != 3) throw new ArgumentException("Selecciona un subtipo válido.");
        if (solicitud.Categoria.Length == 0 || solicitud.Categoria.Length > 20) throw new ArgumentException("Selecciona una categoría válida.");
        if (solicitud.AreaCausante is not null && solicitud.AreaCausante.Length != 3) throw new ArgumentException("Selecciona un área causante válida.");
        ValidarNivel(solicitud.Prioridad, "prioridad");
        ValidarNivel(solicitud.Impacto, "impacto");
        ValidarNivel(solicitud.Complejidad, "complejidad");

        return gestionTicketsTIDAO.ClasificarAsync(
            ValidarUsuario(usuario), ValidarArea(area), ValidarIncidencia(incidenciaNumero), solicitud, Guid.NewGuid(), cancellationToken);
    }

    public Task AsignarAsync(string usuario, string area, string incidenciaNumero, AsignarTicketTISolicitud solicitud, CancellationToken cancellationToken = default)
    {
        var responsable = ValidarUsuario(solicitud.UsuarioTI);
        return gestionTicketsTIDAO.AsignarAsync(
            ValidarUsuario(usuario), ValidarArea(area), ValidarIncidencia(incidenciaNumero), responsable, Guid.NewGuid(), cancellationToken);
    }

    public Task RegistrarAvanceAsync(string usuario, string area, string incidenciaNumero, RegistrarAvanceTISolicitud solicitud, CancellationToken cancellationToken = default)
    {
        var detalle = solicitud.Detalle.Trim();
        if (detalle.Length < 5 || detalle.Length > 4000) throw new ArgumentException("El avance debe contener entre 5 y 4000 caracteres.");

        return gestionTicketsTIDAO.RegistrarAvanceAsync(
            ValidarUsuario(usuario), ValidarArea(area), ValidarIncidencia(incidenciaNumero), detalle, solicitud.VisibleUsuario, Guid.NewGuid(), cancellationToken);
    }

    public Task SolicitarInformacionAsync(string usuario, string area, string incidenciaNumero, SolicitarInformacionTISolicitud solicitud, CancellationToken cancellationToken = default)
    {
        var mensaje = solicitud.Mensaje.Trim();
        if (mensaje.Length < 10 || mensaje.Length > 1000) throw new ArgumentException("La solicitud de información debe contener entre 10 y 1000 caracteres.");

        return gestionTicketsTIDAO.SolicitarInformacionAsync(
            ValidarUsuario(usuario), ValidarArea(area), ValidarIncidencia(incidenciaNumero), mensaje, Guid.NewGuid(), cancellationToken);
    }

    public Task ResolverAsync(string usuario, string area, string incidenciaNumero, ResolverTicketTISolicitud solicitud, CancellationToken cancellationToken = default)
    {
        solicitud.CausaRaiz = solicitud.CausaRaiz.Trim();
        solicitud.Solucion = solicitud.Solucion.Trim();
        solicitud.RespuestaUsuario = solicitud.RespuestaUsuario.Trim();
        solicitud.TipoResolucion = solicitud.TipoResolucion.Trim().ToUpperInvariant();

        if (solicitud.CausaRaiz.Length < 5 || solicitud.CausaRaiz.Length > 4000) throw new ArgumentException("La causa raíz debe contener entre 5 y 4000 caracteres.");
        if (solicitud.Solucion.Length < 5 || solicitud.Solucion.Length > 4000) throw new ArgumentException("La solución debe contener entre 5 y 4000 caracteres.");
        if (solicitud.RespuestaUsuario.Length < 5 || solicitud.RespuestaUsuario.Length > 4000) throw new ArgumentException("La respuesta al usuario debe contener entre 5 y 4000 caracteres.");
        if (solicitud.TipoResolucion.Length > 20) throw new ArgumentException("El tipo de resolución no puede superar los 20 caracteres.");

        return gestionTicketsTIDAO.ResolverAsync(
            ValidarUsuario(usuario), ValidarArea(area), ValidarIncidencia(incidenciaNumero), solicitud, Guid.NewGuid(), cancellationToken);
    }

    public Task NoProcedeAsync(string usuario, string area, string incidenciaNumero, NoProcedeTicketTISolicitud solicitud, CancellationToken cancellationToken = default)
    {
        var motivo = solicitud.Motivo.Trim();
        if (motivo.Length < 10 || motivo.Length > 1000) throw new ArgumentException("El motivo de No Procede debe contener entre 10 y 1000 caracteres.");

        return gestionTicketsTIDAO.NoProcedeAsync(
            ValidarUsuario(usuario), ValidarArea(area), ValidarIncidencia(incidenciaNumero), motivo, Guid.NewGuid(), cancellationToken);
    }

    public Task ResponderAprobacionAsync(string usuario, string area, string incidenciaNumero, int secuencia, ResponderAprobacionTISolicitud solicitud, CancellationToken cancellationToken = default)
    {
        if (secuencia <= 0) throw new ArgumentException("La solicitud de aprobación indicada no es válida.");

        var comentario = NormalizarOpcional(solicitud.Comentario);
        if ((comentario?.Length ?? 0) > 1000) throw new ArgumentException("El comentario no puede superar los 1000 caracteres.");
        if (!solicitud.Aprobar && comentario is null) throw new ArgumentException("Indica el motivo del rechazo.");

        return gestionTicketsTIDAO.ResponderAprobacionAsync(
            ValidarUsuario(usuario), ValidarArea(area), ValidarIncidencia(incidenciaNumero), secuencia, solicitud.Aprobar, comentario, Guid.NewGuid(), cancellationToken);
    }

    public async Task<GestionTicketTIArchivo> ObtenerArchivoAsync(string usuario, string area, string incidenciaNumero, int secuencia, CancellationToken cancellationToken = default)
    {
        if (secuencia <= 0) throw new ArgumentException("El adjunto indicado no es válido.");

        var archivo = await gestionTicketsTIDAO.ObtenerArchivoAsync(
            ValidarUsuario(usuario), ValidarArea(area), ValidarIncidencia(incidenciaNumero), secuencia, cancellationToken)
            ?? throw new KeyNotFoundException("El archivo indicado no existe.");

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

    private static string ValidarArea(string area)
    {
        var valor = area.Trim().ToUpperInvariant();
        if (valor.Length != 3) throw new ArgumentException("El área del operador autenticado no es válida.", nameof(area));
        return valor;
    }

    private static string ValidarIncidencia(string incidenciaNumero)
    {
        var valor = incidenciaNumero.Trim().ToUpperInvariant();
        if (valor.Length == 0 || valor.Length > 12) throw new ArgumentException("El número de ticket no es válido.", nameof(incidenciaNumero));
        return valor;
    }

    private static string? NormalizarOpcional(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static void ValidarNivel(int? valor, string nombre)
    {
        if (valor.HasValue && valor.Value is < 1 or > 5) throw new ArgumentException($"El {nombre} debe estar entre 1 y 5.");
    }
}
