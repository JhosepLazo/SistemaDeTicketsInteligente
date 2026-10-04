/**
 * Archivo: MisTicketsUsuarioPage.tsx
 * Objetivo: Implementar el módulo Mis Tickets para el usuario autenticado siguiendo la estructura visual definida para Calimod.
 * Responsabilidad: Mostrar la bandeja personal, filtros, detalle rápido/completo y permitir responder observaciones, validar/reabrir soluciones, calificar y consultar adjuntos.
 * Dependencias: AutenticacionContext, misTicketsApi, NotificacionesCampana, InicioPage.css y MisTicketsUsuarioPage.css.
 * Flujo: Ruta protegida /mis-tickets -> listado personal -> selección de ticket -> detalle/acción -> actualización de bandeja.
 * Consideraciones: El módulo no permite asignar, priorizar ni clasificar técnicamente tickets; esas funciones pertenecen a TI. Las acciones disponibles dependen del estado real retornado por backend.
 */

import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { useAutenticacion } from '../autenticacion/AutenticacionContext'
import {
  calificarTicket,
  obtenerDetalleMisTicket,
  obtenerMisTickets,
  responderObservacion,
  urlAdjunto,
  validarSolucion,
  type MisTicketDetalle,
  type MisTicketItem,
  type MisTicketsRespuesta,
} from '../../services/misTicketsApi'
import MarcoPortal from '../../components/MarcoPortal'
import Icono from '../../components/Icono'
import './MisTicketsUsuarioPage.css'

type Filtro = 'TODOS' | 'ACTIVOS' | 'PENDIENTES' | 'RESUELTOS'
type Orden = 'RECIENTE' | 'ANTIGUO'
type Modal = 'DETALLE' | 'RESPONDER' | 'VALIDAR' | 'CALIFICAR' | null

const maximoAdjuntos = 5
const maximoBytes = 10 * 1024 * 1024
const extensionesPermitidas = ['.png', '.jpg', '.jpeg', '.webp', '.pdf', '.xls', '.xlsx']

