/**
 * Archivo: recursosSoporteService.ts
 * Objetivo: Comunicar Nuevo Ticket con formatos frecuentes y artículos de ayuda publicados.
 * Responsabilidad: Consultar recursos estáticos reutilizables sin introducir lógica de IA.
 * Dependencias: Fetch API y sesión autenticada mediante cookie HttpOnly.
 * Flujo: NuevoTicketPage -> recursosSoporteService -> API /api/recursos-soporte.
 * Consideraciones: Las descargas se realizan por código autorizado; el frontend no conoce rutas físicas.
 */

import { rechazarSiSesionExpirada } from '../../autenticacion/services/sesionExpirada'
import { peticionHttp } from '../../../shared/services/peticionHttp'

export interface RecursoFormato { formatoCodigo: string; titulo: string; descripcion: string; nombreOriginal: string; tipoMime: string; tipoTicket: string }
export interface RecursoArticulo { conocimientoCodigo: string; titulo: string; problema: string; sintomas: string; solucion: string; procedimiento: string; tipo: string }
export interface RecursosSoporteRespuesta { formatos: RecursoFormato[]; articulos: RecursoArticulo[] }

export async function obtenerRecursosSoporte() {
  const respuesta = await peticionHttp('/api/recursos-soporte', { credentials: 'include' }, 'No fue posible comunicarse con los recursos de soporte.')
  rechazarSiSesionExpirada(respuesta)
  if (!respuesta.ok) throw new Error('No fue posible cargar los recursos de soporte.')
  return respuesta.json() as Promise<RecursosSoporteRespuesta>
}

export const urlFormatoSoporte = (codigo: string) => `/api/recursos-soporte/formatos/${encodeURIComponent(codigo)}`
