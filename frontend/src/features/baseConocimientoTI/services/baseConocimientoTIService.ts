/**
 * Archivo: baseConocimientoTIService.ts
 * Objetivo: Comunicar el módulo Base de Conocimiento TI con sus endpoints protegidos.
 * Responsabilidad: Exponer contratos TypeScript y operaciones simples para consultar, crear, editar, validar e inactivar artículos.
 * Dependencias: Fetch API y sesión autenticada mediante cookie HttpOnly.
 * Flujo: BaseConocimientoTIPage -> baseConocimientoTIService -> API /api/base-conocimiento.
 * Consideraciones: La identidad del operador no se envía desde el navegador; el backend la obtiene de la sesión autenticada.
 */

import { rechazarSiSesionExpirada } from '../../autenticacion/services/sesionExpirada'
import { peticionHttp } from '../../../shared/services/peticionHttp'

const mensajeConexion = 'No fue posible comunicarse con el sistema. Intenta nuevamente.'

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

export interface BaseConocimientoTIDetalle extends BaseConocimientoTIItem {
  sintomas: string
  mensajeError: string
  causa: string
  procedimiento: string
  usuarioValida: string
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
}

interface BaseConocimientoTICreadoRespuesta {
  conocimientoCodigo: string
}

async function procesarRespuesta(respuesta: Response, mensajePredeterminado: string) {
  rechazarSiSesionExpirada(respuesta)
  if (respuesta.status === 403) throw new Error('Tu perfil no tiene acceso a la Base de Conocimiento TI.')
  if (respuesta.ok) return

  try {
    const error = await respuesta.json() as { mensaje?: string }
    throw new Error(error.mensaje || mensajePredeterminado)
  } catch (error) {
    if (error instanceof Error && error.message !== 'Unexpected end of JSON input') throw error
    throw new Error(mensajePredeterminado)
  }
}

export async function obtenerBaseConocimientoTI(): Promise<BaseConocimientoTIRespuesta> {
  const respuesta = await peticionHttp('/api/base-conocimiento', { credentials: 'include' }, mensajeConexion)

  await procesarRespuesta(respuesta, 'No fue posible cargar la Base de Conocimiento.')
  return respuesta.json() as Promise<BaseConocimientoTIRespuesta>
}

export async function obtenerDetalleBaseConocimientoTI(codigo: string): Promise<BaseConocimientoTIDetalle> {
  const respuesta = await peticionHttp(`/api/base-conocimiento/${encodeURIComponent(codigo)}`, { credentials: 'include' }, mensajeConexion)
  await procesarRespuesta(respuesta, 'No fue posible cargar el artículo seleccionado.')
  return respuesta.json() as Promise<BaseConocimientoTIDetalle>
}

export async function crearBaseConocimientoTI(solicitud: GuardarBaseConocimientoTISolicitud): Promise<string> {
  const respuesta = await peticionHttp('/api/base-conocimiento', {
    method: 'POST', credentials: 'include', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(solicitud),
  }, mensajeConexion)
  await procesarRespuesta(respuesta, 'No fue posible crear el artículo.')
  const creado = await respuesta.json() as BaseConocimientoTICreadoRespuesta
  return creado.conocimientoCodigo
}

export async function actualizarBaseConocimientoTI(codigo: string, solicitud: GuardarBaseConocimientoTISolicitud): Promise<void> {
  const respuesta = await peticionHttp(`/api/base-conocimiento/${encodeURIComponent(codigo)}`, {
    method: 'PUT', credentials: 'include', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(solicitud),
  }, mensajeConexion)
  await procesarRespuesta(respuesta, 'No fue posible actualizar el artículo.')
}

async function cambiarEstado(codigo: string, accion: 'enviar-validacion' | 'validar' | 'inactivar', mensaje: string) {
  const respuesta = await peticionHttp(`/api/base-conocimiento/${encodeURIComponent(codigo)}/${accion}`, { method: 'POST', credentials: 'include' }, mensajeConexion)
  await procesarRespuesta(respuesta, mensaje)
}

export function enviarValidacionBaseConocimientoTI(codigo: string) {
  return cambiarEstado(codigo, 'enviar-validacion', 'No fue posible enviar el artículo a validación.')
}

export function validarBaseConocimientoTI(codigo: string) {
  return cambiarEstado(codigo, 'validar', 'No fue posible validar el artículo.')
}

export function inactivarBaseConocimientoTI(codigo: string) {
  return cambiarEstado(codigo, 'inactivar', 'No fue posible inactivar el artículo.')
}
