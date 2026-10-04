/**
 * Archivo: InicioUsuarioPage.tsx
 * Objetivo: Implementar el módulo Inicio para la visión del usuario autenticado.
 * Responsabilidad: Presentar el estado general de sus tickets, pendientes personales, actividad reciente y accesos principales sin duplicar funciones de otros módulos.
 * Dependencias: AutenticacionContext, inicioApi, NotificacionesCampana e InicioPage.css.
 * Flujo: Ruta protegida /inicio -> carga del dashboard -> API /api/inicio/usuario -> presentación de información personal.
 * Consideraciones: Inicio mantiene solo funciones de resumen y dirige a los módulos especializados.
 */

import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { primerNombre, useAutenticacion } from '../autenticacion/AutenticacionContext'
import { obtenerInicioUsuario, type InicioUsuarioRespuesta, type InicioUsuarioTicket } from '../../services/inicioApi'
import InvitacionesReproduccionAviso from '../reproduccion/InvitacionesReproduccionAviso'
import MarcoPortal from '../../components/MarcoPortal'
import Icono from '../../components/Icono'

function obtenerSaludo() {
  const hora = new Date().getHours()
  if (hora < 12) return 'Buenos días'
  if (hora < 19) return 'Buenas tardes'
  return 'Buenas noches'
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

export default function InicioUsuarioPage() {
  const navigate = useNavigate()
  const { usuario } = useAutenticacion()
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
    return datos.ticketsRecientes.filter(ticket =>
      `${ticket.incidenciaNumero} ${ticket.titulo} ${ticket.estadoDescripcion} ${ticket.responsable}`.toLowerCase().includes(texto),
    )
  }, [busqueda, datos])

  if (!usuario) return null

  const nombre = primerNombre(usuario.nombreCompleto)

  return (
    <MarcoPortal
      menu="usuario"
      activa="inicio"
      barra={
        <label className="inicio-buscador">
          <Icono nombre="buscar" size={19} />
          <input
            ref={buscadorRef}
            value={busqueda}
            onChange={event => setBusqueda(event.target.value)}
            placeholder="Buscar entre tus tickets recientes..."
            aria-label="Buscar tickets recientes"
          />
          <span>Ctrl + K</span>
        </label>
      }
    >
      <main className="inicio-contenido">
        <section className="inicio-hero">
          <div className="inicio-hero__contenido">
            <h1>
              {obtenerSaludo()}, {nombre} <span aria-hidden="true">👋</span>
            </h1>
            <p className="inicio-hero__resumen">
              {datos ? (
                <>
                  Tienes <strong>{datos.resumen.ticketsActivos} tickets activos</strong>
                  {datos.resumen.requierenAtencion > 0 ? (
                    <>
                      {' '}
                      y{' '}
                      <strong>
                        {datos.resumen.requierenAtencion} {datos.resumen.requierenAtencion === 1 ? 'requiere' : 'requieren'} tu atención
                      </strong>
                      .
                    </>
                  ) : (
                    '.'
                  )}
                </>
              ) : (
                'Aquí encontrarás el estado de tus solicitudes y pendientes.'
              )}
            </p>
            <p className="inicio-hero__detalle">Estamos aquí para ayudarte. La tecnología también impulsa tu trabajo.</p>
          </div>
          <div className="inicio-hero__acciones">
            <button type="button" onClick={() => navigate('/asistente')}>
              <Icono nombre="asistenteDestellos" size={19} /> Consultar al Asistente TI <Icono nombre="flecha" size={17} />
            </button>
            <button type="button" className="inicio-hero__secundario" onClick={() => navigate('/nuevo-ticket')}>
              <Icono nombre="nuevo" size={19} /> Reportar incidencia <Icono nombre="flecha" size={17} />
            </button>
          </div>
          <div className="inicio-hero__firma">
            <span>Personas</span>
            <span>que avanzan</span>
            <strong>CALIMOD</strong>
          </div>
        </section>

        <InvitacionesReproduccionAviso />

        {cargando && (
          <section className="inicio-estado-carga" role="status">
            <span className="inicio-spinner" /> Cargando tu información...
          </section>
        )}

        {!cargando && error && (
          <section className="inicio-error" role="alert">
            <div>
              <strong>No pudimos cargar tu inicio.</strong>
              <span>{error}</span>
            </div>
            <button type="button" onClick={() => void cargarInicio()}>
              Reintentar
            </button>
          </section>
        )}

        {!cargando && datos && (
          <>
            <section className="inicio-metricas" aria-label="Resumen de tickets">
              <article className="inicio-metrica">
                <div className="inicio-metrica__icono inicio-metrica__icono--turquesa">
                  <Icono nombre="carpeta" size={24} />
                </div>
                <div>
                  <span>Activos</span>
                  <strong>{datos.resumen.ticketsActivos}</strong>
                  <small>tickets abiertos</small>
                </div>
              </article>
              <article className="inicio-metrica">
                <div className="inicio-metrica__icono inicio-metrica__icono--azul">
                  <Icono nombre="engranaje" size={24} />
                </div>
                <div>
                  <span>En atención</span>
                  <strong>{datos.resumen.enAtencion}</strong>
                  <small>siendo gestionados</small>
                </div>
              </article>
              <article className="inicio-metrica">
                <div className="inicio-metrica__icono inicio-metrica__icono--rojo">
                  <Icono nombre="alerta" size={24} />
                </div>
                <div>
                  <span>Requieren tu atención</span>
                  <strong>{datos.resumen.requierenAtencion}</strong>
                  <small>pendientes de tu parte</small>
                </div>
              </article>
              <article className="inicio-metrica">
                <div className="inicio-metrica__icono inicio-metrica__icono--verde">
                  <Icono nombre="check" size={24} />
                </div>
                <div>
                  <span>Resueltos</span>
                  <strong>{datos.resumen.resueltos30Dias}</strong>
                  <small>en los últimos 30 días</small>
                </div>
              </article>
            </section>

            <section className="inicio-grid-superior" ref={pendientesRef}>
              <article className="inicio-panel inicio-panel--atencion">
                <div className="inicio-panel__cabecera">
                  <div>
                    <span className="inicio-panel__titulo-icono inicio-panel__titulo-icono--rojo">
                      <Icono nombre="alerta" size={18} />
                    </span>
                    <div>
                      <h2>Requiere tu atención</h2>
                      <p>Casos que necesitan información o confirmación de tu parte.</p>
                    </div>
                  </div>
                  <button className="inicio-panel__enlace" type="button" onClick={() => navigate('/mis-tickets')}>
                    Ver todos mis tickets <Icono nombre="flecha" size={14} />
                  </button>
                </div>
                <div className="inicio-lista-atencion">
                  {datos.requierenAtencion.length === 0 ? (
                    <div className="inicio-vacio">
                      <Icono nombre="check" size={21} />
                      <span>No tienes tickets esperando una acción tuya.</span>
                    </div>
                  ) : (
                    datos.requierenAtencion.map(ticket => (
                      <div className="inicio-ticket-atencion" key={ticket.incidenciaNumero}>
                        <div className="inicio-ticket-atencion__numero">{ticket.incidenciaNumero}</div>
                        <div className="inicio-ticket-atencion__detalle">
                          <strong>{ticket.titulo}</strong>
                          <span>{textoAccion(ticket)}</span>
                        </div>
                        <div className="inicio-ticket-atencion__estado">
                          <span className={claseEstado(ticket.estado)}>{ticket.estadoDescripcion}</span>
                          <small>{tiempoRelativo(ticket.ultimaFechaModif)}</small>
                        </div>
                        <button className="inicio-ticket-atencion__boton" type="button" onClick={() => navigate('/mis-tickets')}>
                          {ticket.accion === 'CONFIRMAR_SOLUCION' ? 'Confirmar' : 'Completar'} <Icono nombre="flecha" size={14} />
                        </button>
                      </div>
                    ))
                  )}
                </div>
              </article>

              <article className="inicio-panel inicio-panel--acciones">
                <div className="inicio-panel__cabecera">
                  <div>
                    <span className="inicio-panel__titulo-icono">
                      <Icono nombre="check" size={18} />
                    </span>
                    <div>
                      <h2>Pendientes de cierre</h2>
                      <p>Confirmaciones y calificaciones que aún puedes completar.</p>
                    </div>
                  </div>
                </div>
                <div className="inicio-acciones-lista">
                  {datos.accionesPendientes.length === 0 ? (
                    <div className="inicio-vacio">
                      <Icono nombre="check" size={21} />
                      <span>No tienes acciones pendientes de cierre.</span>
                    </div>
                  ) : (
                    datos.accionesPendientes.map(accion => (
                      <button
                        className="inicio-accion"
                        type="button"
                        key={`${accion.incidenciaNumero}-${accion.tipoAccion}`}
                        onClick={() => navigate('/mis-tickets')}
                      >
                        <span
                          className={`inicio-accion__icono ${accion.tipoAccion === 'CALIFICAR_ATENCION' ? 'inicio-accion__icono--estrella' : ''}`}
                        >
                          {accion.tipoAccion === 'CALIFICAR_ATENCION' ? '★' : '✓'}
                        </span>
                        <div>
                          <strong>{accion.tipoAccion === 'CALIFICAR_ATENCION' ? 'Calificar atención' : 'Confirmar solución'}</strong>
                          <span>{accion.incidenciaNumero}</span>
                          <small>{accion.titulo}</small>
                        </div>
                      </button>
                    ))
                  )}
                </div>
              </article>
            </section>

            <section className="inicio-grid-inferior">
              <article className="inicio-panel inicio-panel--tabla">
                <div className="inicio-panel__cabecera">
                  <div>
                    <span className="inicio-panel__titulo-icono">
                      <Icono nombre="tickets" size={18} />
                    </span>
                    <div>
                      <h2>Tus tickets recientes</h2>
                      <p>Consulta rápidamente el estado de tus últimos tickets.</p>
                    </div>
                  </div>
                  {busqueda ? (
                    <span className="inicio-resultados" aria-live="polite">
                      {ticketsFiltrados.length} resultado(s)
                    </span>
                  ) : (
                    <button className="inicio-panel__enlace" type="button" onClick={() => navigate('/mis-tickets')}>
                      Ver todos <Icono nombre="flecha" size={14} />
                    </button>
                  )}
                </div>
                <div className="inicio-tabla-wrap">
                  <table className="inicio-tabla">
                    <thead>
                      <tr>
                        <th>Ticket</th>
                        <th>Título</th>
                        <th>Estado</th>
                        <th>Responsable</th>
                        <th>Última actualización</th>
                      </tr>
                    </thead>
                    <tbody>
                      {ticketsFiltrados.length === 0 ? (
                        <tr>
                          <td colSpan={5} className="inicio-tabla__vacio">
                            No encontramos tickets con ese criterio.
                          </td>
                        </tr>
                      ) : (
                        ticketsFiltrados.map(ticket => (
                          <tr key={ticket.incidenciaNumero} onClick={() => navigate('/mis-tickets')} style={{ cursor: 'pointer' }}>
                            <td>
                              <strong>{ticket.incidenciaNumero}</strong>
                            </td>
                            <td>{ticket.titulo}</td>
                            <td>
                              <span className={claseEstado(ticket.estado)}>{ticket.estadoDescripcion}</span>
                            </td>
                            <td>{ticket.responsable}</td>
                            <td>{tiempoRelativo(ticket.ultimaFechaModif)}</td>
                          </tr>
                        ))
                      )}
                    </tbody>
                  </table>
                </div>
              </article>

              <article className="inicio-panel inicio-panel--actividad">
                <div className="inicio-panel__cabecera">
                  <div>
                    <span className="inicio-panel__titulo-icono">
                      <Icono nombre="actividad" size={18} />
                    </span>
                    <div>
                      <h2>Actividad reciente</h2>
                      <p>Últimos cambios registrados en tus tickets.</p>
                    </div>
                  </div>
                </div>
                <div className="inicio-actividad-lista">
                  {datos.actividadReciente.length === 0 ? (
                    <div className="inicio-vacio">
                      <span>No hay actividad reciente.</span>
                    </div>
                  ) : (
                    datos.actividadReciente.map((actividad, indice) => (
                      <div className="inicio-actividad" key={`${actividad.incidenciaNumero}-${actividad.fechaCambio}-${indice}`}>
                        <span className="inicio-actividad__punto" />
                        <div>
                          <strong>{actividad.estadoDescripcion}</strong>
                          <span>
                            {actividad.incidenciaNumero} · {actividad.titulo}
                          </span>
                          <small>
                            {actividad.actor} · {tiempoRelativo(actividad.fechaCambio)}
                          </small>
                        </div>
                      </div>
                    ))
                  )}
                </div>
              </article>
            </section>
          </>
        )}
      </main>
    </MarcoPortal>
  )
}
