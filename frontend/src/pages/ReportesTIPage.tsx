/*
 * Archivo: ReportesTIPage.tsx
 * Objetivo: Implementar el módulo Reportes para el operador TI autenticado.
 * Responsabilidad: Presentar indicadores, filtros, evolución, distribución, tiempos, tickets prioritarios y exportación del período seleccionado.
 * Dependencias: AutenticacionContext, reportesTIService, NotificacionesCampana, InicioPage.css y ReportesTIPage.css.
 * Flujo: Ruta protegida /reportes -> ReportesTIPage -> API /api/reportes/ti -> Stored Procedure -> SQL Server.
 * Consideraciones: No incorpora programación ni envío de reportes porque esas funciones requieren infraestructura adicional; prioriza análisis operativo real, exportación simple y mantenimiento reducido.
 */

import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAutenticacion } from '../features/autenticacion/context/AutenticacionContext'
import {
  obtenerReportesTI,
  type ReportesTIFiltros,
  type ReportesTIRespuesta,
  type ReportesTIEvolucion,
  type ReportesTIEstado,
} from '../features/reportesTI/services/reportesTIService'
import NotificacionesCampana from '../components/NotificacionesCampana'
import './InicioPage.css'
import './ReportesTIPage.css'

type IconoNombre = 'inicio' | 'asistente' | 'gestion' | 'conocimiento' | 'reporte' | 'buscar' | 'campana' | 'salir' | 'descargar' | 'filtro' | 'carpeta' | 'reloj' | 'check' | 'estrella' | 'grafico' | 'pastel' | 'area' | 'resumen' | 'actualizar' | 'flecha' | 'alerta' | 'ticket'

function Icono({ nombre, size = 19 }: { nombre: IconoNombre; size?: number }) {
  const trazos: Record<IconoNombre, React.ReactNode> = {
    inicio: <><path d="M3 11.5 12 4l9 7.5"/><path d="M5.5 10.5V20h13v-9.5"/><path d="M9.5 20v-6h5v6"/></>,
    asistente: <><path d="m12 3 1.2 3.1L16 7.5l-2.8 1.4L12 12l-1.2-3.1L8 7.5l2.8-1.4L12 3Z"/><path d="m5 13 .8 2.2L8 16l-2.2.8L5 19l-.8-2.2L2 16l2.2-.8L5 13Z"/></>,
    gestion: <><rect x="4" y="4" width="16" height="16" rx="2"/><path d="M8 9h8M8 13h5M8 17h3"/><path d="m16 15 1.5 1.5L20 14"/></>,
    conocimiento: <path d="M4 5.5A3.5 3.5 0 0 1 7.5 2H11v18H7.5A3.5 3.5 0 0 0 4 23V5.5ZM20 5.5A3.5 3.5 0 0 0 16.5 2H13v18h3.5A3.5 3.5 0 0 1 20 23V5.5Z"/>,
    reporte: <><path d="M4 20V10M10 20V4M16 20v-7M22 20H2"/></>,
    buscar: <><circle cx="11" cy="11" r="6"/><path d="m16 16 4 4"/></>,
    campana: <><path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9"/><path d="M10 21h4"/></>,
    salir: <><path d="M10 5H5v14h5"/><path d="m14 8 4 4-4 4M18 12H9"/></>,
    descargar: <><path d="M12 3v12M8 11l4 4 4-4"/><path d="M4 20h16"/></>,
    filtro: <path d="M3 5h18l-7 8v5l-4 2v-7L3 5Z"/>,
    carpeta: <path d="M3 7h6l2 2h10v10H3V7Z"/>,
    reloj: <><circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/></>,
    check: <><circle cx="12" cy="12" r="9"/><path d="m8 12 2.5 2.5L16 9"/></>,
    estrella: <path d="m12 3 2.7 5.5 6.1.9-4.4 4.3 1 6.1-5.4-2.9-5.4 2.9 1-6.1-4.4-4.3 6.1-.9L12 3Z"/>,
    grafico: <><path d="M3 20h18M5 17l4-5 4 2 6-8"/><path d="M17 6h2v2"/></>,
    pastel: <><path d="M12 3v9h9A9 9 0 1 1 12 3Z"/><path d="M15 3.5A8 8 0 0 1 20.5 9H15V3.5Z"/></>,
    area: <><path d="M4 20V10M9 20V6M14 20v-8M19 20V4"/></>,
    resumen: <><path d="M6 3h9l3 3v15H6V3Z"/><path d="M9 10h6M9 14h6M9 18h4"/></>,
    actualizar: <><path d="M20 7v5h-5"/><path d="M19 12a7 7 0 1 1-2-5"/></>,
    flecha: <><path d="M5 12h14M15 8l4 4-4 4"/></>,
    alerta: <><circle cx="12" cy="12" r="9"/><path d="M12 7v6M12 17h.01"/></>,
    ticket: <><path d="M4 7h16v10H4z"/><path d="M8 7v3M16 7v3M8 14h8"/></>,
  }
  return <svg className="inicio-icono" width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">{trazos[nombre]}</svg>
}

