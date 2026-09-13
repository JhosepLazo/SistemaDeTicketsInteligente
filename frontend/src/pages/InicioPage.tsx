/**
 * Archivo: InicioPage.tsx
 * Objetivo: Implementar el módulo Inicio para la visión del usuario autenticado.
 * Responsabilidad: Presentar el estado general de sus tickets, pendientes personales, actividad reciente y accesos principales sin duplicar funciones de otros módulos.
 * Dependencias: AutenticacionContext, inicioUsuarioService y InicioPage.css.
 * Flujo: Ruta protegida /inicio -> carga del dashboard -> API /api/inicio/usuario -> presentación de información personal.
 * Consideraciones: Inicio mantiene solo funciones de resumen; Nuevo Ticket navega a su módulo propio y los accesos a Asistente TI y Mis Tickets permanecen deshabilitados hasta su implementación.
 */

import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAutenticacion } from '../features/autenticacion/context/AutenticacionContext'
import {
  obtenerInicioUsuario,
  type InicioUsuarioRespuesta,
  type InicioUsuarioTicket,
} from '../features/inicioUsuario/services/inicioUsuarioService'
import './InicioPage.css'

type NombreIcono = 'inicio' | 'asistente' | 'nuevo' | 'tickets' | 'buscar' | 'campana' | 'carpeta' | 'engranaje' | 'alerta' | 'check' | 'actividad' | 'salir' | 'flecha'

function Icono({ nombre, size = 20 }: { nombre: NombreIcono; size?: number }) {
  const trazos: Record<NombreIcono, React.ReactNode> = {
    inicio: <><path d="M3 11.5 12 4l9 7.5"/><path d="M5.5 10.5V20h13v-9.5"/><path d="M9.5 20v-6h5v6"/></>,
    asistente: <><path d="m12 3 1.2 3.1L16 7.5l-2.8 1.4L12 12l-1.2-3.1L8 7.5l2.8-1.4L12 3Z"/><path d="m5 13 .8 2.2L8 16l-2.2.8L5 19l-.8-2.2L2 16l2.2-.8L5 13Z"/><path d="m19 12 .7 1.8L21.5 15l-1.8.7L19 17.5l-.7-1.8-1.8-.7 1.8-.7L19 12Z"/></>,
    nuevo: <><circle cx="12" cy="12" r="9"/><path d="M12 8v8M8 12h8"/></>,
    tickets: <><path d="M5 4h14a1 1 0 0 1 1 1v14a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V5a1 1 0 0 1 1-1Z"/><path d="M8 9h8M8 13h6M8 17h4"/></>,
    buscar: <><circle cx="11" cy="11" r="6"/><path d="m16 16 4 4"/></>,
    campana: <><path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9"/><path d="M10 21h4"/></>,
    carpeta: <><path d="M3 7h6l2 2h10v10H3V7Z"/></>,
    engranaje: <><circle cx="12" cy="12" r="3"/><path d="M19 12a7 7 0 0 0-.1-1l2-1.5-2-3.4-2.4 1a8 8 0 0 0-1.7-1L14.5 3h-5L9 6.1a8 8 0 0 0-1.7 1l-2.4-1-2 3.4 2 1.5a7 7 0 0 0 0 2l-2 1.5 2 3.4 2.4-1a8 8 0 0 0 1.7 1l.5 3.1h5l.5-3.1a8 8 0 0 0 1.7-1l2.4 1 2-3.4-2-1.5a7 7 0 0 0 .1-1Z"/></>,
    alerta: <><circle cx="12" cy="12" r="9"/><path d="M12 7v6M12 17h.01"/></>,
    check: <><circle cx="12" cy="12" r="9"/><path d="m8 12 2.5 2.5L16 9"/></>,
    actividad: <><path d="M3 12h4l2-5 4 10 2-5h6"/></>,
    salir: <><path d="M10 5H5v14h5"/><path d="m14 8 4 4-4 4M18 12H9"/></>,
    flecha: <><path d="M5 12h14M15 8l4 4-4 4"/></>,
  }

  return <svg className="inicio-icono" width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">{trazos[nombre]}</svg>
}

