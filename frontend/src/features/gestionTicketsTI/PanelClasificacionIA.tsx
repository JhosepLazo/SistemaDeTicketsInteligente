/**
 * Archivo: PanelClasificacionIA.tsx
 * Objetivo: Ayudar a TI a clasificar un ticket con una propuesta de la IA y el historial de clasificaciones.
 * Responsabilidad: Pedir la propuesta (línea, item, tipo, subtipo y categoría con su confianza, señales, justificación y preguntas
 *   pendientes), mostrar quién clasificó antes y copiar la propuesta al formulario cuando TI decide usarla.
 * Dependencias: gestionTicketsTIApi (obtenerClasificacionesTI y proponerClasificacionTI) e Icono.
 * Flujo: modal Clasificar -> Proponer con IA -> Usar esta propuesta -> TI revisa y guarda con la matriz Item/Categoría.
 * Consideraciones: La IA solo propone: el ticket no cambia hasta que TI guarda la clasificación. Ante duda gana el tipo con menos
 *   autonomía, por eso las preguntas pendientes se muestran antes de aplicar.
 */

import { useEffect, useState } from 'react'
import { obtenerClasificacionesTI, proponerClasificacionTI, type ClasificacionTicketTI } from '../../services/gestionTicketsTIApi'
import Icono from '../../components/Icono'

export interface ClasificacionPropuesta {
  linea: string
  item: string
  tipo: string
  subTipo: string
  categoria: string
}

function fecha(valor: string) {
  return new Intl.DateTimeFormat('es-PE', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(valor))
}

const descripcion = (x: ClasificacionTicketTI) =>
  `${x.lineaDescripcion || x.linea} › ${x.itemDescripcion || x.item} · ${x.tipo}/${x.subTipoDescripcion || x.subTipo} · ${x.categoria}`

export default function PanelClasificacionIA({
  incidenciaNumero,
  onAplicar,
}: {
  incidenciaNumero: string
  onAplicar: (propuesta: ClasificacionPropuesta) => void
}) {
  const [historial, setHistorial] = useState<ClasificacionTicketTI[]>([])
  const [propuesta, setPropuesta] = useState<ClasificacionTicketTI | null>(null)
  const [proponiendo, setProponiendo] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    let vigente = true
    obtenerClasificacionesTI(incidenciaNumero)
      .then(lista => vigente && setHistorial(lista))
      .catch(() => vigente && setHistorial([]))
    return () => {
      vigente = false
    }
  }, [incidenciaNumero])

  async function proponer() {
    setProponiendo(true)
    setError('')
    try {
      const nueva = await proponerClasificacionTI(incidenciaNumero)
      setPropuesta(nueva)
      setHistorial(await obtenerClasificacionesTI(incidenciaNumero).catch(() => historial))
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No fue posible proponer una clasificación.')
    } finally {
      setProponiendo(false)
    }
  }

  return (
    <section className="gestion-ti-clasificacion-ia" aria-label="Propuesta de clasificación">
      <header>
        <div>
          <strong>
            <Icono nombre="asistenteDestellos" size={17} /> Propuesta de clasificación
          </strong>
          <span>La IA propone; el ticket solo cambia cuando guardas la clasificación.</span>
        </div>
        <button type="button" onClick={() => void proponer()} disabled={proponiendo}>
          {proponiendo ? 'Analizando...' : 'Proponer con IA'}
        </button>
      </header>
      {error && <p className="gestion-ti-clasificacion-ia__error">{error}</p>}
      {propuesta && (
        <article className="gestion-ti-clasificacion-ia__propuesta">
          <div>
            <strong>{descripcion(propuesta)}</strong>
            {propuesta.confianza !== null && <span>Confianza {Math.round(propuesta.confianza)}%</span>}
          </div>
          {propuesta.justificacion && <p>{propuesta.justificacion}</p>}
          {propuesta.senales.length > 0 && (
            <ul className="gestion-ti-clasificacion-ia__senales">
              {propuesta.senales.map(x => (
                <li key={x}>{x}</li>
              ))}
            </ul>
          )}
          {propuesta.preguntasPendientes.length > 0 && (
            <div className="gestion-ti-clasificacion-ia__preguntas">
              <b>Antes de aplicar, conviene confirmar:</b>
              <ul>
                {propuesta.preguntasPendientes.map(x => (
                  <li key={x}>{x}</li>
                ))}
              </ul>
            </div>
          )}
          <button
            type="button"
            onClick={() =>
              onAplicar({
                linea: propuesta.linea,
                item: propuesta.item,
                tipo: propuesta.tipo,
                subTipo: propuesta.subTipo,
                categoria: propuesta.categoria,
              })
            }
          >
            <Icono nombre="check" size={16} /> Usar esta propuesta
          </button>
        </article>
      )}
      {historial.length > 0 && (
        <details className="gestion-ti-clasificacion-ia__historial">
          <summary>Historial de clasificaciones ({historial.length})</summary>
          <ol>
            {historial.map(x => (
              <li key={x.secuencia}>
                <b>{x.origen === 'I' ? `IA${x.modelo ? ` · ${x.modelo}` : ''}` : 'TI'}</b> {descripcion(x)}
                <small>
                  {x.nombreUsuario || x.usuario} · {fecha(x.fechaClasificacion)}
                  {x.confianza !== null ? ` · ${Math.round(x.confianza)}%` : ''}
                </small>
              </li>
            ))}
          </ol>
        </details>
      )}
    </section>
  )
}
