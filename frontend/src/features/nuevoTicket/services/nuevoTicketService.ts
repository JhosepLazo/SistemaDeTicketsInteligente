/**
 * Archivo: nuevoTicketService.ts
 * Objetivo: Comunicar la pantalla Nuevo Ticket con los endpoints protegidos del backend.
 * Responsabilidad: Obtener datos iniciales del formulario y enviar una nueva incidencia con sus evidencias mediante FormData.
 * Dependencias: Fetch API y sesión autenticada mediante cookie HttpOnly.
 * Flujo: NuevoTicketPage -> nuevoTicketService -> API /api/tickets/nuevo.
 * Consideraciones: El usuario y área nunca se envían como datos confiables; el backend los obtiene desde la sesión autenticada.
 */

import { rechazarSiSesionExpirada } from '../../autenticacion/services/sesionExpirada'

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

async function obtenerMensajeError(respuesta: Response, mensajePredeterminado: string) {
  try {
    const cuerpo = await respuesta.json() as { mensaje?: string }
    return cuerpo.mensaje || mensajePredeterminado
  } catch {
    return mensajePredeterminado
  }
}

export async function obtenerDatosNuevoTicket(): Promise<NuevoTicketDatos> {
  let respuesta: Response

  try {
    respuesta = await fetch('/api/tickets/nuevo/datos', { method: 'GET', credentials: 'include' })
  } catch {
    throw new Error('No fue posible comunicarse con el sistema. Intenta nuevamente en unos momentos.')
  }

  rechazarSiSesionExpirada(respuesta)
  if (respuesta.status === 403) throw new Error('Tu perfil no tiene acceso al registro de tickets de usuario.')
  if (!respuesta.ok) throw new Error(await obtenerMensajeError(respuesta, 'No fue posible cargar los datos del formulario.'))

  return respuesta.json() as Promise<NuevoTicketDatos>
}

export async function crearNuevoTicket(formulario: NuevoTicketFormulario): Promise<NuevoTicketCreado> {
  const datos = new FormData()
  datos.append('Linea', formulario.linea)
  datos.append('Tipo', formulario.tipo)
  datos.append('Titulo', formulario.titulo)
  datos.append('Detalle', formulario.detalle)
  datos.append('MensajeError', formulario.mensajeError)
  formulario.adjuntos.forEach(archivo => datos.append('Adjuntos', archivo))

  let respuesta: Response

  try {
    respuesta = await fetch('/api/tickets/nuevo', { method: 'POST', credentials: 'include', body: datos })
  } catch {
    throw new Error('No fue posible enviar el ticket. Verifica tu conexión e intenta nuevamente.')
  }

  rechazarSiSesionExpirada(respuesta)
  if (respuesta.status === 403) throw new Error('Tu perfil no tiene permiso para registrar este ticket.')
  if (!respuesta.ok) throw new Error(await obtenerMensajeError(respuesta, 'No fue posible registrar el ticket.'))

  return respuesta.json() as Promise<NuevoTicketCreado>
}
