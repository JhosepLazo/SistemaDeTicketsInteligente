/**
 * Archivo: nuevoTicketApi.ts
 * Objetivo: Comunicar Nuevo Ticket con la API de registro de incidencias del colaborador.
 * Responsabilidad: Obtener los catálogos del formulario (con la ficha de cada tipo) y registrar el ticket con sus adjuntos, su
 *   ficha y, si existe, la evidencia que el colaborador mostró en pantalla al Asistente TI.
 * Dependencias: api.ts (cliente único de la API) y fichaTicketService.ts (tipos de la ficha).
 * Flujo: NuevoTicketPage -> nuevoTicketApi -> /api/tickets/nuevo.
 * Consideraciones: La evidencia del asistente activa la investigación automática de TI en el backend. La ficha viaja como JSON en
 *   FichaJson y el backend la valida con las mismas reglas que el formulario.
 */

import { crearApi } from './api'
import type { CampoFichaTicket } from './fichaTicketService'

export interface NuevoTicketCatalogoItem {
  codigo: string
  descripcion: string
}

export interface NuevoTicketDatos {
  usuario: string
  nombreCompleto: string
  area: string
  areaDescripcion: string
  lineas: NuevoTicketCatalogoItem[]
  tipos: NuevoTicketCatalogoItem[]
  /** Campos activos de la ficha de cada tipo; el formulario muestra los del tipo elegido. */
  ficha: CampoFichaTicket[]
}

export interface NuevoTicketFormulario {
  linea: string
  tipo: string
  titulo: string
  detalle: string
  mensajeError: string
  adjuntos: File[]
}

export interface NuevoTicketCreado {
  incidenciaNumero: string
  fechaRegistro: string
}

const api = crearApi({ sinPermiso: 'Tu perfil no tiene acceso al registro de tickets de usuario.' })
const apiRegistro = crearApi({
  conexion: 'No fue posible enviar el ticket. Verifica tu conexión e intenta nuevamente.',
  sinPermiso: 'Tu perfil no tiene permiso para registrar este ticket.',
})

export const obtenerDatosNuevoTicket = () =>
  api<NuevoTicketDatos>('/api/tickets/nuevo/datos', { error: 'No fue posible cargar los datos del formulario.' })

/**
 * evidenciaAsistente: pasos, error y conversación que el colaborador mostró en pantalla; activa la investigación automática de TI.
 * ficha: JSON con las respuestas de la ficha del tipo (fichaTicketService.fichaJson).
 */
export function crearNuevoTicket(formulario: NuevoTicketFormulario, evidenciaAsistente?: unknown, ficha?: string) {
  const datos = new FormData()
  if (evidenciaAsistente) datos.append('EvidenciaAsistenteJson', JSON.stringify(evidenciaAsistente))
  if (ficha) datos.append('FichaJson', ficha)
  datos.append('Linea', formulario.linea)
  datos.append('Tipo', formulario.tipo)
  datos.append('Titulo', formulario.titulo)
  datos.append('Detalle', formulario.detalle)
  datos.append('MensajeError', formulario.mensajeError)
  formulario.adjuntos.forEach(archivo => datos.append('Adjuntos', archivo))
  return apiRegistro<NuevoTicketCreado>('/api/tickets/nuevo', { cuerpo: datos, error: 'No fue posible registrar el ticket.' })
}
