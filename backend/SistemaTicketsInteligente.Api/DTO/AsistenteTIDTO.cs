/**
 * Archivo: AsistenteTIDTO.cs
 * Objetivo: Definir los contratos del Asistente TI y del Agente de Ingeniería Autónomo.
 * Responsabilidad: Transportar conversación, sesiones Live, evidencia, diagnóstico, informe técnico y decisiones controladas de TI.
 * Dependencias: AsistenteUsuarioMensaje para conservar el historial conversacional existente.
 * Flujo: Frontend <-> AsistenteTIController <-> AsistenteTIBLL <-> Stored Procedures del agente.
 * Consideraciones: El archivo no contiene reglas de negocio ni permite transportar SQL libre para ejecución.
 */

using System.Text.Json.Serialization;

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
    /// <summary>Investigación creada desde la conversación (Tipo = INVESTIGACION).</summary>
    public long? SesionNumero { get; set; }
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

/// <summary>Grabación de pantalla de una observación (multipart/form-data).</summary>
public sealed class SubirGrabacionSolicitud
{
    public IFormFile? Archivo { get; set; }
    public int? DuracionSegundos { get; set; }
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
    public bool SolucionValidada { get; set; }
    public string ConocimientoCodigo { get; set; } = string.Empty;
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
    public string UsuarioTI { get; set; } = string.Empty;
    public string NombreOperador { get; set; } = string.Empty;
    public bool EsPropietario { get; set; }
    /// <summary>Quien consulta puede tomar la investigación (supervisión o responsable TI del ticket).</summary>
    public bool PuedeTomar { get; set; }
    /// <summary>Responsable TI asignado al ticket investigado.</summary>
    public string UsuarioTITicket { get; set; } = string.Empty;
    public string UsuarioInvitado { get; set; } = string.Empty;
    public string NombreInvitado { get; set; } = string.Empty;
    public string EstadoInvitacion { get; set; } = string.Empty;
    public DateTime? InvitacionExpira { get; set; }
    [JsonIgnore] public string EvidenciasJson { get; set; } = string.Empty;
    [JsonIgnore] public string InformeMarkdown { get; set; } = string.Empty;
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
    /// <summary>Pasos de diagnóstico validados por TI (JSON: paso, herramienta, confirma, descarta); vacío si el artículo no tiene guía.</summary>
    public string GuiaDiagnosticoJson { get; set; } = string.Empty;
}

public sealed class AgenteTIAccionDisponible
{
    public string AccionCodigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string NivelRiesgo { get; set; } = string.Empty;
    public bool RequiereAprobacion { get; set; }
    public bool TieneEjecutor { get; set; }
    public string ParametrosDescripcion { get; set; } = string.Empty;
    public bool Reversible { get; set; }
    /// <summary>JSON Schema de los parámetros del ejecutor; el backend valida contra él lo que propone el modelo.</summary>
    public string ParametrosEsquemaJson { get; set; } = string.Empty;
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
    public bool OrigenServidor { get; set; }
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
    public bool Reversible { get; set; }
}

/// <summary>Causa que el agente consideró y descartó, con el motivo (parte del paquete que TI revisa antes de aprobar).</summary>
public sealed class AgenteTIAlternativa
{
    public string Causa { get; set; } = string.Empty;
    public string Motivo { get; set; } = string.Empty;
}

public sealed class AgenteTIDiagnosticoRespuesta
{
    public List<AgenteTICodigoReferencia> Codigo { get; set; } = [];
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
    public string InformeMarkdown { get; set; } = string.Empty;
    public string Limitacion { get; set; } = string.Empty;
    /// <summary>AGENTE (bucle con herramientas), IA (una sola llamada) o SIN_MODELO.</summary>
    public string Modo { get; set; } = string.Empty;
    public List<AgenteTIPasoInvestigacion> Pasos { get; set; } = [];
    public List<AgenteTIHallazgo> Hallazgos { get; set; } = [];
    /// <summary>Datos sensibles ocultados antes de enviar el contexto al proveedor de IA.</summary>
    public int DatosOcultados { get; set; }
    /// <summary>
    /// ALTA: un hallazgo verificado de una prueba determinista (herramienta, base, código, telemetría o auditoría) sostiene la causa.
    /// MEDIA: solo la sostiene conocimiento validado. BAJA: es una hipótesis del modelo y no se propone ninguna acción.
    /// </summary>
    public string NivelEvidencia { get; set; } = string.Empty;
    public List<AgenteTIAlternativa> AlternativasDescartadas { get; set; } = [];
}

