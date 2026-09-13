/*
 * Archivo: NotificacionesDTO.cs
 * Objetivo: Definir los contratos de la campana de notificaciones operativas.
 * Responsabilidad: Transportar el contador de pendientes y las notificaciones recientes del usuario autenticado.
 * Dependencias: Ninguna dependencia funcional fuera de .NET.
 * Flujo: SQL Server -> NotificacionesDAO -> NotificacionesBLL -> NotificacionesController -> Frontend.
 * Consideraciones: Una notificación puede enlazar a un módulo interno, pero nunca concede permisos ni reemplaza las validaciones del destino.
 */

namespace SistemaTicketsInteligente.Api.DTO;

public sealed class NotificacionesRespuesta
{
    public int NoLeidas { get; set; }
    public List<NotificacionItem> Notificaciones { get; set; } = [];
}

public sealed class NotificacionItem
{
    public long NotificacionNumero { get; set; }
    public string IncidenciaNumero { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public string Ruta { get; set; } = string.Empty;
    public bool Leida { get; set; }
    public DateTime Fecha { get; set; }
}