function fechaCorta(fecha?: string | null) {
  if (!fecha) return '—'
  const valor = new Date(fecha)
  return Number.isNaN(valor.getTime())
    ? '—'
    : valor.toLocaleString('es-PE', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}

function tiempoRelativo(fecha: string) {
  const valor = new Date(fecha).getTime()
  const diferencia = Date.now() - valor
  if (!Number.isFinite(valor) || diferencia < 0) return 'recientemente'
  const minutos = Math.floor(diferencia / 60000)
  if (minutos < 1) return 'ahora'
  if (minutos < 60) return `hace ${minutos} min`
  const horas = Math.floor(minutos / 60)
  if (horas < 24) return `hace ${horas} ${horas === 1 ? 'hora' : 'horas'}`
  const dias = Math.floor(horas / 24)
  return `hace ${dias} ${dias === 1 ? 'día' : 'días'}`
}

function formatearTamano(bytes: number) {
  if (bytes < 1024 * 1024) return `${Math.max(1, Math.round(bytes / 1024))} KB`
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`
}

function claseEstado(estado: string) {
  if (estado === 'RS') return 'mis-tickets-estado mis-tickets-estado--resuelto'
  if (estado === 'RC') return 'mis-tickets-estado mis-tickets-estado--respuesta'
  if (estado === 'PV') return 'mis-tickets-estado mis-tickets-estado--validacion'
  if (estado === 'CA') return 'mis-tickets-estado mis-tickets-estado--cancelado'
  if (estado === 'RA' || estado === 'ES') return 'mis-tickets-estado mis-tickets-estado--alerta'
  return 'mis-tickets-estado mis-tickets-estado--atencion'
}

function textoAccion(accion: string) {
  if (accion === 'RESPONDER_OBSERVACION') return 'Responder'
  if (accion === 'CONFIRMAR_SOLUCION') return 'Confirmar'
  if (accion === 'CALIFICAR_ATENCION') return 'Calificar'
  return 'Ver detalle'
}

function prioridadTexto(prioridad: number | null) {
  return prioridad === null ? 'Por definir' : `P${prioridad}`
}

export default function MisTicketsUsuarioPage() {
  const navigate = useNavigate()
  const [parametros] = useSearchParams()
  const ticketNotificacion = parametros.get('ticket')?.trim().toUpperCase()
  const { usuario } = useAutenticacion()
  const buscadorRef = useRef<HTMLInputElement>(null)
  const archivoRef = useRef<HTMLInputElement>(null)
  const solicitudDetalleRef = useRef(0)
  const [datos, setDatos] = useState<MisTicketsRespuesta | null>(null)
  const [detalle, setDetalle] = useState<MisTicketDetalle | null>(null)
  const [seleccionado, setSeleccionado] = useState('')
  const [busqueda, setBusqueda] = useState('')
  const [filtro, setFiltro] = useState<Filtro>('ACTIVOS')
  const [orden, setOrden] = useState<Orden>('RECIENTE')
  const [cargando, setCargando] = useState(true)
  const [cargandoDetalle, setCargandoDetalle] = useState(false)
  const [error, setError] = useState('')
  const [modal, setModal] = useState<Modal>(null)
  const [respuesta, setRespuesta] = useState('')
  const [adjuntos, setAdjuntos] = useState<File[]>([])
  const [solucionada, setSolucionada] = useState<boolean | null>(null)
  const [comentarioValidacion, setComentarioValidacion] = useState('')
  const [calificacion, setCalificacion] = useState(0)
  const [comentarioCalificacion, setComentarioCalificacion] = useState('')
  const [guardando, setGuardando] = useState(false)
  const [mensaje, setMensaje] = useState('')

  async function cargarDetalle(incidenciaNumero: string) {
    const solicitud = ++solicitudDetalleRef.current
    setCargandoDetalle(true)
    try {
      const valor = await obtenerDetalleMisTicket(incidenciaNumero)
      if (solicitud !== solicitudDetalleRef.current) return null
      setDetalle(valor)
      return valor
    } catch (e) {
      if (solicitud === solicitudDetalleRef.current) {
        setDetalle(null)
        setError(e instanceof Error ? e.message : 'No fue posible cargar el detalle del ticket.')
      }
      return null
    } finally {
      if (solicitud === solicitudDetalleRef.current) setCargandoDetalle(false)
    }
  }

  async function seleccionarTicket(incidenciaNumero: string) {
    setSeleccionado(incidenciaNumero)
    setError('')
    setMensaje('')
    return cargarDetalle(incidenciaNumero)
  }

  async function cargarListado(preferido?: string) {
    setCargando(true)
    setError('')
    try {
      const respuestaListado = await obtenerMisTickets()
      setDatos(respuestaListado)
      const candidato =
        respuestaListado.tickets.find(ticket => ticket.incidenciaNumero === preferido) ??
        respuestaListado.tickets.find(ticket => ticket.accion === 'RESPONDER_OBSERVACION' || ticket.accion === 'CONFIRMAR_SOLUCION') ??
        respuestaListado.tickets[0]

      if (candidato) {
        setSeleccionado(candidato.incidenciaNumero)
        await cargarDetalle(candidato.incidenciaNumero)
      } else {
        solicitudDetalleRef.current++
        setSeleccionado('')
        setDetalle(null)
        setCargandoDetalle(false)
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No fue posible cargar tus tickets.')
    } finally {
      setCargando(false)
    }
  }

  useEffect(() => {
    void cargarListado(ticketNotificacion)
  }, [ticketNotificacion])

  useEffect(() => {
    function manejarAtajos(event: KeyboardEvent) {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault()
        buscadorRef.current?.focus()
        buscadorRef.current?.select()
      }
      if (event.key === 'Escape') {
        if (modal) cerrarModal()
        else if (document.activeElement === buscadorRef.current) {
          setBusqueda('')
          buscadorRef.current?.blur()
        }
      }
    }

    window.addEventListener('keydown', manejarAtajos)
    return () => window.removeEventListener('keydown', manejarAtajos)
  }, [modal])

  const ticketsFiltrados = useMemo(() => {
    if (!datos) return []
    const texto = busqueda.trim().toLowerCase()
    const filtrados = datos.tickets.filter(ticket => {
      const coincideTexto =
        !texto ||
        `${ticket.incidenciaNumero} ${ticket.titulo} ${ticket.detalle} ${ticket.estadoDescripcion} ${ticket.responsable}`
          .toLowerCase()
          .includes(texto)
      const coincideFiltro =
        filtro === 'TODOS' ||
        (filtro === 'ACTIVOS' && !['RS', 'CA'].includes(ticket.estado)) ||
        (filtro === 'PENDIENTES' && ['RC', 'PV'].includes(ticket.estado)) ||
        (filtro === 'RESUELTOS' && ticket.estado === 'RS')
      return coincideTexto && coincideFiltro
    })

    return [...filtrados].sort((a, b) => {
      const diferencia = new Date(b.ultimaFechaModif).getTime() - new Date(a.ultimaFechaModif).getTime()
      return orden === 'RECIENTE' ? diferencia : -diferencia
    })
  }, [busqueda, datos, filtro, orden])

  const accionesPendientes = useMemo(() => datos?.tickets.filter(ticket => ticket.accion !== 'VER_DETALLE').slice(0, 3) ?? [], [datos])

  const actividadReciente = useMemo(() => {
    if (!detalle) return []
    return [
      ...detalle.historialEstados.map(item => ({ fecha: item.fecha, titulo: item.estadoDescripcion, detalle: item.actor })),
      ...detalle.avances.map(item => ({ fecha: item.fecha, titulo: 'Avance de TI', detalle: item.responsable })),
      ...detalle.mensajes.map(item => ({ fecha: item.fecha, titulo: 'Nuevo mensaje', detalle: item.autor })),
    ]
      .sort((a, b) => new Date(b.fecha).getTime() - new Date(a.fecha).getTime())
      .slice(0, 5)
  }, [detalle])

  if (!usuario) return null

  function cerrarModal() {
    setModal(null)
    setError('')
    setRespuesta('')
    setAdjuntos([])
    setSolucionada(null)
    setComentarioValidacion('')
    setCalificacion(0)
    setComentarioCalificacion('')
    if (archivoRef.current) archivoRef.current.value = ''
  }

  async function abrirAccion(ticket: MisTicketItem) {
    const valor = ticket.incidenciaNumero === seleccionado && detalle ? detalle : await seleccionarTicket(ticket.incidenciaNumero)
    if (!valor) return

    if (ticket.accion === 'RESPONDER_OBSERVACION') setModal('RESPONDER')
    else if (ticket.accion === 'CONFIRMAR_SOLUCION') setModal('VALIDAR')
    else if (ticket.accion === 'CALIFICAR_ATENCION') setModal('CALIFICAR')
    else setModal('DETALLE')
  }

  function agregarAdjuntos(lista: FileList | File[]) {
    const nuevos = Array.from(lista)
    if (adjuntos.length + nuevos.length > maximoAdjuntos) {
      setError(`Puedes adjuntar como máximo ${maximoAdjuntos} archivos.`)
      return
    }

    for (const archivo of nuevos) {
      const punto = archivo.name.lastIndexOf('.')
      const extension = punto >= 0 ? archivo.name.slice(punto).toLowerCase() : ''
      if (!extensionesPermitidas.includes(extension)) {
        setError(`El archivo ${archivo.name} no tiene un formato permitido.`)
        return
      }
      if (archivo.size > maximoBytes) {
        setError(`El archivo ${archivo.name} supera el límite de 10 MB.`)
        return
      }
    }

    setAdjuntos(actual => [...actual, ...nuevos])
    setError('')
  }

  async function enviarRespuesta() {
    if (!detalle || respuesta.trim().length < 3) {
      setError('Escribe la información solicitada antes de responder.')
      return
    }

    setGuardando(true)
    setError('')
    try {
      await responderObservacion(detalle.incidenciaNumero, respuesta.trim(), adjuntos)
      const numero = detalle.incidenciaNumero
      cerrarModal()
      setMensaje('La información fue enviada correctamente. El ticket volvió a diagnóstico.')
      await cargarListado(numero)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No fue posible enviar la respuesta.')
    } finally {
      setGuardando(false)
    }
  }

  async function enviarValidacion() {
    if (!detalle || solucionada === null) {
      setError('Indica si la solución resolvió el problema.')
      return
    }
    if (!solucionada && !comentarioValidacion.trim()) {
      setError('Explica brevemente qué problema continúa para reabrir el ticket.')
      return
    }

    setGuardando(true)
    setError('')
    try {
      await validarSolucion(detalle.incidenciaNumero, solucionada, comentarioValidacion.trim())
      const numero = detalle.incidenciaNumero
      cerrarModal()
      setMensaje(solucionada ? 'La solución fue confirmada correctamente.' : 'El ticket fue reabierto para continuar la atención.')
      await cargarListado(numero)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No fue posible registrar la validación.')
    } finally {
      setGuardando(false)
    }
  }

  async function enviarCalificacion() {
    if (!detalle || calificacion < 1) {
      setError('Selecciona una calificación entre 1 y 5 estrellas.')
      return
    }

    setGuardando(true)
    setError('')
    try {
      await calificarTicket(detalle.incidenciaNumero, calificacion, comentarioCalificacion.trim())
      const numero = detalle.incidenciaNumero
      cerrarModal()
      setMensaje('Gracias. Tu calificación fue registrada correctamente.')
      await cargarListado(numero)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No fue posible registrar la calificación.')
    } finally {
      setGuardando(false)
    }
  }

  return (
    <MarcoPortal
      menu="usuario"
      activa="misTickets"
      clase="mis-tickets-shell"
      barra={
        <label className="inicio-buscador">
          <Icono nombre="buscar" size={19} />
          <input
            ref={buscadorRef}
            value={busqueda}
            onChange={event => setBusqueda(event.target.value)}
            placeholder="Buscar entre tus tickets..."
            aria-label="Buscar tickets"
          />
          <span>Ctrl + K</span>
        </label>
      }
      capas={
        <>
          {modal && detalle && (
            <div
              className="mis-tickets-modal-fondo"
              role="presentation"
              onMouseDown={event => {
                if (event.target === event.currentTarget && !guardando) cerrarModal()
              }}
            >
              <section
                className={`mis-tickets-modal ${modal === 'DETALLE' ? 'mis-tickets-modal--grande' : ''}`}
                role="dialog"
                aria-modal="true"
                aria-labelledby="modal-titulo"
              >
                <header className="mis-tickets-modal__cabecera">
                  <div>
                    <span>{detalle.incidenciaNumero}</span>
                    <h2 id="modal-titulo">
                      {modal === 'DETALLE'
                        ? 'Detalle completo del ticket'
                        : modal === 'RESPONDER'
                          ? 'Responder observación'
                          : modal === 'VALIDAR'
                            ? 'Validar solución'
                            : 'Calificar atención'}
                    </h2>
                  </div>
                  <button type="button" onClick={cerrarModal} disabled={guardando} aria-label="Cerrar">
                    <Icono nombre="cerrarPequeno" />
                  </button>
                </header>
                {error && (
                  <div className="mis-tickets-alerta mis-tickets-alerta--error mis-tickets-modal__alerta" role="alert">
                    <span>{error}</span>
                  </div>
                )}

                {modal === 'DETALLE' && (
                  <div className="mis-tickets-modal__detalle">
                    <section className="mis-tickets-modal__resumen">
                      <div>
                        <span className={claseEstado(detalle.estado)}>{detalle.estadoDescripcion}</span>
                        <h3>{detalle.titulo}</h3>
                        <p>{detalle.detalle}</p>
                      </div>
                      <dl>
                        <div>
                          <dt>Área</dt>
                          <dd>{detalle.areaDescripcion}</dd>
                        </div>
                        <div>
                          <dt>Sistema / módulo</dt>
                          <dd>{detalle.lineaDescripcion}</dd>
                        </div>
                        <div>
                          <dt>Tipo</dt>
                          <dd>{detalle.tipoDescripcion}</dd>
                        </div>
                        <div>
                          <dt>Responsable</dt>
                          <dd>{detalle.responsable}</dd>
                        </div>
                        <div>
                          <dt>Creación</dt>
                          <dd>{fechaCorta(detalle.fechaRegistro)}</dd>
                        </div>
                        <div>
                          <dt>Prioridad</dt>
                          <dd>{prioridadTexto(detalle.prioridad)}</dd>
                        </div>
                      </dl>
                    </section>
                    {detalle.mensajeError && (
                      <section className="mis-tickets-bloque">
                        <h3>Mensaje de error</h3>
                        <p className="mis-tickets-error-texto">{detalle.mensajeError}</p>
                      </section>
                    )}
                    {detalle.documentos.length > 0 && (
                      <section className="mis-tickets-bloque">
                        <h3>Documentos relacionados</h3>
                        <div className="mis-tickets-documentos">
                          {detalle.documentos.map(documento => (
                            <span key={documento.secuencia}>
                              <strong>{documento.tipoDocumento}</strong> {documento.numeroDocumento}
                              {documento.descripcion && <small>{documento.descripcion}</small>}
                            </span>
                          ))}
                        </div>
                      </section>
                    )}
                    <section className="mis-tickets-modal__columnas">
                      <div className="mis-tickets-bloque">
                        <h3>Historial del ticket</h3>
                        <div className="mis-tickets-historial-completo">
                          {detalle.historialEstados.map(item => (
                            <div key={item.secuencia}>
                              <i />
                              <span>
                                <strong>{item.estadoDescripcion}</strong>
                                <small>
                                  {item.actor} · {fechaCorta(item.fecha)}
                                </small>
                                {item.observacion && <p>{item.observacion}</p>}
                              </span>
                            </div>
                          ))}
                        </div>
                      </div>
                      <div className="mis-tickets-bloque">
                        <h3>Conversación y avances</h3>
                        <div className="mis-tickets-conversacion">
                          {detalle.avances.map(item => (
                            <div key={`a-${item.secuencia}`}>
                              <strong>{item.responsable}</strong>
                              <small>{fechaCorta(item.fecha)}</small>
                              <p>{item.detalle}</p>
                            </div>
                          ))}
                          {detalle.mensajes.map(item => (
                            <div key={`m-${item.secuencia}`} className={item.tipoAutor === 'U' ? 'usuario' : ''}>
                              <strong>{item.autor}</strong>
                              <small>{fechaCorta(item.fecha)}</small>
                              <p>{item.contenido}</p>
                            </div>
                          ))}
                          {detalle.avances.length === 0 && detalle.mensajes.length === 0 && (
                            <p className="mis-tickets-mini-vacio">Aún no hay mensajes o avances visibles.</p>
                          )}
                        </div>
                      </div>
                    </section>
                    {detalle.adjuntos.some(adjunto => adjunto.tipoMime.startsWith('video/')) && (
                      <section className="mis-tickets-bloque">
                        <h3>Grabaciones de tu pantalla</h3>
                        <div className="mis-tickets-videos">
                          {detalle.adjuntos
                            .filter(adjunto => adjunto.tipoMime.startsWith('video/'))
                            .map(adjunto => (
                              <video
                                key={adjunto.secuencia}
                                controls
                                preload="metadata"
                                src={urlAdjunto(detalle.incidenciaNumero, adjunto.secuencia)}
                              />
                            ))}
                        </div>
                      </section>
                    )}
                    {detalle.adjuntos.length > 0 && (
                      <section className="mis-tickets-bloque">
                        <h3>Archivos adjuntos</h3>
                        <div className="mis-tickets-adjuntos-lista">
                          {detalle.adjuntos.map(adjunto => (
                            <a
                              key={adjunto.secuencia}
                              href={urlAdjunto(detalle.incidenciaNumero, adjunto.secuencia)}
                              target="_blank"
                              rel="noreferrer"
                            >
                              <Icono nombre="clip" size={17} />
                              <span>
                                <strong>{adjunto.nombreOriginal}</strong>
                                <small>{formatearTamano(adjunto.tamanoBytes)}</small>
                              </span>
                            </a>
                          ))}
                        </div>
                      </section>
                    )}
                    {detalle.calificacion !== null && (
                      <section className="mis-tickets-bloque">
                        <h3>Tu calificación</h3>
                        <p className="mis-tickets-calificacion-leida">
                          {'★'.repeat(detalle.calificacion)}
                          {'☆'.repeat(5 - detalle.calificacion)} <span>{detalle.comentarioCalificacion}</span>
                        </p>
                      </section>
                    )}
                  </div>
                )}

                {modal === 'RESPONDER' && (
                  <div className="mis-tickets-modal__formulario">
                    <p>
                      TI necesita información adicional para continuar con el diagnóstico. Responde con el contexto solicitado y adjunta
                      evidencia si corresponde.
                    </p>
                    <label>
                      <span>
                        Tu respuesta <b>*</b>
                      </span>
                      <textarea
                        value={respuesta}
                        maxLength={2000}
                        onChange={event => setRespuesta(event.target.value)}
                        placeholder="Describe la información solicitada..."
                      />
                      <small>{respuesta.length}/2000</small>
                    </label>
                    <div className="mis-tickets-adjuntar">
                      <button type="button" onClick={() => archivoRef.current?.click()}>
                        <Icono nombre="subir" size={18} /> Adjuntar evidencia
                      </button>
                      <span>Hasta 5 archivos, 10 MB cada uno</span>
                      <input
                        ref={archivoRef}
                        type="file"
                        multiple
                        accept=".png,.jpg,.jpeg,.webp,.pdf,.xls,.xlsx"
                        hidden
                        onChange={event => event.target.files && agregarAdjuntos(event.target.files)}
                      />
                    </div>
                    {adjuntos.length > 0 && (
                      <div className="mis-tickets-adjuntos-seleccionados">
                        {adjuntos.map((archivo, indice) => (
                          <span key={`${archivo.name}-${indice}`}>
                            <Icono nombre="documento" size={15} /> {archivo.name}
                            <button
                              type="button"
                              onClick={() => setAdjuntos(actual => actual.filter((_, posicion) => posicion !== indice))}
                            >
                              <Icono nombre="cerrarPequeno" size={13} />
                            </button>
                          </span>
                        ))}
                      </div>
                    )}
                  </div>
                )}

                {modal === 'VALIDAR' && (
                  <div className="mis-tickets-modal__formulario">
                    <p>
                      TI indicó que la atención fue completada. Confirma el resultado para cerrar el ticket o reabrirlo si el problema
                      continúa.
                    </p>
                    <div className="mis-tickets-validacion-opciones">
                      <button type="button" className={solucionada === true ? 'activo correcto' : ''} onClick={() => setSolucionada(true)}>
                        <Icono nombre="check" size={21} />
                        <span>
                          <strong>Sí, quedó solucionado</strong>
                          <small>Confirmar y cerrar el ticket</small>
                        </span>
                      </button>
                      <button
                        type="button"
                        className={solucionada === false ? 'activo problema' : ''}
                        onClick={() => setSolucionada(false)}
                      >
                        <Icono nombre="alerta" size={21} />
                        <span>
                          <strong>No, el problema continúa</strong>
                          <small>Reabrir para continuar la atención</small>
                        </span>
                      </button>
                    </div>
                    <label>
                      <span>Comentario {solucionada === false && <b>*</b>}</span>
                      <textarea
                        value={comentarioValidacion}
                        maxLength={1000}
                        onChange={event => setComentarioValidacion(event.target.value)}
                        placeholder={solucionada === false ? 'Indica qué problema continúa...' : 'Comentario opcional sobre la solución...'}
                      />
                    </label>
                  </div>
                )}

                {modal === 'CALIFICAR' && (
                  <div className="mis-tickets-modal__formulario">
                    <p>Tu opinión nos ayuda a mejorar la atención. La calificación se registra una sola vez.</p>
                    <div className="mis-tickets-estrellas" aria-label="Calificación de atención">
                      {[1, 2, 3, 4, 5].map(valor => (
                        <button
                          key={valor}
                          type="button"
                          className={valor <= calificacion ? 'activo' : ''}
                          onClick={() => setCalificacion(valor)}
                          aria-label={`${valor} estrellas`}
                        >
                          <Icono nombre="estrella" size={30} />
                        </button>
                      ))}
                    </div>
                    <label>
                      <span>Comentario</span>
                      <textarea
                        value={comentarioCalificacion}
                        maxLength={500}
                        onChange={event => setComentarioCalificacion(event.target.value)}
                        placeholder="Cuéntanos brevemente cómo fue la atención..."
                      />
                    </label>
                  </div>
                )}

                {modal !== 'DETALLE' && (
                  <footer className="mis-tickets-modal__pie">
                    <button type="button" className="mis-tickets-boton" onClick={cerrarModal} disabled={guardando}>
                      Cancelar
                    </button>
                    <button
                      type="button"
                      className="mis-tickets-boton mis-tickets-boton--principal"
                      disabled={guardando}
                      onClick={() =>
                        void (modal === 'RESPONDER' ? enviarRespuesta() : modal === 'VALIDAR' ? enviarValidacion() : enviarCalificacion())
                      }
                    >
                      {guardando
                        ? 'Guardando...'
                        : modal === 'RESPONDER'
                          ? 'Enviar respuesta'
                          : modal === 'VALIDAR'
                            ? 'Registrar validación'
                            : 'Enviar calificación'}
                    </button>
                  </footer>
                )}
              </section>
            </div>
          )}
        </>
      }
    >
      <main className="mis-tickets-contenido">
        <section className="mis-tickets-hero">
          <div className="mis-tickets-hero__texto">
            <h1>Mis Tickets</h1>
            <p>Consulta el estado, responde observaciones y da seguimiento a tus incidencias.</p>
            <div className="mis-tickets-hero__chips">
              <span>
                <i className="mis-tickets-punto mis-tickets-punto--verde" /> {datos?.resumen.activos ?? 0} activos
              </span>
              <span>
                <i className="mis-tickets-punto mis-tickets-punto--azul" /> {datos?.resumen.enAtencion ?? 0} en atención
              </span>
              <span>
                <i className="mis-tickets-punto mis-tickets-punto--amarillo" /> {datos?.resumen.pendientesRespuesta ?? 0} requieren tu
                respuesta
              </span>
            </div>
          </div>
          <button className="mis-tickets-nuevo" type="button" onClick={() => navigate('/nuevo-ticket')}>
            <Icono nombre="nuevo" size={22} /> Nuevo Ticket
          </button>
          <div className="mis-tickets-hero__firma">
            <span>Personas</span>
            <span>que avanzan</span>
            <strong>CALIMOD</strong>
          </div>
        </section>

        {error && (
          <div className="mis-tickets-alerta mis-tickets-alerta--error" role="alert">
            <span>{error}</span>
            <button type="button" onClick={() => setError('')}>
              <Icono nombre="cerrarPequeno" size={15} />
            </button>
          </div>
        )}
        {mensaje && (
          <div className="mis-tickets-alerta" role="status">
            <Icono nombre="check" size={18} /> {mensaje}
          </div>
        )}

        <section className="mis-tickets-metricas" aria-label="Resumen de tickets">
          <button type="button" onClick={() => setFiltro('ACTIVOS')} className="mis-tickets-metrica">
            <span className="mis-tickets-metrica__icono mis-tickets-metrica__icono--turquesa">
              <Icono nombre="carpeta" size={28} />
            </span>
            <div>
              <small>Activos</small>
              <strong>{datos?.resumen.activos ?? 0}</strong>
              <span>de tus tickets</span>
            </div>
            <i className="mis-tickets-sparkline" />
          </button>
          <button type="button" onClick={() => setFiltro('ACTIVOS')} className="mis-tickets-metrica">
            <span className="mis-tickets-metrica__icono mis-tickets-metrica__icono--azul">
              <Icono nombre="engranaje" size={28} />
            </span>
            <div>
              <small>En atención</small>
              <strong>{datos?.resumen.enAtencion ?? 0}</strong>
              <span>siendo gestionados</span>
            </div>
            <i className="mis-tickets-sparkline" />
          </button>
          <button type="button" onClick={() => setFiltro('PENDIENTES')} className="mis-tickets-metrica">
            <span className="mis-tickets-metrica__icono mis-tickets-metrica__icono--rojo">
              <Icono nombre="alerta" size={28} />
            </span>
            <div>
              <small>Pendientes de tu respuesta</small>
              <strong>{datos?.resumen.pendientesRespuesta ?? 0}</strong>
              <span>requieren tu acción</span>
            </div>
            <i className="mis-tickets-sparkline" />
          </button>
          <button type="button" onClick={() => setFiltro('RESUELTOS')} className="mis-tickets-metrica">
            <span className="mis-tickets-metrica__icono mis-tickets-metrica__icono--verde">
              <Icono nombre="check" size={28} />
            </span>
            <div>
              <small>Resueltos</small>
              <strong>{datos?.resumen.resueltos30Dias ?? 0}</strong>
              <span>en los últimos 30 días</span>
            </div>
            <i className="mis-tickets-sparkline" />
          </button>
        </section>

        {cargando ? (
          <section className="mis-tickets-cargando" role="status">
            <span className="inicio-spinner" /> Cargando tus tickets...
          </section>
        ) : (
          <section className="mis-tickets-grid">
            <article className="mis-tickets-panel mis-tickets-listado">
              <header className="mis-tickets-panel__titulo">
                <span>
                  <Icono nombre="tickets" size={20} />
                </span>
                <div>
                  <h2>Listado de tickets</h2>
                  <p>Selecciona un ticket para revisar su estado y acciones disponibles.</p>
                </div>
              </header>

              <div className="mis-tickets-herramientas">
                <label className="mis-tickets-busqueda">
                  <Icono nombre="buscar" size={17} />
                  <input value={busqueda} onChange={event => setBusqueda(event.target.value)} placeholder="Buscar por ticket o título..." />
                </label>
                <div className="mis-tickets-filtros">
                  {(['TODOS', 'ACTIVOS', 'PENDIENTES', 'RESUELTOS'] as Filtro[]).map(valor => (
                    <button key={valor} type="button" className={filtro === valor ? 'activo' : ''} onClick={() => setFiltro(valor)}>
                      {valor === 'TODOS' ? 'Todos' : valor === 'ACTIVOS' ? 'Activos' : valor === 'PENDIENTES' ? 'Pendientes' : 'Resueltos'}
                    </button>
                  ))}
                </div>
                <label className="mis-tickets-orden">
                  <Icono nombre="ordenar" size={16} />
                  <select value={orden} onChange={event => setOrden(event.target.value as Orden)}>
                    <option value="RECIENTE">Última actualización</option>
                    <option value="ANTIGUO">Más antiguos</option>
                  </select>
                </label>
              </div>

              <div className="mis-tickets-tabla-wrap">
                <table className="mis-tickets-tabla">
                  <thead>
                    <tr>
                      <th>Ticket</th>
                      <th>Título</th>
                      <th>Estado</th>
                      <th>Responsable</th>
                      <th>Última actualización</th>
                      <th>Acción</th>
                      <th />
                    </tr>
                  </thead>
                  <tbody>
                    {ticketsFiltrados.length === 0 ? (
                      <tr>
                        <td colSpan={7} className="mis-tickets-vacio">
                          No encontramos tickets con los filtros seleccionados.
                        </td>
                      </tr>
                    ) : (
                      ticketsFiltrados.map(ticket => (
                        <tr
                          key={ticket.incidenciaNumero}
                          className={seleccionado === ticket.incidenciaNumero ? 'seleccionado' : ''}
                          onClick={() => void seleccionarTicket(ticket.incidenciaNumero)}
                        >
                          <td>
                            <strong>{ticket.incidenciaNumero}</strong>
                          </td>
                          <td>
                            <span className="mis-tickets-titulo-tabla">{ticket.titulo}</span>
                          </td>
                          <td>
                            <span className={claseEstado(ticket.estado)}>{ticket.estadoDescripcion}</span>
                          </td>
                          <td>
                            <span className="mis-tickets-responsable">
                              <i>{ticket.responsable.slice(0, 1).toUpperCase()}</i>
                              {ticket.responsable}
                            </span>
                          </td>
                          <td>{tiempoRelativo(ticket.ultimaFechaModif)}</td>
                          <td>
                            <button
                              className="mis-tickets-accion"
                              type="button"
                              onClick={event => {
                                event.stopPropagation()
                                void abrirAccion(ticket)
                              }}
                            >
                              {textoAccion(ticket.accion)}
                            </button>
                          </td>
                          <td>
                            <button
                              className="mis-tickets-mas"
                              type="button"
                              title="Ver ticket completo"
                              onClick={event => {
                                event.stopPropagation()
                                void seleccionarTicket(ticket.incidenciaNumero).then(valor => valor && setModal('DETALLE'))
                              }}
                            >
                              <Icono nombre="mas" size={17} />
                            </button>
                          </td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
            </article>

            <aside className="mis-tickets-columna-detalle">
              <article className="mis-tickets-panel mis-tickets-detalle-rapido">
                <header className="mis-tickets-panel__titulo">
                  <span>
                    <Icono nombre="tickets" size={20} />
                  </span>
                  <div>
                    <h2>Detalle rápido del ticket</h2>
                    <p>Información actual del caso seleccionado.</p>
                  </div>
                </header>
                {cargandoDetalle ? (
                  <div className="mis-tickets-cargando mis-tickets-cargando--detalle">Cargando detalle...</div>
                ) : detalle ? (
                  <>
                    <div className="mis-tickets-detalle__encabezado">
                      <div>
                        <strong>{detalle.incidenciaNumero}</strong>
                        <span className={claseEstado(detalle.estado)}>{detalle.estadoDescripcion}</span>
                      </div>
                      <h3>{detalle.titulo}</h3>
                      <p>{detalle.detalle}</p>
                    </div>
                    <div className="mis-tickets-detalle__cuerpo">
                      <div className="mis-tickets-linea-tiempo">
                        {detalle.historialEstados.slice(-4).map((item, indice, lista) => (
                          <div key={item.secuencia} className={indice === lista.length - 1 ? 'actual' : ''}>
                            <i />
                            <span>
                              <strong>{item.estadoDescripcion}</strong>
                              <small>{fechaCorta(item.fecha)}</small>
                            </span>
                          </div>
                        ))}
                      </div>
                      <dl className="mis-tickets-datos">
                        <div>
                          <dt>
                            <Icono nombre="usuario" size={16} /> Responsable
                          </dt>
                          <dd>{detalle.responsable}</dd>
                        </div>
                        <div>
                          <dt>
                            <Icono nombre="calendario" size={16} /> Fecha de creación
                          </dt>
                          <dd>{fechaCorta(detalle.fechaRegistro)}</dd>
                        </div>
                        <div>
                          <dt>
                            <Icono nombre="reloj" size={16} /> Último avance
                          </dt>
                          <dd>{tiempoRelativo(detalle.ultimaFechaModif)}</dd>
                        </div>
                        <div>
                          <dt>
                            <Icono nombre="bandera" size={16} /> Prioridad
                          </dt>
                          <dd>
                            <span className="mis-tickets-prioridad">{prioridadTexto(detalle.prioridad)}</span>
                          </dd>
                        </div>
                      </dl>
                    </div>
                    <div className="mis-tickets-detalle__acciones">
                      {detalle.accion !== 'VER_DETALLE' && (
                        <button
                          className="mis-tickets-boton mis-tickets-boton--principal"
                          type="button"
                          onClick={() =>
                            setModal(
                              detalle.accion === 'RESPONDER_OBSERVACION'
                                ? 'RESPONDER'
                                : detalle.accion === 'CONFIRMAR_SOLUCION'
                                  ? 'VALIDAR'
                                  : 'CALIFICAR',
                            )
                          }
                        >
                          <Icono nombre={detalle.accion === 'CALIFICAR_ATENCION' ? 'estrella' : 'mensaje'} size={17} />{' '}
                          {textoAccion(detalle.accion)}
                        </button>
                      )}
                      <button className="mis-tickets-boton" type="button" onClick={() => setModal('DETALLE')}>
                        <Icono nombre="documento" size={17} /> Ver ticket completo
                      </button>
                    </div>
                  </>
                ) : (
                  <div className="mis-tickets-vacio">Selecciona un ticket para ver su detalle.</div>
                )}
              </article>

              <div className="mis-tickets-subgrid">
                <article className="mis-tickets-panel mis-tickets-mini-panel">
                  <header>
                    <span>
                      <Icono nombre="reloj" size={17} />
                    </span>
                    <h3>Acciones pendientes</h3>
                  </header>
                  {accionesPendientes.length === 0 ? (
                    <p className="mis-tickets-mini-vacio">No tienes acciones pendientes.</p>
                  ) : (
                    accionesPendientes.map(ticket => (
                      <button key={ticket.incidenciaNumero} type="button" onClick={() => void abrirAccion(ticket)}>
                        <i
                          className={
                            ticket.accion === 'RESPONDER_OBSERVACION'
                              ? 'rojo'
                              : ticket.accion === 'CONFIRMAR_SOLUCION'
                                ? 'amarillo'
                                : 'verde'
                          }
                        >
                          <Icono
                            nombre={
                              ticket.accion === 'CALIFICAR_ATENCION'
                                ? 'estrella'
                                : ticket.accion === 'CONFIRMAR_SOLUCION'
                                  ? 'reloj'
                                  : 'alerta'
                            }
                            size={15}
                          />
                        </i>
                        <span>
                          <strong>{textoAccion(ticket.accion)}</strong>
                          <small>{ticket.incidenciaNumero}</small>
                        </span>
                        <Icono nombre="flecha" size={14} />
                      </button>
                    ))
                  )}
                </article>

                <article className="mis-tickets-panel mis-tickets-mini-panel mis-tickets-actividad">
                  <header>
                    <span>
                      <Icono nombre="actividad" size={17} />
                    </span>
                    <h3>Actividad reciente</h3>
                  </header>
                  {actividadReciente.length === 0 ? (
                    <p className="mis-tickets-mini-vacio">Sin actividad para mostrar.</p>
                  ) : (
                    actividadReciente.map((item, indice) => (
                      <div key={`${item.fecha}-${indice}`}>
                        <i />
                        <span>
                          <strong>{item.titulo}</strong>
                          <small>
                            {item.detalle} · {tiempoRelativo(item.fecha)}
                          </small>
                        </span>
                      </div>
                    ))
                  )}
                </article>
              </div>
            </aside>
          </section>
        )}
      </main>
    </MarcoPortal>
  )
}
