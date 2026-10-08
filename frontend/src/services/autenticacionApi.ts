/**
 * Archivo: autenticacionApi.ts
 * Objetivo: Iniciar, comprobar y cerrar la sesión del usuario con la API de autenticación.
 * Responsabilidad: Enviar las credenciales y leer la identidad segura que devuelve el backend; la sesión vive en una cookie HttpOnly.
 * Dependencias: api.ts (llamarApi, mensajeError y reiniciarTokenCsrf).
 * Flujo: LoginPage y AutenticacionContext -> autenticacionApi -> /api/autenticacion.
 * Consideraciones: Aquí un 401 no es una sesión vencida: al iniciar son credenciales incorrectas y al comprobar significa "sin sesión".
 *   No se guardan tokens, cookies ni contraseñas. Al iniciar o cerrar sesión cambia la identidad, así que se olvida el token CSRF.
 */

import { llamarApi, mensajeError, reiniciarTokenCsrf } from './api'

export interface SolicitudInicioSesion {
  usuario: string
  contrasena: string
}

export interface RespuestaInicioSesion {
  usuario: string
  nombreCompleto: string
  area: string
  perfil: string
}

export type EstadoAutenticacion = 'comprobandoSesion' | 'autenticado' | 'noAutenticado'

const ruta = (accion: string) => `/api/autenticacion/${accion}`

export async function iniciarSesion(solicitud: SolicitudInicioSesion): Promise<RespuestaInicioSesion> {
  const respuesta = await llamarApi(
    ruta('iniciar-sesion'),
    { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(solicitud) },
    'No fue posible comunicarse con el sistema. Intenta nuevamente en unos momentos.',
  )
  if (respuesta.status === 429) throw new Error('Se realizaron demasiados intentos. Intenta nuevamente en unos momentos.')
  if (!respuesta.ok) throw new Error(await mensajeError(respuesta, 'No fue posible iniciar sesión.'))
  reiniciarTokenCsrf()
  return respuesta.json() as Promise<RespuestaInicioSesion>
}

/** Identidad de la sesión activa, o null si no hay sesión. */
export async function obtenerSesion(): Promise<RespuestaInicioSesion | null> {
  const respuesta = await llamarApi(
    ruta('sesion'),
    { method: 'GET' },
    'No fue posible comprobar la sesión. Verifica que el sistema se encuentre disponible.',
  )
  if (respuesta.status === 401) {
    reiniciarTokenCsrf()
    return null
  }
  if (!respuesta.ok) throw new Error(await mensajeError(respuesta, 'No fue posible comprobar la sesión.'))
  return respuesta.json() as Promise<RespuestaInicioSesion>
}

/** Cierra la sesión; si ya había vencido (401) no hay nada que cerrar. */
export async function cerrarSesion(): Promise<void> {
  const respuesta = await llamarApi(
    ruta('cerrar-sesion'),
    { method: 'POST' },
    'No fue posible comunicarse con el sistema al cerrar la sesión.',
  )
  reiniciarTokenCsrf()
  if (respuesta.status === 401 || respuesta.ok) return
  throw new Error(await mensajeError(respuesta, 'No fue posible cerrar la sesión correctamente.'))
}
