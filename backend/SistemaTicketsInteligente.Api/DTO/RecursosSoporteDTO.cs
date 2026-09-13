/*
 * Archivo: RecursosSoporteDTO.cs
 * Objetivo: Definir los contratos de formatos frecuentes y artículos de ayuda visibles para usuarios.
 * Responsabilidad: Transportar autoservicio estático reutilizando formatos y conocimiento validado sin crear un módulo adicional.
 * Dependencias: Ninguna dependencia funcional fuera de .NET.
 * Flujo: SQL Server -> RecursosSoporteDAO -> RecursosSoporteBLL -> RecursosSoporteController -> Nuevo Ticket.
 * Consideraciones: No implementa RAG ni IA; solo expone contenido marcado explícitamente como visible y vigente.
 */

namespace SistemaTicketsInteligente.Api.DTO;

public sealed class RecursosSoporteRespuesta
{
    public List<RecursoFormato> Formatos { get; set; } = [];
    public List<RecursoArticulo> Articulos { get; set; } = [];
}

public sealed class RecursoFormato
{
    public string FormatoCodigo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string NombreOriginal { get; set; } = string.Empty;
    public string TipoMime { get; set; } = string.Empty;
    public string TipoTicket { get; set; } = string.Empty;
}

public sealed class RecursoArticulo
{
    public string ConocimientoCodigo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Problema { get; set; } = string.Empty;
    public string Sintomas { get; set; } = string.Empty;
    public string Solucion { get; set; } = string.Empty;
    public string Procedimiento { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
}

public sealed class RecursoArchivo
{
    public string NombreOriginal { get; set; } = string.Empty;
    public string RutaArchivo { get; set; } = string.Empty;
    public string TipoMime { get; set; } = string.Empty;
}