function fechaIsoLocal(fecha: Date) {
  const anio = fecha.getFullYear()
  const mes = String(fecha.getMonth() + 1).padStart(2, '0')
  const dia = String(fecha.getDate()).padStart(2, '0')
  return `${anio}-${mes}-${dia}`
}

function filtrosIniciales(): ReportesTIFiltros {
  const fin = new Date()
  const inicio = new Date()
  inicio.setDate(fin.getDate() - 29)
  return { fechaInicio: fechaIsoLocal(inicio), fechaFin: fechaIsoLocal(fin), area: '', estado: '', prioridad: '', tipo: '', usuarioTI: '' }
}

function primerNombre(nombre: string) { return nombre.trim().split(/\s+/)[0] || nombre }
function numero(valor: number | null, decimales = 1) { return valor === null ? '—' : new Intl.NumberFormat('es-PE', { maximumFractionDigits: decimales, minimumFractionDigits: decimales }).format(valor) }
function fechaCorta(fecha: string) { return new Intl.DateTimeFormat('es-PE', { day: '2-digit', month: 'short' }).format(new Date(`${fecha.slice(0, 10)}T00:00:00`)) }
function textoPrioridad(valor: number | null) { return valor === null ? 'Sin asignar' : valor >= 4 ? 'Alta' : valor === 3 ? 'Media' : 'Baja' }
function clasePrioridad(valor: number | null) { return `reportes-ti-badge reportes-ti-badge--${valor === null ? 'neutro' : valor >= 4 ? 'rojo' : valor === 3 ? 'amarillo' : 'verde'}` }
function claseEstado(estado: string) { return `reportes-ti-badge reportes-ti-badge--${estado === 'RS' ? 'verde' : estado === 'RA' ? 'rojo' : ['RC', 'PA', 'PV'].includes(estado) ? 'amarillo' : 'azul'}` }

function variacion(actual: number | null, anterior: number | null) {
  if (actual === null || anterior === null || anterior === 0) return null
  return ((actual - anterior) / Math.abs(anterior)) * 100
}

function Variacion({ actual, anterior, menorEsMejor = false, puntos = false }: { actual: number | null; anterior: number | null; menorEsMejor?: boolean; puntos?: boolean }) {
  if (actual === null || anterior === null) return <small className="reportes-ti-comparacion reportes-ti-comparacion--neutra">Sin comparación disponible</small>
  const cambio = puntos ? actual - anterior : variacion(actual, anterior)
  if (cambio === null) return <small className="reportes-ti-comparacion reportes-ti-comparacion--neutra">Sin base anterior</small>
  const mejora = menorEsMejor ? cambio <= 0 : cambio >= 0
  return <small className={`reportes-ti-comparacion ${mejora ? 'reportes-ti-comparacion--mejora' : 'reportes-ti-comparacion--alerta'}`}>{cambio >= 0 ? '↑' : '↓'} {puntos ? `${Math.abs(cambio).toFixed(1)} pts` : `${Math.abs(cambio).toFixed(1)}%`} vs. período anterior</small>
}

