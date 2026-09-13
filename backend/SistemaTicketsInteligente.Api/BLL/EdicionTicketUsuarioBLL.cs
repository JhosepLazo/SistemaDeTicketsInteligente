/*
 * Archivo: EdicionTicketUsuarioBLL.cs
 * Objetivo: Aplicar las validaciones de edición permitidas al usuario antes del procesamiento técnico.
 * Responsabilidad: Normalizar datos descriptivos y delegar la persistencia al DAO.
 * Dependencias: EdicionTicketUsuarioDAO y EdicionTicketUsuarioDTO.
 * Flujo: EdicionTicketUsuarioController -> EdicionTicketUsuarioBLL -> EdicionTicketUsuarioDAO -> SQL Server.
 * Consideraciones: El backend y el Stored Procedure vuelven a validar propiedad y estado; no se confía en controles del frontend.
 */

using SistemaTicketsInteligente.Api.DAO;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.BLL;

public sealed class EdicionTicketUsuarioBLL
{
    private readonly EdicionTicketUsuarioDAO edicionTicketUsuarioDAO;

    public EdicionTicketUsuarioBLL(EdicionTicketUsuarioDAO edicionTicketUsuarioDAO)
    {
        this.edicionTicketUsuarioDAO = edicionTicketUsuarioDAO;
    }

    public Task EditarAsync(string usuario, string incidenciaNumero, EditarTicketUsuarioSolicitud solicitud, CancellationToken ct = default)
    {
        usuario = Normalizar(usuario, 20, "usuario autenticado");
        incidenciaNumero = Normalizar(incidenciaNumero, 12, "ticket");
        solicitud.Linea = CodigoExacto(solicitud.Linea, 3, "línea");
        solicitud.Tipo = CodigoExacto(solicitud.Tipo, 3, "tipo");
        solicitud.Titulo = Texto(solicitud.Titulo, 5, 250, "título");
        solicitud.Detalle = Texto(solicitud.Detalle, 20, 1000, "detalle");
        solicitud.MensajeError = Opcional(solicitud.MensajeError, 1000, "mensaje de error");

        return edicionTicketUsuarioDAO.EditarAsync(usuario, incidenciaNumero, solicitud, Guid.NewGuid(), ct);
    }

    private static string CodigoExacto(string valor, int longitud, string nombre)
    {
        var codigo = valor.Trim().ToUpperInvariant();
        if (codigo.Length != longitud) throw new ArgumentException($"El código de {nombre} debe tener {longitud} caracteres.");
        return codigo;
    }

    private static string Normalizar(string valor, int maximo, string nombre)
    {
        var texto = valor.Trim();
        if (texto.Length == 0 || texto.Length > maximo) throw new ArgumentException($"El {nombre} no es válido.");
        return texto;
    }

    private static string Texto(string valor, int minimo, int maximo, string nombre)
    {
        var texto = valor.Trim();
        if (texto.Length < minimo || texto.Length > maximo) throw new ArgumentException($"El {nombre} debe contener entre {minimo} y {maximo} caracteres.");
        return texto;
    }

    private static string? Opcional(string? valor, int maximo, string nombre)
    {
        var texto = valor?.Trim();
        if (string.IsNullOrEmpty(texto)) return null;
        if (texto.Length > maximo) throw new ArgumentException($"El {nombre} no puede superar {maximo} caracteres.");
        return texto;
    }
}
