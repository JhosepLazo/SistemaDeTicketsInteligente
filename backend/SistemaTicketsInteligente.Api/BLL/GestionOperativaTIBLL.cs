/*
 * Archivo: GestionOperativaTIBLL.cs
 * Objetivo: Aplicar reglas claras a las mejoras operativas de Gestión de Tickets.
 * Responsabilidad: Validar esfuerzo, área causante, solicitudes de aprobación y tickets creados por mesa de ayuda antes de persistirlos.
 * Dependencias: GestionOperativaTIDAO y GestionOperativaTIDTO.
 * Flujo: GestionOperativaTIController -> GestionOperativaTIBLL -> GestionOperativaTIDAO -> SQL Server.
 * Consideraciones: No ejecuta acciones automáticas ni IA; una aprobación solo controla el flujo y un ticket por otro usuario conserva su autor de registro.
 */

using SistemaTicketsInteligente.Api.DAO;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.BLL;

public sealed class GestionOperativaTIBLL
{
    private readonly GestionOperativaTIDAO gestionOperativaTIDAO;

    public GestionOperativaTIBLL(GestionOperativaTIDAO gestionOperativaTIDAO)
    {
        this.gestionOperativaTIDAO = gestionOperativaTIDAO;
    }

    public Task<GestionOperativaTIDatos> ObtenerDatosAsync(string usuario, string area, CancellationToken ct = default) =>
        gestionOperativaTIDAO.ObtenerDatosAsync(Usuario(usuario), Area(area), ct);

    public Task RegistrarAvanceAsync(string usuario, string area, string incidenciaNumero, RegistrarAvanceDetalladoSolicitud s, CancellationToken ct = default)
    {
        s.Detalle = Texto(s.Detalle, 5, 4000, "detalle del avance");
        s.AreaCausante = Area(s.AreaCausante);
        if (s.TiempoUtilizadoMinutos <= 0 || s.TiempoUtilizadoMinutos > 1440) throw new ArgumentException("El tiempo efectivo debe estar entre 1 y 1440 minutos.");
        return gestionOperativaTIDAO.RegistrarAvanceAsync(Usuario(usuario), Area(area), Incidencia(incidenciaNumero), s, Guid.NewGuid(), ct);
    }

    public Task SolicitarAprobacionAsync(string usuario, string area, string incidenciaNumero, SolicitarAprobacionOperativaSolicitud s, CancellationToken ct = default)
    {
        s.AccionCodigo = Codigo(s.AccionCodigo, 50, "acción");
        s.Justificacion = Texto(s.Justificacion, 10, 1000, "justificación");
        return gestionOperativaTIDAO.SolicitarAprobacionAsync(Usuario(usuario), Area(area), Incidencia(incidenciaNumero), s, Guid.NewGuid(), ct);
    }

    public Task<TicketMesaAyudaCreado> CrearTicketPorUsuarioAsync(string usuario, string area, CrearTicketMesaAyudaSolicitud s, CancellationToken ct = default)
    {
        s.UsuarioSolicitante = Codigo(s.UsuarioSolicitante, 20, "usuario solicitante");
        s.Linea = CodigoExacto(s.Linea, 3, "línea");
        s.Tipo = CodigoExacto(s.Tipo, 3, "tipo");
        s.Titulo = Texto(s.Titulo, 5, 250, "título");
        s.Detalle = Texto(s.Detalle, 20, 4000, "detalle");
        s.MensajeError = Opcional(s.MensajeError, 1000, "mensaje de error");
        return gestionOperativaTIDAO.CrearTicketPorUsuarioAsync(Usuario(usuario), Area(area), s, Guid.NewGuid(), ct);
    }

    private static string Usuario(string valor) => Codigo(valor, 20, "usuario autenticado");
    private static string Area(string valor) => CodigoExacto(valor, 3, "área");
    private static string Incidencia(string valor)
    {
        var numero = valor.Trim().ToUpperInvariant();
        if (numero.Length is < 8 or > 12) throw new ArgumentException("El número de ticket no es válido.");
        return numero;
    }
    private static string CodigoExacto(string valor, int longitud, string nombre)
    {
        var codigo = valor.Trim().ToUpperInvariant();
        if (codigo.Length != longitud) throw new ArgumentException($"El código de {nombre} debe tener {longitud} caracteres.");
        return codigo;
    }
    private static string Codigo(string valor, int maximo, string nombre)
    {
        var codigo = valor.Trim().ToUpperInvariant();
        if (codigo.Length == 0 || codigo.Length > maximo) throw new ArgumentException($"El {nombre} no es válido.");
        return codigo;
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
