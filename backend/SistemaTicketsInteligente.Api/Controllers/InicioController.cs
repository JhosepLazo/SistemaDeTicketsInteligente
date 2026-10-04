/**
 * Archivo: InicioController.cs
 * Objetivo: Exponer la pantalla de inicio del colaborador y la del operador TI.
 * Responsabilidad: Delegar en InicioBLL con la identidad de la cookie; cada pantalla solo responde a su perfil.
 * Dependencias: InicioBLL y la autenticación por cookie.
 * Flujo: InicioPage / InicioTIPage -> InicioController -> InicioBLL.
 * Consideraciones: El perfil se valida aquí; los procedimientos filtran además lo que corresponde al usuario o a su área.
 */

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SistemaTicketsInteligente.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/inicio")]
public sealed class InicioController(InicioBLL inicio) : ControladorBase
{
    [HttpGet("usuario")]
    [Authorize(Roles = "USR")]
    public Task<IActionResult> ObtenerUsuario(CancellationToken ct) => Responder(async () => Ok(await inicio.ObtenerUsuarioAsync(Usuario, ct)));

    [HttpGet("ti")]
    [Authorize(Roles = "TEC,SUP,ADM")]
    public Task<IActionResult> ObtenerTI(CancellationToken ct) => Responder(async () => Ok(await inicio.ObtenerTIAsync(Usuario, Area, ct)));
}
