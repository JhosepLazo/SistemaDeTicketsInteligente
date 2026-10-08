/**
 * Archivo: configuracionTIApi.ts
 * Objetivo: Administrar los maestros de TI: áreas, líneas, ítems, tipos, categorías, subtipos, matriz, SLA, usuarios,
 *   formatos, visibilidad de artículos, control del agente (interruptor, límites, política de autonomía y catálogo de acciones) y
 *   fichas de los tipos de ticket.
 * Responsabilidad: Consultar la configuración vigente y enviar cada alta o cambio al endpoint de su catálogo.
 * Dependencias: api.ts (cliente único de la API).
 * Flujo: ConfiguracionTIPage / AsistenteTIPage -> configuracionTIApi -> /api/configuracion-ti.
 * Consideraciones: TEC, SUP y ADM mantienen los maestros; el control del agente y las fichas solo los cambia un ADM. El backend y
 *   cada procedimiento vuelven a validar el perfil en cada operación.
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

/** Parámetro operativo del agente (TI_Parametro). Valor null = desactivado (vigencia de aprobaciones, autocierre). */
export interface ParametroTI {
  parametro: string
  valor: string
  descripcion: string
  ultimoUsuario: string
  ultimaFechaModif: string | null
}

export type ModoPolitica = 'AUTONOMA' | 'APROBACION' | 'PROHIBIDA'

/** Política de autonomía por tipo de ticket y acción; configurada = false muestra el valor por defecto (APROBACION). */
export interface PoliticaAutonomiaTI {
  tipo: string
  tipoDescripcion: string
  accionCodigo: string
  accionNombre: string
  modo: ModoPolitica
  confianzaMinima: number | null
  estado: string
  configurada: boolean
}

export interface AccionCatalogoTI {
  accionCodigo: string
  nombre: string
  descripcion: string
  /** L lectura, E ejecución. */
  tipo: string
  nivelRiesgo: string
  requiereAprobacion: boolean
  reversible: boolean
  estado: string
  tieneEjecutor: boolean
  procedimiento: string
  maximoFilas: number
  parametrosDescripcion: string
}

export interface ControlAgenteTI {
  parametros: ParametroTI[]
  politica: PoliticaAutonomiaTI[]
  acciones: AccionCatalogoTI[]
}

/** Campo de la ficha de un tipo de ticket, activo o inactivo. */
export interface CampoFichaTI {
  tipo: string
  tipoDescripcion: string
  campo: string
  bloque: string
  orden: number
  pregunta: string
  ayuda: string
  tipoDato: 'TEXTO' | 'TEXTO_LARGO' | 'FECHA' | 'SI_NO'
  obligatorio: boolean
  longitudMinima: number
  longitudMaxima: number
  estado: string
  ultimoUsuario: string
  ultimaFechaModif: string | null
}

export const NIVELES_RIESGO = ['MUY_BAJO', 'BAJO', 'MEDIO', 'ALTO', 'MUY_ALTO'] as const

const api = crearApi({ conexion: 'No fue posible comunicarse con el sistema.', sinPermiso: 'Tu perfil no tiene acceso a Maestros TI.' })
const apiAdministrador = crearApi({
  conexion: 'No fue posible comunicarse con el sistema.',
  sinPermiso: 'Solo un administrador puede cambiar el control del agente y las fichas.',
})
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

export const obtenerControlAgenteTI = () =>
  api<ControlAgenteTI>('/api/configuracion-ti/agente', { error: 'No fue posible cargar el control del agente.' })
const guardarAdministrador = (ruta: string, cuerpo: unknown) =>
  apiAdministrador(`/api/configuracion-ti/${ruta}`, { cuerpo, error: ERROR_GUARDAR })
export const guardarParametroAgenteTI = (parametro: string, valor: string | null) =>
  guardarAdministrador('agente/parametros', { parametro, valor })
export const guardarPoliticaAutonomiaTI = (x: {
  tipo: string
  accionCodigo: string
  modo: ModoPolitica
  confianzaMinima: number | null
  estado: string
}) => guardarAdministrador('agente/politica', x)
export const guardarAccionCatalogoTI = (x: {
  accionCodigo: string
  nivelRiesgo: string
  requiereAprobacion: boolean
  reversible: boolean
  estado: string
}) => guardarAdministrador('agente/acciones', x)
export const obtenerFichasTI = () => api<CampoFichaTI[]>('/api/configuracion-ti/fichas', { error: 'No fue posible cargar las fichas.' })
export const guardarCampoFichaTI = (x: Omit<CampoFichaTI, 'tipoDescripcion' | 'ultimoUsuario' | 'ultimaFechaModif'>) =>
  guardarAdministrador('fichas', x)
