/**
 * Archivo: InicioTIPage.tsx
 * Objetivo: Implementar el módulo Inicio para la visión del operador TI autenticado.
 * Responsabilidad: Presentar carga operativa, tickets que requieren intervención, recordatorios, tickets activos y actividad reciente sin duplicar funciones de los módulos especializados.
 * Dependencias: AutenticacionContext, inicioTIService, NotificacionesCampana, InicioPage.css e InicioTIPage.css.
 * Flujo: Ruta protegida /inicio -> selección por perfil -> InicioTIPage -> API /api/inicio/ti -> presentación, búsqueda, filtros y accesos a los módulos TI.
 * Consideraciones: El Inicio conserva únicamente acciones de resumen; la atención detallada se delega a Gestión de Tickets y los maestros se mantienen en su módulo dedicado.
 */

import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAutenticacion } from '../features/autenticacion/context/AutenticacionContext'
import {
  obtenerInicioTI,
  type InicioTIActividad,
  type InicioTIRespuesta,
  type InicioTITicketActivo,
  type InicioTITicketPrioritario,
} from '../features/inicioTI/services/inicioTIService'
import NotificacionesCampana from '../components/NotificacionesCampana'
import './InicioPage.css'
import './InicioTIPage.css'

type NombreIcono = 'inicio' | 'asistente' | 'gestion' | 'buscar' | 'campana' | 'carpeta' | 'engranaje' | 'alerta' | 'fuego' | 'check' | 'actividad' | 'salir' | 'flecha' | 'reloj' | 'aprobacion' | 'reabrir'
type FiltroAsignacion = 'TODOS' | 'MIOS'
type FiltroOperativo = 'TODOS' | 'PENDIENTES' | 'ATENCION' | 'PRIORIDAD_ALTA' | 'APROBACION' | 'REABIERTOS'
type FiltroSoporte = 'TODOS' | 'SOFTWARE' | 'HARDWARE'

const estadosPendientes = new Set(['NV', 'RC', 'PA', 'RA', 'PE'])
const estadosAtencion = new Set(['DG', 'EJ', 'ES', 'PV', 'AS', 'AT', 'PC', 'OB'])

function Icono({ nombre, size = 20 }: { nombre: NombreIcono; size?: number }) {
  const trazos: Record<NombreIcono, React.ReactNode> = {
    inicio: <><path d="M3 11.5 12 4l9 7.5"/><path d="M5.5 10.5V20h13v-9.5"/><path d="M9.5 20v-6h5v6"/></>,
    asistente: <><path d="m12 3 1.2 3.1L16 7.5l-2.8 1.4L12 12l-1.2-3.1L8 7.5l2.8-1.4L12 3Z"/><path d="m5 13 .8 2.2L8 16l-2.2.8L5 19l-.8-2.2L2 16l2.2-.8L5 13Z"/><path d="m19 12 .7 1.8L21.5 15l-1.8.7L19 17.5l-.7-1.8-1.8-.7 1.8-.7L19 12Z"/></>,
    gestion: <><rect x="4" y="4" width="16" height="16" rx="2"/><path d="M8 9h8M8 13h5M8 17h3"/><path d="m16 15 1.5 1.5L20 14"/></>,
    buscar: <><circle cx="11" cy="11" r="6"/><path d="m16 16 4 4"/></>,
    campana: <><path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9"/><path d="M10 21h4"/></>,
    carpeta: <path d="M3 7h6l2 2h10v10H3V7Z"/>,
    engranaje: <><circle cx="12" cy="12" r="3"/><path d="M19 12a7 7 0 0 0-.1-1l2-1.5-2-3.4-2.4 1a8 8 0 0 0-1.7-1L14.5 3h-5L9 6.1a8 8 0 0 0-1.7 1l-2.4-1-2 3.4 2 1.5a7 7 0 0 0 0 2l-2 1.5 2 3.4 2.4-1a8 8 0 0 0 1.7 1l.5 3.1h5l.5-3.1a8 8 0 0 0 1.7-1l2.4 1 2-3.4-2-1.5a7 7 0 0 0 .1-1Z"/></>,
    alerta: <><circle cx="12" cy="12" r="9"/><path d="M12 7v6M12 17h.01"/></>,
    fuego: <path d="M13 3c1 4-2 5-1 8 1-2 3-2 4-4 3 3 4 6 3 9a7 7 0 0 1-14 0c0-3 2-6 5-9 0 3 1 4 3 5-1-4 1-6 0-9Z"/>,
    check: <><circle cx="12" cy="12" r="9"/><path d="m8 12 2.5 2.5L16 9"/></>,
    actividad: <path d="M3 12h4l2-5 4 10 2-5h6"/>,
    salir: <><path d="M10 5H5v14h5"/><path d="m14 8 4 4-4 4M18 12H9"/></>,
    flecha: <><path d="M5 12h14M15 8l4 4-4 4"/></>,
    reloj: <><circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/></>,
    aprobacion: <><path d="M5 4h14v16H5z"/><path d="M8 9h8M8 13h5"/><path d="m14.5 16 1.5 1.5 3-3"/></>,
    reabrir: <><path d="M20 11a8 8 0 1 0-2.3 5.7"/><path d="M20 5v6h-6"/></>,
  }

  return <svg className="inicio-icono" width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">{trazos[nombre]}</svg>
}

