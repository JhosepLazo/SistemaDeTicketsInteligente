/**
 * Archivo: notificacionesApi.ts
 * Objetivo: Comunicar la campana del portal con las notificaciones del usuario autenticado.
 * Responsabilidad: Consultar avisos recientes y marcarlos como leídos sin enviar identidad desde el frontend.
 * Dependencias: api.ts (cliente único de la API).
 * Flujo: NotificacionesCampana -> notificacionesApi -> /api/notificaciones.
 * Consideraciones: La ruta de una notificación es orientativa; cada módulo destino mantiene su propia autorización.
 */

import { crearApi } from './api'

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

export interface NotificacionesRespuesta {
  noLeidas: number
  notificaciones: NotificacionItem[]
}

const api = crearApi({ conexion: 'No fue posible comunicarse con el servicio de notificaciones.' })
const ERROR = 'No fue posible consultar las notificaciones.'

export const obtenerNotificaciones = () => api<NotificacionesRespuesta>('/api/notificaciones', { error: ERROR })
export const marcarNotificacionLeida = (id: number) => api(`/api/notificaciones/${id}/leida`, { metodo: 'POST', error: ERROR })
