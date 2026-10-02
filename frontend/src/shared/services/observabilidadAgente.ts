const clave = 'calimod.agente.traza.v1'
export function seleccionarSesionTraza(numero: number | null) {
  try { if (numero) sessionStorage.setItem(clave, String(numero)); else sessionStorage.removeItem(clave) } catch { /* Almacenamiento opcional. */ }
}
export function instalarObservabilidadAgente() {
  const original = window.fetch.bind(window)
  window.fetch = (entrada, opciones) => {
    const url = new URL(entrada instanceof Request ? entrada.url : String(entrada), window.location.href)
    if (url.origin !== window.location.origin || !url.pathname.startsWith('/api/') || url.pathname.startsWith('/api/autenticacion')) return original(entrada, opciones)
    let numero: string | null = null
    try { numero = sessionStorage.getItem(clave) } catch { }
    if (!numero || !/^\d+$/.test(numero)) return original(entrada, opciones)
    const headers = new Headers(entrada instanceof Request ? entrada.headers : opciones?.headers)
    if (entrada instanceof Request && opciones?.headers) new Headers(opciones.headers).forEach((v, k) => headers.set(k, v))
    headers.set('X-Agente-Sesion', numero)
    headers.set('X-Frontend-Route', window.location.pathname)
    return original(entrada, { ...opciones, headers })
  }
}
