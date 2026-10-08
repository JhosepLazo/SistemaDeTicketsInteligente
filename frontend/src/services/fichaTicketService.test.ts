/**
 * Archivo: fichaTicketService.test.ts
 * Objetivo: Comprobar que el formulario valida la ficha con las mismas reglas que el backend.
 * Responsabilidad: Cubrir obligatorios, largos, SI/NO, fechas reales, respuestas vacías, normalización y avance.
 * Dependencias: Vitest y fichaTicketService.ts.
 * Flujo: plantilla + respuestas -> validarFicha / fichaJson / avanceFicha.
 * Consideraciones: Los casos replican FichaTicketPruebas.cs del backend.
 */

import { describe, expect, it } from 'vitest'
import { avanceFicha, camposDelTipo, fichaJson, validarFicha, type CampoFichaTicket } from './fichaTicketService'

const campo = (x: Partial<CampoFichaTicket> & Pick<CampoFichaTicket, 'campo' | 'orden' | 'pregunta' | 'tipoDato'>): CampoFichaTicket => ({
  tipo: 'REQ',
  bloque: 'A. Contexto',
  ayuda: '',
  obligatorio: false,
  longitudMinima: 0,
  longitudMaxima: 500,
  ...x,
})

const plantilla = [
  campo({
    campo: 'VALIDA_JEFATURA',
    orden: 20,
    pregunta: '¿Tu jefatura validó el pedido?',
    tipoDato: 'SI_NO',
    obligatorio: true,
    longitudMinima: 2,
    longitudMaxima: 2,
  }),
  campo({
    campo: 'OBJETIVO_NEGOCIO',
    orden: 10,
    pregunta: '¿Qué objetivo persigue?',
    tipoDato: 'TEXTO_LARGO',
    obligatorio: true,
    longitudMinima: 10,
    longitudMaxima: 2000,
  }),
  campo({ campo: 'FECHA_LIMITE', orden: 30, pregunta: '¿Para qué fecha?', tipoDato: 'FECHA', longitudMinima: 10, longitudMaxima: 10 }),
  campo({ tipo: 'SOL', campo: 'ACCESO', orden: 10, pregunta: '¿Qué acceso necesitas?', tipoDato: 'TEXTO', obligatorio: true }),
]

describe('ficha del ticket', () => {
  it('toma solo los campos del tipo, en orden', () => {
    expect(camposDelTipo(plantilla, 'REQ').map(x => x.campo)).toEqual(['OBJETIVO_NEGOCIO', 'VALIDA_JEFATURA', 'FECHA_LIMITE'])
    expect(camposDelTipo(plantilla, 'INC')).toEqual([])
  })

  it('acepta una ficha completa y la normaliza', () => {
    const respuestas = { OBJETIVO_NEGOCIO: '  Reducir el cierre mensual  ', VALIDA_JEFATURA: 'si', FECHA_LIMITE: '', ACCESO: 'ERP' }

    expect(validarFicha(plantilla, 'REQ', respuestas)).toBe('')
    expect(JSON.parse(fichaJson(plantilla, 'REQ', respuestas)!)).toEqual({
      OBJETIVO_NEGOCIO: 'Reducir el cierre mensual',
      VALIDA_JEFATURA: 'SI',
    })
  })

  it.each([
    [{}, 'Completa la ficha: ¿Qué objetivo persigue?'],
    [{ OBJETIVO_NEGOCIO: 'Reducir el cierre mensual' }, 'Completa la ficha: ¿Tu jefatura validó el pedido?'],
    [{ OBJETIVO_NEGOCIO: 'Corto', VALIDA_JEFATURA: 'SI' }, 'Debe tener entre 10 y 2000 caracteres.'],
    [
      { OBJETIVO_NEGOCIO: 'Reducir el cierre mensual', VALIDA_JEFATURA: 'OK' },
      'Revisa la respuesta de la ficha: ¿Tu jefatura validó el pedido?',
    ],
    [
      { OBJETIVO_NEGOCIO: 'Reducir el cierre mensual', VALIDA_JEFATURA: 'SI', FECHA_LIMITE: '2026-02-30' },
      'Revisa la respuesta de la ficha: ¿Para qué fecha?',
    ],
    [
      { OBJETIVO_NEGOCIO: 'Reducir el cierre mensual', VALIDA_JEFATURA: 'SI', FECHA_LIMITE: '15/12/2026' },
      'Revisa la respuesta de la ficha: ¿Para qué fecha?',
    ],
  ])('rechaza %j con la pregunta afectada', (respuestas, mensaje) => {
    expect(validarFicha(plantilla, 'REQ', respuestas)).toContain(mensaje)
  })

  it('un tipo sin ficha no exige ni envía nada', () => {
    expect(validarFicha(plantilla, 'INC', { OBJETIVO_NEGOCIO: 'x' })).toBe('')
    expect(fichaJson(plantilla, 'INC', { OBJETIVO_NEGOCIO: 'x' })).toBeUndefined()
  })

  it('informa el avance de la ficha', () => {
    expect(avanceFicha(plantilla, 'REQ', { OBJETIVO_NEGOCIO: 'Reducir el cierre', FECHA_LIMITE: ' ' })).toEqual({
      respondidos: 1,
      total: 3,
    })
  })
})