function obtenerSaludo() {
  const hora = new Date().getHours()
  if (hora < 12) return 'Buenos días'
  if (hora < 19) return 'Buenas tardes'
  return 'Buenas noches'
}

function obtenerPrimerNombre(nombreCompleto: string) {
  return nombreCompleto.trim().split(/\s+/)[0] || nombreCompleto
}

function tiempoRelativo(fecha: string) {
  const valor = new Date(fecha).getTime()
  const diferencia = Date.now() - valor
  if (!Number.isFinite(valor) || diferencia < 0) return 'actualizado recientemente'

  const minutos = Math.floor(diferencia / 60000)
  if (minutos < 1) return 'ahora'
  if (minutos < 60) return `hace ${minutos} min`

  const horas = Math.floor(minutos / 60)
  if (horas < 24) return `hace ${horas} ${horas === 1 ? 'hora' : 'horas'}`

  const dias = Math.floor(horas / 24)
  return `hace ${dias} ${dias === 1 ? 'día' : 'días'}`
}

function claseEstado(estado: string) {
  if (estado === 'RS') return 'estado estado--resuelto'
  if (estado === 'RC' || estado === 'PV') return 'estado estado--pendiente'
  if (estado === 'ES' || estado === 'PA') return 'estado estado--alerta'
  return 'estado estado--atencion'
}

function textoAccion(ticket: InicioUsuarioTicket) {
  if (ticket.accion === 'COMPLETAR_INFORMACION') return 'Completar información'
  if (ticket.accion === 'CONFIRMAR_SOLUCION') return 'Confirmar solución'
  return 'Revisar ticket'
}

