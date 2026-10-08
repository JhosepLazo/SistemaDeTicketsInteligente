/*
 * Archivo: ReportesTIPage.tsx
 * Objetivo: Implementar el módulo Reportes para el operador TI autenticado.
 * Responsabilidad: Presentar indicadores, filtros, evolución, distribución, tiempos, tickets prioritarios y exportación del período seleccionado,
 *   y los indicadores del agente frente a TI (pestaña Agente).
 * Dependencias: AutenticacionContext, reportesTIApi, MarcoPortal, Icono, VistaAgente y ReportesTIPage.css.
 * Flujo: Ruta protegida /reportes -> ReportesTIPage -> API /api/reportes/ti -> Stored Procedure -> SQL Server.
 * Consideraciones: No incorpora programación ni envío de reportes porque esas funciones requieren infraestructura adicional; prioriza análisis operativo real, exportación simple y mantenimiento reducido.
 */

import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, type NavigateFunction } from 'react-router-dom'
import { useAutenticacion } from '../autenticacion/AutenticacionContext'
import {
  obtenerReportesTI,
  type ReportesTIFiltros,
  type ReportesTIRespuesta,
  type ReportesTIEvolucion,
  type ReportesTIEstado,
} from '../../services/reportesTIApi'
import MarcoPortal from '../../components/MarcoPortal'
import IconoBase, { type PropsIcono, type NombreIcono } from '../../components/Icono'
import VistaAgente from './VistaAgente'
import './ReportesTIPage.css'

// Esta pantalla dibuja sus íconos a 19 px.
const Icono = (props: PropsIcono) => <IconoBase size={19} {...props} />

type VistaReporte = 'resumen' | 'avance' | 'equipo' | 'areas' | 'listado' | 'agente'

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

function numero(valor: number | null, decimales = 1) {
  return valor === null
    ? '—'
    : new Intl.NumberFormat('es-PE', { maximumFractionDigits: decimales, minimumFractionDigits: decimales }).format(valor)
}
function fechaCorta(fecha: string) {
  return new Intl.DateTimeFormat('es-PE', { day: '2-digit', month: 'short' }).format(new Date(`${fecha.slice(0, 10)}T00:00:00`))
}
function textoPrioridad(valor: number | null) {
  return valor === null ? 'Sin asignar' : valor >= 4 ? 'Alta' : valor === 3 ? 'Media' : 'Baja'
}
function clasePrioridad(valor: number | null) {
  return `reportes-ti-badge reportes-ti-badge--${valor === null ? 'neutro' : valor >= 4 ? 'rojo' : valor === 3 ? 'amarillo' : 'verde'}`
}
function claseEstado(estado: string) {
  return `reportes-ti-badge reportes-ti-badge--${estado === 'RS' ? 'verde' : estado === 'RA' ? 'rojo' : ['RC', 'PA', 'PV'].includes(estado) ? 'amarillo' : 'azul'}`
}

function variacion(actual: number | null, anterior: number | null) {
  if (actual === null || anterior === null || anterior === 0) return null
  return ((actual - anterior) / Math.abs(anterior)) * 100
}

function Variacion({
  actual,
  anterior,
  menorEsMejor = false,
  puntos = false,
}: {
  actual: number | null
  anterior: number | null
  menorEsMejor?: boolean
  puntos?: boolean
}) {
  if (actual === null || anterior === null)
    return <small className="reportes-ti-comparacion reportes-ti-comparacion--neutra">Sin comparación disponible</small>
  const cambio = puntos ? actual - anterior : variacion(actual, anterior)
  if (cambio === null) return <small className="reportes-ti-comparacion reportes-ti-comparacion--neutra">Sin base anterior</small>
  const mejora = menorEsMejor ? cambio <= 0 : cambio >= 0
  return (
    <small className={`reportes-ti-comparacion ${mejora ? 'reportes-ti-comparacion--mejora' : 'reportes-ti-comparacion--alerta'}`}>
      {cambio >= 0 ? '↑' : '↓'} {puntos ? `${Math.abs(cambio).toFixed(1)} pts` : `${Math.abs(cambio).toFixed(1)}%`} vs. período anterior
    </small>
  )
}

