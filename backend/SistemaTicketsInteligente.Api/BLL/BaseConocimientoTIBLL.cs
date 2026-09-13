/**
 * Archivo: BaseConocimientoTIBLL.cs
 * Objetivo: Aplicar las reglas funcionales mínimas del módulo Base de Conocimiento para operadores TI.
 * Responsabilidad: Validar identidad, códigos, contenido y clasificación antes de delegar la persistencia al DAO.
 * Dependencias: BaseConocimientoTIDAO y BaseConocimientoTIDTO.
 * Flujo: BaseConocimientoTIController -> BaseConocimientoTIBLL -> BaseConocimientoTIDAO -> SQL Server.
 * Consideraciones: La capa conserva reglas simples y explícitas; la integridad entre línea, item, tipo, subtipo y categoría se valida definitivamente en los Stored Procedures.
 */

using SistemaTicketsInteligente.Api.DAO;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.BLL;

public sealed class BaseConocimientoTIBLL
{
    private readonly BaseConocimientoTIDAO baseConocimientoTIDAO;

    public BaseConocimientoTIBLL(BaseConocimientoTIDAO baseConocimientoTIDAO)
    {
        this.baseConocimientoTIDAO = baseConocimientoTIDAO;
    }

    public Task<BaseConocimientoTIRespuesta> ObtenerAsync(CancellationToken cancellationToken = default) =>
        baseConocimientoTIDAO.ObtenerAsync(cancellationToken);

    public async Task<BaseConocimientoTIDetalle> ObtenerDetalleAsync(string codigo, CancellationToken cancellationToken = default)
    {
        var detalle = await baseConocimientoTIDAO.ObtenerDetalleAsync(ValidarCodigo(codigo), cancellationToken);
        return detalle ?? throw new KeyNotFoundException("El artículo de conocimiento no existe.");
    }

    public async Task<BaseConocimientoTICreadoRespuesta> CrearAsync(string usuario, GuardarBaseConocimientoTISolicitud solicitud, CancellationToken cancellationToken = default)
    {
        NormalizarYValidar(solicitud);
        var codigo = await baseConocimientoTIDAO.CrearAsync(ValidarUsuario(usuario), solicitud, Guid.NewGuid(), cancellationToken);
        if (string.IsNullOrWhiteSpace(codigo)) throw new InvalidOperationException("No fue posible obtener el código del artículo creado.");
        return new BaseConocimientoTICreadoRespuesta { ConocimientoCodigo = codigo };
    }

    public Task ActualizarAsync(string usuario, string codigo, GuardarBaseConocimientoTISolicitud solicitud, CancellationToken cancellationToken = default)
    {
        NormalizarYValidar(solicitud);
        return baseConocimientoTIDAO.ActualizarAsync(ValidarUsuario(usuario), ValidarCodigo(codigo), solicitud, Guid.NewGuid(), cancellationToken);
    }

    public Task EnviarValidacionAsync(string usuario, string codigo, CancellationToken cancellationToken = default) =>
        baseConocimientoTIDAO.EnviarValidacionAsync(ValidarUsuario(usuario), ValidarCodigo(codigo), Guid.NewGuid(), cancellationToken);

    public Task ValidarAsync(string usuario, string codigo, CancellationToken cancellationToken = default) =>
        baseConocimientoTIDAO.ValidarAsync(ValidarUsuario(usuario), ValidarCodigo(codigo), Guid.NewGuid(), cancellationToken);

    public Task InactivarAsync(string usuario, string codigo, CancellationToken cancellationToken = default) =>
        baseConocimientoTIDAO.InactivarAsync(ValidarUsuario(usuario), ValidarCodigo(codigo), Guid.NewGuid(), cancellationToken);

    private static string ValidarUsuario(string usuario)
    {
        var valor = usuario.Trim();
        if (valor.Length == 0 || valor.Length > 20) throw new ArgumentException("El operador autenticado no es válido.", nameof(usuario));
        return valor;
    }

    private static string ValidarCodigo(string codigo)
    {
        var valor = codigo.Trim().ToUpperInvariant();
        if (valor.Length == 0 || valor.Length > 20 || !valor.StartsWith("KB-", StringComparison.Ordinal)) throw new ArgumentException("El código del artículo no es válido.", nameof(codigo));
        return valor;
    }

    private static void NormalizarYValidar(GuardarBaseConocimientoTISolicitud solicitud)
    {
        solicitud.Titulo = solicitud.Titulo.Trim();
        solicitud.Problema = solicitud.Problema.Trim();
        solicitud.Sintomas = solicitud.Sintomas.Trim();
        solicitud.MensajeError = solicitud.MensajeError?.Trim();
        solicitud.Causa = solicitud.Causa.Trim();
        solicitud.Solucion = solicitud.Solucion.Trim();
        solicitud.Procedimiento = solicitud.Procedimiento?.Trim();
        solicitud.Linea = solicitud.Linea.Trim().ToUpperInvariant();
        solicitud.Item = solicitud.Item.Trim().ToUpperInvariant();
        solicitud.Tipo = solicitud.Tipo.Trim().ToUpperInvariant();
        solicitud.SubTipo = solicitud.SubTipo.Trim().ToUpperInvariant();
        solicitud.Categoria = solicitud.Categoria.Trim().ToUpperInvariant();
        solicitud.IncidenciaOrigen = solicitud.IncidenciaOrigen?.Trim().ToUpperInvariant();

        if (solicitud.Titulo.Length is < 5 or > 250) throw new ArgumentException("El título debe contener entre 5 y 250 caracteres.");
        if (solicitud.Problema.Length is < 10 or > 6000) throw new ArgumentException("El problema debe contener entre 10 y 6000 caracteres.");
        if (solicitud.Sintomas.Length is < 5 or > 6000) throw new ArgumentException("Los síntomas deben contener entre 5 y 6000 caracteres.");
        if ((solicitud.MensajeError?.Length ?? 0) > 1000) throw new ArgumentException("El mensaje de error no puede superar los 1000 caracteres.");
        if (solicitud.Causa.Length is < 5 or > 6000) throw new ArgumentException("La causa debe contener entre 5 y 6000 caracteres.");
        if (solicitud.Solucion.Length is < 5 or > 6000) throw new ArgumentException("La solución debe contener entre 5 y 6000 caracteres.");
        if ((solicitud.Procedimiento?.Length ?? 0) > 8000) throw new ArgumentException("El procedimiento no puede superar los 8000 caracteres.");

        if (solicitud.Linea.Length != 3) throw new ArgumentException("Selecciona una línea válida.");
        if (solicitud.Item.Length is < 1 or > 20) throw new ArgumentException("Selecciona un item válido.");
        if (solicitud.Tipo.Length != 3) throw new ArgumentException("Selecciona un tipo válido.");
        if (solicitud.SubTipo.Length != 3) throw new ArgumentException("Selecciona un subtipo válido.");
        if (solicitud.Categoria.Length is < 1 or > 20) throw new ArgumentException("Selecciona una categoría válida.");
        if (!string.IsNullOrWhiteSpace(solicitud.IncidenciaOrigen) && solicitud.IncidenciaOrigen.Length > 12) throw new ArgumentException("El ticket de origen no es válido.");
    }
}