/// <summary>Herramienta diagnóstica de solo lectura del catálogo TI_AgenteHerramienta.</summary>
public sealed class AgenteTIHerramienta
{
    public string HerramientaCodigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Procedimiento { get; set; } = string.Empty;
    public string ParametrosEsquemaJson { get; set; } = string.Empty;
    public bool Automatica { get; set; }
    public bool RequiereTicket { get; set; }
    public int MaximoFilas { get; set; }
    /// <summary>SP (procedimiento dbo.Usp_TI_AgenteDiag_*) o INTERNA (ejecutada por el backend, por ejemplo la búsqueda semántica).</summary>
    public string Tipo { get; set; } = "SP";
}

public sealed class AgenteTIHerramientaResultado
{
    public List<Dictionary<string, object?>> Filas { get; set; } = [];
    public bool Truncado { get; set; }
}

/// <summary>Un paso de la investigación: qué herramienta se usó, con qué parámetros y qué devolvió.</summary>
public sealed class AgenteTIPasoInvestigacion
{
    public int Orden { get; set; }
    public string HerramientaCodigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    /// <summary>AUTOMATICA (antes del modelo) o MODELO (el agente decidió usarla).</summary>
    public string Origen { get; set; } = string.Empty;
    public string ParametrosJson { get; set; } = "{}";
    public int Filas { get; set; }
    public bool Truncado { get; set; }
    public long DuracionMs { get; set; }
    public string Resumen { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}

/// <summary>Hallazgo del agente que cita una evidencia verificable (referencia comprobada por el servidor).</summary>
public sealed class AgenteTIHallazgo
{
    public string Fuente { get; set; } = string.Empty;
    public string Referencia { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
}

public sealed class AgenteTIEjecutorSimulacion
{
    public string IncidenciaNumero { get; set; } = string.Empty;
    public string AccionCodigo { get; set; } = string.Empty;
    public string ParametrosJson { get; set; } = "{}";
    public string Procedimiento { get; set; } = string.Empty;
    public int MaximoFilas { get; set; }
}

public sealed class AgenteTISimulacionRespuesta
{
    public long SesionNumero { get; set; }
    public string AccionCodigo { get; set; } = string.Empty;
    public bool Exito { get; set; }
    public int? FilasAfectadas { get; set; }
    public string ResultadoJson { get; set; } = string.Empty;
    public string ParametrosJson { get; set; } = "{}";
    public string Mensaje { get; set; } = string.Empty;
}

public sealed class AgenteTIInvestigacionTicket
{
    public long SesionNumero { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTime FechaInicio { get; set; }
    public DateTime? FechaDiagnostico { get; set; }
    public decimal? Confianza { get; set; }
    public string Diagnostico { get; set; } = string.Empty;
    public string AccionCodigo { get; set; } = string.Empty;
    public string UsuarioTI { get; set; } = string.Empty;
    public string NombreOperador { get; set; } = string.Empty;
    public bool InformeDisponible { get; set; }
    public bool PuedeAbrir { get; set; }
}

public sealed record AgenteTICatalogoItem(string Codigo, string Descripcion);

public sealed class AgenteTICatalogos
{
    public List<AgenteTICatalogoItem> Areas { get; set; } = [];
    public List<AgenteTICatalogoItem> Operadores { get; set; } = [];
}

public sealed record AgenteTIInforme(string InformeMarkdown, string IncidenciaNumero);

public sealed class ReasignarInvestigacionTISolicitud
{
    public string NuevoUsuario { get; set; } = string.Empty;
}

public sealed record AgenteTIInvitacionRespuesta(string UsuarioInvitado, string NombreInvitado, DateTime InvitacionExpira);

/// <summary>Invitación vista por el usuario final: solo datos de su propio ticket, nunca el diagnóstico interno.</summary>
public sealed class ReproduccionInvitacion
{
    public long SesionNumero { get; set; }
    public string IncidenciaNumero { get; set; } = string.Empty;
    public string TituloTicket { get; set; } = string.Empty;
    public string OperadorTI { get; set; } = string.Empty;
    public string EstadoInvitacion { get; set; } = string.Empty;
    public DateTime FechaInvitacion { get; set; }
    public DateTime InvitacionExpira { get; set; }
}

public sealed class ResponderReproduccionSolicitud
{
    public bool Aceptar { get; set; }
    public bool AceptaConsentimiento { get; set; }
    public string? Motivo { get; set; }
}

public sealed class RegistrarEventoReproduccionSolicitud
{
    public string Tipo { get; set; } = string.Empty;
    public string Contenido { get; set; } = string.Empty;
}

public sealed record AgenteTICodigoReferencia(string Archivo, int Linea, string Fragmento);
public sealed record AgenteTIComprobacion(string Codigo, string Descripcion, string Resultado);
public sealed class VincularIncidenciaTISolicitud
{
    public string IncidenciaNumero { get; set; } = string.Empty;
}

public sealed class AgenteTILiveTokenRespuesta
{
    public bool Disponible { get; set; }
    public string Token { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public string WebSocketUrl { get; set; } = string.Empty;
    public string InstruccionSistema { get; set; } = string.Empty;
    /// <summary>La configuración (modelo, instrucción, herramientas) quedó fijada en el token; el navegador no puede cambiarla.</summary>
    public bool Restringido { get; set; }
    /// <summary>Declaraciones de funciones Live, solo para tokens no restringidos.</summary>
    public object? Herramientas { get; set; }
    public DateTimeOffset? ExpiraEn { get; set; }
    public string Mensaje { get; set; } = string.Empty;
}

public sealed class AgenteTIPreparacionCambio
{
    public int MaximoFilas { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public bool PuedeEjecutar { get; set; }
    public string ProcedimientoEjecutor { get; set; } = string.Empty;
    public int? EjecucionSecuencia { get; set; }
    public int? SolicitudAprobacionSecuencia { get; set; }
    public string ParametrosJson { get; set; } = string.Empty;
    /// <summary>Esquema de parámetros del ejecutor; los parámetros aprobados se validan contra él antes de ejecutar.</summary>
    public string ParametrosEsquemaJson { get; set; } = string.Empty;
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

/// <summary>Parámetros operativos vigentes del agente y de los tickets (TI_Parametro), con caché corta en ControlAgenteTI.</summary>
public sealed record ParametrosAgenteTI(string Modo, string RiesgoMaximoAutonomo, decimal ConfianzaMinimaPropuesta, int? VigenciaAprobacionHoras,
    int? AutocierreValidacionDias)
{
    /// <summary>APAGADO: no investiga ni ejecuta.</summary>
    public bool Apagado => Modo == "APAGADO";
    /// <summary>APAGADO o SOMBRA: la ejecución de cambios está deshabilitada.</summary>
    public bool EjecucionDeshabilitada => Modo is "APAGADO" or "SOMBRA";
}

/// <summary>Todo lo que PoliticaAutonomia necesita para decidir si una acción propuesta puede ejecutarse sin humano.</summary>
public sealed class AgenteTIDatosAutonomia
{
    public long SesionNumero { get; set; }
    public string UsuarioTI { get; set; } = string.Empty;
    public string AreaTI { get; set; } = string.Empty;
    public string EstadoSesion { get; set; } = string.Empty;
    public decimal? Confianza { get; set; }
    public string AccionCodigo { get; set; } = string.Empty;
    public string ParametrosJson { get; set; } = "{}";
    public bool SolicitudPendiente { get; set; }
    public string IncidenciaNumero { get; set; } = string.Empty;
    public string TipoTicket { get; set; } = string.Empty;
    public string SubTipo { get; set; } = string.Empty;
    public string EstadoTicket { get; set; } = string.Empty;
    public string PerfilSolicitante { get; set; } = string.Empty;
    public string AccionTipo { get; set; } = string.Empty;
    public string AccionEstado { get; set; } = string.Empty;
    public bool RequiereAprobacion { get; set; } = true;
    public bool Reversible { get; set; }
    public string NivelRiesgo { get; set; } = string.Empty;
    public bool TieneEjecutor { get; set; }
    public string ParametrosEsquemaJson { get; set; } = string.Empty;
    public string ModoPolitica { get; set; } = "APROBACION";
    public decimal? ConfianzaMinima { get; set; }
    public string EstadoPolitica { get; set; } = string.Empty;
    public string ModoAgente { get; set; } = "ASISTIDO";
    public string RiesgoMaximo { get; set; } = string.Empty;
}

/// <summary>Resultado de la regla compuesta: Permitida solo si no quedó ningún motivo para exigir la decisión de TI.</summary>
public sealed record ResultadoPoliticaAutonomia(bool Permitida, IReadOnlyList<string> Motivos);
