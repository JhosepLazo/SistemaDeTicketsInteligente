/**
 * Archivo: GestionTicketsTIPage.tsx
 * Objetivo: Implementar el módulo Gestión de Tickets para el operador TI autenticado.
 * Responsabilidad: Mostrar la bandeja operativa, filtros, detalle rápido y flujo completo de clasificación, asignación, avances, recopilación, resolución, No Procede y aprobaciones.
 * Dependencias: AutenticacionContext, gestionTicketsTIService, InicioPage.css y GestionTicketsTIPage.css.
 * Flujo: Ruta protegida /gestion-tickets -> GestionTicketsTIPage -> API /api/gestion-tickets -> acciones controladas -> recarga de bandeja/detalle.
 * Consideraciones: Consolida funciones dispersas del sistema legado en una sola vista; no usa porcentaje manual de avance, no cierra directamente tickets y no ejecuta acciones automatizadas de negocio.
 */

import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAutenticacion } from '../features/autenticacion/context/AutenticacionContext'
import {
  asignarTicketTI,
  clasificarTicketTI,
  marcarNoProcedeTI,
  obtenerDetalleGestionTicketTI,
  obtenerGestionTicketsTI,
  registrarAvanceTI,
  resolverTicketTI,
  responderAprobacionTI,
  solicitarInformacionTI,
  urlAdjuntoGestionTI,
  type ClasificarTicketTISolicitud,
  type GestionTicketTIDetalle,
  type GestionTicketsTIRespuesta,
} from '../features/gestionTicketsTI/services/gestionTicketsTIService'
import './InicioPage.css'
import './GestionTicketsTIPage.css'

type IconoNombre = 'inicio' | 'asistente' | 'gestion' | 'conocimiento' | 'reporte' | 'buscar' | 'campana' | 'salir' | 'carpeta' | 'proceso' | 'alerta' | 'reabrir' | 'lista' | 'filtro' | 'descargar' | 'usuario' | 'mensaje' | 'editar' | 'check' | 'cerrar' | 'archivo' | 'actualizar'
type FiltroRapido = 'TODOS' | 'SIN_ASIGNAR' | 'MIOS' | 'PRIORIDAD_ALTA' | 'POR_VENCER'
type ModalAccion = 'DETALLE' | 'CLASIFICAR' | 'ASIGNAR' | 'AVANCE' | 'INFORMACION' | 'RESOLVER' | 'NO_PROCEDE' | null

function Icono({ nombre, size = 19 }: { nombre: IconoNombre; size?: number }) {
  const trazos: Record<IconoNombre, React.ReactNode> = {
    inicio: <><path d="M3 11.5 12 4l9 7.5"/><path d="M5.5 10.5V20h13v-9.5"/><path d="M9.5 20v-6h5v6"/></>,
    asistente: <><path d="m12 3 1.2 3.1L16 7.5l-2.8 1.4L12 12l-1.2-3.1L8 7.5l2.8-1.4L12 3Z"/><path d="m5 13 .8 2.2L8 16l-2.2.8L5 19l-.8-2.2L2 16l2.2-.8L5 13Z"/></>,
    gestion: <><rect x="4" y="4" width="16" height="16" rx="2"/><path d="M8 9h8M8 13h5M8 17h3"/><path d="m16 15 1.5 1.5L20 14"/></>,
    conocimiento: <><path d="M4 5.5A3.5 3.5 0 0 1 7.5 2H11v18H7.5A3.5 3.5 0 0 0 4 23V5.5ZM20 5.5A3.5 3.5 0 0 0 16.5 2H13v18h3.5A3.5 3.5 0 0 1 20 23V5.5Z"/></>,
    reporte: <><path d="M4 20V10M10 20V4M16 20v-7M22 20H2"/></>,
    buscar: <><circle cx="11" cy="11" r="6"/><path d="m16 16 4 4"/></>,
    campana: <><path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9"/><path d="M10 21h4"/></>,
    salir: <><path d="M10 5H5v14h5"/><path d="m14 8 4 4-4 4M18 12H9"/></>,
    carpeta: <path d="M3 7h6l2 2h10v10H3V7Z"/>,
    proceso: <><circle cx="12" cy="12" r="3"/><path d="M19 12a7 7 0 0 0-.1-1l2-1.5-2-3.4-2.4 1a8 8 0 0 0-1.7-1L14.5 3h-5L9 6.1a8 8 0 0 0-1.7 1l-2.4-1-2 3.4 2 1.5a7 7 0 0 0 0 2l-2 1.5 2 3.4 2.4-1a8 8 0 0 0 1.7 1l.5 3.1h5l.5-3.1a8 8 0 0 0 1.7-1l2.4 1 2-3.4-2-1.5a7 7 0 0 0 .1-1Z"/></>,
    alerta: <><circle cx="12" cy="12" r="9"/><path d="M12 7v6M12 17h.01"/></>,
    reabrir: <><path d="M20 11a8 8 0 1 0-2.3 5.7"/><path d="M20 5v6h-6"/></>,
    lista: <><path d="M8 6h12M8 12h12M8 18h12"/><path d="M4 6h.01M4 12h.01M4 18h.01"/></>,
    filtro: <path d="M3 5h18l-7 8v5l-4 2v-7L3 5Z"/>,
    descargar: <><path d="M12 3v12M8 11l4 4 4-4"/><path d="M4 20h16"/></>,
    usuario: <><circle cx="12" cy="8" r="4"/><path d="M4 21a8 8 0 0 1 16 0"/></>,
    mensaje: <><path d="M4 5h16v12H8l-4 4V5Z"/><path d="M8 9h8M8 13h5"/></>,
    editar: <><path d="M4 20h4L19 9l-4-4L4 16v4Z"/><path d="m13.5 6.5 4 4"/></>,
    check: <><circle cx="12" cy="12" r="9"/><path d="m8 12 2.5 2.5L16 9"/></>,
    cerrar: <path d="m6 6 12 12M18 6 6 18"/>,
    archivo: <><path d="M6 3h8l4 4v14H6V3Z"/><path d="M14 3v5h5"/></>,
    actualizar: <><path d="M20 7v5h-5"/><path d="M19 12a7 7 0 1 1-2-5"/></>,
  }

  return <svg className="inicio-icono" width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">{trazos[nombre]}</svg>
}

