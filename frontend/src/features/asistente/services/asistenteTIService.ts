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

async function procesar<T>(respuesta: Response, mensaje: string): Promise<T> {
  if (respuesta.ok) return respuesta.json() as Promise<T>
  rechazarSiSesionExpirada(respuesta)
  if (respuesta.status === 403) throw new Error('Tu perfil no tiene acceso al Asistente TI.')
  if (respuesta.status === 429) throw new Error('Se realizaron varias consultas seguidas. Espera un momento.')
  const datos = await respuesta.json().catch(() => null) as { mensaje?: string } | null
  throw new Error(datos?.mensaje || mensaje)
}

export async function consultarAsistenteTI(mensaje: string, historial: AsistenteMensajeHistorial[]) {
  let respuesta: Response
  try {
    respuesta = await fetch('/api/asistente/ti/mensajes', {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ mensaje, historial: historial.slice(-10) }),
    })
  } catch {
    throw new Error('No fue posible comunicarse con el Asistente TI.')
  }
  return procesar<AsistenteTIRespuesta>(respuesta, 'No fue posible procesar la consulta.')
}

export async function confirmarAccionTI(tokenConfirmacion: string) {
  let respuesta: Response
  try {
    respuesta = await fetch('/api/asistente/ti/acciones/confirmar', {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ tokenConfirmacion }),
    })
  } catch {
    throw new Error('No fue posible ejecutar la acción confirmada.')
  }
  return procesar<AccionTIConfirmada>(respuesta, 'No fue posible ejecutar la acción confirmada.')
}
