/**
 * Archivo: ManejadorAutenticacionPrueba.cs
 * Objetivo: Simular una sesión iniciada sin pasar por el login ni por la base de datos.
 * Responsabilidad: Construir la identidad con los mismos claims que crea AutenticacionController (usuario, nombre, área y perfil) a partir
 *   del encabezado X-Prueba-Identidad ("USUARIO|AREA|PERFIL"); sin encabezado la petición es anónima.
 * Dependencias: ASP.NET Core Authentication.
 * Flujo: FabricaApi lo registra como esquema por defecto -> cada petición de prueba declara su identidad -> autorización real de la API.
 * Consideraciones: Solo existe en el proyecto de pruebas. La autorización, el CSRF y los controladores son los de producción.
 */

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SistemaTicketsInteligente.Pruebas.Integracion;

public sealed class ManejadorAutenticacionPrueba(IOptionsMonitor<AuthenticationSchemeOptions> opciones, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(opciones, logger, encoder)
{
    public const string Esquema = "Prueba";
    public const string Encabezado = "X-Prueba-Identidad";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var valor = Request.Headers[Encabezado].ToString();
        if (string.IsNullOrEmpty(valor)) return Task.FromResult(AuthenticateResult.NoResult());
        var partes = valor.Split('|');
        if (partes.Length != 3) return Task.FromResult(AuthenticateResult.Fail("Identidad de prueba inválida."));

        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, partes[0]),
            new(ClaimTypes.Name, partes[0]),
            new("Area", partes[1]),
            new(ClaimTypes.Role, partes[2])
        ];
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Esquema));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Esquema)));
    }
}
