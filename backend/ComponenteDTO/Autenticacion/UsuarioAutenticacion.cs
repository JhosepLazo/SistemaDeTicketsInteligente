namespace SistemaTicketsInteligente.Entidades.ComponenteDTO.Autenticacion;

/// <summary>
/// Modelo interno de autenticación. Nunca debe devolverse al frontend porque contiene el hash de la clave.
/// </summary>
public sealed class UsuarioAutenticacion
{
    public string Usuario { get; init; } = string.Empty;
    public string NombreCompleto { get; init; } = string.Empty;
    public string ClaveHash { get; init; } = string.Empty;
    public string Area { get; init; } = string.Empty;
    public string Perfil { get; init; } = string.Empty;
    public string EstadoUsuario { get; init; } = string.Empty;
    public string EstadoPerfil { get; init; } = string.Empty;
}