export default function ReportesTIPage() {
  const navigate = useNavigate()
  const { usuario } = useAutenticacion()
  const buscadorRef = useRef<HTMLInputElement>(null)
  const filtrosRef = useRef<HTMLElement>(null)
  const solicitudReporteRef = useRef(0)
  const [filtros, setFiltros] = useState<ReportesTIFiltros>(filtrosIniciales)
  const [filtrosAplicados, setFiltrosAplicados] = useState<ReportesTIFiltros>(filtrosIniciales)
  const [datos, setDatos] = useState<ReportesTIRespuesta | null>(null)
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState('')
  const [busqueda, setBusqueda] = useState('')
  const [vista, setVista] = useState<VistaReporte>('resumen')

  async function cargar(nuevosFiltros = filtros) {
    const solicitud = ++solicitudReporteRef.current
    setCargando(true)
    setError('')
    try {
      const respuesta = await obtenerReportesTI(nuevosFiltros)
      if (solicitud !== solicitudReporteRef.current) return
      setDatos(respuesta)
      setFiltrosAplicados({ ...nuevosFiltros })
    } catch (e) {
      if (solicitud === solicitudReporteRef.current) setError(e instanceof Error ? e.message : 'No fue posible generar el reporte.')
    } finally {
      if (solicitud === solicitudReporteRef.current) setCargando(false)
    }
  }

  useEffect(() => {
    void cargar(filtros)
  }, [])
  useEffect(() => {
    const manejar = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault()
        buscadorRef.current?.focus()
        buscadorRef.current?.select()
      }
    }
    window.addEventListener('keydown', manejar)
    return () => window.removeEventListener('keydown', manejar)
  }, [])

  const ticketsPrioritarios = useMemo(() => {
    if (!datos) return []
    const texto = busqueda.trim().toLowerCase()
    return datos.ticketsPrioridadAlta.filter(
      x =>
        !texto ||
        `${x.incidenciaNumero} ${x.titulo} ${x.areaDescripcion} ${x.estadoDescripcion} ${x.responsable}`.toLowerCase().includes(texto),
    )
  }, [busqueda, datos])

  if (!usuario) return null

  function limpiarFiltros() {
    const nuevos = filtrosIniciales()
    setFiltros(nuevos)
    setBusqueda('')
    void cargar(nuevos)
  }

  function exportarCsv() {
    if (!datos?.detalleExportacion.length) return
    const encabezado = [
      'Ticket',
      'Solicitante',
      'Título',
      'Área',
      'Tipo',
      'Estado',
      'Prioridad',
      'Responsable',
      'Fecha registro',
      'Fecha cierre',
      'Calificación',
    ]
    const filas = datos.detalleExportacion.map(x => [
      x.incidenciaNumero,
      x.solicitante,
      x.titulo,
      x.areaDescripcion,
      x.tipoDescripcion,
      x.estadoDescripcion,
      textoPrioridad(x.prioridad),
      x.responsable,
      x.fechaRegistro,
      x.fechaCierre ?? '',
      x.calificacion ?? '',
    ])
    const contenido = [encabezado, ...filas].map(fila => fila.map(valor => `"${String(valor).replaceAll('"', '""')}"`).join(',')).join('\n')
    const url = URL.createObjectURL(new Blob([`\uFEFF${contenido}`], { type: 'text/csv;charset=utf-8' }))
    const enlace = document.createElement('a')
    enlace.href = url
    enlace.download = `reporte-ti-${filtrosAplicados.fechaInicio}-${filtrosAplicados.fechaFin}.csv`
    enlace.click()
    URL.revokeObjectURL(url)
  }

  return (
    <MarcoPortal
      menu="ti"
      activa="reportes"
      clase="reportes-ti-page"
      barra={
        <label className="inicio-buscador">
          <Icono nombre="buscar" />
          <input
            ref={buscadorRef}
            value={busqueda}
            onChange={e => setBusqueda(e.target.value)}
            placeholder="Buscar tickets, áreas o responsables..."
          />
          <span>Ctrl + K</span>
        </label>
      }
    >
      <main className="inicio-contenido reportes-ti-contenido">
        <section className="inicio-hero reportes-ti-hero">
          <div className="inicio-hero__contenido">
            <h1>Reportes</h1>
            <p className="inicio-hero__resumen">Analiza indicadores, estados, tiempos y comportamiento de incidencias.</p>
            <p className="inicio-hero__detalle">Convierte los datos operativos en decisiones claras sin salir del flujo de atención TI.</p>
          </div>
          <div className="inicio-hero__acciones reportes-ti-hero__acciones">
            <button type="button" onClick={exportarCsv} disabled={!datos?.detalleExportacion.length}>
              <Icono nombre="descargar" /> Exportar CSV
            </button>
            <button
              type="button"
              className="inicio-hero__secundario"
              onClick={() => filtrosRef.current?.scrollIntoView({ behavior: 'smooth' })}
            >
              <Icono nombre="filtro" /> Filtros
            </button>
            <button type="button" className="inicio-hero__secundario" onClick={() => navigate('/gestion-tickets')}>
              <Icono nombre="gestion" /> Gestión de Tickets
            </button>
          </div>
          <div className="inicio-hero__firma">
            <span>Personas</span>
            <span>que avanzan</span>
            <strong>CALIMOD</strong>
          </div>
        </section>

        {error && (
          <div className="reportes-ti-alerta" role="alert">
            <Icono nombre="alerta" />
            <span>{error}</span>
            <button type="button" onClick={() => void cargar()}>
              <Icono nombre="actualizar" size={16} /> Reintentar
            </button>
          </div>
        )}

        <nav className="reportes-ti-vistas" aria-label="Vistas del módulo de reportes">
          <button type="button" className={vista === 'resumen' ? 'activo' : ''} onClick={() => setVista('resumen')}>
            <Icono nombre="resumen" />
            Resumen
          </button>
          <button type="button" className={vista === 'avance' ? 'activo' : ''} onClick={() => setVista('avance')}>
            <Icono nombre="grafico" />
            Avance de atención
          </button>
          <button type="button" className={vista === 'equipo' ? 'activo' : ''} onClick={() => setVista('equipo')}>
            <Icono nombre="gestion" />
            Equipo TI
          </button>
          <button type="button" className={vista === 'areas' ? 'activo' : ''} onClick={() => setVista('areas')}>
            <Icono nombre="area" />
            Áreas
          </button>
          <button type="button" className={vista === 'listado' ? 'activo' : ''} onClick={() => setVista('listado')}>
            <Icono nombre="ticket" />
            Listado de tickets
          </button>
          <button type="button" className={vista === 'agente' ? 'activo' : ''} onClick={() => setVista('agente')}>
            <Icono nombre="agente" />
            Agente
          </button>
        </nav>

        <section className="reportes-ti-metricas" aria-label="Indicadores del período">
          <article>
            <span className="reportes-ti-metrica__icono reportes-ti-metrica__icono--turquesa">
              <Icono nombre="carpeta" />
            </span>
            <div>
              <small>Tickets del período</small>
              <strong>{cargando ? '—' : (datos?.resumen.total ?? 0)}</strong>
              <Variacion actual={datos?.resumen.total ?? null} anterior={datos?.resumen.totalAnterior ?? null} />
            </div>
          </article>
          <article>
            <span className="reportes-ti-metrica__icono reportes-ti-metrica__icono--azul">
              <Icono nombre="reloj" />
            </span>
            <div>
              <small>Tiempo promedio de resolución</small>
              <strong>{cargando ? '—' : `${numero(datos?.resumen.tiempoPromedioHoras ?? null)} h`}</strong>
              <Variacion
                actual={datos?.resumen.tiempoPromedioHoras ?? null}
                anterior={datos?.resumen.tiempoPromedioHorasAnterior ?? null}
                menorEsMejor
              />
            </div>
          </article>
          <article>
            <span className="reportes-ti-metrica__icono reportes-ti-metrica__icono--verde">
              <Icono nombre="check" />
            </span>
            <div>
              <small>Resueltos</small>
              <strong>{cargando ? '—' : (datos?.resumen.resueltos ?? 0)}</strong>
              <Variacion actual={datos?.resumen.resueltos ?? null} anterior={datos?.resumen.resueltosAnterior ?? null} />
            </div>
          </article>
          <article>
            <span className="reportes-ti-metrica__icono reportes-ti-metrica__icono--amarillo">
              <Icono nombre="estrella" />
            </span>
            <div>
              <small>Satisfacción</small>
              <strong>
                {cargando
                  ? '—'
                  : datos?.resumen.satisfaccion === null || datos?.resumen.satisfaccion === undefined
                    ? 'Sin datos'
                    : `${numero(datos.resumen.satisfaccion)} / 5`}
              </strong>
              <Variacion actual={datos?.resumen.satisfaccion ?? null} anterior={datos?.resumen.satisfaccionAnterior ?? null} puntos />
            </div>
          </article>
        </section>

        <section className="reportes-ti-panel reportes-ti-filtros-panel" ref={filtrosRef}>
          <div className="reportes-ti-panel__cabecera">
            <div>
              <span className="reportes-ti-panel__icono">
                <Icono nombre="filtro" />
              </span>
              <div>
                <h2>Filtros del reporte</h2>
                <p>Personaliza el rango y los criterios aplicados a todos los indicadores.</p>
              </div>
            </div>
            <button type="button" className="reportes-ti-limpiar" onClick={limpiarFiltros}>
              <Icono nombre="actualizar" size={15} /> Limpiar filtros
            </button>
          </div>
          <form
            className="reportes-ti-filtros"
            onSubmit={e => {
              e.preventDefault()
              void cargar()
            }}
          >
            <label>
              Desde
              <input
                type="date"
                required
                value={filtros.fechaInicio}
                max={filtros.fechaFin}
                onChange={e => setFiltros(v => ({ ...v, fechaInicio: e.target.value }))}
              />
            </label>
            <label>
              Hasta
              <input
                type="date"
                required
                value={filtros.fechaFin}
                min={filtros.fechaInicio}
                onChange={e => setFiltros(v => ({ ...v, fechaFin: e.target.value }))}
              />
            </label>
            <label>
              Área
              <select value={filtros.area} onChange={e => setFiltros(v => ({ ...v, area: e.target.value }))}>
                <option value="">Todas las áreas</option>
                {datos?.catalogos.areas.map(x => (
                  <option key={x.codigo} value={x.codigo}>
                    {x.descripcion}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Estado
              <select value={filtros.estado} onChange={e => setFiltros(v => ({ ...v, estado: e.target.value }))}>
                <option value="">Todos los estados</option>
                {datos?.catalogos.estados.map(x => (
                  <option key={x.codigo} value={x.codigo}>
                    {x.descripcion}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Prioridad
              <select value={filtros.prioridad} onChange={e => setFiltros(v => ({ ...v, prioridad: e.target.value }))}>
                <option value="">Todas</option>
                <option value="ALTA">Alta</option>
                <option value="MEDIA">Media</option>
                <option value="BAJA">Baja</option>
                <option value="SIN">Sin asignar</option>
              </select>
            </label>
            <label>
              Tipo
              <select value={filtros.tipo} onChange={e => setFiltros(v => ({ ...v, tipo: e.target.value }))}>
                <option value="">Todos los tipos</option>
                {datos?.catalogos.tipos.map(x => (
                  <option key={x.codigo} value={x.codigo}>
                    {x.descripcion}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Responsable
              <select value={filtros.usuarioTI} onChange={e => setFiltros(v => ({ ...v, usuarioTI: e.target.value }))}>
                <option value="">Todos</option>
                <option value="SIN_ASIGNAR">Sin asignar</option>
                {datos?.catalogos.operadores.map(x => (
                  <option key={x.codigo} value={x.codigo}>
                    {x.descripcion}
                  </option>
                ))}
              </select>
            </label>
            <button type="submit" disabled={cargando}>
              {cargando ? 'Actualizando...' : 'Aplicar filtros'} <Icono nombre="flecha" size={15} />
            </button>
          </form>
        </section>

        {vista === 'resumen' && (
          <VistaResumen
            datos={datos}
            cargando={cargando}
            ticketsPrioritarios={ticketsPrioritarios}
            filtros={filtrosAplicados}
            navigate={navigate}
            exportarCsv={exportarCsv}
            cargar={() => void cargar()}
          />
        )}
        {vista === 'avance' && <VistaAvances datos={datos} cargando={cargando} busqueda={busqueda} />}
        {vista === 'equipo' && <VistaEquipo datos={datos} cargando={cargando} />}
        {vista === 'areas' && (
          <VistaAreas
            datos={datos}
            cargando={cargando}
            seleccionarArea={area => {
              const nuevos = { ...filtros, area }
              setFiltros(nuevos)
              void cargar(nuevos)
            }}
          />
        )}
        {vista === 'listado' && <VistaListado datos={datos} cargando={cargando} busqueda={busqueda} />}
        {vista === 'agente' && <VistaAgente desde={filtrosAplicados.fechaInicio} hasta={filtrosAplicados.fechaFin} />}
      </main>
    </MarcoPortal>
  )
}

function Cabecera({ icono, titulo, subtitulo }: { icono: NombreIcono; titulo: string; subtitulo: string }) {
  return (
    <div className="reportes-ti-panel__cabecera">
      <div>
        <span className="reportes-ti-panel__icono">
          <Icono nombre={icono} />
        </span>
        <div>
          <h2>{titulo}</h2>
          <p>{subtitulo}</p>
        </div>
      </div>
    </div>
  )
}
function Vacio({ texto }: { texto: string }) {
  return <div className="reportes-ti-vacio">{texto}</div>
}

function VistaResumen({
  datos,
  cargando,
  ticketsPrioritarios,
  filtros,
  navigate,
  exportarCsv,
  cargar,
}: {
  datos: ReportesTIRespuesta | null
  cargando: boolean
  ticketsPrioritarios: ReportesTIRespuesta['ticketsPrioridadAlta']
  filtros: ReportesTIFiltros
  navigate: NavigateFunction
  exportarCsv: () => void
  cargar: () => void
}) {
  return (
    <>
      <section className="reportes-ti-grid-superior">
        <article className="reportes-ti-panel reportes-ti-evolucion">
          <Cabecera
            icono="grafico"
            titulo="Incidencias por período"
            subtitulo="Evolución de tickets registrados en el rango seleccionado."
          />
          {cargando ? (
            <Vacio texto="Cargando evolución..." />
          ) : (
            <GraficoEvolucion datos={datos?.evolucion ?? []} inicio={filtros.fechaInicio} fin={filtros.fechaFin} />
          )}
        </article>
        <article className="reportes-ti-panel reportes-ti-estados">
          <Cabecera icono="pastel" titulo="Estados de tickets" subtitulo="Distribución actual de los tickets filtrados." />
          {cargando ? <Vacio texto="Cargando estados..." /> : <GraficoEstados datos={datos?.estados ?? []} />}
        </article>
        <article className="reportes-ti-panel reportes-ti-areas">
          <Cabecera icono="area" titulo="Incidencias por área" subtitulo="Áreas solicitantes con mayor volumen." />
          {cargando ? <Vacio texto="Cargando áreas..." /> : <BarrasAreas datos={datos?.areas ?? []} />}
        </article>
        <article className="reportes-ti-panel reportes-ti-resumen">
          <Cabecera icono="resumen" titulo="Resumen ejecutivo" subtitulo="Lectura rápida del período seleccionado." />
          <ResumenEjecutivo datos={datos} />
        </article>
      </section>
      <section className="reportes-ti-grid-inferior">
        <article className="reportes-ti-panel reportes-ti-tiempos">
          <Cabecera
            icono="reloj"
            titulo="Tiempo de resolución por prioridad"
            subtitulo="Duración calculada desde la creación hasta el cierre."
          />
          <div className="reportes-ti-tabla-wrap">
            <table>
              <thead>
                <tr>
                  <th>Prioridad</th>
                  <th>Tickets</th>
                  <th>Promedio</th>
                  <th>Más rápido</th>
                  <th>Más largo</th>
                </tr>
              </thead>
              <tbody>
                {!datos?.tiemposPorPrioridad.length ? (
                  <tr>
                    <td colSpan={5} className="reportes-ti-tabla-vacia">
                      No hay tickets cerrados suficientes para calcular tiempos.
                    </td>
                  </tr>
                ) : (
                  datos.tiemposPorPrioridad.map(x => (
                    <tr key={x.prioridad}>
                      <td>
                        <span className={`reportes-ti-prioridad reportes-ti-prioridad--${x.prioridad.toLowerCase().replace(' ', '-')}`}>
                          {x.prioridad}
                        </span>
                      </td>
                      <td>{x.tickets}</td>
                      <td>{formatearHoras(x.tiempoPromedioHoras)}</td>
                      <td>{formatearHoras(x.tiempoMasRapidoHoras)}</td>
                      <td>{formatearHoras(x.tiempoMasLargoHoras)}</td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </article>
        <article className="reportes-ti-panel reportes-ti-prioritarios">
          <div className="reportes-ti-panel__cabecera">
            <div>
              <span className="reportes-ti-panel__icono reportes-ti-panel__icono--rojo">
                <Icono nombre="alerta" />
              </span>
              <div>
                <h2>Tickets de prioridad alta recientes</h2>
                <p>Casos que requieren seguimiento dentro del período.</p>
              </div>
            </div>
            <button className="reportes-ti-enlace" type="button" onClick={() => navigate('/gestion-tickets')}>
              Ver todos <Icono nombre="flecha" size={14} />
            </button>
          </div>
          <div className="reportes-ti-tabla-wrap">
            <table>
              <thead>
                <tr>
                  <th>Ticket</th>
                  <th>Título</th>
                  <th>Área</th>
                  <th>Estado</th>
                  <th>Tiempo</th>
                </tr>
              </thead>
              <tbody>
                {ticketsPrioritarios.length === 0 ? (
                  <tr>
                    <td colSpan={5} className="reportes-ti-tabla-vacia">
                      No hay tickets de prioridad alta con el criterio actual.
                    </td>
                  </tr>
                ) : (
                  ticketsPrioritarios.map(x => (
                    <tr key={x.incidenciaNumero}>
                      <td>
                        <strong>{x.incidenciaNumero}</strong>
                      </td>
                      <td>{x.titulo}</td>
                      <td>{x.areaDescripcion}</td>
                      <td>
                        <span className={claseEstado(x.estado)}>{x.estadoDescripcion}</span>
                      </td>
                      <td>{formatearHoras(x.tiempoAbiertoHoras)}</td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </article>
        <aside className="reportes-ti-panel reportes-ti-accesos">
          <Cabecera icono="ticket" titulo="Accesos rápidos" subtitulo="Continúa desde los datos del reporte." />
          <button type="button" onClick={() => navigate('/gestion-tickets')}>
            <Icono nombre="gestion" />
            <span>
              <strong>Ver Gestión de Tickets</strong>
              <small>Revisa y atiende incidencias</small>
            </span>
            <Icono nombre="flecha" size={15} />
          </button>
          <button type="button" onClick={exportarCsv} disabled={!datos?.detalleExportacion.length}>
            <Icono nombre="descargar" />
            <span>
              <strong>Exportar detalle CSV</strong>
              <small>{datos?.detalleExportacion.length ?? 0} registros filtrados</small>
            </span>
            <Icono nombre="flecha" size={15} />
          </button>
          <button type="button" onClick={cargar}>
            <Icono nombre="actualizar" />
            <span>
              <strong>Actualizar reporte</strong>
              <small>Consulta nuevamente SQL Server</small>
            </span>
            <Icono nombre="flecha" size={15} />
          </button>
        </aside>
      </section>
    </>
  )
}

function VistaAvances({ datos, cargando, busqueda }: { datos: ReportesTIRespuesta | null; cargando: boolean; busqueda: string }) {
  const texto = busqueda.trim().toLowerCase()
  const filas = (datos?.avances ?? []).filter(
    x =>
      !texto ||
      `${x.incidenciaNumero} ${x.titulo} ${x.areaDescripcion} ${x.responsable} ${x.estadoDescripcion}`.toLowerCase().includes(texto),
  )
  return (
    <section className="reportes-ti-panel reportes-ti-vista-panel">
      <Cabecera icono="grafico" titulo="Avance de atención" subtitulo="Último avance, actividad registrada y responsable de cada ticket." />
      {cargando ? (
        <Vacio texto="Cargando avances..." />
      ) : (
        <div className="reportes-ti-tabla-wrap">
          <table>
            <thead>
              <tr>
                <th>Ticket</th>
                <th>Asunto</th>
                <th>Área</th>
                <th>Responsable</th>
                <th>Estado</th>
                <th>Último avance</th>
                <th>Actividad</th>
              </tr>
            </thead>
            <tbody>
              {filas.length === 0 ? (
                <tr>
                  <td colSpan={7} className="reportes-ti-tabla-vacia">
                    No hay avances con los criterios seleccionados.
                  </td>
                </tr>
              ) : (
                filas.map(x => (
                  <tr key={x.incidenciaNumero}>
                    <td>
                      <strong>{x.incidenciaNumero}</strong>
                    </td>
                    <td>{x.titulo}</td>
                    <td>{x.areaDescripcion}</td>
                    <td>{x.responsable}</td>
                    <td>
                      <span className={claseEstado(x.estado)}>{x.estadoDescripcion}</span>
                    </td>
                    <td>
                      <Progreso valor={x.porcentajeAvance} />
                      <small className="reportes-ti-fecha-avance">
                        {x.fechaUltimoAvance ? formatearFechaHora(x.fechaUltimoAvance) : 'Sin avance registrado'}
                      </small>
                    </td>
                    <td>
                      <strong>{x.avancesRegistrados}</strong> avances
                      <br />
                      <small>{formatearMinutos(x.minutosRegistrados)}</small>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}

function VistaEquipo({ datos, cargando }: { datos: ReportesTIRespuesta | null; cargando: boolean }) {
  const filas = datos?.avancePorUsuario ?? []
  return (
    <section className="reportes-ti-panel reportes-ti-vista-panel">
      <Cabecera
        icono="gestion"
        titulo="Avance de atención por usuario"
        subtitulo="Carga, resolución y esfuerzo del equipo TI en el período."
      />
      {cargando ? (
        <Vacio texto="Cargando desempeño del equipo..." />
      ) : (
        <div className="reportes-ti-tabla-wrap">
          <table>
            <thead>
              <tr>
                <th>Responsable</th>
                <th>Asignados</th>
                <th>En curso</th>
                <th>Resueltos</th>
                <th>Avances</th>
                <th>Tiempo registrado</th>
                <th>Avance promedio</th>
              </tr>
            </thead>
            <tbody>
              {filas.length === 0 ? (
                <tr>
                  <td colSpan={7} className="reportes-ti-tabla-vacia">
                    No hay asignaciones en el período seleccionado.
                  </td>
                </tr>
              ) : (
                filas.map(x => (
                  <tr key={x.usuario}>
                    <td>
                      <strong>{x.responsable}</strong>
                      <small className="reportes-ti-fecha-avance">
                        {x.usuario === 'SIN_ASIGNAR' ? 'Pendiente de asignación' : x.usuario}
                      </small>
                    </td>
                    <td>{x.ticketsAsignados}</td>
                    <td>{x.ticketsEnCurso}</td>
                    <td>{x.ticketsResueltos}</td>
                    <td>{x.avancesRegistrados}</td>
                    <td>{formatearMinutos(x.minutosRegistrados)}</td>
                    <td>
                      <Progreso valor={x.porcentajePromedio} />
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}

function VistaAreas({
  datos,
  cargando,
  seleccionarArea,
}: {
  datos: ReportesTIRespuesta | null
  cargando: boolean
  seleccionarArea: (area: string) => void
}) {
  const total = datos?.areas.reduce((suma, x) => suma + x.cantidad, 0) ?? 0
  return (
    <section className="reportes-ti-areas-detalle">
      <article className="reportes-ti-panel">
        <Cabecera icono="area" titulo="Incidencias por áreas" subtitulo="Compara el volumen de solicitudes entre áreas." />
        {cargando ? <Vacio texto="Cargando áreas..." /> : <BarrasAreas datos={datos?.areas ?? []} />}
      </article>
      <article className="reportes-ti-panel">
        <Cabecera icono="ticket" titulo="Detalle por cada área" subtitulo="Selecciona un área para aplicar el filtro a todo el módulo." />
        <div className="reportes-ti-tabla-wrap">
          <table>
            <thead>
              <tr>
                <th>Área</th>
                <th>Tickets</th>
                <th>Participación</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {!datos?.areas.length ? (
                <tr>
                  <td colSpan={4} className="reportes-ti-tabla-vacia">
                    No hay información por área.
                  </td>
                </tr>
              ) : (
                datos.areas.map(x => (
                  <tr key={x.area}>
                    <td>
                      <strong>{x.areaDescripcion}</strong>
                    </td>
                    <td>{x.cantidad}</td>
                    <td>{total ? `${((x.cantidad / total) * 100).toFixed(1)}%` : '0%'}</td>
                    <td>
                      <button type="button" className="reportes-ti-enlace" onClick={() => seleccionarArea(x.area)}>
                        Ver área <Icono nombre="flecha" size={14} />
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </article>
    </section>
  )
}

function VistaListado({ datos, cargando, busqueda }: { datos: ReportesTIRespuesta | null; cargando: boolean; busqueda: string }) {
  const [pagina, setPagina] = useState(1)
  const texto = busqueda.trim().toLowerCase()
  const filas = useMemo(
    () =>
      (datos?.detalleExportacion ?? []).filter(
        x =>
          !texto ||
          `${x.incidenciaNumero} ${x.solicitante} ${x.titulo} ${x.areaDescripcion} ${x.responsable} ${x.estadoDescripcion}`
            .toLowerCase()
            .includes(texto),
      ),
    [datos, texto],
  )
  const paginas = Math.max(1, Math.ceil(filas.length / 10))
  useEffect(() => {
    setPagina(1)
  }, [datos, texto])
  const visibles = filas.slice((pagina - 1) * 10, pagina * 10)
  return (
    <section className="reportes-ti-panel reportes-ti-vista-panel">
      <Cabecera
        icono="ticket"
        titulo="Listado de tickets de atención"
        subtitulo={`${filas.length} tickets coinciden con el período y los filtros aplicados.`}
      />
      {cargando ? (
        <Vacio texto="Cargando tickets..." />
      ) : (
        <>
          <div className="reportes-ti-tabla-wrap">
            <table>
              <thead>
                <tr>
                  <th>Ticket</th>
                  <th>Solicitante</th>
                  <th>Asunto</th>
                  <th>Área</th>
                  <th>Tipo</th>
                  <th>Estado</th>
                  <th>Prioridad</th>
                  <th>Responsable</th>
                  <th>Registro</th>
                </tr>
              </thead>
              <tbody>
                {visibles.length === 0 ? (
                  <tr>
                    <td colSpan={9} className="reportes-ti-tabla-vacia">
                      No hay tickets con los criterios seleccionados.
                    </td>
                  </tr>
                ) : (
                  visibles.map(x => (
                    <tr key={x.incidenciaNumero}>
                      <td>
                        <strong>{x.incidenciaNumero}</strong>
                      </td>
                      <td>{x.solicitante}</td>
                      <td>{x.titulo}</td>
                      <td>{x.areaDescripcion}</td>
                      <td>{x.tipoDescripcion}</td>
                      <td>{x.estadoDescripcion}</td>
                      <td>
                        <span className={clasePrioridad(x.prioridad)}>{textoPrioridad(x.prioridad)}</span>
                      </td>
                      <td>{x.responsable}</td>
                      <td>{formatearFechaHora(x.fechaRegistro)}</td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
          <Paginacion pagina={pagina} paginas={paginas} total={filas.length} cambiar={setPagina} />
        </>
      )}
    </section>
  )
}

function Progreso({ valor }: { valor: number | null }) {
  return (
    <div className="reportes-ti-progreso">
      <div>
        <i style={{ width: `${Math.min(100, Math.max(0, valor ?? 0))}%` }} />
      </div>
      <strong>{valor === null ? 'Sin %' : `${numero(valor, 0)}%`}</strong>
    </div>
  )
}

function Paginacion({
  pagina,
  paginas,
  total,
  cambiar,
}: {
  pagina: number
  paginas: number
  total: number
  cambiar: (pagina: number) => void
}) {
  const inicio = Math.max(1, Math.min(pagina - 2, paginas - 4))
  const visibles = Array.from({ length: Math.min(5, paginas) }, (_, i) => inicio + i)
  return (
    <div className="reportes-ti-paginacion">
      <span>{total === 0 ? 'Sin registros' : `${(pagina - 1) * 10 + 1}-${Math.min(pagina * 10, total)} de ${total}`}</span>
      <div>
        {pagina > 1 && (
          <button type="button" onClick={() => cambiar(pagina - 1)} aria-label="Página anterior">
            ‹
          </button>
        )}
        {visibles.map(x => (
          <button type="button" key={x} className={x === pagina ? 'activo' : ''} onClick={() => cambiar(x)} aria-label={`Página ${x}`}>
            {x}
          </button>
        ))}
        {pagina < paginas && (
          <button type="button" onClick={() => cambiar(pagina + 1)} aria-label="Página siguiente">
            ›
          </button>
        )}
      </div>
    </div>
  )
}

function formatearFechaHora(valor: string) {
  return new Intl.DateTimeFormat('es-PE', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(valor))
}
function formatearMinutos(valor: number) {
  return valor <= 0 ? 'Sin tiempo' : valor >= 60 ? `${numero(valor / 60)} h` : `${numero(valor, 0)} min`
}

function GraficoEvolucion({ datos, inicio, fin }: { datos: ReportesTIEvolucion[]; inicio: string; fin: string }) {
  const serie = useMemo(() => completarSerie(datos, inicio, fin), [datos, inicio, fin])
  if (!serie.length) return <Vacio texto="No hay información para el período seleccionado." />
  const max = Math.max(...serie.map(x => x.cantidad), 1)
  const puntos = serie
    .map((x, i) => `${serie.length === 1 ? 50 : (i / (serie.length - 1)) * 100},${92 - (x.cantidad / max) * 78}`)
    .join(' ')
  const area = `0,92 ${puntos} 100,92`
  const indices = [0, Math.floor((serie.length - 1) / 2), serie.length - 1].filter((v, i, a) => a.indexOf(v) === i)
  return (
    <div className="reportes-ti-linea">
      <svg viewBox="0 0 100 100" preserveAspectRatio="none" role="img" aria-label="Evolución de incidencias">
        <defs>
          <linearGradient id="reporteArea" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor="currentColor" stopOpacity=".28" />
            <stop offset="100%" stopColor="currentColor" stopOpacity="0" />
          </linearGradient>
        </defs>
        <g className="reportes-ti-linea__rejilla">
          {[20, 40, 60, 80].map(y => (
            <line key={y} x1="0" y1={y} x2="100" y2={y} />
          ))}
        </g>
        <polygon points={area} fill="url(#reporteArea)" />
        <polyline points={puntos} />
        {serie.map((x, i) => (
          <circle key={x.fecha} cx={serie.length === 1 ? 50 : (i / (serie.length - 1)) * 100} cy={92 - (x.cantidad / max) * 78} r="1.4">
            <title>{`${fechaCorta(x.fecha)}: ${x.cantidad} tickets`}</title>
          </circle>
        ))}
      </svg>
      <div className="reportes-ti-linea__eje">
        {indices.map(i => (
          <span key={i}>{fechaCorta(serie[i].fecha)}</span>
        ))}
      </div>
    </div>
  )
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
  if (!total) return <Vacio texto="No hay tickets para distribuir por estado." />
  const paleta = ['#15977f', '#1f82e6', '#f0b429', '#ef5350', '#7c5ce7', '#5aa9a2', '#607d8b']
  let acumulado = 0
  const segmentos = datos
    .map((x, i) => {
      const inicio = (acumulado / total) * 100
      acumulado += x.cantidad
      const fin = (acumulado / total) * 100
      return `${paleta[i % paleta.length]} ${inicio}% ${fin}%`
    })
    .join(', ')
  return (
    <div className="reportes-ti-donut-wrap">
      <div className="reportes-ti-donut" style={{ background: `conic-gradient(${segmentos})` }}>
        <div>
          <strong>{total}</strong>
          <span>tickets</span>
        </div>
      </div>
      <div className="reportes-ti-leyenda">
        {datos.slice(0, 6).map((x, i) => (
          <div key={x.estado}>
            <span className="reportes-ti-leyenda__punto" style={{ background: paleta[i % paleta.length] }} />
            <span>{x.estadoDescripcion}</span>
            <strong>{x.cantidad}</strong>
            <small>{Math.round((x.cantidad / total) * 100)}%</small>
          </div>
        ))}
      </div>
    </div>
  )
}

function BarrasAreas({ datos }: { datos: { area: string; areaDescripcion: string; cantidad: number }[] }) {
  if (!datos.length) return <Vacio texto="No hay información por área." />
  const max = Math.max(...datos.map(x => x.cantidad), 1)
  return (
    <div className="reportes-ti-barras">
      {datos.slice(0, 7).map(x => (
        <div key={x.area}>
          <span title={x.areaDescripcion}>{x.areaDescripcion}</span>
          <div>
            <i style={{ width: `${Math.max(4, (x.cantidad / max) * 100)}%` }} />
          </div>
          <strong>{x.cantidad}</strong>
        </div>
      ))}
    </div>
  )
}

function ResumenEjecutivo({ datos }: { datos: ReportesTIRespuesta | null }) {
  const r = datos?.resumen
  const tasaResolucion = r && r.total > 0 ? (r.resueltos / r.total) * 100 : null
  return (
    <div className="reportes-ti-resumen-lista">
      <div>
        <span className="reportes-ti-resumen-lista__icono">
          <Icono nombre="grafico" size={17} />
        </span>
        <p>
          <strong>{r?.total ?? 0}</strong>
          <small>tickets registrados</small>
        </p>
      </div>
      <div>
        <span className="reportes-ti-resumen-lista__icono">
          <Icono nombre="check" size={17} />
        </span>
        <p>
          <strong>{r?.resueltos ?? 0}</strong>
          <small>tickets resueltos · {tasaResolucion === null ? '—' : `${tasaResolucion.toFixed(0)}%`}</small>
        </p>
      </div>
      <div>
        <span className="reportes-ti-resumen-lista__icono">
          <Icono nombre="reloj" size={17} />
        </span>
        <p>
          <strong>{numero(r?.horasEfectivas ?? null)} h</strong>
          <small>esfuerzo efectivo · {r?.ticketsConEsfuerzo ?? 0} tickets</small>
        </p>
      </div>
      <div>
        <span className="reportes-ti-resumen-lista__icono">
          <Icono nombre="reloj" size={17} />
        </span>
        <p>
          <strong>{numero(r?.cumplimientoSla ?? null)}%</strong>
          <small>cumplimiento de SLA</small>
        </p>
      </div>
      <div>
        <span className="reportes-ti-resumen-lista__icono">
          <Icono nombre="alerta" size={17} />
        </span>
        <p>
          <strong>{r?.reabiertos ?? 0}</strong>
          <small>tickets reabiertos</small>
        </p>
      </div>
      <div>
        <span className="reportes-ti-resumen-lista__icono">
          <Icono nombre="estrella" size={17} />
        </span>
        <p>
          <strong>{r?.satisfaccion === null || r?.satisfaccion === undefined ? '—' : `${numero(r.satisfaccion)} / 5`}</strong>
          <small>satisfacción de usuarios</small>
        </p>
      </div>
    </div>
  )
}

function formatearHoras(valor: number | null) {
  if (valor === null) return '—'
  if (valor >= 48) return `${(valor / 24).toFixed(1)} días`
  if (valor < 1) return `${Math.round(valor * 60)} min`
  return `${valor.toFixed(1)} h`
}