function primerNombre(nombre: string) { return nombre.trim().split(/\s+/)[0] || nombre }

function tiempoRelativo(fecha: string) {
  const valor = new Date(fecha).getTime()
  const minutos = Math.floor((Date.now() - valor) / 60000)
  if (!Number.isFinite(valor) || minutos < 1) return 'ahora'
  if (minutos < 60) return `hace ${minutos} min`
  const horas = Math.floor(minutos / 60)
  if (horas < 24) return `hace ${horas} ${horas === 1 ? 'hora' : 'horas'}`
  const dias = Math.floor(horas / 24)
  return `hace ${dias} ${dias === 1 ? 'día' : 'días'}`
}

function fechaHora(fecha: string) { return new Intl.DateTimeFormat('es-PE', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(fecha)) }
function textoPrioridad(valor: number | null) { return valor === null ? 'Sin definir' : valor >= 4 ? 'Alta' : valor === 3 ? 'Media' : 'Baja' }
function clasePrioridad(valor: number | null) { return `gestion-ti-badge gestion-ti-badge--${valor === null ? 'neutro' : valor >= 4 ? 'rojo' : valor === 3 ? 'amarillo' : 'verde'}` }
function claseEstado(estado: string) { return `gestion-ti-badge gestion-ti-badge--${estado === 'RS' ? 'verde' : estado === 'RA' ? 'violeta' : estado === 'CA' ? 'neutro' : ['RC', 'PA', 'PV'].includes(estado) ? 'amarillo' : estado === 'NV' ? 'rojo' : 'azul'}` }
function puedeOperar(estado: string) { return !['RS', 'CA', 'PV'].includes(estado) }

