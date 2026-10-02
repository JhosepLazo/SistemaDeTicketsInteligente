/**
 * Archivo: AsistenteTIDTO.cs
 * Objetivo: Definir los contratos del Asistente TI y del Agente de Ingeniería Autónomo.
 * Responsabilidad: Transportar conversación, sesiones Live, evidencia, diagnóstico, informe técnico y decisiones controladas de TI.
 * Dependencias: AsistenteUsuarioMensaje para conservar el historial conversacional existente.
 * Flujo: Frontend <-> AsistenteTIController <-> AsistenteTIBLL <-> AsistenteTIDAO.
 * Consideraciones: El archivo no contiene reglas de negocio ni permite transportar SQL libre para ejecución.
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

public sealed class CrearInvestigacionTISolicitud
{
    public string? IncidenciaNumero { get; set; }
    public string Descripcion { get; set; } = string.Empty;
}

public sealed class RegistrarEventoAgenteTISolicitud
{
    public string Tipo { get; set; } = string.Empty;
    public string Fuente { get; set; } = string.Empty;
    public string Contenido { get; set; } = string.Empty;
    public string? DatosJson { get; set; }
}

public sealed class FinalizarObservacionAgenteTISolicitud
{
    public string ResumenObservacion { get; set; } = string.Empty;
    public string ProcesoObservado { get; set; } = string.Empty;
    public string? ErrorObservado { get; set; }
}

public sealed class RealizarCambioAgenteTISolicitud
{
    public bool Confirmar { get; set; }
}

public sealed class AgenteTISesion
{
    public long SesionNumero { get; set; }
    public string IncidenciaNumero { get; set; } = string.Empty;
    public Guid IdCorrelacion { get; set; }
    public string DescripcionInicial { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string ResumenObservacion { get; set; } = string.Empty;
    public string ProcesoObservado { get; set; } = string.Empty;
    public string ErrorObservado { get; set; } = string.Empty;
    public string Diagnostico { get; set; } = string.Empty;
    public string CausaProbable { get; set; } = string.Empty;
    public string SolucionPropuesta { get; set; } = string.Empty;
    public decimal? Confianza { get; set; }
    public string AccionCodigo { get; set; } = string.Empty;
    public string NivelRiesgo { get; set; } = string.Empty;
    public string ParametrosJson { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public int? SolicitudAprobacionSecuencia { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime? FechaDiagnostico { get; set; }
    public DateTime? FechaDecision { get; set; }
    public bool InformeDisponible { get; set; }
}

public sealed class AgenteTITicketContexto
{
    public string IncidenciaNumero { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
    public string MensajeError { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string Linea { get; set; } = string.Empty;
    public string Item { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string SubTipo { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string UsuarioSolicitante { get; set; } = string.Empty;
    public DateTime? FechaRegistro { get; set; }
}

public sealed class AgenteTIDocumento
{
    public string CompaniaSocio { get; set; } = string.Empty;
    public string TipoDocumento { get; set; } = string.Empty;
    public string NumeroDocumento { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
}

public sealed class AgenteTIMensaje
{
    public string TipoAutor { get; set; } = string.Empty;
    public string Autor { get; set; } = string.Empty;
    public string Contenido { get; set; } = string.Empty;
    public DateTime FechaMensaje { get; set; }
    public bool EsInterno { get; set; }
}

public sealed class AgenteTIConocimiento
{
    public string ConocimientoCodigo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Problema { get; set; } = string.Empty;
    public string Sintomas { get; set; } = string.Empty;
    public string Causa { get; set; } = string.Empty;
    public string Solucion { get; set; } = string.Empty;
    public string Procedimiento { get; set; } = string.Empty;
    public int PuntajeContextual { get; set; }
}

public sealed class AgenteTIAccionDisponible
{
    public string AccionCodigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string NivelRiesgo { get; set; } = string.Empty;
    public bool RequiereAprobacion { get; set; }
}

public sealed class AgenteTIAuditoria
{
    public string Entidad { get; set; } = string.Empty;
    public string Registro { get; set; } = string.Empty;
    public string Evento { get; set; } = string.Empty;
    public string Resultado { get; set; } = string.Empty;
    public string DetalleJson { get; set; } = string.Empty;
    public Guid IdCorrelacion { get; set; }
    public DateTime Fecha { get; set; }
}

public sealed class AgenteTIEvento
{
    public int Secuencia { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Fuente { get; set; } = string.Empty;
    public string Contenido { get; set; } = string.Empty;
    public string DatosJson { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
}

public sealed class AgenteTIContextoInvestigacion
{
    public AgenteTISesion Sesion { get; set; } = new();
    public AgenteTITicketContexto Ticket { get; set; } = new();
    public List<AgenteTIDocumento> Documentos { get; set; } = [];
    public List<AgenteTIMensaje> Mensajes { get; set; } = [];
    public List<AgenteTIConocimiento> Conocimientos { get; set; } = [];
    public List<AgenteTIAccionDisponible> Acciones { get; set; } = [];
    public List<AgenteTIAuditoria> Auditoria { get; set; } = [];
    public List<AgenteTIEvento> Eventos { get; set; } = [];
}

public sealed class AgenteTIEvidencia
{
    public string TipoFuente { get; set; } = string.Empty;
    public string Referencia { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal? Similitud { get; set; }
}

public sealed class AgenteTIAccionPropuesta
{
    public string AccionCodigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string NivelRiesgo { get; set; } = string.Empty;
    public bool RequiereAprobacion { get; set; }
    public string ParametrosJson { get; set; } = string.Empty;
}

public sealed class AgenteTIDiagnosticoRespuesta
{
    public long SesionNumero { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string Diagnostico { get; set; } = string.Empty;
    public string CausaProbable { get; set; } = string.Empty;
    public string SolucionPropuesta { get; set; } = string.Empty;
    public decimal Confianza { get; set; }
    public List<AgenteTIEvidencia> Evidencias { get; set; } = [];
    public List<string> TrazaTecnica { get; set; } = [];
    public AgenteTIAccionPropuesta? Accion { get; set; }
    public bool InformeDisponible { get; set; }
    public string Limitacion { get; set; } = string.Empty;
}

public sealed class AgenteTILiveTokenRespuesta
{
    public bool Disponible { get; set; }
    public string Token { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public string WebSocketUrl { get; set; } = string.Empty;
    public string InstruccionSistema { get; set; } = string.Empty;
    public DateTimeOffset? ExpiraEn { get; set; }
    public string Mensaje { get; set; } = string.Empty;
}

public sealed class AgenteTIPreparacionCambio
{
    public string Estado { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public bool PuedeEjecutar { get; set; }
    public string ProcedimientoEjecutor { get; set; } = string.Empty;
    public int? EjecucionSecuencia { get; set; }
    public int? SolicitudAprobacionSecuencia { get; set; }
    public string ParametrosJson { get; set; } = string.Empty;
}

public sealed class AgenteTIEjecucionResultado
{
    public string ResultadoJson { get; set; } = string.Empty;
    public int FilasAfectadas { get; set; }
}

public sealed class AgenteTIDecisionRespuesta
{
    public long SesionNumero { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public int? SolicitudAprobacionSecuencia { get; set; }
    public bool Ejecutado { get; set; }
}
