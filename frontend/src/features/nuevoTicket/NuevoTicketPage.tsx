/**
 * Archivo: NuevoTicketPage.tsx
 * Objetivo: Implementar el módulo Nuevo Ticket para el usuario autenticado siguiendo la estructura visual definida para Calimod.
 * Responsabilidad: Cargar identidad, catálogos, fichas y recursos de soporte; validar formulario, ficha, borrador y evidencias; mostrar
 *   resumen y registrar la incidencia.
 * Dependencias: React Router, AutenticacionContext, nuevoTicketApi, fichaTicketService, FichaTicket, recursosSoporteApi, MarcoPortal,
 *   Icono y NuevoTicketPage.css.
 * Flujo: /nuevo-ticket -> catálogos/recursos -> edición y validación (con la ficha del tipo) -> POST /api/tickets/nuevo -> confirmación.
 * Consideraciones: El asistente puede preparar o conservar un borrador, pero el usuario confirma el envío; no se expone clasificación técnica,
 *   prioridad ni responsable TI. Si el tipo elegido tiene ficha (por ejemplo, el requerimiento), sus obligatorias se completan antes de enviar.
 */

import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { primerNombre, useAutenticacion } from '../autenticacion/AutenticacionContext'
import { crearNuevoTicket, obtenerDatosNuevoTicket, type NuevoTicketDatos, type NuevoTicketFormulario } from '../../services/nuevoTicketApi'
import { avanceFicha, camposDelTipo, fichaJson, validarFicha, type RespuestasFicha } from '../../services/fichaTicketService'
import { obtenerRecursosSoporte, urlFormatoSoporte, type RecursosSoporteRespuesta } from '../../services/recursosSoporteApi'
import MarcoPortal from '../../components/MarcoPortal'
import Icono from '../../components/Icono'
import FichaTicket from './FichaTicket'
import './NuevoTicketPage.css'

const formularioInicial: NuevoTicketFormulario = { linea: '', tipo: '', titulo: '', detalle: '', mensajeError: '', adjuntos: [] }
const maximoAdjuntos = 5
const maximoBytes = 10 * 1024 * 1024
const extensionesPermitidas = ['.png', '.jpg', '.jpeg', '.webp', '.pdf', '.xls', '.xlsx', '.webm', '.mp4']
// Una grabación de pantalla puede pesar más que un documento: hasta 2 videos de 40 MB.
const extensionesVideo = ['.webm', '.mp4']
const maximoBytesVideo = 40 * 1024 * 1024
const maximoVideos = 2
const esVideo = (nombre: string) => extensionesVideo.some(x => nombre.toLowerCase().endsWith(x))
function formatearTamano(bytes: number) {
  return bytes < 1024 * 1024 ? `${Math.max(1, Math.round(bytes / 1024))} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB`
}
function extraerDocumentos(texto: string) {
  const coincidencias = texto.toUpperCase().match(/\b(?:OC|RQ|REQ|PE|PEDIDO)?[- ]?\d{5,14}\b/g) ?? []
  return [...new Set(coincidencias.map(v => v.trim()))].slice(0, 3)
}

