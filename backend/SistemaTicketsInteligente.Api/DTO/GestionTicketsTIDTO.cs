/**
 * Archivo: GestionTicketsTIDTO.cs
 * Objetivo: Definir los contratos utilizados por el módulo Gestión de Tickets para operadores TI.
 * Responsabilidad: Transportar resumen, bandeja, catálogos, detalle técnico y solicitudes de acciones operativas entre Controller, BLL y frontend.
 * Dependencias: Ninguna dependencia funcional fuera de .NET.
 * Flujo: Frontend <-> GestionTicketsTIController <-> GestionTicketsTIBLL <-> SQL Server.
 * Consideraciones: No contiene reglas de negocio ni acceso a datos; la identidad, área y perfil del operador se obtienen exclusivamente desde la sesión autenticada.
 */

namespace SistemaTicketsInteligente.Api.DTO;

public sealed class GestionTicketsTIRespuesta
{
    public GestionTicketsTIResumen Resumen { get; set; } = new();
    public List<GestionTicketsTIItem> Tickets { get; set; } = [];
    public GestionTicketsTICatalogos Catalogos { get; set; } = new();
}

public sealed class GestionTicketsTIResumen
{
    public int Pendientes { get; set; }
    public int EnAtencion { get; set; }
    public int PorVencer { get; set; }
    public int Reabiertos { get; set; }
    public int SinAsignar { get; set; }
    public int MisAsignados { get; set; }
    public int PrioridadAlta { get; set; }
    public int AprobacionesPendientes { get; set; }
    public int Total { get; set; }
}

