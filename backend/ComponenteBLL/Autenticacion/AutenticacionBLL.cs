using SistemaTicketsInteligente.Datos.ComponenteDAO.Autenticacion;
using SistemaTicketsInteligente.Entidades.ComponenteDTO.Autenticacion;

namespace SistemaTicketsInteligente.Negocio.ComponenteBLL.Autenticacion;

public sealed class AutenticacionBLL
{
    private readonly AutenticacionDAO autenticacionDAO;

    public AutenticacionBLL(AutenticacionDAO autenticacionDAO)
    {
        this.autenticacionDAO = autenticacionDAO;
    }

    public Task<UsuarioAutenticacion?> BuscarUsuarioAsync(
        string usuario,
        CancellationToken cancellationToken = default)
    {
        return autenticacionDAO.BuscarUsuarioAsync(usuario.Trim(), cancellationToken);
    }
}
