/**
 * Archivo: gestionTicketsTIService.ts
 * Objetivo: Comunicar el módulo Gestión de Tickets TI con sus endpoints protegidos del backend.
 * Responsabilidad: Exponer contratos TypeScript y funciones simples para consultar bandeja/detalle y ejecutar las acciones operativas permitidas.
 * Dependencias: Fetch API y sesión autenticada mediante cookie HttpOnly.
 * Flujo: GestionTicketsTIPage -> gestionTicketsTIService -> API /api/gestion-tickets.
 * Consideraciones: No envía usuario, área ni perfil; la identidad y autorización se resuelven en el backend desde la sesión autenticada.
 */

import { rechazarSiSesionExpirada } from '../../autenticacion/services/sesionExpirada'

export interface GestionTicketsTIResumen {
  pendientes: number
  enAtencion: number
  porVencer: number
  reabiertos: number
  sinAsignar: number
  misAsignados: number
  prioridadAlta: number
  aprobacionesPendientes: number
  total: number
}

export interface GestionTicketsTIItem {
  incidenciaNumero: string
  usuarioSolicitante: string
  solicitante: string
  titulo: string
  detalle: string
  areaSolicitante: string
  areaDescripcion: string
  linea: string
  lineaDescripcion: string
  tipo: string
  tipoDescripcion: string
  estado: string
  estadoDescripcion: string
  prioridad: number | null
  impacto: number | null
  complejidad: number | null
  usuarioTI: string
  responsable: string
  fechaRegistro: string
  ultimaFechaModif: string
  slaMinutosRestantes: number | null
  tieneAprobacionPendiente: boolean
  accion: string
}

export interface CatalogoItem {
  codigo: string
  descripcion: string
}

export interface LineaCatalogo extends CatalogoItem {
  area: string
}

export interface ItemCatalogo extends CatalogoItem {
  linea: string
}

export interface SubTipoCatalogo extends CatalogoItem {
  tipo: string
  categoria: string
}

export interface GestionTicketsTICatalogos {
  estados: CatalogoItem[]
  areas: CatalogoItem[]
  operadores: CatalogoItem[]
  lineas: LineaCatalogo[]
  items: ItemCatalogo[]
  tipos: CatalogoItem[]
  categorias: CatalogoItem[]
  subTipos: SubTipoCatalogo[]
}

export interface GestionTicketsTIRespuesta {
  resumen: GestionTicketsTIResumen
  tickets: GestionTicketsTIItem[]
  catalogos: GestionTicketsTICatalogos
}

export interface GestionTicketTIEstado {
  secuencia: number
  estado: string
  estadoDescripcion: string
  actor: string
  fechaCambio: string
  observacion: string
}

export interface GestionTicketTIAvance {
  secuencia: number
  usuarioTI: string
  responsable: string
  fechaAvance: string
  detalle: string
  tiempoUtilizado: number | null
  porcentajeAvance: number | null
}

export interface GestionTicketTIMensaje {
  secuencia: number
  tipoAutor: string
  autor: string
  contenido: string
  fechaMensaje: string
  esInterno: boolean
}

export interface GestionTicketTIAdjunto {
  secuencia: number
  mensajeSecuencia: number | null
  nombreOriginal: string
  tipoMime: string
  tamanoBytes: number
  fechaRegistro: string
  usuarioRegistro: string
}

export interface GestionTicketTIDocumento {
  secuencia: number
  companiaSocio: string
  tipoDocumento: string
  numeroDocumento: string
  descripcion: string
}

export interface GestionTicketTIAprobacion {
  secuencia: number
  accionCodigo: string
  accionNombre: string
  nivelRiesgo: string
  estado: string
  justificacion: string
  comentarioRespuesta: string
  usuarioSolicitante: string
  solicitante: string
  usuarioAprobador: string
  aprobador: string
  fechaSolicitud: string
  fechaRespuesta: string | null
}

