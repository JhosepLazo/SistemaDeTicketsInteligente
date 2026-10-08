/**
 * Archivo: gestionTicketsTIApi.ts
 * Objetivo: Comunicar la bandeja de Gestión de Tickets con la API de TI.
 * Responsabilidad: Consultar la bandeja, el detalle y la ficha, clasificar (con propuesta opcional de la IA e historial), asignar, pedir
 *   información, resolver, marcar no procede, responder aprobaciones y armar la ruta de descarga de adjuntos.
 * Dependencias: api.ts (cliente único de la API) y fichaTicketService.ts (tipo de la ficha registrada).
 * Flujo: GestionTicketsTIPage / AsistenteTIPage -> gestionTicketsTIApi -> /api/gestion-tickets.
 * Consideraciones: Los avances se registran con gestionOperativaTIApi (incluyen tiempo y porcentaje). La propuesta de la IA no cambia
 *   el ticket: queda en el historial (origen I) hasta que TI guarda la clasificación (origen T).
 */

import { crearApi } from './api'
import type { DatoFichaTicket } from './fichaTicketService'

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
  parametrosJson: string
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

/** Clasificación del historial: propuesta de la IA (origen I) o aplicada por TI con la matriz (origen T). */
export interface ClasificacionTicketTI {
  secuencia: number
  origen: 'I' | 'T'
  linea: string
  lineaDescripcion: string
  item: string
  itemDescripcion: string
  tipo: string
  subTipo: string
  subTipoDescripcion: string
  categoria: string
  prioridad: number | null
  impacto: number | null
  complejidad: number | null
  confianza: number | null
  senales: string[]
  justificacion: string
  preguntasPendientes: string[]
  modelo: string
  usuario: string
  nombreUsuario: string
  fechaClasificacion: string
}

export interface ResolverTicketTISolicitud {
  causaRaiz: string
  solucion: string
  respuestaUsuario: string
  tipoResolucion: string
}

const api = crearApi({ sinPermiso: 'Tu perfil no tiene acceso a Gestión de Tickets.' })
const apiAsistida = crearApi({
  sinPermiso: 'Tu perfil no tiene acceso a Gestión de Tickets.',
  saturado: 'Se pidieron varias propuestas seguidas. Espera un momento y vuelve a intentarlo.',
})
const ERROR = 'No fue posible completar la operación solicitada.'
const ticket = (incidenciaNumero: string, ruta = '') => `/api/gestion-tickets/${encodeURIComponent(incidenciaNumero)}${ruta}`

export const obtenerGestionTicketsTI = () => api<GestionTicketsTIRespuesta>('/api/gestion-tickets', { error: ERROR })
export const obtenerDetalleGestionTicketTI = (incidenciaNumero: string) =>
  api<GestionTicketTIDetalle>(ticket(incidenciaNumero), { error: ERROR })
export const clasificarTicketTI = (incidenciaNumero: string, solicitud: ClasificarTicketTISolicitud) =>
  api(ticket(incidenciaNumero, '/clasificar'), { cuerpo: solicitud, error: ERROR })
export const asignarTicketTI = (incidenciaNumero: string, usuarioTI: string) =>
  api(ticket(incidenciaNumero, '/asignar'), { cuerpo: { usuarioTI }, error: ERROR })
export const solicitarInformacionTI = (incidenciaNumero: string, mensaje: string) =>
  api(ticket(incidenciaNumero, '/solicitar-informacion'), { cuerpo: { mensaje }, error: ERROR })
export const resolverTicketTI = (incidenciaNumero: string, solicitud: ResolverTicketTISolicitud) =>
  api(ticket(incidenciaNumero, '/resolver'), { cuerpo: solicitud, error: ERROR })
export const marcarNoProcedeTI = (incidenciaNumero: string, motivo: string) =>
  api(ticket(incidenciaNumero, '/no-procede'), { cuerpo: { motivo }, error: ERROR })
export const responderAprobacionTI = (incidenciaNumero: string, secuencia: number, aprobar: boolean, comentario: string) =>
  api(ticket(incidenciaNumero, `/aprobaciones/${secuencia}`), { cuerpo: { aprobar, comentario }, error: ERROR })
export const urlAdjuntoGestionTI = (incidenciaNumero: string, secuencia: number) => ticket(incidenciaNumero, `/adjuntos/${secuencia}`)
export const obtenerFichaTI = (incidenciaNumero: string) => api<DatoFichaTicket[]>(ticket(incidenciaNumero, '/ficha'), { error: ERROR })
export const obtenerClasificacionesTI = (incidenciaNumero: string) =>
  api<ClasificacionTicketTI[]>(ticket(incidenciaNumero, '/clasificaciones'), { error: ERROR })
/** La IA propone una clasificación validada contra el catálogo; no cambia el ticket. */
export const proponerClasificacionTI = (incidenciaNumero: string) =>
  apiAsistida<ClasificacionTicketTI>(ticket(incidenciaNumero, '/clasificacion/proponer'), {
    metodo: 'POST',
    error: 'No fue posible proponer una clasificación. Clasifica el ticket manualmente.',
  })
