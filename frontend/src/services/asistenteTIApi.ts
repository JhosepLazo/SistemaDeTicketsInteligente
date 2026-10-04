/**
 * Archivo: asistenteTIApi.ts
 * Objetivo: Comunicar la consola del Asistente TI y del Agente de Ingeniería con su API.
 * Responsabilidad: Conversar con el asistente, confirmar acciones firmadas y llevar una investigación completa: sesión, evidencia Live,
 *   grabaciones, diagnóstico, expediente, simulación, decisión de cambio, invitación al colaborador y cierre.
 * Dependencias: api.ts y grabadorPantallaService.ts (formulario de la grabación).
 * Flujo: AsistenteTIPage / AsistenteTIConversacion / GestionTicketsTIPage -> asistenteTIApi -> /api/asistente/ti.
 * Consideraciones: El navegador nunca envía usuario, área ni aprobaciones; el backend decide con la sesión y los procedimientos.
 */

import { crearApi } from './api'
import type { AsistenteMensajeHistorial } from './asistenteUsuarioApi'
import { formularioGrabacion } from './grabadorPantallaService'

export interface AsistenteTIUsuarioPropuesto {
  usuario: string
  area: string
  areaDescripcion: string
  perfil: string
  perfilDescripcion: string
  correo: string
  estado: string
  yaExiste: boolean
}

export interface AsistenteTIAccion {
  /** Investigación creada desde la conversación (tipo INVESTIGACION). */
  sesionNumero?: number | null
  tipo: string
  titulo: string
  tokenConfirmacion: string
  expiraEn: string | null
  faltantes: string[]
  usuario: AsistenteTIUsuarioPropuesto | null
}

export interface AsistenteTIRespuesta {
  respuesta: string
  modo: 'IA' | 'CONOCIMIENTO'
  fuentes: string[]
  sugerencias: string[]
  accion: AsistenteTIAccion | null
}

export interface AccionTIConfirmada {
  mensaje: string
  tipo: string
  registro: string
}

export interface AgenteTISesion {
  solucionValidada: boolean
  conocimientoCodigo: string
  sesionNumero: number
  incidenciaNumero: string
  idCorrelacion: string
  descripcionInicial: string
  estado: string
  resumenObservacion: string
  procesoObservado: string
  errorObservado: string
  diagnostico: string
  causaProbable: string
  solucionPropuesta: string
  confianza: number | null
  accionCodigo: string
  nivelRiesgo: string
  parametrosJson: string
  decision: string
  solicitudAprobacionSecuencia: number | null
  fechaInicio: string
  fechaDiagnostico: string | null
  fechaDecision: string | null
  informeDisponible: boolean
  usuarioTI: string
  nombreOperador: string
  esPropietario: boolean
  /** Supervisión o responsable TI del ticket: puede tomar la investigación. */
  puedeTomar?: boolean
  usuarioTITicket?: string
  usuarioInvitado: string
  nombreInvitado: string
  estadoInvitacion: '' | 'PENDIENTE' | 'ACEPTADA' | 'RECHAZADA' | 'CANCELADA' | 'FINALIZADA' | 'VENCIDA'
  invitacionExpira: string | null
}

export interface AgenteTITicketContexto {
  incidenciaNumero: string
  titulo: string
  detalle: string
  mensajeError: string
  estado: string
  linea: string
  item: string
  tipo: string
  subTipo: string
  categoria: string
  usuarioSolicitante: string
  fechaRegistro: string | null
}

export interface AgenteTIEvento {
  origenServidor: boolean
  secuencia: number
  tipo: string
  fuente: string
  contenido: string
  datosJson: string
  fecha: string
}

export interface AgenteTIDocumento {
  companiaSocio: string
  tipoDocumento: string
  numeroDocumento: string
  descripcion: string
}

export interface AgenteTIConocimiento {
  conocimientoCodigo: string
  titulo: string
  problema: string
  sintomas: string
  causa: string
  solucion: string
  procedimiento: string
  puntajeContextual: number
}

