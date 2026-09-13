/**
 * Archivo: gestionOperativaTIService.ts
 * Objetivo: Comunicar Gestión de Tickets con las mejoras operativas complementarias que no requieren IA.
 * Responsabilidad: Consultar usuarios/acciones y registrar esfuerzo, aprobaciones manuales o tickets creados por mesa de ayuda.
 * Dependencias: Fetch API y sesión autenticada mediante cookie HttpOnly.
 * Flujo: GestionTicketsTIPage -> gestionOperativaTIService -> API /api/gestion-operativa-ti.
 * Consideraciones: No envía identidad del operador; el backend la obtiene de la sesión autenticada.
 */

export interface GestionOperativaCatalogoItem { codigo: string; descripcion: string }
export interface GestionOperativaUsuario extends GestionOperativaCatalogoItem { area: string }
export interface GestionOperativaAccion { accionCodigo: string; nombre: string; nivelRiesgo: string }
export interface GestionOperativaTIDatos {
  usuarios: GestionOperativaUsuario[]
  accionesAprobacion: GestionOperativaAccion[]
  lineas: GestionOperativaCatalogoItem[]
  tipos: GestionOperativaCatalogoItem[]
}
export interface RegistrarAvanceDetalladoSolicitud { detalle: string; visibleUsuario: boolean; tiempoUtilizadoMinutos: number; areaCausante: string }
export interface SolicitarAprobacionOperativaSolicitud { accionCodigo: string; justificacion: string }
export interface CrearTicketMesaAyudaSolicitud { usuarioSolicitante: string; linea: string; tipo: string; titulo: string; detalle: string; mensajeError?: string | null }
export interface TicketMesaAyudaCreado { incidenciaNumero: string; fechaRegistro: string }

async function solicitar<T>(url: string, opciones?: RequestInit): Promise<T> {
  let respuesta: Response
  try { respuesta = await fetch(url, { credentials: 'include', ...opciones }) }
  catch { throw new Error('No fue posible comunicarse con el sistema. Intenta nuevamente en unos momentos.') }

  if (respuesta.status === 401) throw new Error('Tu sesión ya no se encuentra disponible. Vuelve a iniciar sesión.')
  if (respuesta.status === 403) throw new Error('Tu perfil no tiene acceso a esta operación.')
  if (!respuesta.ok) {
    const datos = await respuesta.json().catch(() => null) as { mensaje?: string } | null
    throw new Error(datos?.mensaje || 'No fue posible completar la operación solicitada.')
  }
  if (respuesta.status === 204) return undefined as T
  return respuesta.json() as Promise<T>
}

function post<T>(url: string, body: unknown) {
  return solicitar<T>(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) })
}

export const obtenerDatosGestionOperativaTI = () => solicitar<GestionOperativaTIDatos>('/api/gestion-operativa-ti/datos')
export const registrarAvanceDetalladoTI = (ticket: string, solicitud: RegistrarAvanceDetalladoSolicitud) => post<void>(`/api/gestion-operativa-ti/tickets/${encodeURIComponent(ticket)}/avances`, solicitud)
export const solicitarAprobacionOperativaTI = (ticket: string, solicitud: SolicitarAprobacionOperativaSolicitud) => post<void>(`/api/gestion-operativa-ti/tickets/${encodeURIComponent(ticket)}/solicitar-aprobacion`, solicitud)
export const crearTicketMesaAyudaTI = (solicitud: CrearTicketMesaAyudaSolicitud) => post<TicketMesaAyudaCreado>('/api/gestion-operativa-ti/tickets/mesa-ayuda', solicitud)