function obtenerSaludo() {
  const hora = new Date().getHours()
  if (hora < 12) return 'Buenos días'
  if (hora < 19) return 'Buenas tardes'
  return 'Buenas noches'
}

function obtenerPrimerNombre(nombreCompleto: string) { return nombreCompleto.trim().split(/\s+/)[0] || nombreCompleto }

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
  const { usuario, cerrarSesion } = useAutenticacion()
  const [datos, setDatos] = useState<InicioTIRespuesta | null>(null)
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState('')
  const [busqueda, setBusqueda] = useState('')
  const [filtroAsignacion, setFiltroAsignacion] = useState<FiltroAsignacion>('TODOS')
  const [filtroOperativo, setFiltroOperativo] = useState<FiltroOperativo>('TODOS')
  const [filtroSoporte, setFiltroSoporte] = useState<FiltroSoporte>('TODOS')
  const [paginaTickets, setPaginaTickets] = useState(1)
  const buscadorRef = useRef<HTMLInputElement>(null)
  const pendientesRef = useRef<HTMLElement>(null)
  const tablaRef = useRef<HTMLElement>(null)

  async function cargarInicio() {
    setCargando(true)
    setError('')
    try { setDatos(await obtenerInicioTI()) }
    catch (e) { setError(e instanceof Error ? e.message : 'No fue posible cargar el inicio de TI.') }
    finally { setCargando(false) }
  }

  useEffect(() => { void cargarInicio() }, [])

  useEffect(() => {
    function manejarAtajos(event: KeyboardEvent) {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault(); buscadorRef.current?.focus(); buscadorRef.current?.select()
      }
      if (event.key === 'Escape' && document.activeElement === buscadorRef.current) { setBusqueda(''); buscadorRef.current?.blur() }
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
      if (!cumpleFiltroOperativo(ticket, filtroOperativo)) return false
      return !texto || `${ticket.incidenciaNumero} ${ticket.usuarioSolicitante} ${ticket.titulo} ${ticket.estadoDescripcion} ${ticket.responsable}`.toLowerCase().includes(texto)
    })
  }, [busqueda, datos, filtroAsignacion, filtroOperativo, filtroSoporte, usuario])

  const conteoSoftware = datos?.ticketsActivos.filter(ticket => ticket.grupoSoporte === 'SOFTWARE').length ?? 0
  const conteoHardware = datos?.ticketsActivos.filter(ticket => ticket.grupoSoporte === 'HARDWARE').length ?? 0

  const ticketsPorPagina = 10
  const totalPaginas = Math.max(1, Math.ceil(ticketsFiltrados.length / ticketsPorPagina))
  const paginaActual = Math.min(paginaTickets, totalPaginas)
  const ticketsPagina = ticketsFiltrados.slice((paginaActual - 1) * ticketsPorPagina, paginaActual * ticketsPorPagina)

  useEffect(() => { setPaginaTickets(1) }, [busqueda, filtroAsignacion, filtroOperativo, filtroSoporte])

  if (!usuario) return null

  async function manejarCierreSesion() { await cerrarSesion(); navigate('/login', { replace: true }) }

  const nombre = obtenerPrimerNombre(usuario.nombreCompleto)
  const hayFiltros = busqueda.trim().length > 0 || filtroAsignacion !== 'TODOS' || filtroOperativo !== 'TODOS' || filtroSoporte !== 'TODOS'
  const irArriba = () => window.scrollTo({ top: 0, behavior: 'smooth' })
  const irAElemento = (elemento: HTMLElement | null) => {
    if (!elemento) return
    const posicion = elemento.getBoundingClientRect().top + window.scrollY - 78
    window.scrollTo({ top: posicion, behavior: 'smooth' })
  }
  const irAPendientes = () => irAElemento(pendientesRef.current)
  const irATicketsActivos = () => irAElemento(tablaRef.current)
  const aplicarFiltroOperativo = (filtro: FiltroOperativo) => { setFiltroOperativo(filtro); requestAnimationFrame(irATicketsActivos) }
  const limpiarFiltros = () => { setBusqueda(''); setFiltroAsignacion('TODOS'); setFiltroOperativo('TODOS'); setFiltroSoporte('TODOS') }
  const irAGestion = () => navigate('/gestion-tickets')
  const irAConocimiento = () => navigate('/base-conocimiento')
  const irAReportes = () => navigate('/reportes')
  const irAConfiguracion = () => navigate('/configuracion-ti')

  return (
    <div className="inicio-shell inicio-ti-page">
      <aside className="inicio-sidebar" aria-label="Navegación principal">
        <div className="inicio-marca"><span className="inicio-marca__nombre">CALIMOD</span><span className="inicio-marca__sistema">Sistema inteligente<br />de incidencias TI</span></div>
        <nav className="inicio-menu">
          <button className="inicio-menu__item inicio-menu__item--activo" type="button" onClick={irArriba} aria-current="page"><Icono nombre="inicio" /> <span>Inicio</span></button>
          <button className="inicio-menu__item" type="button" disabled title="Se implementará en el módulo Asistente TI"><Icono nombre="asistente" /> <span>Asistente TI</span></button>
          <button className="inicio-menu__item" type="button" onClick={irAGestion}><Icono nombre="gestion" /> <span>Gestión de Tickets</span></button>
          <button className="inicio-menu__item" type="button" onClick={irAConocimiento}><Icono nombre="carpeta" /> <span>Base de Conocimiento</span></button>
          <button className="inicio-menu__item" type="button" onClick={irAReportes}><Icono nombre="actividad" /> <span>Reportes</span></button>
          <button className="inicio-menu__item" type="button" onClick={irAConfiguracion}><Icono nombre="engranaje" /> <span>Maestros TI</span></button>
        </nav>
        <div className="inicio-sidebar__mensaje"><span>La tecnología también impulsa grandes historias.</span><strong>CALIMOD</strong></div>
        <div className="inicio-sidebar__pie"><span className="inicio-sidebar__ayuda-icono">?</span><span>¿Necesitas ayuda?</span><small>Disponible desde Asistente TI</small></div>
      </aside>

      <section className="inicio-principal">
        <header className="inicio-topbar">
          <label className="inicio-buscador"><Icono nombre="buscar" size={19} /><input ref={buscadorRef} value={busqueda} onChange={event => setBusqueda(event.target.value)} placeholder="Buscar tickets, usuarios o responsables..." aria-label="Buscar tickets activos" /><span>Ctrl + K</span></label>
          <div className="inicio-topbar__usuario">
            <NotificacionesCampana />
            <div className="inicio-avatar" aria-hidden="true">{nombre.slice(0, 1).toUpperCase()}</div><div className="inicio-identidad"><strong>{nombre}</strong><span>Operador TI</span></div>
            <button className="inicio-salir" type="button" onClick={manejarCierreSesion} title="Cerrar sesión"><Icono nombre="salir" size={18} /></button>
          </div>
        </header>

        <main className="inicio-contenido">
          <section className="inicio-hero inicio-ti-hero">
            <div className="inicio-hero__contenido"><h1>{obtenerSaludo()}, {nombre} <span aria-hidden="true">👋</span></h1><p className="inicio-hero__resumen">{datos ? <>Hay <strong>{datos.resumen.pendientes} tickets pendientes</strong>{datos.resumen.requierenAccion > 0 ? <> y <strong>{datos.resumen.requierenAccion} requieren atención operativa</strong>.</> : '.'}</> : 'Aquí encontrarás el estado operativo de la atención TI.'}</p><p className="inicio-hero__detalle">Tu trabajo mantiene la operación en marcha. Prioriza, revisa y continúa sin perder contexto.</p></div>
            <div className="inicio-hero__acciones inicio-ti-hero__acciones"><button type="button" onClick={irAGestion}><Icono nombre="gestion" size={19} /> Gestionar tickets <Icono nombre="flecha" size={17} /></button><button type="button" className="inicio-hero__secundario" onClick={irAConocimiento}><Icono nombre="carpeta" size={19} /> Base de Conocimiento</button><button type="button" className="inicio-hero__secundario" disabled title="Disponible cuando se implemente Asistente TI"><Icono nombre="asistente" size={19} /> Consultar al Asistente TI</button></div>
            <div className="inicio-hero__firma"><span>Personas</span><span>que avanzan</span><strong>CALIMOD</strong></div>
          </section>

          {cargando && <section className="inicio-estado-carga" role="status"><span className="inicio-spinner" /> Cargando operación de TI...</section>}
          {!cargando && error && <section className="inicio-error" role="alert"><div><strong>No pudimos cargar el inicio de TI.</strong><span>{error}</span></div><button type="button" onClick={() => void cargarInicio()}>Reintentar</button></section>}

          {!cargando && datos && <>
            <section className="inicio-metricas" aria-label="Resumen operativo de TI">
              <button type="button" className={`inicio-metrica inicio-ti-metrica-interactiva ${filtroOperativo === 'PENDIENTES' ? 'inicio-ti-metrica--activa' : ''}`} onClick={() => aplicarFiltroOperativo('PENDIENTES')}><div className="inicio-metrica__icono inicio-metrica__icono--turquesa"><Icono nombre="carpeta" size={24} /></div><div><span>Pendientes</span><strong>{datos.resumen.pendientes}</strong><small>{datos.resumen.pendientesDesdeAyer} registrados desde ayer</small></div></button>
              <button type="button" className={`inicio-metrica inicio-ti-metrica-interactiva ${filtroOperativo === 'ATENCION' ? 'inicio-ti-metrica--activa' : ''}`} onClick={() => aplicarFiltroOperativo('ATENCION')}><div className="inicio-metrica__icono inicio-metrica__icono--azul"><Icono nombre="engranaje" size={24} /></div><div><span>En atención</span><strong>{datos.resumen.enAtencion}</strong><small>{datos.resumen.enProgresoHoy} con movimiento hoy</small></div></button>
              <button type="button" className="inicio-metrica inicio-ti-metrica-interactiva" onClick={irAPendientes}><div className="inicio-metrica__icono inicio-metrica__icono--rojo"><Icono nombre="alerta" size={24} /></div><div><span>Requieren acción</span><strong>{datos.resumen.requierenAccion}</strong><small>{datos.resumen.sinAsignar} sin asignar</small></div></button>
              <button type="button" className={`inicio-metrica inicio-ti-metrica-interactiva inicio-ti-metrica--prioridad ${filtroOperativo === 'PRIORIDAD_ALTA' ? 'inicio-ti-metrica--activa' : ''}`} onClick={() => aplicarFiltroOperativo('PRIORIDAD_ALTA')}><div className="inicio-metrica__icono inicio-ti-metrica__icono--prioridad"><Icono nombre="fuego" size={24} /></div><div><span>Prioridad alta</span><strong>{datos.resumen.prioridadAlta}</strong><small>requieren atención inmediata</small></div></button>
            </section>

            <section className="inicio-ti-grid-superior" ref={pendientesRef}>
              <article className="inicio-panel inicio-ti-panel-atencion">
                <div className="inicio-panel__cabecera"><div><span className="inicio-panel__titulo-icono inicio-panel__titulo-icono--rojo"><Icono nombre="alerta" size={18} /></span><div><h2>Requiere atención</h2><p>Tickets que necesitan una intervención operativa para continuar.</p></div></div><button className="inicio-panel__enlace" type="button" onClick={irAGestion}>Ver todos los pendientes <Icono nombre="flecha" size={14} /></button></div>
                <div className="inicio-ti-lista-atencion">{datos.requierenAtencion.length === 0 ? <div className="inicio-vacio"><Icono nombre="check" size={21} /><span>No hay tickets que requieran una intervención prioritaria.</span></div> : datos.requierenAtencion.map(ticket => <div className={`inicio-ti-ticket-atencion inicio-ti-ticket-atencion--${ticket.tipoAtencion.toLowerCase()}`} key={ticket.incidenciaNumero}><div className="inicio-ti-ticket-atencion__numero">{ticket.incidenciaNumero}</div><div className="inicio-ti-ticket-atencion__detalle"><strong>{ticket.titulo}</strong><span>{ticket.usuarioSolicitante} · {ticket.estadoDescripcion}</span></div><div className="inicio-ti-ticket-atencion__estado"><span className={`inicio-ti-atencion-badge inicio-ti-atencion-badge--${ticket.tipoAtencion.toLowerCase()}`}>{etiquetaAtencion(ticket)}</span><small>{detalleAtencion(ticket)}</small></div><button className="inicio-ticket-atencion__boton" type="button" onClick={irAGestion}>{textoAccion(ticket)} <Icono nombre="flecha" size={14} /></button></div>)}</div>
              </article>

              <article className="inicio-panel inicio-ti-panel-recordatorios">
                <div className="inicio-panel__cabecera"><div><span className="inicio-panel__titulo-icono"><Icono nombre="reloj" size={18} /></span><div><h2>Recordatorios operativos</h2><p>Mantén al día las tareas críticas del equipo.</p></div></div></div>
                <div className="inicio-ti-recordatorios"><button className="inicio-ti-recordatorio" type="button" disabled={datos.recordatorios.aprobacionesPendientes === 0} onClick={() => aplicarFiltroOperativo('APROBACION')}><span className="inicio-ti-recordatorio__icono inicio-ti-recordatorio__icono--amarillo"><Icono nombre="aprobacion" size={19} /></span><div><strong>Aprobaciones pendientes</strong><span>{datos.recordatorios.aprobacionesPendientes}</span><small>Solicitudes pendientes de respuesta</small></div><span className="inicio-ti-recordatorio__flecha">›</span></button><button className="inicio-ti-recordatorio" type="button" disabled={datos.recordatorios.slaPorVencer === 0} onClick={irAPendientes}><span className="inicio-ti-recordatorio__icono inicio-ti-recordatorio__icono--rojo"><Icono nombre="reloj" size={19} /></span><div><strong>SLA por vencer</strong><span>{datos.recordatorios.slaPorVencer}</span><small>En las próximas 4 horas</small></div><span className="inicio-ti-recordatorio__flecha">›</span></button><button className="inicio-ti-recordatorio" type="button" disabled={datos.recordatorios.ticketsReabiertos === 0} onClick={() => aplicarFiltroOperativo('REABIERTOS')}><span className="inicio-ti-recordatorio__icono inicio-ti-recordatorio__icono--violeta"><Icono nombre="reabrir" size={19} /></span><div><strong>Tickets reabiertos</strong><span>{datos.recordatorios.ticketsReabiertos}</span><small>Requieren nueva revisión</small></div><span className="inicio-ti-recordatorio__flecha">›</span></button></div>
              </article>
            </section>

            <section className="inicio-ti-grid-inferior" ref={tablaRef}>
              <article className="inicio-panel inicio-ti-panel-tabla">
                <div className="inicio-panel__cabecera inicio-ti-panel__cabecera-tabla"><div><span className="inicio-panel__titulo-icono"><Icono nombre="gestion" size={18} /></span><div><h2>Tickets activos</h2><p>Visualiza y prioriza la carga operativa actual.</p></div></div><div className="inicio-ti-tabs" role="group"><button type="button" className={filtroAsignacion === 'TODOS' ? 'inicio-ti-tab inicio-ti-tab--activo' : 'inicio-ti-tab'} onClick={() => setFiltroAsignacion('TODOS')}>Todos ({datos.resumen.ticketsActivos})</button><button type="button" className={filtroAsignacion === 'MIOS' ? 'inicio-ti-tab inicio-ti-tab--activo' : 'inicio-ti-tab'} onClick={() => setFiltroAsignacion('MIOS')}>Mis asignados ({datos.resumen.misAsignados})</button></div></div>
                <div className="inicio-ti-selector-soporte"><div><strong>Tipo de soporte</strong><span>Organiza la bandeja sin ocultar tickets.</span></div><div className="inicio-ti-segmentos" role="group" aria-label="Filtrar tickets por tipo de soporte"><button type="button" className={filtroSoporte === 'TODOS' ? 'activo' : ''} onClick={() => setFiltroSoporte('TODOS')}>Todos <span>{datos.ticketsActivos.length}</span></button><button type="button" className={filtroSoporte === 'SOFTWARE' ? 'activo' : ''} onClick={() => setFiltroSoporte('SOFTWARE')}>Software <span>{conteoSoftware}</span></button><button type="button" className={filtroSoporte === 'HARDWARE' ? 'activo' : ''} onClick={() => setFiltroSoporte('HARDWARE')}>Hardware <span>{conteoHardware}</span></button></div></div>
                {hayFiltros && <div className="inicio-ti-filtros-resumen" aria-live="polite"><span><strong>{ticketsFiltrados.length}</strong> tickets visibles</span>{filtroSoporte !== 'TODOS' && <span className="inicio-ti-filtro-chip">Soporte: {filtroSoporte === 'SOFTWARE' ? 'Software' : 'Hardware'}</span>}{filtroOperativo !== 'TODOS' && <span className="inicio-ti-filtro-chip">Vista: {textoFiltroOperativo(filtroOperativo)}</span>}{filtroAsignacion === 'MIOS' && <span className="inicio-ti-filtro-chip">Solo mis asignados</span>}{busqueda.trim() && <span className="inicio-ti-filtro-chip">Búsqueda: “{busqueda.trim()}”</span>}<button type="button" onClick={limpiarFiltros}>Limpiar filtros</button></div>}
                <div className="inicio-tabla-wrap"><table className="inicio-tabla inicio-ti-tabla"><thead><tr><th>Ticket</th><th>Usuario</th><th>Título</th><th>Prioridad</th><th>Estado</th><th>Responsable</th><th>Última actualización</th></tr></thead><tbody>{ticketsFiltrados.length === 0 ? <tr><td colSpan={7} className="inicio-tabla__vacio">No encontramos tickets con los filtros actuales.</td></tr> : ticketsPagina.map((ticket: InicioTITicketActivo) => <tr key={ticket.incidenciaNumero} onDoubleClick={irAGestion} title="Doble clic para abrir Gestión de Tickets"><td><strong>{ticket.incidenciaNumero}</strong></td><td>{ticket.usuarioSolicitante}</td><td><div className="inicio-ti-titulo-ticket"><span className={`inicio-ti-soporte inicio-ti-soporte--${ticket.grupoSoporte.toLowerCase()}`}>{ticket.grupoSoporte === 'SOFTWARE' ? 'Software' : ticket.grupoSoporte === 'HARDWARE' ? 'Hardware' : 'Otro'}</span><span>{ticket.titulo}</span></div></td><td><span className={clasePrioridad(ticket.prioridad)}>{textoPrioridad(ticket.prioridad)}</span></td><td><span className={claseEstado(ticket.estado)}>{ticket.estadoDescripcion}</span></td><td>{ticket.responsable}</td><td>{tiempoRelativo(ticket.ultimaFechaModif)}</td></tr>)}</tbody></table></div>
                {ticketsFiltrados.length > 0 && <nav className="inicio-ti-paginacion" aria-label="Paginación de tickets activos"><span>Mostrando {(paginaActual - 1) * ticketsPorPagina + 1}-{Math.min(paginaActual * ticketsPorPagina, ticketsFiltrados.length)} de {ticketsFiltrados.length}</span><div><button type="button" onClick={() => setPaginaTickets(valor => Math.max(1, valor - 1))} disabled={paginaActual === 1}>Anterior</button>{Array.from({ length: totalPaginas }, (_, indice) => indice + 1).map(pagina => <button type="button" key={pagina} className={pagina === paginaActual ? 'activo' : ''} aria-current={pagina === paginaActual ? 'page' : undefined} aria-label={`Página ${pagina}`} onClick={() => setPaginaTickets(pagina)}>{pagina}</button>)}<button type="button" onClick={() => setPaginaTickets(valor => Math.min(totalPaginas, valor + 1))} disabled={paginaActual === totalPaginas}>Siguiente</button></div></nav>}
              </article>

              <article className="inicio-panel inicio-ti-panel-actividad"><div className="inicio-panel__cabecera"><div><span className="inicio-panel__titulo-icono"><Icono nombre="actividad" size={18} /></span><div><h2>Actividad reciente</h2><p>Eventos relevantes de la operación.</p></div></div></div><div className="inicio-actividad-lista inicio-ti-actividad-lista">{datos.actividadReciente.length === 0 ? <div className="inicio-vacio"><span>No hay actividad reciente.</span></div> : datos.actividadReciente.map((actividad: InicioTIActividad, indice) => <div className="inicio-actividad inicio-ti-actividad" key={`${actividad.incidenciaNumero}-${actividad.fechaCambio}-${indice}`}><span className={`inicio-actividad__punto inicio-ti-actividad__punto--${actividad.estado.toLowerCase()}`} /><div><strong>{textoActividad(actividad)}</strong><span>{actividad.incidenciaNumero} · {actividad.estadoDescripcion}</span><small>{tiempoRelativo(actividad.fechaCambio)}</small></div></div>)}</div><div className="inicio-ti-frase"><span>“</span><p>Cada ticket resuelto<br />es un equipo que avanza.</p><strong>CALIMOD</strong></div></article>
            </section>
          </>}
        </main>
      </section>
    </div>
  )
}
