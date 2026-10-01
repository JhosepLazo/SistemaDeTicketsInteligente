/*
 * Archivo: IdentidadCorporativaDTO.cs
 * Objetivo: Definir los datos mínimos obtenidos desde el origen corporativo para autenticación y sincronización.
 * Responsabilidad: Transportar identidad, cargo y estado sin exponer ni almacenar la contraseña corporativa.
 * Dependencias: Ninguna dependencia funcional fuera de .NET.
 * Flujo: Spring -> IdentidadCorporativaDAO -> AutenticacionBLL / ConfiguracionTIBLL.
 * Consideraciones: La autorización local (Área y Perfil) pertenece al Sistema de Tickets; Spring solo provee identidad corporativa.
 */

namespace SistemaTicketsInteligente.Api.DTO;

public sealed class UsuarioCorporativo
{
    public string Usuario { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string Cargo { get; set; } = string.Empty;
    public string Documento { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string EstadoEmpleado { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
}

public sealed class CargoCorporativo
{
    public string Cargo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
}
