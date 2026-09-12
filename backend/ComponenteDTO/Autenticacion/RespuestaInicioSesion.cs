/**
 * Archivo: RespuestaInicioSesion.cs
 * Objetivo: Representar la información segura que la API devuelve después de autenticar al usuario.
 * Responsabilidad: Exponer únicamente los datos mínimos requeridos por el frontend y la sesión autenticada.
 * Dependencias: Ninguna dependencia funcional externa.
 * Flujo: AutenticacionBLL -> AutenticacionController -> Frontend.
 * Consideraciones: Nunca debe contener contraseña, hash, estados internos ni datos técnicos de autenticación.
 */

namespace SistemaTicketsInteligente.Entidades.ComponenteDTO.Autenticacion;

public sealed class RespuestaInicioSesion
{
    public string Usuario { get; init; } = string.Empty;
    public string NombreCompleto { get; init; } = string.Empty;
    public string Area { get; init; } = string.Empty;
    public string Perfil { get; init; } = string.Empty;
}
