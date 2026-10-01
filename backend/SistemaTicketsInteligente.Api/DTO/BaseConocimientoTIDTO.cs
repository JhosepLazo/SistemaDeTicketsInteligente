/**
 * Archivo: BaseConocimientoTIDTO.cs
 * Objetivo: Definir los contratos utilizados por el módulo Base de Conocimiento del operador TI.
 * Responsabilidad: Transportar resumen, artículos, catálogos, tickets candidatos y solicitudes de mantenimiento sin incorporar acceso a datos ni reglas de negocio.
 * Dependencias: Ninguna capa de infraestructura; solo tipos base de .NET.
 * Flujo: Controller <-> BLL <-> DAO <-> Stored Procedures del módulo Base de Conocimiento TI.
 * Consideraciones: Los estados manejados por el módulo son A Publicado, B Borrador, P Pendiente de validación e I Inactivo.
 */

namespace SistemaTicketsInteligente.Api.DTO;

public sealed class BaseConocimientoTIResumen
{
    public int Total { get; set; }
    public int Activos { get; set; }
    public int Borradores { get; set; }
    public int PendientesValidacion { get; set; }
    public int PorRevisar { get; set; }
    public int CandidatosDesdeTickets { get; set; }
}

public class BaseConocimientoTIItem
{
    public string ConocimientoCodigo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Problema { get; set; } = string.Empty;
    public string Solucion { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string EstadoDescripcion { get; set; } = string.Empty;
    public string Linea { get; set; } = string.Empty;
    public string LineaDescripcion { get; set; } = string.Empty;
    public string Item { get; set; } = string.Empty;
    public string ItemDescripcion { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string TipoDescripcion { get; set; } = string.Empty;
    public string SubTipo { get; set; } = string.Empty;
    public string SubTipoDescripcion { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string CategoriaDescripcion { get; set; } = string.Empty;
    public string IncidenciaOrigen { get; set; } = string.Empty;
    public string Validador { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaValidacion { get; set; }
    public DateTime? FechaRevision { get; set; }
    public bool RequiereRevision { get; set; }
}

public sealed class BaseConocimientoTIDetalle : BaseConocimientoTIItem
{
    public string Sintomas { get; set; } = string.Empty;
    public string MensajeError { get; set; } = string.Empty;
    public string Causa { get; set; } = string.Empty;
    public string Procedimiento { get; set; } = string.Empty;
    public string UsuarioValida { get; set; } = string.Empty;
}

public class BaseConocimientoTICatalogo
{
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
}

public sealed class BaseConocimientoTIItemCatalogo : BaseConocimientoTICatalogo
{
    public string Linea { get; set; } = string.Empty;
}

public sealed class BaseConocimientoTISubTipoCatalogo : BaseConocimientoTICatalogo
{
    public string Tipo { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
}

public sealed class BaseConocimientoTITicketOrigen
{
    public string IncidenciaNumero { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
    public string MensajeError { get; set; } = string.Empty;
    public string Linea { get; set; } = string.Empty;
    public string Item { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string SubTipo { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string CausaRaiz { get; set; } = string.Empty;
    public string SolucionTecnica { get; set; } = string.Empty;
    public DateTime? FechaCierre { get; set; }
}

public sealed class BaseConocimientoTICatalogos
{
    public List<BaseConocimientoTICatalogo> Lineas { get; set; } = [];
    public List<BaseConocimientoTIItemCatalogo> Items { get; set; } = [];
    public List<BaseConocimientoTICatalogo> Tipos { get; set; } = [];
    public List<BaseConocimientoTICatalogo> Categorias { get; set; } = [];
    public List<BaseConocimientoTISubTipoCatalogo> SubTipos { get; set; } = [];
    public List<BaseConocimientoTITicketOrigen> TicketsOrigen { get; set; } = [];
}

public sealed class BaseConocimientoTIRespuesta
{
    public BaseConocimientoTIResumen Resumen { get; set; } = new();
    public List<BaseConocimientoTIItem> Articulos { get; set; } = [];
    public BaseConocimientoTICatalogos Catalogos { get; set; } = new();
}

public sealed class GuardarBaseConocimientoTISolicitud
{
    public string Titulo { get; set; } = string.Empty;
    public string Problema { get; set; } = string.Empty;
    public string Sintomas { get; set; } = string.Empty;
    public string? MensajeError { get; set; }
    public string Causa { get; set; } = string.Empty;
    public string Solucion { get; set; } = string.Empty;
    public string? Procedimiento { get; set; }
    public string Linea { get; set; } = string.Empty;
    public string Item { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string SubTipo { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string? IncidenciaOrigen { get; set; }
}

public sealed class BaseConocimientoTICreadoRespuesta
{
    public string ConocimientoCodigo { get; set; } = string.Empty;
}
