/**
 * Archivo: reproduccionApi.ts
 * Objetivo: Permitir que el colaborador reproduzca su error, por invitación de TI, desde su propio portal.
 * Responsabilidad: Listar invitaciones, aceptarlas o rechazarlas, obtener el token Live y enviar la evidencia (eventos y grabación).
 * Dependencias: api.ts y grabadorPantallaService.ts (formulario de la grabación).
 * Flujo: ReproduccionUsuarioPage / InvitacionesReproduccionAviso -> reproduccionApi -> /api/reproducciones.
 * Consideraciones: El colaborador solo envía evidencia observacional; nunca ve el diagnóstico ni las decisiones de TI.
 */

import { crearApi } from './api'
import type { AgenteTILiveTokenRespuesta } from './asistenteTIApi'
import { formularioGrabacion } from './grabadorPantallaService'

export interface ReproduccionInvitacion {
  sesionNumero: number
  incidenciaNumero: string
  tituloTicket: string
  operadorTI: string
  estadoInvitacion: 'PENDIENTE' | 'ACEPTADA'
  fechaInvitacion: string
  invitacionExpira: string
}

const api = crearApi({
  conexion: 'No fue posible comunicarse con el servidor.',
  saturado: 'Se realizaron varias operaciones seguidas. Espera un momento.',
})
const ERROR = 'No fue posible completar la operación.'
const invitacion = (sesion: number, ruta = '') => `/api/reproducciones/${sesion}${ruta}`

export const listarReproducciones = () => api<ReproduccionInvitacion[]>('/api/reproducciones', { error: ERROR })
export const obtenerReproduccion = (sesion: number) => api<ReproduccionInvitacion>(invitacion(sesion), { error: ERROR })
export const responderReproduccion = (sesion: number, aceptar: boolean, aceptaConsentimiento: boolean, motivo?: string) =>
  api(invitacion(sesion, '/responder'), { cuerpo: { aceptar, aceptaConsentimiento, motivo: motivo || null }, error: ERROR })
export const crearTokenReproduccion = (sesion: number) =>
  api<AgenteTILiveTokenRespuesta>(invitacion(sesion, '/live/token'), { metodo: 'POST', error: ERROR })
export const registrarEventoReproduccion = (sesion: number, tipo: string, contenido: string) =>
  api(invitacion(sesion, '/eventos'), { cuerpo: { tipo, contenido }, error: ERROR })
export const finalizarReproduccion = (sesion: number) => api(invitacion(sesion, '/finalizar'), { metodo: 'POST', error: ERROR })

/** Sube la grabación de la pantalla de la reproducción (consentida) para que TI la vea y el agente la analice. */
export const subirGrabacionReproduccion = (sesion: number, grabacion: Blob, duracionSegundos: number) =>
  api(invitacion(sesion, '/grabaciones'), {
    cuerpo: formularioGrabacion(grabacion, duracionSegundos),
    conexion: 'No fue posible enviar la grabación de tu pantalla.',
    error: 'No fue posible guardar la grabación de tu pantalla.',
  })