export default function GestionTicketsTIPage() {
  const navigate = useNavigate()
  const { usuario, cerrarSesion: finalizarSesion } = useAutenticacion()
  const buscadorRef = useRef<HTMLInputElement>(null)
  const filtrosRef = useRef<HTMLDivElement>(null)
  const [datos, setDatos] = useState<GestionTicketsTIRespuesta | null>(null)
  const [detalle, setDetalle] = useState<GestionTicketTIDetalle | null>(null)
  const [seleccionado, setSeleccionado] = useState('')
  const [cargando, setCargando] = useState(true)
  const [cargandoDetalle, setCargandoDetalle] = useState(false)
  const [procesando, setProcesando] = useState(false)
  const [error, setError] = useState('')
  const [mensaje, setMensaje] = useState('')
  const [busqueda, setBusqueda] = useState('')
  const [filtroRapido, setFiltroRapido] = useState<FiltroRapido>('TODOS')
  const [estado, setEstado] = useState('')
  const [prioridad, setPrioridad] = useState('')
  const [area, setArea] = useState('')
  const [responsable, setResponsable] = useState('')
  const [modal, setModal] = useState<ModalAccion>(null)
  const [textoAccion, setTextoAccion] = useState('')
  const [visibleUsuario, setVisibleUsuario] = useState(false)
  const [responsableAccion, setResponsableAccion] = useState('')
  const [clasificacion, setClasificacion] = useState<ClasificarTicketTISolicitud>({ linea: '', item: '', tipo: '', subTipo: '', categoria: '', areaCausante: null, prioridad: null, impacto: null, complejidad: null })
  const [resolucion, setResolucion] = useState({ causaRaiz: '', solucion: '', respuestaUsuario: '', tipoResolucion: 'CORRECCION' })

  async function cargarDetalle(numero: string) {
    setSeleccionado(numero)
    setCargandoDetalle(true)
    try { setDetalle(await obtenerDetalleGestionTicketTI(numero)) }
    catch (e) { setError(e instanceof Error ? e.message : 'No fue posible cargar el detalle.') }
    finally { setCargandoDetalle(false) }
  }

  async function cargarBandeja(preferido?: string) {
    setCargando(true)
    setError('')
    try {
      const respuesta = await obtenerGestionTicketsTI()
      setDatos(respuesta)
      const numero = preferido || seleccionado || respuesta.tickets[0]?.incidenciaNumero || ''
      if (numero) await cargarDetalle(numero)
      else { setSeleccionado(''); setDetalle(null) }
    } catch (e) { setError(e instanceof Error ? e.message : 'No fue posible cargar Gestión de Tickets.') }
    finally { setCargando(false) }
  }

  useEffect(() => { void cargarBandeja() }, [])
  useEffect(() => {
    const manejar = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') { e.preventDefault(); buscadorRef.current?.focus() }
      if (e.key === 'Escape' && !procesando) setModal(null)
    }
    window.addEventListener('keydown', manejar)
    return () => window.removeEventListener('keydown', manejar)
  }, [procesando])

  const ticketsFiltrados = useMemo(() => {
    if (!datos || !usuario) return []
    const texto = busqueda.trim().toLowerCase()
    return datos.tickets.filter(ticket => {
      if (filtroRapido === 'SIN_ASIGNAR' && ticket.usuarioTI) return false
      if (filtroRapido === 'MIOS' && ticket.usuarioTI !== usuario.usuario) return false
      if (filtroRapido === 'PRIORIDAD_ALTA' && (ticket.prioridad ?? 0) < 4) return false
      if (filtroRapido === 'POR_VENCER' && !(ticket.slaMinutosRestantes !== null && ticket.slaMinutosRestantes >= 0 && ticket.slaMinutosRestantes <= 1440)) return false
      if (estado && ticket.estado !== estado) return false
      if (prioridad === 'ALTA' && (ticket.prioridad ?? 0) < 4) return false
      if (prioridad === 'MEDIA' && ticket.prioridad !== 3) return false
      if (prioridad === 'BAJA' && (ticket.prioridad === null || ticket.prioridad > 2)) return false
      if (area && ticket.areaSolicitante !== area) return false
      if (responsable === 'SIN_ASIGNAR' && ticket.usuarioTI) return false
      if (responsable && responsable !== 'SIN_ASIGNAR' && ticket.usuarioTI !== responsable) return false
      return !texto || `${ticket.incidenciaNumero} ${ticket.solicitante} ${ticket.usuarioSolicitante} ${ticket.titulo} ${ticket.areaDescripcion} ${ticket.estadoDescripcion} ${ticket.responsable}`.toLowerCase().includes(texto)
    })
  }, [area, busqueda, datos, estado, filtroRapido, prioridad, responsable, usuario])

  if (!usuario) return null
  const nombre = primerNombre(usuario.nombreCompleto)

  function limpiarFiltros() {
    setBusqueda(''); setFiltroRapido('TODOS'); setEstado(''); setPrioridad(''); setArea(''); setResponsable('')
  }

  function abrirModal(tipo: Exclude<ModalAccion, null>) {
    if (!detalle) return
    setTextoAccion(''); setVisibleUsuario(false); setResponsableAccion(detalle.usuarioTI || usuario.usuario)
    setClasificacion({ linea: detalle.linea, item: detalle.item, tipo: detalle.tipo, subTipo: detalle.subTipo, categoria: detalle.categoria, areaCausante: detalle.areaCausante || null, prioridad: detalle.prioridad, impacto: detalle.impacto, complejidad: detalle.complejidad })
    setResolucion({ causaRaiz: detalle.causaRaiz, solucion: detalle.solucionTecnica, respuestaUsuario: detalle.respuestaUsuario || 'Se completó la atención. Por favor vuelve a realizar el proceso y confirma si el inconveniente fue solucionado.', tipoResolucion: detalle.tipoResolucion || 'CORRECCION' })
    setModal(tipo)
  }

  async function ejecutar(accion: () => Promise<void>, exito: string) {
    if (!detalle) return
    setProcesando(true); setError(''); setMensaje('')
    try { await accion(); setModal(null); setMensaje(exito); await cargarBandeja(detalle.incidenciaNumero) }
    catch (e) { setError(e instanceof Error ? e.message : 'No fue posible completar la operación.') }
    finally { setProcesando(false) }
  }

  function exportarCsv() {
    const filas = [['Ticket', 'Solicitante', 'Título', 'Área', 'Prioridad', 'Estado', 'Responsable', 'Última actualización'], ...ticketsFiltrados.map(x => [x.incidenciaNumero, x.solicitante, x.titulo, x.areaDescripcion, textoPrioridad(x.prioridad), x.estadoDescripcion, x.responsable, fechaHora(x.ultimaFechaModif)])]
    const contenido = filas.map(fila => fila.map(valor => `"${String(valor).replaceAll('"', '""')}"`).join(',')).join('\n')
    const url = URL.createObjectURL(new Blob([`\uFEFF${contenido}`], { type: 'text/csv;charset=utf-8' }))
    const enlace = document.createElement('a'); enlace.href = url; enlace.download = `gestion-tickets-${new Date().toISOString().slice(0, 10)}.csv`; enlace.click(); URL.revokeObjectURL(url)
  }

  return <div className="inicio-shell gestion-ti-page">
    <aside className="inicio-sidebar" aria-label="Navegación principal">
      <div className="inicio-marca"><span className="inicio-marca__nombre">CALIMOD</span><span className="inicio-marca__sistema">Sistema inteligente<br />de incidencias TI</span></div>
      <nav className="inicio-menu">
        <button className="inicio-menu__item" type="button" onClick={() => navigate('/inicio')}><Icono nombre="inicio" /> <span>Inicio</span></button>
        <button className="inicio-menu__item" type="button" disabled title="Se implementará en el módulo Asistente TI"><Icono nombre="asistente" /> <span>Asistente TI</span></button>
        <button className="inicio-menu__item inicio-menu__item--activo" type="button" aria-current="page"><Icono nombre="gestion" /> <span>Gestión de Tickets</span></button>
        <button className="inicio-menu__item" type="button" disabled title="Se implementará en Base de Conocimiento"><Icono nombre="conocimiento" /> <span>Base de Conocimiento</span></button>
        <button className="inicio-menu__item" type="button" disabled title="Se implementará en Reportes"><Icono nombre="reporte" /> <span>Reportes</span></button>
      </nav>
      <div className="inicio-sidebar__mensaje"><span>La tecnología también impulsa grandes historias.</span><strong>CALIMOD</strong></div>
      <div className="inicio-sidebar__pie"><span className="inicio-sidebar__ayuda-icono">?</span><span>¿Necesitas ayuda?</span><small>Disponible desde Asistente TI</small></div>
    </aside>

    <section className="inicio-principal">
      <header className="inicio-topbar">
        <label className="inicio-buscador"><Icono nombre="buscar" /><input ref={buscadorRef} value={busqueda} onChange={e => setBusqueda(e.target.value)} placeholder="Buscar tickets, usuarios o títulos..."/><span>Ctrl + K</span></label>
        <div className="inicio-topbar__usuario">
          <button className="inicio-notificacion" type="button" onClick={() => { setFiltroRapido('SIN_ASIGNAR'); filtrosRef.current?.scrollIntoView({ behavior: 'smooth' }) }} aria-label="Ver tickets sin asignar"><Icono nombre="campana" />{(datos?.resumen.sinAsignar ?? 0) > 0 && <span>{(datos?.resumen.sinAsignar ?? 0) > 9 ? '9+' : datos?.resumen.sinAsignar}</span>}</button>
          <div className="inicio-avatar">{nombre.slice(0, 1).toUpperCase()}</div><div className="inicio-identidad"><strong>{nombre}</strong><span>Operador TI</span></div>
          <button className="inicio-salir" type="button" onClick={() => void finalizarSesion().then(() => navigate('/login', { replace: true }))} title="Cerrar sesión"><Icono nombre="salir" /></button>
        </div>
      </header>

      <main className="inicio-contenido gestion-ti-contenido">
        <section className="inicio-hero gestion-ti-hero">
          <div className="inicio-hero__contenido"><h1>Gestión de Tickets</h1><p className="inicio-hero__resumen">Revisa, clasifica, asigna y da seguimiento a las incidencias de la organización.</p><p className="inicio-hero__detalle">Una sola bandeja para mantener el contexto, la trazabilidad y la atención técnica.</p></div>
          <div className="inicio-hero__acciones gestion-ti-hero__acciones"><button type="button" onClick={() => void cargarBandeja()}><Icono nombre="actualizar" /> Actualizar</button><button type="button" className="inicio-hero__secundario" onClick={() => filtrosRef.current?.scrollIntoView({ behavior: 'smooth' })}><Icono nombre="filtro" /> Filtros</button></div>
          <div className="inicio-hero__firma"><span>Personas</span><span>que avanzan</span><strong>CALIMOD</strong></div>
        </section>

        {error && <div className="gestion-ti-alerta gestion-ti-alerta--error" role="alert"><span>{error}</span><button type="button" onClick={() => setError('')}><Icono nombre="cerrar" size={16}/></button></div>}
        {mensaje && <div className="gestion-ti-alerta gestion-ti-alerta--ok" role="status"><Icono nombre="check"/><span>{mensaje}</span><button type="button" onClick={() => setMensaje('')}><Icono nombre="cerrar" size={16}/></button></div>}

        <section className="gestion-ti-metricas">
          <button type="button" onClick={() => { setFiltroRapido('TODOS'); setEstado('NV') }}><span className="gestion-ti-metrica__icono gestion-ti-metrica__icono--turquesa"><Icono nombre="carpeta"/></span><div><small>Pendientes</small><strong>{datos?.resumen.pendientes ?? 0}</strong><span>requieren revisión</span></div></button>
          <button type="button" onClick={() => { setFiltroRapido('TODOS'); setEstado('DG') }}><span className="gestion-ti-metrica__icono gestion-ti-metrica__icono--azul"><Icono nombre="proceso"/></span><div><small>En atención</small><strong>{datos?.resumen.enAtencion ?? 0}</strong><span>en gestión técnica</span></div></button>
          <button type="button" onClick={() => { setEstado(''); setFiltroRapido('POR_VENCER') }}><span className="gestion-ti-metrica__icono gestion-ti-metrica__icono--rojo"><Icono nombre="alerta"/></span><div><small>Por vencer</small><strong>{datos?.resumen.porVencer ?? 0}</strong><span>próximas 24 horas</span></div></button>
          <button type="button" onClick={() => { setFiltroRapido('TODOS'); setEstado('RA') }}><span className="gestion-ti-metrica__icono gestion-ti-metrica__icono--violeta"><Icono nombre="reabrir"/></span><div><small>Reabiertos</small><strong>{datos?.resumen.reabiertos ?? 0}</strong><span>requieren nueva revisión</span></div></button>
        </section>

        <section className="gestion-ti-grid" ref={filtrosRef}>
          <article className="gestion-ti-panel gestion-ti-bandeja">
            <div className="gestion-ti-panel__cabecera"><div><span className="gestion-ti-panel__icono"><Icono nombre="lista"/></span><div><h2>Listado de tickets</h2><p>Gestiona las incidencias de toda la organización.</p></div></div><button className="gestion-ti-boton-secundario" type="button" onClick={exportarCsv} disabled={!ticketsFiltrados.length}><Icono nombre="descargar" size={16}/> Exportar</button></div>
            <div className="gestion-ti-tabs"><button className={filtroRapido === 'TODOS' ? 'activo' : ''} onClick={() => setFiltroRapido('TODOS')}>Todos ({datos?.resumen.total ?? 0})</button><button className={filtroRapido === 'SIN_ASIGNAR' ? 'activo' : ''} onClick={() => setFiltroRapido('SIN_ASIGNAR')}>Sin asignar ({datos?.resumen.sinAsignar ?? 0})</button><button className={filtroRapido === 'MIOS' ? 'activo' : ''} onClick={() => setFiltroRapido('MIOS')}>Mis asignados ({datos?.resumen.misAsignados ?? 0})</button><button className={filtroRapido === 'PRIORIDAD_ALTA' ? 'activo' : ''} onClick={() => setFiltroRapido('PRIORIDAD_ALTA')}>Prioridad alta ({datos?.resumen.prioridadAlta ?? 0})</button></div>
            <div className="gestion-ti-filtros">
              <label>Estado<select value={estado} onChange={e => setEstado(e.target.value)}><option value="">Todos</option>{datos?.catalogos.estados.map(x => <option key={x.codigo} value={x.codigo}>{x.descripcion}</option>)}</select></label>
              <label>Prioridad<select value={prioridad} onChange={e => setPrioridad(e.target.value)}><option value="">Todas</option><option value="ALTA">Alta</option><option value="MEDIA">Media</option><option value="BAJA">Baja</option></select></label>
              <label>Área<select value={area} onChange={e => setArea(e.target.value)}><option value="">Todas</option>{datos?.catalogos.areas.map(x => <option key={x.codigo} value={x.codigo}>{x.descripcion}</option>)}</select></label>
              <label>Responsable<select value={responsable} onChange={e => setResponsable(e.target.value)}><option value="">Todos</option><option value="SIN_ASIGNAR">Sin asignar</option>{datos?.catalogos.operadores.map(x => <option key={x.codigo} value={x.codigo}>{x.descripcion}</option>)}</select></label>
              <button type="button" onClick={limpiarFiltros}>Limpiar</button>
            </div>
            <div className="gestion-ti-tabla-wrap"><table className="gestion-ti-tabla"><thead><tr><th>Ticket</th><th>Usuario</th><th>Título</th><th>Área</th><th>Prioridad</th><th>Estado</th><th>Responsable</th><th>Actualización</th><th>Acción</th></tr></thead><tbody>
              {cargando ? <tr><td colSpan={9} className="gestion-ti-tabla__vacio">Cargando tickets...</td></tr> : ticketsFiltrados.length === 0 ? <tr><td colSpan={9} className="gestion-ti-tabla__vacio">No hay tickets con los filtros seleccionados.</td></tr> : ticketsFiltrados.map(ticket => <tr key={ticket.incidenciaNumero} className={seleccionado === ticket.incidenciaNumero ? 'seleccionado' : ''} onClick={() => void cargarDetalle(ticket.incidenciaNumero)}><td><strong>{ticket.incidenciaNumero}</strong></td><td>{ticket.solicitante}</td><td className="gestion-ti-tabla__titulo">{ticket.titulo}</td><td>{ticket.areaDescripcion}</td><td><span className={clasePrioridad(ticket.prioridad)}>{textoPrioridad(ticket.prioridad)}</span></td><td><span className={claseEstado(ticket.estado)}>{ticket.estadoDescripcion}</span></td><td>{ticket.responsable}</td><td>{tiempoRelativo(ticket.ultimaFechaModif)}</td><td><button type="button" className="gestion-ti-accion-tabla" onClick={async e => { e.stopPropagation(); await cargarDetalle(ticket.incidenciaNumero); setModal('DETALLE') }}>{ticket.accion === 'ASIGNAR' ? 'Asignar' : ticket.accion === 'CONTINUAR' ? 'Continuar' : 'Ver detalle'}</button></td></tr>)}
            </tbody></table></div>
            <div className="gestion-ti-tabla__pie"><span><strong>{ticketsFiltrados.length}</strong> tickets visibles</span>{(busqueda || estado || prioridad || area || responsable || filtroRapido !== 'TODOS') && <button onClick={limpiarFiltros}>Quitar filtros</button>}</div>
          </article>

          <aside className="gestion-ti-columna-derecha">
            <article className="gestion-ti-panel gestion-ti-detalle-rapido"><div className="gestion-ti-panel__cabecera"><div><span className="gestion-ti-panel__icono"><Icono nombre="mensaje"/></span><div><h2>Detalle rápido del ticket</h2><p>Contexto esencial para decidir la siguiente acción.</p></div></div></div>
              {cargandoDetalle ? <div className="gestion-ti-vacio">Cargando detalle...</div> : !detalle ? <div className="gestion-ti-vacio">Selecciona un ticket de la bandeja.</div> : <><div className="gestion-ti-detalle__titulo"><div><strong>{detalle.incidenciaNumero}</strong><span className={clasePrioridad(detalle.prioridad)}>{textoPrioridad(detalle.prioridad)}</span></div><h3>{detalle.titulo}</h3><p>{detalle.detalle}</p></div><dl className="gestion-ti-datos"><DatoRapido label="Solicitante" valor={detalle.solicitante}/><DatoRapido label="Área" valor={detalle.areaSolicitanteDescripcion}/><DatoRapido label="Clasificación" valor={detalle.itemDescripcion || 'Pendiente'}/><DatoRapido label="Responsable" valor={detalle.responsable}/><DatoRapido label="Estado" valor={detalle.estadoDescripcion}/><DatoRapido label="SLA" valor={detalle.slaMinutosRestantes === null ? 'Sin objetivo' : detalle.slaMinutosRestantes < 0 ? `Vencido hace ${Math.abs(detalle.slaMinutosRestantes)} min` : `${detalle.slaMinutosRestantes} min restantes`}/></dl><div className="gestion-ti-linea-tiempo">{detalle.historialEstados.slice(-4).map((evento, i, arr) => <div key={evento.secuencia} className={i === arr.length - 1 ? 'actual' : ''}><span/><div><strong>{evento.estadoDescripcion}</strong><small>{fechaHora(evento.fechaCambio)}</small>{evento.observacion && <p>{evento.observacion}</p>}</div></div>)}</div><div className="gestion-ti-acciones-rapidas">{puedeOperar(detalle.estado) && <><button onClick={() => abrirModal('CLASIFICAR')}><Icono nombre="editar" size={16}/> Clasificar</button><button onClick={() => abrirModal('ASIGNAR')}><Icono nombre="usuario" size={16}/> {detalle.usuarioTI ? 'Reasignar' : 'Asignar'}</button><button onClick={() => abrirModal('AVANCE')}><Icono nombre="mensaje" size={16}/> Registrar avance</button></>}<button className="gestion-ti-acciones-rapidas__completo" onClick={() => abrirModal('DETALLE')}>Ver ticket completo →</button></div></>}
            </article>
            <article className="gestion-ti-panel gestion-ti-tareas"><div className="gestion-ti-panel__cabecera"><div><span className="gestion-ti-panel__icono"><Icono nombre="check"/></span><div><h2>Tareas del operador</h2><p>Acciones que requieren atención.</p></div></div></div><div className="gestion-ti-tareas__grid"><button onClick={() => { setEstado('PA'); setFiltroRapido('TODOS') }}><strong>{datos?.resumen.aprobacionesPendientes ?? 0}</strong><span>Aprobaciones pendientes</span></button><button onClick={() => { setEstado(''); setFiltroRapido('POR_VENCER') }}><strong>{datos?.resumen.porVencer ?? 0}</strong><span>SLA por vencer</span></button><button onClick={() => { setEstado(''); setFiltroRapido('PRIORIDAD_ALTA') }}><strong>{datos?.resumen.prioridadAlta ?? 0}</strong><span>Prioridad alta</span></button><button onClick={() => setFiltroRapido('SIN_ASIGNAR')}><strong>{datos?.resumen.sinAsignar ?? 0}</strong><span>Tickets sin asignar</span></button></div></article>
          </aside>
        </section>
      </main>
    </section>

    {modal && detalle && <div className="gestion-ti-modal-fondo" onMouseDown={e => { if (e.target === e.currentTarget && !procesando) setModal(null) }}><section className={`gestion-ti-modal ${modal === 'DETALLE' ? 'gestion-ti-modal--grande' : ''}`} role="dialog" aria-modal="true"><header><div><span>{detalle.incidenciaNumero}</span><h2>{tituloModal(modal, detalle.usuarioTI.length > 0)}</h2></div><button onClick={() => setModal(null)} disabled={procesando}><Icono nombre="cerrar"/></button></header>
      {modal === 'DETALLE' && <DetalleCompleto detalle={detalle} onAccion={abrirModal} onAprobar={(secuencia, aprobar) => void ejecutar(() => responderAprobacionTI(detalle.incidenciaNumero, secuencia, aprobar, aprobar ? 'Aprobado por el operador TI.' : 'Rechazado por el operador TI.'), aprobar ? 'La acción fue aprobada.' : 'La acción fue rechazada.')} />}
      {modal === 'CLASIFICAR' && datos && <form onSubmit={e => { e.preventDefault(); void ejecutar(() => clasificarTicketTI(detalle.incidenciaNumero, clasificacion), 'La clasificación técnica fue actualizada.') }}><div className="gestion-ti-form-grid"><label>Línea<select required value={clasificacion.linea} onChange={e => setClasificacion(v => ({ ...v, linea: e.target.value, item: '' }))}>{datos.catalogos.lineas.map(x => <option key={x.codigo} value={x.codigo}>{x.descripcion}</option>)}</select></label><label>Item<select required value={clasificacion.item} onChange={e => setClasificacion(v => ({ ...v, item: e.target.value }))}><option value="">Seleccionar</option>{datos.catalogos.items.filter(x => x.linea === clasificacion.linea).map(x => <option key={x.codigo} value={x.codigo}>{x.descripcion}</option>)}</select></label><label>Tipo<select required value={clasificacion.tipo} onChange={e => setClasificacion(v => ({ ...v, tipo: e.target.value, subTipo: '' }))}>{datos.catalogos.tipos.map(x => <option key={x.codigo} value={x.codigo}>{x.descripcion}</option>)}</select></label><label>Categoría<select required value={clasificacion.categoria} onChange={e => setClasificacion(v => ({ ...v, categoria: e.target.value, subTipo: '' }))}><option value="">Seleccionar</option>{datos.catalogos.categorias.map(x => <option key={x.codigo} value={x.codigo}>{x.descripcion}</option>)}</select></label><label>Subtipo<select required value={clasificacion.subTipo} onChange={e => setClasificacion(v => ({ ...v, subTipo: e.target.value }))}><option value="">Seleccionar</option>{datos.catalogos.subTipos.filter(x => x.tipo === clasificacion.tipo && x.categoria === clasificacion.categoria).map(x => <option key={`${x.tipo}-${x.categoria}-${x.codigo}`} value={x.codigo}>{x.descripcion}</option>)}</select></label><label>Área causante<select value={clasificacion.areaCausante ?? ''} onChange={e => setClasificacion(v => ({ ...v, areaCausante: e.target.value || null }))}><option value="">Sin determinar</option>{datos.catalogos.areas.map(x => <option key={x.codigo} value={x.codigo}>{x.descripcion}</option>)}</select></label><Nivel label="Prioridad" valor={clasificacion.prioridad} onChange={valor => setClasificacion(v => ({ ...v, prioridad: valor }))}/><Nivel label="Impacto" valor={clasificacion.impacto} onChange={valor => setClasificacion(v => ({ ...v, impacto: valor }))}/><Nivel label="Complejidad" valor={clasificacion.complejidad} onChange={valor => setClasificacion(v => ({ ...v, complejidad: valor }))}/></div><PieModal procesando={procesando} texto="Guardar clasificación" onCancelar={() => setModal(null)}/></form>}
      {modal === 'ASIGNAR' && datos && <form onSubmit={e => { e.preventDefault(); void ejecutar(() => asignarTicketTI(detalle.incidenciaNumero, responsableAccion), detalle.usuarioTI ? 'El ticket fue reasignado.' : 'El ticket fue asignado.') }}><label className="gestion-ti-campo">Responsable TI<select required value={responsableAccion} onChange={e => setResponsableAccion(e.target.value)}><option value="">Seleccionar operador</option>{datos.catalogos.operadores.map(x => <option key={x.codigo} value={x.codigo}>{x.descripcion}</option>)}</select><small>El responsable quedará registrado junto con el operador que realizó la asignación.</small></label><PieModal procesando={procesando} texto={detalle.usuarioTI ? 'Reasignar' : 'Asignar'} onCancelar={() => setModal(null)}/></form>}
      {modal === 'AVANCE' && <form onSubmit={e => { e.preventDefault(); void ejecutar(() => registrarAvanceTI(detalle.incidenciaNumero, textoAccion, visibleUsuario), 'El avance fue registrado.') }}><label className="gestion-ti-campo">Detalle del avance<textarea required minLength={5} maxLength={4000} value={textoAccion} onChange={e => setTextoAccion(e.target.value)} placeholder="Describe la validación o trabajo realizado..."/></label><label className="gestion-ti-check"><input type="checkbox" checked={visibleUsuario} onChange={e => setVisibleUsuario(e.target.checked)}/><span>Mostrar este avance también al usuario solicitante</span></label><PieModal procesando={procesando} texto="Registrar avance" onCancelar={() => setModal(null)}/></form>}
      {modal === 'INFORMACION' && <form onSubmit={e => { e.preventDefault(); void ejecutar(() => solicitarInformacionTI(detalle.incidenciaNumero, textoAccion), 'Se solicitó información adicional al usuario.') }}><label className="gestion-ti-campo">Información requerida<textarea required minLength={10} maxLength={1000} value={textoAccion} onChange={e => setTextoAccion(e.target.value)} placeholder="Indica exactamente qué información o evidencia necesitas."/></label><p className="gestion-ti-nota">El ticket pasará a <strong>En recopilación</strong>. La respuesta del usuario lo devolverá a diagnóstico.</p><PieModal procesando={procesando} texto="Solicitar información" onCancelar={() => setModal(null)}/></form>}
      {modal === 'RESOLVER' && <form onSubmit={e => { e.preventDefault(); void ejecutar(() => resolverTicketTI(detalle.incidenciaNumero, resolucion), 'La solución fue enviada al usuario para validación.') }}><div className="gestion-ti-form-vertical"><label>Causa raíz<textarea required minLength={5} maxLength={4000} value={resolucion.causaRaiz} onChange={e => setResolucion(v => ({ ...v, causaRaiz: e.target.value }))}/></label><label>Solución aplicada<textarea required minLength={5} maxLength={4000} value={resolucion.solucion} onChange={e => setResolucion(v => ({ ...v, solucion: e.target.value }))}/></label><label>Respuesta al usuario<textarea required minLength={5} maxLength={4000} value={resolucion.respuestaUsuario} onChange={e => setResolucion(v => ({ ...v, respuestaUsuario: e.target.value }))}/></label><label>Tipo de resolución<select value={resolucion.tipoResolucion} onChange={e => setResolucion(v => ({ ...v, tipoResolucion: e.target.value }))}><option value="CORRECCION">Corrección</option><option value="CONFIGURACION">Configuración</option><option value="GUIA">Guía</option><option value="REPROCESO">Reproceso</option></select></label></div><p className="gestion-ti-nota">El ticket pasará a <strong>Pendiente de validación</strong>. Solo el usuario confirmará su cierre.</p><PieModal procesando={procesando} texto="Enviar a validación" onCancelar={() => setModal(null)}/></form>}
      {modal === 'NO_PROCEDE' && <form onSubmit={e => { e.preventDefault(); void ejecutar(() => marcarNoProcedeTI(detalle.incidenciaNumero, textoAccion), 'El ticket fue marcado como No Procede.') }}><label className="gestion-ti-campo">Motivo<textarea required minLength={10} maxLength={1000} value={textoAccion} onChange={e => setTextoAccion(e.target.value)} placeholder="Explica de forma clara por qué el ticket no procede..."/></label><p className="gestion-ti-nota gestion-ti-nota--alerta">El motivo será visible para el usuario y quedará registrado en historial y auditoría.</p><PieModal procesando={procesando} texto="Confirmar No Procede" onCancelar={() => setModal(null)} peligro/></form>}
    </section></div>}
  </div>
}