export interface GestionTicketTIDetalle {
  incidenciaNumero: string
  usuarioSolicitante: string
  solicitante: string
  correoSolicitante: string
  areaSolicitante: string
  areaSolicitanteDescripcion: string
  areaTI: string
  areaTIDescripcion: string
  usuarioTI: string
  responsable: string
  usuarioAsigno: string
  linea: string
  lineaDescripcion: string
  item: string
  itemDescripcion: string
  tipo: string
  tipoDescripcion: string
  subTipo: string
  subTipoDescripcion: string
  categoria: string
  categoriaDescripcion: string
  estado: string
  estadoDescripcion: string
  areaCausante: string
  areaCausanteDescripcion: string
  titulo: string
  detalle: string
  mensajeError: string
  fechaRegistro: string
  fechaAsignacion: string | null
  fechaAtencion: string | null
  fechaCierre: string | null
  slaObjetivoMinutos: number | null
  prioridad: number | null
  impacto: number | null
  complejidad: number | null
  canalRegistro: string
  causaRaiz: string
  solucionTecnica: string
  respuestaUsuario: string
  tipoResolucion: string
  calificacion: number | null
  comentarioCalificacion: string
  ultimaFechaModif: string
  slaMinutosRestantes: number | null
  historialEstados: GestionTicketTIEstado[]
  avances: GestionTicketTIAvance[]
  mensajes: GestionTicketTIMensaje[]
  adjuntos: GestionTicketTIAdjunto[]
  documentos: GestionTicketTIDocumento[]
  aprobaciones: GestionTicketTIAprobacion[]
}

export interface ClasificarTicketTISolicitud {
  linea: string
  item: string
  tipo: string
  subTipo: string
  categoria: string
  areaCausante: string | null
  prioridad: number | null
  impacto: number | null
  complejidad: number | null
}

export interface ResolverTicketTISolicitud {
  causaRaiz: string
  solucion: string
  respuestaUsuario: string
  tipoResolucion: string
}

async function solicitar<T>(url: string, opciones?: RequestInit): Promise<T> {
  let respuesta: Response

  try {
    respuesta = await fetch(url, { credentials: 'include', ...opciones })
  } catch {
    throw new Error('No fue posible comunicarse con el sistema. Intenta nuevamente en unos momentos.')
  }

  rechazarSiSesionExpirada(respuesta)
  if (respuesta.status === 403) throw new Error('Tu perfil no tiene acceso a Gestión de Tickets.')

  if (!respuesta.ok) {
    const datos = await respuesta.json().catch(() => null) as { mensaje?: string } | null
    throw new Error(datos?.mensaje || 'No fue posible completar la operación solicitada.')
  }

  if (respuesta.status === 204) return undefined as T
  return respuesta.json() as Promise<T>
}

function post(url: string, body: unknown) {
  return solicitar<void>(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
}

export function obtenerGestionTicketsTI() {
  return solicitar<GestionTicketsTIRespuesta>('/api/gestion-tickets')
}

export function obtenerDetalleGestionTicketTI(incidenciaNumero: string) {
  return solicitar<GestionTicketTIDetalle>(`/api/gestion-tickets/${encodeURIComponent(incidenciaNumero)}`)
}

export function clasificarTicketTI(incidenciaNumero: string, solicitud: ClasificarTicketTISolicitud) {
  return post(`/api/gestion-tickets/${encodeURIComponent(incidenciaNumero)}/clasificar`, solicitud)
}

export function asignarTicketTI(incidenciaNumero: string, usuarioTI: string) {
  return post(`/api/gestion-tickets/${encodeURIComponent(incidenciaNumero)}/asignar`, { usuarioTI })
}

export function registrarAvanceTI(incidenciaNumero: string, detalle: string, visibleUsuario: boolean) {
  return post(`/api/gestion-tickets/${encodeURIComponent(incidenciaNumero)}/avances`, { detalle, visibleUsuario })
}

export function solicitarInformacionTI(incidenciaNumero: string, mensaje: string) {
  return post(`/api/gestion-tickets/${encodeURIComponent(incidenciaNumero)}/solicitar-informacion`, { mensaje })
}

export function resolverTicketTI(incidenciaNumero: string, solicitud: ResolverTicketTISolicitud) {
  return post(`/api/gestion-tickets/${encodeURIComponent(incidenciaNumero)}/resolver`, solicitud)
}

export function marcarNoProcedeTI(incidenciaNumero: string, motivo: string) {
  return post(`/api/gestion-tickets/${encodeURIComponent(incidenciaNumero)}/no-procede`, { motivo })
}

export function responderAprobacionTI(incidenciaNumero: string, secuencia: number, aprobar: boolean, comentario: string) {
  return post(`/api/gestion-tickets/${encodeURIComponent(incidenciaNumero)}/aprobaciones/${secuencia}`, { aprobar, comentario })
}

export function urlAdjuntoGestionTI(incidenciaNumero: string, secuencia: number) {
  return `/api/gestion-tickets/${encodeURIComponent(incidenciaNumero)}/adjuntos/${secuencia}`
}
