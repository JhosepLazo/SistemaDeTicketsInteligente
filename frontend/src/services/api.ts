/**
 * Archivo: api.ts
 * Objetivo: Hacer todas las peticiones a la API del sistema con las mismas reglas.
 * Responsabilidad: Enviar la cookie de sesión, avisar a la aplicación cuando la sesión venció (401), marcar las peticiones
 *   del proceso que se está reproduciendo para el agente y convertir cada error en un mensaje para la pantalla.
 * Dependencias: Fetch API y sessionStorage.
 * Flujo: <modulo>Api.ts -> crearApi(textos) -> fetch -> API ASP.NET Core.
 * Consideraciones: El mensaje de un error es el { mensaje } que devuelve la API; si no trae uno, el texto del módulo para ese
 *   estado (sin permiso o demasiadas solicitudes) o el de la operación. Nunca se muestran detalles técnicos.
 */

export const EVENTO_SESION_EXPIRADA = 'sistema-tickets:sesion-expirada'

const CONEXION = 'No fue posible comunicarse con el sistema. Intenta nuevamente en unos momentos.'
const CLAVE_SESION_AGENTE = 'calimod.agente.traza.v1'

/** Textos de un módulo para los errores que la API devuelve sin mensaje propio. */
export interface TextosModulo {
  /** Sin conexión con el servidor. */
  conexion?: string
  /** 403: el perfil no tiene acceso. */
  sinPermiso?: string
  /** 429: demasiadas solicitudes seguidas. */
  saturado?: string
}

export interface Peticion {
  /** Mensaje si la operación falla y la API no explica por qué. */
  error: string
  /** Objeto que viaja como JSON, o FormData cuando hay archivos. */
  cuerpo?: unknown
  /** Por defecto GET sin cuerpo y POST con cuerpo. */
  metodo?: 'GET' | 'POST' | 'PUT'
  /** Mensaje propio de esta operación cuando no hay conexión. */
  conexion?: string
  /** Devuelve el archivo recibido (Blob) en lugar de JSON. */
  archivo?: boolean
}

/**
 * Mientras TI (o el colaborador invitado) reproduce un error, cada petición lleva el número de la investigación para que
 * el servidor la guarde como evidencia del proceso. null deja de marcarlas.
 */
export function seleccionarSesionTraza(numero: number | null) {
  try {
    if (numero) sessionStorage.setItem(CLAVE_SESION_AGENTE, String(numero))
    else sessionStorage.removeItem(CLAVE_SESION_AGENTE)
  } catch {
    /* Almacenamiento opcional. */
  }
}

// La campana y la autenticación consultan en segundo plano: no forman parte del proceso que se reproduce.
function cabeceras(url: string, propias?: HeadersInit): HeadersInit | undefined {
  let sesion: string | null = null
  try {
    sesion = sessionStorage.getItem(CLAVE_SESION_AGENTE)
  } catch {
    /* Almacenamiento opcional. */
  }
  const ruta = new URL(url, window.location.href).pathname
  if (!sesion || !/^\d+$/.test(sesion) || ruta.startsWith('/api/autenticacion') || ruta.startsWith('/api/notificaciones')) return propias
  const resultado = new Headers(propias)
  resultado.set('X-Agente-Sesion', sesion)
  resultado.set('X-Frontend-Route', window.location.pathname)
  return resultado
}

/** fetch con la cookie de sesión; sin conexión con el servidor lanza el mensaje indicado. */
export async function llamarApi(url: string, opciones: RequestInit, conexion = CONEXION): Promise<Response> {
  try {
    return await fetch(url, { ...opciones, credentials: 'include', headers: cabeceras(url, opciones.headers) })
  } catch {
    throw new Error(conexion)
  }
}

/** Mensaje de error que devuelve la API ({ mensaje }) o el indicado si no trae uno. */
export async function mensajeError(respuesta: Response, defecto: string) {
  const datos = (await respuesta.json().catch(() => null)) as { mensaje?: string } | null
  return datos?.mensaje || defecto
}

/** Cliente de un módulo: cada petición devuelve el JSON de la respuesta (nada si es 204) o lanza un Error con su mensaje. */
export function crearApi(textos: TextosModulo = {}) {
  return async function api<T = void>(url: string, { error, cuerpo, metodo, conexion, archivo }: Peticion): Promise<T> {
    const formulario = cuerpo instanceof FormData
    const respuesta = await llamarApi(
      url,
      {
        method: metodo ?? (cuerpo === undefined ? 'GET' : 'POST'),
        headers: cuerpo === undefined || formulario ? undefined : { 'Content-Type': 'application/json' },
        body: cuerpo === undefined ? undefined : formulario ? cuerpo : JSON.stringify(cuerpo),
      },
      conexion ?? textos.conexion,
    )
    if (respuesta.status === 401) {
      window.dispatchEvent(new Event(EVENTO_SESION_EXPIRADA))
      throw new Error('Tu sesión venció. Vuelve a iniciar sesión para continuar.')
    }
    if (!respuesta.ok) {
      const porEstado = respuesta.status === 403 ? textos.sinPermiso : respuesta.status === 429 ? textos.saturado : undefined
      throw new Error(await mensajeError(respuesta, porEstado ?? error))
    }
    if (respuesta.status === 204) return undefined as T
    return (archivo ? respuesta.blob() : respuesta.json()) as Promise<T>
  }
}
