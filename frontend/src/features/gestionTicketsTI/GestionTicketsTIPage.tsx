/**
 * Archivo: GestionTicketsTIPage.tsx
 * Objetivo: Implementar el módulo Gestión de Tickets para el operador TI autenticado.
 * Responsabilidad: Mostrar bandeja, detalle (con la ficha del colaborador) y flujo completo de clasificación (con propuesta opcional de la IA),
 *   asignación, avances con esfuerzo, recopilación, aprobación, resolución, No Procede y registro por mesa de ayuda.
 * Dependencias: AutenticacionContext, gestionTicketsTIApi, gestionOperativaTIApi, asistenteTIApi, MarcoPortal, Icono, FichaRegistrada,
 *   PanelClasificacionIA y GestionTicketsTIPage.css.
 * Flujo: /gestion-tickets -> Gestión TI -> endpoints protegidos -> acciones controladas -> recarga de bandeja/detalle.
 * Consideraciones: Prioridad/impacto/complejidad salen de la matriz configurable; las aprobaciones bloquean el ticket mientras están pendientes.
 *   La IA solo propone la clasificación: TI la revisa y la guarda; esta pantalla no ejecuta acciones automatizadas.
 */

import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { useAutenticacion } from '../autenticacion/AutenticacionContext'
import {
  asignarTicketTI,
  clasificarTicketTI,
  marcarNoProcedeTI,
  obtenerDetalleGestionTicketTI,
  obtenerFichaTI,
  obtenerGestionTicketsTI,
  resolverTicketTI,
  responderAprobacionTI,
  solicitarInformacionTI,
  urlAdjuntoGestionTI,
  type ClasificarTicketTISolicitud,
  type GestionTicketTIDetalle,
  type GestionTicketsTIRespuesta,
} from '../../services/gestionTicketsTIApi'
import {
  crearTicketMesaAyudaTI,
  obtenerDatosGestionOperativaTI,
  registrarAvanceDetalladoTI,
  solicitarAprobacionOperativaTI,
  type GestionOperativaTIDatos,
} from '../../services/gestionOperativaTIApi'
import { listarInvestigacionesTicketTI, obtenerInformeTI, type AgenteTIInvestigacionTicket } from '../../services/asistenteTIApi'
import type { DatoFichaTicket } from '../../services/fichaTicketService'
import MarcoPortal from '../../components/MarcoPortal'
import IconoBase, { type PropsIcono } from '../../components/Icono'
import FichaRegistrada from '../../components/FichaRegistrada'
import PanelClasificacionIA from './PanelClasificacionIA'
import './GestionTicketsTIPage.css'

// Esta pantalla dibuja sus íconos a 19 px.
const Icono = (props: PropsIcono) => <IconoBase size={19} {...props} />

type FiltroRapido = 'TODOS' | 'SIN_ASIGNAR' | 'MIOS' | 'PRIORIDAD_ALTA' | 'POR_VENCER'
type VistaProceso = 'ATENCION' | 'ASIGNACION' | 'AUTORIZACION' | 'AVANCES' | 'CONFIRMACION'
type ModalAccion = 'DETALLE' | 'CLASIFICAR' | 'ASIGNAR' | 'AVANCE' | 'APROBACION' | 'INFORMACION' | 'RESOLVER' | 'NO_PROCEDE' | null

function fechaHora(fecha: string) {
  return new Intl.DateTimeFormat('es-PE', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(fecha))
}
function textoPrioridad(valor: number | null) {
  return valor === null ? 'Sin definir' : valor >= 4 ? 'Alta' : valor === 3 ? 'Media' : 'Baja'
}
function clasePrioridad(valor: number | null) {
  return `gestion-ti-badge gestion-ti-badge--${valor === null ? 'neutro' : valor >= 4 ? 'rojo' : valor === 3 ? 'amarillo' : 'verde'}`
}
function claseEstado(estado: string) {
  return `gestion-ti-badge gestion-ti-badge--${estado === 'RS' ? 'verde' : estado === 'RA' ? 'violeta' : estado === 'CA' ? 'neutro' : ['RC', 'PA', 'PV'].includes(estado) ? 'amarillo' : estado === 'NV' ? 'rojo' : 'azul'}`
}
function puedeOperar(estado: string) {
  return !['RS', 'CA', 'PV', 'PA'].includes(estado)
}
function tiempoRelativo(fecha: string) {
  const m = Math.floor((Date.now() - new Date(fecha).getTime()) / 60000)
  if (!Number.isFinite(m) || m < 1) return 'ahora'
  if (m < 60) return `hace ${m} min`
  const h = Math.floor(m / 60)
  if (h < 24) return `hace ${h} ${h === 1 ? 'hora' : 'horas'}`
  const d = Math.floor(h / 24)
  return `hace ${d} ${d === 1 ? 'día' : 'días'}`
}

