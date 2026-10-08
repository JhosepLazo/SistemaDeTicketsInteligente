/**
 * Archivo: FichaRegistrada.tsx
 * Objetivo: Mostrar la ficha que el colaborador completó al registrar su ticket.
 * Responsabilidad: Agrupar las respuestas por bloque, en el orden de la plantilla, con fechas y respuestas SI/NO legibles.
 * Dependencias: fichaTicketService (DatoFichaTicket y agruparPorBloque) y FichaRegistrada.css.
 * Flujo: Mis Tickets (detalle) y Gestión de Tickets (detalle completo) -> FichaRegistrada.
 * Consideraciones: Solo muestra; la ficha no se edita después de registrada. La pantalla decide el título y el contenedor.
 */

import { agruparPorBloque, type DatoFichaTicket } from '../services/fichaTicketService'
import './FichaRegistrada.css'

function valorLegible(dato: DatoFichaTicket) {
  if (dato.tipoDato === 'SI_NO') return dato.valor === 'SI' ? 'Sí' : dato.valor === 'NO' ? 'No' : dato.valor
  if (dato.tipoDato === 'FECHA' && /^\d{4}-\d{2}-\d{2}$/.test(dato.valor)) return dato.valor.split('-').reverse().join('/')
  return dato.valor
}

export default function FichaRegistrada({ datos }: { datos: DatoFichaTicket[] }) {
  const ordenados = [...datos].sort((a, b) => a.orden - b.orden)
  return (
    <div className="ficha-registrada">
      {agruparPorBloque(ordenados).map(([bloque, lista]) => (
        <section key={bloque}>
          <h4>{bloque}</h4>
          <dl>
            {lista.map(dato => (
              <div key={dato.campo}>
                <dt>{dato.pregunta}</dt>
                <dd>{valorLegible(dato)}</dd>
              </div>
            ))}
          </dl>
        </section>
      ))}
    </div>
  )
}