export interface AgenteTIAccionDisponible {
  accionCodigo: string
  nombre: string
  descripcion: string
  tipo: string
  nivelRiesgo: string
  requiereAprobacion: boolean
  tieneEjecutor: boolean
  parametrosDescripcion: string
}

export interface AgenteTIAuditoria {
  entidad: string
  registro: string
  evento: string
  resultado: string
  detalleJson: string
  idCorrelacion: string
  fecha: string
}

export interface AgenteTIContextoInvestigacion {
  sesion: AgenteTISesion
  ticket: AgenteTITicketContexto
  documentos: AgenteTIDocumento[]
  mensajes: Array<{ tipoAutor: string; autor: string; contenido: string; fechaMensaje: string; esInterno: boolean }>
  conocimientos: AgenteTIConocimiento[]
  acciones: AgenteTIAccionDisponible[]
  auditoria: AgenteTIAuditoria[]
  eventos: AgenteTIEvento[]
}

export interface AgenteTIEvidencia {
  tipoFuente: string
  referencia: string
  descripcion: string
  similitud: number | null
}

export interface AgenteTIAccionPropuesta {
  accionCodigo: string
  nombre: string
  nivelRiesgo: string
  requiereAprobacion: boolean
  parametrosJson: string
}

export interface AgenteTIDiagnosticoRespuesta {
  sesionNumero: number
  estado: string
  diagnostico: string
  causaProbable: string
  solucionPropuesta: string
  confianza: number
  evidencias: AgenteTIEvidencia[]
  trazaTecnica: string[]
  accion: AgenteTIAccionPropuesta | null
  informeDisponible: boolean
  informeMarkdown: string
  limitacion: string
  /** AGENTE (varios pasos con herramientas), IA (una llamada) o SIN_MODELO. */
  modo: string
  pasos: AgenteTIPasoInvestigacion[]
  hallazgos: AgenteTIHallazgo[]
  datosOcultados: number
}

export interface AgenteTIPasoInvestigacion {
  orden: number
  herramientaCodigo: string
  nombre: string
  origen: 'AUTOMATICA' | 'MODELO' | string
  parametrosJson: string
  filas: number
  truncado: boolean
  duracionMs: number
  resumen: string
  error: string
}

export interface AgenteTIHallazgo {
  fuente: string
  referencia: string
  descripcion: string
}

export interface AgenteTISimulacionRespuesta {
  sesionNumero: number
  accionCodigo: string
  exito: boolean
  filasAfectadas: number | null
  resultadoJson: string
  parametrosJson: string
  mensaje: string
}

export interface AgenteTILiveTokenRespuesta {
  disponible: boolean
  token: string
  modelo: string
  webSocketUrl: string
  instruccionSistema: string
  /** Modelo, instrucción y funciones quedaron fijados en el token: el navegador solo abre la conexión. */
  restringido: boolean
  herramientas: unknown[] | null
  expiraEn: string | null
  mensaje: string
}

export interface AgenteTIDecisionRespuesta {
  sesionNumero: number
  estado: string
  mensaje: string
  solicitudAprobacionSecuencia: number | null
  ejecutado: boolean
}

export interface AgenteCodigoReferencia {
  archivo: string
  linea: number
  fragmento: string
}

export interface AgenteComprobacion {
  codigo: string
  descripcion: string
  resultado: 'OK' | 'ERROR' | 'PENDIENTE' | 'NO_DISPONIBLE'
}

export interface AgenteTICatalogoItem {
  codigo: string
  descripcion: string
}

export interface AgenteTICatalogos {
  areas: AgenteTICatalogoItem[]
  operadores: AgenteTICatalogoItem[]
}

export interface AgenteTIInvestigacionTicket {
  sesionNumero: number
  estado: string
  fechaInicio: string
  fechaDiagnostico: string | null
  confianza: number | null
  diagnostico: string
  accionCodigo: string
  usuarioTI: string
  nombreOperador: string
  informeDisponible: boolean
  puedeAbrir: boolean
}

