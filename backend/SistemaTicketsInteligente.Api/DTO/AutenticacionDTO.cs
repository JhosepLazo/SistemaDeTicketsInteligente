/**
 * Archivo: AutenticacionDTO.cs
 * Objetivo: Definir los contratos del inicio de sesión y de la identidad corporativa (Spring).
 * Responsabilidad: Transportar credenciales, la sesión segura que recibe el frontend y los datos internos de identidad.
 * Dependencias: Ninguna; solo estructuras de transporte de datos.
 * Flujo: Frontend -> AutenticacionController -> AutenticacionBLL (-> IdentidadCorporativa) -> Frontend.
 * Consideraciones: UsuarioAutenticacion contiene ClaveHash y nunca se envía al frontend. Spring solo provee identidad;
 *   el área y el perfil locales pertenecen al Sistema de Tickets.
 */

namespace SistemaTicketsInteligente.Api.DTO;

/// <summary>Credenciales del formulario de ingreso; la contraseña no se recorta ni se transforma.</summary>
public sealed class SolicitudInicioSesion
{
    public string Usuario { get; init; } = string.Empty;
    public string Contrasena { get; init; } = string.Empty;
}

/// <summary>Datos mínimos de la sesión que recibe el frontend: nunca contraseña, hash ni estados internos.</summary>
public sealed class RespuestaInicioSesion
{
    public string Usuario { get; init; } = string.Empty;
    public string NombreCompleto { get; init; } = string.Empty;
    public string Area { get; init; } = string.Empty;
    public string Perfil { get; init; } = string.Empty;
}

/// <summary>Usuario local leído para validar el ingreso (incluye el hash de la contraseña).</summary>
public sealed class UsuarioAutenticacion
{
    public string Usuario { get; init; } = string.Empty;
    public string NombreCompleto { get; init; } = string.Empty;
    public string ClaveHash { get; init; } = string.Empty;
    public string Area { get; init; } = string.Empty;
    public string Perfil { get; init; } = string.Empty;
    public string EstadoUsuario { get; init; } = string.Empty;
    public string EstadoPerfil { get; init; } = string.Empty;
}

/// <summary>Identidad corporativa del colaborador según Spring.</summary>
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
