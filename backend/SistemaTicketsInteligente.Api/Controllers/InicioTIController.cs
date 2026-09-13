/**
 * Archivo: InicioTIController.cs
 * Objetivo: Exponer la información del módulo Inicio para operadores TI autenticados.
 * Responsabilidad: Obtener identidad, área y perfil desde la sesión y devolver el dashboard operativo mediante un endpoint protegido.
 * Dependencias: InicioTIBLL, autenticación por cookie y claims de ASP.NET Core.
 * Flujo: Frontend -> InicioTIController -> InicioTIBLL -> InicioTIDAO -> SQL Server.
 * Consideraciones: No acepta usuario ni área por parámetro; utiliza exclusivamente la identidad autenticada y restringe el acceso a perfiles TI.
 */

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaTicketsInteligente.Api.BLL;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.Controllers;

[ApiController]
[Authorize(Roles = "TEC,SUP,ADM")]
[Route("api/inicio/ti")]
public sealed class InicioTIController : ControllerBase
{
    private readonly InicioTIBLL inicioTIBLL;

    public InicioTIController(InicioTIBLL inicioTIBLL)
    {
        this.inicioTIBLL = inicioTIBLL;
    }

    [HttpGet]
    public async Task<ActionResult<InicioTIRespuesta>> Obtener(CancellationToken cancellationToken)
    {
        var usuario = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var area = User.FindFirst("Area")?.Value;
        if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(area)) return Unauthorized();

        return Ok(await inicioTIBLL.ObtenerAsync(usuario, area, cancellationToken));
    }
}
