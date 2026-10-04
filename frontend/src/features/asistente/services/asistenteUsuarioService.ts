/**
 * Cliente del Asistente TI para colaboradores.
 * La identidad se resuelve desde la cookie HttpOnly y nunca se envía desde React.
 */

import { rechazarSiSesionExpirada } from '../../autenticacion/services/sesionExpirada'
import type { AgenteTILiveTokenRespuesta } from './asistenteTIService'

export interface AsistenteMensajeHistorial {
  rol: 'usuario' | 'asistente'
  contenido: string
}

export interface AsistenteFuente {
  tipo: 'CONOCIMIENTO' | 'TICKET'
  codigo: string
  titulo: string
  resumen: string
}

export interface AsistenteRespuesta {
  respuesta: string
  modo: 'IA' | 'CONOCIMIENTO'
  escalarATicket: boolean
  fuentes: AsistenteFuente[]
  sugerencias: string[]
  accion: AsistenteUsuarioAccion | null
}

export interface AsistenteUsuarioAccion {
  tipo: 'CREAR_TICKET'
  titulo: string
  detalle: string
  /** Mensaje de error exacto registrado al mostrar el problema en pantalla. */
  mensajeError?: string
}

export interface EvidenciaReproduccion {
  descripcion: string
  pasos: string[]
  mensajeError: string
  conversacion: AsistenteMensajeHistorial[]
}

async function enviarAsistente<T>(url: string, cuerpo: unknown, defecto: string): Promise<T> {
  let respuesta: Response
  try {
    respuesta = await fetch(url, { method: 'POST', credentials: 'include', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(cuerpo) })
  } catch {
    throw new Error('No fue posible comunicarse con el Asistente TI. Intenta nuevamente en unos momentos.')
  }
  if (respuesta.ok) return respuesta.json() as Promise<T>
  rechazarSiSesionExpirada(respuesta)
  const datos = await respuesta.json().catch(() => null) as { mensaje?: string } | null
  throw new Error(datos?.mensaje || defecto)
}

/** Token de un solo uso para mostrar el error compartiendo pantalla con el asistente de voz. */
export const crearTokenLiveColaborador = (descripcion: string) =>
  enviarAsistente<AgenteTILiveTokenRespuesta>('/api/asistente/usuario/live/token', { descripcion: descripcion.slice(0, 1000) }, 'No fue posible iniciar la sesión de pantalla.')

/** Convierte lo mostrado en pantalla en un borrador de ticket (título, descripción y mensaje de error). */
export const prepararBorradorEvidencia = (evidencia: EvidenciaReproduccion) =>
  enviarAsistente<AsistenteUsuarioAccion>('/api/asistente/usuario/evidencia/borrador', evidencia, 'No fue posible preparar el ticket.')

export async function consultarAsistente(
  mensaje: string,
  historial: AsistenteMensajeHistorial[],
): Promise<AsistenteRespuesta> {
  let respuesta: Response
  try {
    respuesta = await fetch('/api/asistente/usuario/mensajes', {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ mensaje, historial: historial.slice(-10) }),
    })
  } catch {
    throw new Error('No fue posible comunicarse con el Asistente TI. Intenta nuevamente en unos momentos.')
  }

  if (respuesta.ok) return respuesta.json() as Promise<AsistenteRespuesta>

  rechazarSiSesionExpirada(respuesta)
  if (respuesta.status === 403) throw new Error('Tu perfil no tiene acceso a este asistente.')
  if (respuesta.status === 429) throw new Error('Has realizado varias consultas seguidas. Espera un momento y vuelve a intentarlo.')

  try {
    const datos = await respuesta.json() as { mensaje?: string }
    throw new Error(datos.mensaje || 'El Asistente TI no pudo procesar tu consulta.')
  } catch (error) {
    if (error instanceof Error && error.message !== 'Unexpected end of JSON input') throw error
    throw new Error('El Asistente TI no pudo procesar tu consulta.')
  }
}
