/**
 * Archivo: MisTicketsUsuarioDTO.cs
 * Objetivo: Definir los contratos utilizados por el módulo Mis Tickets para la visión del usuario.
 * Responsabilidad: Transportar resumen, listado, detalle, historial, mensajes, adjuntos, documentos y solicitudes de acciones del usuario.
 * Dependencias: ASP.NET Core para IFormFile en la respuesta de observaciones.
 * Flujo: Frontend <-> MisTicketsUsuarioController <-> MisTicketsUsuarioBLL <-> SQL Server.
 * Consideraciones: Los DTO no contienen reglas de negocio ni acceso a datos; la identidad del usuario nunca forma parte de las solicitudes enviadas por el frontend.
 */

using Microsoft.AspNetCore.Http;

namespace SistemaTicketsInteligente.Api.DTO;

public sealed class MisTicketsUsuarioRespuesta
{
    public MisTicketsUsuarioResumen Resumen { get; set; } = new();
    public List<MisTicketsUsuarioItem> Tickets { get; set; } = [];
}

public sealed class MisTicketsUsuarioResumen
{
    public int Activos { get; set; }
    public int EnAtencion { get; set; }
    public int PendientesRespuesta { get; set; }
    public int Resueltos30Dias { get; set; }
}

public sealed class MisTicketsUsuarioItem
{
    public string IncidenciaNumero { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string EstadoDescripcion { get; set; } = string.Empty;
    public string Responsable { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; }
    public DateTime UltimaFechaModif { get; set; }
    public int? Prioridad { get; set; }
    public string Accion { get; set; } = string.Empty;
}

public sealed class MisTicketsUsuarioDetalle
{
    /// <summary>Ficha registrada con el ticket (vacía si el tipo no tiene ficha).</summary>
    public List<DatoFichaTicket> Ficha { get; set; } = [];
    public string IncidenciaNumero { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
    public string MensajeError { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string EstadoDescripcion { get; set; } = string.Empty;
    public string Linea { get; set; } = string.Empty;
    public string LineaDescripcion { get; set; } = string.Empty;
    public string Item { get; set; } = string.Empty;
    public string ItemDescripcion { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string TipoDescripcion { get; set; } = string.Empty;
    public string AreaDescripcion { get; set; } = string.Empty;
    public string Responsable { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; }
    public DateTime? FechaAsignacion { get; set; }
    public DateTime? FechaAtencion { get; set; }
    public DateTime? FechaCierre { get; set; }
    public DateTime UltimaFechaModif { get; set; }
    public int? Prioridad { get; set; }
    public byte? Calificacion { get; set; }
    public string ComentarioCalificacion { get; set; } = string.Empty;
    public string RespuestaUsuario { get; set; } = string.Empty;
    public string Accion { get; set; } = string.Empty;
    public List<MisTicketsUsuarioEstado> HistorialEstados { get; set; } = [];
    public List<MisTicketsUsuarioAvance> Avances { get; set; } = [];
    public List<MisTicketsUsuarioMensaje> Mensajes { get; set; } = [];
    public List<MisTicketsUsuarioAdjunto> Adjuntos { get; set; } = [];
    public List<MisTicketsUsuarioDocumento> Documentos { get; set; } = [];
}

public sealed class MisTicketsUsuarioEstado
{
    public int Secuencia { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string EstadoDescripcion { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string Observacion { get; set; } = string.Empty;
}

public sealed class MisTicketsUsuarioAvance
{
    public int Secuencia { get; set; }
    public string Responsable { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string Detalle { get; set; } = string.Empty;
    public decimal? PorcentajeAvance { get; set; }
}

public sealed class MisTicketsUsuarioMensaje
{
    public int Secuencia { get; set; }
    public string TipoAutor { get; set; } = string.Empty;
    public string Autor { get; set; } = string.Empty;
    public string Contenido { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
}

public sealed class MisTicketsUsuarioAdjunto
{
    public int Secuencia { get; set; }
    public int? MensajeSecuencia { get; set; }
    public string NombreOriginal { get; set; } = string.Empty;
    public string TipoMime { get; set; } = string.Empty;
    public long TamanoBytes { get; set; }
    public DateTime FechaRegistro { get; set; }
}

public sealed class MisTicketsUsuarioDocumento
{
    public int Secuencia { get; set; }
    public string CompaniaSocio { get; set; } = string.Empty;
    public string TipoDocumento { get; set; } = string.Empty;
    public string NumeroDocumento { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
}

public sealed class ResponderObservacionSolicitud
{
    public string Contenido { get; set; } = string.Empty;
    public List<IFormFile> Adjuntos { get; set; } = [];
}

public sealed class ValidarSolucionSolicitud
{
    public bool Solucionada { get; set; }
    public string? Comentario { get; set; }
}

public sealed class CalificarTicketSolicitud
{
    public byte Calificacion { get; set; }
    public string? Comentario { get; set; }
}

/// <summary>Corrección de un ticket propio antes de su procesamiento técnico: solo campos descriptivos, nunca clasificación ni responsable.</summary>
public sealed class EditarTicketUsuarioSolicitud
{
    public string Linea { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
    public string? MensajeError { get; set; }
}
