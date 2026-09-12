/**
 * Archivo: AutenticacionController.cs
 * Objetivo: Exponer los endpoints HTTP necesarios para iniciar, consultar y cerrar la sesión del usuario.
 * Responsabilidad: Recibir solicitudes, delegar la autenticación a BLL y administrar la identidad web mediante cookie segura.
 * Dependencias: AutenticacionBLL, DTO de autenticación y autenticación de ASP.NET Core.
 * Flujo: Frontend -> AutenticacionController -> AutenticacionBLL -> AutenticacionDAO -> SQL Server.
 * Consideraciones: No contiene SQL ni valida hashes; los códigos HTTP se determinan a partir del resultado entregado por BLL.
 */

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SistemaTicketsInteligente.Api.BLL;
using SistemaTicketsInteligente.Api.DTO.Autenticacion;

namespace SistemaTicketsInteligente.Api.Controllers;

[ApiController]
[Route("api/autenticacion")]
public sealed class AutenticacionController : ControllerBase
{
    private readonly AutenticacionBLL autenticacionBLL;

    public AutenticacionController(AutenticacionBLL autenticacionBLL)
    {
        this.autenticacionBLL = autenticacionBLL;
    }

    [AllowAnonymous]
    [EnableRateLimiting("Login")]
    [HttpPost("iniciar-sesion")]
    public async Task<IActionResult> IniciarSesion(SolicitudInicioSesion solicitud, CancellationToken cancellationToken)
    {
        var (resultado, respuesta) = await autenticacionBLL.IniciarSesionAsync(solicitud, cancellationToken);

        if (resultado != ResultadoInicioSesion.Correcto)
        {
            return resultado switch
            {
                ResultadoInicioSesion.DatosInvalidos => BadRequest(new { mensaje = "Ingrese un usuario y contraseña válidos." }),
                ResultadoInicioSesion.CredencialesIncorrectas => Unauthorized(new { mensaje = "Usuario o contraseña incorrectos." }),
                ResultadoInicioSesion.UsuarioInactivo => StatusCode(StatusCodes.Status403Forbidden, new { mensaje = "El usuario no se encuentra habilitado para ingresar al sistema." }),
                ResultadoInicioSesion.PerfilInactivo => StatusCode(StatusCodes.Status403Forbidden, new { mensaje = "El perfil del usuario no se encuentra habilitado para ingresar al sistema." }),
                _ => StatusCode(StatusCodes.Status500InternalServerError, new { mensaje = "Se produjo un error al procesar la solicitud." })
            };
        }

        var usuario = respuesta!;
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.Usuario),
            new Claim(ClaimTypes.Name, usuario.NombreCompleto),
            new Claim("Area", usuario.Area),
            new Claim(ClaimTypes.Role, usuario.Perfil)
        };

        var identidad = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidad));

        return Ok(usuario);
    }

    [Authorize]
    [HttpGet("sesion")]
    public ActionResult<RespuestaInicioSesion> Sesion()
    {
        var usuario = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(usuario)) return Unauthorized();

        return Ok(new RespuestaInicioSesion
        {
            Usuario = usuario,
            NombreCompleto = User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty,
            Area = User.FindFirst("Area")?.Value ?? string.Empty,
            Perfil = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty
        });
    }

    [Authorize]
    [HttpPost("cerrar-sesion")]
    public async Task<IActionResult> CerrarSesion(CancellationToken cancellationToken)
    {
        var usuario = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(usuario)) return Unauthorized();

        try
        {
            await autenticacionBLL.CerrarSesionAsync(usuario, cancellationToken);
        }
        finally
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }

        return NoContent();
    }
}
