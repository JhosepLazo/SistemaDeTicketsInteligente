/**
 * Archivo: notificacionesService.ts
 * Objetivo: Comunicar la campana del portal con las notificaciones persistidas del usuario autenticado.
 * Responsabilidad: Consultar avisos recientes y marcar lecturas sin enviar identidad desde el frontend.
 * Dependencias: Fetch API y cookie HttpOnly.
 * Flujo: NotificacionesCampana -> notificacionesService -> API /api/notificaciones.
 * Consideraciones: La ruta de una notificación es orientativa; cada módulo destino mantiene su propia autorización.
 */

import { rechazarSiSesionExpirada } from '../../autenticacion/services/sesionExpirada'
import { peticionHttp } from '../../../shared/services/peticionHttp'

export interface NotificacionItem {
  notificacionNumero: number
  incidenciaNumero: string
  tipo: string
  titulo: string
  mensaje: string
  ruta: string
  leida: boolean
  fecha: string
}
export interface NotificacionesRespuesta { noLeidas: number; notificaciones: NotificacionItem[] }

async function solicitar<T>(url: string, opciones?: RequestInit): Promise<T> {
  const respuesta = await peticionHttp(url, { credentials: 'include', ...opciones }, 'No fue posible comunicarse con el servicio de notificaciones.')
  rechazarSiSesionExpirada(respuesta)
  if (!respuesta.ok) {
    const datos = await respuesta.json().catch(() => null) as { mensaje?: string } | null
    throw new Error(datos?.mensaje || 'No fue posible consultar las notificaciones.')
  }
  if (respuesta.status === 204) return undefined as T
  return respuesta.json() as Promise<T>
}

export const obtenerNotificaciones = () => solicitar<NotificacionesRespuesta>('/api/notificaciones')
export const marcarNotificacionLeida = (id: number) => solicitar<void>(`/api/notificaciones/${id}/leida`, { method: 'POST' })
