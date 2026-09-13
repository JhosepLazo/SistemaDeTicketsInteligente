/**
 * Archivo: InicioUsuarioController.cs
 * Objetivo: Exponer la información del módulo Inicio para el usuario autenticado.
 * Responsabilidad: Obtener la identidad desde la sesión y devolver el dashboard personal mediante un endpoint protegido.
 * Dependencias: InicioUsuarioBLL, autenticación por cookie y claims de ASP.NET Core.
 * Flujo: Frontend -> InicioUsuarioController -> InicioUsuarioBLL -> InicioUsuarioDAO -> SQL Server.
 * Consideraciones: No acepta el usuario por parámetro para evitar consultar información de otra cuenta; siempre utiliza la identidad autenticada.
 */

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaTicketsInteligente.Api.BLL;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/inicio")]
public sealed class InicioUsuarioController : ControllerBase
{
    private readonly InicioUsuarioBLL inicioUsuarioBLL;

    public InicioUsuarioController(InicioUsuarioBLL inicioUsuarioBLL)
    {
        this.inicioUsuarioBLL = inicioUsuarioBLL;
    }

    [HttpGet("usuario")]
    public async Task<ActionResult<InicioUsuarioRespuesta>> Obtener(CancellationToken cancellationToken)
    {
        var usuario = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(usuario)) return Unauthorized();

        return Ok(await inicioUsuarioBLL.ObtenerAsync(usuario, cancellationToken));
    }
}
