/**
 * Archivo: configuracionTIService.ts
 * Objetivo: Comunicar Maestros TI con sus endpoints restringidos al equipo TI.
 * Responsabilidad: Exponer contratos y operaciones simples para catálogos, matriz, SLA, identidad corporativa, formatos y publicación de conocimiento.
 * Dependencias: Fetch API y sesión autenticada mediante cookie HttpOnly.
 * Flujo: ConfiguracionTIPage -> configuracionTIService -> API /api/configuracion-ti.
 * Consideraciones: Los perfiles TI autorizados pueden usar estos endpoints; los registros se activan/inactivan en lugar de eliminarse.
 */

import { rechazarSiSesionExpirada } from '../../autenticacion/services/sesionExpirada'

export interface AreaTI { area:string; descripcion:string; estado:string; telefono:string }
export interface LineaTI { linea:string; area:string; descripcion:string; estado:string }
export interface ItemTI { item:string; linea:string; descripcion:string; estado:string }
export interface TipoTI { tipo:string; descripcion:string; abreviatura:string; estado:string }
export interface CategoriaTI { categoria:string; descripcion:string; abreviatura:string; estado:string }
export interface SubTipoTI { tipo:string; subTipo:string; categoria:string; descripcion:string; abreviatura:string; estado:string }
export interface MatrizTI { item:string; categoria:string; prioridad:number|null; impacto:number|null; complejidad:number|null; estado:string }
export interface SlaTI { prioridad:number; slaObjetivoMinutos:number; estado:string }
export interface UsuarioTIConfig { usuario:string; nombreCompleto:string; area:string; cargo:string; perfil:string; correo:string; estado:string; fuenteIdentidad:string; documento:string; estadoCorporativo:string; ultimaSincronizacion:string|null }
export interface FormatoTI { formatoCodigo:string; titulo:string; descripcion:string; nombreOriginal:string; tipoMime:string; tipoTicket:string; estado:string }
export interface ConocimientoTIConfig { conocimientoCodigo:string; titulo:string; estado:string; visibleUsuario:boolean }
export interface ConfiguracionTIRespuesta { areas:AreaTI[]; lineas:LineaTI[]; items:ItemTI[]; tipos:TipoTI[]; categorias:CategoriaTI[]; subTipos:SubTipoTI[]; matriz:MatrizTI[]; sla:SlaTI[]; usuarios:UsuarioTIConfig[]; formatos:FormatoTI[]; conocimientos:ConocimientoTIConfig[] }

async function solicitar<T>(url:string, opciones?:RequestInit):Promise<T>{
  let respuesta:Response
  try{respuesta=await fetch(url,{credentials:'include',...opciones})}catch{throw new Error('No fue posible comunicarse con el sistema.')}
  rechazarSiSesionExpirada(respuesta)
  if(respuesta.status===403)throw new Error('Tu perfil no tiene acceso a Maestros TI.')
  if(!respuesta.ok){const datos=await respuesta.json().catch(()=>null) as {mensaje?:string}|null;throw new Error(datos?.mensaje||'No fue posible guardar la configuración.')}
  if(respuesta.status===204)return undefined as T
  return respuesta.json() as Promise<T>
}
const post=(ruta:string,body:unknown)=>solicitar<void>(ruta,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)})

export const obtenerConfiguracionTI=()=>solicitar<ConfiguracionTIRespuesta>('/api/configuracion-ti')
export const guardarAreaTI=(x:unknown)=>post('/api/configuracion-ti/areas',x)
export const guardarLineaTI=(x:unknown)=>post('/api/configuracion-ti/lineas',x)
export const guardarItemTI=(x:unknown)=>post('/api/configuracion-ti/items',x)
export const guardarTipoTI=(x:unknown)=>post('/api/configuracion-ti/tipos',x)
export const guardarCategoriaTI=(x:unknown)=>post('/api/configuracion-ti/categorias',x)
export const guardarSubTipoTI=(x:unknown)=>post('/api/configuracion-ti/subtipos',x)
export const guardarMatrizTI=(x:unknown)=>post('/api/configuracion-ti/matriz',x)
export const guardarSlaTI=(x:unknown)=>post('/api/configuracion-ti/sla',x)
export const sincronizarUsuarioCorporativo=(x:unknown)=>post('/api/configuracion-ti/usuarios/sincronizar',x)
export const sincronizarCargosCorporativos=()=>solicitar<{procesados:number}>('/api/configuracion-ti/cargos/sincronizar',{method:'POST'})
export const actualizarVisibilidadConocimiento=(codigo:string,visibleUsuario:boolean)=>post(`/api/configuracion-ti/conocimiento/${encodeURIComponent(codigo)}/visibilidad`,{visibleUsuario})
export function guardarFormatoSoporte(formulario:FormData){return solicitar<void>('/api/configuracion-ti/formatos',{method:'POST',body:formulario})}
