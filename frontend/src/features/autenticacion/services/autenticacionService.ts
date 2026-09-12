/**
 * Archivo: autenticacionService.ts
 * Objetivo: Comunicar el frontend con los endpoints de autenticación de la API.
 * Responsabilidad: Iniciar, recuperar y cerrar la sesión mediante la cookie administrada por el navegador.
 * Dependencias: Fetch API y contratos TypeScript de autenticación.
 * Flujo: React -> autenticacionService -> API de autenticación.
 * Consideraciones: No almacena tokens, cookies, contraseñas ni detalles técnicos de errores.
 */

import type {
  RespuestaInicioSesion,
  SolicitudInicioSesion,
} from '../types/autenticacion'

const rutaAutenticacion = '/api/autenticacion'

async function realizarPeticion(
  ruta: string,
  opciones: RequestInit,
  mensajeError: string,
): Promise<Response> {
  try {
    return await fetch(`${rutaAutenticacion}/${ruta}`, {
      ...opciones,
      credentials: 'include',
    })
  } catch {
    throw new Error(mensajeError)
  }
}

async function obtenerMensajeError(respuesta: Response, mensajePredeterminado: string) {
  try {
    const error = await respuesta.json() as { mensaje?: string }
    return error.mensaje ?? mensajePredeterminado
  } catch {
    return mensajePredeterminado
  }
}

export async function iniciarSesion(
  solicitud: SolicitudInicioSesion,
): Promise<RespuestaInicioSesion> {
  const respuesta = await realizarPeticion('iniciar-sesion', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(solicitud),
  }, 'No fue posible comunicarse con el sistema. Intenta nuevamente en unos momentos.')

  if (respuesta.status === 429) {
    throw new Error('Se realizaron demasiados intentos. Intenta nuevamente en unos momentos.')
  }

  if (!respuesta.ok) {
    throw new Error(await obtenerMensajeError(respuesta, 'No fue posible iniciar sesión.'))
  }

  return respuesta.json() as Promise<RespuestaInicioSesion>
}

export async function obtenerSesion(): Promise<RespuestaInicioSesion | null> {
  const respuesta = await realizarPeticion('sesion', {
    method: 'GET',
  }, 'No fue posible comprobar la sesión. Verifica que el sistema se encuentre disponible.')

  if (respuesta.status === 401) return null

  if (!respuesta.ok) {
    throw new Error(await obtenerMensajeError(respuesta, 'No fue posible comprobar la sesión.'))
  }

  return respuesta.json() as Promise<RespuestaInicioSesion>
}

export async function cerrarSesion(): Promise<void> {
  const respuesta = await realizarPeticion('cerrar-sesion', {
    method: 'POST',
  }, 'No fue posible comunicarse con el sistema al cerrar la sesión.')

  if (respuesta.status === 401 || respuesta.ok) return

  throw new Error(await obtenerMensajeError(respuesta, 'No fue posible cerrar la sesión correctamente.'))
}
