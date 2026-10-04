/**
 * Archivo: InicioTIPage.tsx
 * Objetivo: Implementar el módulo Inicio para la visión del operador TI autenticado.
 * Responsabilidad: Presentar carga operativa, tickets que requieren intervención, recordatorios, tickets activos y actividad reciente sin duplicar funciones de los módulos especializados.
 * Dependencias: AutenticacionContext, inicioApi, NotificacionesCampana, InicioPage.css e InicioTIPage.css.
 * Flujo: Ruta protegida /inicio -> selección por perfil -> InicioTIPage -> API /api/inicio/ti -> presentación, búsqueda, filtros y accesos a los módulos TI.
 * Consideraciones: El Inicio conserva únicamente acciones de resumen; la atención detallada se delega a Gestión de Tickets y los maestros se mantienen en su módulo dedicado.
 */

import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { primerNombre, useAutenticacion } from '../autenticacion/AutenticacionContext'
import {
  obtenerInicioTI,
  type InicioTIActividad,
  type InicioTIRespuesta,
  type InicioTITicketActivo,
  type InicioTITicketPrioritario,
} from '../../services/inicioApi'
import MarcoPortal from '../../components/MarcoPortal'
import Icono from '../../components/Icono'
import './InicioTIPage.css'

type FiltroAsignacion = 'TODOS' | 'MIOS'
type FiltroOperativo = 'TODOS' | 'PENDIENTES' | 'ATENCION' | 'PRIORIDAD_ALTA' | 'APROBACION' | 'REABIERTOS'
type FiltroSoporte = 'TODOS' | 'SOFTWARE' | 'HARDWARE'
type FiltroTipoTicket = 'TODOS' | '001' | '002' | '003'

const estadosPendientes = new Set(['NV', 'RC', 'PA', 'RA', 'PE'])
const estadosAtencion = new Set(['DG', 'EJ', 'ES', 'PV', 'AS', 'AT', 'PC', 'OB'])

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

function textoPrioridad(prioridad: number | null) {
  if (prioridad === null) return 'Sin definir'
  if (prioridad >= 4) return 'Alta'
  if (prioridad === 3) return 'Media'
  return 'Baja'
}

function clasePrioridad(prioridad: number | null) {
  if (prioridad === null) return 'inicio-ti-prioridad inicio-ti-prioridad--sin-definir'
  if (prioridad >= 4) return 'inicio-ti-prioridad inicio-ti-prioridad--alta'
  if (prioridad === 3) return 'inicio-ti-prioridad inicio-ti-prioridad--media'
  return 'inicio-ti-prioridad inicio-ti-prioridad--baja'
}

function claseEstado(estado: string) {
  if (estado === 'RS') return 'estado estado--resuelto'
  if (estado === 'PA' || estado === 'RC') return 'estado estado--pendiente'
  if (estado === 'ES' || estado === 'RA') return 'estado estado--alerta'
  return 'estado estado--atencion'
}

function claseTipoTicket(tipo: string) {
  if (tipo === '002') return 'requerimiento'
  if (tipo === '003') return 'solicitud'
  if (tipo === '001') return 'incidencia'
  return 'otro'
}

function cumpleFiltroOperativo(ticket: InicioTITicketActivo, filtro: FiltroOperativo) {
  if (filtro === 'PENDIENTES') return estadosPendientes.has(ticket.estado)
  if (filtro === 'ATENCION') return estadosAtencion.has(ticket.estado)
  if (filtro === 'PRIORIDAD_ALTA') return (ticket.prioridad ?? 0) >= 4
  if (filtro === 'APROBACION') return ticket.estado === 'PA'
  if (filtro === 'REABIERTOS') return ticket.estado === 'RA'
  return true
}

function textoFiltroOperativo(filtro: FiltroOperativo) {
  if (filtro === 'PENDIENTES') return 'Pendientes'
  if (filtro === 'ATENCION') return 'En atención'
  if (filtro === 'PRIORIDAD_ALTA') return 'Prioridad alta'
  if (filtro === 'APROBACION') return 'Aprobaciones'
  if (filtro === 'REABIERTOS') return 'Reabiertos'
  return 'Todos'
}

