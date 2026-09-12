/**
 * Archivo: UsuarioAutenticacion.cs
 * Objetivo: Representar internamente la información necesaria para validar la autenticación de un usuario.
 * Responsabilidad: Transportar desde la capa de datos hacia el servicio de autenticación únicamente los datos requeridos para validar identidad, contraseña, estado y perfil.
 * Dependencias: Será construido por AutenticacionRepository y utilizado por AutenticacionService.
 * Flujo: SQL Server -> AutenticacionRepository -> UsuarioAutenticacion -> AutenticacionService.
 * Consideraciones: Es un modelo interno del backend y nunca debe enviarse directamente al frontend porque contiene el hash utilizado para validar la contraseña.
 */

namespace SistemaTicketsInteligente.Api.Models.Autenticacion;
public class UsuarioAutenticacion
{
    public string Usuario { get; set; } = string.Empty;

    public string NombreCompleto { get; set; } = string.Empty;

    public string ClaveHash { get; set; } = string.Empty;

    public string Area { get; set; } = string.Empty;

    public string Perfil { get; set; } = string.Empty;

    public string EstadoUsuario { get; set; } = string.Empty;

    public string EstadoPerfil { get; set; } = string.Empty;
}