export default function ReportesTIPage() {
  const navigate = useNavigate()
  const { usuario, cerrarSesion } = useAutenticacion()
  const buscadorRef = useRef<HTMLInputElement>(null)
  const filtrosRef = useRef<HTMLElement>(null)
  const [filtros, setFiltros] = useState<ReportesTIFiltros>(filtrosIniciales)
  const [datos, setDatos] = useState<ReportesTIRespuesta | null>(null)
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState('')
  const [busqueda, setBusqueda] = useState('')

  async function cargar(nuevosFiltros = filtros) {
    setCargando(true); setError('')
    try { setDatos(await obtenerReportesTI(nuevosFiltros)) }
    catch (e) { setError(e instanceof Error ? e.message : 'No fue posible generar el reporte.') }
    finally { setCargando(false) }
  }

  useEffect(() => { void cargar(filtros) }, [])
  useEffect(() => {
    const manejar = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') { e.preventDefault(); buscadorRef.current?.focus(); buscadorRef.current?.select() }
    }
    window.addEventListener('keydown', manejar)
    return () => window.removeEventListener('keydown', manejar)
  }, [])

  const ticketsPrioritarios = useMemo(() => {
    if (!datos) return []
    const texto = busqueda.trim().toLowerCase()
    return datos.ticketsPrioridadAlta.filter(x => !texto || `${x.incidenciaNumero} ${x.titulo} ${x.areaDescripcion} ${x.estadoDescripcion} ${x.responsable}`.toLowerCase().includes(texto))
  }, [busqueda, datos])

  if (!usuario) return null
  const nombre = primerNombre(usuario.nombreCompleto)

  function limpiarFiltros() {
    const nuevos = filtrosIniciales()
    setFiltros(nuevos)
    setBusqueda('')
    void cargar(nuevos)
  }

  function exportarCsv() {
    if (!datos?.detalleExportacion.length) return
    const encabezado = ['Ticket', 'Solicitante', 'Título', 'Área', 'Tipo', 'Estado', 'Prioridad', 'Responsable', 'Fecha registro', 'Fecha cierre', 'Calificación']
    const filas = datos.detalleExportacion.map(x => [x.incidenciaNumero, x.solicitante, x.titulo, x.areaDescripcion, x.tipoDescripcion, x.estadoDescripcion, textoPrioridad(x.prioridad), x.responsable, x.fechaRegistro, x.fechaCierre ?? '', x.calificacion ?? ''])
    const contenido = [encabezado, ...filas].map(fila => fila.map(valor => `"${String(valor).replaceAll('"', '""')}"`).join(',')).join('\n')
    const url = URL.createObjectURL(new Blob([`\uFEFF${contenido}`], { type: 'text/csv;charset=utf-8' }))
    const enlace = document.createElement('a')
    enlace.href = url
    enlace.download = `reporte-ti-${filtros.fechaInicio}-${filtros.fechaFin}.csv`
    enlace.click()
    URL.revokeObjectURL(url)
  }

  return <div className="inicio-shell reportes-ti-page">
    <aside className="inicio-sidebar" aria-label="Navegación principal">
      <div className="inicio-marca"><span className="inicio-marca__nombre">CALIMOD</span><span className="inicio-marca__sistema">Sistema inteligente<br />de incidencias TI</span></div>
      <nav className="inicio-menu">
        <button className="inicio-menu__item" type="button" onClick={() => navigate('/inicio')}><Icono nombre="inicio"/> <span>Inicio</span></button>
        <button className="inicio-menu__item" type="button" disabled title="Se implementará en el módulo Asistente TI"><Icono nombre="asistente"/> <span>Asistente TI</span></button>
        <button className="inicio-menu__item" type="button" onClick={() => navigate('/gestion-tickets')}><Icono nombre="gestion"/> <span>Gestión de Tickets</span></button>
        <button className="inicio-menu__item" type="button" onClick={() => navigate('/base-conocimiento')}><Icono nombre="conocimiento"/> <span>Base de Conocimiento</span></button>
        <button className="inicio-menu__item inicio-menu__item--activo" type="button" aria-current="page"><Icono nombre="reporte"/> <span>Reportes</span></button>
        {['SUP', 'ADM'].includes(usuario.perfil) && <button className="inicio-menu__item" type="button" onClick={() => navigate('/configuracion-ti')}><span aria-hidden="true">⚙</span><span>Configuración TI</span></button>}
      </nav>
      <div className="inicio-sidebar__mensaje"><span>La tecnología también impulsa grandes historias.</span><strong>CALIMOD</strong></div>
      <div className="inicio-sidebar__pie"><span className="inicio-sidebar__ayuda-icono">?</span><span>¿Necesitas ayuda?</span><small>Disponible desde Asistente TI</small></div>
    </aside>

    <section className="inicio-principal">
      <header className="inicio-topbar">
        <label className="inicio-buscador"><Icono nombre="buscar"/><input ref={buscadorRef} value={busqueda} onChange={e => setBusqueda(e.target.value)} placeholder="Buscar ticket crítico del período..."/><span>Ctrl + K</span></label>
        <div className="inicio-topbar__usuario">
          <NotificacionesCampana />
          <div className="inicio-avatar">{nombre.slice(0, 1).toUpperCase()}</div><div className="inicio-identidad"><strong>{nombre}</strong><span>Operador TI</span></div>
          <button className="inicio-salir" type="button" onClick={() => void cerrarSesion().then(() => navigate('/login', { replace: true }))} title="Cerrar sesión"><Icono nombre="salir"/></button>
        </div>
      </header>

      <main className="inicio-contenido reportes-ti-contenido">
        <section className="inicio-hero reportes-ti-hero">
          <div className="inicio-hero__contenido"><h1>Reportes</h1><p className="inicio-hero__resumen">Analiza indicadores, estados, tiempos y comportamiento de incidencias.</p><p className="inicio-hero__detalle">Convierte los datos operativos en decisiones claras sin salir del flujo de atención TI.</p></div>
          <div className="inicio-hero__acciones reportes-ti-hero__acciones"><button type="button" onClick={exportarCsv} disabled={!datos?.detalleExportacion.length}><Icono nombre="descargar"/> Exportar CSV</button><button type="button" className="inicio-hero__secundario" onClick={() => filtrosRef.current?.scrollIntoView({ behavior: 'smooth' })}><Icono nombre="filtro"/> Filtros</button><button type="button" className="inicio-hero__secundario" onClick={() => navigate('/gestion-tickets')}><Icono nombre="gestion"/> Gestión de Tickets</button></div>
          <div className="inicio-hero__firma"><span>Personas</span><span>que avanzan</span><strong>CALIMOD</strong></div>
        </section>

        {error && <div className="reportes-ti-alerta" role="alert"><Icono nombre="alerta"/><span>{error}</span><button type="button" onClick={() => void cargar()}><Icono nombre="actualizar" size={16}/> Reintentar</button></div>}

        <section className="reportes-ti-metricas" aria-label="Indicadores del período">
          <article><span className="reportes-ti-metrica__icono reportes-ti-metrica__icono--turquesa"><Icono nombre="carpeta"/></span><div><small>Tickets del período</small><strong>{cargando ? '—' : datos?.resumen.total ?? 0}</strong><Variacion actual={datos?.resumen.total ?? null} anterior={datos?.resumen.totalAnterior ?? null}/></div></article>
          <article><span className="reportes-ti-metrica__icono reportes-ti-metrica__icono--azul"><Icono nombre="reloj"/></span><div><small>Tiempo promedio de resolución</small><strong>{cargando ? '—' : `${numero(datos?.resumen.tiempoPromedioHoras ?? null)} h`}</strong><Variacion actual={datos?.resumen.tiempoPromedioHoras ?? null} anterior={datos?.resumen.tiempoPromedioHorasAnterior ?? null} menorEsMejor/></div></article>
          <article><span className="reportes-ti-metrica__icono reportes-ti-metrica__icono--verde"><Icono nombre="check"/></span><div><small>Resueltos</small><strong>{cargando ? '—' : datos?.resumen.resueltos ?? 0}</strong><Variacion actual={datos?.resumen.resueltos ?? null} anterior={datos?.resumen.resueltosAnterior ?? null}/></div></article>
          <article><span className="reportes-ti-metrica__icono reportes-ti-metrica__icono--amarillo"><Icono nombre="estrella"/></span><div><small>Satisfacción</small><strong>{cargando ? '—' : datos?.resumen.satisfaccion === null || datos?.resumen.satisfaccion === undefined ? 'Sin datos' : `${numero(datos.resumen.satisfaccion)} / 5`}</strong><Variacion actual={datos?.resumen.satisfaccion ?? null} anterior={datos?.resumen.satisfaccionAnterior ?? null} puntos/></div></article>
        </section>

        <section className="reportes-ti-panel reportes-ti-filtros-panel" ref={filtrosRef}>
          <div className="reportes-ti-panel__cabecera"><div><span className="reportes-ti-panel__icono"><Icono nombre="filtro"/></span><div><h2>Filtros del reporte</h2><p>Personaliza el rango y los criterios aplicados a todos los indicadores.</p></div></div><button type="button" className="reportes-ti-limpiar" onClick={limpiarFiltros}><Icono nombre="actualizar" size={15}/> Limpiar filtros</button></div>
          <form className="reportes-ti-filtros" onSubmit={e => { e.preventDefault(); void cargar() }}>
            <label>Desde<input type="date" required value={filtros.fechaInicio} max={filtros.fechaFin} onChange={e => setFiltros(v => ({ ...v, fechaInicio: e.target.value }))}/></label>
            <label>Hasta<input type="date" required value={filtros.fechaFin} min={filtros.fechaInicio} onChange={e => setFiltros(v => ({ ...v, fechaFin: e.target.value }))}/></label>
            <label>Área<select value={filtros.area} onChange={e => setFiltros(v => ({ ...v, area: e.target.value }))}><option value="">Todas las áreas</option>{datos?.catalogos.areas.map(x => <option key={x.codigo} value={x.codigo}>{x.descripcion}</option>)}</select></label>
            <label>Estado<select value={filtros.estado} onChange={e => setFiltros(v => ({ ...v, estado: e.target.value }))}><option value="">Todos los estados</option>{datos?.catalogos.estados.map(x => <option key={x.codigo} value={x.codigo}>{x.descripcion}</option>)}</select></label>
            <label>Prioridad<select value={filtros.prioridad} onChange={e => setFiltros(v => ({ ...v, prioridad: e.target.value }))}><option value="">Todas</option><option value="ALTA">Alta</option><option value="MEDIA">Media</option><option value="BAJA">Baja</option><option value="SIN">Sin asignar</option></select></label>
            <label>Tipo<select value={filtros.tipo} onChange={e => setFiltros(v => ({ ...v, tipo: e.target.value }))}><option value="">Todos los tipos</option>{datos?.catalogos.tipos.map(x => <option key={x.codigo} value={x.codigo}>{x.descripcion}</option>)}</select></label>
            <label>Responsable<select value={filtros.usuarioTI} onChange={e => setFiltros(v => ({ ...v, usuarioTI: e.target.value }))}><option value="">Todos</option><option value="SIN_ASIGNAR">Sin asignar</option>{datos?.catalogos.operadores.map(x => <option key={x.codigo} value={x.codigo}>{x.descripcion}</option>)}</select></label>
            <button type="submit" disabled={cargando}>{cargando ? 'Actualizando...' : 'Aplicar filtros'} <Icono nombre="flecha" size={15}/></button>
          </form>
        </section>

        <section className="reportes-ti-grid-superior">
          <article className="reportes-ti-panel reportes-ti-evolucion"><Cabecera icono="grafico" titulo="Incidencias por período" subtitulo="Evolución de tickets registrados en el rango seleccionado."/>{cargando ? <Vacio texto="Cargando evolución..."/> : <GraficoEvolucion datos={datos?.evolucion ?? []} inicio={filtros.fechaInicio} fin={filtros.fechaFin}/>}</article>
          <article className="reportes-ti-panel reportes-ti-estados"><Cabecera icono="pastel" titulo="Estados de tickets" subtitulo="Distribución actual de los tickets filtrados."/>{cargando ? <Vacio texto="Cargando estados..."/> : <GraficoEstados datos={datos?.estados ?? []}/>}</article>
          <article className="reportes-ti-panel reportes-ti-areas"><Cabecera icono="area" titulo="Incidencias por área" subtitulo="Áreas solicitantes con mayor volumen."/>{cargando ? <Vacio texto="Cargando áreas..."/> : <BarrasAreas datos={datos?.areas ?? []}/>}</article>
          <article className="reportes-ti-panel reportes-ti-resumen"><Cabecera icono="resumen" titulo="Resumen ejecutivo" subtitulo="Lectura rápida del período seleccionado."/><ResumenEjecutivo datos={datos}/></article>
        </section>

        <section className="reportes-ti-grid-inferior">
          <article className="reportes-ti-panel reportes-ti-tiempos"><Cabecera icono="reloj" titulo="Tiempo de resolución por prioridad" subtitulo="Duración calculada desde la creación hasta el cierre."/><div className="reportes-ti-tabla-wrap"><table><thead><tr><th>Prioridad</th><th>Tickets</th><th>Promedio</th><th>Más rápido</th><th>Más largo</th></tr></thead><tbody>{!datos?.tiemposPorPrioridad.length ? <tr><td colSpan={5} className="reportes-ti-tabla-vacia">No hay tickets cerrados suficientes para calcular tiempos.</td></tr> : datos.tiemposPorPrioridad.map(x => <tr key={x.prioridad}><td><span className={`reportes-ti-prioridad reportes-ti-prioridad--${x.prioridad.toLowerCase().replace(' ', '-')}`}>{x.prioridad}</span></td><td>{x.tickets}</td><td>{formatearHoras(x.tiempoPromedioHoras)}</td><td>{formatearHoras(x.tiempoMasRapidoHoras)}</td><td>{formatearHoras(x.tiempoMasLargoHoras)}</td></tr>)}</tbody></table></div></article>
          <article className="reportes-ti-panel reportes-ti-prioritarios"><div className="reportes-ti-panel__cabecera"><div><span className="reportes-ti-panel__icono reportes-ti-panel__icono--rojo"><Icono nombre="alerta"/></span><div><h2>Tickets de prioridad alta recientes</h2><p>Casos que requieren seguimiento dentro del período.</p></div></div><button className="reportes-ti-enlace" type="button" onClick={() => navigate('/gestion-tickets')}>Ver todos <Icono nombre="flecha" size={14}/></button></div><div className="reportes-ti-tabla-wrap"><table><thead><tr><th>Ticket</th><th>Título</th><th>Área</th><th>Estado</th><th>Tiempo</th></tr></thead><tbody>{ticketsPrioritarios.length === 0 ? <tr><td colSpan={5} className="reportes-ti-tabla-vacia">No hay tickets de prioridad alta con el criterio actual.</td></tr> : ticketsPrioritarios.map(x => <tr key={x.incidenciaNumero}><td><strong>{x.incidenciaNumero}</strong></td><td>{x.titulo}</td><td>{x.areaDescripcion}</td><td><span className={claseEstado(x.estado)}>{x.estadoDescripcion}</span></td><td>{formatearHoras(x.tiempoAbiertoHoras)}</td></tr>)}</tbody></table></div></article>
          <aside className="reportes-ti-panel reportes-ti-accesos"><Cabecera icono="ticket" titulo="Accesos rápidos" subtitulo="Continúa desde los datos del reporte."/><button type="button" onClick={() => navigate('/gestion-tickets')}><Icono nombre="gestion"/><span><strong>Ver Gestión de Tickets</strong><small>Revisa y atiende incidencias</small></span><Icono nombre="flecha" size={15}/></button><button type="button" onClick={exportarCsv} disabled={!datos?.detalleExportacion.length}><Icono nombre="descargar"/><span><strong>Exportar detalle CSV</strong><small>{datos?.detalleExportacion.length ?? 0} registros filtrados</small></span><Icono nombre="flecha" size={15}/></button><button type="button" onClick={() => void cargar()}><Icono nombre="actualizar"/><span><strong>Actualizar reporte</strong><small>Consulta nuevamente SQL Server</small></span><Icono nombre="flecha" size={15}/></button></aside>
        </section>
      </main>
    </section>
  </div>
}

