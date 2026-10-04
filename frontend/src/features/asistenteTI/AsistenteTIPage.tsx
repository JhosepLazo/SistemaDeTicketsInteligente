/**
 * Archivo: AsistenteTIPage.tsx
 * Objetivo: Proporcionar la consola del Agente de Ingeniería Autónomo para operadores TI.
 * Responsabilidad: Iniciar o retomar investigaciones, observar/reproducir mediante Live, mostrar evidencia, diagnóstico, expediente,
 *   herramientas de investigación sin cambios y la decisión human-in-the-loop; además aloja las consultas operativas del Asistente TI.
 * Consideraciones: La pantalla compartida es evidencia de usuario; los cambios solo se solicitan después del diagnóstico y nunca se ejecutan desde el cliente.
 */

import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import AsistenteTIConversacion from './AsistenteTIConversacion'
import { useAutenticacion } from '../autenticacion/AutenticacionContext'
import {
  analizarInvestigacionTI,
  buscarCodigoInvestigacionTI,
  cancelarInvestigacionTI,
  cancelarInvitacionReproduccionTI,
  comprobarInvestigacionTI,
  crearConocimientoInvestigacionTI,
  crearInvestigacionTI,
  crearTokenLiveTI,
  finalizarObservacionTI,
  grabarInformacionTI,
  invitarUsuarioReproduccionTI,
  listarInvestigacionesTI,
  obtenerCatalogosAgenteTI,
  obtenerDiagnosticoTI,
  obtenerInvestigacionTI,
  realizarCambioTI,
  reasignarInvestigacionTI,
  reabrirObservacionTI,
  registrarEventoInvestigacionTI,
  simularCambioTI,
  subirGrabacionTI,
  urlGrabacionTI,
  validarSolucionInvestigacionTI,
  vincularIncidenciaTI,
  type AgenteCodigoReferencia,
  type AgenteComprobacion,
  type AgenteTICatalogos,
  type AgenteTIContextoInvestigacion,
  type AgenteTIDiagnosticoRespuesta,
  type AgenteTISesion,
  type AgenteTISimulacionRespuesta,
} from '../../services/asistenteTIApi'
import { GeminiLiveSesion } from '../../services/geminiLiveApi'
import { GrabadorPantalla } from '../../services/grabadorPantallaService'
import { registrarAvanceDetalladoTI } from '../../services/gestionOperativaTIApi'
import { resolverTicketTI } from '../../services/gestionTicketsTIApi'
import { seleccionarSesionTraza } from '../../services/api'
import MarcoPortal from '../../components/MarcoPortal'
import IconoBase, { type PropsIcono } from '../../components/Icono'
import './AsistenteTIPage.css'

// Los íconos de la consola fluyen con el texto (sin la clase inicio-icono).
const Icono = (props: PropsIcono) => <IconoBase enLinea {...props} />

type Transcripcion = { rol: 'usuario' | 'agente'; texto: string }
const claveSesionActiva = 'calimod.agente.sesionActiva.v1'
const estadosObservacion = ['RECOPILANDO', 'OBSERVANDO', 'LISTO_INVESTIGAR']
const estadosFinales = ['INFORME_GRABADO', 'CAMBIO_VALIDADO', 'CANCELADO']
const estadosDecision = ['PENDIENTE_TI', 'PENDIENTE_APROBACION', 'SIN_EJECUTOR']
const estadosSimulacion = ['PENDIENTE_TI', 'PENDIENTE_APROBACION', 'LISTO_EJECUCION', 'ERROR_EJECUCION']
const modoDiagnostico = (modo: string) =>
  modo === 'AGENTE' ? 'Agente con herramientas' : modo === 'IA' ? 'Análisis de una llamada' : 'Sin modelo de IA'
const etiquetaEventoLive = (tipo: string, fuente: string) => {
  const usuarioFinal = fuente === 'LIVE_USUARIO'
  if (tipo === 'PASO_OBSERVADO') return usuarioFinal ? 'Paso observado (usuario final)' : 'Paso observado'
  if (tipo === 'TRANSCRIPCION_USUARIO') return usuarioFinal ? 'Usuario final' : 'Usuario'
  return usuarioFinal ? 'Asistente del usuario' : 'Agente'
}
const estadosCancelables = [...estadosObservacion, ...estadosDecision]
const estadosReasignables = [...estadosCancelables, 'ERROR_EJECUCION']
const estadosTicketCerrados = ['PV', 'RS', 'CA', 'NP', 'CF', 'PA']
type Resolucion = {
  causaRaiz: string
  solucion: string
  respuestaUsuario: string
  tipoResolucion: string
  registrarAvance: boolean
  minutos: number
  areaCausante: string
}
const fuentesUsuarioFinal = ['LIVE_USUARIO', 'USUARIO_FINAL']
const esTranscripcion = (tipo: string) => tipo === 'TRANSCRIPCION_USUARIO' || tipo === 'TRANSCRIPCION_AGENTE'
const patronError = /error|fall|excep|no puede|no se puede|incorrect|denegad|no encontr/i
const codigoSesion = (numero: number) => `AGT-${String(numero).padStart(6, '0')}`
const mensajeError = (e: unknown, defecto: string) => (e instanceof Error ? e.message : defecto)
const fechaCorta = (valor: string) => new Date(valor).toLocaleString('es-PE', { dateStyle: 'short', timeStyle: 'short' })

function leerSesionGuardada() {
  try {
    const v = sessionStorage.getItem(claveSesionActiva)
    return v && /^\d+$/.test(v) ? Number(v) : null
  } catch {
    return null
  }
}
function guardarSesionActiva(numero: number | null) {
  try {
    if (numero) sessionStorage.setItem(claveSesionActiva, String(numero))
    else sessionStorage.removeItem(claveSesionActiva)
  } catch {
    /* Almacenamiento opcional. */
  }
}

