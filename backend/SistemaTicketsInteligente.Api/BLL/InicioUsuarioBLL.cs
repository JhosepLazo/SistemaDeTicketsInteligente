/**
 * Archivo: InicioUsuarioBLL.cs
 * Objetivo: Coordinar la obtención del Inicio del usuario autenticado.
 * Responsabilidad: Validar la identidad recibida y delegar la consulta consolidada al DAO.
 * Dependencias: InicioUsuarioDAO e InicioUsuarioDTO.
 * Flujo: InicioUsuarioController -> InicioUsuarioBLL -> InicioUsuarioDAO -> SQL Server.
 * Consideraciones: Mantiene la lógica mínima necesaria; no duplica cálculos que pertenecen al Stored Procedure ni contiene acceso SQL.
 */

using SistemaTicketsInteligente.Api.DAO;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.BLL;

public sealed class InicioUsuarioBLL
{
    private readonly InicioUsuarioDAO inicioUsuarioDAO;

    public InicioUsuarioBLL(InicioUsuarioDAO inicioUsuarioDAO)
    {
        this.inicioUsuarioDAO = inicioUsuarioDAO;
    }

    public Task<InicioUsuarioRespuesta> ObtenerAsync(string usuario, CancellationToken cancellationToken = default)
    {
        var usuarioNormalizado = usuario.Trim();
        if (usuarioNormalizado.Length == 0 || usuarioNormalizado.Length > 20) throw new ArgumentException("El usuario autenticado no es válido.", nameof(usuario));

        return inicioUsuarioDAO.ObtenerAsync(usuarioNormalizado, cancellationToken);
    }
}
