/**
 * Archivo: reproduccionUsuarioService.ts
 * Objetivo: Consumir las invitaciones de TI para que el usuario reproduzca su error con pantalla y voz.
 * Responsabilidad: Transportar consentimiento, sesión Live y evidencia del usuario sin enviar identidad ni permisos desde el navegador.
 */

import { rechazarSiSesionExpirada } from '../../autenticacion/services/sesionExpirada'
import type { AgenteTILiveTokenRespuesta } from '../../asistente/services/asistenteTIService'

export interface ReproduccionInvitacion {
  sesionNumero: number
  incidenciaNumero: string
  tituloTicket: string
  operadorTI: string
  estadoInvitacion: 'PENDIENTE' | 'ACEPTADA'
  fechaInvitacion: string
  invitacionExpira: string
}

async function solicitar<T>(url: string, metodo: 'GET' | 'POST', body?: unknown): Promise<T> {
  let respuesta: Response
  try {
    respuesta = await fetch(url, {
      method: metodo,
      credentials: 'include',
      headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  } catch {
    throw new Error('No fue posible comunicarse con el servidor.')
  }
  if (respuesta.ok) return (respuesta.status === 204 ? undefined : await respuesta.json()) as T
  rechazarSiSesionExpirada(respuesta)
  if (respuesta.status === 429) throw new Error('Se realizaron varias operaciones seguidas. Espera un momento.')
  const datos = await respuesta.json().catch(() => null) as { mensaje?: string } | null
  throw new Error(datos?.mensaje || 'No fue posible completar la operación.')
}

export const listarReproducciones = () => solicitar<ReproduccionInvitacion[]>('/api/reproducciones', 'GET')
export const obtenerReproduccion = (sesion: number) => solicitar<ReproduccionInvitacion>(`/api/reproducciones/${sesion}`, 'GET')
export const responderReproduccion = (sesion: number, aceptar: boolean, aceptaConsentimiento: boolean, motivo?: string) =>
  solicitar<void>(`/api/reproducciones/${sesion}/responder`, 'POST', { aceptar, aceptaConsentimiento, motivo: motivo || null })
export const crearTokenReproduccion = (sesion: number) => solicitar<AgenteTILiveTokenRespuesta>(`/api/reproducciones/${sesion}/live/token`, 'POST')
export const registrarEventoReproduccion = (sesion: number, tipo: string, contenido: string) =>
  solicitar<void>(`/api/reproducciones/${sesion}/eventos`, 'POST', { tipo, contenido })
export const finalizarReproduccion = (sesion: number) => solicitar<void>(`/api/reproducciones/${sesion}/finalizar`, 'POST')

/** Sube la grabación de la pantalla de la reproducción (consentida) para que TI la vea y el agente la analice. */
export async function subirGrabacionReproduccion(sesion: number, grabacion: Blob, duracionSegundos: number) {
  const datos = new FormData()
  datos.append('Archivo', new File([grabacion], 'grabacion-pantalla.webm', { type: 'video/webm' }))
  datos.append('DuracionSegundos', String(Math.round(duracionSegundos)))
  let respuesta: Response
  try {
    respuesta = await fetch(`/api/reproducciones/${sesion}/grabaciones`, { method: 'POST', credentials: 'include', body: datos })
  } catch {
    throw new Error('No fue posible enviar la grabación de tu pantalla.')
  }
  if (!respuesta.ok) {
    const error = await respuesta.json().catch(() => null) as { mensaje?: string } | null
    throw new Error(error?.mensaje || 'No fue posible guardar la grabación de tu pantalla.')
  }
}
