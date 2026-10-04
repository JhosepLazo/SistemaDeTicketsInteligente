/**
 * Archivo: asistenteUsuarioApi.ts
 * Objetivo: Comunicar el Asistente TI del colaborador con su API.
 * Responsabilidad: Enviar consultas con el historial reciente, pedir el token para mostrar el error en pantalla y convertir lo
 *   mostrado en un borrador de ticket.
 * Dependencias: api.ts (cliente único de la API).
 * Flujo: AsistenteUsuarioPage / MostrarErrorLive -> asistenteUsuarioApi -> /api/asistente/usuario.
 * Consideraciones: Solo viajan los últimos 10 mensajes del historial; la identidad sale de la cookie en el backend.
 */

import { crearApi } from './api'
import type { AgenteTILiveTokenRespuesta } from './asistenteTIApi'

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

const api = crearApi({
  conexion: 'No fue posible comunicarse con el Asistente TI. Intenta nuevamente en unos momentos.',
  sinPermiso: 'Tu perfil no tiene acceso a este asistente.',
  saturado: 'Has realizado varias consultas seguidas. Espera un momento y vuelve a intentarlo.',
})

export const consultarAsistente = (mensaje: string, historial: AsistenteMensajeHistorial[]) =>
  api<AsistenteRespuesta>('/api/asistente/usuario/mensajes', {
    cuerpo: { mensaje, historial: historial.slice(-10) },
    error: 'El Asistente TI no pudo procesar tu consulta.',
  })

/** Token de un solo uso para mostrar el error compartiendo pantalla con el asistente de voz. */
export const crearTokenLiveColaborador = (descripcion: string) =>
  api<AgenteTILiveTokenRespuesta>('/api/asistente/usuario/live/token', {
    cuerpo: { descripcion: descripcion.slice(0, 1000) },
    error: 'No fue posible iniciar la sesión de pantalla.',
  })

/** Convierte lo mostrado en pantalla en un borrador de ticket (título, descripción y mensaje de error). */
export const prepararBorradorEvidencia = (evidencia: EvidenciaReproduccion) =>
  api<AsistenteUsuarioAccion>('/api/asistente/usuario/evidencia/borrador', {
    cuerpo: evidencia,
    error: 'No fue posible preparar el ticket.',
  })