function etiquetaAtencion(ticket: InicioTITicketPrioritario) {
  if (ticket.tipoAtencion === 'SIN_ASIGNAR') return 'Sin asignar'
  if (ticket.tipoAtencion === 'REABIERTO') return 'Ticket reabierto'
  if (ticket.tipoAtencion === 'ESCALADO') return 'Caso escalado'
  if (ticket.tipoAtencion === 'APROBACION') return 'En aprobación'
  if (ticket.tipoAtencion === 'SLA_POR_VENCER') return 'SLA por vencer'
  return ticket.estadoDescripcion
}

function detalleAtencion(ticket: InicioTITicketPrioritario) {
  if (ticket.tipoAtencion === 'SIN_ASIGNAR') return `Creado ${tiempoRelativo(ticket.ultimaFechaModif)}`
  if (ticket.tipoAtencion === 'SLA_POR_VENCER' && ticket.slaMinutosRestantes !== null) {
    const minutos = Math.max(ticket.slaMinutosRestantes, 0)
    if (minutos < 60) return `SLA en ${minutos} min`
    const horas = Math.ceil(minutos / 60)
    return `SLA en ${horas} ${horas === 1 ? 'hora' : 'horas'}`
  }
  if (ticket.tipoAtencion === 'APROBACION') return `Esperando desde ${tiempoRelativo(ticket.ultimaFechaModif)}`
  return tiempoRelativo(ticket.ultimaFechaModif)
}

function textoAccion(ticket: InicioTITicketPrioritario) {
  if (ticket.accion === 'ASIGNAR') return 'Asignar'
  if (ticket.accion === 'CONTINUAR') return 'Continuar'
  return 'Revisar'
}

function textoActividad(actividad: InicioTIActividad) {
  if (actividad.estado === 'NV') return `${actividad.actor} registró un nuevo ticket`
  if (actividad.estado === 'RA') return `${actividad.actor} reabrió el ticket`
  if (actividad.estado === 'RS') return `${actividad.actor} resolvió el ticket`
  if (actividad.estado === 'PA') return `${actividad.actor} envió el ticket a aprobación`
  if (actividad.estado === 'PV') return `${actividad.actor} solicitó validación`
  if (actividad.estado === 'ES') return `${actividad.actor} escaló el ticket`
  return `${actividad.actor} actualizó el estado`
}

