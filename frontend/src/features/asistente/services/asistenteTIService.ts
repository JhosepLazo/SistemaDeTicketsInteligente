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
  limitacion: string
}

export interface AgenteTILiveTokenRespuesta {
  disponible: boolean
  token: string
  modelo: string
  webSocketUrl: string
  instruccionSistema: string
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
