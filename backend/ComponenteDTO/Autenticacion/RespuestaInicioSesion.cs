namespace SistemaTicketsInteligente.Entidades.ComponenteDTO.Autenticacion;

public sealed class RespuestaInicioSesion
{
    public bool Autenticado { get; init; }
    public string? Usuario { get; init; }
    public string? NombreCompleto { get; init; }
    public string? Area { get; init; }
    public string? Perfil { get; init; }
    public string? Mensaje { get; init; }
}
