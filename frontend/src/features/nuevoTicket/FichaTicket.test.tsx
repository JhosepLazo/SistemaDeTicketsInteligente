/**
 * Archivo: FichaTicket.test.tsx
 * Objetivo: Comprobar la ficha que ve el colaborador al registrar un requerimiento.
 * Responsabilidad: Verificar bloques, obligatorios, el control según el tipo de dato, el aviso de cada cambio y la revisión con IA.
 * Dependencias: Vitest, Testing Library, FichaTicket y asistenteUsuarioApi (simulado).
 * Flujo: plantilla -> render -> cambios y revisión -> respuestas y observaciones visibles.
 * Consideraciones: La revisión con IA se simula; no hay red.
 */

import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { CampoFichaTicket } from '../../services/fichaTicketService'
import FichaTicket from './FichaTicket'

const revisarFicha = vi.hoisted(() => vi.fn())
vi.mock('../../services/asistenteUsuarioApi', () => ({ revisarFicha }))

const campos: CampoFichaTicket[] = [
  {
    tipo: 'REQ',
    campo: 'OBJETIVO_NEGOCIO',
    bloque: 'A. Contexto',
    orden: 10,
    pregunta: '¿Qué objetivo persigue?',
    ayuda: 'Un resultado medible.',
    tipoDato: 'TEXTO_LARGO',
    obligatorio: true,
    longitudMinima: 10,
    longitudMaxima: 2000,
  },
  {
    tipo: 'REQ',
    campo: 'VALIDA_JEFATURA',
    bloque: 'A. Contexto',
    orden: 20,
    pregunta: '¿Tu jefatura validó el pedido?',
    ayuda: '',
    tipoDato: 'SI_NO',
    obligatorio: true,
    longitudMinima: 2,
    longitudMaxima: 2,
  },
  {
    tipo: 'REQ',
    campo: 'FECHA_LIMITE',
    bloque: 'F. Priorización',
    orden: 30,
    pregunta: '¿Para qué fecha?',
    ayuda: '',
    tipoDato: 'FECHA',
    obligatorio: false,
    longitudMinima: 10,
    longitudMaxima: 10,
  },
]

afterEach(() => {
  cleanup()
  revisarFicha.mockReset()
})

function dibujar(alCambiar = vi.fn()) {
  render(
    <FichaTicket
      campos={campos}
      respuestas={{ OBJETIVO_NEGOCIO: 'Reducir el cierre' }}
      alCambiar={alCambiar}
      tipo="REQ"
      titulo="Reporte de cierre"
      detalle="Necesitamos un reporte."
    />,
  )
  return alCambiar
}

describe('ficha del ticket', () => {
  it('agrupa por bloque y usa el control de cada tipo de dato', () => {
    dibujar()

    expect(screen.getByText('A. Contexto')).toBeTruthy()
    expect(screen.getByText('F. Priorización')).toBeTruthy()
    expect(screen.getByText('Un resultado medible.')).toBeTruthy()
    expect((screen.getByLabelText(/Qué objetivo persigue/) as HTMLTextAreaElement).tagName).toBe('TEXTAREA')
    expect((screen.getByLabelText(/jefatura validó/) as HTMLSelectElement).tagName).toBe('SELECT')
    expect((screen.getByLabelText(/Para qué fecha/) as HTMLInputElement).type).toBe('date')
  })

  it('avisa cada respuesta a la página', () => {
    const alCambiar = dibujar()

    fireEvent.change(screen.getByLabelText(/jefatura validó/), { target: { value: 'SI' } })

    expect(alCambiar).toHaveBeenCalledWith('VALIDA_JEFATURA', 'SI')
  })

  it('muestra las observaciones de la revisión con IA', async () => {
    revisarFicha.mockResolvedValue({
      disponible: true,
      resumen: 'La ficha necesita precisión.',
      observaciones: [{ campo: 'OBJETIVO_NEGOCIO', tipo: 'VAGA', detalle: 'Indica cuánto debe reducirse.' }],
    })
    dibujar()

    fireEvent.click(screen.getByRole('button', { name: /Revisar ficha con IA/ }))

    expect(await screen.findByText('La ficha necesita precisión.')).toBeTruthy()
    expect(screen.getByText(/Indica cuánto debe reducirse/)).toBeTruthy()
    expect(revisarFicha).toHaveBeenCalledWith(
      'REQ',
      'Reporte de cierre',
      'Necesitamos un reporte.',
      '{"OBJETIVO_NEGOCIO":"Reducir el cierre"}',
    )
  })
})