export default function NuevoTicketPage() {
  const navigate = useNavigate()
  const location = useLocation()
  const { usuario } = useAutenticacion()
  const archivoRef = useRef<HTMLInputElement>(null)
  const borradorAsistente = (
    location.state as {
      asistenteBorrador?: { titulo?: string; detalle?: string; mensajeError?: string; grabaciones?: File[]; evidencia?: unknown }
    } | null
  )?.asistenteBorrador
  // Lo que el colaborador mostró en pantalla: la grabación viaja como adjunto y la evidencia activa la investigación automática de TI.
  const evidenciaAsistente = borradorAsistente?.evidencia
  const [datos, setDatos] = useState<NuevoTicketDatos | null>(null)
  const [recursos, setRecursos] = useState<RecursosSoporteRespuesta>({ formatos: [], articulos: [] })
  const [formulario, setFormulario] = useState<NuevoTicketFormulario>(() => ({
    ...formularioInicial,
    titulo: borradorAsistente?.titulo ?? '',
    detalle: borradorAsistente?.detalle ?? '',
    mensajeError: borradorAsistente?.mensajeError ?? '',
    adjuntos: (borradorAsistente?.grabaciones ?? []).filter(x => x instanceof File).slice(0, 2),
  }))
  const [ficha, setFicha] = useState<RespuestasFicha>({})
  const [cargando, setCargando] = useState(true)
  const [enviando, setEnviando] = useState(false)
  const [arrastrando, setArrastrando] = useState(false)
  const [error, setError] = useState('')
  const [mensaje, setMensaje] = useState(
    evidenciaAsistente
      ? 'El Asistente TI preparó tu ticket con lo que mostraste en pantalla y adjuntó la grabación. Completa la clasificación y envíalo: el agente de TI empezará a investigarlo de inmediato.'
      : borradorAsistente
        ? 'El Asistente TI preparó un borrador. Revísalo y completa la clasificación antes de enviarlo.'
        : '',
  )
  const [ticketCreado, setTicketCreado] = useState('')
  const [ticketConEvidencia, setTicketConEvidencia] = useState(false)
  const claveBorrador = usuario ? `nuevo-ticket-borrador:${usuario.usuario}` : ''

  const cargarFormulario = useCallback(async () => {
    setCargando(true)
    setError('')
    try {
      const [datosTicket, recursosSoporte] = await Promise.all([
        obtenerDatosNuevoTicket(),
        obtenerRecursosSoporte().catch(() => ({ formatos: [], articulos: [] })),
      ])
      setDatos(datosTicket)
      setRecursos(recursosSoporte)
    } catch (e) {
      setDatos(null)
      setError(e instanceof Error ? e.message : 'No fue posible cargar el formulario.')
    } finally {
      setCargando(false)
    }
  }, [])
  useEffect(() => {
    void cargarFormulario()
  }, [cargarFormulario])
  useEffect(() => {
    if (!claveBorrador || borradorAsistente) return
    const guardado = localStorage.getItem(claveBorrador)
    if (!guardado) return
    try {
      const { ficha: fichaGuardada, ...borrador } = JSON.parse(guardado) as Omit<NuevoTicketFormulario, 'adjuntos'> & {
        ficha?: RespuestasFicha
      }
      setFormulario(actual => ({ ...actual, ...borrador, adjuntos: [] }))
      setFicha(fichaGuardada ?? {})
      setMensaje('Se recuperó tu último borrador. Los archivos deben adjuntarse nuevamente.')
    } catch {
      localStorage.removeItem(claveBorrador)
    }
  }, [claveBorrador, borradorAsistente])

  const lineaSeleccionada = datos?.lineas.find(x => x.codigo === formulario.linea)?.descripcion ?? 'Sin seleccionar'
  const tipoSeleccionado = datos?.tipos.find(x => x.codigo === formulario.tipo)?.descripcion ?? 'Sin seleccionar'
  const documentos = useMemo(() => extraerDocumentos(`${formulario.titulo} ${formulario.detalle}`), [formulario.titulo, formulario.detalle])
  const formatosVisibles = recursos.formatos.filter(x => !x.tipoTicket || !formulario.tipo || x.tipoTicket === formulario.tipo)
  const camposFicha = useMemo(() => (datos ? camposDelTipo(datos.ficha, formulario.tipo) : []), [datos, formulario.tipo])
  const avance = avanceFicha(camposFicha, formulario.tipo, ficha)
  if (!usuario) return null

  function actualizar<K extends keyof NuevoTicketFormulario>(campo: K, valor: NuevoTicketFormulario[K]) {
    setFormulario(actual => ({ ...actual, [campo]: valor }))
    setError('')
    setTicketCreado('')
  }
  function actualizarFicha(campo: string, valor: string) {
    setFicha(actual => ({ ...actual, [campo]: valor }))
    setError('')
    setTicketCreado('')
  }
  function agregarArchivos(lista: FileList | File[]) {
    const nuevos = Array.from(lista)
    if (formulario.adjuntos.length + nuevos.length > maximoAdjuntos) {
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
      if (esVideo(archivo.name) ? archivo.size > maximoBytesVideo : archivo.size > maximoBytes) {
        setError(`El archivo ${archivo.name} supera el límite de ${esVideo(archivo.name) ? '40' : '10'} MB.`)
        return
      }
    }
    if ([...formulario.adjuntos, ...nuevos].filter(x => esVideo(x.name)).length > maximoVideos) {
      setError(`Puedes adjuntar como máximo ${maximoVideos} grabaciones de pantalla.`)
      return
    }
    setError('')
    actualizar('adjuntos', [...formulario.adjuntos, ...nuevos])
  }
  function eliminarAdjunto(indice: number) {
    actualizar(
      'adjuntos',
      formulario.adjuntos.filter((_, i) => i !== indice),
    )
  }
  function guardarBorrador(silencioso = false) {
    if (!claveBorrador) return
    const { adjuntos: _, ...borrador } = formulario
    const tieneContenido = [...Object.values(borrador), ...Object.values(ficha)].some(valor => valor.trim().length > 0)
    if (!tieneContenido) {
      if (!silencioso) setMensaje('Completa al menos un dato antes de guardar el borrador.')
      return
    }
    localStorage.setItem(claveBorrador, JSON.stringify({ ...borrador, ficha }))
    if (!silencioso) setMensaje('Borrador guardado en este equipo.')
    setError('')
  }
  function abrirAsistente() {
    guardarBorrador(true)
    navigate('/asistente')
  }
  function validarFormulario() {
    if (!formulario.titulo.trim()) return 'Ingresa el título del problema.'
    if (!formulario.linea) return 'Selecciona el sistema o módulo afectado.'
    if (!formulario.tipo) return 'Selecciona el tipo de ticket.'
    if (formulario.detalle.trim().length < 20) return 'Describe con mayor detalle el proceso realizado y el problema encontrado.'
    if (formulario.tipo === 'REQ' && formulario.adjuntos.length === 0)
      return 'Los requerimientos deben incluir al menos un archivo de sustento.'
    return validarFicha(camposFicha, formulario.tipo, ficha)
  }
  async function enviarTicket(event: React.FormEvent) {
    event.preventDefault()
    const validacion = validarFormulario()
    if (validacion) {
      setError(validacion)
      return
    }
    setEnviando(true)
    setError('')
    setMensaje('')
    try {
      const creado = await crearNuevoTicket(
        {
          ...formulario,
          titulo: formulario.titulo.trim(),
          detalle: formulario.detalle.trim(),
          mensajeError: formulario.mensajeError.trim(),
        },
        evidenciaAsistente,
        fichaJson(camposFicha, formulario.tipo, ficha),
      )
      setTicketConEvidencia(!!evidenciaAsistente)
      setTicketCreado(creado.incidenciaNumero)
      setFormulario(formularioInicial)
      setFicha({})
      if (claveBorrador) localStorage.removeItem(claveBorrador)
      if (archivoRef.current) archivoRef.current.value = ''
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No fue posible registrar el ticket.')
    } finally {
      setEnviando(false)
    }
  }
  const nombre = primerNombre(usuario.nombreCompleto)

  return (
    <MarcoPortal
      menu="usuario"
      activa="nuevoTicket"
      clase="nuevo-ticket-shell"
      ayuda={{ titulo: '¿Necesitas ayuda?', detalle: 'Consulta los recursos publicados en esta pantalla' }}
      alNavegar={ruta => (ruta === '/asistente' ? abrirAsistente() : navigate(ruta))}
      barra={
        <button
          className="inicio-buscador nuevo-ticket-buscador"
          type="button"
          onClick={abrirAsistente}
          title="Buscar con el Asistente TI"
          aria-label="Abrir el Asistente TI para buscar tickets, artículos o soluciones"
        >
          <Icono nombre="buscar" size={19} />
          <span className="nuevo-ticket-buscador__texto">Buscar tickets, artículos o soluciones...</span>
          <Icono nombre="asistente" size={17} />
        </button>
      }
    >
      <main className="nuevo-ticket-contenido">
        <section className="nuevo-ticket-hero">
          <div>
            <h1>Nuevo Ticket</h1>
            <p>Describe tu incidencia con claridad y adjunta evidencia para acelerar la atención.</p>
            <div className="nuevo-ticket-modos">
              <button className="nuevo-ticket-modo nuevo-ticket-modo--activo">
                <Icono nombre="archivoLineas" size={17} /> Formulario
              </button>
              <button className="nuevo-ticket-modo" type="button" onClick={abrirAsistente}>
                <Icono nombre="asistente" size={17} /> Asistente guiado
              </button>
            </div>
          </div>
          <div className="nuevo-ticket-hero__datos">
            <span>
              <Icono nombre="reloj" size={17} /> Tiempo estimado: 2 min
            </span>
            <span>
              <Icono nombre="clip" size={17} /> Adjuntos: imagen, PDF, Excel, video
            </span>
          </div>
        </section>
        {cargando && <section className="nuevo-ticket-cargando">Cargando formulario...</section>}
        {!cargando && !datos && (
          <section className="nuevo-ticket-cargando nuevo-ticket-cargando--error" role="alert">
            <strong>No pudimos preparar el formulario.</strong>
            <span>{error || 'Comprueba la conexión e intenta nuevamente.'}</span>
            <button type="button" onClick={() => void cargarFormulario()}>
              Reintentar
            </button>
          </section>
        )}
        {!cargando && datos && (
          <form className="nuevo-ticket-layout" onSubmit={enviarTicket}>
            <section className="nuevo-ticket-formulario">
              <header className="nuevo-ticket-seccion-titulo">
                <span>
                  <Icono nombre="archivoLineas" />
                </span>
                <div>
                  <h2>Registrar incidencia</h2>
                  <p>Completa la información necesaria para que podamos atender tu ticket correctamente.</p>
                </div>
              </header>
              <label className="nuevo-ticket-campo nuevo-ticket-campo--completo">
                <span>
                  Título del problema <b>*</b>
                </span>
                <input
                  value={formulario.titulo}
                  maxLength={250}
                  onChange={e => actualizar('titulo', e.target.value)}
                  placeholder="Ej. No puedo generar la orden de compra 260091"
                />
              </label>
              <div className="nuevo-ticket-fila">
                <label className="nuevo-ticket-campo">
                  <span>Área</span>
                  <div className="nuevo-ticket-solo-lectura">
                    <Icono nombre="edificio" size={17} /> {datos.areaDescripcion}
                  </div>
                </label>
                <label className="nuevo-ticket-campo">
                  <span>
                    Sistema / Módulo <b>*</b>
                  </span>
                  <select value={formulario.linea} onChange={e => actualizar('linea', e.target.value)}>
                    <option value="">Selecciona una opción</option>
                    {datos.lineas.map(x => (
                      <option key={x.codigo} value={x.codigo}>
                        {x.descripcion}
                      </option>
                    ))}
                  </select>
                </label>
              </div>
              <label className="nuevo-ticket-campo nuevo-ticket-campo--mitad">
                <span>
                  Tipo de ticket <b>*</b>
                </span>
                <select value={formulario.tipo} onChange={e => actualizar('tipo', e.target.value)}>
                  <option value="">Selecciona una opción</option>
                  {datos.tipos.map(x => (
                    <option key={x.codigo} value={x.codigo}>
                      {x.descripcion}
                    </option>
                  ))}
                </select>
              </label>
              <label className="nuevo-ticket-campo nuevo-ticket-campo--completo">
                <span>Mensaje de error</span>
                <div className="nuevo-ticket-input-icono">
                  <Icono nombre="info" size={17} />
                  <input
                    value={formulario.mensajeError}
                    maxLength={1000}
                    onChange={e => actualizar('mensajeError', e.target.value)}
                    placeholder="Ingresa el mensaje exacto si aparece en pantalla"
                  />
                </div>
              </label>
              <div className="nuevo-ticket-campo nuevo-ticket-campo--completo">
                <span>
                  Descripción detallada <b>*</b>
                </span>
                <div className="nuevo-ticket-ayuda">
                  <Icono nombre="info" size={20} />
                  <p>
                    <strong>Describe qué proceso estabas realizando y qué documentos están involucrados o presentan el error.</strong>
                    <br />
                    Incluye el paso a paso, pantallas, datos relevantes y cualquier información que nos ayude a replicar el problema.
                  </p>
                </div>
                <textarea
                  value={formulario.detalle}
                  maxLength={1000}
                  onChange={e => actualizar('detalle', e.target.value)}
                  placeholder="Describe lo ocurrido con el mayor contexto posible..."
                />
                <small className="nuevo-ticket-contador">{formulario.detalle.length}/1000</small>
              </div>
              {camposFicha.length > 0 && (
                <FichaTicket
                  campos={camposFicha}
                  respuestas={ficha}
                  alCambiar={actualizarFicha}
                  tipo={formulario.tipo}
                  titulo={formulario.titulo}
                  detalle={formulario.detalle}
                />
              )}
              <div className="nuevo-ticket-adjuntos">
                <div className="nuevo-ticket-adjuntos__cabecera">
                  <strong>
                    <Icono nombre="clip" size={18} /> Adjuntar evidencia
                  </strong>
                  <span>Hasta 5 archivos: documentos de 10 MB y hasta 2 videos de 40 MB</span>
                </div>
                <div
                  className={`nuevo-ticket-dropzone ${arrastrando ? 'nuevo-ticket-dropzone--activo' : ''}`}
                  role="button"
                  tabIndex={0}
                  onClick={() => archivoRef.current?.click()}
                  onKeyDown={e => {
                    if (e.key === 'Enter' || e.key === ' ') archivoRef.current?.click()
                  }}
                  onDragOver={e => {
                    e.preventDefault()
                    setArrastrando(true)
                  }}
                  onDragLeave={() => setArrastrando(false)}
                  onDrop={e => {
                    e.preventDefault()
                    setArrastrando(false)
                    agregarArchivos(e.dataTransfer.files)
                  }}
                >
                  <Icono nombre="subir" size={28} />
                  <strong>Arrastra archivos aquí o haz clic para adjuntar</strong>
                  <span>Imagen, PDF, Excel o video de tu pantalla (WebM, MP4)</span>
                  <input
                    ref={archivoRef}
                    type="file"
                    multiple
                    accept=".png,.jpg,.jpeg,.webp,.pdf,.xls,.xlsx,.webm,.mp4"
                    onChange={e => e.target.files && agregarArchivos(e.target.files)}
                    hidden
                  />
                </div>
                {formulario.adjuntos.length > 0 && (
                  <div className="nuevo-ticket-archivos">
                    {formulario.adjuntos.map((archivo, indice) => (
                      <div className="nuevo-ticket-archivo" key={`${archivo.name}-${indice}`}>
                        <span className="nuevo-ticket-archivo__icono">
                          <Icono nombre="archivoLineas" size={18} />
                        </span>
                        <div>
                          <strong>{archivo.name}</strong>
                          <small>{formatearTamano(archivo.size)}</small>
                        </div>
                        <button type="button" onClick={() => eliminarAdjunto(indice)}>
                          <Icono nombre="cerrarPequeno" size={16} />
                        </button>
                      </div>
                    ))}
                  </div>
                )}
              </div>
              {error && <div className="nuevo-ticket-alerta nuevo-ticket-alerta--error">{error}</div>}
              {mensaje && <div className="nuevo-ticket-alerta">{mensaje}</div>}
              {ticketCreado && (
                <div className="nuevo-ticket-exito">
                  <Icono nombre="check" size={22} />
                  <div>
                    <strong>Ticket {ticketCreado} registrado correctamente.</strong>
                    <span>
                      {ticketConEvidencia
                        ? 'TI ya lo recibió con la grabación y los pasos que mostraste; el agente de TI está investigándolo y avisará al responsable.'
                        : 'Ya se encuentra disponible para revisión de TI.'}
                    </span>
                  </div>
                  <button type="button" onClick={() => navigate(`/mis-tickets?ticket=${encodeURIComponent(ticketCreado)}`)}>
                    Ver ticket
                  </button>
                </div>
              )}
              <footer className="nuevo-ticket-formulario__pie">
                <span className="nuevo-ticket-nota">
                  Los cambios técnicos se gestionarán después del registro; aquí solo necesitamos el contexto real del problema.
                </span>
                <div>
                  <button className="nuevo-ticket-boton nuevo-ticket-boton--secundario" type="button" onClick={() => guardarBorrador()}>
                    <Icono nombre="guardar" size={17} /> Guardar borrador
                  </button>
                  <button className="nuevo-ticket-boton nuevo-ticket-boton--principal" type="submit" disabled={enviando}>
                    <Icono nombre="enviar" size={17} /> {enviando ? 'Enviando...' : 'Enviar incidencia'}
                  </button>
                </div>
              </footer>
            </section>
            <aside className="nuevo-ticket-panel">
              <section className="nuevo-ticket-resumen">
                <header className="nuevo-ticket-seccion-titulo">
                  <span>
                    <Icono nombre="archivoLineas" />
                  </span>
                  <div>
                    <h2>Resumen del ticket</h2>
                    <p>Revisa la información que se enviará con tu ticket.</p>
                  </div>
                </header>
                <dl>
                  <div>
                    <dt>
                      <Icono nombre="usuario" size={18} /> Usuario solicitante
                    </dt>
                    <dd>{nombre}</dd>
                  </div>
                  <div>
                    <dt>
                      <Icono nombre="edificio" size={18} /> Área
                    </dt>
                    <dd>{datos.areaDescripcion}</dd>
                  </div>
                  <div>
                    <dt>
                      <Icono nombre="modulo" size={18} /> Módulo
                    </dt>
                    <dd>{lineaSeleccionada}</dd>
                  </div>
                  <div>
                    <dt>
                      <Icono nombre="tipo" size={18} /> Tipo de ticket
                    </dt>
                    <dd>{tipoSeleccionado}</dd>
                  </div>
                  {avance.total > 0 && (
                    <div>
                      <dt>
                        <Icono nombre="lista" size={18} /> Ficha
                      </dt>
                      <dd>
                        {avance.respondidos} de {avance.total} respuestas
                      </dd>
                    </div>
                  )}
                  <div>
                    <dt>
                      <Icono nombre="archivoLineas" size={18} /> Documentos mencionados
                    </dt>
                    <dd>{documentos.length > 0 ? documentos.join(', ') : 'Aún no identificados'}</dd>
                  </div>
                  <div>
                    <dt>
                      <Icono nombre="clip" size={18} /> Evidencias adjuntas
                    </dt>
                    <dd>
                      {formulario.adjuntos.length} {formulario.adjuntos.length === 1 ? 'archivo' : 'archivos'}
                    </dd>
                  </div>
                  <div>
                    <dt>
                      <Icono nombre="reloj" size={18} /> Estado inicial
                    </dt>
                    <dd>
                      <span className="nuevo-ticket-estado">Pendiente de revisión</span>
                    </dd>
                  </div>
                </dl>
              </section>
              {(formatosVisibles.length > 0 || recursos.articulos.length > 0) && (
                <section className="nuevo-ticket-recursos">
                  <header>
                    <span>
                      <Icono nombre="luz" />
                    </span>
                    <div>
                      <h2>Recursos de soporte</h2>
                      <p>Formatos y ayuda publicados por TI.</p>
                    </div>
                  </header>
                  {formatosVisibles.length > 0 && (
                    <div className="nuevo-ticket-recursos__lista">
                      <strong>Formatos frecuentes</strong>
                      {formatosVisibles.map(x => (
                        <a key={x.formatoCodigo} href={urlFormatoSoporte(x.formatoCodigo)}>
                          <Icono nombre="descargar" size={16} />
                          <span>
                            {x.titulo}
                            <small>{x.descripcion || x.nombreOriginal}</small>
                          </span>
                        </a>
                      ))}
                    </div>
                  )}
                  {recursos.articulos.length > 0 && (
                    <div className="nuevo-ticket-recursos__lista">
                      <strong>Ayuda rápida</strong>
                      {recursos.articulos.slice(0, 5).map(x => (
                        <details key={x.conocimientoCodigo}>
                          <summary>{x.titulo}</summary>
                          <p>{x.solucion}</p>
                          {x.procedimiento && <small>{x.procedimiento}</small>}
                        </details>
                      ))}
                    </div>
                  )}
                </section>
              )}
              <section className="nuevo-ticket-recomendaciones">
                <header>
                  <span>
                    <Icono nombre="luz" />
                  </span>
                  <div>
                    <h2>Recomendaciones antes de enviar</h2>
                    <p>Sigue estas recomendaciones para una atención más rápida.</p>
                  </div>
                </header>
                <ol>
                  <li>
                    <b>1</b>
                    <div>
                      <strong>Incluye el número de documentos</strong>
                      <span>Orden de compra, solicitud, pedido, factura, etc.</span>
                    </div>
                  </li>
                  <li>
                    <b>2</b>
                    <div>
                      <strong>Adjunta captura del error</strong>
                      <span>Nos ayuda a entender mejor el problema.</span>
                    </div>
                  </li>
                  <li>
                    <b>3</b>
                    <div>
                      <strong>Describe qué proceso estabas realizando</strong>
                      <span>Indica los pasos previos al error.</span>
                    </div>
                  </li>
                  <li>
                    <b>4</b>
                    <div>
                      <strong>Indica si el problema bloquea tu trabajo</strong>
                      <span>Esto nos ayuda a priorizar la atención.</span>
                    </div>
                  </li>
                </ol>
              </section>
              <section className="nuevo-ticket-asistente">
                <span>
                  <Icono nombre="audifonos" size={25} />
                </span>
                <div>
                  <strong>¿Prefieres ayuda paso a paso?</strong>
                  <p>Consulta una solución antes de registrar el caso.</p>
                </div>
                <button type="button" onClick={abrirAsistente}>
                  Abrir asistente
                </button>
              </section>
            </aside>
          </form>
        )}
      </main>
    </MarcoPortal>
  )
}
