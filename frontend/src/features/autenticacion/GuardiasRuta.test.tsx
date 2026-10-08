/**
 * Archivo: GuardiasRuta.test.tsx
 * Objetivo: Comprobar que cada perfil solo abre las pantallas que le corresponden.
 * Responsabilidad: Verificar el envío al login sin sesión, al inicio desde el login con sesión y al inicio cuando un perfil abre una
 *   pantalla de otro (colaborador frente a equipo TI).
 * Dependencias: Vitest, Testing Library, React Router y GuardiasRuta.
 * Flujo: sesión simulada -> MemoryRouter en la ruta pedida -> pantalla que queda visible.
 * Consideraciones: La sesión se simula reemplazando useAutenticacion; el backend sigue siendo la autorización definitiva.
 */

import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { RutaProtegida, RutaPublica, RutaTI, RutaUsuario } from './GuardiasRuta'

const sesion = vi.hoisted(() => ({ actual: { estado: 'noAutenticado', usuario: null as { perfil: string } | null } }))
vi.mock('./AutenticacionContext', () => ({ useAutenticacion: () => sesion.actual }))

function abrir(ruta: string, perfil: string | null) {
  sesion.actual = perfil ? { estado: 'autenticado', usuario: { perfil } } : { estado: 'noAutenticado', usuario: null }
  render(
    <MemoryRouter initialEntries={[ruta]}>
      <Routes>
        <Route path="/login" element={<RutaPublica>Pantalla de acceso</RutaPublica>} />
        <Route path="/inicio" element={<RutaProtegida>Inicio</RutaProtegida>} />
        <Route
          path="/mis-tickets"
          element={
            <RutaProtegida>
              <RutaUsuario>Mis tickets</RutaUsuario>
            </RutaProtegida>
          }
        />
        <Route
          path="/gestion-tickets"
          element={
            <RutaProtegida>
              <RutaTI>Gestión de tickets</RutaTI>
            </RutaProtegida>
          }
        />
      </Routes>
    </MemoryRouter>,
  )
}

afterEach(cleanup)

describe('guardias de ruta', () => {
  it('sin sesión envía al login', () => {
    abrir('/gestion-tickets', null)
    expect(screen.getByText('Pantalla de acceso')).toBeTruthy()
  })

  it('con sesión, el login lleva al inicio', () => {
    abrir('/login', 'USR')
    expect(screen.getByText('Inicio')).toBeTruthy()
  })

  it.each(['TEC', 'SUP', 'ADM'])('%s abre Gestión de tickets pero no Mis tickets', perfil => {
    abrir('/gestion-tickets', perfil)
    expect(screen.getByText('Gestión de tickets')).toBeTruthy()
    cleanup()
    abrir('/mis-tickets', perfil)
    expect(screen.getByText('Inicio')).toBeTruthy()
  })

  it('el colaborador abre Mis tickets pero no Gestión de tickets', () => {
    abrir('/mis-tickets', 'USR')
    expect(screen.getByText('Mis tickets')).toBeTruthy()
    cleanup()
    abrir('/gestion-tickets', 'USR')
    expect(screen.getByText('Inicio')).toBeTruthy()
  })
})
