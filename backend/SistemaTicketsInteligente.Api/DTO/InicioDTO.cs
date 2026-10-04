/**
 * Archivo: InicioDTO.cs
 * Objetivo: Definir los contratos del módulo Inicio, tanto para el colaborador como para el operador TI.
 * Responsabilidad: Representar resúmenes, tickets relevantes, acciones pendientes, recordatorios y actividad reciente.
 * Dependencias: Ninguna; solo estructuras de transporte de datos.
 * Flujo: SQL Server -> InicioBLL -> InicioController -> Frontend.
 * Consideraciones: No contiene reglas de negocio, acceso a datos ni lógica de presentación.
 */

namespace SistemaTicketsInteligente.Api.DTO;

// ---- Inicio del colaborador

public sealed class InicioUsuarioRespuesta
{
    public InicioUsuarioResumen Resumen { get; set; } = new();
    public List<InicioUsuarioTicket> RequierenAtencion { get; set; } = [];
    public List<InicioUsuarioTicket> TicketsRecientes { get; set; } = [];
    public List<InicioUsuarioAccionPendiente> AccionesPendientes { get; set; } = [];
    public List<InicioUsuarioActividad> ActividadReciente { get; set; } = [];
}

public sealed class InicioUsuarioResumen
{
    public int TicketsActivos { get; set; }
    public int EnAtencion { get; set; }
    public int RequierenAtencion { get; set; }
    public int Resueltos30Dias { get; set; }
    public int PendientesCalificacion { get; set; }
}

public sealed class InicioUsuarioTicket
{
    public string IncidenciaNumero { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string EstadoDescripcion { get; set; } = string.Empty;
    public string Responsable { get; set; } = string.Empty;
    public string Accion { get; set; } = string.Empty;
    public DateTime UltimaFechaModif { get; set; }
}

public sealed class InicioUsuarioAccionPendiente
{
    public string IncidenciaNumero { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string TipoAccion { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public DateTime UltimaFechaModif { get; set; }
}

public sealed class InicioUsuarioActividad
{
    public string IncidenciaNumero { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string EstadoDescripcion { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public DateTime FechaCambio { get; set; }
}

// ---- Inicio del operador TI

public sealed class InicioTIRespuesta
{
    public InicioTIResumen Resumen { get; set; } = new();
    public List<InicioTITicketPrioritario> RequierenAtencion { get; set; } = [];
    public InicioTIRecordatorios Recordatorios { get; set; } = new();
    public List<InicioTITicketActivo> TicketsActivos { get; set; } = [];
    public List<InicioTIActividad> ActividadReciente { get; set; } = [];
}

public sealed class InicioTIResumen
{
    public int Pendientes { get; set; }
    public int PendientesDesdeAyer { get; set; }
    public int EnAtencion { get; set; }
    public int EnProgresoHoy { get; set; }
    public int RequierenAccion { get; set; }
    public int SinAsignar { get; set; }
    public int PrioridadAlta { get; set; }
    public int TicketsActivos { get; set; }
    public int MisAsignados { get; set; }
}

public sealed class InicioTITicketPrioritario
{
    public string IncidenciaNumero { get; set; } = string.Empty;
    public string UsuarioSolicitante { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string TipoDescripcion { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string EstadoDescripcion { get; set; } = string.Empty;
    public int? Prioridad { get; set; }
    public string UsuarioTI { get; set; } = string.Empty;
    public string Responsable { get; set; } = string.Empty;
    public DateTime UltimaFechaModif { get; set; }
    public int? SlaMinutosRestantes { get; set; }
    public string TipoAtencion { get; set; } = string.Empty;
    public string Accion { get; set; } = string.Empty;
}

public sealed class InicioTIRecordatorios
{
    public int AprobacionesPendientes { get; set; }
    public int SlaPorVencer { get; set; }
    public int TicketsReabiertos { get; set; }
}

public sealed class InicioTITicketActivo
{
    public string IncidenciaNumero { get; set; } = string.Empty;
    public string UsuarioSolicitante { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string TipoDescripcion { get; set; } = string.Empty;
    public int? Prioridad { get; set; }
    public string AreaTI { get; set; } = string.Empty;
    public string GrupoSoporte { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string EstadoDescripcion { get; set; } = string.Empty;
    public string UsuarioTI { get; set; } = string.Empty;
    public string Responsable { get; set; } = string.Empty;
    public DateTime UltimaFechaModif { get; set; }
}

public sealed class InicioTIActividad
{
    public string IncidenciaNumero { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string EstadoDescripcion { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public DateTime FechaCambio { get; set; }
}