const api = crearApi({
  conexion: 'No fue posible comunicarse con el Agente de Ingeniería.',
  sinPermiso: 'Tu perfil no tiene acceso al Asistente TI.',
  saturado: 'Se realizaron varias operaciones seguidas. Espera un momento.',
})
const investigacion = (numero: number, ruta = '') => `/api/asistente/ti/investigaciones/${numero}${ruta}`

// Conversación y acciones confirmadas.

export const consultarAsistenteTI = (mensaje: string, historial: AsistenteMensajeHistorial[]) =>
  api<AsistenteTIRespuesta>('/api/asistente/ti/mensajes', {
    cuerpo: { mensaje, historial: historial.slice(-10) },
    error: 'No fue posible procesar la consulta.',
  })

export const confirmarAccionTI = (tokenConfirmacion: string) =>
  api<AccionTIConfirmada>('/api/asistente/ti/acciones/confirmar', {
    cuerpo: { tokenConfirmacion },
    error: 'No fue posible ejecutar la acción confirmada.',
  })

export const obtenerCatalogosAgenteTI = () =>
  api<AgenteTICatalogos>('/api/asistente/ti/catalogos', { error: 'No se pudieron cargar los catálogos del agente.' })

export const listarInvestigacionesTicketTI = (incidenciaNumero: string) =>
  api<AgenteTIInvestigacionTicket[]>(`/api/asistente/ti/tickets/${encodeURIComponent(incidenciaNumero)}/investigaciones`, {
    error: 'No se pudieron cargar las investigaciones del ticket.',
  })

// Sesión de investigación y evidencia.

export const listarInvestigacionesTI = (todas = false) =>
  api<AgenteTISesion[]>(`/api/asistente/ti/investigaciones${todas ? '?alcance=todas' : ''}`, {
    error: 'No se pudo cargar el historial de investigaciones.',
  })

export const crearInvestigacionTI = (incidenciaNumero: string, descripcion: string) =>
  api<AgenteTISesion>('/api/asistente/ti/investigaciones', {
    cuerpo: { incidenciaNumero: incidenciaNumero.trim() || null, descripcion },
    error: 'No fue posible iniciar la investigación.',
  })

export const obtenerInvestigacionTI = (sesionNumero: number) =>
  api<AgenteTIContextoInvestigacion>(investigacion(sesionNumero), { error: 'No fue posible recuperar la investigación.' })

export const crearTokenLiveTI = (sesionNumero: number) =>
  api<AgenteTILiveTokenRespuesta>(investigacion(sesionNumero, '/live/token'), {
    metodo: 'POST',
    error: 'No fue posible iniciar la asistencia Live.',
  })

export const registrarEventoInvestigacionTI = (sesionNumero: number, tipo: string, fuente: string, contenido: string, datosJson?: string) =>
  api(investigacion(sesionNumero, '/eventos'), {
    cuerpo: { tipo, fuente, contenido, datosJson: datosJson || null },
    error: 'No fue posible registrar la evidencia de la sesión.',
  })

export const finalizarObservacionTI = (
  sesionNumero: number,
  resumenObservacion: string,
  procesoObservado: string,
  errorObservado?: string,
) =>
  api(investigacion(sesionNumero, '/observacion/finalizar'), {
    cuerpo: { resumenObservacion, procesoObservado, errorObservado: errorObservado || null },
    error: 'No fue posible cerrar la etapa de observación.',
  })

/** Sube la grabación de la pantalla compartida durante la observación TI. */
export const subirGrabacionTI = (numero: number, grabacion: Blob, duracionSegundos: number) =>
  api<{ eventoSecuencia: number }>(investigacion(numero, '/grabaciones'), {
    cuerpo: formularioGrabacion(grabacion, duracionSegundos),
    conexion: 'No fue posible subir la grabación de la pantalla.',
    error: 'No fue posible guardar la grabación de la pantalla.',
  })

