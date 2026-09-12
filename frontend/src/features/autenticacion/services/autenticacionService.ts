import type {
  RespuestaInicioSesion,
  SolicitudInicioSesion,
} from '../types/autenticacion'

const rutaAutenticacion = '/api/autenticacion/iniciar-sesion'

async function obtenerMensajeError(respuesta: Response) {
  try {
    const error = await respuesta.json() as { mensaje?: string }
    return error.mensaje ?? 'No fue posible iniciar sesión.'
  } catch {
    return 'No fue posible iniciar sesión.'
  }
}

export async function iniciarSesion(
  solicitud: SolicitudInicioSesion,
): Promise<RespuestaInicioSesion> {
  const respuesta = await fetch(rutaAutenticacion, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify(solicitud),
  })

  if (!respuesta.ok) throw new Error(await obtenerMensajeError(respuesta))

  return respuesta.json() as Promise<RespuestaInicioSesion>
}