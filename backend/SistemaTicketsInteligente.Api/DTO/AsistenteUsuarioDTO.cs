/**
 * Archivo: AsistenteUsuarioDTO.cs
 * Objetivo: Definir los contratos del Asistente TI para colaboradores.
 * Responsabilidad: Transportar mensajes, historial breve, fuentes autorizadas y el resultado de orientación sin persistir conversaciones.
 * Dependencias: Ninguna capa de infraestructura.
 * Flujo: Frontend -> AsistenteUsuarioController -> AsistenteUsuarioBLL -> proveedor IA opcional.
 * Consideraciones: La identidad se obtiene de la sesión; el cliente nunca indica qué usuario o tickets puede consultar.
 */

namespace SistemaTicketsInteligente.Api.DTO;

public sealed class AsistenteUsuarioSolicitud
{
    public string Mensaje { get; set; } = string.Empty;
    public List<AsistenteUsuarioMensaje> Historial { get; set; } = [];
}

public sealed class AsistenteUsuarioMensaje
{
    public string Rol { get; set; } = string.Empty;
    public string Contenido { get; set; } = string.Empty;
}

public sealed class AsistenteUsuarioFuente
{
    public string Tipo { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Resumen { get; set; } = string.Empty;
}

public sealed class AsistenteUsuarioRespuesta
{
    public string Respuesta { get; set; } = string.Empty;
    public string Modo { get; set; } = "CONOCIMIENTO";
    public bool EscalarATicket { get; set; }
    public List<AsistenteUsuarioFuente> Fuentes { get; set; } = [];
    public List<string> Sugerencias { get; set; } = [];
    public AsistenteUsuarioAccion? Accion { get; set; }
}

public sealed class AsistenteUsuarioAccion
{
    public string Tipo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
}
