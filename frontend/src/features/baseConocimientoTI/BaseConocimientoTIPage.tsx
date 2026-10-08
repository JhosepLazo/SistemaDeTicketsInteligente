/**
 * Archivo: BaseConocimientoTIPage.tsx
 * Objetivo: Implementar el módulo Base de Conocimiento para el operador TI autenticado.
 * Responsabilidad: Permitir buscar, revisar, crear, editar, validar, revalidar e inactivar conocimiento reutilizable, incluyendo la creación
 *   guiada desde tickets resueltos y la guía de diagnóstico que el agente sigue como playbook.
 * Dependencias: AutenticacionContext, baseConocimientoTIApi, MarcoPortal, Icono, EditorGuiaDiagnostico y BaseConocimientoTIPage.css.
 * Flujo: Ruta protegida /base-conocimiento -> BaseConocimientoTIPage -> API /api/base-conocimiento -> Stored Procedures -> SQL Server.
 * Consideraciones: La vista conserva el patrón visual CALIMOD; no inventa métricas de favoritos o visualizaciones que el modelo actual no registra y separa borrador, validación, publicación e inactivación.
 */

import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAutenticacion } from '../autenticacion/AutenticacionContext'
import {
  actualizarBaseConocimientoTI,
  crearBaseConocimientoTI,
  enviarValidacionBaseConocimientoTI,
  inactivarBaseConocimientoTI,
  obtenerBaseConocimientoTI,
  obtenerDetalleBaseConocimientoTI,
  validarBaseConocimientoTI,
  type BaseConocimientoTIDetalle,
  type BaseConocimientoTIItem,
  type BaseConocimientoTIRespuesta,
  type BaseConocimientoTITicketOrigen,
  type GuardarBaseConocimientoTISolicitud,
} from '../../services/baseConocimientoTIApi'
import MarcoPortal from '../../components/MarcoPortal'
import IconoBase, { type PropsIcono } from '../../components/Icono'
import EditorGuiaDiagnostico from './EditorGuiaDiagnostico'
import './BaseConocimientoTIPage.css'

// Esta pantalla dibuja sus íconos a 19 px.
const Icono = (props: PropsIcono) => <IconoBase size={19} {...props} />

type FiltroEstado = 'TODOS' | 'A' | 'P' | 'B' | 'I' | 'REVISION'
type Orden = 'RECIENTES' | 'TITULO'

const solicitudVacia: GuardarBaseConocimientoTISolicitud = {
  titulo: '',
  problema: '',
  sintomas: '',
  mensajeError: null,
  causa: '',
  solucion: '',
  procedimiento: null,
  linea: '',
  item: '',
  tipo: '',
  subTipo: '',
  categoria: '',
  incidenciaOrigen: null,
  guia: [],
}

function fechaCorta(fecha: string | null) {
  return fecha ? new Intl.DateTimeFormat('es-PE', { dateStyle: 'medium' }).format(new Date(fecha)) : '—'
}
function fechaHora(fecha: string | null) {
  return fecha ? new Intl.DateTimeFormat('es-PE', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(fecha)) : '—'
}
function estadoClase(estado: string) {
  return `conocimiento-ti-badge conocimiento-ti-badge--${estado === 'A' ? 'activo' : estado === 'P' ? 'pendiente' : estado === 'B' ? 'borrador' : 'inactivo'}`
}

