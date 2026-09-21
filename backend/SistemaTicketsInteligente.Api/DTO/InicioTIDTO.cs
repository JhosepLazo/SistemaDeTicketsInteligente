/**
 * Archivo: InicioTIDTO.cs
 * Objetivo: Definir los contratos de datos utilizados por el módulo Inicio para operadores TI.
 * Responsabilidad: Representar resumen operativo, tickets prioritarios, recordatorios, tickets activos y actividad reciente devueltos por la API.
 * Dependencias: Ninguna dependencia funcional; contiene únicamente estructuras de transporte de datos.
 * Flujo: SQL Server -> InicioTIDAO -> InicioTIBLL -> InicioTIController -> Frontend.
 * Consideraciones: No contiene reglas de negocio, acceso a datos ni lógica de presentación.
 */

namespace SistemaTicketsInteligente.Api.DTO;

public sealed class InicioTIRespuesta
{
    public InicioTIResumen Resumen { get; set; } = new();
    public List<InicioTITicketPrioritario> RequierenAtencion { get; set; } = [];
    public InicioTIReminder Recordatorios { get; set; } = new();
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

public sealed class InicioTIReminder
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
