/**
 * Archivo: misTicketsUsuarioService.ts
 * Objetivo: Comunicar la vista Mis Tickets con los endpoints protegidos del usuario.
 * Responsabilidad: Consultar bandeja/detalle, responder observaciones, validar soluciones, calificar y construir la ruta segura de descarga de adjuntos.
 * Dependencias: Fetch API y sesión autenticada mediante cookie HttpOnly.
 * Flujo: MisTicketsUsuarioPage -> misTicketsUsuarioService -> API /api/mis-tickets.
 * Consideraciones: No envía la identidad del usuario; el backend la obtiene desde la sesión autenticada y valida nuevamente la propiedad del ticket.
 */

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

async function procesarRespuesta(respuesta: Response, mensajePredeterminado: string) {
  if (respuesta.status === 401) throw new Error('Tu sesión ya no se encuentra disponible. Vuelve a iniciar sesión.')
  if (respuesta.ok) return

  try {
    const datos = await respuesta.json() as { mensaje?: string }
    throw new Error(datos.mensaje || mensajePredeterminado)
  } catch (error) {
    if (error instanceof Error && error.message !== 'Unexpected end of JSON input') throw error
    throw new Error(mensajePredeterminado)
  }
}

export async function obtenerMisTickets(): Promise<MisTicketsRespuesta> {
  let respuesta: Response
  try {
    respuesta = await fetch('/api/mis-tickets', { credentials: 'include' })
  } catch {
    throw new Error('No fue posible comunicarse con el sistema. Intenta nuevamente en unos momentos.')
  }

  await procesarRespuesta(respuesta, 'No fue posible cargar tus tickets.')
  return respuesta.json() as Promise<MisTicketsRespuesta>
}

export async function obtenerDetalleMisTicket(incidenciaNumero: string): Promise<MisTicketDetalle> {
  const respuesta = await fetch(`/api/mis-tickets/${encodeURIComponent(incidenciaNumero)}`, { credentials: 'include' })
  await procesarRespuesta(respuesta, 'No fue posible cargar el detalle del ticket.')
  return respuesta.json() as Promise<MisTicketDetalle>
}

export async function responderObservacion(incidenciaNumero: string, contenido: string, adjuntos: File[]): Promise<void> {
  const datos = new FormData()
  datos.append('contenido', contenido)
  adjuntos.forEach(archivo => datos.append('adjuntos', archivo))

  const respuesta = await fetch(`/api/mis-tickets/${encodeURIComponent(incidenciaNumero)}/responder-observacion`, {
    method: 'POST',
    credentials: 'include',
    body: datos,
  })
  await procesarRespuesta(respuesta, 'No fue posible enviar la información solicitada.')
}

export async function validarSolucion(incidenciaNumero: string, solucionada: boolean, comentario: string): Promise<void> {
  const respuesta = await fetch(`/api/mis-tickets/${encodeURIComponent(incidenciaNumero)}/validar-solucion`, {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ solucionada, comentario }),
  })
  await procesarRespuesta(respuesta, 'No fue posible registrar la validación de la solución.')
}

export async function calificarTicket(incidenciaNumero: string, calificacion: number, comentario: string): Promise<void> {
  const respuesta = await fetch(`/api/mis-tickets/${encodeURIComponent(incidenciaNumero)}/calificar`, {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ calificacion, comentario }),
  })
  await procesarRespuesta(respuesta, 'No fue posible registrar la calificación.')
}

export function urlAdjunto(incidenciaNumero: string, secuencia: number) {
  return `/api/mis-tickets/${encodeURIComponent(incidenciaNumero)}/adjuntos/${secuencia}`
}
