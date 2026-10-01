/**
 * Archivo: InicioUsuarioDTO.cs
 * Objetivo: Definir los contratos de datos utilizados por el módulo Inicio para la visión del usuario.
 * Responsabilidad: Representar el resumen, tickets relevantes, acciones pendientes y actividad reciente devueltos por la API.
 * Dependencias: Ninguna dependencia funcional; contiene únicamente estructuras de transporte de datos.
 * Flujo: SQL Server -> InicioUsuarioDAO -> InicioUsuarioBLL -> InicioUsuarioController -> Frontend.
 * Consideraciones: No contiene reglas de negocio, acceso a datos ni lógica de presentación.
 */

namespace SistemaTicketsInteligente.Api.DTO;

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
