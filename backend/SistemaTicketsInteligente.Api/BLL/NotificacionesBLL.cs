/*
 * Archivo: NotificacionesBLL.cs
 * Objetivo: Aplicar validaciones mínimas al flujo de notificaciones del usuario autenticado.
 * Responsabilidad: Validar identidad y delegar consulta/marcado de lectura al DAO.
 * Dependencias: NotificacionesDAO y NotificacionesDTO.
 * Flujo: NotificacionesController -> NotificacionesBLL -> NotificacionesDAO -> SQL Server.
 * Consideraciones: No decide permisos del módulo destino; cada endpoint continúa aplicando su propia autorización.
 */

using SistemaTicketsInteligente.Api.DAO;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.BLL;

public sealed class NotificacionesBLL
{
    private readonly NotificacionesDAO notificacionesDAO;

    public NotificacionesBLL(NotificacionesDAO notificacionesDAO)
    {
        this.notificacionesDAO = notificacionesDAO;
    }

    public Task<NotificacionesRespuesta> ObtenerAsync(string usuario, CancellationToken cancellationToken = default) =>
        notificacionesDAO.ObtenerAsync(ValidarUsuario(usuario), cancellationToken);

    public Task MarcarLeidaAsync(string usuario, long notificacionNumero, CancellationToken cancellationToken = default)
    {
        if (notificacionNumero <= 0) throw new ArgumentException("La notificación indicada no es válida.");
        return notificacionesDAO.MarcarLeidaAsync(ValidarUsuario(usuario), notificacionNumero, cancellationToken);
    }

    private static string ValidarUsuario(string usuario)
    {
        var valor = usuario.Trim();
        if (valor.Length == 0 || valor.Length > 20) throw new ArgumentException("El usuario autenticado no es válido.");
        return valor;
    }
}