export default function BaseConocimientoTIPage() {
  const navigate = useNavigate()
  const { usuario } = useAutenticacion()
  const buscadorRef = useRef<HTMLInputElement>(null)
  const articulosRef = useRef<HTMLElement>(null)
  const ticketsRef = useRef<HTMLElement>(null)
  const solicitudDetalleRef = useRef(0)
  const [datos, setDatos] = useState<BaseConocimientoTIRespuesta | null>(null)
  const [detalle, setDetalle] = useState<BaseConocimientoTIDetalle | null>(null)
  const [seleccionado, setSeleccionado] = useState('')
  const [cargando, setCargando] = useState(true)
  const [cargandoDetalle, setCargandoDetalle] = useState(false)
  const [procesando, setProcesando] = useState(false)
  const [error, setError] = useState('')
  const [mensaje, setMensaje] = useState('')
  const [busqueda, setBusqueda] = useState('')
  const [filtroEstado, setFiltroEstado] = useState<FiltroEstado>('TODOS')
  const [filtroLinea, setFiltroLinea] = useState('')
  const [filtroCategoria, setFiltroCategoria] = useState('')
  const [orden, setOrden] = useState<Orden>('RECIENTES')
  const [modalAbierto, setModalAbierto] = useState(false)
  const [codigoEdicion, setCodigoEdicion] = useState<string | null>(null)
  const [formulario, setFormulario] = useState<GuardarBaseConocimientoTISolicitud>(solicitudVacia)

  async function cargarDetalle(codigo: string) {
    const solicitud = ++solicitudDetalleRef.current
    setSeleccionado(codigo)
    setCargandoDetalle(true)
    try {
      const valor = await obtenerDetalleBaseConocimientoTI(codigo)
      if (solicitud === solicitudDetalleRef.current) setDetalle(valor)
    } catch (e) {
      if (solicitud === solicitudDetalleRef.current) {
        setDetalle(null)
        setError(e instanceof Error ? e.message : 'No fue posible cargar el artículo.')
      }
    } finally {
      if (solicitud === solicitudDetalleRef.current) setCargandoDetalle(false)
    }
  }

  async function cargarModulo(preferido?: string) {
    setCargando(true)
    setError('')
    try {
      const respuesta = await obtenerBaseConocimientoTI()
      setDatos(respuesta)
      const codigo = preferido || seleccionado || respuesta.articulos[0]?.conocimientoCodigo || ''
      if (codigo && respuesta.articulos.some(x => x.conocimientoCodigo === codigo)) await cargarDetalle(codigo)
      else {
        solicitudDetalleRef.current++
        setSeleccionado('')
        setDetalle(null)
        setCargandoDetalle(false)
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No fue posible cargar la Base de Conocimiento.')
    } finally {
      setCargando(false)
    }
  }

  useEffect(() => {
    void cargarModulo()
  }, [])
  useEffect(() => {
    const manejar = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault()
        buscadorRef.current?.focus()
        buscadorRef.current?.select()
      }
      if (e.key === 'Escape' && !procesando) setModalAbierto(false)
    }
    window.addEventListener('keydown', manejar)
    return () => window.removeEventListener('keydown', manejar)
  }, [procesando])

  const articulosFiltrados = useMemo(() => {
    if (!datos) return []
    const texto = busqueda.trim().toLowerCase()
    const resultado = datos.articulos.filter(articulo => {
      if (filtroEstado === 'REVISION' && !articulo.requiereRevision) return false
      if (!['TODOS', 'REVISION'].includes(filtroEstado) && articulo.estado !== filtroEstado) return false
      if (filtroLinea && articulo.linea !== filtroLinea) return false
      if (filtroCategoria && articulo.categoria !== filtroCategoria) return false
      return (
        !texto ||
        `${articulo.conocimientoCodigo} ${articulo.titulo} ${articulo.problema} ${articulo.solucion} ${articulo.itemDescripcion} ${articulo.categoriaDescripcion} ${articulo.lineaDescripcion}`
          .toLowerCase()
          .includes(texto)
      )
    })

    return [...resultado].sort((a, b) =>
      orden === 'TITULO'
        ? a.titulo.localeCompare(b.titulo, 'es')
        : new Date(b.fechaRevision || b.fechaValidacion || b.fechaCreacion).getTime() -
          new Date(a.fechaRevision || a.fechaValidacion || a.fechaCreacion).getTime(),
    )
  }, [busqueda, datos, filtroCategoria, filtroEstado, filtroLinea, orden])

  const categoriasPopulares = useMemo(() => {
    if (!datos) return []
    const conteo = new Map<string, { descripcion: string; cantidad: number }>()
    datos.articulos
      .filter(x => x.estado !== 'I')
      .forEach(x => {
        if (!x.categoria) return
        const actual = conteo.get(x.categoria)
        conteo.set(x.categoria, { descripcion: x.categoriaDescripcion || x.categoria, cantidad: (actual?.cantidad ?? 0) + 1 })
      })
    return [...conteo.entries()].sort((a, b) => b[1].cantidad - a[1].cantidad).slice(0, 6)
  }, [datos])

  if (!usuario) return null

  function limpiarFiltros() {
    setBusqueda('')
    setFiltroEstado('TODOS')
    setFiltroLinea('')
    setFiltroCategoria('')
    setOrden('RECIENTES')
  }
  function irAArticulos() {
    articulosRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  }
  function filtrarEstado(estado: FiltroEstado) {
    setFiltroEstado(estado)
    requestAnimationFrame(irAArticulos)
  }

  function abrirNuevo(ticket?: BaseConocimientoTITicketOrigen) {
    setError('')
    setCodigoEdicion(null)
    setFormulario(ticket ? solicitudDesdeTicket(ticket) : { ...solicitudVacia })
    setModalAbierto(true)
  }

  function abrirEditar() {
    if (!detalle) return
    setError('')
    setCodigoEdicion(detalle.conocimientoCodigo)
    setFormulario({
      titulo: detalle.titulo,
      problema: detalle.problema,
      sintomas: detalle.sintomas,
      mensajeError: detalle.mensajeError || null,
      causa: detalle.causa,
      solucion: detalle.solucion,
      procedimiento: detalle.procedimiento || null,
      linea: detalle.linea,
      item: detalle.item,
      tipo: detalle.tipo,
      subTipo: detalle.subTipo,
      categoria: detalle.categoria,
      incidenciaOrigen: detalle.incidenciaOrigen || null,
      guia: detalle.guia ?? [],
    })
    setModalAbierto(true)
  }

  function seleccionarTicketOrigen(numero: string) {
    if (!numero) {
      setFormulario(v => ({ ...v, incidenciaOrigen: null }))
      return
    }
    const ticket = datos?.catalogos.ticketsOrigen.find(x => x.incidenciaNumero === numero)
    if (ticket && !codigoEdicion) setFormulario(solicitudDesdeTicket(ticket))
    else setFormulario(v => ({ ...v, incidenciaOrigen: numero }))
  }

  async function guardarArticulo() {
    setProcesando(true)
    setError('')
    setMensaje('')
    try {
      const codigo = codigoEdicion
        ? (await actualizarBaseConocimientoTI(codigoEdicion, formulario), codigoEdicion)
        : await crearBaseConocimientoTI(formulario)
      setModalAbierto(false)
      setMensaje(
        codigoEdicion
          ? 'El artículo fue actualizado y quedó listo para continuar su flujo de validación.'
          : `Se creó ${codigo} como borrador.`,
      )
      await cargarModulo(codigo)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No fue posible guardar el artículo.')
    } finally {
      setProcesando(false)
    }
  }

  async function ejecutarEstado(accion: () => Promise<void>, exito: string) {
    if (!detalle) return
    setProcesando(true)
    setError('')
    setMensaje('')
    try {
      await accion()
      setMensaje(exito)
      await cargarModulo(detalle.conocimientoCodigo)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No fue posible completar la operación.')
    } finally {
      setProcesando(false)
    }
  }

  async function manejarInactivacion() {
    if (
      !detalle ||
      !window.confirm(`¿Inactivar ${detalle.conocimientoCodigo}? El artículo dejará de estar disponible para consulta publicada.`)
    )
      return
    await ejecutarEstado(
      () => inactivarBaseConocimientoTI(detalle.conocimientoCodigo),
      'El artículo fue inactivado sin eliminar su historial.',
    )
  }

  return (
    <MarcoPortal
      menu="ti"
      activa="conocimiento"
      clase="conocimiento-ti-page"
      barra={
        <label className="inicio-buscador">
          <Icono nombre="buscar" />
          <input
            ref={buscadorRef}
            value={busqueda}
            onChange={e => setBusqueda(e.target.value)}
            placeholder="Buscar artículos, errores o soluciones..."
          />
          <span>Ctrl + K</span>
        </label>
      }
      capas={
        <>
          {modalAbierto && datos && (
            <div
              className="conocimiento-ti-modal-fondo"
              onMouseDown={e => {
                if (e.target === e.currentTarget && !procesando) setModalAbierto(false)
              }}
            >
              <section className="conocimiento-ti-modal" role="dialog" aria-modal="true">
                <header>
                  <div>
                    <span>{codigoEdicion ? codigoEdicion : 'Nuevo borrador'}</span>
                    <h2>{codigoEdicion ? 'Editar artículo de conocimiento' : 'Crear artículo de conocimiento'}</h2>
                  </div>
                  <button type="button" onClick={() => setModalAbierto(false)} disabled={procesando}>
                    <Icono nombre="cerrar" />
                  </button>
                </header>
                {error && (
                  <div className="conocimiento-ti-alerta conocimiento-ti-alerta--error conocimiento-ti-modal__alerta" role="alert">
                    <span>{error}</span>
                  </div>
                )}
                <form
                  onSubmit={e => {
                    e.preventDefault()
                    void guardarArticulo()
                  }}
                >
                  <div className="conocimiento-ti-form-grid">
                    <label className="ancho-completo">
                      Título
                      <input
                        required
                        minLength={5}
                        maxLength={250}
                        value={formulario.titulo}
                        onChange={e => setFormulario(v => ({ ...v, titulo: e.target.value }))}
                        placeholder="Ej. Error al generar orden de compra por inconsistencia de datos"
                      />
                    </label>
                    <label>
                      Ticket de origen
                      <select value={formulario.incidenciaOrigen ?? ''} onChange={e => seleccionarTicketOrigen(e.target.value)}>
                        <option value="">Sin ticket de origen</option>
                        {formulario.incidenciaOrigen &&
                          !datos.catalogos.ticketsOrigen.some(x => x.incidenciaNumero === formulario.incidenciaOrigen) && (
                            <option value={formulario.incidenciaOrigen}>{formulario.incidenciaOrigen}</option>
                          )}
                        {datos.catalogos.ticketsOrigen.map(x => (
                          <option key={x.incidenciaNumero} value={x.incidenciaNumero}>
                            {x.incidenciaNumero} · {x.titulo}
                          </option>
                        ))}
                      </select>
                      <small>Solo se ofrecen tickets resueltos con solución técnica registrada.</small>
                    </label>
                    <label>
                      Línea
                      <select
                        required
                        value={formulario.linea}
                        onChange={e => setFormulario(v => ({ ...v, linea: e.target.value, item: '' }))}
                      >
                        <option value="">Seleccionar</option>
                        {datos.catalogos.lineas.map(x => (
                          <option key={x.codigo} value={x.codigo}>
                            {x.descripcion}
                          </option>
                        ))}
                      </select>
                    </label>
                    <label>
                      Item
                      <select required value={formulario.item} onChange={e => setFormulario(v => ({ ...v, item: e.target.value }))}>
                        <option value="">Seleccionar</option>
                        {datos.catalogos.items
                          .filter(x => x.linea === formulario.linea)
                          .map(x => (
                            <option key={x.codigo} value={x.codigo}>
                              {x.descripcion}
                            </option>
                          ))}
                      </select>
                    </label>
                    <label>
                      Tipo
                      <select
                        required
                        value={formulario.tipo}
                        onChange={e => setFormulario(v => ({ ...v, tipo: e.target.value, subTipo: '' }))}
                      >
                        <option value="">Seleccionar</option>
                        {datos.catalogos.tipos.map(x => (
                          <option key={x.codigo} value={x.codigo}>
                            {x.descripcion}
                          </option>
                        ))}
                      </select>
                    </label>
                    <label>
                      Categoría
                      <select
                        required
                        value={formulario.categoria}
                        onChange={e => setFormulario(v => ({ ...v, categoria: e.target.value, subTipo: '' }))}
                      >
                        <option value="">Seleccionar</option>
                        {datos.catalogos.categorias.map(x => (
                          <option key={x.codigo} value={x.codigo}>
                            {x.descripcion}
                          </option>
                        ))}
                      </select>
                    </label>
                    <label>
                      Subtipo
                      <select required value={formulario.subTipo} onChange={e => setFormulario(v => ({ ...v, subTipo: e.target.value }))}>
                        <option value="">Seleccionar</option>
                        {datos.catalogos.subTipos
                          .filter(x => x.tipo === formulario.tipo && x.categoria === formulario.categoria)
                          .map(x => (
                            <option key={`${x.tipo}-${x.categoria}-${x.codigo}`} value={x.codigo}>
                              {x.descripcion}
                            </option>
                          ))}
                      </select>
                    </label>
                    <label className="ancho-completo">
                      Problema
                      <textarea
                        required
                        minLength={10}
                        maxLength={6000}
                        value={formulario.problema}
                        onChange={e => setFormulario(v => ({ ...v, problema: e.target.value }))}
                        placeholder="Describe qué situación resuelve este conocimiento."
                      />
                    </label>
                    <label className="ancho-completo">
                      Síntomas
                      <textarea
                        required
                        minLength={5}
                        maxLength={6000}
                        value={formulario.sintomas}
                        onChange={e => setFormulario(v => ({ ...v, sintomas: e.target.value }))}
                        placeholder="Indica señales, comportamientos o condiciones que permiten reconocer el caso."
                      />
                    </label>
                    <label className="ancho-completo">
                      Mensaje de error <span>(opcional)</span>
                      <textarea
                        maxLength={1000}
                        value={formulario.mensajeError ?? ''}
                        onChange={e => setFormulario(v => ({ ...v, mensajeError: e.target.value || null }))}
                        placeholder="Copia el mensaje exacto cuando exista."
                      />
                    </label>
                    <label className="ancho-completo">
                      Causa
                      <textarea
                        required
                        minLength={5}
                        maxLength={6000}
                        value={formulario.causa}
                        onChange={e => setFormulario(v => ({ ...v, causa: e.target.value }))}
                        placeholder="Documenta la causa confirmada o validada por TI."
                      />
                    </label>
                    <label className="ancho-completo">
                      Solución
                      <textarea
                        required
                        minLength={5}
                        maxLength={6000}
                        value={formulario.solucion}
                        onChange={e => setFormulario(v => ({ ...v, solucion: e.target.value }))}
                        placeholder="Explica la solución de forma clara y reutilizable."
                      />
                    </label>
                    <label className="ancho-completo">
                      Procedimiento <span>(opcional)</span>
                      <textarea
                        maxLength={8000}
                        value={formulario.procedimiento ?? ''}
                        onChange={e => setFormulario(v => ({ ...v, procedimiento: e.target.value || null }))}
                        placeholder="Detalla los pasos en orden cuando la solución requiera una secuencia específica."
                      />
                    </label>
                    <EditorGuiaDiagnostico pasos={formulario.guia} onCambiar={guia => setFormulario(v => ({ ...v, guia }))} />
                  </div>
                  <div className="conocimiento-ti-modal__nota">
                    <Icono nombre="borrador" size={18} />
                    <span>
                      {codigoEdicion
                        ? 'Guardar cambios actualizará la fecha de revisión. Si el artículo estaba publicado, volverá a Pendiente de validación.'
                        : 'Los nuevos artículos se guardan como Borrador. Publícalos únicamente después de revisar su contenido.'}
                    </span>
                  </div>
                  <footer>
                    <button type="button" onClick={() => setModalAbierto(false)} disabled={procesando}>
                      Cancelar
                    </button>
                    <button className="principal" type="submit" disabled={procesando}>
                      {procesando ? 'Guardando...' : codigoEdicion ? 'Guardar cambios' : 'Crear borrador'}
                    </button>
                  </footer>
                </form>
              </section>
            </div>
          )}
        </>
      }
    >
      <main className="inicio-contenido conocimiento-ti-contenido">
        <section className="inicio-hero conocimiento-ti-hero">
          <div className="inicio-hero__contenido">
            <h1>
              <Icono nombre="conocimiento" size={31} /> Base de Conocimiento
            </h1>
            <p className="inicio-hero__resumen">
              Documenta, valida y reutiliza soluciones técnicas confiables para acelerar la atención TI.
            </p>
            <p className="inicio-hero__detalle">
              Convierte experiencia operativa y tickets resueltos en conocimiento mantenible, trazable y listo para el futuro Asistente TI.
            </p>
          </div>
          <div className="inicio-hero__acciones conocimiento-ti-hero__acciones">
            <button type="button" onClick={() => abrirNuevo()}>
              <Icono nombre="nuevo" /> Nuevo artículo
            </button>
            <button
              type="button"
              className="inicio-hero__secundario"
              onClick={() => ticketsRef.current?.scrollIntoView({ behavior: 'smooth' })}
            >
              <Icono nombre="ticket" /> Desde ticket resuelto
            </button>
            <button type="button" className="inicio-hero__secundario" onClick={() => filtrarEstado('P')}>
              <Icono nombre="validar" /> Revisar pendientes
            </button>
          </div>
          <div className="inicio-hero__firma">
            <span>Personas</span>
            <span>que avanzan</span>
            <strong>CALIMOD</strong>
          </div>
        </section>

        {error && (
          <div className="conocimiento-ti-alerta conocimiento-ti-alerta--error" role="alert">
            <span>{error}</span>
            <button onClick={() => setError('')}>
              <Icono nombre="cerrar" size={16} />
            </button>
          </div>
        )}
        {mensaje && (
          <div className="conocimiento-ti-alerta conocimiento-ti-alerta--ok" role="status">
            <Icono nombre="check" />
            <span>{mensaje}</span>
            <button onClick={() => setMensaje('')}>
              <Icono nombre="cerrar" size={16} />
            </button>
          </div>
        )}

        <section className="conocimiento-ti-metricas">
          <button onClick={() => filtrarEstado('A')}>
            <span className="conocimiento-ti-metrica__icono conocimiento-ti-metrica__icono--turquesa">
              <Icono nombre="libro" />
            </span>
            <div>
              <small>Artículos publicados</small>
              <strong>{datos?.resumen.activos ?? 0}</strong>
              <span>conocimiento validado</span>
            </div>
          </button>
          <button onClick={() => filtrarEstado('P')}>
            <span className="conocimiento-ti-metrica__icono conocimiento-ti-metrica__icono--azul">
              <Icono nombre="validar" />
            </span>
            <div>
              <small>Pendientes de validación</small>
              <strong>{datos?.resumen.pendientesValidacion ?? 0}</strong>
              <span>esperan revisión técnica</span>
            </div>
          </button>
          <button onClick={() => filtrarEstado('REVISION')}>
            <span className="conocimiento-ti-metrica__icono conocimiento-ti-metrica__icono--rojo">
              <Icono nombre="alertaTrianguloFino" />
            </span>
            <div>
              <small>Requieren revisión</small>
              <strong>{datos?.resumen.porRevisar ?? 0}</strong>
              <span>más de 180 días</span>
            </div>
          </button>
          <button onClick={() => ticketsRef.current?.scrollIntoView({ behavior: 'smooth' })}>
            <span className="conocimiento-ti-metrica__icono conocimiento-ti-metrica__icono--amarillo">
              <Icono nombre="ticket" />
            </span>
            <div>
              <small>Tickets candidatos</small>
              <strong>{datos?.resumen.candidatosDesdeTickets ?? 0}</strong>
              <span>resueltos sin artículo</span>
            </div>
          </button>
        </section>

        <section className="conocimiento-ti-grid">
          <div className="conocimiento-ti-columna-principal">
            <article className="conocimiento-ti-panel conocimiento-ti-buscador-panel">
              <div className="conocimiento-ti-panel__cabecera">
                <div>
                  <span className="conocimiento-ti-panel__icono">
                    <Icono nombre="buscar" />
                  </span>
                  <div>
                    <h2>Buscar en la Base de Conocimiento</h2>
                    <p>Encuentra soluciones por palabra clave, clasificación o estado editorial.</p>
                  </div>
                </div>
                <button type="button" className="conocimiento-ti-boton-secundario" onClick={() => void cargarModulo()}>
                  <Icono nombre="actualizar" size={16} /> Actualizar
                </button>
              </div>
              <div className="conocimiento-ti-busqueda">
                <Icono nombre="buscar" />
                <input
                  ref={buscadorRef}
                  value={busqueda}
                  onChange={e => setBusqueda(e.target.value)}
                  placeholder="Buscar por título, problema, solución, error o código..."
                />
                <button type="button" onClick={irAArticulos}>
                  Buscar <Icono nombre="flecha" size={15} />
                </button>
              </div>
              <div className="conocimiento-ti-filtros">
                <label>
                  Línea
                  <select value={filtroLinea} onChange={e => setFiltroLinea(e.target.value)}>
                    <option value="">Todas</option>
                    {datos?.catalogos.lineas.map(x => (
                      <option key={x.codigo} value={x.codigo}>
                        {x.descripcion}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  Categoría
                  <select value={filtroCategoria} onChange={e => setFiltroCategoria(e.target.value)}>
                    <option value="">Todas</option>
                    {datos?.catalogos.categorias.map(x => (
                      <option key={x.codigo} value={x.codigo}>
                        {x.descripcion}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  Orden
                  <select value={orden} onChange={e => setOrden(e.target.value as Orden)}>
                    <option value="RECIENTES">Más recientes</option>
                    <option value="TITULO">Título A-Z</option>
                  </select>
                </label>
                <button type="button" onClick={limpiarFiltros}>
                  Limpiar filtros
                </button>
              </div>
              {categoriasPopulares.length > 0 && (
                <div className="conocimiento-ti-categorias">
                  <strong>Categorías con contenido</strong>
                  <div>
                    {categoriasPopulares.map(([codigo, info]) => (
                      <button
                        key={codigo}
                        className={filtroCategoria === codigo ? 'activo' : ''}
                        onClick={() => setFiltroCategoria(filtroCategoria === codigo ? '' : codigo)}
                      >
                        <Icono nombre="categoria" size={15} />
                        {info.descripcion}
                        <span>{info.cantidad}</span>
                      </button>
                    ))}
                  </div>
                </div>
              )}
            </article>

            <section className="conocimiento-ti-articulos" ref={articulosRef}>
              <div className="conocimiento-ti-tabs">
                <button className={filtroEstado === 'TODOS' ? 'activo' : ''} onClick={() => setFiltroEstado('TODOS')}>
                  Todos ({datos?.resumen.total ?? 0})
                </button>
                <button className={filtroEstado === 'A' ? 'activo' : ''} onClick={() => setFiltroEstado('A')}>
                  Publicados ({datos?.resumen.activos ?? 0})
                </button>
                <button className={filtroEstado === 'P' ? 'activo' : ''} onClick={() => setFiltroEstado('P')}>
                  Pendientes ({datos?.resumen.pendientesValidacion ?? 0})
                </button>
                <button className={filtroEstado === 'B' ? 'activo' : ''} onClick={() => setFiltroEstado('B')}>
                  Borradores ({datos?.resumen.borradores ?? 0})
                </button>
                <button className={filtroEstado === 'I' ? 'activo' : ''} onClick={() => setFiltroEstado('I')}>
                  Inactivos
                </button>
              </div>
              {cargando ? (
                <div className="conocimiento-ti-vacio">Cargando artículos...</div>
              ) : articulosFiltrados.length === 0 ? (
                <div className="conocimiento-ti-vacio">
                  <Icono nombre="libro" size={24} />
                  <strong>No hay artículos con los filtros actuales.</strong>
                  <span>Ajusta la búsqueda o crea un nuevo borrador.</span>
                </div>
              ) : (
                <div className="conocimiento-ti-tarjetas">
                  {articulosFiltrados.map(articulo => (
                    <TarjetaArticulo
                      key={articulo.conocimientoCodigo}
                      articulo={articulo}
                      seleccionado={seleccionado === articulo.conocimientoCodigo}
                      onSeleccionar={() => void cargarDetalle(articulo.conocimientoCodigo)}
                    />
                  ))}
                </div>
              )}
              <div className="conocimiento-ti-articulos__pie">
                <span>
                  <strong>{articulosFiltrados.length}</strong> artículos visibles
                </span>
                {(busqueda || filtroLinea || filtroCategoria || filtroEstado !== 'TODOS') && (
                  <button onClick={limpiarFiltros}>Quitar filtros</button>
                )}
              </div>
            </section>
          </div>

          <aside className="conocimiento-ti-columna-derecha">
            <article className="conocimiento-ti-panel conocimiento-ti-detalle">
              <div className="conocimiento-ti-panel__cabecera">
                <div>
                  <span className="conocimiento-ti-panel__icono">
                    <Icono nombre="libro" />
                  </span>
                  <div>
                    <h2>Detalle del artículo</h2>
                    <p>Contenido y control editorial.</p>
                  </div>
                </div>
              </div>
              {cargandoDetalle ? (
                <div className="conocimiento-ti-vacio">Cargando detalle...</div>
              ) : !detalle ? (
                <div className="conocimiento-ti-vacio">Selecciona un artículo para revisar su contenido.</div>
              ) : (
                <DetalleArticulo
                  detalle={detalle}
                  procesando={procesando}
                  onEditar={abrirEditar}
                  onEnviar={() =>
                    void ejecutarEstado(
                      () => enviarValidacionBaseConocimientoTI(detalle.conocimientoCodigo),
                      'El borrador fue enviado a validación.',
                    )
                  }
                  onValidar={() =>
                    void ejecutarEstado(
                      () => validarBaseConocimientoTI(detalle.conocimientoCodigo),
                      detalle.estado === 'A' ? 'La revisión periódica quedó registrada.' : 'El artículo fue validado y publicado.',
                    )
                  }
                  onInactivar={() => void manejarInactivacion()}
                />
              )}
            </article>

            <article className="conocimiento-ti-panel conocimiento-ti-tickets" ref={ticketsRef}>
              <div className="conocimiento-ti-panel__cabecera">
                <div>
                  <span className="conocimiento-ti-panel__icono">
                    <Icono nombre="ticket" />
                  </span>
                  <div>
                    <h2>Convertir experiencia en conocimiento</h2>
                    <p>Tickets resueltos que todavía no tienen artículo asociado.</p>
                  </div>
                </div>
              </div>
              {(datos?.catalogos.ticketsOrigen.length ?? 0) === 0 ? (
                <div className="conocimiento-ti-vacio conocimiento-ti-vacio--compacto">
                  <Icono nombre="check" />
                  <span>No hay tickets resueltos pendientes de documentar.</span>
                </div>
              ) : (
                <div className="conocimiento-ti-ticket-lista">
                  {datos?.catalogos.ticketsOrigen.slice(0, 4).map(ticket => (
                    <button key={ticket.incidenciaNumero} onClick={() => abrirNuevo(ticket)}>
                      <span className="conocimiento-ti-ticket-lista__icono">
                        <Icono nombre="ticket" size={17} />
                      </span>
                      <div>
                        <strong>{ticket.incidenciaNumero}</strong>
                        <span>{ticket.titulo}</span>
                        <small>{fechaCorta(ticket.fechaCierre)}</small>
                      </div>
                      <Icono nombre="flecha" size={15} />
                    </button>
                  ))}
                </div>
              )}
              <button className="conocimiento-ti-ir-gestion" type="button" onClick={() => navigate('/gestion-tickets')}>
                Ir a Gestión de Tickets <Icono nombre="flecha" size={15} />
              </button>
            </article>
          </aside>
        </section>
      </main>
    </MarcoPortal>
  )
}

function solicitudDesdeTicket(ticket: BaseConocimientoTITicketOrigen): GuardarBaseConocimientoTISolicitud {
  return {
    titulo: ticket.titulo,
    problema: ticket.detalle,
    sintomas: ticket.mensajeError
      ? `Mensaje reportado: ${ticket.mensajeError}`
      : 'Síntomas descritos en el ticket de origen; revisar y completar antes de validar.',
    mensajeError: ticket.mensajeError || null,
    causa: ticket.causaRaiz || 'Completar la causa confirmada durante la atención.',
    solucion: ticket.solucionTecnica,
    procedimiento: null,
    linea: ticket.linea,
    item: ticket.item,
    tipo: ticket.tipo,
    subTipo: ticket.subTipo,
    categoria: ticket.categoria,
    incidenciaOrigen: ticket.incidenciaNumero,
    guia: [],
  }
}

function TarjetaArticulo({
  articulo,
  seleccionado,
  onSeleccionar,
}: {
  articulo: BaseConocimientoTIItem
  seleccionado: boolean
  onSeleccionar: () => void
}) {
  return (
    <article className={`conocimiento-ti-tarjeta ${seleccionado ? 'seleccionada' : ''}`} onClick={onSeleccionar}>
      <div className="conocimiento-ti-tarjeta__encabezado">
        <span className="conocimiento-ti-tarjeta__icono">
          <Icono nombre={articulo.estado === 'B' ? 'borrador' : articulo.requiereRevision ? 'alerta' : 'archivo'} />
        </span>
        <span className={estadoClase(articulo.estado)}>{articulo.estadoDescripcion}</span>
      </div>
      <strong>{articulo.titulo}</strong>
      <p>{articulo.problema}</p>
      <div className="conocimiento-ti-tarjeta__tags">
        <span>{articulo.itemDescripcion || articulo.lineaDescripcion}</span>
        <span>{articulo.categoriaDescripcion}</span>
        {articulo.incidenciaOrigen && <span>{articulo.incidenciaOrigen}</span>}
      </div>
      <footer>
        <small>
          {articulo.requiereRevision
            ? 'Revisión vencida'
            : `Revisado ${fechaCorta(articulo.fechaRevision || articulo.fechaValidacion || articulo.fechaCreacion)}`}
        </small>
        <button
          type="button"
          onClick={e => {
            e.stopPropagation()
            onSeleccionar()
          }}
        >
          Ver artículo <Icono nombre="flecha" size={14} />
        </button>
      </footer>
    </article>
  )
}

function DetalleArticulo({
  detalle,
  procesando,
  onEditar,
  onEnviar,
  onValidar,
  onInactivar,
}: {
  detalle: BaseConocimientoTIDetalle
  procesando: boolean
  onEditar: () => void
  onEnviar: () => void
  onValidar: () => void
  onInactivar: () => void
}) {
  return (
    <div className="conocimiento-ti-detalle__contenido">
      <div className="conocimiento-ti-detalle__titulo">
        <div>
          <strong>{detalle.conocimientoCodigo}</strong>
          <span className={estadoClase(detalle.estado)}>{detalle.estadoDescripcion}</span>
        </div>
        <h3>{detalle.titulo}</h3>
        <div className="conocimiento-ti-detalle__tags">
          <span>{detalle.itemDescripcion || detalle.lineaDescripcion}</span>
          <span>{detalle.categoriaDescripcion}</span>
        </div>
      </div>
      {detalle.requiereRevision && (
        <div className="conocimiento-ti-revision">
          <Icono nombre="alertaTrianguloFino" size={17} />
          <span>Este artículo superó 180 días sin revisión. Confirma que la solución continúa vigente.</span>
        </div>
      )}
      <dl className="conocimiento-ti-datos">
        <div>
          <dt>Tipo</dt>
          <dd>{detalle.tipoDescripcion}</dd>
        </div>
        <div>
          <dt>Subtipo</dt>
          <dd>{detalle.subTipoDescripcion}</dd>
        </div>
        <div>
          <dt>Validado por</dt>
          <dd>{detalle.validador || 'Pendiente'}</dd>
        </div>
        <div>
          <dt>Última revisión</dt>
          <dd>{fechaHora(detalle.fechaRevision || detalle.fechaValidacion)}</dd>
        </div>
        {detalle.incidenciaOrigen && (
          <div>
            <dt>Ticket origen</dt>
            <dd>{detalle.incidenciaOrigen}</dd>
          </div>
        )}
      </dl>
      <div className="conocimiento-ti-contenido-scroll">
        <Bloque titulo="Problema" texto={detalle.problema} />
        <Bloque titulo="Síntomas" texto={detalle.sintomas} />
        {detalle.mensajeError && <Bloque titulo="Mensaje de error" texto={detalle.mensajeError} alerta />}
        <Bloque titulo="Causa" texto={detalle.causa} />
        <Bloque titulo="Solución" texto={detalle.solucion} />
        {detalle.procedimiento && <Bloque titulo="Procedimiento" texto={detalle.procedimiento} />}
        {(detalle.guia ?? []).length > 0 && (
          <section className="conocimiento-ti-bloque">
            <strong>Guía de diagnóstico</strong>
            <ol className="conocimiento-ti-guia-vista">
              {detalle.guia.map((x, indice) => (
                <li key={indice}>
                  <p>{x.paso}</p>
                  {x.herramienta && <small>Herramienta: {x.herramienta}</small>}
                  {x.confirma && <small>Confirma: {x.confirma}</small>}
                  {x.descarta && <small>Descarta: {x.descarta}</small>}
                </li>
              ))}
            </ol>
          </section>
        )}
      </div>
      <div className="conocimiento-ti-detalle__acciones">
        <button onClick={onEditar} disabled={procesando}>
          <Icono nombre="editar" size={16} />
          {detalle.estado === 'I' ? 'Editar como borrador' : 'Editar'}
        </button>
        {detalle.estado === 'B' && (
          <button className="principal" onClick={onEnviar} disabled={procesando}>
            <Icono nombre="revisar" size={16} />
            Enviar a validación
          </button>
        )}
        {detalle.estado === 'P' && (
          <button className="principal" onClick={onValidar} disabled={procesando}>
            <Icono nombre="validar" size={16} />
            Validar y publicar
          </button>
        )}
        {detalle.estado === 'A' && detalle.requiereRevision && (
          <button className="principal" onClick={onValidar} disabled={procesando}>
            <Icono nombre="check" size={16} />
            Marcar revisado
          </button>
        )}
        {detalle.estado !== 'I' && (
          <button className="peligro" onClick={onInactivar} disabled={procesando}>
            <Icono nombre="inactivar" size={16} />
            Inactivar
          </button>
        )}
      </div>
    </div>
  )
}

function Bloque({ titulo, texto, alerta = false }: { titulo: string; texto: string; alerta?: boolean }) {
  return (
    <section className={`conocimiento-ti-bloque ${alerta ? 'conocimiento-ti-bloque--alerta' : ''}`}>
      <strong>{titulo}</strong>
      <p>{texto}</p>
    </section>
  )
}
