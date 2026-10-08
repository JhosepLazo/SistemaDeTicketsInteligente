/**
 * Archivo: AutenticacionContext.test.tsx
 * Objetivo: Comprobar el estado de sesión que comparte toda la aplicación.
 * Responsabilidad: Verificar que la sesión se recupera de la cookie al iniciar, que sin cookie queda sin autenticar y que el aviso de
 *   sesión vencida la cierra con un mensaje para el usuario.
 * Dependencias: Vitest, Testing Library, AutenticacionContext y api.ts.
 * Flujo: fetch simulado -> AutenticacionProvider -> componente que muestra estado, perfil y mensaje.
 * Consideraciones: No hay red: la API de sesión responde según cada caso.
 */

import { act, cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { EVENTO_SESION_EXPIRADA } from '../../services/api'
import { AutenticacionProvider, useAutenticacion } from './AutenticacionContext'

function Estado() {
  const { estado, usuario, mensajeSesion } = useAutenticacion()
  return (
    <p>
      {estado}|{usuario?.perfil ?? 'sin perfil'}|{mensajeSesion}
    </p>
  )
}

function responderSesion(respuesta: Response) {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(respuesta))
  render(
    <AutenticacionProvider>
      <Estado />
    </AutenticacionProvider>,
  )
}

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

describe('contexto de sesión', () => {
  it('recupera la sesión de la cookie al iniciar', async () => {
    responderSesion(
      new Response(JSON.stringify({ usuario: 'TEC001', nombreCompleto: 'Técnico', area: 'TIC', perfil: 'TEC' }), { status: 200 }),
    )

    expect(await screen.findByText('autenticado|TEC|')).toBeTruthy()
  })

  it('sin cookie queda sin autenticar y sin mensaje de error', async () => {
    responderSesion(new Response(null, { status: 401 }))

    expect(await screen.findByText('noAutenticado|sin perfil|')).toBeTruthy()
  })

  it('el aviso de sesión vencida cierra la sesión con un mensaje', async () => {
    responderSesion(
      new Response(JSON.stringify({ usuario: 'USR001', nombreCompleto: 'Usuario', area: 'CMP', perfil: 'USR' }), { status: 200 }),
    )
    await screen.findByText('autenticado|USR|')

    act(() => {
      window.dispatchEvent(new Event(EVENTO_SESION_EXPIRADA))
    })

    expect(await screen.findByText(/^noAutenticado\|sin perfil\|Tu sesión venció/)).toBeTruthy()
  })
})
