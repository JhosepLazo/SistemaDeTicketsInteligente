import type {
  RespuestaInicioSesion,
  SolicitudInicioSesion,
} from '../types/autenticacion'

const rutaAutenticacion = '/api/autenticacion/iniciar-sesion'

export async function iniciarSesion(
  solicitud: SolicitudInicioSesion,
  signal?: AbortSignal,
): Promise<RespuestaInicioSesion> {
  const respuesta = await fetch(rutaAutenticacion, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(solicitud),
    signal,
  })

  if (!respuesta.ok) {
    throw new Error('No fue posible iniciar sesión.')
  }

  return respuesta.json() as Promise<RespuestaInicioSesion>
}
