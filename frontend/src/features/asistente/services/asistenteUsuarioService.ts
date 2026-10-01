/**
 * Cliente del Asistente TI para colaboradores.
 * La identidad se resuelve desde la cookie HttpOnly y nunca se envía desde React.
 */

import { rechazarSiSesionExpirada } from '../../autenticacion/services/sesionExpirada'

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
}

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
