/**
 * Archivo: EditorGuiaDiagnostico.tsx
 * Objetivo: Editar la guía de diagnóstico estructurada de un artículo de la Base de Conocimiento.
 * Responsabilidad: Agregar, ordenar y quitar pasos (hasta 15), cada uno con la verificación a realizar, la herramienta del agente que la
 *   hace (opcional) y qué causa confirma o descarta su resultado.
 * Dependencias: baseConocimientoTIApi (PasoGuiaDiagnostico) e Icono.
 * Flujo: modal del artículo -> EditorGuiaDiagnostico -> formulario.guia -> crear/actualizar artículo.
 * Consideraciones: El agente sigue esta guía como playbook y cita sus pasos al diagnosticar. El backend valida largos y cantidad; cambiar
 *   la guía de un artículo publicado lo devuelve a Pendiente de validación.
 */

import type { PasoGuiaDiagnostico } from '../../services/baseConocimientoTIApi'
import Icono from '../../components/Icono'

const MAXIMO_PASOS = 15
const pasoVacio: PasoGuiaDiagnostico = { paso: '', herramienta: null, confirma: null, descarta: null }

export default function EditorGuiaDiagnostico({
  pasos,
  onCambiar,
}: {
  pasos: PasoGuiaDiagnostico[]
  onCambiar: (pasos: PasoGuiaDiagnostico[]) => void
}) {
  const actualizar = (indice: number, cambios: Partial<PasoGuiaDiagnostico>) =>
    onCambiar(pasos.map((x, i) => (i === indice ? { ...x, ...cambios } : x)))
  function mover(indice: number, destino: number) {
    const lista = [...pasos]
    const [paso] = lista.splice(indice, 1)
    lista.splice(destino, 0, paso)
    onCambiar(lista)
  }

  return (
    <fieldset className="conocimiento-ti-guia ancho-completo">
      <legend>
        Guía de diagnóstico <span>(opcional, hasta {MAXIMO_PASOS} pasos)</span>
      </legend>
      <p>Cada paso es una verificación de lectura en orden: qué revisar, con qué herramienta del agente y qué causa confirma o descarta.</p>
      {pasos.map((paso, indice) => (
        <div className="conocimiento-ti-guia__paso" key={indice}>
          <header>
            <strong>Paso {indice + 1}</strong>
            <div>
              <button type="button" onClick={() => mover(indice, indice - 1)} disabled={indice === 0} aria-label="Subir paso">
                ↑
              </button>
              <button
                type="button"
                onClick={() => mover(indice, indice + 1)}
                disabled={indice === pasos.length - 1}
                aria-label="Bajar paso"
              >
                ↓
              </button>
              <button type="button" onClick={() => onCambiar(pasos.filter((_, i) => i !== indice))} aria-label="Quitar paso">
                <Icono nombre="inactivar" size={15} />
              </button>
            </div>
          </header>
          <label>
            Qué revisar
            <textarea
              required
              minLength={5}
              maxLength={500}
              value={paso.paso}
              onChange={e => actualizar(indice, { paso: e.target.value })}
              placeholder="Ej. Revisar el log de distribución FIFO del documento."
            />
          </label>
          <label>
            Herramienta del agente <span>(opcional)</span>
            <input
              maxLength={40}
              value={paso.herramienta ?? ''}
              onChange={e => actualizar(indice, { herramienta: e.target.value.toUpperCase() || null })}
              placeholder="Ej. DIAG_TICKET_ESTADO"
            />
          </label>
          <label>
            Si el resultado es positivo, confirma
            <input maxLength={500} value={paso.confirma ?? ''} onChange={e => actualizar(indice, { confirma: e.target.value || null })} />
          </label>
          <label>
            Si es negativo, descarta
            <input maxLength={500} value={paso.descarta ?? ''} onChange={e => actualizar(indice, { descarta: e.target.value || null })} />
          </label>
        </div>
      ))}
      {pasos.length < MAXIMO_PASOS && (
        <button type="button" className="conocimiento-ti-guia__agregar" onClick={() => onCambiar([...pasos, { ...pasoVacio }])}>
          <Icono nombre="mas" size={16} /> Agregar paso
        </button>
      )}
    </fieldset>
  )
}