export default function InicioPage() {
  const navigate = useNavigate()
  const { usuario, cerrarSesion } = useAutenticacion()
  const [datos, setDatos] = useState<InicioUsuarioRespuesta | null>(null)
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState('')
  const [busqueda, setBusqueda] = useState('')
  const buscadorRef = useRef<HTMLInputElement>(null)
  const pendientesRef = useRef<HTMLElement>(null)

  async function cargarInicio() {
    setCargando(true)
    setError('')

    try {
      setDatos(await obtenerInicioUsuario())
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No fue posible cargar el inicio.')
    } finally {
      setCargando(false)
    }
  }

  useEffect(() => {
    void cargarInicio()
  }, [])

  useEffect(() => {
    function manejarAtajos(event: KeyboardEvent) {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault()
        buscadorRef.current?.focus()
        buscadorRef.current?.select()
      }

      if (event.key === 'Escape' && document.activeElement === buscadorRef.current) {
        setBusqueda('')
        buscadorRef.current?.blur()
      }
    }

    window.addEventListener('keydown', manejarAtajos)
    return () => window.removeEventListener('keydown', manejarAtajos)
  }, [])

  const ticketsFiltrados = useMemo(() => {
    if (!datos) return []
    const texto = busqueda.trim().toLowerCase()
    if (!texto) return datos.ticketsRecientes
    return datos.ticketsRecientes.filter(ticket => `${ticket.incidenciaNumero} ${ticket.titulo} ${ticket.estadoDescripcion} ${ticket.responsable}`.toLowerCase().includes(texto))
  }, [busqueda, datos])

  if (!usuario) return null

  async function manejarCierreSesion() {
    await cerrarSesion()
    navigate('/login', { replace: true })
  }

  const nombre = obtenerPrimerNombre(usuario.nombreCompleto)
  const totalNotificaciones = datos ? datos.resumen.requierenAtencion + datos.resumen.pendientesCalificacion : 0
  const irArriba = () => window.scrollTo({ top: 0, behavior: 'smooth' })
  const irAPendientes = () => {
    if (!pendientesRef.current) return
    const posicion = pendientesRef.current.getBoundingClientRect().top + window.scrollY - 78
    window.scrollTo({ top: posicion, behavior: 'smooth' })
  }

  return (
    <div className="inicio-shell">
      <aside className="inicio-sidebar" aria-label="Navegación principal">
        <div className="inicio-marca">
          <span className="inicio-marca__nombre">CALIMOD</span>
          <span className="inicio-marca__sistema">Sistema inteligente<br />de incidencias TI</span>
        </div>

        <nav className="inicio-menu">
          <button className="inicio-menu__item inicio-menu__item--activo" type="button" onClick={irArriba} aria-current="page"><Icono nombre="inicio" /> <span>Inicio</span></button>
          <button className="inicio-menu__item" type="button" disabled title="Se implementará en el módulo Asistente TI"><Icono nombre="asistente" /> <span>Asistente TI</span></button>
          <button className="inicio-menu__item" type="button" onClick={() => navigate('/nuevo-ticket')}><Icono nombre="nuevo" /> <span>Nuevo Ticket</span></button>
          <button className="inicio-menu__item" type="button" disabled title="Se implementará en el módulo Mis Tickets"><Icono nombre="tickets" /> <span>Mis Tickets</span></button>
        </nav>

        <div className="inicio-sidebar__mensaje">
          <span>La tecnología también impulsa grandes historias.</span>
          <strong>CALIMOD</strong>
        </div>

        <div className="inicio-sidebar__pie">
          <span className="inicio-sidebar__ayuda-icono">?</span>
          <span>¿Necesitas ayuda?</span>
          <small>Disponible desde Asistente TI</small>
        </div>
      </aside>

      <section className="inicio-principal">
        <header className="inicio-topbar">
          <label className="inicio-buscador">
            <Icono nombre="buscar" size={19} />
            <input ref={buscadorRef} value={busqueda} onChange={event => setBusqueda(event.target.value)} placeholder="Buscar entre tus tickets recientes..." aria-label="Buscar tickets recientes" />
            <span>Ctrl + K</span>
          </label>

          <div className="inicio-topbar__usuario">
            <button
              className="inicio-notificacion"
              type="button"
              onClick={irAPendientes}
              disabled={totalNotificaciones === 0}
              title={totalNotificaciones > 0 ? 'Ir a tus pendientes' : 'No tienes pendientes personales'}
              aria-label={totalNotificaciones > 0 ? `${totalNotificaciones} pendientes personales. Ir a pendientes.` : 'No tienes pendientes personales'}
              style={{ border: 0, padding: 0, background: 'transparent', cursor: totalNotificaciones > 0 ? 'pointer' : 'default' }}
            >
              <Icono nombre="campana" size={21} />
              {totalNotificaciones > 0 && <span>{totalNotificaciones > 9 ? '9+' : totalNotificaciones}</span>}
            </button>
            <div className="inicio-avatar" aria-hidden="true">{nombre.slice(0, 1).toUpperCase()}</div>
            <div className="inicio-identidad">
              <strong>{nombre}</strong>
              <span>{usuario.perfil === 'USR' ? 'Colaborador' : usuario.perfil}</span>
            </div>
            <button className="inicio-salir" type="button" onClick={manejarCierreSesion} title="Cerrar sesión" aria-label="Cerrar sesión"><Icono nombre="salir" size={18} /></button>
          </div>
        </header>

        <main className="inicio-contenido">
          <section className="inicio-hero">
            <div className="inicio-hero__contenido">
              <h1>{obtenerSaludo()}, {nombre} <span aria-hidden="true">👋</span></h1>
              <p className="inicio-hero__resumen">
                {datos ? <>Tienes <strong>{datos.resumen.ticketsActivos} tickets activos</strong>{datos.resumen.requierenAtencion > 0 ? <> y <strong>{datos.resumen.requierenAtencion} {datos.resumen.requierenAtencion === 1 ? 'requiere' : 'requieren'} tu atención</strong>.</> : '.'}</> : 'Aquí encontrarás el estado de tus solicitudes y pendientes.'}
              </p>
              <p className="inicio-hero__detalle">Estamos aquí para ayudarte. La tecnología también impulsa tu trabajo.</p>
            </div>
            <div className="inicio-hero__acciones">
              <button type="button" disabled title="Disponible cuando se implemente Asistente TI"><Icono nombre="asistente" size={19} /> Consultar al Asistente TI <Icono nombre="flecha" size={17} /></button>
              <button type="button" className="inicio-hero__secundario" onClick={() => navigate('/nuevo-ticket')}><Icono nombre="nuevo" size={19} /> Reportar incidencia <Icono nombre="flecha" size={17} /></button>
            </div>
            <div className="inicio-hero__firma"><span>Personas</span><span>que avanzan</span><strong>CALIMOD</strong></div>
          </section>

          {cargando && <section className="inicio-estado-carga" role="status"><span className="inicio-spinner" /> Cargando tu información...</section>}

          {!cargando && error && (
            <section className="inicio-error" role="alert">
              <div><strong>No pudimos cargar tu inicio.</strong><span>{error}</span></div>
              <button type="button" onClick={() => void cargarInicio()}>Reintentar</button>
            </section>
          )}

          {!cargando && datos && (
            <>
              <section className="inicio-metricas" aria-label="Resumen de tickets">
                <article className="inicio-metrica"><div className="inicio-metrica__icono inicio-metrica__icono--turquesa"><Icono nombre="carpeta" size={24} /></div><div><span>Activos</span><strong>{datos.resumen.ticketsActivos}</strong><small>tickets abiertos</small></div></article>
                <article className="inicio-metrica"><div className="inicio-metrica__icono inicio-metrica__icono--azul"><Icono nombre="engranaje" size={24} /></div><div><span>En atención</span><strong>{datos.resumen.enAtencion}</strong><small>siendo gestionados</small></div></article>
                <article className="inicio-metrica"><div className="inicio-metrica__icono inicio-metrica__icono--rojo"><Icono nombre="alerta" size={24} /></div><div><span>Requieren tu atención</span><strong>{datos.resumen.requierenAtencion}</strong><small>pendientes de tu parte</small></div></article>
                <article className="inicio-metrica"><div className="inicio-metrica__icono inicio-metrica__icono--verde"><Icono nombre="check" size={24} /></div><div><span>Resueltos</span><strong>{datos.resumen.resueltos30Dias}</strong><small>en los últimos 30 días</small></div></article>
              </section>

              <section className="inicio-grid-superior" ref={pendientesRef}>
                <article className="inicio-panel inicio-panel--atencion">
                  <div className="inicio-panel__cabecera">
                    <div><span className="inicio-panel__titulo-icono inicio-panel__titulo-icono--rojo"><Icono nombre="alerta" size={18} /></span><div><h2>Requiere tu atención</h2><p>Casos que necesitan información o confirmación de tu parte.</p></div></div>
                    <button className="inicio-panel__enlace" type="button" disabled title="Disponible cuando se implemente Mis Tickets">Ver todos mis tickets <Icono nombre="flecha" size={14} /></button>
                  </div>
                  <div className="inicio-lista-atencion">
                    {datos.requierenAtencion.length === 0 ? <div className="inicio-vacio"><Icono nombre="check" size={21} /><span>No tienes tickets esperando una acción tuya.</span></div> : datos.requierenAtencion.map(ticket => (
                      <div className="inicio-ticket-atencion" key={ticket.incidenciaNumero}>
                        <div className="inicio-ticket-atencion__numero">{ticket.incidenciaNumero}</div>
                        <div className="inicio-ticket-atencion__detalle"><strong>{ticket.titulo}</strong><span>{textoAccion(ticket)}</span></div>
                        <div className="inicio-ticket-atencion__estado"><span className={claseEstado(ticket.estado)}>{ticket.estadoDescripcion}</span><small>{tiempoRelativo(ticket.ultimaFechaModif)}</small></div>
                        <button className="inicio-ticket-atencion__boton" type="button" disabled title="Disponible desde el detalle del ticket">{ticket.accion === 'CONFIRMAR_SOLUCION' ? 'Confirmar' : 'Completar'} <Icono nombre="flecha" size={14} /></button>
                      </div>
                    ))}
                  </div>
                </article>

                <article className="inicio-panel inicio-panel--acciones">
                  <div className="inicio-panel__cabecera"><div><span className="inicio-panel__titulo-icono"><Icono nombre="check" size={18} /></span><div><h2>Pendientes de cierre</h2><p>Confirmaciones y calificaciones que aún puedes completar.</p></div></div></div>
                  <div className="inicio-acciones-lista">
                    {datos.accionesPendientes.length === 0 ? <div className="inicio-vacio"><Icono nombre="check" size={21} /><span>No tienes acciones pendientes de cierre.</span></div> : datos.accionesPendientes.map(accion => (
                      <div className="inicio-accion" key={`${accion.incidenciaNumero}-${accion.tipoAccion}`} title="La acción estará disponible desde el detalle del ticket">
                        <span className={`inicio-accion__icono ${accion.tipoAccion === 'CALIFICAR_ATENCION' ? 'inicio-accion__icono--estrella' : ''}`}>{accion.tipoAccion === 'CALIFICAR_ATENCION' ? '★' : '✓'}</span>
                        <div><strong>{accion.tipoAccion === 'CALIFICAR_ATENCION' ? 'Calificar atención' : 'Confirmar solución'}</strong><span>{accion.incidenciaNumero}</span><small>{accion.titulo}</small></div>
                      </div>
                    ))}
                  </div>
                </article>
              </section>

              <section className="inicio-grid-inferior">
                <article className="inicio-panel inicio-panel--tabla">
                  <div className="inicio-panel__cabecera"><div><span className="inicio-panel__titulo-icono"><Icono nombre="tickets" size={18} /></span><div><h2>Tus tickets recientes</h2><p>Consulta rápidamente el estado de tus últimos tickets.</p></div></div>{busqueda ? <span className="inicio-resultados" aria-live="polite">{ticketsFiltrados.length} resultado(s)</span> : <button className="inicio-panel__enlace" type="button" disabled title="Disponible cuando se implemente Mis Tickets">Ver todos <Icono nombre="flecha" size={14} /></button>}</div>
                  <div className="inicio-tabla-wrap">
                    <table className="inicio-tabla">
                      <thead><tr><th>Ticket</th><th>Título</th><th>Estado</th><th>Responsable</th><th>Última actualización</th></tr></thead>
                      <tbody>
                        {ticketsFiltrados.length === 0 ? <tr><td colSpan={5} className="inicio-tabla__vacio">No encontramos tickets con ese criterio.</td></tr> : ticketsFiltrados.map(ticket => (
                          <tr key={ticket.incidenciaNumero}><td><strong>{ticket.incidenciaNumero}</strong></td><td>{ticket.titulo}</td><td><span className={claseEstado(ticket.estado)}>{ticket.estadoDescripcion}</span></td><td>{ticket.responsable}</td><td>{tiempoRelativo(ticket.ultimaFechaModif)}</td></tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </article>

                <article className="inicio-panel inicio-panel--actividad">
                  <div className="inicio-panel__cabecera"><div><span className="inicio-panel__titulo-icono"><Icono nombre="actividad" size={18} /></span><div><h2>Actividad reciente</h2><p>Últimos cambios registrados en tus tickets.</p></div></div></div>
                  <div className="inicio-actividad-lista">
                    {datos.actividadReciente.length === 0 ? <div className="inicio-vacio"><span>No hay actividad reciente.</span></div> : datos.actividadReciente.map((actividad, indice) => (
                      <div className="inicio-actividad" key={`${actividad.incidenciaNumero}-${actividad.fechaCambio}-${indice}`}>
                        <span className="inicio-actividad__punto" />
                        <div><strong>{actividad.estadoDescripcion}</strong><span>{actividad.incidenciaNumero} · {actividad.titulo}</span><small>{actividad.actor} · {tiempoRelativo(actividad.fechaCambio)}</small></div>
                      </div>
                    ))}
                  </div>
                </article>
              </section>
            </>
          )}
        </main>
      </section>
    </div>
  )
}
