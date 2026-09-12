using Microsoft.AspNetCore.Mvc;
using SistemaTicketsInteligente.Negocio.ComponenteBLL.Autenticacion;

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

    // El endpoint se agregará cuando se aprueben el origen de identidad,
    // el algoritmo de hash y el contrato definitivo de autenticación.
}
