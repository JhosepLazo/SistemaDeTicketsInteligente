/**
 * Archivo: asistenteTIService.ts
 * Objetivo: Consumir el Asistente TI y el flujo persistente del Agente de Ingeniería Autónomo.
 * Responsabilidad: Transportar sesiones, eventos Live, diagnóstico, expediente y decisiones de TI sin enviar identidad ni permisos desde el navegador.
 */

import type { AsistenteMensajeHistorial } from './asistenteUsuarioService'
import { rechazarSiSesionExpirada } from '../../autenticacion/services/sesionExpirada'

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

export interface AgenteTIHallazgo { fuente: string; referencia: string; descripcion: string }

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

async function procesar<T>(respuesta: Response, mensaje: string): Promise<T> {
  if (respuesta.ok) return respuesta.json() as Promise<T>
  rechazarSiSesionExpirada(respuesta)
  if (respuesta.status === 403) throw new Error('Tu perfil no tiene acceso al Asistente TI.')
  if (respuesta.status === 429) throw new Error('Se realizaron varias operaciones seguidas. Espera un momento.')
  const datos = await respuesta.json().catch(() => null) as { mensaje?: string } | null
  throw new Error(datos?.mensaje || mensaje)
}

async function procesarSinContenido(respuesta: Response, mensaje: string) {
  if (respuesta.ok) return
  rechazarSiSesionExpirada(respuesta)
  const datos = await respuesta.json().catch(() => null) as { mensaje?: string } | null
  throw new Error(datos?.mensaje || mensaje)
}