export default function AsistenteTIPage() {
  const navigate = useNavigate()
  const [parametros, setParametros] = useSearchParams()
  const { usuario } = useAutenticacion()
  const [vista, setVista] = useState<'investigacion' | 'consulta'>('investigacion')
  const [historial, setHistorial] = useState<AgenteTISesion[] | null>(null)
  const [incidencia, setIncidencia] = useState('')
  const [descripcion, setDescripcion] = useState('')
  const [sesion, setSesion] = useState<AgenteTISesion | null>(null)
  const [contexto, setContexto] = useState<AgenteTIContextoInvestigacion | null>(null)
  const [diagnostico, setDiagnostico] = useState<AgenteTIDiagnosticoRespuesta | null>(null)
  const [transcripciones, setTranscripciones] = useState<Transcripcion[]>([])
  const transcripcionesRef = useRef<Transcripcion[]>([])
  const sesionRef = useRef<AgenteTISesion | null>(null)
  const [errorObservado, setErrorObservado] = useState('')
  const [errorRegistrado, setErrorRegistradoEstado] = useState('')
  const errorRegistradoRef = useRef('')
  const setErrorRegistrado = (texto: string) => {
    errorRegistradoRef.current = texto
    setErrorRegistradoEstado(texto)
  }
  const [textoLive, setTextoLive] = useState('')
  const [microSilenciado, setMicroSilenciado] = useState(false)
  const [liveActivo, setLiveActivo] = useState(false)
  const [liveEstado, setLiveEstado] = useState('Listo para iniciar observación')
  const [vinculo, setVinculo] = useState('')
  const [comprobaciones, setComprobaciones] = useState<AgenteComprobacion[] | null>(null)
  const [simulacion, setSimulacion] = useState<AgenteTISimulacionRespuesta | null>(null)
  const [codigo, setCodigo] = useState<AgenteCodigoReferencia[] | null>(null)
  const [verInforme, setVerInforme] = useState(false)
  const [alcanceEquipo, setAlcanceEquipo] = useState(false)
  const [catalogos, setCatalogos] = useState<AgenteTICatalogos | null>(null)
  const [operadorDestino, setOperadorDestino] = useState('')
  const [resolucion, setResolucion] = useState<Resolucion | null>(null)
  const montadoRef = useRef(false)
  const [procesando, setProcesando] = useState(false)
  const [error, setError] = useState('')
  const [mensaje, setMensaje] = useState('')
  const videoRef = useRef<HTMLVideoElement>(null)
  const liveRef = useRef<GeminiLiveSesion | null>(null)
  const grabadorRef = useRef<GrabadorPantalla | null>(null)
  const [grabarSesion, setGrabarSesion] = useState(true)

  useEffect(
    () => () => {
      void liveRef.current?.detener()
    },
    [],
  )

  // ?sesion= llega desde notificaciones o desde Gestión de Tickets; ?incidencia= prepara una investigación nueva para ese ticket.
  // Sin parámetros, al montar se retoma la investigación abierta antes de recargar.
  const sesionParam = parametros.get('sesion')
  const incidenciaParam = parametros.get('incidencia')
  useEffect(() => {
    if (sesionParam && /^\d+$/.test(sesionParam)) void abrirSesion(Number(sesionParam))
    else if (incidenciaParam) void prepararDesdeTicket(incidenciaParam)
    else if (!montadoRef.current) {
      const guardada = leerSesionGuardada()
      if (guardada) void abrirSesion(guardada)
      else void cargarHistorial()
    }
    montadoRef.current = true
    // Las funciones se recrean en cada render; solo deben reaccionar a los parámetros de la URL.
  }, [sesionParam, incidenciaParam])

  // La traza técnica solo se correlaciona mientras la investigación está en etapa de observación.
  useEffect(() => {
    sesionRef.current = sesion
    seleccionarSesionTraza(sesion && sesion.esPropietario && estadosObservacion.includes(sesion.estado) ? sesion.sesionNumero : null)
    guardarSesionActiva(sesion && !estadosFinales.includes(sesion.estado) ? sesion.sesionNumero : null)
  }, [sesion])

  // Mientras el usuario está invitado, la consola consulta su evidencia periódicamente.
  const invitacionVigente =
    !!sesion &&
    sesion.esPropietario &&
    estadosObservacion.includes(sesion.estado) &&
    (sesion.estadoInvitacion === 'PENDIENTE' || sesion.estadoInvitacion === 'ACEPTADA')
  useEffect(() => {
    if (!invitacionVigente) return
    const intervalo = window.setInterval(() => {
      void refrescar().catch(() => undefined)
    }, 8000)
    return () => window.clearInterval(intervalo)
  }, [invitacionVigente, sesion?.sesionNumero])

  const progreso = useMemo(() => obtenerProgreso(sesion?.estado || ''), [sesion?.estado])
  if (!usuario) return null
  const estado = sesion?.estado || ''
  const enObservacion = estadosObservacion.includes(estado)
  const finalizada = estadosFinales.includes(estado)
  const esSupervisor = ['SUP', 'ADM'].includes(usuario.perfil)
  const puedeActuar = !!sesion?.esPropietario
  const estadoTicket = contexto?.ticket.estado || ''
  const eventosUsuarioFinal = contexto?.eventos.filter(x => fuentesUsuarioFinal.includes(x.fuente)) ?? []
  const hayTranscripcionServidor = !!contexto?.eventos.some(x => esTranscripcion(x.tipo))
  const puedeEnviarSolucion =
    puedeActuar &&
    ['CAMBIO_VALIDADO', 'INFORME_GRABADO'].includes(estado) &&
    !!sesion?.incidenciaNumero &&
    !!estadoTicket &&
    !estadosTicketCerrados.includes(estadoTicket)
  const accionCatalogo = diagnostico?.accion ? contexto?.acciones.find(x => x.accionCodigo === diagnostico.accion?.accionCodigo) : undefined
  const grabaciones = contexto?.eventos.filter(x => x.tipo === 'GRABACION_PANTALLA' && x.origenServidor) ?? []
  const puedeTomar = !puedeActuar && !!sesion && (!!sesion.puedeTomar || (esSupervisor && estadosReasignables.includes(estado)))
  // Explica por qué Live e Investigar están deshabilitados y ofrece la salida cuando existe.
  const puedeReabrir = puedeActuar && ['PENDIENTE_TI', 'SIN_EJECUTOR'].includes(estado)
  const motivoBloqueo = !puedeActuar
    ? puedeTomar
      ? 'Solo el responsable de la investigación puede observar e investigar. Usa "Tomar investigación" para continuar tú.'
      : 'Solo el responsable de la investigación puede observar e investigar. Pide a un supervisor que te la reasigne.'
    : finalizada
      ? 'La investigación está cerrada: no admite más observación ni un nuevo diagnóstico.'
      : !enObservacion && puedeReabrir
        ? 'El diagnóstico ya fue generado, por eso Live e Investigar están bloqueados. Para reunir más evidencia (otra reproducción, otra grabación), reabre la observación.'
        : !enObservacion
          ? 'Hay una decisión de cambio en curso; la observación no puede reabrirse hasta que termine.'
          : ''

  async function cargarHistorial(equipo = alcanceEquipo) {
    try {
      setHistorial(null)
      setHistorial(await listarInvestigacionesTI(equipo))
    } catch (e) {
      setHistorial([])
      setError(mensajeError(e, 'No fue posible cargar el historial de investigaciones.'))
    }
  }

  function cambiarAlcance(equipo: boolean) {
    setAlcanceEquipo(equipo)
    void cargarHistorial(equipo)
  }

  async function asegurarCatalogos() {
    if (catalogos) return catalogos
    const cargados = await obtenerCatalogosAgenteTI()
    setCatalogos(cargados)
    return cargados
  }

  async function prepararDesdeTicket(numero: string) {
    await nuevaInvestigacion()
    setVista('investigacion')
    setIncidencia(numero.trim().toUpperCase().slice(0, 12))
    setParametros({}, { replace: true })
    setMensaje(
      `Ticket ${numero.toUpperCase()} listo para investigar. Describe el problema e inicia la investigación; revisa abajo si ya existe una en curso.`,
    )
  }

  async function cargarContexto(numero: number) {
    const actual = await obtenerInvestigacionTI(numero)
    setContexto(actual)
    setSesion(actual.sesion)
    setDiagnostico(actual.sesion.informeDisponible ? await obtenerDiagnosticoTI(numero) : null)
    return actual
  }

  async function abrirSesion(numero: number) {
    setProcesando(true)
    setError('')
    setMensaje('')
    limpiarHerramientas()
    try {
      const actual = await cargarContexto(numero)
      const turnos = actual.eventos
        .filter(x => x.tipo === 'TRANSCRIPCION_USUARIO' || x.tipo === 'TRANSCRIPCION_AGENTE')
        .map(x => ({ rol: x.tipo === 'TRANSCRIPCION_USUARIO' ? 'usuario' : 'agente', texto: x.contenido }) as Transcripcion)
      transcripcionesRef.current = turnos
      setTranscripciones(turnos)
      const ultimoError =
        actual.sesion.errorObservado || [...actual.eventos].reverse().find(x => x.tipo === 'ERROR_OBSERVADO')?.contenido || ''
      setErrorRegistrado(ultimoError)
      setErrorObservado(ultimoError)
      if (parametros.has('sesion')) setParametros({}, { replace: true })
      if (esSupervisor) void asegurarCatalogos().catch(() => undefined)
    } catch (e) {
      guardarSesionActiva(null)
      setSesion(null)
      setContexto(null)
      setError(mensajeError(e, 'No fue posible abrir la investigación.'))
      void cargarHistorial()
    } finally {
      setProcesando(false)
    }
  }

  async function refrescar() {
    const numero = sesionRef.current?.sesionNumero
    if (numero) await cargarContexto(numero)
  }

  function limpiarHerramientas() {
    setComprobaciones(null)
    setSimulacion(null)
    setCodigo(null)
    setVerInforme(false)
    setVinculo('')
    setResolucion(null)
    setOperadorDestino('')
  }

  async function ejecutar(accion: () => Promise<void>, defecto: string) {
    if (procesando) return
    setProcesando(true)
    setError('')
    setMensaje('')
    try {
      await accion()
    } catch (e) {
      setError(mensajeError(e, defecto))
    } finally {
      setProcesando(false)
    }
  }

  const iniciarInvestigacion = () =>
    descripcion.trim().length >= 5 &&
    ejecutar(async () => {
      const creada = await crearInvestigacionTI(incidencia, descripcion.trim())
      transcripcionesRef.current = []
      setTranscripciones([])
      setErrorObservado('')
      setErrorRegistrado('')
      limpiarHerramientas()
      await cargarContexto(creada.sesionNumero)
      setMensaje(
        `Investigación ${codigoSesion(creada.sesionNumero)} creada. Reproduce el problema por Live o investiga con la evidencia del ticket.`,
      )
    }, 'No fue posible iniciar la investigación.')

  function agregarTranscripcion(rol: 'usuario' | 'agente', texto: string) {
    const limpio = texto.trim()
    const actual = sesionRef.current
    if (!limpio) return
    const nueva = [...transcripcionesRef.current, { rol, texto: limpio }]
    transcripcionesRef.current = nueva
    setTranscripciones(nueva)
    if (actual)
      void registrarEventoInvestigacionTI(
        actual.sesionNumero,
        rol === 'usuario' ? 'TRANSCRIPCION_USUARIO' : 'TRANSCRIPCION_AGENTE',
        'LIVE',
        limpio.slice(0, 12000),
      ).catch(() => setLiveEstado('Live activo · una transcripción no pudo registrarse; continúa la observación'))
  }

  // El modelo Live registra pasos y el error exacto mediante funciones; solo se guarda evidencia, nunca se cambia nada.
  async function atenderFuncionLive(nombre: string, argumentos: Record<string, unknown>) {
    const actual = sesionRef.current
    if (!actual) return 'No hay una investigación activa.'
    const texto = String(argumentos.descripcion ?? argumentos.mensaje ?? '')
      .trim()
      .slice(0, 1000)
    if (!texto) return 'La descripción está vacía; no se registró.'
    if (nombre === 'registrar_paso') {
      await registrarEventoInvestigacionTI(actual.sesionNumero, 'PASO_OBSERVADO', 'LIVE', texto, JSON.stringify({ origen: 'funcion_live' }))
      return 'Paso registrado como evidencia.'
    }
    if (nombre === 'registrar_error') {
      await registrarEventoInvestigacionTI(
        actual.sesionNumero,
        'ERROR_OBSERVADO',
        'LIVE',
        texto,
        JSON.stringify({ origen: 'funcion_live' }),
      )
      setErrorRegistrado(texto)
      setErrorObservado(texto)
      setLiveEstado('Live activo · el agente registró el error observado')
      return 'Error registrado como evidencia.'
    }
    return 'Función no disponible.'
  }

  // La grabación de la pantalla queda en la investigación (y como adjunto del ticket) para revisarla y para que el agente la analice.
  async function guardarGrabacion(numero: number) {
    const grabador = grabadorRef.current
    grabadorRef.current = null
    if (!grabador) return
    const video = await grabador.detener()
    if (!video) return
    try {
      await subirGrabacionTI(numero, video, grabador.segundos)
    } catch (e) {
      setError(mensajeError(e, 'No se pudo guardar la grabación de la pantalla.'))
    }
  }

  async function iniciarLive() {
    if (!sesion || liveActivo || procesando) return
    setProcesando(true)
    setError('')
    setMensaje('')
    try {
      const token = await crearTokenLiveTI(sesion.sesionNumero)
      if (!token.disponible) throw new Error(token.mensaje)
      await registrarEventoInvestigacionTI(
        sesion.sesionNumero,
        'INICIO_LIVE',
        'LIVE',
        'El operador inició una sesión multimodal para observar la reproducción del problema.',
      )
      const live = new GeminiLiveSesion({
        onEstado: setLiveEstado,
        onTranscripcion: agregarTranscripcion,
        onFuncion: atenderFuncionLive,
        onError: setError,
        onPantallaFinalizada: () => void cerrarObservacion(false),
        onDesconexion: () => {
          const actual = sesionRef.current
          if (actual)
            void guardarGrabacion(actual.sesionNumero)
              .then(() => refrescar())
              .catch(() => undefined)
          liveRef.current = null
          setLiveActivo(false)
          if (videoRef.current) videoRef.current.srcObject = null
          setLiveEstado(
            'Gemini cerró la sesión Live (límite de tiempo o red). La transcripción se conservó; puedes reanudar o finalizar la reproducción.',
          )
        },
      })
      liveRef.current = live
      const stream = await live.iniciar(token)
      if (videoRef.current) {
        videoRef.current.srcObject = stream
        void videoRef.current.play()
      }
      if (grabarSesion) {
        const grabador = new GrabadorPantalla()
        if (grabador.iniciar(stream)) grabadorRef.current = grabador
      }
      setMicroSilenciado(false)
      setLiveActivo(true)
      setLiveEstado(
        live.tieneMicrofono
          ? 'Live activo · reproduce el proceso hasta el error'
          : 'Live activo sin micrófono · escribe al agente desde el campo de texto',
      )
      await refrescar()
    } catch (e) {
      setError(mensajeError(e, 'No fue posible iniciar Live.'))
      await liveRef.current?.detener()
      liveRef.current = null
      setLiveActivo(false)
    } finally {
      setProcesando(false)
    }
  }

  function enviarTextoLive() {
    const texto = textoLive.trim()
    if (!texto || !liveRef.current) return
    liveRef.current.enviarTexto(texto)
    agregarTranscripcion('usuario', texto)
    setTextoLive('')
  }

  function alternarMicrofono() {
    const nuevo = !microSilenciado
    liveRef.current?.silenciarMicrofono(nuevo)
    setMicroSilenciado(nuevo)
  }

  const marcarError = () => {
    const texto = (
      errorObservado.trim() ||
      [...transcripcionesRef.current].reverse().find(x => x.rol === 'usuario' && patronError.test(x.texto))?.texto ||
      ''
    ).slice(0, 1000)
    if (!sesion || !texto) {
      setError('Escribe el mensaje de error que muestra la pantalla para registrarlo como evidencia.')
      return
    }
    return ejecutar(async () => {
      await registrarEventoInvestigacionTI(sesion.sesionNumero, 'ERROR_OBSERVADO', 'USUARIO', texto)
      setErrorRegistrado(texto)
      setErrorObservado(texto)
      setMensaje('Error observado registrado como evidencia de la investigación.')
      await refrescar()
    }, 'No fue posible registrar el error observado.')
  }

  async function cerrarObservacion(detener = true) {
    const actual = sesionRef.current
    if (!actual) return
    if (detener) await liveRef.current?.detener()
    liveRef.current = null
    setLiveActivo(false)
    if (videoRef.current) videoRef.current.srcObject = null
    try {
      await guardarGrabacion(actual.sesionNumero)
      await registrarEventoInvestigacionTI(
        actual.sesionNumero,
        'FIN_LIVE',
        'LIVE',
        'La etapa de observación finalizó y el agente continuará con investigación técnica.',
      )
      // Se usa lo registrado en el servidor: incluye la reproducción del usuario final, no solo la conversación de esta consola.
      const registrado = await obtenerInvestigacionTI(actual.sesionNumero)
      const turnos = registrado.eventos.filter(x => esTranscripcion(x.tipo))
      const lineas = registrado.eventos.filter(x => esTranscripcion(x.tipo) || x.tipo === 'PASO_OBSERVADO')
      const delUsuario = lineas.some(x => x.fuente === 'LIVE_USUARIO')
      const proceso = lineas.map((x, i) => `${i + 1}. ${etiquetaEventoLive(x.tipo, x.fuente)}: ${x.contenido}`).join('\n')
      const error =
        errorRegistradoRef.current ||
        [...registrado.eventos].reverse().find(x => x.tipo === 'ERROR_OBSERVADO')?.contenido ||
        [...turnos].reverse().find(x => x.tipo === 'TRANSCRIPCION_USUARIO' && patronError.test(x.contenido))?.contenido ||
        ''
      await finalizarObservacionTI(
        actual.sesionNumero,
        `Observación finalizada con ${turnos.length} turnos registrados${delUsuario ? ', incluida la reproducción del usuario final desde su portal' : ''}.`,
        proceso || 'No se obtuvo transcripción; la pantalla fue compartida durante la sesión.',
        error.slice(0, 1000),
      )
      await refrescar()
      setLiveEstado('Observación finalizada · lista para investigar')
      setMensaje('La reproducción humana terminó. El agente ya puede analizar ticket, conocimiento, auditoría y telemetría disponible.')
    } catch (e) {
      setError(mensajeError(e, 'No fue posible finalizar la observación.'))
    }
  }

  function confirmarCierreConUsuario() {
    return (
      sesion?.estadoInvitacion !== 'ACEPTADA' ||
      window.confirm(
        `${sesion.nombreInvitado} todavía puede estar mostrando el error. Al cerrar la observación ya no podrá enviar más evidencia. ¿Continuar?`,
      )
    )
  }

  const investigar = () => {
    if (!confirmarCierreConUsuario()) return
    return ejecutar(async () => {
      if (liveActivo || (estado !== 'LISTO_INVESTIGAR' && hayTranscripcionServidor)) await cerrarObservacion()
      const r = await analizarInvestigacionTI(sesion!.sesionNumero)
      setDiagnostico(r)
      await refrescar()
      setMensaje('Investigación completada. Revisa la evidencia, la causa probable y la acción propuesta antes de decidir.')
    }, 'No fue posible completar la investigación.')
  }

  const invitarUsuario = () =>
    ejecutar(async () => {
      const r = await invitarUsuarioReproduccionTI(sesion!.sesionNumero)
      await refrescar()
      setMensaje(
        `Invitación enviada a ${r.nombreInvitado}. Verás su respuesta y su evidencia aquí; vence el ${fechaCorta(r.invitacionExpira)}.`,
      )
    }, 'No fue posible invitar al usuario.')

  const cancelarInvitacion = () => {
    if (!window.confirm('El usuario ya no podrá compartir su pantalla para esta investigación. ¿Continuar?')) return
    return ejecutar(async () => {
      await cancelarInvitacionReproduccionTI(sesion!.sesionNumero)
      await refrescar()
      setMensaje('Invitación cancelada; se avisó al usuario.')
    }, 'No fue posible cancelar la invitación.')
  }

  const cerrarObservacionManual = () => {
    if (confirmarCierreConUsuario()) void ejecutar(() => cerrarObservacion(false), 'No fue posible cerrar la observación.')
  }

  const grabar = () =>
    ejecutar(async () => {
      const blob = await grabarInformacionTI(sesion!.sesionNumero)
      const url = URL.createObjectURL(blob)
      const enlace = document.createElement('a')
      enlace.href = url
      enlace.download = `${sesion!.incidenciaNumero || codigoSesion(sesion!.sesionNumero)}-investigacion.md`
      enlace.click()
      URL.revokeObjectURL(url)
      await refrescar()
      setMensaje('Expediente Markdown grabado y descargado. El agente finalizó sin realizar cambios y dejó una nota interna en el ticket.')
    }, 'No fue posible grabar la información.')

  const realizarCambio = () => {
    if (
      !window.confirm(
        'TI autorizará la acción estructurada propuesta con sus parámetros exactos. El archivo .md no se ejecutará. ¿Deseas continuar?',
      )
    )
      return
    return ejecutar(async () => {
      try {
        const r = await realizarCambioTI(sesion!.sesionNumero)
        setMensaje(
          r.estado === 'PENDIENTE_APROBACION' && r.solicitudAprobacionSecuencia
            ? `${r.mensaje} Solicitud #${r.solicitudAprobacionSecuencia}: debe aprobarla otro operador desde Gestión de Tickets.`
            : r.mensaje,
        )
      } finally {
        await refrescar()
      }
    }, 'No fue posible procesar el cambio.')
  }

  const vincular = () =>
    ejecutar(async () => {
      await vincularIncidenciaTI(sesion!.sesionNumero, vinculo.trim().toUpperCase())
      setVinculo('')
      await refrescar()
      setMensaje('Ticket vinculado. El agente usará su contexto, documentos, mensajes y auditoría.')
    }, 'No fue posible vincular el ticket.')

  const simular = () =>
    ejecutar(async () => {
      const r = await simularCambioTI(sesion!.sesionNumero)
      setSimulacion(r)
      setComprobaciones(null)
      await refrescar()
      setMensaje(
        r.exito
          ? 'Simulación completada sin persistir cambios. Revisa el resultado antes de decidir.'
          : 'La simulación detectó que la acción no podría aplicarse. Revisa el motivo.',
      )
    }, 'No fue posible simular el cambio.')

  const reabrir = () => {
    if (
      !window.confirm(
        'El diagnóstico actual se guardará en el historial de la investigación y podrás volver a observar, invitar al usuario e investigar. ¿Continuar?',
      )
    )
      return
    return ejecutar(async () => {
      await reabrirObservacionTI(sesion!.sesionNumero)
      setDiagnostico(null)
      setSimulacion(null)
      setComprobaciones(null)
      await refrescar()
      setLiveEstado('Observación reabierta · puedes compartir pantalla o invitar al usuario')
      setMensaje('Observación reabierta. El diagnóstico anterior quedó guardado en el historial de la investigación.')
    }, 'No fue posible reabrir la observación.')
  }

  const comprobar = () =>
    ejecutar(async () => {
      setComprobaciones(await comprobarInvestigacionTI(sesion!.sesionNumero))
    }, 'No fue posible ejecutar la comprobación sin cambios.')
  const consultarCodigo = () =>
    ejecutar(async () => {
      setCodigo(await buscarCodigoInvestigacionTI(sesion!.sesionNumero))
    }, 'No fue posible consultar referencias de código.')

  const cancelar = () => {
    if (!window.confirm('Se cancelará la investigación y cualquier aprobación pendiente del agente. ¿Deseas continuar?')) return
    return ejecutar(async () => {
      if (liveActivo) {
        await liveRef.current?.detener()
        liveRef.current = null
        setLiveActivo(false)
      }
      await cancelarInvestigacionTI(sesion!.sesionNumero)
      await refrescar()
      setMensaje('Investigación cancelada. Si el ticket estaba bloqueado por la acción del agente, volvió a diagnóstico.')
    }, 'No fue posible cancelar la investigación.')
  }

  const validarSolucion = () => {
    if (!window.confirm('Confirma solo si verificaste con el usuario o en el sistema que la solución funcionó. ¿Continuar?')) return
    return ejecutar(async () => {
      await validarSolucionInvestigacionTI(sesion!.sesionNumero)
      await refrescar()
      setMensaje('Solución validada por TI. Ya puedes convertirla en un borrador de conocimiento.')
    }, 'No fue posible validar la solución.')
  }

  const crearConocimiento = () =>
    ejecutar(async () => {
      const r = await crearConocimientoInvestigacionTI(sesion!.sesionNumero)
      await refrescar()
      setMensaje(`Borrador ${r.conocimientoCodigo} creado. Revísalo y valídalo en Base de Conocimiento antes de publicarlo.`)
    }, 'No fue posible crear el borrador de conocimiento.')

  const reasignar = (destino: string) => {
    if (!destino) return
    const nombreDestino = destino === usuario.usuario ? 'ti' : catalogos?.operadores.find(x => x.codigo === destino)?.descripcion || destino
    if (
      !window.confirm(
        `La investigación pasará a ${nombreDestino}. Live, decisiones y ejecución quedarán a cargo del nuevo responsable. ¿Continuar?`,
      )
    )
      return
    return ejecutar(async () => {
      await reasignarInvestigacionTI(sesion!.sesionNumero, destino)
      setOperadorDestino('')
      await refrescar()
      setMensaje(
        destino === usuario.usuario
          ? 'Tomaste la investigación; ahora puedes actuar sobre ella.'
          : 'Investigación reasignada; el nuevo responsable fue notificado.',
      )
    }, 'No fue posible reasignar la investigación.')
  }

  async function abrirResolucion() {
    if (!diagnostico || !sesion) return
    try {
      await asegurarCatalogos()
    } catch (e) {
      setError(mensajeError(e, 'No fue posible cargar las áreas.'))
      return
    }
    const minutos = Math.min(1440, Math.max(1, Math.round((Date.now() - Date.parse(sesion.fechaInicio)) / 60000)))
    const accion =
      estado === 'CAMBIO_VALIDADO' && diagnostico.accion
        ? ` La acción ${diagnostico.accion.accionCodigo} (${diagnostico.accion.nombre}) se ejecutó mediante el procedimiento autorizado y confirmó su validación posterior.`
        : ''
    setResolucion({
      causaRaiz: diagnostico.causaProbable,
      solucion:
        `${diagnostico.solucionPropuesta}${accion}\nReferencia: investigación ${codigoSesion(sesion.sesionNumero)} del Agente de Ingeniería.`.slice(
          0,
          4000,
        ),
      respuestaUsuario:
        'Revisamos tu caso y aplicamos la corrección. Por favor vuelve a realizar el proceso y confirma si el inconveniente fue solucionado.',
      tipoResolucion: estado === 'CAMBIO_VALIDADO' ? 'CORRECCION' : 'GUIA',
      registrarAvance: true,
      minutos,
      areaCausante: '',
    })
  }

  const enviarSolucion = () => {
    if (!resolucion || !sesion?.incidenciaNumero) return
    if (resolucion.registrarAvance && !resolucion.areaCausante) {
      setError('Selecciona el área causante para registrar el avance técnico.')
      return
    }
    const ticket = sesion.incidenciaNumero
    return ejecutar(async () => {
      if (resolucion.registrarAvance)
        await registrarAvanceDetalladoTI(ticket, {
          detalle:
            `Investigación ${codigoSesion(sesion.sesionNumero)} del Agente de Ingeniería. Diagnóstico: ${diagnostico?.diagnostico || ''}`.slice(
              0,
              4000,
            ),
          visibleUsuario: false,
          tiempoUtilizadoMinutos: resolucion.minutos,
          areaCausante: resolucion.areaCausante,
        })
      await resolverTicketTI(ticket, {
        causaRaiz: resolucion.causaRaiz,
        solucion: resolucion.solucion,
        respuestaUsuario: resolucion.respuestaUsuario,
        tipoResolucion: resolucion.tipoResolucion,
      })
      setResolucion(null)
      await refrescar()
      setMensaje(
        `El ticket ${ticket} pasó a Pendiente de validación. El usuario recibió la respuesta y confirmará si el problema quedó resuelto.`,
      )
    }, 'No fue posible enviar la solución al usuario.')
  }

  async function nuevaInvestigacion() {
    if (liveActivo) await liveRef.current?.detener()
    liveRef.current = null
    setLiveActivo(false)
    setSesion(null)
    setContexto(null)
    setDiagnostico(null)
    setIncidencia('')
    setDescripcion('')
    setTranscripciones([])
    transcripcionesRef.current = []
    setErrorObservado('')
    setErrorRegistrado('')
    limpiarHerramientas()
    setMensaje('')
    setError('')
    setLiveEstado('Listo para iniciar observación')
    guardarSesionActiva(null)
    seleccionarSesionTraza(null)
    void cargarHistorial()
  }

  // Al cerrar sesión se detiene Live y se olvida la investigación activa.
  async function antesDeSalir() {
    await liveRef.current?.detener()
    seleccionarSesionTraza(null)
    guardarSesionActiva(null)
  }

  const diag = diagnostico

  return (
    <MarcoPortal
      antesDeSalir={antesDeSalir}
      menu="ti"
      activa="asistente"
      clase="agente-ti-shell"
      claseBarra="agente-topbar"
      rol={usuario.perfil}
      sinFrase
      pie={
        <>
          <div className="agente-sidebar-info">
            <Icono nombre="escudo" size={16} />
            <div>
              <strong>Human-in-the-loop</strong>
              <small>El agente investiga. TI decide y autoriza.</small>
            </div>
          </div>
        </>
      }
      barra={
        <div className="agente-topbar__titulo">
          <span>
            <Icono nombre="agente" size={19} />
          </span>
          <div>
            <strong>Agente de Ingeniería Autónomo</strong>
            <small>Observación · Investigación · Diagnóstico · Ejecución controlada</small>
          </div>
        </div>
      }
    >
      <main className="agente-contenido">
        <header className="agente-hero">
          <div>
            <span className="agente-eyebrow">
              <Icono nombre="escudo" size={14} /> Investigación autónoma bajo control TI
            </span>
            <h1>
              {vista === 'consulta'
                ? 'Consultas operativas de TI'
                : sesion
                  ? `Investigación ${codigoSesion(sesion.sesionNumero)}`
                  : 'Investiga la incidencia de extremo a extremo'}
            </h1>
            <p>
              {vista === 'consulta'
                ? 'Consulta configuración vigente (SLA, áreas, líneas, catálogos) o prepara el alta de un usuario corporativo con confirmación explícita.'
                : sesion
                  ? 'El agente conserva evidencia, correlación y límites de autoridad durante todo el proceso.'
                  : 'Reproduce el error por Live o inicia con un ticket existente. El agente documentará lo comprobable y nunca inventará el trazado técnico faltante.'}
            </p>
          </div>
          {sesion && vista === 'investigacion' && (
            <button className="agente-btn agente-btn--secundario" onClick={() => void nuevaInvestigacion()} disabled={procesando}>
              Nueva investigación
            </button>
          )}
        </header>

        <nav className="agente-pestanas" aria-label="Modo del asistente">
          <button className={vista === 'investigacion' ? 'activa' : ''} onClick={() => setVista('investigacion')}>
            <Icono nombre="buscarAmplio" size={15} />
            Investigación
          </button>
          <button className={vista === 'consulta' ? 'activa' : ''} onClick={() => setVista('consulta')}>
            <Icono nombre="chat" size={15} />
            Consultas operativas
          </button>
        </nav>

        {(error || mensaje) && (
          <div className={`agente-aviso ${error ? 'agente-aviso--error' : 'agente-aviso--ok'}`} role={error ? 'alert' : 'status'}>
            <Icono nombre={error ? 'alerta' : 'check'} size={17} />
            <span>{error || mensaje}</span>
            <button
              onClick={() => {
                setError('')
                setMensaje('')
              }}
              aria-label="Cerrar aviso"
            >
              ×
            </button>
          </div>
        )}

        {vista === 'consulta' ? (
          <div className="agente-consulta">
            <AsistenteTIConversacion
              onAbrirInvestigacion={numero => {
                setVista('investigacion')
                void abrirSesion(numero)
              }}
            />
          </div>
        ) : !sesion ? (
          <>
            <section className="agente-inicio-card">
              <div className="agente-inicio-card__icono">
                <Icono nombre="buscarAmplio" size={27} />
              </div>
              <div className="agente-inicio-card__titulo">
                <h2>Crear expediente de investigación</h2>
                <p>
                  Asocia el ticket cuando exista. Si todavía no existe, el agente puede recopilar la evidencia Live y documentarla sin
                  ejecutar cambios.
                </p>
              </div>
              <label>
                <span>
                  Incidencia <small>opcional</small>
                </span>
                <input
                  value={incidencia}
                  onChange={e => setIncidencia(e.target.value.toUpperCase())}
                  maxLength={12}
                  placeholder="INC-000523 o TKT-00042342"
                />
              </label>
              <label>
                <span>Problema a investigar</span>
                <textarea
                  value={descripcion}
                  onChange={e => setDescripcion(e.target.value)}
                  maxLength={1200}
                  rows={4}
                  placeholder="Ej. El usuario no puede generar el picking después de aprobar la requisición..."
                />
              </label>
              <button
                className="agente-btn agente-btn--primario"
                onClick={() => void iniciarInvestigacion()}
                disabled={descripcion.trim().length < 5 || procesando}
              >
                <Icono nombre="flecha" size={17} />
                {procesando ? 'Procesando...' : 'Iniciar investigación'}
              </button>
            </section>
            <section className="agente-panel agente-historial">
              <header>
                <div>
                  <span className="agente-panel__icono">
                    <Icono nombre="reloj" size={18} />
                  </span>
                  <div>
                    <h2>{alcanceEquipo ? 'Investigaciones del equipo' : 'Mis investigaciones'}</h2>
                    <p>
                      {alcanceEquipo
                        ? 'Supervisa, consulta o reasigna investigaciones de otros operadores'
                        : 'Retoma una investigación en curso o revisa expedientes anteriores'}
                    </p>
                  </div>
                </div>
                <div className="agente-historial__acciones">
                  {esSupervisor && (
                    <div className="agente-segmentado" role="group" aria-label="Alcance">
                      <button className={!alcanceEquipo ? 'activo' : ''} onClick={() => cambiarAlcance(false)}>
                        Mías
                      </button>
                      <button className={alcanceEquipo ? 'activo' : ''} onClick={() => cambiarAlcance(true)}>
                        Equipo TI
                      </button>
                    </div>
                  )}
                  <button className="agente-btn agente-btn--secundario" onClick={() => void cargarHistorial()} disabled={procesando}>
                    Actualizar
                  </button>
                </div>
              </header>
              {historial === null ? (
                <p className="agente-vacio">Cargando historial...</p>
              ) : historial.length === 0 ? (
                <p className="agente-vacio">Todavía no tienes investigaciones registradas.</p>
              ) : (
                <div className="agente-historial__lista">
                  {historial.map(x => (
                    <button key={x.sesionNumero} onClick={() => void abrirSesion(x.sesionNumero)} disabled={procesando}>
                      <strong>{codigoSesion(x.sesionNumero)}</strong>
                      <span>{x.incidenciaNumero || 'Sin ticket'}</span>
                      <p>
                        {alcanceEquipo && <b>{x.esPropietario ? 'Tú' : x.nombreOperador} · </b>}
                        {x.descripcionInicial}
                      </p>
                      <span className={`agente-chip ${estadosFinales.includes(x.estado) ? 'agente-chip--final' : ''}`}>
                        {x.estado.replaceAll('_', ' ')}
                      </span>
                      <small>{fechaCorta(x.fechaInicio)}</small>
                    </button>
                  ))}
                </div>
              )}
            </section>
          </>
        ) : (
          <>
            <section className="agente-progreso" aria-label="Progreso de investigación">
              {['RECOPILAR', 'OBSERVAR', 'INVESTIGAR', 'DIAGNOSTICAR', 'DECIDIR'].map((paso, i) => (
                <div
                  key={paso}
                  className={
                    i < progreso
                      ? 'agente-progreso__paso agente-progreso__paso--ok'
                      : i === progreso
                        ? 'agente-progreso__paso agente-progreso__paso--actual'
                        : 'agente-progreso__paso'
                  }
                >
                  <span>{i < progreso ? <Icono nombre="check" size={13} /> : i + 1}</span>
                  <strong>{paso}</strong>
                </div>
              ))}
            </section>

            {!puedeActuar && (
              <section className="agente-solo-lectura">
                <Icono nombre="escudo" size={18} />
                <div>
                  <strong>Investigación de {sesion.nombreOperador} · modo consulta</strong>
                  <p>
                    {puedeTomar
                      ? 'Puedes revisar evidencia, diagnóstico y expediente. Para observar, decidir o ejecutar, tómala: quedarás como responsable.'
                      : 'Puedes revisar evidencia, diagnóstico y expediente. Para observar, decidir o ejecutar, un supervisor o el responsable del ticket debe tomarla.'}
                  </p>
                </div>
                {puedeTomar && (
                  <button className="agente-btn agente-btn--primario" onClick={() => void reasignar(usuario.usuario)} disabled={procesando}>
                    Tomar investigación
                  </button>
                )}
              </section>
            )}

            <div className="agente-grid">
              <section className="agente-panel agente-live">
                <header>
                  <div>
                    <span className="agente-panel__icono">
                      <Icono nombre="pantalla" size={19} />
                    </span>
                    <div>
                      <h2>Observación Live</h2>
                      <p>Pantalla + voz para reproducir exactamente el proceso del usuario</p>
                    </div>
                  </div>
                  <span className={`agente-estado ${liveActivo ? 'agente-estado--live' : ''}`}>
                    {liveActivo ? 'EN VIVO' : 'OBSERVACIÓN'}
                  </span>
                </header>
                <div className="agente-live__visor">
                  <video ref={videoRef} muted playsInline />
                  <div className={liveActivo ? 'agente-live__placeholder agente-live__placeholder--oculto' : 'agente-live__placeholder'}>
                    <Icono nombre="pantalla" size={34} />
                    <strong>
                      {contexto?.sesion.procesoObservado ? 'Reproducción registrada' : 'Comparte la pantalla cuando estés listo'}
                    </strong>
                    <span>
                      La imagen se procesa durante Live y no se almacena; el expediente conserva eventos, transcripción y el error
                      observado.
                    </span>
                  </div>
                </div>
                <div className="agente-live__estado">
                  <span className={liveActivo ? 'agente-pulso' : ''} />
                  <p>{liveEstado}</p>
                </div>
                <div className="agente-live__acciones">
                  {!liveActivo ? (
                    <button
                      className="agente-btn agente-btn--live"
                      onClick={() => void iniciarLive()}
                      disabled={procesando || !enObservacion || !puedeActuar}
                    >
                      <Icono nombre="pantalla" size={17} />
                      {transcripciones.length ? 'Reanudar pantalla y conversación' : 'Compartir pantalla y conversar'}
                    </button>
                  ) : (
                    <>
                      <button className="agente-btn agente-btn--stop" onClick={() => void cerrarObservacion()}>
                        <Icono nombre="stop" size={15} />
                        Finalizar reproducción
                      </button>
                      <button className="agente-btn agente-btn--secundario" onClick={alternarMicrofono} aria-pressed={microSilenciado}>
                        <Icono nombre="microfono" size={15} />
                        {microSilenciado ? 'Activar micrófono' : 'Silenciar micrófono'}
                      </button>
                    </>
                  )}
                  <button
                    className="agente-btn agente-btn--primario"
                    onClick={() => void investigar()}
                    disabled={procesando || finalizada || !!diag || !puedeActuar}
                  >
                    <Icono nombre="buscarAmplio" size={17} />
                    {procesando ? 'Investigando...' : diag ? 'Diagnóstico generado' : 'Investigar ahora'}
                  </button>
                </div>
                {!liveActivo && enObservacion && puedeActuar && (
                  <label className="agente-grabar">
                    <input type="checkbox" checked={grabarSesion} onChange={e => setGrabarSesion(e.target.checked)} />
                    <span>Grabar la pantalla compartida para el expediente (sin audio)</span>
                  </label>
                )}
                {!liveActivo && motivoBloqueo && (
                  <div className="agente-motivo">
                    <Icono nombre="alertaTriangulo" size={15} />
                    <span>{motivoBloqueo}</span>
                    {puedeReabrir && (
                      <button className="agente-btn agente-btn--secundario" onClick={() => void reabrir()} disabled={procesando}>
                        Reabrir observación
                      </button>
                    )}
                  </div>
                )}
                {liveActivo && (
                  <form
                    className="agente-live__texto"
                    onSubmit={e => {
                      e.preventDefault()
                      enviarTextoLive()
                    }}
                  >
                    <input
                      value={textoLive}
                      onChange={e => setTextoLive(e.target.value)}
                      maxLength={500}
                      placeholder="Escribe al agente Live (por ejemplo, el número de documento)"
                    />
                    <button className="agente-btn agente-btn--secundario" disabled={!textoLive.trim()}>
                      <Icono nombre="flecha" size={15} />
                      Enviar
                    </button>
                  </form>
                )}
                {enObservacion && puedeActuar && (
                  <div className="agente-error-observado">
                    <label htmlFor="error-observado">
                      <Icono nombre="alertaTriangulo" size={14} />
                      Error observado {errorRegistrado && <small>· registrado</small>}
                    </label>
                    <div>
                      <input
                        id="error-observado"
                        value={errorObservado}
                        onChange={e => setErrorObservado(e.target.value)}
                        maxLength={1000}
                        placeholder="Copia el mensaje exacto que muestra la pantalla cuando aparece el error"
                      />
                      <button className="agente-btn agente-btn--stop" onClick={() => void marcarError()} disabled={procesando}>
                        Marcar error
                      </button>
                    </div>
                  </div>
                )}
                {(enObservacion || sesion.estadoInvitacion) && (
                  <div className="agente-invitacion">
                    <div className="agente-invitacion__cabecera">
                      <div>
                        <strong>
                          <Icono nombre="pantalla" size={14} />
                          Reproducción por el usuario
                        </strong>
                        <p>{textoInvitacion(sesion, !!sesion.incidenciaNumero)}</p>
                      </div>
                      {puedeActuar &&
                        enObservacion &&
                        (invitacionVigente ? (
                          <button
                            className="agente-btn agente-btn--secundario"
                            onClick={() => void cancelarInvitacion()}
                            disabled={procesando}
                          >
                            Cancelar invitación
                          </button>
                        ) : (
                          <button
                            className="agente-btn agente-btn--live"
                            onClick={() => void invitarUsuario()}
                            disabled={procesando || !sesion.incidenciaNumero}
                          >
                            <Icono nombre="flecha" size={15} />
                            {sesion.estadoInvitacion ? 'Invitar de nuevo' : 'Invitar al usuario'}
                          </button>
                        ))}
                    </div>
                    {eventosUsuarioFinal.length > 0 && (
                      <div className="agente-invitacion__lista">
                        {eventosUsuarioFinal.slice(-8).map(x => (
                          <p key={x.secuencia} className={x.tipo === 'ERROR_OBSERVADO' ? 'agente-invitacion__error' : ''}>
                            <b>
                              {x.tipo === 'TRANSCRIPCION_USUARIO'
                                ? 'Usuario'
                                : x.tipo === 'TRANSCRIPCION_AGENTE'
                                  ? 'Asistente'
                                  : x.tipo === 'ERROR_OBSERVADO'
                                    ? 'Error marcado'
                                    : x.tipo === 'PASO_OBSERVADO'
                                      ? 'Paso'
                                      : 'Sesión'}
                            </b>
                            <span>{x.contenido}</span>
                          </p>
                        ))}
                      </div>
                    )}
                    {puedeActuar && enObservacion && !liveActivo && (hayTranscripcionServidor || eventosUsuarioFinal.length > 0) && (
                      <button
                        className="agente-btn agente-btn--stop agente-invitacion__cerrar"
                        onClick={cerrarObservacionManual}
                        disabled={procesando}
                      >
                        <Icono nombre="stop" size={14} />
                        Cerrar observación
                      </button>
                    )}
                  </div>
                )}
                {grabaciones.length > 0 && (
                  <div className="agente-grabaciones">
                    <strong>
                      <Icono nombre="pantalla" size={14} />
                      Grabaciones de pantalla ({grabaciones.length})
                    </strong>
                    {grabaciones.map(g => (
                      <figure key={g.secuencia}>
                        <video controls preload="metadata" src={urlGrabacionTI(sesion.sesionNumero, g.secuencia)} />
                        <figcaption>
                          <b>{g.fuente === 'LIVE_USUARIO' ? 'Reproducción del usuario' : 'Observación TI'}</b> · {fechaCorta(g.fecha)} ·{' '}
                          {g.contenido}
                        </figcaption>
                      </figure>
                    ))}
                  </div>
                )}
                <div className="agente-transcripcion">
                  <div className="agente-transcripcion__titulo">
                    <Icono nombre="microfono" size={15} />
                    <strong>Conversación Live</strong>
                    <span>{transcripciones.length} turnos</span>
                  </div>
                  {transcripciones.length === 0 ? (
                    <p className="agente-vacio">La transcripción aparecerá aquí durante la reproducción.</p>
                  ) : (
                    <div className="agente-transcripcion__lista">
                      {transcripciones.slice(-12).map((t, i) => (
                        <div
                          key={`${i}-${t.texto.slice(0, 20)}`}
                          className={`agente-transcripcion__item agente-transcripcion__item--${t.rol}`}
                        >
                          <b>{t.rol === 'usuario' ? 'Usuario' : 'Agente'}</b>
                          <span>{t.texto}</span>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              </section>

              <aside className="agente-columna">
                <section className="agente-panel agente-contexto">
                  <header>
                    <div>
                      <span className="agente-panel__icono">
                        <Icono nombre="datos" size={18} />
                      </span>
                      <div>
                        <h2>Contexto correlacionado</h2>
                        <p>Identidad y fuentes autorizadas</p>
                      </div>
                    </div>
                  </header>
                  <dl>
                    <div>
                      <dt>Incidencia</dt>
                      <dd>{contexto?.ticket.incidenciaNumero || 'Sin ticket asociado'}</dd>
                    </div>
                    <div>
                      <dt>Correlation ID</dt>
                      <dd className="agente-mono">{sesion.idCorrelacion}</dd>
                    </div>
                    <div>
                      <dt>Documentos</dt>
                      <dd>{contexto?.documentos.length ?? 0}</dd>
                    </div>
                    <div>
                      <dt>Mensajes</dt>
                      <dd>{contexto?.mensajes.length ?? 0}</dd>
                    </div>
                    <div>
                      <dt>Conocimiento</dt>
                      <dd>{contexto?.conocimientos.length ?? 0} referencias</dd>
                    </div>
                    <div>
                      <dt>Auditoría</dt>
                      <dd>{contexto?.auditoria.length ?? 0} eventos</dd>
                    </div>
                    <div>
                      <dt>Trazas</dt>
                      <dd>{contexto?.eventos.filter(x => x.tipo === 'TRAZA_BACKEND').length ?? 0} del servidor</dd>
                    </div>
                    <div>
                      <dt>Estado</dt>
                      <dd>
                        <span className="agente-chip">{estado.replaceAll('_', ' ')}</span>
                      </dd>
                    </div>
                    <div>
                      <dt>Estado del ticket</dt>
                      <dd>{estadoTicket || '—'}</dd>
                    </div>
                    <div>
                      <dt>Responsable</dt>
                      <dd>{sesion.esPropietario ? 'Tú' : sesion.nombreOperador}</dd>
                    </div>
                  </dl>
                  {enObservacion && (
                    <p className="agente-nota">
                      Mientras la investigación esté en observación, tus acciones en otros módulos del portal se registran como traza
                      técnica correlacionada.
                    </p>
                  )}
                </section>

                <section className="agente-panel agente-herramientas">
                  <header>
                    <div>
                      <span className="agente-panel__icono">
                        <Icono nombre="codigo" size={18} />
                      </span>
                      <div>
                        <h2>Herramientas sin cambios</h2>
                        <p>Consultas de solo lectura sobre la investigación</p>
                      </div>
                    </div>
                  </header>
                  <div className="agente-herramientas__cuerpo">
                    {!sesion.incidenciaNumero && enObservacion && puedeActuar && (
                      <form
                        className="agente-herramientas__vinculo"
                        onSubmit={e => {
                          e.preventDefault()
                          void vincular()
                        }}
                      >
                        <input
                          value={vinculo}
                          onChange={e => setVinculo(e.target.value.toUpperCase())}
                          maxLength={12}
                          placeholder="Vincular ticket: INC-000523"
                        />
                        <button className="agente-btn agente-btn--secundario" disabled={procesando || vinculo.trim().length < 10}>
                          Vincular
                        </button>
                      </form>
                    )}
                    <div className="agente-herramientas__botones">
                      <button
                        className="agente-btn agente-btn--secundario"
                        onClick={() => void comprobar()}
                        disabled={procesando || !puedeActuar}
                      >
                        <Icono nombre="escudo" size={15} />
                        Comprobar sin cambios
                      </button>
                      <button className="agente-btn agente-btn--secundario" onClick={() => void consultarCodigo()} disabled={procesando}>
                        <Icono nombre="codigo" size={15} />
                        Referencias de código
                      </button>
                    </div>
                    {comprobaciones && (
                      <ul className="agente-comprobaciones">
                        {comprobaciones.map(x => (
                          <li key={x.codigo}>
                            <span className={`agente-resultado agente-resultado--${x.resultado.toLowerCase()}`}>
                              {x.resultado.replaceAll('_', ' ')}
                            </span>
                            <span>{x.descripcion}</span>
                          </li>
                        ))}
                      </ul>
                    )}
                    {codigo &&
                      (codigo.length === 0 ? (
                        <p className="agente-vacio">No se encontraron referencias estáticas para los términos de la investigación.</p>
                      ) : (
                        <ul className="agente-codigo">
                          {codigo.map(x => (
                            <li key={`${x.archivo}:${x.linea}`}>
                              <strong>
                                {x.archivo}:{x.linea}
                              </strong>
                              <code>{x.fragmento}</code>
                            </li>
                          ))}
                          <li className="agente-codigo__nota">Referencias posibles: no demuestran qué ruta se ejecutó.</li>
                        </ul>
                      ))}
                    {esSupervisor && estadosReasignables.includes(estado) && (
                      <div className="agente-herramientas__vinculo">
                        <select
                          value={operadorDestino}
                          onChange={e => setOperadorDestino(e.target.value)}
                          onFocus={() => void asegurarCatalogos().catch(() => undefined)}
                          aria-label="Reasignar a"
                        >
                          <option value="">Reasignar a...</option>
                          {catalogos?.operadores
                            .filter(x => x.codigo !== sesion.usuarioTI)
                            .map(x => (
                              <option key={x.codigo} value={x.codigo}>
                                {x.descripcion}
                              </option>
                            ))}
                        </select>
                        <button
                          className="agente-btn agente-btn--secundario"
                          onClick={() => void reasignar(operadorDestino)}
                          disabled={procesando || !operadorDestino}
                        >
                          Reasignar
                        </button>
                      </div>
                    )}
                    {puedeActuar && estadosCancelables.includes(estado) && (
                      <button className="agente-herramientas__cancelar" onClick={() => void cancelar()} disabled={procesando}>
                        Cancelar investigación
                      </button>
                    )}
                  </div>
                </section>
              </aside>
            </div>

            <section className="agente-panel agente-diagnostico">
              <header>
                <div>
                  <span className="agente-panel__icono">
                    <Icono nombre="codigo" size={19} />
                  </span>
                  <div>
                    <h2>Investigación técnica</h2>
                    <p>Evidencia verificada y reproducción técnica disponible</p>
                  </div>
                </div>
                {diag && (
                  <div className="agente-confianza">
                    <span>{Math.round(diag.confianza)}%</span>
                    <small>confianza diagnóstica</small>
                    {diag.modo && <em className={`agente-modo agente-modo--${diag.modo.toLowerCase()}`}>{modoDiagnostico(diag.modo)}</em>}
                  </div>
                )}
              </header>
              {!diag ? (
                <div className="agente-diagnostico__espera">
                  <div>
                    <Icono nombre="buscarAmplio" size={25} />
                  </div>
                  <strong>{estado === 'CANCELADO' ? 'Investigación cancelada' : 'Esperando investigación'}</strong>
                  <p>
                    Al iniciar el análisis, el agente combinará el flujo observado con ticket, mensajes, documentos, conocimiento, auditoría
                    y telemetría autorizada.
                  </p>
                </div>
              ) : (
                <div className="agente-diagnostico__contenido">
                  <div className="agente-hallazgo">
                    <span>DIAGNÓSTICO</span>
                    <h3>{diag.diagnostico}</h3>
                  </div>
                  <div className="agente-dos-columnas">
                    <article>
                      <span>CAUSA PROBABLE</span>
                      <p>{diag.causaProbable}</p>
                    </article>
                    <article>
                      <span>SOLUCIÓN PROPUESTA</span>
                      <p>{diag.solucionPropuesta}</p>
                    </article>
                  </div>
                  {diag.limitacion && (
                    <div className="agente-limitacion">
                      <Icono nombre="alertaTriangulo" size={17} />
                      <span>{diag.limitacion}</span>
                    </div>
                  )}
                  <div className="agente-evidencia-grid">
                    <article>
                      <header>
                        <Icono nombre="archivoAlto" size={16} />
                        <strong>Evidencia</strong>
                        <span>{diag.evidencias.length}</span>
                      </header>
                      {diag.evidencias.length === 0 ? (
                        <p className="agente-vacio">Sin evidencia adicional.</p>
                      ) : (
                        diag.evidencias.slice(0, 10).map((e, i) => (
                          <div className="agente-evidencia" key={`${e.referencia}-${i}`}>
                            <b>{e.tipoFuente}</b>
                            <strong>{e.referencia}</strong>
                            <p>{e.descripcion}</p>
                          </div>
                        ))
                      )}
                    </article>
                    <article>
                      <header>
                        <Icono nombre="codigo" size={16} />
                        <strong>Traza técnica</strong>
                        <span>{diag.trazaTecnica.length}</span>
                      </header>
                      {diag.trazaTecnica.length === 0 ? (
                        <p className="agente-vacio">No hay trazas instrumentadas. El agente no inventará un recorrido de código.</p>
                      ) : (
                        diag.trazaTecnica.map((t, i) => (
                          <div className="agente-traza" key={i}>
                            <span>{i + 1}</span>
                            <code>{t}</code>
                          </div>
                        ))
                      )}
                    </article>
                  </div>
                  {diag.pasos.length > 0 && (
                    <section className="agente-pasos">
                      <header>
                        <Icono nombre="buscarAmplio" size={16} />
                        <strong>Pasos de investigación</strong>
                        <span>{diag.pasos.length} consultas de solo lectura</span>
                      </header>
                      <ol>
                        {diag.pasos.map(p => (
                          <li key={p.orden} className={p.error ? 'agente-paso agente-paso--error' : 'agente-paso'}>
                            <span className={`agente-paso__origen agente-paso__origen--${p.origen === 'MODELO' ? 'modelo' : 'auto'}`}>
                              {p.origen === 'MODELO' ? 'Agente' : 'Automática'}
                            </span>
                            <div>
                              <strong>
                                {p.nombre}
                                {p.parametrosJson && p.parametrosJson !== '{}' && <code>{p.parametrosJson}</code>}
                              </strong>
                              <p>{p.error ? `No disponible: ${p.error}` : p.resumen}</p>
                            </div>
                            <small>{p.error ? '—' : `${p.filas} fila(s)${p.truncado ? ' · truncado' : ''} · ${p.duracionMs} ms`}</small>
                          </li>
                        ))}
                      </ol>
                      <p className="agente-nota">Cada consulta corrió en una transacción revertida: investigar no modifica información.</p>
                    </section>
                  )}
                  {diag.hallazgos.length > 0 && (
                    <section className="agente-hallazgos">
                      <header>
                        <Icono nombre="check" size={16} />
                        <strong>Hallazgos con evidencia verificada</strong>
                      </header>
                      <ul>
                        {diag.hallazgos.map((h, i) => (
                          <li key={`${h.referencia}-${i}`}>
                            <b>{h.fuente.replaceAll('_', ' ')}</b>
                            <code>{h.referencia}</code>
                            <span>{h.descripcion}</span>
                          </li>
                        ))}
                      </ul>
                    </section>
                  )}
                  {diag.datosOcultados > 0 && (
                    <p className="agente-nota agente-nota--privacidad">
                      <Icono nombre="escudo" size={14} />
                      Se ocultaron {diag.datosOcultados} dato(s) sensibles antes de enviar el contexto al proveedor de IA.
                    </p>
                  )}
                  {diag.accion && (
                    <section className="agente-accion-propuesta">
                      <div>
                        <span className="agente-panel__icono">
                          <Icono nombre="escudo" size={18} />
                        </span>
                        <div>
                          <small>ACCIÓN PROPUESTA</small>
                          <strong>
                            {diag.accion.accionCodigo} · {diag.accion.nombre}
                          </strong>
                          <p>
                            Riesgo {diag.accion.nivelRiesgo} ·{' '}
                            {diag.accion.requiereAprobacion
                              ? 'requiere aprobación de otro operador TI'
                              : 'sin aprobación adicional según catálogo'}{' '}
                            ·{' '}
                            {accionCatalogo?.tieneEjecutor
                              ? 'ejecutor autorizado disponible'
                              : 'sin ejecutor autorizado (no podrá aplicarse automáticamente)'}
                          </p>
                        </div>
                      </div>
                      <code>{diag.accion.parametrosJson}</code>
                      {puedeActuar && accionCatalogo?.tieneEjecutor && estadosSimulacion.includes(estado) && (
                        <div className="agente-simulacion">
                          <button className="agente-btn agente-btn--secundario" onClick={() => void simular()} disabled={procesando}>
                            <Icono nombre="escudo" size={15} />
                            Simular cambio
                          </button>
                          <span>Ejecuta la acción con estos parámetros en una transacción que siempre se revierte.</span>
                        </div>
                      )}
                      {simulacion && (
                        <div
                          className={`agente-simulacion__resultado ${simulacion.exito ? 'agente-simulacion__resultado--ok' : 'agente-simulacion__resultado--error'}`}
                          role="status"
                        >
                          <strong>{simulacion.exito ? 'Simulación exitosa' : 'La acción no podría aplicarse'}</strong>
                          <p>{simulacion.mensaje}</p>
                          {simulacion.exito && simulacion.resultadoJson && <code>{simulacion.resultadoJson}</code>}
                        </div>
                      )}
                    </section>
                  )}
                  {diag.informeMarkdown && (
                    <div className="agente-informe">
                      <button className="agente-btn agente-btn--secundario" onClick={() => setVerInforme(v => !v)}>
                        <Icono nombre="archivoAlto" size={15} />
                        {verInforme ? 'Ocultar expediente' : 'Ver expediente técnico'}
                      </button>
                      {verInforme && <pre>{diag.informeMarkdown}</pre>}
                    </div>
                  )}
                  <footer className="agente-decision">
                    <div>
                      <strong>
                        {finalizada
                          ? 'Investigación cerrada'
                          : estado === 'ERROR_EJECUCION'
                            ? 'Ejecución pendiente de revisión TI'
                            : 'Decisión de TI'}
                      </strong>
                      <p>
                        {estado === 'PENDIENTE_APROBACION'
                          ? 'La acción espera la aprobación de otro operador. Cuando sea aprobada, vuelve a pulsar Realizar cambio.'
                          : estado === 'ERROR_EJECUCION'
                            ? 'El cambio no se confirmó. Revisa la auditoría del ticket antes de cualquier nuevo intento.'
                            : 'El .md documenta la investigación; nunca se interpreta como instrucción ejecutable.'}
                      </p>
                    </div>
                    <div className="agente-decision__botones">
                      <button
                        className="agente-btn agente-btn--secundario"
                        onClick={() => void grabar()}
                        disabled={procesando || !puedeActuar || !(estadosDecision.includes(estado) || estado === 'INFORME_GRABADO')}
                      >
                        <Icono nombre="archivoAlto" size={16} />
                        {estado === 'INFORME_GRABADO' ? 'Descargar expediente' : 'Grabar información'}
                      </button>
                      <button
                        className="agente-btn agente-btn--cambio"
                        onClick={() => void realizarCambio()}
                        disabled={procesando || !puedeActuar || !diag.accion || !estadosDecision.includes(estado)}
                      >
                        <Icono nombre="escudo" size={16} />
                        {estado === 'PENDIENTE_APROBACION' ? 'Verificar aprobación y ejecutar' : 'Realizar cambio'}
                      </button>
                    </div>
                  </footer>
                </div>
              )}
            </section>

            {(estado === 'INFORME_GRABADO' || estado === 'CAMBIO_VALIDADO') && (
              <section className="agente-panel agente-cierre">
                <header>
                  <div>
                    <span className="agente-panel__icono">
                      <Icono nombre="libroAbierto" size={18} />
                    </span>
                    <div>
                      <h2>Cierre y conocimiento</h2>
                      <p>Convierte una solución comprobada en conocimiento reutilizable</p>
                    </div>
                  </div>
                </header>
                {puedeEnviarSolucion &&
                  (!resolucion ? (
                    <div className="agente-cierre__cuerpo agente-cierre__paso">
                      <p>
                        <strong>1. Enviar la solución al usuario.</strong> El ticket {sesion.incidenciaNumero} pasará a Pendiente de
                        validación y se registrará el avance técnico con el tiempo invertido.
                      </p>
                      <button className="agente-btn agente-btn--primario" onClick={() => void abrirResolucion()} disabled={procesando}>
                        <Icono nombre="flecha" size={15} />
                        Preparar respuesta
                      </button>
                    </div>
                  ) : (
                    <form
                      className="agente-resolucion"
                      onSubmit={e => {
                        e.preventDefault()
                        void enviarSolucion()
                      }}
                    >
                      <label>
                        Causa raíz
                        <textarea
                          required
                          minLength={5}
                          maxLength={4000}
                          rows={2}
                          value={resolucion.causaRaiz}
                          onChange={e => setResolucion({ ...resolucion, causaRaiz: e.target.value })}
                        />
                      </label>
                      <label>
                        Solución aplicada
                        <textarea
                          required
                          minLength={5}
                          maxLength={4000}
                          rows={3}
                          value={resolucion.solucion}
                          onChange={e => setResolucion({ ...resolucion, solucion: e.target.value })}
                        />
                      </label>
                      <label>
                        Respuesta al usuario <small>visible para el usuario</small>
                        <textarea
                          required
                          minLength={5}
                          maxLength={4000}
                          rows={2}
                          value={resolucion.respuestaUsuario}
                          onChange={e => setResolucion({ ...resolucion, respuestaUsuario: e.target.value })}
                        />
                      </label>
                      <div className="agente-resolucion__fila">
                        <label>
                          Tipo de resolución
                          <select
                            value={resolucion.tipoResolucion}
                            onChange={e => setResolucion({ ...resolucion, tipoResolucion: e.target.value })}
                          >
                            <option value="CORRECCION">Corrección</option>
                            <option value="CONFIGURACION">Configuración</option>
                            <option value="GUIA">Guía</option>
                            <option value="REPROCESO">Reproceso</option>
                          </select>
                        </label>
                        <label className="agente-resolucion__check">
                          <input
                            type="checkbox"
                            checked={resolucion.registrarAvance}
                            onChange={e => setResolucion({ ...resolucion, registrarAvance: e.target.checked })}
                          />
                          Registrar avance técnico
                        </label>
                        {resolucion.registrarAvance && (
                          <>
                            <label>
                              Minutos invertidos
                              <input
                                type="number"
                                min={1}
                                max={1440}
                                value={resolucion.minutos}
                                onChange={e =>
                                  setResolucion({ ...resolucion, minutos: Math.min(1440, Math.max(1, Number(e.target.value) || 1)) })
                                }
                              />
                            </label>
                            <label>
                              Área causante
                              <select
                                required
                                value={resolucion.areaCausante}
                                onChange={e => setResolucion({ ...resolucion, areaCausante: e.target.value })}
                              >
                                <option value="">Seleccionar</option>
                                {catalogos?.areas.map(x => (
                                  <option key={x.codigo} value={x.codigo}>
                                    {x.descripcion}
                                  </option>
                                ))}
                              </select>
                            </label>
                          </>
                        )}
                      </div>
                      <div className="agente-resolucion__botones">
                        <button
                          type="button"
                          className="agente-btn agente-btn--secundario"
                          onClick={() => setResolucion(null)}
                          disabled={procesando}
                        >
                          Cancelar
                        </button>
                        <button className="agente-btn agente-btn--primario" disabled={procesando}>
                          <Icono nombre="check" size={15} />
                          Enviar a validación del usuario
                        </button>
                      </div>
                    </form>
                  ))}
                {estadoTicket === 'PV' && (
                  <p className="agente-cierre__aviso">
                    El ticket {sesion.incidenciaNumero} espera la validación del usuario. Cuando confirme, valida la solución aquí para
                    convertirla en conocimiento.
                  </p>
                )}
                {puedeActuar && (
                  <div className="agente-cierre__cuerpo">
                    {!sesion.solucionValidada ? (
                      <>
                        <p>Valida la solución solo después de confirmar con el usuario o en el sistema que el problema desapareció.</p>
                        <button className="agente-btn agente-btn--primario" onClick={() => void validarSolucion()} disabled={procesando}>
                          <Icono nombre="check" size={15} />
                          Validar solución
                        </button>
                      </>
                    ) : sesion.conocimientoCodigo ? (
                      <>
                        <p>
                          Borrador <strong>{sesion.conocimientoCodigo}</strong> creado. Debe revisarse y validarse antes de publicarse.
                        </p>
                        <button className="agente-btn agente-btn--secundario" onClick={() => navigate('/base-conocimiento')}>
                          <Icono nombre="libroAbierto" size={15} />
                          Abrir Base de Conocimiento
                        </button>
                      </>
                    ) : (
                      <>
                        <p>
                          Solución validada por TI.
                          {!sesion.incidenciaNumero && ' Para crear conocimiento la investigación debe tener un ticket vinculado.'}
                        </p>
                        <button
                          className="agente-btn agente-btn--primario"
                          onClick={() => void crearConocimiento()}
                          disabled={procesando || !sesion.incidenciaNumero}
                        >
                          <Icono nombre="libroAbierto" size={15} />
                          Crear borrador de conocimiento
                        </button>
                      </>
                    )}
                  </div>
                )}
              </section>
            )}
          </>
        )}
      </main>
    </MarcoPortal>
  )
}

function textoInvitacion(sesion: AgenteTISesion, tieneTicket: boolean) {
  const nombre = sesion.nombreInvitado || 'El usuario'
  switch (sesion.estadoInvitacion) {
    case 'PENDIENTE':
      return `Invitación enviada a ${nombre}${sesion.invitacionExpira ? `; vence ${fechaCorta(sesion.invitacionExpira)}` : ''}. Esperando su respuesta.`
    case 'ACEPTADA':
      return `${nombre} aceptó y otorgó su consentimiento. Su conversación y el error que marque aparecen aquí.`
    case 'RECHAZADA':
      return `${nombre} rechazó la sesión. Continúa con la evidencia disponible o invítalo de nuevo.`
    case 'CANCELADA':
      return 'Cancelaste la invitación.'
    case 'FINALIZADA':
      return `${nombre} terminó de mostrar el error. Cierra la observación para investigar.`
    case 'VENCIDA':
      return 'La invitación venció sin completarse.'
    default:
      return tieneTicket
        ? 'Invita al solicitante a mostrar el error desde su propio portal, con su consentimiento.'
        : 'Vincula un ticket para poder invitar a su solicitante.'
  }
}

function obtenerProgreso(estado: string) {
  if (!estado || estado === 'RECOPILANDO') return 0
  if (['OBSERVANDO', 'LISTO_INVESTIGAR'].includes(estado)) return 1
  if (estado === 'INVESTIGANDO') return 2
  if (estado === 'PENDIENTE_TI') return 4
  if (
    [
      'PENDIENTE_APROBACION',
      'LISTO_EJECUCION',
      'EJECUTANDO',
      'SIN_EJECUTOR',
      'INFORME_GRABADO',
      'CAMBIO_VALIDADO',
      'ERROR_EJECUCION',
    ].includes(estado)
  )
    return 5
  return 2
}