export default function InicioTIPage() {
  const navigate = useNavigate()
  const { usuario } = useAutenticacion()
  const [datos, setDatos] = useState<InicioTIRespuesta | null>(null)
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState('')
  const [busqueda, setBusqueda] = useState('')
  const [filtroAsignacion, setFiltroAsignacion] = useState<FiltroAsignacion>('TODOS')
  const [filtroOperativo, setFiltroOperativo] = useState<FiltroOperativo>('TODOS')
  const [filtroSoporte, setFiltroSoporte] = useState<FiltroSoporte>('TODOS')
  const [filtroTipoTicket, setFiltroTipoTicket] = useState<FiltroTipoTicket>('TODOS')
  const [paginaTickets, setPaginaTickets] = useState(1)
  const buscadorRef = useRef<HTMLInputElement>(null)
  const pendientesRef = useRef<HTMLElement>(null)
  const tablaRef = useRef<HTMLElement>(null)

  async function cargarInicio() {
    setCargando(true)
    setError('')
    try {
      setDatos(await obtenerInicioTI())
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No fue posible cargar el inicio de TI.')
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
    if (!datos || !usuario) return []
    const texto = busqueda.trim().toLowerCase()
    return datos.ticketsActivos.filter(ticket => {
      if (filtroAsignacion === 'MIOS' && ticket.usuarioTI !== usuario.usuario) return false
      if (filtroSoporte !== 'TODOS' && ticket.grupoSoporte !== filtroSoporte) return false
      if (filtroTipoTicket !== 'TODOS' && ticket.tipo !== filtroTipoTicket) return false
      if (!cumpleFiltroOperativo(ticket, filtroOperativo)) return false
      return (
        !texto ||
        `${ticket.incidenciaNumero} ${ticket.usuarioSolicitante} ${ticket.titulo} ${ticket.tipoDescripcion} ${ticket.estadoDescripcion} ${ticket.responsable}`
          .toLowerCase()
          .includes(texto)
      )
    })
  }, [busqueda, datos, filtroAsignacion, filtroOperativo, filtroSoporte, filtroTipoTicket, usuario])

  const conteoSoftware = datos?.ticketsActivos.filter(ticket => ticket.grupoSoporte === 'SOFTWARE').length ?? 0
  const conteoHardware = datos?.ticketsActivos.filter(ticket => ticket.grupoSoporte === 'HARDWARE').length ?? 0
  const conteoIncidencias = datos?.ticketsActivos.filter(ticket => ticket.tipo === '001').length ?? 0
  const conteoRequerimientos = datos?.ticketsActivos.filter(ticket => ticket.tipo === '002').length ?? 0
  const conteoSolicitudes = datos?.ticketsActivos.filter(ticket => ticket.tipo === '003').length ?? 0

  const ticketsPorPagina = 10
  const totalPaginas = Math.max(1, Math.ceil(ticketsFiltrados.length / ticketsPorPagina))
  const paginaActual = Math.min(paginaTickets, totalPaginas)
  const ticketsPagina = ticketsFiltrados.slice((paginaActual - 1) * ticketsPorPagina, paginaActual * ticketsPorPagina)

  useEffect(() => {
    setPaginaTickets(1)
  }, [busqueda, filtroAsignacion, filtroOperativo, filtroSoporte, filtroTipoTicket])

  if (!usuario) return null

  const nombre = primerNombre(usuario.nombreCompleto)
  const hayFiltros =
    busqueda.trim().length > 0 ||
    filtroAsignacion !== 'TODOS' ||
    filtroOperativo !== 'TODOS' ||
    filtroSoporte !== 'TODOS' ||
    filtroTipoTicket !== 'TODOS'
  const irAElemento = (elemento: HTMLElement | null) => {
    if (!elemento) return
    const posicion = elemento.getBoundingClientRect().top + window.scrollY - 78
    window.scrollTo({ top: posicion, behavior: 'smooth' })
  }
  const irAPendientes = () => irAElemento(pendientesRef.current)
  const irATicketsActivos = () => irAElemento(tablaRef.current)
  const aplicarFiltroOperativo = (filtro: FiltroOperativo) => {
    setFiltroOperativo(filtro)
    requestAnimationFrame(irATicketsActivos)
  }
  const limpiarFiltros = () => {
    setBusqueda('')
    setFiltroAsignacion('TODOS')
    setFiltroOperativo('TODOS')
    setFiltroSoporte('TODOS')
    setFiltroTipoTicket('TODOS')
  }
  const irAGestion = () => navigate('/gestion-tickets')
  const irAConocimiento = () => navigate('/base-conocimiento')

  return (
    <MarcoPortal
      menu="ti"
      activa="inicio"
      clase="inicio-ti-page"
      barra={
        <label className="inicio-buscador">
          <Icono nombre="buscar" size={19} />
          <input
            ref={buscadorRef}
            value={busqueda}
            onChange={event => setBusqueda(event.target.value)}
            placeholder="Buscar tickets, usuarios o responsables..."
            aria-label="Buscar tickets activos"
          />
          <span>Ctrl + K</span>
        </label>
      }
    >
      <main className="inicio-contenido">
        <section className="inicio-hero inicio-ti-hero">
          <div className="inicio-hero__contenido">
            <h1>
              {obtenerSaludo()}, {nombre} <span aria-hidden="true">👋</span>
            </h1>
            <p className="inicio-hero__resumen">
              {datos ? (
                <>
                  Hay <strong>{datos.resumen.pendientes} tickets pendientes</strong>
                  {datos.resumen.requierenAccion > 0 ? (
                    <>
                      {' '}
                      y <strong>{datos.resumen.requierenAccion} requieren atención operativa</strong>.
                    </>
                  ) : (
                    '.'
                  )}
                </>
              ) : (
                'Aquí encontrarás el estado operativo de la atención TI.'
              )}
            </p>
            <p className="inicio-hero__detalle">
              Tu trabajo mantiene la operación en marcha. Prioriza, revisa y continúa sin perder contexto.
            </p>
          </div>
          <div className="inicio-hero__acciones inicio-ti-hero__acciones">
            <button type="button" onClick={irAGestion}>
              <Icono nombre="gestion" size={19} /> Gestionar tickets <Icono nombre="flecha" size={17} />
            </button>
            <button type="button" className="inicio-hero__secundario" onClick={irAConocimiento}>
              <Icono nombre="carpeta" size={19} /> Base de Conocimiento
            </button>
            <button type="button" className="inicio-hero__secundario" onClick={() => navigate('/asistente-ti')}>
              <Icono nombre="asistenteDestellos" size={19} /> Consultar al Asistente TI
            </button>
          </div>
          <div className="inicio-hero__firma">
            <span>Personas</span>
            <span>que avanzan</span>
            <strong>CALIMOD</strong>
          </div>
        </section>

        {cargando && (
          <section className="inicio-estado-carga" role="status">
            <span className="inicio-spinner" /> Cargando operación de TI...
          </section>
        )}
        {!cargando && error && (
          <section className="inicio-error" role="alert">
            <div>
              <strong>No pudimos cargar el inicio de TI.</strong>
              <span>{error}</span>
            </div>
            <button type="button" onClick={() => void cargarInicio()}>
              Reintentar
            </button>
          </section>
        )}

        {!cargando && datos && (
          <>
            <section className="inicio-metricas" aria-label="Resumen operativo de TI">
              <button
                type="button"
                className={`inicio-metrica inicio-ti-metrica-interactiva ${filtroOperativo === 'PENDIENTES' ? 'inicio-ti-metrica--activa' : ''}`}
                onClick={() => aplicarFiltroOperativo('PENDIENTES')}
              >
                <div className="inicio-metrica__icono inicio-metrica__icono--turquesa">
                  <Icono nombre="carpeta" size={24} />
                </div>
                <div>
                  <span>Pendientes</span>
                  <strong>{datos.resumen.pendientes}</strong>
                  <small>{datos.resumen.pendientesDesdeAyer} registrados desde ayer</small>
                </div>
              </button>
              <button
                type="button"
                className={`inicio-metrica inicio-ti-metrica-interactiva ${filtroOperativo === 'ATENCION' ? 'inicio-ti-metrica--activa' : ''}`}
                onClick={() => aplicarFiltroOperativo('ATENCION')}
              >
                <div className="inicio-metrica__icono inicio-metrica__icono--azul">
                  <Icono nombre="engranaje" size={24} />
                </div>
                <div>
                  <span>En atención</span>
                  <strong>{datos.resumen.enAtencion}</strong>
                  <small>{datos.resumen.enProgresoHoy} con movimiento hoy</small>
                </div>
              </button>
              <button type="button" className="inicio-metrica inicio-ti-metrica-interactiva" onClick={irAPendientes}>
                <div className="inicio-metrica__icono inicio-metrica__icono--rojo">
                  <Icono nombre="alerta" size={24} />
                </div>
                <div>
                  <span>Requieren acción</span>
                  <strong>{datos.resumen.requierenAccion}</strong>
                  <small>{datos.resumen.sinAsignar} sin asignar</small>
                </div>
              </button>
              <button
                type="button"
                className={`inicio-metrica inicio-ti-metrica-interactiva inicio-ti-metrica--prioridad ${filtroOperativo === 'PRIORIDAD_ALTA' ? 'inicio-ti-metrica--activa' : ''}`}
                onClick={() => aplicarFiltroOperativo('PRIORIDAD_ALTA')}
              >
                <div className="inicio-metrica__icono inicio-ti-metrica__icono--prioridad">
                  <Icono nombre="fuego" size={24} />
                </div>
                <div>
                  <span>Prioridad alta</span>
                  <strong>{datos.resumen.prioridadAlta}</strong>
                  <small>requieren atención inmediata</small>
                </div>
              </button>
            </section>

            <section className="inicio-ti-grid-superior" ref={pendientesRef}>
              <article className="inicio-panel inicio-ti-panel-atencion">
                <div className="inicio-panel__cabecera">
                  <div>
                    <span className="inicio-panel__titulo-icono inicio-panel__titulo-icono--rojo">
                      <Icono nombre="alerta" size={18} />
                    </span>
                    <div>
                      <h2>Requiere atención</h2>
                      <p>Tickets que necesitan una intervención operativa para continuar.</p>
                    </div>
                  </div>
                  <button className="inicio-panel__enlace" type="button" onClick={irAGestion}>
                    Ver todos los pendientes <Icono nombre="flecha" size={14} />
                  </button>
                </div>
                <div className="inicio-ti-lista-atencion">
                  {datos.requierenAtencion.length === 0 ? (
                    <div className="inicio-vacio">
                      <Icono nombre="check" size={21} />
                      <span>No hay tickets que requieran una intervención prioritaria.</span>
                    </div>
                  ) : (
                    datos.requierenAtencion.map(ticket => (
                      <div
                        className={`inicio-ti-ticket-atencion inicio-ti-ticket-atencion--${ticket.tipoAtencion.toLowerCase()}`}
                        key={ticket.incidenciaNumero}
                      >
                        <div className="inicio-ti-ticket-atencion__numero">{ticket.incidenciaNumero}</div>
                        <div className="inicio-ti-ticket-atencion__detalle">
                          <strong>{ticket.titulo}</strong>
                          <div className="inicio-ti-ticket-atencion__meta">
                            <span className={`inicio-ti-tipo inicio-ti-tipo--${claseTipoTicket(ticket.tipo)}`}>
                              <i />
                              {ticket.tipoDescripcion || 'Otro'}
                            </span>
                            <span>
                              {ticket.usuarioSolicitante} · {ticket.estadoDescripcion}
                            </span>
                          </div>
                        </div>
                        <div className="inicio-ti-ticket-atencion__estado">
                          <span className={`inicio-ti-atencion-badge inicio-ti-atencion-badge--${ticket.tipoAtencion.toLowerCase()}`}>
                            {etiquetaAtencion(ticket)}
                          </span>
                          <small>{detalleAtencion(ticket)}</small>
                        </div>
                        <button className="inicio-ticket-atencion__boton" type="button" onClick={irAGestion}>
                          {textoAccion(ticket)} <Icono nombre="flecha" size={14} />
                        </button>
                      </div>
                    ))
                  )}
                </div>
              </article>

              <article className="inicio-panel inicio-ti-panel-recordatorios">
                <div className="inicio-panel__cabecera">
                  <div>
                    <span className="inicio-panel__titulo-icono">
                      <Icono nombre="reloj" size={18} />
                    </span>
                    <div>
                      <h2>Recordatorios operativos</h2>
                      <p>Mantén al día las tareas críticas del equipo.</p>
                    </div>
                  </div>
                </div>
                <div className="inicio-ti-recordatorios">
                  <button
                    className="inicio-ti-recordatorio"
                    type="button"
                    disabled={datos.recordatorios.aprobacionesPendientes === 0}
                    onClick={() => aplicarFiltroOperativo('APROBACION')}
                  >
                    <span className="inicio-ti-recordatorio__icono inicio-ti-recordatorio__icono--amarillo">
                      <Icono nombre="aprobacion" size={19} />
                    </span>
                    <div>
                      <strong>Aprobaciones pendientes</strong>
                      <span>{datos.recordatorios.aprobacionesPendientes}</span>
                      <small>Solicitudes pendientes de respuesta</small>
                    </div>
                    <span className="inicio-ti-recordatorio__flecha">›</span>
                  </button>
                  <button
                    className="inicio-ti-recordatorio"
                    type="button"
                    disabled={datos.recordatorios.slaPorVencer === 0}
                    onClick={irAPendientes}
                  >
                    <span className="inicio-ti-recordatorio__icono inicio-ti-recordatorio__icono--rojo">
                      <Icono nombre="reloj" size={19} />
                    </span>
                    <div>
                      <strong>SLA por vencer</strong>
                      <span>{datos.recordatorios.slaPorVencer}</span>
                      <small>En las próximas 4 horas</small>
                    </div>
                    <span className="inicio-ti-recordatorio__flecha">›</span>
                  </button>
                  <button
                    className="inicio-ti-recordatorio"
                    type="button"
                    disabled={datos.recordatorios.ticketsReabiertos === 0}
                    onClick={() => aplicarFiltroOperativo('REABIERTOS')}
                  >
                    <span className="inicio-ti-recordatorio__icono inicio-ti-recordatorio__icono--violeta">
                      <Icono nombre="reabrir" size={19} />
                    </span>
                    <div>
                      <strong>Tickets reabiertos</strong>
                      <span>{datos.recordatorios.ticketsReabiertos}</span>
                      <small>Requieren nueva revisión</small>
                    </div>
                    <span className="inicio-ti-recordatorio__flecha">›</span>
                  </button>
                </div>
              </article>
            </section>

            <section className="inicio-ti-grid-inferior" ref={tablaRef}>
              <article className="inicio-panel inicio-ti-panel-tabla">
                <div className="inicio-panel__cabecera inicio-ti-panel__cabecera-tabla">
                  <div>
                    <span className="inicio-panel__titulo-icono">
                      <Icono nombre="gestion" size={18} />
                    </span>
                    <div>
                      <h2>Tickets activos</h2>
                      <p>Visualiza y prioriza la carga operativa actual.</p>
                    </div>
                  </div>
                  <div className="inicio-ti-tabs" role="group">
                    <button
                      type="button"
                      className={filtroAsignacion === 'TODOS' ? 'inicio-ti-tab inicio-ti-tab--activo' : 'inicio-ti-tab'}
                      onClick={() => setFiltroAsignacion('TODOS')}
                    >
                      Todos ({datos.resumen.ticketsActivos})
                    </button>
                    <button
                      type="button"
                      className={filtroAsignacion === 'MIOS' ? 'inicio-ti-tab inicio-ti-tab--activo' : 'inicio-ti-tab'}
                      onClick={() => setFiltroAsignacion('MIOS')}
                    >
                      Mis asignados ({datos.resumen.misAsignados})
                    </button>
                  </div>
                </div>
                <div className="inicio-ti-selector-soporte">
                  <div className="inicio-ti-selector-soporte__intro">
                    <strong>Organiza tu bandeja</strong>
                    <span>Combina soporte y tipo de ticket para encontrar lo importante.</span>
                  </div>
                  <div className="inicio-ti-filtros-bandeja">
                    <div className="inicio-ti-filtro-grupo">
                      <span>Soporte</span>
                      <div className="inicio-ti-segmentos" role="group" aria-label="Filtrar tickets por tipo de soporte">
                        <button
                          type="button"
                          className={filtroSoporte === 'TODOS' ? 'activo' : ''}
                          onClick={() => setFiltroSoporte('TODOS')}
                        >
                          Todos <span>{datos.ticketsActivos.length}</span>
                        </button>
                        <button
                          type="button"
                          className={filtroSoporte === 'SOFTWARE' ? 'activo' : ''}
                          onClick={() => setFiltroSoporte('SOFTWARE')}
                        >
                          Software <span>{conteoSoftware}</span>
                        </button>
                        <button
                          type="button"
                          className={filtroSoporte === 'HARDWARE' ? 'activo' : ''}
                          onClick={() => setFiltroSoporte('HARDWARE')}
                        >
                          Hardware <span>{conteoHardware}</span>
                        </button>
                      </div>
                    </div>
                    <div className="inicio-ti-filtro-grupo">
                      <span>Tipo de ticket</span>
                      <div className="inicio-ti-tipos" role="group" aria-label="Filtrar por tipo de ticket">
                        <button
                          type="button"
                          className={filtroTipoTicket === 'TODOS' ? 'activo' : ''}
                          onClick={() => setFiltroTipoTicket('TODOS')}
                        >
                          Todos
                        </button>
                        <button
                          type="button"
                          className={`inicio-ti-tipo-filtro inicio-ti-tipo-filtro--incidencia ${filtroTipoTicket === '001' ? 'activo' : ''}`}
                          onClick={() => setFiltroTipoTicket('001')}
                        >
                          <i />
                          Incidencias <span>{conteoIncidencias}</span>
                        </button>
                        <button
                          type="button"
                          className={`inicio-ti-tipo-filtro inicio-ti-tipo-filtro--requerimiento ${filtroTipoTicket === '002' ? 'activo' : ''}`}
                          onClick={() => setFiltroTipoTicket('002')}
                        >
                          <i />
                          Requerimientos <span>{conteoRequerimientos}</span>
                        </button>
                        <button
                          type="button"
                          className={`inicio-ti-tipo-filtro inicio-ti-tipo-filtro--solicitud ${filtroTipoTicket === '003' ? 'activo' : ''}`}
                          onClick={() => setFiltroTipoTicket('003')}
                        >
                          <i />
                          Solicitudes <span>{conteoSolicitudes}</span>
                        </button>
                      </div>
                    </div>
                  </div>
                </div>
                {hayFiltros && (
                  <div className="inicio-ti-filtros-resumen" aria-live="polite">
                    <span>
                      <strong>{ticketsFiltrados.length}</strong> tickets visibles
                    </span>
                    {filtroSoporte !== 'TODOS' && (
                      <span className="inicio-ti-filtro-chip">Soporte: {filtroSoporte === 'SOFTWARE' ? 'Software' : 'Hardware'}</span>
                    )}
                    {filtroTipoTicket !== 'TODOS' && (
                      <span className={`inicio-ti-filtro-chip inicio-ti-filtro-chip--${claseTipoTicket(filtroTipoTicket)}`}>
                        Tipo: {filtroTipoTicket === '001' ? 'Incidencia' : filtroTipoTicket === '002' ? 'Requerimiento' : 'Solicitud'}
                      </span>
                    )}
                    {filtroOperativo !== 'TODOS' && (
                      <span className="inicio-ti-filtro-chip">Vista: {textoFiltroOperativo(filtroOperativo)}</span>
                    )}
                    {filtroAsignacion === 'MIOS' && <span className="inicio-ti-filtro-chip">Solo mis asignados</span>}
                    {busqueda.trim() && <span className="inicio-ti-filtro-chip">Búsqueda: “{busqueda.trim()}”</span>}
                    <button type="button" onClick={limpiarFiltros}>
                      Limpiar filtros
                    </button>
                  </div>
                )}
                <div className="inicio-tabla-wrap">
                  <table className="inicio-tabla inicio-ti-tabla">
                    <thead>
                      <tr>
                        <th>Ticket</th>
                        <th>Tipo</th>
                        <th>Solicitante</th>
                        <th>Asunto</th>
                        <th>Prioridad</th>
                        <th>Estado</th>
                        <th>Responsable</th>
                        <th>Actualización</th>
                        <th>
                          <span className="sr-only">Acciones</span>
                        </th>
                      </tr>
                    </thead>
                    <tbody>
                      {ticketsFiltrados.length === 0 ? (
                        <tr>
                          <td colSpan={9} className="inicio-tabla__vacio">
                            No encontramos tickets con los filtros actuales.
                          </td>
                        </tr>
                      ) : (
                        ticketsPagina.map((ticket: InicioTITicketActivo) => {
                          const claseTipo = claseTipoTicket(ticket.tipo)
                          return (
                            <tr
                              className={`inicio-ti-fila inicio-ti-fila--${claseTipo}`}
                              key={ticket.incidenciaNumero}
                              onDoubleClick={irAGestion}
                              title="Doble clic para abrir Gestión de Tickets"
                            >
                              <td>
                                <strong className="inicio-ti-ticket-numero">{ticket.incidenciaNumero}</strong>
                              </td>
                              <td>
                                <span className={`inicio-ti-tipo inicio-ti-tipo--${claseTipo}`}>
                                  <i />
                                  {ticket.tipoDescripcion || 'Otro'}
                                </span>
                              </td>
                              <td>
                                <span className="inicio-ti-solicitante">{ticket.usuarioSolicitante}</span>
                              </td>
                              <td>
                                <div className="inicio-ti-titulo-ticket">
                                  <span>{ticket.titulo}</span>
                                  <span className={`inicio-ti-soporte inicio-ti-soporte--${ticket.grupoSoporte.toLowerCase()}`}>
                                    {ticket.grupoSoporte === 'SOFTWARE'
                                      ? 'Software'
                                      : ticket.grupoSoporte === 'HARDWARE'
                                        ? 'Hardware'
                                        : 'Otro soporte'}
                                  </span>
                                </div>
                              </td>
                              <td>
                                <span className={clasePrioridad(ticket.prioridad)}>{textoPrioridad(ticket.prioridad)}</span>
                              </td>
                              <td>
                                <span className={claseEstado(ticket.estado)}>{ticket.estadoDescripcion}</span>
                              </td>
                              <td>
                                <span
                                  className={
                                    ticket.usuarioTI ? 'inicio-ti-responsable' : 'inicio-ti-responsable inicio-ti-responsable--sin-asignar'
                                  }
                                >
                                  {ticket.responsable}
                                </span>
                              </td>
                              <td>
                                <span className="inicio-ti-actualizacion" title={new Date(ticket.ultimaFechaModif).toLocaleString('es-PE')}>
                                  {tiempoRelativo(ticket.ultimaFechaModif)}
                                </span>
                              </td>
                              <td>
                                <button
                                  type="button"
                                  className="inicio-ti-abrir"
                                  onClick={irAGestion}
                                  title={`Abrir ${ticket.incidenciaNumero}`}
                                >
                                  Abrir <Icono nombre="flecha" size={13} />
                                </button>
                              </td>
                            </tr>
                          )
                        })
                      )}
                    </tbody>
                  </table>
                </div>
                {ticketsFiltrados.length > 0 && (
                  <nav className="inicio-ti-paginacion" aria-label="Paginación de tickets activos">
                    <span>
                      Mostrando {(paginaActual - 1) * ticketsPorPagina + 1}-
                      {Math.min(paginaActual * ticketsPorPagina, ticketsFiltrados.length)} de {ticketsFiltrados.length}
                    </span>
                    <div>
                      <button type="button" onClick={() => setPaginaTickets(valor => Math.max(1, valor - 1))} disabled={paginaActual === 1}>
                        Anterior
                      </button>
                      {Array.from({ length: totalPaginas }, (_, indice) => indice + 1).map(pagina => (
                        <button
                          type="button"
                          key={pagina}
                          className={pagina === paginaActual ? 'activo' : ''}
                          aria-current={pagina === paginaActual ? 'page' : undefined}
                          aria-label={`Página ${pagina}`}
                          onClick={() => setPaginaTickets(pagina)}
                        >
                          {pagina}
                        </button>
                      ))}
                      <button
                        type="button"
                        onClick={() => setPaginaTickets(valor => Math.min(totalPaginas, valor + 1))}
                        disabled={paginaActual === totalPaginas}
                      >
                        Siguiente
                      </button>
                    </div>
                  </nav>
                )}
              </article>

              <article className="inicio-panel inicio-ti-panel-actividad">
                <div className="inicio-panel__cabecera">
                  <div>
                    <span className="inicio-panel__titulo-icono">
                      <Icono nombre="actividad" size={18} />
                    </span>
                    <div>
                      <h2>Actividad reciente</h2>
                      <p>Eventos relevantes de la operación.</p>
                    </div>
                  </div>
                </div>
                <div className="inicio-actividad-lista inicio-ti-actividad-lista">
                  {datos.actividadReciente.length === 0 ? (
                    <div className="inicio-vacio">
                      <span>No hay actividad reciente.</span>
                    </div>
                  ) : (
                    datos.actividadReciente.map((actividad: InicioTIActividad, indice) => (
                      <div
                        className="inicio-actividad inicio-ti-actividad"
                        key={`${actividad.incidenciaNumero}-${actividad.fechaCambio}-${indice}`}
                      >
                        <span className={`inicio-actividad__punto inicio-ti-actividad__punto--${actividad.estado.toLowerCase()}`} />
                        <div>
                          <strong>{textoActividad(actividad)}</strong>
                          <span>
                            {actividad.incidenciaNumero} · {actividad.estadoDescripcion}
                          </span>
                          <small>{tiempoRelativo(actividad.fechaCambio)}</small>
                        </div>
                      </div>
                    ))
                  )}
                </div>
                <div className="inicio-ti-frase">
                  <span>“</span>
                  <p>
                    Cada ticket resuelto
                    <br />
                    es un equipo que avanza.
                  </p>
                  <strong>CALIMOD</strong>
                </div>
              </article>
            </section>
          </>
        )}
      </main>
    </MarcoPortal>
  )
}
