/**
 * Archivo: edicionTicketUsuarioService.ts
 * Objetivo: Comunicar Mis Tickets con la edición previa al procesamiento técnico.
 * Responsabilidad: Enviar únicamente los campos descriptivos que el usuario puede corregir.
 * Dependencias: Fetch API y sesión autenticada mediante cookie HttpOnly.
 * Flujo: MisTicketsUsuarioPage -> edicionTicketUsuarioService -> API /api/mis-tickets/{ticket}/editar.
 * Consideraciones: El backend valida nuevamente propiedad y estado; prioridad y clasificación técnica nunca se envían.
 */

import { rechazarSiSesionExpirada } from '../../autenticacion/services/sesionExpirada'
import { peticionHttp } from '../../../shared/services/peticionHttp'

export interface EditarTicketUsuarioSolicitud { linea: string; tipo: string; titulo: string; detalle: string; mensajeError?: string | null }

export async function editarTicketUsuario(ticket: string, solicitud: EditarTicketUsuarioSolicitud) {
  const respuesta = await peticionHttp(`/api/mis-tickets/${encodeURIComponent(ticket)}/editar`, {
    method: 'POST', credentials: 'include', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(solicitud),
  }, 'No fue posible comunicarse con el sistema para actualizar el ticket.')
  rechazarSiSesionExpirada(respuesta)
  if (respuesta.status === 403) throw new Error('Tu perfil no puede editar este ticket.')
  if (!respuesta.ok) {
    const datos = await respuesta.json().catch(() => null) as { mensaje?: string } | null
    throw new Error(datos?.mensaje || 'No fue posible actualizar el ticket.')
  }
}
