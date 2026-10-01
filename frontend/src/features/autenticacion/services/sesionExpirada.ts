export const EVENTO_SESION_EXPIRADA = 'sistema-tickets:sesion-expirada'

export function rechazarSiSesionExpirada(respuesta: Response): void {
  if (respuesta.status !== 401) return

  window.dispatchEvent(new Event(EVENTO_SESION_EXPIRADA))
  throw new Error('Tu sesión venció. Vuelve a iniciar sesión para continuar.')
}
