/**
 * Archivo: SolicitudInicioSesion.cs
 * Objetivo: Representar las credenciales enviadas para iniciar sesión.
 * Responsabilidad: Transportar únicamente usuario y contraseña desde la API hacia la lógica de autenticación.
 * Dependencias: Ninguna dependencia funcional externa.
 * Flujo: Frontend -> AutenticacionController -> AutenticacionBLL.
 * Consideraciones: El usuario se normaliza en BLL; la contraseña no se recorta ni se transforma en este DTO.
 */

namespace SistemaTicketsInteligente.Api.DTO.Autenticacion;

public sealed class SolicitudInicioSesion
{
    public string Usuario { get; init; } = string.Empty;
    public string Contrasena { get; init; } = string.Empty;
}
