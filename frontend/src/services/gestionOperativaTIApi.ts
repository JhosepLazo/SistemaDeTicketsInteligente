/**
 * Archivo: gestionOperativaTIApi.ts
 * Objetivo: Registrar la operación diaria de TI sobre los tickets: avances detallados, aprobaciones y mesa de ayuda.
 * Responsabilidad: Consultar los catálogos operativos y enviar cada acción al endpoint correspondiente.
 * Dependencias: api.ts (cliente único de la API).
 * Flujo: GestionTicketsTIPage / AsistenteTIPage -> gestionOperativaTIApi -> /api/gestion-operativa-ti.
 * Consideraciones: El responsable y su área salen de la cookie en el backend.
 */

import { crearApi } from './api'

export interface GestionOperativaCatalogoItem {
  codigo: string
  descripcion: string
}

export interface GestionOperativaUsuario extends GestionOperativaCatalogoItem {
  area: string
}

export interface GestionOperativaAccion {
  accionCodigo: string
  nombre: string
  nivelRiesgo: string
}

export interface GestionOperativaTIDatos {
  usuarios: GestionOperativaUsuario[]
  accionesAprobacion: GestionOperativaAccion[]
  lineas: GestionOperativaCatalogoItem[]
  tipos: GestionOperativaCatalogoItem[]
}

export interface RegistrarAvanceDetalladoSolicitud {
  detalle: string
  visibleUsuario: boolean
  tiempoUtilizadoMinutos: number
  areaCausante: string
}

export interface SolicitarAprobacionOperativaSolicitud {
  accionCodigo: string
  justificacion: string
}

export interface CrearTicketMesaAyudaSolicitud {
  usuarioSolicitante: string
  linea: string
  tipo: string
  titulo: string
  detalle: string
  mensajeError?: string | null
}

export interface TicketMesaAyudaCreado {
  incidenciaNumero: string
  fechaRegistro: string
}

const api = crearApi({ sinPermiso: 'Tu perfil no tiene acceso a esta operación.' })
const ERROR = 'No fue posible completar la operación solicitada.'
const ticket = (incidenciaNumero: string, ruta: string) =>
  `/api/gestion-operativa-ti/tickets/${encodeURIComponent(incidenciaNumero)}/${ruta}`

export const obtenerDatosGestionOperativaTI = () => api<GestionOperativaTIDatos>('/api/gestion-operativa-ti/datos', { error: ERROR })
export const registrarAvanceDetalladoTI = (incidenciaNumero: string, solicitud: RegistrarAvanceDetalladoSolicitud) =>
  api(ticket(incidenciaNumero, 'avances'), { cuerpo: solicitud, error: ERROR })
export const solicitarAprobacionOperativaTI = (incidenciaNumero: string, solicitud: SolicitarAprobacionOperativaSolicitud) =>
  api(ticket(incidenciaNumero, 'solicitar-aprobacion'), { cuerpo: solicitud, error: ERROR })
export const crearTicketMesaAyudaTI = (solicitud: CrearTicketMesaAyudaSolicitud) =>
  api<TicketMesaAyudaCreado>('/api/gestion-operativa-ti/tickets/mesa-ayuda', { cuerpo: solicitud, error: ERROR })
