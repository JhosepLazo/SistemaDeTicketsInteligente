/**
 * Contratos del Asistente TI para consultas operativas y acciones confirmables.
 */

namespace SistemaTicketsInteligente.Api.DTO;

public sealed class AsistenteTISolicitud
{
    public string Mensaje { get; set; } = string.Empty;
    public List<AsistenteUsuarioMensaje> Historial { get; set; } = [];
}

public sealed class AsistenteTIUsuarioPropuesto
{
    public string Usuario { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string AreaDescripcion { get; set; } = string.Empty;
    public string Perfil { get; set; } = string.Empty;
    public string PerfilDescripcion { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Estado { get; set; } = "A";
    public bool YaExiste { get; set; }
}

public sealed class AsistenteTIAccion
{
    public string Tipo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string TokenConfirmacion { get; set; } = string.Empty;
    public DateTimeOffset? ExpiraEn { get; set; }
    public List<string> Faltantes { get; set; } = [];
    public AsistenteTIUsuarioPropuesto? Usuario { get; set; }
}

public sealed class AsistenteTIRespuesta
{
    public string Respuesta { get; set; } = string.Empty;
    public string Modo { get; set; } = "CONOCIMIENTO";
    public List<string> Fuentes { get; set; } = [];
    public List<string> Sugerencias { get; set; } = [];
    public AsistenteTIAccion? Accion { get; set; }
}

public sealed class ConfirmarAccionTISolicitud
{
    public string TokenConfirmacion { get; set; } = string.Empty;
}

public sealed class ConfirmarAccionTIRespuesta
{
    public string Mensaje { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Registro { get; set; } = string.Empty;
}
