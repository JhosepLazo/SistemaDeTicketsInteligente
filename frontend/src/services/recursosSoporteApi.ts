/**
 * Archivo: recursosSoporteApi.ts
 * Objetivo: Ofrecer a Nuevo Ticket los formatos frecuentes y los artículos de ayuda publicados.
 * Responsabilidad: Consultar los recursos de autoservicio y armar la ruta de descarga de un formato.
 * Dependencias: api.ts (cliente único de la API).
 * Flujo: NuevoTicketPage -> recursosSoporteApi -> /api/recursos-soporte.
 * Consideraciones: Las descargas se piden por código autorizado; el frontend no conoce rutas físicas.
 */

import { crearApi } from './api'

export interface RecursoFormato {
  formatoCodigo: string
  titulo: string
  descripcion: string
  nombreOriginal: string
  tipoMime: string
  tipoTicket: string
}

export interface RecursoArticulo {
  conocimientoCodigo: string
  titulo: string
  problema: string
  sintomas: string
  solucion: string
  procedimiento: string
  tipo: string
}

export interface RecursosSoporteRespuesta {
  formatos: RecursoFormato[]
  articulos: RecursoArticulo[]
}

const api = crearApi({ conexion: 'No fue posible comunicarse con los recursos de soporte.' })

export const obtenerRecursosSoporte = () =>
  api<RecursosSoporteRespuesta>('/api/recursos-soporte', { error: 'No fue posible cargar los recursos de soporte.' })

export const urlFormatoSoporte = (codigo: string) => `/api/recursos-soporte/formatos/${encodeURIComponent(codigo)}`