function Cabecera({ icono, titulo, subtitulo }: { icono: IconoNombre; titulo: string; subtitulo: string }) { return <div className="reportes-ti-panel__cabecera"><div><span className="reportes-ti-panel__icono"><Icono nombre={icono}/></span><div><h2>{titulo}</h2><p>{subtitulo}</p></div></div></div> }
function Vacio({ texto }: { texto: string }) { return <div className="reportes-ti-vacio">{texto}</div> }

function GraficoEvolucion({ datos, inicio, fin }: { datos: ReportesTIEvolucion[]; inicio: string; fin: string }) {
  const serie = useMemo(() => completarSerie(datos, inicio, fin), [datos, inicio, fin])
  if (!serie.length) return <Vacio texto="No hay información para el período seleccionado."/>
  const max = Math.max(...serie.map(x => x.cantidad), 1)
  const puntos = serie.map((x, i) => `${serie.length === 1 ? 50 : (i / (serie.length - 1)) * 100},${92 - (x.cantidad / max) * 78}`).join(' ')
  const area = `0,92 ${puntos} 100,92`
  const indices = [0, Math.floor((serie.length - 1) / 2), serie.length - 1].filter((v, i, a) => a.indexOf(v) === i)
  return <div className="reportes-ti-linea"><svg viewBox="0 0 100 100" preserveAspectRatio="none" role="img" aria-label="Evolución de incidencias"><defs><linearGradient id="reporteArea" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stopColor="currentColor" stopOpacity=".28"/><stop offset="100%" stopColor="currentColor" stopOpacity="0"/></linearGradient></defs><g className="reportes-ti-linea__rejilla">{[20,40,60,80].map(y => <line key={y} x1="0" y1={y} x2="100" y2={y}/>)}</g><polygon points={area} fill="url(#reporteArea)"/><polyline points={puntos}/>{serie.map((x, i) => <circle key={x.fecha} cx={serie.length === 1 ? 50 : (i / (serie.length - 1)) * 100} cy={92 - (x.cantidad / max) * 78} r="1.4"><title>{`${fechaCorta(x.fecha)}: ${x.cantidad} tickets`}</title></circle>)}</svg><div className="reportes-ti-linea__eje">{indices.map(i => <span key={i}>{fechaCorta(serie[i].fecha)}</span>)}</div></div>
}

