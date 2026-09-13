/*
 * Archivo: EdicionTicketUsuarioDTO.cs
 * Objetivo: Definir el contrato mínimo para corregir un ticket propio antes de su procesamiento técnico.
 * Responsabilidad: Transportar únicamente campos descriptivos permitidos al usuario final.
 * Dependencias: Ninguna dependencia funcional fuera de .NET.
 * Flujo: Frontend -> EdicionTicketUsuarioController -> BLL -> DAO -> SQL Server.
 * Consideraciones: No expone prioridad, impacto, complejidad, clasificación técnica ni responsable TI.
 */

namespace SistemaTicketsInteligente.Api.DTO;

public sealed class EditarTicketUsuarioSolicitud
{
    public string Linea { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
    public string? MensajeError { get; set; }
}