/** URL para reproducir una grabación en la consola (la cookie de sesión autoriza la descarga). */
export const urlGrabacionTI = (numero: number, eventoSecuencia: number) => investigacion(numero, `/grabaciones/${eventoSecuencia}`)

export const reabrirObservacionTI = (numero: number) =>
  api(investigacion(numero, '/reabrir'), { metodo: 'POST', error: 'No fue posible reabrir la observación.' })

export const buscarCodigoInvestigacionTI = (numero: number) =>
  api<AgenteCodigoReferencia[]>(investigacion(numero, '/codigo'), { error: 'No se pudieron consultar las referencias de código.' })

export const vincularIncidenciaTI = (numero: number, incidenciaNumero: string) =>
  api(investigacion(numero, '/vincular'), { cuerpo: { incidenciaNumero }, error: 'No se pudo vincular el ticket.' })

export const reasignarInvestigacionTI = (numero: number, nuevoUsuario: string) =>
  api(investigacion(numero, '/reasignar'), { cuerpo: { nuevoUsuario }, error: 'No se pudo reasignar la investigación.' })

export const invitarUsuarioReproduccionTI = (numero: number) =>
  api<{ usuarioInvitado: string; nombreInvitado: string; invitacionExpira: string }>(investigacion(numero, '/invitacion'), {
    metodo: 'POST',
    error: 'No se pudo invitar al usuario.',
  })

export const cancelarInvitacionReproduccionTI = (numero: number) =>
  api(investigacion(numero, '/invitacion/cancelar'), { metodo: 'POST', error: 'No se pudo cancelar la invitación.' })

export const cancelarInvestigacionTI = (numero: number) =>
  api(investigacion(numero, '/cancelar'), { metodo: 'POST', error: 'No se pudo cancelar la investigación.' })

// Investigación y diagnóstico.

export const analizarInvestigacionTI = (sesionNumero: number) =>
  api<AgenteTIDiagnosticoRespuesta>(investigacion(sesionNumero, '/analizar'), {
    metodo: 'POST',
    error: 'No fue posible completar el diagnóstico.',
  })

export const obtenerDiagnosticoTI = (sesionNumero: number) =>
  api<AgenteTIDiagnosticoRespuesta>(investigacion(sesionNumero, '/diagnostico'), { error: 'No fue posible recuperar el diagnóstico.' })

export const obtenerInformeTI = (numero: number) =>
  api<{ informeMarkdown: string; incidenciaNumero: string }>(investigacion(numero, '/informe'), {
    error: 'No se pudo obtener el expediente.',
  })

// Decisión de TI.

/** Conserva el expediente y devuelve el informe Markdown para descargarlo. */
export const grabarInformacionTI = (sesionNumero: number) =>
  api<Blob>(investigacion(sesionNumero, '/grabar-informacion'), {
    metodo: 'POST',
    archivo: true,
    error: 'No fue posible generar el expediente técnico.',
  })

export const comprobarInvestigacionTI = (numero: number) =>
  api<AgenteComprobacion[]>(investigacion(numero, '/dry-run'), { error: 'No se pudo ejecutar el diagnóstico sin cambios.' })

export const simularCambioTI = (numero: number) =>
  api<AgenteTISimulacionRespuesta>(investigacion(numero, '/simular-cambio'), { metodo: 'POST', error: 'No se pudo simular el cambio.' })

export const realizarCambioTI = (sesionNumero: number) =>
  api<AgenteTIDecisionRespuesta>(investigacion(sesionNumero, '/realizar-cambio'), {
    cuerpo: { confirmar: true },
    error: 'No fue posible procesar la decisión de cambio.',
  })

export const validarSolucionInvestigacionTI = (numero: number) =>
  api(investigacion(numero, '/validar-solucion'), { cuerpo: { confirmar: true }, error: 'No se pudo validar la solución.' })

export const crearConocimientoInvestigacionTI = (numero: number) =>
  api<{ conocimientoCodigo: string }>(investigacion(numero, '/conocimiento'), { metodo: 'POST', error: 'No se pudo crear el borrador.' })