function completarSerie(datos: ReportesTIEvolucion[], inicio: string, fin: string) {
  if (!inicio || !fin) return datos
  const mapa = new Map(datos.map(x => [x.fecha.slice(0, 10), x.cantidad]))
  const resultado: ReportesTIEvolucion[] = []
  const actual = new Date(`${inicio}T00:00:00`)
  const ultimo = new Date(`${fin}T00:00:00`)
  while (actual <= ultimo) {
    const fecha = fechaIsoLocal(actual)
    resultado.push({ fecha, cantidad: mapa.get(fecha) ?? 0 })
    actual.setDate(actual.getDate() + 1)
  }
  return resultado
}

function GraficoEstados({ datos }: { datos: ReportesTIEstado[] }) {
  const total = datos.reduce((suma, x) => suma + x.cantidad, 0)
  if (!total) return <Vacio texto="No hay tickets para distribuir por estado."/>
  const paleta = ['#15977f', '#1f82e6', '#f0b429', '#ef5350', '#7c5ce7', '#5aa9a2', '#607d8b']
  let acumulado = 0
  const segmentos = datos.map((x, i) => {
    const inicio = (acumulado / total) * 100
    acumulado += x.cantidad
    const fin = (acumulado / total) * 100
    return `${paleta[i % paleta.length]} ${inicio}% ${fin}%`
  }).join(', ')
  return <div className="reportes-ti-donut-wrap"><div className="reportes-ti-donut" style={{ background: `conic-gradient(${segmentos})` }}><div><strong>{total}</strong><span>tickets</span></div></div><div className="reportes-ti-leyenda">{datos.slice(0, 6).map((x, i) => <div key={x.estado}><span className="reportes-ti-leyenda__punto" style={{ background: paleta[i % paleta.length] }}/><span>{x.estadoDescripcion}</span><strong>{x.cantidad}</strong><small>{Math.round((x.cantidad / total) * 100)}%</small></div>)}</div></div>
}

