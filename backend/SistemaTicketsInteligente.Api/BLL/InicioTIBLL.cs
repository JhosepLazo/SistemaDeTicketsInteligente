/**
 * Archivo: InicioTIBLL.cs
 * Objetivo: Coordinar la obtención del Inicio del operador TI autenticado.
 * Responsabilidad: Validar identidad y área recibidas desde la sesión antes de delegar la consulta consolidada al DAO.
 * Dependencias: InicioTIDAO e InicioTIDTO.
 * Flujo: InicioTIController -> InicioTIBLL -> InicioTIDAO -> SQL Server.
 * Consideraciones: Mantiene únicamente validaciones mínimas; no duplica cálculos del Stored Procedure ni contiene acceso SQL.
 */

using SistemaTicketsInteligente.Api.DAO;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.BLL;

public sealed class InicioTIBLL
{
    private readonly InicioTIDAO inicioTIDAO;

    public InicioTIBLL(InicioTIDAO inicioTIDAO)
    {
        this.inicioTIDAO = inicioTIDAO;
    }

    public Task<InicioTIRespuesta> ObtenerAsync(string usuario, string area, CancellationToken cancellationToken = default)
    {
        var usuarioNormalizado = usuario.Trim();
        var areaNormalizada = area.Trim();

        if (usuarioNormalizado.Length == 0 || usuarioNormalizado.Length > 20) throw new ArgumentException("El usuario autenticado no es válido.", nameof(usuario));
        if (areaNormalizada.Length != 3) throw new ArgumentException("El área del operador TI no es válida.", nameof(area));

        return inicioTIDAO.ObtenerAsync(usuarioNormalizado, areaNormalizada, cancellationToken);
    }
}