function tituloModal(modal: Exclude<ModalAccion, null>, asignado: boolean) {
  if (modal === 'DETALLE') return 'Detalle completo del ticket'
  if (modal === 'CLASIFICAR') return 'Clasificar ticket'
  if (modal === 'ASIGNAR') return asignado ? 'Reasignar ticket' : 'Asignar ticket'
  if (modal === 'AVANCE') return 'Registrar avance'
  if (modal === 'INFORMACION') return 'Solicitar información'
  if (modal === 'RESOLVER') return 'Enviar solución a validación'
  return 'Marcar como No Procede'
}

function DatoRapido({ label, valor }: { label: string; valor: string }) { return <div><dt>{label}</dt><dd>{valor || '—'}</dd></div> }
function Nivel({ label, valor, onChange }: { label: string; valor: number | null; onChange: (valor: number | null) => void }) { return <label>{label}<select value={valor ?? ''} onChange={e => onChange(e.target.value ? Number(e.target.value) : null)}><option value="">Sin definir</option><option value="1">1 - Muy bajo</option><option value="2">2 - Bajo</option><option value="3">3 - Medio</option><option value="4">4 - Alto</option><option value="5">5 - Muy alto</option></select></label> }
function PieModal({ procesando, texto, onCancelar, peligro = false }: { procesando: boolean; texto: string; onCancelar: () => void; peligro?: boolean }) { return <footer className="gestion-ti-modal__pie"><button type="button" onClick={onCancelar} disabled={procesando}>Cancelar</button><button type="submit" className={peligro ? 'peligro' : 'principal'} disabled={procesando}>{procesando ? 'Procesando...' : texto}</button></footer> }