function BarrasAreas({ datos }: { datos: { area: string; areaDescripcion: string; cantidad: number }[] }) {
  if (!datos.length) return <Vacio texto="No hay información por área."/>
  const max = Math.max(...datos.map(x => x.cantidad), 1)
  return <div className="reportes-ti-barras">{datos.slice(0, 7).map(x => <div key={x.area}><span title={x.areaDescripcion}>{x.areaDescripcion}</span><div><i style={{ width: `${Math.max(4, (x.cantidad / max) * 100)}%` }}/></div><strong>{x.cantidad}</strong></div>)}</div>
}

function ResumenEjecutivo({ datos }: { datos: ReportesTIRespuesta | null }) {
  const r = datos?.resumen
  const tasaResolucion = r && r.total > 0 ? (r.resueltos / r.total) * 100 : null
  return <div className="reportes-ti-resumen-lista"><div><span className="reportes-ti-resumen-lista__icono"><Icono nombre="grafico" size={17}/></span><p><strong>{r?.total ?? 0}</strong><small>tickets registrados</small></p></div><div><span className="reportes-ti-resumen-lista__icono"><Icono nombre="check" size={17}/></span><p><strong>{r?.resueltos ?? 0}</strong><small>tickets resueltos · {tasaResolucion === null ? '—' : `${tasaResolucion.toFixed(0)}%`}</small></p></div><div><span className="reportes-ti-resumen-lista__icono"><Icono nombre="reloj" size={17}/></span><p><strong>{numero(r?.horasEfectivas ?? null)} h</strong><small>esfuerzo efectivo · {r?.ticketsConEsfuerzo ?? 0} tickets</small></p></div><div><span className="reportes-ti-resumen-lista__icono"><Icono nombre="reloj" size={17}/></span><p><strong>{numero(r?.cumplimientoSla ?? null)}%</strong><small>cumplimiento de SLA</small></p></div><div><span className="reportes-ti-resumen-lista__icono"><Icono nombre="alerta" size={17}/></span><p><strong>{r?.reabiertos ?? 0}</strong><small>tickets reabiertos</small></p></div><div><span className="reportes-ti-resumen-lista__icono"><Icono nombre="estrella" size={17}/></span><p><strong>{r?.satisfaccion === null || r?.satisfaccion === undefined ? '—' : `${numero(r.satisfaccion)} / 5`}</strong><small>satisfacción de usuarios</small></p></div></div>
}

function formatearHoras(valor: number | null) {
  if (valor === null) return '—'
  if (valor >= 48) return `${(valor / 24).toFixed(1)} días`
  if (valor < 1) return `${Math.round(valor * 60)} min`
  return `${valor.toFixed(1)} h`
}