async function enviar(url: string, metodo: 'POST' | 'GET', body?: unknown) {
  try {
    return await fetch(url, {
      method: metodo,
      credentials: 'include',
      headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  } catch {
    throw new Error('No fue posible comunicarse con el Agente de Ingeniería.')
  }
}

export async function consultarAsistenteTI(mensaje: string, historial: AsistenteMensajeHistorial[]) {
  const respuesta = await enviar('/api/asistente/ti/mensajes', 'POST', { mensaje, historial: historial.slice(-10) })
  return procesar<AsistenteTIRespuesta>(respuesta, 'No fue posible procesar la consulta.')
}

export async function confirmarAccionTI(tokenConfirmacion: string) {
  const respuesta = await enviar('/api/asistente/ti/acciones/confirmar', 'POST', { tokenConfirmacion })
  return procesar<AccionTIConfirmada>(respuesta, 'No fue posible ejecutar la acción confirmada.')
}

export async function crearInvestigacionTI(incidenciaNumero: string, descripcion: string) {
  const respuesta = await enviar('/api/asistente/ti/investigaciones', 'POST', { incidenciaNumero: incidenciaNumero.trim() || null, descripcion })
  return procesar<AgenteTISesion>(respuesta, 'No fue posible iniciar la investigación.')
}

export async function obtenerInvestigacionTI(sesionNumero: number) {
  const respuesta = await enviar(`/api/asistente/ti/investigaciones/${sesionNumero}`, 'GET')
  return procesar<AgenteTIContextoInvestigacion>(respuesta, 'No fue posible recuperar la investigación.')
}

export async function registrarEventoInvestigacionTI(sesionNumero: number, tipo: string, fuente: string, contenido: string, datosJson?: string) {
  const respuesta = await enviar(`/api/asistente/ti/investigaciones/${sesionNumero}/eventos`, 'POST', { tipo, fuente, contenido, datosJson: datosJson || null })
  await procesarSinContenido(respuesta, 'No fue posible registrar la evidencia de la sesión.')
}

export async function finalizarObservacionTI(sesionNumero: number, resumenObservacion: string, procesoObservado: string, errorObservado?: string) {
  const respuesta = await enviar(`/api/asistente/ti/investigaciones/${sesionNumero}/observacion/finalizar`, 'POST', { resumenObservacion, procesoObservado, errorObservado: errorObservado || null })
  await procesarSinContenido(respuesta, 'No fue posible cerrar la etapa de observación.')
}

export async function crearTokenLiveTI(sesionNumero: number) {
  const respuesta = await enviar(`/api/asistente/ti/investigaciones/${sesionNumero}/live/token`, 'POST')
  return procesar<AgenteTILiveTokenRespuesta>(respuesta, 'No fue posible iniciar la asistencia Live.')
}

export async function analizarInvestigacionTI(sesionNumero: number) {
  const respuesta = await enviar(`/api/asistente/ti/investigaciones/${sesionNumero}/analizar`, 'POST')
  return procesar<AgenteTIDiagnosticoRespuesta>(respuesta, 'No fue posible completar el diagnóstico.')
}

export async function obtenerDiagnosticoTI(sesionNumero: number) {
  const respuesta = await enviar(`/api/asistente/ti/investigaciones/${sesionNumero}/diagnostico`, 'GET')
  return procesar<AgenteTIDiagnosticoRespuesta>(respuesta, 'No fue posible recuperar el diagnóstico.')
}

export async function grabarInformacionTI(sesionNumero: number) {
  const respuesta = await enviar(`/api/asistente/ti/investigaciones/${sesionNumero}/grabar-informacion`, 'POST')
  if (respuesta.ok) return respuesta.blob()
  rechazarSiSesionExpirada(respuesta)
  const datos = await respuesta.json().catch(() => null) as { mensaje?: string } | null
  throw new Error(datos?.mensaje || 'No fue posible generar el expediente técnico.')
}

export async function realizarCambioTI(sesionNumero: number) {
  const respuesta = await enviar(`/api/asistente/ti/investigaciones/${sesionNumero}/realizar-cambio`, 'POST', { confirmar: true })
  return procesar<AgenteTIDecisionRespuesta>(respuesta, 'No fue posible procesar la decisión de cambio.')
}

export interface AgenteCodigoReferencia { archivo: string; linea: number; fragmento: string }
export interface AgenteComprobacion { codigo: string; descripcion: string; resultado: 'OK' | 'ERROR' | 'PENDIENTE' | 'NO_DISPONIBLE' }

export async function listarInvestigacionesTI(todas = false) {
  return procesar<AgenteTISesion[]>(await enviar(`/api/asistente/ti/investigaciones${todas ? '?alcance=todas' : ''}`, 'GET'), 'No se pudo cargar el historial de investigaciones.')
}

export interface AgenteTICatalogoItem { codigo: string; descripcion: string }
export interface AgenteTICatalogos { areas: AgenteTICatalogoItem[]; operadores: AgenteTICatalogoItem[] }
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

export async function obtenerCatalogosAgenteTI() {
  return procesar<AgenteTICatalogos>(await enviar('/api/asistente/ti/catalogos', 'GET'), 'No se pudieron cargar los catálogos del agente.')
}
export async function listarInvestigacionesTicketTI(incidenciaNumero: string) {
  return procesar<AgenteTIInvestigacionTicket[]>(await enviar(`/api/asistente/ti/tickets/${encodeURIComponent(incidenciaNumero)}/investigaciones`, 'GET'), 'No se pudieron cargar las investigaciones del ticket.')
}
export async function obtenerInformeTI(numero: number) {
  return procesar<{ informeMarkdown: string; incidenciaNumero: string }>(await enviar(`/api/asistente/ti/investigaciones/${numero}/informe`, 'GET'), 'No se pudo obtener el expediente.')
}
export async function invitarUsuarioReproduccionTI(numero: number) {
  return procesar<{ usuarioInvitado: string; nombreInvitado: string; invitacionExpira: string }>(await enviar(`/api/asistente/ti/investigaciones/${numero}/invitacion`, 'POST'), 'No se pudo invitar al usuario.')
}
export async function cancelarInvitacionReproduccionTI(numero: number) {
  await procesarSinContenido(await enviar(`/api/asistente/ti/investigaciones/${numero}/invitacion/cancelar`, 'POST'), 'No se pudo cancelar la invitación.')
}
export async function reasignarInvestigacionTI(numero: number, nuevoUsuario: string) {
  await procesarSinContenido(await enviar(`/api/asistente/ti/investigaciones/${numero}/reasignar`, 'POST', { nuevoUsuario }), 'No se pudo reasignar la investigación.')
}
export async function cancelarInvestigacionTI(numero: number) {
  await procesarSinContenido(await enviar(`/api/asistente/ti/investigaciones/${numero}/cancelar`, 'POST'), 'No se pudo cancelar la investigación.')
}
export async function vincularIncidenciaTI(numero: number, incidenciaNumero: string) {
  await procesarSinContenido(await enviar(`/api/asistente/ti/investigaciones/${numero}/vincular`, 'POST', { incidenciaNumero }), 'No se pudo vincular el ticket.')
}
export async function comprobarInvestigacionTI(numero: number) {
  return procesar<AgenteComprobacion[]>(await enviar(`/api/asistente/ti/investigaciones/${numero}/dry-run`, 'GET'), 'No se pudo ejecutar el diagnóstico sin cambios.')
}
export async function buscarCodigoInvestigacionTI(numero: number) {
  return procesar<AgenteCodigoReferencia[]>(await enviar(`/api/asistente/ti/investigaciones/${numero}/codigo`, 'GET'), 'No se pudieron consultar las referencias de código.')
}
export async function validarSolucionInvestigacionTI(numero: number) {
  await procesarSinContenido(await enviar(`/api/asistente/ti/investigaciones/${numero}/validar-solucion`, 'POST', { confirmar: true }), 'No se pudo validar la solución.')
}
export async function simularCambioTI(numero: number) {
  return procesar<AgenteTISimulacionRespuesta>(await enviar(`/api/asistente/ti/investigaciones/${numero}/simular-cambio`, 'POST'), 'No se pudo simular el cambio.')
}
export async function crearConocimientoInvestigacionTI(numero: number) {
  return procesar<{ conocimientoCodigo: string }>(await enviar(`/api/asistente/ti/investigaciones/${numero}/conocimiento`, 'POST'), 'No se pudo crear el borrador.')
}
