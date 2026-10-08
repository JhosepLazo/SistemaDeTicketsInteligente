/**
 * Archivo: FichaTicket.tsx
 * Objetivo: Pedir al colaborador la ficha estructurada del tipo de ticket elegido (por ejemplo, la del requerimiento).
 * Responsabilidad: Mostrar los campos por bloque con su ayuda, el control que corresponde a cada tipo de dato y la revisión opcional con
 *   IA (respuestas vagas, contradicciones o tickets que parecen atender lo mismo).
 * Dependencias: fichaTicketService (tipos y JSON), asistenteUsuarioApi.revisarFicha e Icono.
 * Flujo: NuevoTicketPage -> FichaTicket -> respuestas en el estado de la página -> FichaJson al enviar.
 * Consideraciones: La revisión con IA es una ayuda: no cambia respuestas ni bloquea el envío. La validación obligatoria la hacen
 *   validarFicha en la página y el backend.
 */

import { useState } from 'react'
import { agruparPorBloque, fichaJson, type CampoFichaTicket, type RespuestasFicha } from '../../services/fichaTicketService'
import { revisarFicha, type RevisionFicha } from '../../services/asistenteUsuarioApi'
import Icono from '../../components/Icono'

interface PropsFichaTicket {
  /** Campos del tipo elegido, ya ordenados. */
  campos: CampoFichaTicket[]
  respuestas: RespuestasFicha
  alCambiar: (campo: string, valor: string) => void
  tipo: string
  titulo: string
  detalle: string
}

const textoObservacion: Record<string, string> = {
  VAGA: 'Respuesta poco concreta',
  CONTRADICCION: 'Contradicción',
  DUPLICADO: 'Posible duplicado',
  OTRA: 'Observación',
}

export default function FichaTicket({ campos, respuestas, alCambiar, tipo, titulo, detalle }: PropsFichaTicket) {
  const [revision, setRevision] = useState<RevisionFicha | null>(null)
  const [revisando, setRevisando] = useState(false)
  const [errorRevision, setErrorRevision] = useState('')
  const pregunta = (campo: string) => campos.find(x => x.campo === campo)?.pregunta ?? campo

  async function revisar() {
    setRevisando(true)
    setErrorRevision('')
    try {
      setRevision(await revisarFicha(tipo, titulo, detalle, fichaJson(campos, tipo, respuestas) ?? '{}'))
    } catch (e) {
      setRevision(null)
      setErrorRevision(e instanceof Error ? e.message : 'No fue posible revisar la ficha.')
    } finally {
      setRevisando(false)
    }
  }

  function control(campo: CampoFichaTicket) {
    const valor = respuestas[campo.campo] ?? ''
    const cambiar = (nuevo: string) => {
      alCambiar(campo.campo, nuevo)
      setRevision(null)
    }
    if (campo.tipoDato === 'TEXTO_LARGO')
      return <textarea value={valor} maxLength={campo.longitudMaxima} onChange={e => cambiar(e.target.value)} />
    if (campo.tipoDato === 'FECHA') return <input type="date" value={valor} onChange={e => cambiar(e.target.value)} />
    if (campo.tipoDato === 'SI_NO')
      return (
        <select value={valor} onChange={e => cambiar(e.target.value)}>
          <option value="">Selecciona una opción</option>
          <option value="SI">Sí</option>
          <option value="NO">No</option>
        </select>
      )
    return <input value={valor} maxLength={campo.longitudMaxima} onChange={e => cambiar(e.target.value)} />
  }

  return (
    <section className="nuevo-ticket-ficha" aria-label="Ficha del ticket">
      <header className="nuevo-ticket-ficha__cabecera">
        <div>
          <strong>
            <Icono nombre="lista" size={18} /> Ficha del ticket
          </strong>
          <span>Responde con datos concretos: TI necesita esta información para atender el pedido sin volver a preguntarte.</span>
        </div>
        <button
          type="button"
          className="nuevo-ticket-boton nuevo-ticket-boton--secundario"
          onClick={() => void revisar()}
          disabled={revisando}
        >
          <Icono nombre="asistenteDestellos" size={17} /> {revisando ? 'Revisando...' : 'Revisar ficha con IA'}
        </button>
      </header>
      {agruparPorBloque(campos).map(([bloque, lista]) => (
        <fieldset className="nuevo-ticket-ficha__bloque" key={bloque}>
          <legend>{bloque}</legend>
          {lista.map(campo => (
            <label className="nuevo-ticket-campo nuevo-ticket-campo--completo" key={campo.campo}>
              <span>
                {campo.pregunta} {campo.obligatorio && <b>*</b>}
              </span>
              {campo.ayuda && <small className="nuevo-ticket-ficha__ayuda">{campo.ayuda}</small>}
              {control(campo)}
            </label>
          ))}
        </fieldset>
      ))}
      {errorRevision && <div className="nuevo-ticket-alerta nuevo-ticket-alerta--error">{errorRevision}</div>}
      {revision && (
        <div className="nuevo-ticket-ficha__revision" role="status">
          <strong>
            <Icono nombre={revision.observaciones.length > 0 ? 'alerta' : 'check'} size={17} /> {revision.resumen}
          </strong>
          {revision.observaciones.length > 0 && (
            <ul>
              {revision.observaciones.map((x, i) => (
                <li key={`${x.campo}-${i}`}>
                  <b>{textoObservacion[x.tipo] ?? 'Observación'}</b> · {pregunta(x.campo)}: {x.detalle}
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </section>
  )
}
