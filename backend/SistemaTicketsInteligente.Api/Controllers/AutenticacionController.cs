/**
 * Archivo: AutenticacionController.cs
 * Objetivo: Iniciar, consultar y cerrar la sesión web del usuario.
 * Responsabilidad: Traducir el resultado del inicio de sesión a HTTP, crear la cookie con los claims mínimos, entregar el token
 *   antifalsificación (CSRF) de la sesión y cerrarla.
 * Dependencias: AutenticacionBLL, Cookie Authentication, IAntiforgery y el rate limiting "Login".
 * Flujo: LoginPage -> AutenticacionController -> AutenticacionBLL -> cookie HttpOnly.
 * Consideraciones: Las respuestas de error son genéricas para no revelar si un usuario existe; la contraseña nunca se registra.
 */

using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace SistemaTicketsInteligente.Api.Controllers;

[ApiController]
[Route("api/autenticacion")]
public sealed class AutenticacionController(AutenticacionBLL autenticacion, IAntiforgery antiforgery) : ControladorBase
{
    [AllowAnonymous]
    [EnableRateLimiting("Login")]
    [HttpPost("iniciar-sesion")]
    public async Task<IActionResult> IniciarSesion(SolicitudInicioSesion solicitud, CancellationToken ct)
    {
        var (resultado, sesion) = await autenticacion.IniciarSesionAsync(solicitud, ct);
        if (resultado != ResultadoInicioSesion.Correcto || sesion is null)
        {
            return resultado switch
            {
                ResultadoInicioSesion.DatosInvalidos => BadRequest(new { mensaje = "Ingrese un usuario y contraseña válidos." }),
                ResultadoInicioSesion.CredencialesIncorrectas => Unauthorized(new { mensaje = "Usuario o contraseña incorrectos." }),
                ResultadoInicioSesion.UsuarioInactivo => StatusCode(StatusCodes.Status403Forbidden, new { mensaje = "El usuario no se encuentra habilitado para ingresar al sistema." }),
                ResultadoInicioSesion.PerfilInactivo => StatusCode(StatusCodes.Status403Forbidden, new { mensaje = "El perfil del usuario no se encuentra habilitado para ingresar al sistema." }),
                ResultadoInicioSesion.UsuarioSinConfiguracionLocal => StatusCode(StatusCodes.Status403Forbidden, new { mensaje = "Tu identidad corporativa es válida, pero todavía no tiene área y perfil configurados en Gestión TI. Comunícate con TI." }),
                ResultadoInicioSesion.IdentidadCorporativaNoDisponible => StatusCode(StatusCodes.Status503ServiceUnavailable, new { mensaje = "El servicio de identidad corporativa no se encuentra disponible. Intenta nuevamente en unos minutos." }),
                ResultadoInicioSesion.DemasiadosIntentos => StatusCode(StatusCodes.Status429TooManyRequests, new { mensaje = "Demasiados intentos fallidos para este usuario. Espera un minuto e intenta nuevamente." }),
                _ => StatusCode(StatusCodes.Status500InternalServerError, new { mensaje = "Se produjo un error al procesar la solicitud." })
            };
        }

        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, sesion.Usuario),
            new(ClaimTypes.Name, sesion.NombreCompleto),
            new("Area", sesion.Area),
            new(ClaimTypes.Role, sesion.Perfil)
        ];
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties { IsPersistent = true, AllowRefresh = true });
        return Ok(sesion);
    }

    [Authorize]
    [HttpGet("sesion")]
    public IActionResult Sesion()
    {
        if (string.IsNullOrWhiteSpace(Usuario)) return Unauthorized();
        return Ok(new RespuestaInicioSesion
        {
            Usuario = Usuario,
            NombreCompleto = User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty,
            Area = Area,
            Perfil = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty
        });
    }

    /// <summary>Token antifalsificación de la sesión: el frontend lo envía en X-CSRF-TOKEN en toda operación que modifica datos.</summary>
    [Authorize]
    [HttpGet("token-csrf")]
    public IActionResult TokenCsrf() => Ok(new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });

    [Authorize]
    [HttpPost("cerrar-sesion")]
    public async Task<IActionResult> CerrarSesion(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Usuario)) return Unauthorized();
        try { await autenticacion.CerrarSesionAsync(Usuario, ct); }
        finally { await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); }
        return NoContent();
    }
}
