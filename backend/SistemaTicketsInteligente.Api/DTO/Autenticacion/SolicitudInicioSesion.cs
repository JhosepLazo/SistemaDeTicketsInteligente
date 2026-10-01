/**
 * Archivo: SolicitudInicioSesion.cs
 * Objetivo: Representar y validar las credenciales enviadas para iniciar sesión.
 * Responsabilidad: Transportar únicamente usuario y contraseña desde la API hacia la lógica de autenticación.
 * Dependencias: System.ComponentModel.DataAnnotations.
 * Flujo: Frontend -> AutenticacionController -> AutenticacionBLL.
 * Consideraciones: El usuario se normaliza en BLL; la contraseña no se recorta ni se transforma en este DTO.
 */

using System.ComponentModel.DataAnnotations;

namespace SistemaTicketsInteligente.Api.DTO.Autenticacion;

public sealed class SolicitudInicioSesion
{
    [Required]
    [StringLength(20)]
    public string Usuario { get; init; } = string.Empty;

    [Required]
    public string Contrasena { get; init; } = string.Empty;
}
