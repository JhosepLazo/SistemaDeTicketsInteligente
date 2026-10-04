/**
 * Archivo: MarcoPortal.tsx
 * Objetivo: Dar a todas las pantallas del portal el mismo marco: barra lateral con el menú y barra superior con la
 *   identidad del usuario, la campana de notificaciones y el cierre de sesión.
 * Responsabilidad: Mostrar el menú del colaborador o el de TI marcando la pantalla actual, y dejar a cada página lo que
 *   le es propio: el buscador o título de la barra superior, el pie de la barra lateral y sus ventanas (capas).
 * Dependencias: React Router, AutenticacionContext, NotificacionesCampana, Icono y MarcoPortal.css.
 * Flujo: Página -> <MarcoPortal> -> barra lateral + barra superior + contenido de la página.
 * Consideraciones: El menú solo navega; cada ruta y cada endpoint vuelven a validar el perfil. Las clases conservan el
 *   prefijo inicio- porque el marco nació en la página de Inicio.
 */

import type { ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import { primerNombre, useAutenticacion } from '../features/autenticacion/AutenticacionContext'
import NotificacionesCampana from './NotificacionesCampana'
import Icono, { type NombreIcono } from './Icono'
import './MarcoPortal.css'

export type OpcionPortal = 'inicio' | 'asistente' | 'nuevoTicket' | 'misTickets' | 'gestion' | 'conocimiento' | 'reportes' | 'configuracion'

interface OpcionMenu {
  opcion: OpcionPortal
  ruta: string
  icono: NombreIcono
  texto: string
}

const MENUS: Record<'usuario' | 'ti', OpcionMenu[]> = {
  usuario: [
    { opcion: 'inicio', ruta: '/inicio', icono: 'inicio', texto: 'Inicio' },
    { opcion: 'asistente', ruta: '/asistente', icono: 'asistente', texto: 'Asistente TI' },
    { opcion: 'nuevoTicket', ruta: '/nuevo-ticket', icono: 'nuevo', texto: 'Nuevo Ticket' },
    { opcion: 'misTickets', ruta: '/mis-tickets', icono: 'tickets', texto: 'Mis Tickets' },
  ],
  ti: [
    { opcion: 'inicio', ruta: '/inicio', icono: 'inicio', texto: 'Inicio' },
    { opcion: 'asistente', ruta: '/asistente-ti', icono: 'asistente', texto: 'Asistente TI' },
    { opcion: 'gestion', ruta: '/gestion-tickets', icono: 'gestion', texto: 'Gestión de Tickets' },
    { opcion: 'conocimiento', ruta: '/base-conocimiento', icono: 'conocimiento', texto: 'Base de Conocimiento' },
    { opcion: 'reportes', ruta: '/reportes', icono: 'reporte', texto: 'Reportes' },
    { opcion: 'configuracion', ruta: '/configuracion-ti', icono: 'configuracion', texto: 'Maestros TI' },
  ],
}

/** Pie breve de la barra lateral: un símbolo, un título y un detalle. */
export interface AyudaPortal {
  icono?: string
  titulo: string
  detalle: string
}

const AYUDA: AyudaPortal = { icono: '?', titulo: '¿Necesitas ayuda?', detalle: 'Disponible desde Asistente TI' }

interface PropsMarco {
  /** Menú que se muestra: el del colaborador o el de TI. */
  menu: 'usuario' | 'ti'
  /** Opción del menú que corresponde a la pantalla actual (ninguna si no está en el menú). */
  activa?: OpcionPortal
  /** Clase propia de la pantalla en el contenedor (por ejemplo, gestion-ti-page). */
  clase?: string
  /** Izquierda de la barra superior: el buscador o el título de la pantalla. */
  barra: ReactNode
  claseBarra?: string
  /** Texto bajo el nombre; por defecto Colaborador u Operador TI según el menú. */
  rol?: string
  /** Pie de la barra lateral: la ayuda breve o, si se indica, un contenido propio. */
  ayuda?: AyudaPortal
  pie?: ReactNode
  /** Oculta la frase de marca que va sobre el pie. */
  sinFrase?: boolean
  /** Reemplaza la navegación del menú (por ejemplo, para guardar un borrador antes de salir). */
  alNavegar?: (ruta: string) => void
  /** Limpieza antes de cerrar la sesión (detener Live, olvidar la investigación activa). */
  antesDeSalir?: () => Promise<void> | void
  /** Ventanas que se dibujan fuera del área principal (modales). */
  capas?: ReactNode
  children: ReactNode
}

export default function MarcoPortal(props: PropsMarco) {
  const { menu, activa, clase, barra, claseBarra, rol, ayuda = AYUDA, pie, sinFrase, alNavegar, antesDeSalir, capas, children } = props
  const navigate = useNavigate()
  const { usuario, cerrarSesion } = useAutenticacion()
  const nombre = primerNombre(usuario?.nombreCompleto ?? '')
  const ir = (ruta: string) => (alNavegar ? alNavegar(ruta) : navigate(ruta))

  async function salir() {
    await antesDeSalir?.()
    await cerrarSesion()
    navigate('/login', { replace: true })
  }

  return (
    <div className={clase ? `inicio-shell ${clase}` : 'inicio-shell'}>
      <aside className="inicio-sidebar" aria-label="Navegación principal">
        <div className="inicio-marca">
          <span className="inicio-marca__nombre">CALIMOD</span>
          <span className="inicio-marca__sistema">
            Sistema inteligente
            <br />
            de incidencias TI
          </span>
        </div>
        <nav className="inicio-menu">
          {MENUS[menu].map(x => {
            const actual = x.opcion === activa
            // En Inicio, la opción activa vuelve al principio de la página; en las demás pantallas no hace nada.
            const alPulsar = !actual
              ? () => ir(x.ruta)
              : x.opcion === 'inicio'
                ? () => window.scrollTo({ top: 0, behavior: 'smooth' })
                : undefined
            return (
              <button
                key={x.opcion}
                className={actual ? 'inicio-menu__item inicio-menu__item--activo' : 'inicio-menu__item'}
                type="button"
                onClick={alPulsar}
                aria-current={actual ? 'page' : undefined}
              >
                <Icono nombre={x.icono} />
                <span>{x.texto}</span>
              </button>
            )
          })}
        </nav>
        {!sinFrase && (
          <div className="inicio-sidebar__mensaje">
            <span>La tecnología también impulsa grandes historias.</span>
            <strong>CALIMOD</strong>
          </div>
        )}
        {pie ?? (
          <div className="inicio-sidebar__pie">
            <span className="inicio-sidebar__ayuda-icono">{ayuda.icono ?? '?'}</span>
            <span>{ayuda.titulo}</span>
            <small>{ayuda.detalle}</small>
          </div>
        )}
      </aside>

      <section className="inicio-principal">
        <header className={claseBarra ? `inicio-topbar ${claseBarra}` : 'inicio-topbar'}>
          {barra}
          <div className="inicio-topbar__usuario">
            <NotificacionesCampana />
            <div className="inicio-avatar" aria-hidden="true">
              {nombre.slice(0, 1).toUpperCase()}
            </div>
            <div className="inicio-identidad">
              <strong>{nombre}</strong>
              <span>{rol ?? (menu === 'usuario' ? 'Colaborador' : 'Operador TI')}</span>
            </div>
            <button className="inicio-salir" type="button" onClick={() => void salir()} title="Cerrar sesión" aria-label="Cerrar sesión">
              <Icono nombre="salir" size={18} />
            </button>
          </div>
        </header>
        {children}
      </section>
      {capas}
    </div>
  )
}