public sealed class GestionTicketsTIItem
{
    public string IncidenciaNumero { get; set; } = string.Empty;
    public string UsuarioSolicitante { get; set; } = string.Empty;
    public string Solicitante { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
    public string AreaSolicitante { get; set; } = string.Empty;
    public string AreaDescripcion { get; set; } = string.Empty;
    public string Linea { get; set; } = string.Empty;
    public string LineaDescripcion { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string TipoDescripcion { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string EstadoDescripcion { get; set; } = string.Empty;
    public int? Prioridad { get; set; }
    public int? Impacto { get; set; }
    public int? Complejidad { get; set; }
    public string UsuarioTI { get; set; } = string.Empty;
    public string Responsable { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; }
    public DateTime UltimaFechaModif { get; set; }
    public int? SlaMinutosRestantes { get; set; }
    public bool TieneAprobacionPendiente { get; set; }
    public string Accion { get; set; } = string.Empty;
}

public sealed class GestionTicketsTICatalogos
{
    public List<GestionTicketsTICatalogoItem> Estados { get; set; } = [];
    public List<GestionTicketsTICatalogoItem> Areas { get; set; } = [];
    public List<GestionTicketsTICatalogoItem> Operadores { get; set; } = [];
    public List<GestionTicketsTILinea> Lineas { get; set; } = [];
    public List<GestionTicketsTIItemCatalogo> Items { get; set; } = [];
    public List<GestionTicketsTICatalogoItem> Tipos { get; set; } = [];
    public List<GestionTicketsTICatalogoItem> Categorias { get; set; } = [];
    public List<GestionTicketsTISubTipo> SubTipos { get; set; } = [];
}

public class GestionTicketsTICatalogoItem
{
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
}

public sealed class GestionTicketsTILinea : GestionTicketsTICatalogoItem
{
    public string Area { get; set; } = string.Empty;
}

public sealed class GestionTicketsTIItemCatalogo : GestionTicketsTICatalogoItem
{
    public string Linea { get; set; } = string.Empty;
}

public sealed class GestionTicketsTISubTipo : GestionTicketsTICatalogoItem
{
    public string Tipo { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
}

public sealed class GestionTicketTIDetalle
{
    public string IncidenciaNumero { get; set; } = string.Empty;
    public string UsuarioSolicitante { get; set; } = string.Empty;
    public string Solicitante { get; set; } = string.Empty;
    public string CorreoSolicitante { get; set; } = string.Empty;
    public string AreaSolicitante { get; set; } = string.Empty;
    public string AreaSolicitanteDescripcion { get; set; } = string.Empty;
    public string AreaTI { get; set; } = string.Empty;
    public string AreaTIDescripcion { get; set; } = string.Empty;
    public string UsuarioTI { get; set; } = string.Empty;
    public string Responsable { get; set; } = string.Empty;
    public string UsuarioAsigno { get; set; } = string.Empty;
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
    public string Estado { get; set; } = string.Empty;
    public string EstadoDescripcion { get; set; } = string.Empty;
    public string AreaCausante { get; set; } = string.Empty;
    public string AreaCausanteDescripcion { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
    public string MensajeError { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; }
    public DateTime? FechaAsignacion { get; set; }
    public DateTime? FechaAtencion { get; set; }
    public DateTime? FechaCierre { get; set; }
    public int? SlaObjetivoMinutos { get; set; }
    public int? Prioridad { get; set; }
    public int? Impacto { get; set; }
    public int? Complejidad { get; set; }
    public string CanalRegistro { get; set; } = string.Empty;
    public string CausaRaiz { get; set; } = string.Empty;
    public string SolucionTecnica { get; set; } = string.Empty;
    public string RespuestaUsuario { get; set; } = string.Empty;
    public string TipoResolucion { get; set; } = string.Empty;
    public byte? Calificacion { get; set; }
    public string ComentarioCalificacion { get; set; } = string.Empty;
    public DateTime UltimaFechaModif { get; set; }
    public int? SlaMinutosRestantes { get; set; }
    public List<GestionTicketTIEstado> HistorialEstados { get; set; } = [];
    public List<GestionTicketTIAvance> Avances { get; set; } = [];
    public List<GestionTicketTIMensaje> Mensajes { get; set; } = [];
    public List<GestionTicketTIAdjunto> Adjuntos { get; set; } = [];
    public List<GestionTicketTIDocumento> Documentos { get; set; } = [];
    public List<GestionTicketTIAprobacion> Aprobaciones { get; set; } = [];
}

public sealed class GestionTicketTIEstado
{
    public int Secuencia { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string EstadoDescripcion { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public DateTime FechaCambio { get; set; }
    public string Observacion { get; set; } = string.Empty;
}

public sealed class GestionTicketTIAvance
{
    public int Secuencia { get; set; }
    public string UsuarioTI { get; set; } = string.Empty;
    public string Responsable { get; set; } = string.Empty;
    public DateTime FechaAvance { get; set; }
    public string Detalle { get; set; } = string.Empty;
    public decimal? TiempoUtilizado { get; set; }
    public decimal? PorcentajeAvance { get; set; }
}

public sealed class GestionTicketTIMensaje
{
    public int Secuencia { get; set; }
    public string TipoAutor { get; set; } = string.Empty;
    public string Autor { get; set; } = string.Empty;
    public string Contenido { get; set; } = string.Empty;
    public DateTime FechaMensaje { get; set; }
    public bool EsInterno { get; set; }
}

public sealed class GestionTicketTIAdjunto
{
    public int Secuencia { get; set; }
    public int? MensajeSecuencia { get; set; }
    public string NombreOriginal { get; set; } = string.Empty;
    public string TipoMime { get; set; } = string.Empty;
    public long TamanoBytes { get; set; }
    public DateTime FechaRegistro { get; set; }
    public string UsuarioRegistro { get; set; } = string.Empty;
}

public sealed class GestionTicketTIDocumento
{
    public int Secuencia { get; set; }
    public string CompaniaSocio { get; set; } = string.Empty;
    public string TipoDocumento { get; set; } = string.Empty;
    public string NumeroDocumento { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
}

public sealed class GestionTicketTIAprobacion
{
    public int Secuencia { get; set; }
    public string AccionCodigo { get; set; } = string.Empty;
    public string AccionNombre { get; set; } = string.Empty;
    public string NivelRiesgo { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string Justificacion { get; set; } = string.Empty;
    public string ComentarioRespuesta { get; set; } = string.Empty;
    public string UsuarioSolicitante { get; set; } = string.Empty;
    public string Solicitante { get; set; } = string.Empty;
    public string UsuarioAprobador { get; set; } = string.Empty;
    public string Aprobador { get; set; } = string.Empty;
    public DateTime FechaSolicitud { get; set; }
    public DateTime? FechaRespuesta { get; set; }
    public string ParametrosJson { get; set; } = string.Empty;
}

public sealed class ClasificarTicketTISolicitud
{
    public string Linea { get; set; } = string.Empty;
    public string Item { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string SubTipo { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string? AreaCausante { get; set; }
    public int? Prioridad { get; set; }
    public int? Impacto { get; set; }
    public int? Complejidad { get; set; }
}

public sealed class AsignarTicketTISolicitud
{
    public string UsuarioTI { get; set; } = string.Empty;
}

public sealed class SolicitarInformacionTISolicitud
{
    public string Mensaje { get; set; } = string.Empty;
}

public sealed class ResolverTicketTISolicitud
{
    public string CausaRaiz { get; set; } = string.Empty;
    public string Solucion { get; set; } = string.Empty;
    public string RespuestaUsuario { get; set; } = string.Empty;
    public string TipoResolucion { get; set; } = string.Empty;
}

public sealed class NoProcedeTicketTISolicitud
{
    public string Motivo { get; set; } = string.Empty;
}

public sealed class ResponderAprobacionTISolicitud
{
    public bool Aprobar { get; set; }
    public string? Comentario { get; set; }
}

/// <summary>Clasificación del historial: propuesta de la IA (Origen I) o aplicada por TI con la matriz (Origen T).</summary>
public sealed class ClasificacionTicketTI
{
    public int Secuencia { get; set; }
    public string Origen { get; set; } = string.Empty;
    public string Linea { get; set; } = string.Empty;
    public string LineaDescripcion { get; set; } = string.Empty;
    public string Item { get; set; } = string.Empty;
    public string ItemDescripcion { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string SubTipo { get; set; } = string.Empty;
    public string SubTipoDescripcion { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public int? Prioridad { get; set; }
    public int? Impacto { get; set; }
    public int? Complejidad { get; set; }
    public decimal? Confianza { get; set; }
    public List<string> Senales { get; set; } = [];
    public string Justificacion { get; set; } = string.Empty;
    public List<string> PreguntasPendientes { get; set; } = [];
    public string Modelo { get; set; } = string.Empty;
    public string Usuario { get; set; } = string.Empty;
    public string NombreUsuario { get; set; } = string.Empty;
    public DateTime FechaClasificacion { get; set; }
}
