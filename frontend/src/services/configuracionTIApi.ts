/**
 * Archivo: configuracionTIApi.ts
 * Objetivo: Administrar los maestros de TI: áreas, líneas, ítems, tipos, categorías, subtipos, matriz, SLA, usuarios,
 *   formatos y visibilidad de artículos.
 * Responsabilidad: Consultar la configuración vigente y enviar cada alta o cambio al endpoint de su catálogo.
 * Dependencias: api.ts (cliente único de la API).
 * Flujo: ConfiguracionTIPage / AsistenteTIPage -> configuracionTIApi -> /api/configuracion-ti.
 * Consideraciones: Solo SUP y ADM pueden guardar; el backend vuelve a validar el perfil en cada operación.
 */

import { crearApi } from './api'

export interface AreaTI {
  area: string
  descripcion: string
  estado: string
  telefono: string
}

export interface LineaTI {
  linea: string
  area: string
  descripcion: string
  estado: string
}

export interface ItemTI {
  item: string
  linea: string
  descripcion: string
  estado: string
}

export interface TipoTI {
  tipo: string
  descripcion: string
  abreviatura: string
  estado: string
}

export interface CategoriaTI {
  categoria: string
  descripcion: string
  abreviatura: string
  estado: string
}

export interface SubTipoTI {
  tipo: string
  subTipo: string
  categoria: string
  descripcion: string
  abreviatura: string
  estado: string
}

export interface MatrizTI {
  item: string
  categoria: string
  prioridad: number | null
  impacto: number | null
  complejidad: number | null
  estado: string
}

export interface SlaTI {
  prioridad: number
  slaObjetivoMinutos: number
  estado: string
}

export interface UsuarioTIConfig {
  usuario: string
  nombreCompleto: string
  area: string
  cargo: string
  perfil: string
  correo: string
  estado: string
  fuenteIdentidad: string
  documento: string
  estadoCorporativo: string
  ultimaSincronizacion: string | null
}

export interface FormatoTI {
  formatoCodigo: string
  titulo: string
  descripcion: string
  nombreOriginal: string
  tipoMime: string
  tipoTicket: string
  estado: string
}

export interface ConocimientoTIConfig {
  conocimientoCodigo: string
  titulo: string
  estado: string
  visibleUsuario: boolean
}

export interface ConfiguracionTIRespuesta {
  areas: AreaTI[]
  lineas: LineaTI[]
  items: ItemTI[]
  tipos: TipoTI[]
  categorias: CategoriaTI[]
  subTipos: SubTipoTI[]
  matriz: MatrizTI[]
  sla: SlaTI[]
  usuarios: UsuarioTIConfig[]
  formatos: FormatoTI[]
  conocimientos: ConocimientoTIConfig[]
}

const api = crearApi({ conexion: 'No fue posible comunicarse con el sistema.', sinPermiso: 'Tu perfil no tiene acceso a Maestros TI.' })
const ERROR_GUARDAR = 'No fue posible guardar la configuración.'
const guardar = (ruta: string, cuerpo: unknown) => api(`/api/configuracion-ti/${ruta}`, { cuerpo, error: ERROR_GUARDAR })

export const obtenerConfiguracionTI = () =>
  api<ConfiguracionTIRespuesta>('/api/configuracion-ti', { error: 'No fue posible cargar la configuración.' })
export const guardarAreaTI = (x: unknown) => guardar('areas', x)
export const guardarLineaTI = (x: unknown) => guardar('lineas', x)
export const guardarItemTI = (x: unknown) => guardar('items', x)
export const guardarTipoTI = (x: unknown) => guardar('tipos', x)
export const guardarCategoriaTI = (x: unknown) => guardar('categorias', x)
export const guardarSubTipoTI = (x: unknown) => guardar('subtipos', x)
export const guardarMatrizTI = (x: unknown) => guardar('matriz', x)
export const guardarSlaTI = (x: unknown) => guardar('sla', x)
export const sincronizarUsuarioCorporativo = (x: unknown) => guardar('usuarios/sincronizar', x)
export const sincronizarCargosCorporativos = () =>
  api<{ procesados: number }>('/api/configuracion-ti/cargos/sincronizar', { metodo: 'POST', error: ERROR_GUARDAR })
export const actualizarVisibilidadConocimiento = (codigo: string, visibleUsuario: boolean) =>
  guardar(`conocimiento/${encodeURIComponent(codigo)}/visibilidad`, { visibleUsuario })
export const guardarFormatoSoporte = (formulario: FormData) => guardar('formatos', formulario)