function DetalleCompleto({ detalle, onAccion, onAprobar }: { detalle: GestionTicketTIDetalle; onAccion: (tipo: Exclude<ModalAccion, null>) => void; onAprobar: (secuencia: number, aprobar: boolean) => void }) {
  return <div className="gestion-ti-detalle-completo">
    <section><h3>Solicitud original</h3><div className="gestion-ti-detalle-completo__cuadricula"><Dato label="Solicitante" valor={detalle.solicitante}/><Dato label="Área" valor={detalle.areaSolicitanteDescripcion}/><Dato label="Tipo" valor={detalle.tipoDescripcion}/><Dato label="Estado" valor={detalle.estadoDescripcion}/><Dato label="Línea" valor={detalle.lineaDescripcion}/><Dato label="Item" valor={detalle.itemDescripcion || 'Pendiente'}/><Dato label="Categoría" valor={detalle.categoriaDescripcion || 'Pendiente'}/><Dato label="Responsable" valor={detalle.responsable}/></div><Bloque titulo="Detalle" texto={detalle.detalle}/>{detalle.mensajeError && <Bloque titulo="Mensaje de error" texto={detalle.mensajeError} error/>}</section>
    {detalle.documentos.length > 0 && <section><h3>Documentos relacionados</h3><div className="gestion-ti-lista-simple">{detalle.documentos.map(x => <div key={x.secuencia}><Icono nombre="archivo" size={17}/><span><strong>{x.tipoDocumento} {x.numeroDocumento}</strong><small>{x.descripcion || x.companiaSocio}</small></span></div>)}</div></section>}
    {detalle.adjuntos.length > 0 && <section><h3>Evidencias adjuntas</h3><div className="gestion-ti-lista-simple">{detalle.adjuntos.map(x => <a key={x.secuencia} href={urlAdjuntoGestionTI(detalle.incidenciaNumero, x.secuencia)} target="_blank" rel="noreferrer"><Icono nombre="descargar" size={17}/><span><strong>{x.nombreOriginal}</strong><small>{Math.max(1, Math.round(x.tamanoBytes / 1024))} KB · {fechaHora(x.fechaRegistro)}</small></span></a>)}</div></section>}
    <section><h3>Historial</h3><div className="gestion-ti-historial">{detalle.historialEstados.map(x => <div key={x.secuencia}><span/><div><strong>{x.estadoDescripcion}</strong><small>{x.actor} · {fechaHora(x.fechaCambio)}</small>{x.observacion && <p>{x.observacion}</p>}</div></div>)}</div></section>
    {detalle.avances.length > 0 && <section><h3>Avances técnicos</h3><div className="gestion-ti-tarjetas-texto">{detalle.avances.map(x => <article key={x.secuencia}><header><strong>{x.responsable}</strong><small>{fechaHora(x.fechaAvance)}</small></header><p>{x.detalle}</p></article>)}</div></section>}
    {detalle.mensajes.length > 0 && <section><h3>Mensajes</h3><div className="gestion-ti-tarjetas-texto">{detalle.mensajes.map(x => <article key={x.secuencia} className={x.esInterno ? 'interno' : ''}><header><strong>{x.autor}{x.esInterno ? ' · Interno' : ''}</strong><small>{fechaHora(x.fechaMensaje)}</small></header><p>{x.contenido}</p></article>)}</div></section>}
    {detalle.aprobaciones.length > 0 && <section><h3>Aprobaciones</h3><div className="gestion-ti-aprobaciones">{detalle.aprobaciones.map(x => <article key={x.secuencia}><div><strong>{x.accionNombre}</strong><span>Riesgo: {x.nivelRiesgo}</span><p>{x.justificacion}</p></div><div><span className={`gestion-ti-badge gestion-ti-badge--${x.estado === 'P' ? 'amarillo' : x.estado === 'A' ? 'verde' : 'rojo'}`}>{x.estado === 'P' ? 'Pendiente' : x.estado === 'A' ? 'Aprobada' : x.estado === 'R' ? 'Rechazada' : 'Cancelada'}</span>{x.estado === 'P' && <div className="gestion-ti-aprobaciones__acciones"><button onClick={() => onAprobar(x.secuencia, false)}>Rechazar</button><button onClick={() => onAprobar(x.secuencia, true)}>Aprobar</button></div>}</div></article>)}</div></section>}
    {(detalle.causaRaiz || detalle.solucionTecnica) && <section><h3>Resolución</h3><Bloque titulo="Causa raíz" texto={detalle.causaRaiz || 'Pendiente'}/><Bloque titulo="Solución técnica" texto={detalle.solucionTecnica || 'Pendiente'}/></section>}
    {puedeOperar(detalle.estado) && <footer className="gestion-ti-detalle-completo__acciones"><button onClick={() => onAccion('CLASIFICAR')}>Clasificar</button><button onClick={() => onAccion('ASIGNAR')}>{detalle.usuarioTI ? 'Reasignar' : 'Asignar'}</button><button onClick={() => onAccion('AVANCE')}>Registrar avance</button><button onClick={() => onAccion('INFORMACION')}>Solicitar información</button><button className="principal" onClick={() => onAccion('RESOLVER')}>Resolver / validar</button><button className="peligro" onClick={() => onAccion('NO_PROCEDE')}>No procede</button></footer>}
  </div>
}

function Dato({ label, valor }: { label: string; valor: string }) { return <div><span>{label}</span><strong>{valor || '—'}</strong></div> }
function Bloque({ titulo, texto, error = false }: { titulo: string; texto: string; error?: boolean }) { return <div className={`gestion-ti-bloque-texto ${error ? 'gestion-ti-bloque-texto--error' : ''}`}><strong>{titulo}</strong><p>{texto}</p></div> }
