/*
 * Archivo: GestionOperativaTIDTO.cs
 * Objetivo: Definir los contratos de las mejoras operativas que complementan Gestión de Tickets sin introducir IA.
 * Responsabilidad: Transportar catálogos de mesa de ayuda/aprobación, avances con esfuerzo y solicitudes de creación de tickets por otro usuario.
 * Dependencias: Ninguna dependencia funcional fuera de .NET.
 * Flujo: Frontend <-> GestionOperativaTIController <-> GestionOperativaTIBLL <-> GestionOperativaTIDAO <-> SQL Server.
 * Consideraciones: Se mantiene separado del DTO histórico de Gestión de Tickets para no inflar contratos que no requieren estas acciones.
 */

namespace SistemaTicketsInteligente.Api.DTO;

public sealed class GestionOperativaTIDatos
{
    public List<GestionOperativaUsuario> Usuarios { get; set; } = [];
    public List<GestionOperativaAccion> AccionesAprobacion { get; set; } = [];
    public List<GestionTicketsTICatalogoItem> Lineas { get; set; } = [];
    public List<GestionTicketsTICatalogoItem> Tipos { get; set; } = [];
}

public sealed class GestionOperativaUsuario : GestionTicketsTICatalogoItem
{
    public string Area { get; set; } = string.Empty;
}

public sealed class GestionOperativaAccion
{
    public string AccionCodigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string NivelRiesgo { get; set; } = string.Empty;
}

public sealed class RegistrarAvanceDetalladoSolicitud
{
    public string Detalle { get; set; } = string.Empty;
    public bool VisibleUsuario { get; set; }
    public decimal TiempoUtilizadoMinutos { get; set; }
    public string AreaCausante { get; set; } = string.Empty;
}

public sealed class SolicitarAprobacionOperativaSolicitud
{
    public string AccionCodigo { get; set; } = string.Empty;
    public string Justificacion { get; set; } = string.Empty;
}

public sealed class CrearTicketMesaAyudaSolicitud
{
    public string UsuarioSolicitante { get; set; } = string.Empty;
    public string Linea { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
    public string? MensajeError { get; set; }
}

public sealed class TicketMesaAyudaCreado
{
    public string IncidenciaNumero { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; }
}
