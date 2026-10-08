/**
 * Archivo: api.test.ts
 * Objetivo: Comprobar las reglas comunes de toda petición a la API.
 * Responsabilidad: Verificar el token CSRF (se pide una vez, viaja solo en operaciones que modifican datos, se renueva y reintenta una vez
 *   si el servidor lo rechaza, no se usa en el login), el aviso de sesión vencida y los mensajes de error.
 * Dependencias: Vitest y api.ts.
 * Flujo: fetch simulado -> crearApi / llamarApi -> peticiones y errores observados.
 * Consideraciones: No hay red: fetch es un doble que responde según la ruta.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { crearApi, EVENTO_SESION_EXPIRADA, llamarApi, reiniciarTokenCsrf } from './api'

interface Llamada {
  ruta: string
  metodo: string
  token: string | null
}

const RUTA_TOKEN = '/api/autenticacion/token-csrf'

function json(estado: number, cuerpo?: unknown, encabezados: Record<string, string> = {}) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), {
    status: estado,
    headers: { 'Content-Type': 'application/json', ...encabezados },
  })
}

/** Simula el servidor: entrega tokens T1, T2... y responde cada operación con la función indicada. */
function simularServidor(responder: (llamada: Llamada, numero: number) => Response) {
  const llamadas: Llamada[] = []
  let tokens = 0
  let operaciones = 0
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, opciones: RequestInit = {}) => {
      const ruta = new URL(url, 'http://localhost').pathname
      const token = new Headers(opciones.headers).get('X-CSRF-TOKEN')
      const llamada = { ruta, metodo: (opciones.method ?? 'GET').toUpperCase(), token }
      llamadas.push(llamada)
      if (ruta === RUTA_TOKEN) return json(200, { token: `T${++tokens}` })
      return responder(llamada, ++operaciones)
    }),
  )
  return llamadas
}

const api = crearApi({ sinPermiso: 'Sin permiso en este módulo.' })

beforeEach(() => reiniciarTokenCsrf())
afterEach(() => vi.unstubAllGlobals())

describe('token CSRF', () => {
  it('una consulta GET no pide token ni lo envía', async () => {
    const llamadas = simularServidor(() => json(200, { ok: true }))

    await api('/api/gestion-tickets', { error: 'x' })

    expect(llamadas).toEqual([{ ruta: '/api/gestion-tickets', metodo: 'GET', token: null }])
  })

  it('las operaciones que modifican datos piden el token una sola vez y lo envían', async () => {
    const llamadas = simularServidor(() => json(204))

    await Promise.all([
      api('/api/mis-tickets/INC-1/calificar', { cuerpo: { calificacion: 5 }, error: 'x' }),
      api('/api/notificaciones/1/leida', { metodo: 'POST', error: 'x' }),
    ])

    expect(llamadas.filter(x => x.ruta === RUTA_TOKEN)).toHaveLength(1)
    expect(llamadas.filter(x => x.ruta !== RUTA_TOKEN).map(x => x.token)).toEqual(['T1', 'T1'])
  })

  it('si el servidor rechaza el token pide otro y reintenta una sola vez', async () => {
    const llamadas = simularServidor((_, numero) =>
      numero === 1 ? json(400, { mensaje: 'vencido' }, { 'X-Csrf-Invalido': '1' }) : json(204),
    )

    await api('/api/gestion-tickets/INC-1/asignar', { cuerpo: { usuarioTI: 'TEC001' }, error: 'x' })

    expect(llamadas.map(x => `${x.ruta}:${x.token ?? ''}`)).toEqual([
      `${RUTA_TOKEN}:`,
      '/api/gestion-tickets/INC-1/asignar:T1',
      `${RUTA_TOKEN}:`,
      '/api/gestion-tickets/INC-1/asignar:T2',
    ])
  })

  it('no reintenta más de una vez', async () => {
    const llamadas = simularServidor(() => json(400, { mensaje: 'La protección de la sesión venció.' }, { 'X-Csrf-Invalido': '1' }))

    await expect(api('/api/gestion-tickets/INC-1/asignar', { cuerpo: {}, error: 'x' })).rejects.toThrow(
      'La protección de la sesión venció.',
    )
    expect(llamadas.filter(x => x.ruta !== RUTA_TOKEN)).toHaveLength(2)
  })

  it('el inicio de sesión no pide token', async () => {
    const llamadas = simularServidor(() => json(200, {}))

    await llamarApi('/api/autenticacion/iniciar-sesion', { method: 'POST', body: '{}' })

    expect(llamadas).toEqual([{ ruta: '/api/autenticacion/iniciar-sesion', metodo: 'POST', token: null }])
  })
})

describe('sesión y errores', () => {
  it('un 401 avisa que la sesión venció y olvida el token', async () => {
    const llamadas = simularServidor((_, numero) => (numero === 1 ? json(401) : json(204)))
    const aviso = vi.fn()
    window.addEventListener(EVENTO_SESION_EXPIRADA, aviso)

    await expect(api('/api/mis-tickets/INC-1/calificar', { cuerpo: {}, error: 'x' })).rejects.toThrow('Tu sesión venció')
    await api('/api/mis-tickets/INC-1/calificar', { cuerpo: {}, error: 'x' })

    window.removeEventListener(EVENTO_SESION_EXPIRADA, aviso)
    expect(aviso).toHaveBeenCalledTimes(1)
    expect(llamadas.filter(x => x.ruta === RUTA_TOKEN)).toHaveLength(2)
  })

  it('usa el mensaje de la API; si no trae uno, el del módulo o el de la operación', async () => {
    simularServidor((_, numero) =>
      numero === 1 ? json(409, { mensaje: 'El ticket ya fue resuelto.' }) : numero === 2 ? json(403) : json(500),
    )

    await expect(api('/api/a', { error: 'Falló la operación.' })).rejects.toThrow('El ticket ya fue resuelto.')
    await expect(api('/api/b', { error: 'Falló la operación.' })).rejects.toThrow('Sin permiso en este módulo.')
    await expect(api('/api/c', { error: 'Falló la operación.' })).rejects.toThrow('Falló la operación.')
  })

  it('sin conexión lanza el mensaje de conexión indicado', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))

    await expect(api('/api/a', { error: 'x', conexion: 'Sin conexión con el sistema.' })).rejects.toThrow('Sin conexión con el sistema.')
  })
})
