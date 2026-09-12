using System.ComponentModel.DataAnnotations;

namespace SistemaTicketsInteligente.Entidades.ComponenteDTO.Autenticacion;

public sealed class SolicitudInicioSesion
{
    [Required]
    [StringLength(20)]
    public string Usuario { get; init; } = string.Empty;

    [Required]
    public string Contrasena { get; init; } = string.Empty;
}
