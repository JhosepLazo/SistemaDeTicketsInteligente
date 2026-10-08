/**
 * Archivo: baseConocimientoTIApi.ts
 * Objetivo: Comunicar la Base de Conocimiento TI con su API.
 * Responsabilidad: Consultar artículos y su detalle, crear y actualizar artículos y moverlos por su ciclo de validación.
 * Dependencias: api.ts (cliente único de la API).
 * Flujo: BaseConocimientoTIPage -> baseConocimientoTIApi -> /api/base-conocimiento.
 * Consideraciones: El backend decide qué transiciones de estado están permitidas para el perfil autenticado.
 */

import { crearApi } from './api'

export interface BaseConocimientoTIResumen {
  total: number
  activos: number
  borradores: number
  pendientesValidacion: number
  porRevisar: number
  candidatosDesdeTickets: number
}

export interface BaseConocimientoTIItem {
  conocimientoCodigo: string
  titulo: string
  problema: string
  solucion: string
  estado: string
  estadoDescripcion: string
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
  incidenciaOrigen: string
  validador: string
  fechaCreacion: string
  fechaValidacion: string | null
  fechaRevision: string | null
  requiereRevision: boolean
}

/** Paso de la guía de diagnóstico: qué revisar, con qué herramienta del agente y qué causa confirma o descarta. */
export interface PasoGuiaDiagnostico {
  paso: string
  herramienta?: string | null
  confirma?: string | null
  descarta?: string | null
}

export interface BaseConocimientoTIDetalle extends BaseConocimientoTIItem {
  sintomas: string
  mensajeError: string
  causa: string
  procedimiento: string
  usuarioValida: string
  guia: PasoGuiaDiagnostico[]
}

export interface BaseConocimientoTICatalogo {
  codigo: string
  descripcion: string
}

export interface BaseConocimientoTIItemCatalogo extends BaseConocimientoTICatalogo {
  linea: string
}

export interface BaseConocimientoTISubTipoCatalogo extends BaseConocimientoTICatalogo {
  tipo: string
  categoria: string
}

export interface BaseConocimientoTITicketOrigen {
  incidenciaNumero: string
  titulo: string
  detalle: string
  mensajeError: string
  linea: string
  item: string
  tipo: string
  subTipo: string
  categoria: string
  causaRaiz: string
  solucionTecnica: string
  fechaCierre: string | null
}

export interface BaseConocimientoTICatalogos {
  lineas: BaseConocimientoTICatalogo[]
  items: BaseConocimientoTIItemCatalogo[]
  tipos: BaseConocimientoTICatalogo[]
  categorias: BaseConocimientoTICatalogo[]
  subTipos: BaseConocimientoTISubTipoCatalogo[]
  ticketsOrigen: BaseConocimientoTITicketOrigen[]
}

export interface BaseConocimientoTIRespuesta {
  resumen: BaseConocimientoTIResumen
  articulos: BaseConocimientoTIItem[]
  catalogos: BaseConocimientoTICatalogos
}

export interface GuardarBaseConocimientoTISolicitud {
  titulo: string
  problema: string
  sintomas: string
  mensajeError: string | null
  causa: string
  solucion: string
  procedimiento: string | null
  linea: string
  item: string
  tipo: string
  subTipo: string
  categoria: string
  incidenciaOrigen: string | null
  /** Siempre se envía la guía completa: al actualizar, la que llega reemplaza a la guardada. */
  guia: PasoGuiaDiagnostico[]
}

interface BaseConocimientoTICreadoRespuesta {
  conocimientoCodigo: string
}

const api = crearApi({
  conexion: 'No fue posible comunicarse con el sistema. Intenta nuevamente.',
  sinPermiso: 'Tu perfil no tiene acceso a la Base de Conocimiento TI.',
})
const articulo = (codigo: string, ruta = '') => `/api/base-conocimiento/${encodeURIComponent(codigo)}${ruta}`

export const obtenerBaseConocimientoTI = () =>
  api<BaseConocimientoTIRespuesta>('/api/base-conocimiento', { error: 'No fue posible cargar la Base de Conocimiento.' })

export const obtenerDetalleBaseConocimientoTI = (codigo: string) =>
  api<BaseConocimientoTIDetalle>(articulo(codigo), { error: 'No fue posible cargar el artículo seleccionado.' })

/** Crea el artículo y devuelve su código. */
export async function crearBaseConocimientoTI(solicitud: GuardarBaseConocimientoTISolicitud): Promise<string> {
  const creado = await api<BaseConocimientoTICreadoRespuesta>('/api/base-conocimiento', {
    cuerpo: solicitud,
    error: 'No fue posible crear el artículo.',
  })
  return creado.conocimientoCodigo
}

export const actualizarBaseConocimientoTI = (codigo: string, solicitud: GuardarBaseConocimientoTISolicitud) =>
  api(articulo(codigo), { metodo: 'PUT', cuerpo: solicitud, error: 'No fue posible actualizar el artículo.' })

export const enviarValidacionBaseConocimientoTI = (codigo: string) =>
  api(articulo(codigo, '/enviar-validacion'), { metodo: 'POST', error: 'No fue posible enviar el artículo a validación.' })
export const validarBaseConocimientoTI = (codigo: string) =>
  api(articulo(codigo, '/validar'), { metodo: 'POST', error: 'No fue posible validar el artículo.' })
export const inactivarBaseConocimientoTI = (codigo: string) =>
  api(articulo(codigo, '/inactivar'), { metodo: 'POST', error: 'No fue posible inactivar el artículo.' })
