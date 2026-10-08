/**
 * Archivo: misTicketsApi.ts
 * Objetivo: Comunicar Mis Tickets con los servicios del colaborador sobre sus propios tickets.
 * Responsabilidad: Consultar bandeja y detalle, responder observaciones con adjuntos, validar la solución, calificar, armar la ruta
 *   de descarga de adjuntos y editar un ticket antes de que TI lo procese.
 * Dependencias: api.ts (cliente único de la API) y fichaTicketService.ts (tipo de la ficha registrada).
 * Flujo: MisTicketsUsuarioPage / AsistenteUsuarioPage -> misTicketsApi -> /api/mis-tickets.
 * Consideraciones: El backend obtiene la identidad de la cookie y valida que el ticket sea del usuario. La edición temprana tiene API
 *   pero todavía no tiene pantalla (pendiente).
 */

import { crearApi } from './api'
import type { DatoFichaTicket } from './fichaTicketService'

export interface MisTicketsResumen {
  activos: number
  enAtencion: number
  pendientesRespuesta: number
  resueltos30Dias: number
}

export interface MisTicketItem {
  incidenciaNumero: string
  titulo: string
  detalle: string
  estado: string
  estadoDescripcion: string
  responsable: string
  fechaRegistro: string
  ultimaFechaModif: string
  prioridad: number | null
  accion: string
}

export interface MisTicketsRespuesta {
  resumen: MisTicketsResumen
  tickets: MisTicketItem[]
}

export interface MisTicketEstado {
  secuencia: number
  estado: string
  estadoDescripcion: string
  actor: string
  fecha: string
  observacion: string
}

export interface MisTicketAvance {
  secuencia: number
  responsable: string
  fecha: string
  detalle: string
  porcentajeAvance: number | null
}

export interface MisTicketMensaje {
  secuencia: number
  tipoAutor: string
  autor: string
  contenido: string
  fecha: string
}

export interface MisTicketAdjunto {
  secuencia: number
  mensajeSecuencia: number | null
  nombreOriginal: string
  tipoMime: string
  tamanoBytes: number
  fechaRegistro: string
}

export interface MisTicketDocumento {
  secuencia: number
  companiaSocio: string
  tipoDocumento: string
  numeroDocumento: string
  descripcion: string
}

export interface MisTicketDetalle {
  /** Ficha completada al registrar el ticket (vacía si el tipo no tiene ficha). */
  ficha: DatoFichaTicket[]
  incidenciaNumero: string
  titulo: string
  detalle: string
  mensajeError: string
  estado: string
  estadoDescripcion: string
  linea: string
  lineaDescripcion: string
  item: string
  itemDescripcion: string
  tipo: string
  tipoDescripcion: string
  areaDescripcion: string
  responsable: string
  fechaRegistro: string
  fechaAsignacion: string | null
  fechaAtencion: string | null
  fechaCierre: string | null
  ultimaFechaModif: string
  prioridad: number | null
  calificacion: number | null
  comentarioCalificacion: string
  respuestaUsuario: string
  accion: string
  historialEstados: MisTicketEstado[]
  avances: MisTicketAvance[]
  mensajes: MisTicketMensaje[]
  adjuntos: MisTicketAdjunto[]
  documentos: MisTicketDocumento[]
}

export interface EditarTicketUsuarioSolicitud {
  linea: string
  tipo: string
  titulo: string
  detalle: string
  mensajeError?: string | null
}

const api = crearApi()
const ticket = (incidenciaNumero: string, ruta = '') => `/api/mis-tickets/${encodeURIComponent(incidenciaNumero)}${ruta}`

export const obtenerMisTickets = () => api<MisTicketsRespuesta>('/api/mis-tickets', { error: 'No fue posible cargar tus tickets.' })

export const obtenerDetalleMisTicket = (incidenciaNumero: string) =>
  api<MisTicketDetalle>(ticket(incidenciaNumero), { error: 'No fue posible cargar el detalle del ticket.' })

export function responderObservacion(incidenciaNumero: string, contenido: string, adjuntos: File[]) {
  const datos = new FormData()
  datos.append('contenido', contenido)
  adjuntos.forEach(archivo => datos.append('adjuntos', archivo))
  return api(ticket(incidenciaNumero, '/responder-observacion'), {
    cuerpo: datos,
    error: 'No fue posible enviar la información solicitada.',
  })
}

export const validarSolucion = (incidenciaNumero: string, solucionada: boolean, comentario: string) =>
  api(ticket(incidenciaNumero, '/validar-solucion'), {
    cuerpo: { solucionada, comentario },
    error: 'No fue posible registrar la validación de la solución.',
  })

export const calificarTicket = (incidenciaNumero: string, calificacion: number, comentario: string) =>
  api(ticket(incidenciaNumero, '/calificar'), { cuerpo: { calificacion, comentario }, error: 'No fue posible registrar la calificación.' })

export const urlAdjunto = (incidenciaNumero: string, secuencia: number) => ticket(incidenciaNumero, `/adjuntos/${secuencia}`)

const apiEdicion = crearApi({
  conexion: 'No fue posible comunicarse con el sistema para actualizar el ticket.',
  sinPermiso: 'Tu perfil no puede editar este ticket.',
})

/** Corrige los datos descriptivos de un ticket que TI todavía no procesa (pendiente: aún no tiene pantalla). */
export const editarTicketUsuario = (incidenciaNumero: string, solicitud: EditarTicketUsuarioSolicitud) =>
  apiEdicion(ticket(incidenciaNumero, '/editar'), { cuerpo: solicitud, error: 'No fue posible actualizar el ticket.' })