export default function GestionTicketsTIPage() {
  const navigate = useNavigate()
  const [parametros] = useSearchParams()
  const ticketNotificacion = parametros.get('ticket')?.trim().toUpperCase()
  const { usuario } = useAutenticacion()
  const buscadorRef = useRef<HTMLInputElement>(null)
  const filtrosRef = useRef<HTMLDivElement>(null)
  const solicitudDetalleRef = useRef(0)
  const [datos, setDatos] = useState<GestionTicketsTIRespuesta | null>(null)
  const [operativa, setOperativa] = useState<GestionOperativaTIDatos | null>(null)
  const [detalle, setDetalle] = useState<GestionTicketTIDetalle | null>(null)
  const [seleccionado, setSeleccionado] = useState('')
  const [cargando, setCargando] = useState(true)
  const [cargandoDetalle, setCargandoDetalle] = useState(false)
  const [procesando, setProcesando] = useState(false)
  const [error, setError] = useState('')
  const [mensaje, setMensaje] = useState('')
  const [busqueda, setBusqueda] = useState('')
  const [filtroRapido, setFiltroRapido] = useState<FiltroRapido>('TODOS')
  const [vistaProceso, setVistaProceso] = useState<VistaProceso>('ATENCION')
  const [estado, setEstado] = useState('')
  const [prioridad, setPrioridad] = useState('')
  const [area, setArea] = useState('')
  const [responsable, setResponsable] = useState('')
  const [pagina, setPagina] = useState(1)
  const [modal, setModal] = useState<ModalAccion>(null)
  const [mesaAyudaAbierta, setMesaAyudaAbierta] = useState(false)
  const [textoAccion, setTextoAccion] = useState('')
  const [visibleUsuario, setVisibleUsuario] = useState(false)
  const [responsableAccion, setResponsableAccion] = useState('')
  const [tiempoAvance, setTiempoAvance] = useState(15)
  const [areaCausanteAvance, setAreaCausanteAvance] = useState('')
  const [aprobacion, setAprobacion] = useState({ accionCodigo: '', justificacion: '' })
  const [mesaAyuda, setMesaAyuda] = useState({ usuarioSolicitante: '', linea: '', tipo: '', titulo: '', detalle: '', mensajeError: '' })
  const [clasificacion, setClasificacion] = useState<ClasificarTicketTISolicitud>({
    linea: '',
    item: '',
    tipo: '',
    subTipo: '',
    categoria: '',
    areaCausante: null,
    prioridad: null,
    impacto: null,
    complejidad: null,
  })
  const [resolucion, setResolucion] = useState({ causaRaiz: '', solucion: '', respuestaUsuario: '', tipoResolucion: 'CORRECCION' })

  async function cargarDetalle(numero: string) {
    const solicitud = ++solicitudDetalleRef.current
    setModal(null)
    setSeleccionado(numero)
    setCargandoDetalle(true)
    try {
      const valor = await obtenerDetalleGestionTicketTI(numero)
      if (solicitud === solicitudDetalleRef.current) setDetalle(valor)
      return solicitud === solicitudDetalleRef.current ? valor : null
    } catch (e) {
      if (solicitud === solicitudDetalleRef.current) {
        setDetalle(null)
        setError(e instanceof Error ? e.message : 'No fue posible cargar el detalle.')
      }
      return null
    } finally {
      if (solicitud === solicitudDetalleRef.current) setCargandoDetalle(false)
    }
  }
  async function cargarBandeja(preferido?: string) {
    setCargando(true)
    setError('')
    try {
      const [respuesta, datosOperativos] = await Promise.all([obtenerGestionTicketsTI(), obtenerDatosGestionOperativaTI()])
      setDatos(respuesta)
      setOperativa(datosOperativos)
      const numero = preferido || seleccionado || respuesta.tickets[0]?.incidenciaNumero || ''
      if (numero) await cargarDetalle(numero)
      else {
        solicitudDetalleRef.current++
        setSeleccionado('')
        setDetalle(null)
        setCargandoDetalle(false)
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No fue posible cargar Gestión de Tickets.')
    } finally {
      setCargando(false)
    }
  }

  useEffect(() => {
    void cargarBandeja(ticketNotificacion)
  }, [ticketNotificacion])
  useEffect(() => {
    const manejar = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault()
        buscadorRef.current?.focus()
      }
      if (e.key === 'Escape' && !procesando) {
        setModal(null)
        setMesaAyudaAbierta(false)
      }
    }
    window.addEventListener('keydown', manejar)
    return () => window.removeEventListener('keydown', manejar)
  }, [procesando])
  useEffect(() => {
    if (mesaAyudaAbierta) setError('')
  }, [mesaAyudaAbierta])

  const ticketsFiltrados = useMemo(() => {
    if (!datos || !usuario) return []
    const texto = busqueda.trim().toLowerCase()
    return datos.tickets.filter(ticket => {
      if (vistaProceso === 'ASIGNACION' && ticket.usuarioTI) return false
      if (vistaProceso === 'AUTORIZACION' && !ticket.tieneAprobacionPendiente && ticket.estado !== 'PA') return false
      if (vistaProceso === 'AVANCES' && (!ticket.usuarioTI || ['PA', 'PV', 'RS', 'CA', 'CF', 'NP'].includes(ticket.estado))) return false
      if (vistaProceso === 'CONFIRMACION' && ticket.estado !== 'PV') return false
      if (filtroRapido === 'SIN_ASIGNAR' && ticket.usuarioTI) return false
      if (filtroRapido === 'MIOS' && ticket.usuarioTI !== usuario.usuario) return false
      if (filtroRapido === 'PRIORIDAD_ALTA' && (ticket.prioridad ?? 0) < 4) return false
      if (
        filtroRapido === 'POR_VENCER' &&
        !(ticket.slaMinutosRestantes !== null && ticket.slaMinutosRestantes >= 0 && ticket.slaMinutosRestantes <= 1440)
      )
        return false
      if (estado && ticket.estado !== estado) return false
      if (prioridad === 'ALTA' && (ticket.prioridad ?? 0) < 4) return false
      if (prioridad === 'MEDIA' && ticket.prioridad !== 3) return false
      if (prioridad === 'BAJA' && (ticket.prioridad === null || ticket.prioridad > 2)) return false
      if (area && ticket.areaSolicitante !== area) return false
      if (responsable === 'SIN_ASIGNAR' && ticket.usuarioTI) return false
      if (responsable && responsable !== 'SIN_ASIGNAR' && ticket.usuarioTI !== responsable) return false
      return (
        !texto ||
        `${ticket.incidenciaNumero} ${ticket.solicitante} ${ticket.usuarioSolicitante} ${ticket.titulo} ${ticket.areaDescripcion} ${ticket.estadoDescripcion} ${ticket.responsable}`
          .toLowerCase()
          .includes(texto)
      )
    })
  }, [area, busqueda, datos, estado, filtroRapido, prioridad, responsable, usuario, vistaProceso])
  const ticketsPorPagina = 12
  const totalPaginas = Math.max(1, Math.ceil(ticketsFiltrados.length / ticketsPorPagina))
  const paginaActual = Math.min(pagina, totalPaginas)
  const ticketsPagina = ticketsFiltrados.slice((paginaActual - 1) * ticketsPorPagina, paginaActual * ticketsPorPagina)
  const primeroVisible = ticketsFiltrados.length === 0 ? 0 : (paginaActual - 1) * ticketsPorPagina + 1
  const ultimoVisible = Math.min(paginaActual * ticketsPorPagina, ticketsFiltrados.length)
  const paginasVisibles = Array.from({ length: totalPaginas }, (_, indice) => indice + 1).filter(
    numero => totalPaginas <= 5 || numero === 1 || numero === totalPaginas || Math.abs(numero - paginaActual) <= 1,
  )
  const conteosProceso = useMemo(() => {
    const tickets = datos?.tickets ?? []
    return {
      ATENCION: tickets.length,
      ASIGNACION: tickets.filter(x => !x.usuarioTI).length,
      AUTORIZACION: tickets.filter(x => x.tieneAprobacionPendiente || x.estado === 'PA').length,
      AVANCES: tickets.filter(x => Boolean(x.usuarioTI) && !['PA', 'PV', 'RS', 'CA', 'CF', 'NP'].includes(x.estado)).length,
      CONFIRMACION: tickets.filter(x => x.estado === 'PV').length,
    }
  }, [datos])

  useEffect(() => setPagina(1), [area, busqueda, estado, filtroRapido, prioridad, responsable, vistaProceso])

  if (!usuario) return null
  const usuarioActual = usuario.usuario

  function limpiarFiltros() {
    setBusqueda('')
    setFiltroRapido('TODOS')
    setEstado('')
    setPrioridad('')
    setArea('')
    setResponsable('')
  }
  function cambiarVistaProceso(vista: VistaProceso) {
    setVistaProceso(vista)
    limpiarFiltros()
  }
  function abrirModal(tipo: Exclude<ModalAccion, null>) {
    if (!detalle) return
    setError('')
    setTextoAccion('')
    setVisibleUsuario(false)
    setResponsableAccion(detalle.usuarioTI || usuarioActual)
    setTiempoAvance(15)
    setAreaCausanteAvance(detalle.areaCausante || '')
    setAprobacion({ accionCodigo: operativa?.accionesAprobacion[0]?.accionCodigo || '', justificacion: '' })
    setClasificacion({
      linea: detalle.linea,
      item: detalle.item,
      tipo: detalle.tipo,
      subTipo: detalle.subTipo,
      categoria: detalle.categoria,
      areaCausante: detalle.areaCausante || null,
      prioridad: null,
      impacto: null,
      complejidad: null,
    })
    setResolucion({
      causaRaiz: detalle.causaRaiz,
      solucion: detalle.solucionTecnica,
      respuestaUsuario:
        detalle.respuestaUsuario ||
        'Se completó la atención. Por favor vuelve a realizar el proceso y confirma si el inconveniente fue solucionado.',
      tipoResolucion: detalle.tipoResolucion || 'CORRECCION',
    })
    setModal(tipo)
  }
  async function ejecutar(accion: () => Promise<void>, exito: string) {
    if (!detalle) return
    setProcesando(true)
    setError('')
    setMensaje('')
    try {
      await accion()
      setModal(null)
      setMensaje(exito)
      await cargarBandeja(detalle.incidenciaNumero)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No fue posible completar la operación.')
    } finally {
      setProcesando(false)
    }
  }
  async function registrarMesaAyuda() {
    setProcesando(true)
    setError('')
    setMensaje('')
    try {
      const creado = await crearTicketMesaAyudaTI(mesaAyuda)
      setMesaAyudaAbierta(false)
      setMensaje(`Se registró ${creado.incidenciaNumero} a nombre del usuario.`)
      setMesaAyuda({ usuarioSolicitante: '', linea: '', tipo: '', titulo: '', detalle: '', mensajeError: '' })
      await cargarBandeja(creado.incidenciaNumero)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No fue posible registrar el ticket.')
    } finally {
      setProcesando(false)
    }
  }
  function exportarCsv() {
    const filas = [
      ['Ticket', 'Solicitante', 'Título', 'Área', 'Prioridad', 'Estado', 'Responsable', 'Última actualización'],
      ...ticketsFiltrados.map(x => [
        x.incidenciaNumero,
        x.solicitante,
        x.titulo,
        x.areaDescripcion,
        textoPrioridad(x.prioridad),
        x.estadoDescripcion,
        x.responsable,
        fechaHora(x.ultimaFechaModif),
      ]),
    ]
    const contenido = filas.map(f => f.map(v => `"${String(v).replaceAll('"', '""')}"`).join(',')).join('\n')
    const url = URL.createObjectURL(new Blob([`\uFEFF${contenido}`], { type: 'text/csv;charset=utf-8' }))
    const a = document.createElement('a')
    a.href = url
    a.download = `gestion-tickets-${new Date().toISOString().slice(0, 10)}.csv`
    a.click()
    URL.revokeObjectURL(url)
  }

  return (
    <MarcoPortal
      menu="ti"
      activa="gestion"
      clase="gestion-ti-page"
      barra={
        <label className="inicio-buscador">
          <Icono nombre="buscar" />
          <input
            ref={buscadorRef}
            value={busqueda}
            onChange={e => setBusqueda(e.target.value)}
            placeholder="Buscar tickets, usuarios o títulos..."
          />
          <span>Ctrl + K</span>
        </label>
      }
      capas={
        <>
          {modal && detalle && (
            <div
              className="gestion-ti-modal-fondo"
              onMouseDown={e => {
                if (e.target === e.currentTarget && !procesando) setModal(null)
              }}
            >
              <section
                className={`gestion-ti-modal ${modal === 'DETALLE' ? 'gestion-ti-modal--grande' : ''}`}
                role="dialog"
                aria-modal="true"
              >
                <header>
                  <div>
                    <span>{detalle.incidenciaNumero}</span>
                    <h2>{tituloModal(modal, detalle.usuarioTI.length > 0)}</h2>
                  </div>
                  <button onClick={() => setModal(null)} disabled={procesando}>
                    <Icono nombre="cerrar" />
                  </button>
                </header>
                {error && (
                  <div className="gestion-ti-alerta gestion-ti-alerta--error gestion-ti-modal__alerta" role="alert">
                    <span>{error}</span>
                  </div>
                )}
                {modal === 'DETALLE' && (
                  <DetalleCompleto
                    detalle={detalle}
                    onAccion={abrirModal}
                    usuarioActual={usuarioActual}
                    onAprobar={(secuencia, aprobar) =>
                      void ejecutar(
                        () =>
                          responderAprobacionTI(
                            detalle.incidenciaNumero,
                            secuencia,
                            aprobar,
                            aprobar ? 'Aprobado por el responsable autorizado.' : 'Rechazado por el responsable autorizado.',
                          ),
                        aprobar ? 'La acción fue aprobada.' : 'La acción fue rechazada.',
                      )
                    }
                  />
                )}
                {modal === 'CLASIFICAR' && datos && (
                  <form
                    onSubmit={e => {
                      e.preventDefault()
                      void ejecutar(
                        () => clasificarTicketTI(detalle.incidenciaNumero, clasificacion),
                        'La clasificación técnica fue actualizada.',
                      )
                    }}
                  >
                    <PanelClasificacionIA
                      incidenciaNumero={detalle.incidenciaNumero}
                      onAplicar={propuesta => setClasificacion(v => ({ ...v, ...propuesta }))}
                    />
                    <div className="gestion-ti-form-grid">
                      <label>
                        Línea
                        <select
                          required
                          value={clasificacion.linea}
                          onChange={e => setClasificacion(v => ({ ...v, linea: e.target.value, item: '' }))}
                        >
                          {datos.catalogos.lineas.map(x => (
                            <option key={x.codigo} value={x.codigo}>
                              {x.descripcion}
                            </option>
                          ))}
                        </select>
                      </label>
                      <label>
                        Item
                        <select required value={clasificacion.item} onChange={e => setClasificacion(v => ({ ...v, item: e.target.value }))}>
                          <option value="">Seleccionar</option>
                          {datos.catalogos.items
                            .filter(x => x.linea === clasificacion.linea)
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
                          value={clasificacion.tipo}
                          onChange={e => setClasificacion(v => ({ ...v, tipo: e.target.value, subTipo: '' }))}
                        >
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
                          value={clasificacion.categoria}
                          onChange={e => setClasificacion(v => ({ ...v, categoria: e.target.value, subTipo: '' }))}
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
                        <select
                          required
                          value={clasificacion.subTipo}
                          onChange={e => setClasificacion(v => ({ ...v, subTipo: e.target.value }))}
                        >
                          <option value="">Seleccionar</option>
                          {datos.catalogos.subTipos
                            .filter(x => x.tipo === clasificacion.tipo && x.categoria === clasificacion.categoria)
                            .map(x => (
                              <option key={`${x.tipo}-${x.categoria}-${x.codigo}`} value={x.codigo}>
                                {x.descripcion}
                              </option>
                            ))}
                        </select>
                      </label>
                      <label>
                        Área causante
                        <select
                          value={clasificacion.areaCausante ?? ''}
                          onChange={e => setClasificacion(v => ({ ...v, areaCausante: e.target.value || null }))}
                        >
                          <option value="">Sin determinar</option>
                          {datos.catalogos.areas.map(x => (
                            <option key={x.codigo} value={x.codigo}>
                              {x.descripcion}
                            </option>
                          ))}
                        </select>
                      </label>
                    </div>
                    <p className="gestion-ti-nota">
                      <strong>Prioridad, impacto y complejidad</strong> se aplicarán automáticamente desde la matriz Ítem/Categoría
                      configurada para evitar criterios manuales inconsistentes.
                    </p>
                    <PieModal procesando={procesando} texto="Guardar clasificación" onCancelar={() => setModal(null)} />
                  </form>
                )}
                {modal === 'ASIGNAR' && datos && (
                  <form
                    onSubmit={e => {
                      e.preventDefault()
                      void ejecutar(
                        () => asignarTicketTI(detalle.incidenciaNumero, responsableAccion),
                        detalle.usuarioTI ? 'El ticket fue reasignado.' : 'El ticket fue asignado.',
                      )
                    }}
                  >
                    <label className="gestion-ti-campo">
                      Responsable TI
                      <select required value={responsableAccion} onChange={e => setResponsableAccion(e.target.value)}>
                        <option value="">Seleccionar operador</option>
                        {datos.catalogos.operadores.map(x => (
                          <option key={x.codigo} value={x.codigo}>
                            {x.descripcion}
                          </option>
                        ))}
                      </select>
                      <small>El responsable quedará registrado junto con quien realizó la asignación.</small>
                    </label>
                    <PieModal
                      procesando={procesando}
                      texto={detalle.usuarioTI ? 'Reasignar' : 'Asignar'}
                      onCancelar={() => setModal(null)}
                    />
                  </form>
                )}
                {modal === 'AVANCE' && datos && (
                  <form
                    onSubmit={e => {
                      e.preventDefault()
                      void ejecutar(
                        () =>
                          registrarAvanceDetalladoTI(detalle.incidenciaNumero, {
                            detalle: textoAccion,
                            visibleUsuario,
                            tiempoUtilizadoMinutos: tiempoAvance,
                            areaCausante: areaCausanteAvance,
                          }),
                        'El avance y el esfuerzo efectivo fueron registrados.',
                      )
                    }}
                  >
                    <label className="gestion-ti-campo">
                      Detalle del avance
                      <textarea
                        required
                        minLength={5}
                        maxLength={4000}
                        value={textoAccion}
                        onChange={e => setTextoAccion(e.target.value)}
                        placeholder="Describe la validación o trabajo realizado..."
                      />
                    </label>
                    <div className="gestion-ti-form-grid">
                      <label>
                        Tiempo efectivo (min)
                        <input
                          type="number"
                          min="1"
                          max="1440"
                          required
                          value={tiempoAvance}
                          onChange={e => setTiempoAvance(Number(e.target.value))}
                        />
                      </label>
                      <label>
                        Área causante
                        <select required value={areaCausanteAvance} onChange={e => setAreaCausanteAvance(e.target.value)}>
                          <option value="">Seleccionar</option>
                          {datos.catalogos.areas.map(x => (
                            <option key={x.codigo} value={x.codigo}>
                              {x.descripcion}
                            </option>
                          ))}
                        </select>
                      </label>
                    </div>
                    <label className="gestion-ti-check">
                      <input type="checkbox" checked={visibleUsuario} onChange={e => setVisibleUsuario(e.target.checked)} />
                      <span>Mostrar este avance también al usuario solicitante</span>
                    </label>
                    <PieModal procesando={procesando} texto="Registrar avance" onCancelar={() => setModal(null)} />
                  </form>
                )}
                {modal === 'APROBACION' && (
                  <form
                    onSubmit={e => {
                      e.preventDefault()
                      void ejecutar(
                        () => solicitarAprobacionOperativaTI(detalle.incidenciaNumero, aprobacion),
                        'La aprobación fue solicitada y el ticket quedó bloqueado hasta su respuesta.',
                      )
                    }}
                  >
                    <label className="gestion-ti-campo">
                      Acción a aprobar
                      <select
                        required
                        value={aprobacion.accionCodigo}
                        onChange={e => setAprobacion(v => ({ ...v, accionCodigo: e.target.value }))}
                      >
                        <option value="">Seleccionar</option>
                        {operativa?.accionesAprobacion.map(x => (
                          <option key={x.accionCodigo} value={x.accionCodigo}>
                            {x.nombre} · Riesgo {x.nivelRiesgo}
                          </option>
                        ))}
                      </select>
                    </label>
                    <label className="gestion-ti-campo">
                      Justificación
                      <textarea
                        required
                        minLength={10}
                        maxLength={1000}
                        value={aprobacion.justificacion}
                        onChange={e => setAprobacion(v => ({ ...v, justificacion: e.target.value }))}
                        placeholder="Explica por qué esta acción requiere autorización."
                      />
                    </label>
                    <p className="gestion-ti-nota gestion-ti-nota--alerta">
                      Mientras la solicitud esté pendiente, clasificación, avances, resolución y otras acciones del ticket quedarán
                      bloqueadas.
                    </p>
                    <PieModal procesando={procesando} texto="Solicitar aprobación" onCancelar={() => setModal(null)} />
                  </form>
                )}
                {modal === 'INFORMACION' && (
                  <form
                    onSubmit={e => {
                      e.preventDefault()
                      void ejecutar(
                        () => solicitarInformacionTI(detalle.incidenciaNumero, textoAccion),
                        'Se solicitó información adicional al usuario.',
                      )
                    }}
                  >
                    <label className="gestion-ti-campo">
                      Información requerida
                      <textarea
                        required
                        minLength={10}
                        maxLength={1000}
                        value={textoAccion}
                        onChange={e => setTextoAccion(e.target.value)}
                        placeholder="Indica exactamente qué información o evidencia necesitas."
                      />
                    </label>
                    <p className="gestion-ti-nota">
                      El ticket pasará a <strong>En recopilación</strong>. La respuesta del usuario lo devolverá a diagnóstico.
                    </p>
                    <PieModal procesando={procesando} texto="Solicitar información" onCancelar={() => setModal(null)} />
                  </form>
                )}
                {modal === 'RESOLVER' && (
                  <form
                    onSubmit={e => {
                      e.preventDefault()
                      void ejecutar(
                        () => resolverTicketTI(detalle.incidenciaNumero, resolucion),
                        'La solución fue enviada al usuario para validación.',
                      )
                    }}
                  >
                    <div className="gestion-ti-form-vertical">
                      <label>
                        Causa raíz
                        <textarea
                          required
                          minLength={5}
                          maxLength={4000}
                          value={resolucion.causaRaiz}
                          onChange={e => setResolucion(v => ({ ...v, causaRaiz: e.target.value }))}
                        />
                      </label>
                      <label>
                        Solución aplicada
                        <textarea
                          required
                          minLength={5}
                          maxLength={4000}
                          value={resolucion.solucion}
                          onChange={e => setResolucion(v => ({ ...v, solucion: e.target.value }))}
                        />
                      </label>
                      <label>
                        Respuesta al usuario
                        <textarea
                          required
                          minLength={5}
                          maxLength={4000}
                          value={resolucion.respuestaUsuario}
                          onChange={e => setResolucion(v => ({ ...v, respuestaUsuario: e.target.value }))}
                        />
                      </label>
                      <label>
                        Tipo de resolución
                        <select
                          value={resolucion.tipoResolucion}
                          onChange={e => setResolucion(v => ({ ...v, tipoResolucion: e.target.value }))}
                        >
                          <option value="CORRECCION">Corrección</option>
                          <option value="CONFIGURACION">Configuración</option>
                          <option value="GUIA">Guía</option>
                          <option value="REPROCESO">Reproceso</option>
                        </select>
                      </label>
                    </div>
                    <p className="gestion-ti-nota">
                      El ticket pasará a <strong>Pendiente de validación</strong>. Solo el usuario confirmará su cierre.
                    </p>
                    <PieModal procesando={procesando} texto="Enviar a validación" onCancelar={() => setModal(null)} />
                  </form>
                )}
                {modal === 'NO_PROCEDE' && (
                  <form
                    onSubmit={e => {
                      e.preventDefault()
                      void ejecutar(
                        () => marcarNoProcedeTI(detalle.incidenciaNumero, textoAccion),
                        'El ticket fue marcado como No Procede.',
                      )
                    }}
                  >
                    <label className="gestion-ti-campo">
                      Motivo
                      <textarea
                        required
                        minLength={10}
                        maxLength={1000}
                        value={textoAccion}
                        onChange={e => setTextoAccion(e.target.value)}
                        placeholder="Explica de forma clara por qué el ticket no procede..."
                      />
                    </label>
                    <p className="gestion-ti-nota gestion-ti-nota--alerta">
                      El motivo será visible para el usuario y quedará registrado en historial y auditoría.
                    </p>
                    <PieModal procesando={procesando} texto="Confirmar No Procede" onCancelar={() => setModal(null)} peligro />
                  </form>
                )}
              </section>
            </div>
          )}

          {mesaAyudaAbierta && (
            <div
              className="gestion-ti-modal-fondo"
              onMouseDown={e => {
                if (e.target === e.currentTarget && !procesando) setMesaAyudaAbierta(false)
              }}
            >
              <section className="gestion-ti-modal" role="dialog" aria-modal="true">
                <header>
                  <div>
                    <span>Mesa de ayuda</span>
                    <h2>Registrar ticket por otro usuario</h2>
                  </div>
                  <button onClick={() => setMesaAyudaAbierta(false)} disabled={procesando}>
                    <Icono nombre="cerrar" />
                  </button>
                </header>
                {error && (
                  <div className="gestion-ti-alerta gestion-ti-alerta--error gestion-ti-modal__alerta" role="alert">
                    <span>{error}</span>
                  </div>
                )}
                <form
                  onSubmit={e => {
                    e.preventDefault()
                    void registrarMesaAyuda()
                  }}
                >
                  <div className="gestion-ti-form-vertical">
                    <label>
                      Usuario solicitante
                      <select
                        required
                        value={mesaAyuda.usuarioSolicitante}
                        onChange={e => setMesaAyuda(v => ({ ...v, usuarioSolicitante: e.target.value }))}
                      >
                        <option value="">Seleccionar usuario</option>
                        {operativa?.usuarios.map(x => (
                          <option key={x.codigo} value={x.codigo}>
                            {x.descripcion} · {x.codigo}
                          </option>
                        ))}
                      </select>
                    </label>
                    <label>
                      Línea
                      <select required value={mesaAyuda.linea} onChange={e => setMesaAyuda(v => ({ ...v, linea: e.target.value }))}>
                        <option value="">Seleccionar</option>
                        {operativa?.lineas.map(x => (
                          <option key={x.codigo} value={x.codigo}>
                            {x.descripcion}
                          </option>
                        ))}
                      </select>
                    </label>
                    <label>
                      Tipo
                      <select required value={mesaAyuda.tipo} onChange={e => setMesaAyuda(v => ({ ...v, tipo: e.target.value }))}>
                        <option value="">Seleccionar</option>
                        {operativa?.tipos.map(x => (
                          <option key={x.codigo} value={x.codigo}>
                            {x.descripcion}
                          </option>
                        ))}
                      </select>
                    </label>
                    <label>
                      Título
                      <input
                        required
                        minLength={5}
                        maxLength={250}
                        value={mesaAyuda.titulo}
                        onChange={e => setMesaAyuda(v => ({ ...v, titulo: e.target.value }))}
                      />
                    </label>
                    <label>
                      Detalle
                      <textarea
                        required
                        minLength={20}
                        maxLength={4000}
                        value={mesaAyuda.detalle}
                        onChange={e => setMesaAyuda(v => ({ ...v, detalle: e.target.value }))}
                        placeholder="Describe el proceso, documentos relacionados y error reportado por el usuario."
                      />
                    </label>
                    <label>
                      Mensaje de error
                      <textarea
                        maxLength={1000}
                        value={mesaAyuda.mensajeError}
                        onChange={e => setMesaAyuda(v => ({ ...v, mensajeError: e.target.value }))}
                      />
                    </label>
                  </div>
                  <p className="gestion-ti-nota">
                    El ticket quedará a nombre del usuario seleccionado y registrará al operador TI como autor de la carga por mesa de
                    ayuda.
                  </p>
                  <PieModal procesando={procesando} texto="Registrar ticket" onCancelar={() => setMesaAyudaAbierta(false)} />
                </form>
              </section>
            </div>
          )}
        </>
      }
    >
      <main className="inicio-contenido gestion-ti-contenido">
        <section className="inicio-hero gestion-ti-hero">
          <div className="inicio-hero__contenido">
            <h1>Gestión de Tickets</h1>
            <p className="inicio-hero__resumen">Revisa, clasifica, asigna y da seguimiento a las incidencias de la organización.</p>
            <p className="inicio-hero__detalle">Una sola bandeja para mantener el contexto, la trazabilidad y la atención técnica.</p>
          </div>
          <div className="inicio-hero__acciones gestion-ti-hero__acciones">
            <button onClick={() => setMesaAyudaAbierta(true)}>
              <Icono nombre="usuarioGrande" /> Registrar por usuario
            </button>
            <button onClick={() => void cargarBandeja()}>
              <Icono nombre="actualizar" /> Actualizar
            </button>
            <button className="inicio-hero__secundario" onClick={() => filtrosRef.current?.scrollIntoView({ behavior: 'smooth' })}>
              <Icono nombre="filtro" /> Filtros
            </button>
          </div>
          <div className="inicio-hero__firma">
            <span>Personas</span>
            <span>que avanzan</span>
            <strong>CALIMOD</strong>
          </div>
        </section>
        {error && (
          <div className="gestion-ti-alerta gestion-ti-alerta--error">
            <span>{error}</span>
            <button onClick={() => setError('')}>
              <Icono nombre="cerrar" size={16} />
            </button>
          </div>
        )}
        {mensaje && (
          <div className="gestion-ti-alerta gestion-ti-alerta--ok">
            <Icono nombre="check" />
            <span>{mensaje}</span>
            <button onClick={() => setMensaje('')}>
              <Icono nombre="cerrar" size={16} />
            </button>
          </div>
        )}
        <section className="gestion-ti-metricas">
          <button
            onClick={() => {
              setFiltroRapido('TODOS')
              setEstado('NV')
            }}
          >
            <span className="gestion-ti-metrica__icono gestion-ti-metrica__icono--turquesa">
              <Icono nombre="carpeta" />
            </span>
            <div>
              <small>Pendientes</small>
              <strong>{datos?.resumen.pendientes ?? 0}</strong>
              <span>requieren revisión</span>
            </div>
          </button>
          <button
            onClick={() => {
              setFiltroRapido('TODOS')
              setEstado('DG')
            }}
          >
            <span className="gestion-ti-metrica__icono gestion-ti-metrica__icono--azul">
              <Icono nombre="proceso" />
            </span>
            <div>
              <small>En atención</small>
              <strong>{datos?.resumen.enAtencion ?? 0}</strong>
              <span>en gestión técnica</span>
            </div>
          </button>
          <button
            onClick={() => {
              setEstado('')
              setFiltroRapido('POR_VENCER')
            }}
          >
            <span className="gestion-ti-metrica__icono gestion-ti-metrica__icono--rojo">
              <Icono nombre="alerta" />
            </span>
            <div>
              <small>Por vencer</small>
              <strong>{datos?.resumen.porVencer ?? 0}</strong>
              <span>próximas 24 horas</span>
            </div>
          </button>
          <button
            onClick={() => {
              setFiltroRapido('TODOS')
              setEstado('RA')
            }}
          >
            <span className="gestion-ti-metrica__icono gestion-ti-metrica__icono--violeta">
              <Icono nombre="reabrir" />
            </span>
            <div>
              <small>Reabiertos</small>
              <strong>{datos?.resumen.reabiertos ?? 0}</strong>
              <span>requieren nueva revisión</span>
            </div>
          </button>
        </section>
        <section className="gestion-ti-grid" ref={filtrosRef}>
          <article className="gestion-ti-panel gestion-ti-bandeja">
            <div className="gestion-ti-panel__cabecera">
              <div>
                <span className="gestion-ti-panel__icono">
                  <Icono nombre="lista" />
                </span>
                <div>
                  <h2>Listado de tickets</h2>
                  <p>Gestiona las incidencias de toda la organización.</p>
                </div>
              </div>
              <button className="gestion-ti-boton-secundario" onClick={exportarCsv} disabled={!ticketsFiltrados.length}>
                <Icono nombre="descargar" size={16} /> Exportar
              </button>
            </div>
            <div className="gestion-ti-tabs">
              <button className={filtroRapido === 'TODOS' ? 'activo' : ''} onClick={() => setFiltroRapido('TODOS')}>
                Todos ({datos?.resumen.total ?? 0})
              </button>
              <button className={filtroRapido === 'SIN_ASIGNAR' ? 'activo' : ''} onClick={() => setFiltroRapido('SIN_ASIGNAR')}>
                Sin asignar ({datos?.resumen.sinAsignar ?? 0})
              </button>
              <button className={filtroRapido === 'MIOS' ? 'activo' : ''} onClick={() => setFiltroRapido('MIOS')}>
                Mis asignados ({datos?.resumen.misAsignados ?? 0})
              </button>
              <button className={filtroRapido === 'PRIORIDAD_ALTA' ? 'activo' : ''} onClick={() => setFiltroRapido('PRIORIDAD_ALTA')}>
                Prioridad alta ({datos?.resumen.prioridadAlta ?? 0})
              </button>
            </div>
            <div className="gestion-ti-filtros">
              <label>
                Estado
                <select value={estado} onChange={e => setEstado(e.target.value)}>
                  <option value="">Todos</option>
                  {datos?.catalogos.estados.map(x => (
                    <option key={x.codigo} value={x.codigo}>
                      {x.descripcion}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Prioridad
                <select value={prioridad} onChange={e => setPrioridad(e.target.value)}>
                  <option value="">Todas</option>
                  <option value="ALTA">Alta</option>
                  <option value="MEDIA">Media</option>
                  <option value="BAJA">Baja</option>
                </select>
              </label>
              <label>
                Área
                <select value={area} onChange={e => setArea(e.target.value)}>
                  <option value="">Todas</option>
                  {datos?.catalogos.areas.map(x => (
                    <option key={x.codigo} value={x.codigo}>
                      {x.descripcion}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Responsable
                <select value={responsable} onChange={e => setResponsable(e.target.value)}>
                  <option value="">Todos</option>
                  <option value="SIN_ASIGNAR">Sin asignar</option>
                  {datos?.catalogos.operadores.map(x => (
                    <option key={x.codigo} value={x.codigo}>
                      {x.descripcion}
                    </option>
                  ))}
                </select>
              </label>
              <button onClick={limpiarFiltros}>Limpiar</button>
            </div>
            <div className="gestion-ti-tabla-wrap">
              <table className="gestion-ti-tabla">
                <thead>
                  <tr>
                    <th>Ticket</th>
                    <th>Usuario</th>
                    <th>Título</th>
                    <th>Área</th>
                    <th>Prioridad</th>
                    <th>Estado</th>
                    <th>Responsable</th>
                    <th>Actualización</th>
                    <th>Acción</th>
                  </tr>
                </thead>
                <tbody>
                  {cargando ? (
                    <tr>
                      <td colSpan={9} className="gestion-ti-tabla__vacio">
                        Cargando tickets...
                      </td>
                    </tr>
                  ) : ticketsFiltrados.length === 0 ? (
                    <tr>
                      <td colSpan={9} className="gestion-ti-tabla__vacio">
                        No hay tickets con los filtros seleccionados.
                      </td>
                    </tr>
                  ) : (
                    ticketsPagina.map(ticket => (
                      <tr
                        key={ticket.incidenciaNumero}
                        className={seleccionado === ticket.incidenciaNumero ? 'seleccionado' : ''}
                        onClick={() => void cargarDetalle(ticket.incidenciaNumero)}
                      >
                        <td>
                          <strong>{ticket.incidenciaNumero}</strong>
                        </td>
                        <td>{ticket.solicitante}</td>
                        <td className="gestion-ti-tabla__titulo">{ticket.titulo}</td>
                        <td>{ticket.areaDescripcion}</td>
                        <td>
                          <span className={clasePrioridad(ticket.prioridad)}>{textoPrioridad(ticket.prioridad)}</span>
                        </td>
                        <td>
                          <span className={claseEstado(ticket.estado)}>{ticket.estadoDescripcion}</span>
                        </td>
                        <td>{ticket.responsable}</td>
                        <td>{tiempoRelativo(ticket.ultimaFechaModif)}</td>
                        <td>
                          <button
                            className="gestion-ti-accion-tabla"
                            onClick={async e => {
                              e.stopPropagation()
                              await cargarDetalle(ticket.incidenciaNumero)
                              setModal('DETALLE')
                            }}
                          >
                            {ticket.accion === 'ASIGNAR' ? 'Asignar' : ticket.accion === 'CONTINUAR' ? 'Continuar' : 'Ver detalle'}
                          </button>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
            <div className="gestion-ti-tabla__pie">
              <span>
                Mostrando{' '}
                <strong>
                  {primeroVisible}-{ultimoVisible}
                </strong>{' '}
                de {ticketsFiltrados.length}
              </span>
              <div className="gestion-ti-paginacion" aria-label="Paginación de tickets">
                <button type="button" onClick={() => setPagina(valor => Math.max(1, valor - 1))} disabled={paginaActual === 1}>
                  Anterior
                </button>
                {paginasVisibles.map((numero, indice) => (
                  <span key={numero} className="gestion-ti-paginacion__grupo">
                    {indice > 0 && numero - paginasVisibles[indice - 1] > 1 && <i>…</i>}
                    <button
                      type="button"
                      className={numero === paginaActual ? 'activo' : ''}
                      onClick={() => setPagina(numero)}
                      aria-current={numero === paginaActual ? 'page' : undefined}
                    >
                      {numero}
                    </button>
                  </span>
                ))}
                <button
                  type="button"
                  onClick={() => setPagina(valor => Math.min(totalPaginas, valor + 1))}
                  disabled={paginaActual === totalPaginas}
                >
                  Siguiente
                </button>
              </div>
              {(busqueda || estado || prioridad || area || responsable || filtroRapido !== 'TODOS') && (
                <button className="gestion-ti-quitar-filtros" onClick={limpiarFiltros}>
                  Quitar filtros
                </button>
              )}
            </div>
          </article>
          <aside className="gestion-ti-columna-derecha">
            <article className="gestion-ti-panel gestion-ti-detalle-rapido">
              <div className="gestion-ti-panel__cabecera">
                <div>
                  <span className="gestion-ti-panel__icono">
                    <Icono nombre="mensaje" />
                  </span>
                  <div>
                    <h2>Detalle rápido del ticket</h2>
                    <p>Contexto esencial para decidir la siguiente acción.</p>
                  </div>
                </div>
              </div>
              {cargandoDetalle ? (
                <div className="gestion-ti-vacio">Cargando detalle...</div>
              ) : !detalle ? (
                <div className="gestion-ti-vacio">Selecciona un ticket de la bandeja.</div>
              ) : (
                <>
                  <div className="gestion-ti-detalle__titulo">
                    <div>
                      <strong>{detalle.incidenciaNumero}</strong>
                      <span className={clasePrioridad(detalle.prioridad)}>{textoPrioridad(detalle.prioridad)}</span>
                    </div>
                    <h3>{detalle.titulo}</h3>
                    <p>{detalle.detalle}</p>
                  </div>
                  <dl className="gestion-ti-datos">
                    <DatoRapido label="Solicitante" valor={detalle.solicitante} />
                    <DatoRapido label="Área" valor={detalle.areaSolicitanteDescripcion} />
                    <DatoRapido label="Clasificación" valor={detalle.itemDescripcion || 'Pendiente'} />
                    <DatoRapido label="Responsable" valor={detalle.responsable} />
                    <DatoRapido label="Estado" valor={detalle.estadoDescripcion} />
                    <DatoRapido
                      label="SLA"
                      valor={
                        detalle.slaMinutosRestantes === null
                          ? 'Sin objetivo'
                          : detalle.slaMinutosRestantes < 0
                            ? `Vencido hace ${Math.abs(detalle.slaMinutosRestantes)} min`
                            : `${detalle.slaMinutosRestantes} min restantes`
                      }
                    />
                  </dl>
                  <div className="gestion-ti-linea-tiempo">
                    {detalle.historialEstados.slice(-4).map((evento, i, arr) => (
                      <div key={evento.secuencia} className={i === arr.length - 1 ? 'actual' : ''}>
                        <span />
                        <div>
                          <strong>{evento.estadoDescripcion}</strong>
                          <small>{fechaHora(evento.fechaCambio)}</small>
                          {evento.observacion && <p>{evento.observacion}</p>}
                        </div>
                      </div>
                    ))}
                  </div>
                  <div className="gestion-ti-acciones-rapidas">
                    {puedeOperar(detalle.estado) && (
                      <>
                        <button onClick={() => abrirModal('CLASIFICAR')}>
                          <Icono nombre="editar" size={16} /> Clasificar
                        </button>
                        <button onClick={() => abrirModal('ASIGNAR')}>
                          <Icono nombre="usuarioGrande" size={16} /> {detalle.usuarioTI ? 'Reasignar' : 'Asignar'}
                        </button>
                        <button onClick={() => abrirModal('AVANCE')}>
                          <Icono nombre="mensaje" size={16} /> Registrar avance
                        </button>
                        <button onClick={() => abrirModal('APROBACION')}>
                          <Icono nombre="aprobacion" size={16} /> Solicitar aprobación
                        </button>
                      </>
                    )}
                    {!['RS', 'CA', 'NP', 'CF'].includes(detalle.estado) && (
                      <button onClick={() => navigate(`/asistente-ti?incidencia=${encodeURIComponent(detalle.incidenciaNumero)}`)}>
                        <Icono nombre="buscar" size={16} /> Investigar con agente
                      </button>
                    )}
                    <button className="gestion-ti-acciones-rapidas__completo" onClick={() => abrirModal('DETALLE')}>
                      Ver ticket completo →
                    </button>
                  </div>
                </>
              )}
            </article>
            <article className="gestion-ti-panel gestion-ti-tareas">
              <div className="gestion-ti-panel__cabecera">
                <div>
                  <span className="gestion-ti-panel__icono">
                    <Icono nombre="check" />
                  </span>
                  <div>
                    <h2>Tareas del operador</h2>
                    <p>Acciones que requieren atención.</p>
                  </div>
                </div>
              </div>
              <div className="gestion-ti-tareas__grid">
                <button
                  onClick={() => {
                    setEstado('PA')
                    setFiltroRapido('TODOS')
                  }}
                >
                  <strong>{datos?.resumen.aprobacionesPendientes ?? 0}</strong>
                  <span>Aprobaciones pendientes</span>
                </button>
                <button
                  onClick={() => {
                    setEstado('')
                    setFiltroRapido('POR_VENCER')
                  }}
                >
                  <strong>{datos?.resumen.porVencer ?? 0}</strong>
                  <span>SLA por vencer</span>
                </button>
                <button
                  onClick={() => {
                    setEstado('')
                    setFiltroRapido('PRIORIDAD_ALTA')
                  }}
                >
                  <strong>{datos?.resumen.prioridadAlta ?? 0}</strong>
                  <span>Prioridad alta</span>
                </button>
                <button onClick={() => setFiltroRapido('SIN_ASIGNAR')}>
                  <strong>{datos?.resumen.sinAsignar ?? 0}</strong>
                  <span>Tickets sin asignar</span>
                </button>
              </div>
            </article>
          </aside>
        </section>
        <nav className="gestion-ti-proceso" aria-label="Etapas de gestión de tickets">
          <button className={vistaProceso === 'ATENCION' ? 'activo' : ''} onClick={() => cambiarVistaProceso('ATENCION')}>
            <Icono nombre="lista" size={17} />
            <span>
              <strong>Atención</strong>
              <small>Bandeja completa</small>
            </span>
            <b>{conteosProceso.ATENCION}</b>
          </button>
          <button className={vistaProceso === 'ASIGNACION' ? 'activo' : ''} onClick={() => cambiarVistaProceso('ASIGNACION')}>
            <Icono nombre="usuarioGrande" size={17} />
            <span>
              <strong>Asignación</strong>
              <small>Sin responsable</small>
            </span>
            <b>{conteosProceso.ASIGNACION}</b>
          </button>
          <button className={vistaProceso === 'AUTORIZACION' ? 'activo' : ''} onClick={() => cambiarVistaProceso('AUTORIZACION')}>
            <Icono nombre="aprobacion" size={17} />
            <span>
              <strong>Autorización</strong>
              <small>Decisión pendiente</small>
            </span>
            <b>{conteosProceso.AUTORIZACION}</b>
          </button>
          <button className={vistaProceso === 'AVANCES' ? 'activo' : ''} onClick={() => cambiarVistaProceso('AVANCES')}>
            <Icono nombre="mensaje" size={17} />
            <span>
              <strong>Avances</strong>
              <small>Trabajo en curso</small>
            </span>
            <b>{conteosProceso.AVANCES}</b>
          </button>
          <button className={vistaProceso === 'CONFIRMACION' ? 'activo' : ''} onClick={() => cambiarVistaProceso('CONFIRMACION')}>
            <Icono nombre="check" size={17} />
            <span>
              <strong>Confirmación</strong>
              <small>Validación del usuario</small>
            </span>
            <b>{conteosProceso.CONFIRMACION}</b>
          </button>
        </nav>
      </main>
    </MarcoPortal>
  )
}

function tituloModal(modal: Exclude<ModalAccion, null>, asignado: boolean) {
  if (modal === 'DETALLE') return 'Detalle completo del ticket'
  if (modal === 'CLASIFICAR') return 'Clasificar ticket'
  if (modal === 'ASIGNAR') return asignado ? 'Reasignar ticket' : 'Asignar ticket'
  if (modal === 'AVANCE') return 'Registrar avance'
  if (modal === 'APROBACION') return 'Solicitar aprobación'
  if (modal === 'INFORMACION') return 'Solicitar información'
  if (modal === 'RESOLVER') return 'Enviar solución a validación'
  return 'Marcar como No Procede'
}
function DatoRapido({ label, valor }: { label: string; valor: string }) {
  return (
    <div>
      <dt>{label}</dt>
      <dd>{valor || '—'}</dd>
    </div>
  )
}
function PieModal({
  procesando,
  texto,
  onCancelar,
  peligro = false,
}: {
  procesando: boolean
  texto: string
  onCancelar: () => void
  peligro?: boolean
}) {
  return (
    <footer className="gestion-ti-modal__pie">
      <button type="button" onClick={onCancelar} disabled={procesando}>
        Cancelar
      </button>
      <button type="submit" className={peligro ? 'peligro' : 'principal'} disabled={procesando}>
        {procesando ? 'Procesando...' : texto}
      </button>
    </footer>
  )
}

function DetalleCompleto({
  detalle,
  onAccion,
  onAprobar,
  usuarioActual,
}: {
  detalle: GestionTicketTIDetalle
  onAccion: (tipo: Exclude<ModalAccion, null>) => void
  onAprobar: (secuencia: number, aprobar: boolean) => void
  usuarioActual: string
}) {
  return (
    <div className="gestion-ti-detalle-completo">
      <section>
        <h3>Solicitud original</h3>
        <div className="gestion-ti-detalle-completo__cuadricula">
          <Dato label="Solicitante" valor={detalle.solicitante} />
          <Dato label="Área" valor={detalle.areaSolicitanteDescripcion} />
          <Dato label="Tipo" valor={detalle.tipoDescripcion} />
          <Dato label="Estado" valor={detalle.estadoDescripcion} />
          <Dato label="Línea" valor={detalle.lineaDescripcion} />
          <Dato label="Item" valor={detalle.itemDescripcion || 'Pendiente'} />
          <Dato label="Categoría" valor={detalle.categoriaDescripcion || 'Pendiente'} />
          <Dato label="Responsable" valor={detalle.responsable} />
          <Dato label="Área causante" valor={detalle.areaCausanteDescripcion || 'Pendiente'} />
        </div>
        <Bloque titulo="Detalle" texto={detalle.detalle} />
        {detalle.mensajeError && <Bloque titulo="Mensaje de error" texto={detalle.mensajeError} error />}
      </section>
      <FichaTicketTI incidenciaNumero={detalle.incidenciaNumero} />
      {detalle.documentos.length > 0 && (
        <section>
          <h3>Documentos relacionados</h3>
          <div className="gestion-ti-lista-simple">
            {detalle.documentos.map(x => (
              <div key={x.secuencia}>
                <Icono nombre="archivo" size={17} />
                <span>
                  <strong>
                    {x.tipoDocumento} {x.numeroDocumento}
                  </strong>
                  <small>{x.descripcion || x.companiaSocio}</small>
                </span>
              </div>
            ))}
          </div>
        </section>
      )}
      {detalle.adjuntos.some(x => x.tipoMime.startsWith('video/')) && (
        <section>
          <h3>Grabaciones de pantalla</h3>
          <div className="gestion-ti-videos">
            {detalle.adjuntos
              .filter(x => x.tipoMime.startsWith('video/'))
              .map(x => (
                <figure key={x.secuencia}>
                  <video controls preload="metadata" src={urlAdjuntoGestionTI(detalle.incidenciaNumero, x.secuencia)} />
                  <figcaption>
                    {x.nombreOriginal} · {fechaHora(x.fechaRegistro)}
                  </figcaption>
                </figure>
              ))}
          </div>
        </section>
      )}
      {detalle.adjuntos.length > 0 && (
        <section>
          <h3>Evidencias adjuntas</h3>
          <div className="gestion-ti-lista-simple">
            {detalle.adjuntos.map(x => (
              <a key={x.secuencia} href={urlAdjuntoGestionTI(detalle.incidenciaNumero, x.secuencia)} target="_blank" rel="noreferrer">
                <Icono nombre="descargar" size={17} />
                <span>
                  <strong>{x.nombreOriginal}</strong>
                  <small>
                    {Math.max(1, Math.round(x.tamanoBytes / 1024))} KB · {fechaHora(x.fechaRegistro)}
                  </small>
                </span>
              </a>
            ))}
          </div>
        </section>
      )}
      <section>
        <h3>Historial</h3>
        <div className="gestion-ti-historial">
          {detalle.historialEstados.map(x => (
            <div key={x.secuencia}>
              <span />
              <div>
                <strong>{x.estadoDescripcion}</strong>
                <small>
                  {x.actor} · {fechaHora(x.fechaCambio)}
                </small>
                {x.observacion && <p>{x.observacion}</p>}
              </div>
            </div>
          ))}
        </div>
      </section>
      {detalle.avances.length > 0 && (
        <section>
          <h3>Avances técnicos</h3>
          <div className="gestion-ti-tarjetas-texto">
            {detalle.avances.map(x => (
              <article key={x.secuencia}>
                <header>
                  <strong>{x.responsable}</strong>
                  <small>{fechaHora(x.fechaAvance)}</small>
                </header>
                <p>{x.detalle}</p>
                {x.tiempoUtilizado !== null && <small>Tiempo efectivo: {x.tiempoUtilizado} min</small>}
              </article>
            ))}
          </div>
        </section>
      )}
      {detalle.mensajes.length > 0 && (
        <section>
          <h3>Mensajes</h3>
          <div className="gestion-ti-tarjetas-texto">
            {detalle.mensajes.map(x => (
              <article key={x.secuencia} className={x.esInterno ? 'interno' : ''}>
                <header>
                  <strong>
                    {x.autor}
                    {x.esInterno ? ' · Interno' : ''}
                  </strong>
                  <small>{fechaHora(x.fechaMensaje)}</small>
                </header>
                <p>{x.contenido}</p>
              </article>
            ))}
          </div>
        </section>
      )}
      {detalle.aprobaciones.length > 0 && (
        <section>
          <h3>Aprobaciones</h3>
          <div className="gestion-ti-aprobaciones">
            {detalle.aprobaciones.map(x => (
              <article key={x.secuencia}>
                <div>
                  <strong>{x.accionNombre}</strong>
                  <span>Riesgo: {x.nivelRiesgo}</span>
                  <p>{x.justificacion}</p>
                  {x.parametrosJson && x.parametrosJson !== '{}' && (
                    <code className="gestion-ti-aprobaciones__parametros" title="Parámetros exactos que se autorizan">
                      {x.parametrosJson}
                    </code>
                  )}
                  {x.comentarioRespuesta && <small>{x.comentarioRespuesta}</small>}
                  {x.estado === 'P' && x.usuarioSolicitante === usuarioActual && (
                    <small>Solicitada por ti: debe responderla otro operador TI.</small>
                  )}
                </div>
                <div>
                  <span
                    className={`gestion-ti-badge gestion-ti-badge--${x.estado === 'P' ? 'amarillo' : x.estado === 'A' ? 'verde' : 'rojo'}`}
                  >
                    {x.estado === 'P' ? 'Pendiente' : x.estado === 'A' ? 'Aprobada' : x.estado === 'R' ? 'Rechazada' : 'Cancelada'}
                  </span>
                  {x.estado === 'P' && x.usuarioSolicitante !== usuarioActual && (
                    <div className="gestion-ti-aprobaciones__acciones">
                      <button onClick={() => onAprobar(x.secuencia, false)}>Rechazar</button>
                      <button onClick={() => onAprobar(x.secuencia, true)}>Aprobar</button>
                    </div>
                  )}
                </div>
              </article>
            ))}
          </div>
        </section>
      )}
      <InvestigacionesAgente incidenciaNumero={detalle.incidenciaNumero} />
      {(detalle.causaRaiz || detalle.solucionTecnica) && (
        <section>
          <h3>Resolución</h3>
          <Bloque titulo="Causa raíz" texto={detalle.causaRaiz || 'Pendiente'} />
          <Bloque titulo="Solución técnica" texto={detalle.solucionTecnica || 'Pendiente'} />
        </section>
      )}
      {puedeOperar(detalle.estado) && (
        <footer className="gestion-ti-detalle-completo__acciones">
          <button onClick={() => onAccion('CLASIFICAR')}>Clasificar</button>
          <button onClick={() => onAccion('ASIGNAR')}>{detalle.usuarioTI ? 'Reasignar' : 'Asignar'}</button>
          <button onClick={() => onAccion('AVANCE')}>Registrar avance</button>
          <button onClick={() => onAccion('APROBACION')}>Solicitar aprobación</button>
          <button onClick={() => onAccion('INFORMACION')}>Solicitar información</button>
          <button className="principal" onClick={() => onAccion('RESOLVER')}>
            Resolver / validar
          </button>
          <button className="peligro" onClick={() => onAccion('NO_PROCEDE')}>
            No procede
          </button>
        </footer>
      )}
    </div>
  )
}
// Expedientes del Agente de Ingeniería asociados al ticket: consulta para cualquier operador TI; abrir la sesión depende de propiedad o supervisión.
/** Ficha que el colaborador completó al registrar el ticket; no ocupa espacio si el tipo no tiene ficha. */
function FichaTicketTI({ incidenciaNumero }: { incidenciaNumero: string }) {
  const [ficha, setFicha] = useState<DatoFichaTicket[]>([])
  useEffect(() => {
    let vigente = true
    obtenerFichaTI(incidenciaNumero)
      .then(datos => vigente && setFicha(datos))
      .catch(() => vigente && setFicha([]))
    return () => {
      vigente = false
    }
  }, [incidenciaNumero])
  if (ficha.length === 0) return null
  return (
    <section>
      <h3>Ficha del ticket</h3>
      <FichaRegistrada datos={ficha} />
    </section>
  )
}

function InvestigacionesAgente({ incidenciaNumero }: { incidenciaNumero: string }) {
  const navigate = useNavigate()
  const [lista, setLista] = useState<AgenteTIInvestigacionTicket[] | null>(null)
  const [informe, setInforme] = useState<{ sesion: number; texto: string } | null>(null)
  const [error, setError] = useState('')
  useEffect(() => {
    let activo = true
    listarInvestigacionesTicketTI(incidenciaNumero)
      .then(x => {
        if (activo) setLista(x)
      })
      .catch(e => {
        if (activo) {
          setLista([])
          setError(e instanceof Error ? e.message : 'No se pudieron cargar las investigaciones del agente.')
        }
      })
    return () => {
      activo = false
    }
  }, [incidenciaNumero])
  async function alternarInforme(sesion: number) {
    if (informe?.sesion === sesion) {
      setInforme(null)
      return
    }
    try {
      const r = await obtenerInformeTI(sesion)
      setInforme({ sesion, texto: r.informeMarkdown })
      setError('')
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo obtener el expediente.')
    }
  }
  return (
    <section>
      <h3>Investigaciones del agente</h3>
      {error && <p className="gestion-ti-nota gestion-ti-nota--alerta">{error}</p>}
      {lista === null ? (
        <p className="gestion-ti-nota">Cargando investigaciones...</p>
      ) : lista.length === 0 ? (
        <p className="gestion-ti-nota">Este ticket todavía no tiene investigaciones del Agente de Ingeniería.</p>
      ) : (
        <div className="gestion-ti-agente">
          {lista.map(x => (
            <article key={x.sesionNumero}>
              <header>
                <strong>AGT-{String(x.sesionNumero).padStart(6, '0')}</strong>
                <span>{x.estado.replaceAll('_', ' ')}</span>
                <small>
                  {x.nombreOperador} · {fechaHora(x.fechaInicio)}
                  {x.confianza !== null ? ` · confianza ${Math.round(x.confianza)}%` : ''}
                  {x.accionCodigo ? ` · acción ${x.accionCodigo}` : ''}
                </small>
              </header>
              {x.diagnostico && <p>{x.diagnostico}</p>}
              <div className="gestion-ti-agente__acciones">
                {x.informeDisponible && (
                  <button onClick={() => void alternarInforme(x.sesionNumero)}>
                    {informe?.sesion === x.sesionNumero ? 'Ocultar expediente' : 'Ver expediente'}
                  </button>
                )}
                {x.puedeAbrir && <button onClick={() => navigate(`/asistente-ti?sesion=${x.sesionNumero}`)}>Abrir investigación</button>}
              </div>
              {informe?.sesion === x.sesionNumero && <pre>{informe.texto}</pre>}
            </article>
          ))}
        </div>
      )}
    </section>
  )
}

function Dato({ label, valor }: { label: string; valor: string }) {
  return (
    <div>
      <span>{label}</span>
      <strong>{valor || '—'}</strong>
    </div>
  )
}
function Bloque({ titulo, texto, error = false }: { titulo: string; texto: string; error?: boolean }) {
  return (
    <div className={`gestion-ti-bloque-texto ${error ? 'gestion-ti-bloque-texto--error' : ''}`}>
      <strong>{titulo}</strong>
      <p>{texto}</p>
    </div>
  )
}
