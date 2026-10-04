/**
 * Archivo: ConfiguracionTIDTO.cs
 * Objetivo: Definir los contratos del módulo restringido Configuración TI.
 * Responsabilidad: Transportar catálogos, matriz de clasificación, SLA, usuarios, formatos y visibilidad de conocimiento entre Controller, BLL y frontend.
 * Dependencias: ASP.NET Core IFormFile únicamente para la carga controlada de formatos.
 * Flujo: Frontend <-> ConfiguracionTIController <-> ConfiguracionTIBLL <-> SQL Server.
 * Consideraciones: No contiene lógica de IA; los registros se activan/inactivan para conservar trazabilidad histórica.
 */

using Microsoft.AspNetCore.Http;

namespace SistemaTicketsInteligente.Api.DTO;

public sealed class ConfiguracionTIRespuesta
{
    public List<ConfiguracionTIArea> Areas { get; set; } = [];
    public List<ConfiguracionTILinea> Lineas { get; set; } = [];
    public List<ConfiguracionTIItem> Items { get; set; } = [];
    public List<ConfiguracionTITipo> Tipos { get; set; } = [];
    public List<ConfiguracionTICategoria> Categorias { get; set; } = [];
    public List<ConfiguracionTISubTipo> SubTipos { get; set; } = [];
    public List<ConfiguracionTIMatriz> Matriz { get; set; } = [];
    public List<ConfiguracionTISla> Sla { get; set; } = [];
    public List<ConfiguracionTIUsuario> Usuarios { get; set; } = [];
    public List<ConfiguracionTIFormato> Formatos { get; set; } = [];
    public List<ConfiguracionTIConocimiento> Conocimientos { get; set; } = [];
}

public sealed class ConfiguracionTIArea
{
    public string Area { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
}

public sealed class ConfiguracionTILinea
{
    public string Linea { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
}

public sealed class ConfiguracionTIItem
{
    public string Item { get; set; } = string.Empty;
    public string Linea { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
}

public sealed class ConfiguracionTITipo
{
    public string Tipo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Abreviatura { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
}

public sealed class ConfiguracionTICategoria
{
    public string Categoria { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Abreviatura { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
}

public sealed class ConfiguracionTISubTipo
{
    public string Tipo { get; set; } = string.Empty;
    public string SubTipo { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Abreviatura { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
}

public sealed class ConfiguracionTIMatriz
{
    public string Item { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public int? Prioridad { get; set; }
    public int? Impacto { get; set; }
    public int? Complejidad { get; set; }
    public string Estado { get; set; } = string.Empty;
}

public sealed class ConfiguracionTISla
{
    public int Prioridad { get; set; }
    public int SlaObjetivoMinutos { get; set; }
    public string Estado { get; set; } = string.Empty;
}

public sealed class ConfiguracionTIUsuario
{
    public string Usuario { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Cargo { get; set; } = string.Empty;
    public string Perfil { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string FuenteIdentidad { get; set; } = string.Empty;
    public string Documento { get; set; } = string.Empty;
    public string EstadoCorporativo { get; set; } = string.Empty;
    public DateTime? UltimaSincronizacion { get; set; }
}

public sealed class ConfiguracionTIFormato
{
    public string FormatoCodigo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string NombreOriginal { get; set; } = string.Empty;
    public string TipoMime { get; set; } = string.Empty;
    public string TipoTicket { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
}

public sealed class ConfiguracionTIConocimiento
{
    public string ConocimientoCodigo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public bool VisibleUsuario { get; set; }
}

public sealed class GuardarAreaTISolicitud
{
    public string Area { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string Estado { get; set; } = "A";
}

public sealed class GuardarLineaTISolicitud
{
    public string Linea { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Estado { get; set; } = "A";
}

public sealed class GuardarItemTISolicitud
{
    public string Item { get; set; } = string.Empty;
    public string Linea { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Estado { get; set; } = "A";
}

public sealed class GuardarTipoTISolicitud
{
    public string Tipo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string? Abreviatura { get; set; }
    public string Estado { get; set; } = "A";
}

public sealed class GuardarCategoriaTISolicitud
{
    public string Categoria { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string? Abreviatura { get; set; }
    public string Estado { get; set; } = "A";
}

public sealed class GuardarSubTipoTISolicitud
{
    public string Tipo { get; set; } = string.Empty;
    public string SubTipo { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string? Abreviatura { get; set; }
    public string Estado { get; set; } = "A";
}

public sealed class GuardarMatrizTISolicitud
{
    public string Item { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public int Prioridad { get; set; }
    public int Impacto { get; set; }
    public int Complejidad { get; set; }
    public string Estado { get; set; } = "A";
}

public sealed class GuardarSlaTISolicitud
{
    public int Prioridad { get; set; }
    public int SlaObjetivoMinutos { get; set; }
    public string Estado { get; set; } = "A";
}

public sealed class SincronizarUsuarioCorporativoSolicitud
{
    public string Usuario { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Perfil { get; set; } = "USR";
    public string? Correo { get; set; }
    public string Estado { get; set; } = "A";
}

public sealed class GuardarFormatoSoporteSolicitud
{
    public string FormatoCodigo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? TipoTicket { get; set; }
    public string Estado { get; set; } = "A";
    public IFormFile? Archivo { get; set; }
}

public sealed class VisibilidadConocimientoSolicitud
{
    public bool VisibleUsuario { get; set; }
}
