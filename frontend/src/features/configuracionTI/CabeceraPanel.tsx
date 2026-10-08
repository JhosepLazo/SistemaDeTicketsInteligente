/**
 * Archivo: CabeceraPanel.tsx
 * Objetivo: Dibujar la cabecera común de los paneles de Maestros TI.
 * Responsabilidad: Mostrar ícono, título, subtítulo y una acción opcional con el mismo formato en todas las secciones.
 * Dependencias: Icono.
 * Flujo: ConfiguracionTIPage, ControlAgentePanel y FichasPanel -> CabeceraPanel.
 * Consideraciones: Los estilos están en ConfiguracionTIPage.css (config-ti-panel__cabecera).
 */

import type { ReactNode } from 'react'
import Icono, { type NombreIcono } from '../../components/Icono'

export default function CabeceraPanel({
  icono,
  titulo,
  subtitulo,
  accion,
}: {
  icono: NombreIcono
  titulo: string
  subtitulo: string
  accion?: ReactNode
}) {
  return (
    <header className="config-ti-panel__cabecera">
      <div>
        <span className="config-ti-panel__icono">
          <Icono nombre={icono} size={19} />
        </span>
        <div>
          <h2>{titulo}</h2>
          <p>{subtitulo}</p>
        </div>
      </div>
      {accion}
    </header>
  )
}
