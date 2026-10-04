/**
 * Archivo: ReproduccionUsuarioPage.tsx
 * Objetivo: Permitir que el colaborador muestre a TI cómo ocurre su error, compartiendo pantalla y voz por invitación.
 * Responsabilidad: Explicar y registrar el consentimiento, conducir la sesión Live guiada y enviar solo evidencia observacional.
 * Consideraciones: El usuario decide cuándo empezar, pausar y terminar; la pantalla no se graba y el diagnóstico permanece exclusivamente en TI.
 */

import { useEffect, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { primerNombre, useAutenticacion } from '../autenticacion/AutenticacionContext'
import { GeminiLiveSesion } from '../../services/geminiLiveApi'
import { GrabadorPantalla } from '../../services/grabadorPantallaService'
import {
  crearTokenReproduccion,
  finalizarReproduccion,
  listarReproducciones,
  obtenerReproduccion,
  registrarEventoReproduccion,
  responderReproduccion,
  subirGrabacionReproduccion,
  type ReproduccionInvitacion,
} from '../../services/reproduccionApi'
import { seleccionarSesionTraza } from '../../services/api'
import MarcoPortal from '../../components/MarcoPortal'
import Icono from '../../components/Icono'
import './ReproduccionUsuarioPage.css'

type Turno = { rol: 'usuario' | 'agente'; texto: string }
const mensajeError = (e: unknown, defecto: string) => (e instanceof Error ? e.message : defecto)
const horaLimite = (valor: string) => new Date(valor).toLocaleString('es-PE', { dateStyle: 'short', timeStyle: 'short' })

export default function ReproduccionUsuarioPage() {
  const navigate = useNavigate()
  const [parametros, setParametros] = useSearchParams()
  const { usuario } = useAutenticacion()
  const [invitaciones, setInvitaciones] = useState<ReproduccionInvitacion[] | null>(null)
  const [invitacion, setInvitacion] = useState<ReproduccionInvitacion | null>(null)
  const [consiente, setConsiente] = useState(false)
  const [rechazando, setRechazando] = useState(false)
  const [motivo, setMotivo] = useState('')
  const [liveActivo, setLiveActivo] = useState(false)
  const [estadoLive, setEstadoLive] = useState('Cuando estés listo, comparte la ventana donde ocurre el error.')
  const [silenciado, setSilenciado] = useState(false)
  const [turnos, setTurnos] = useState<Turno[]>([])
  const [textoLive, setTextoLive] = useState('')
  const [errorPantalla, setErrorPantalla] = useState('')
  const [errorMarcado, setErrorMarcado] = useState(false)
  const [terminada, setTerminada] = useState(false)
  const [procesando, setProcesando] = useState(false)
  const [error, setError] = useState('')
  const [mensaje, setMensaje] = useState('')
  const videoRef = useRef<HTMLVideoElement>(null)
  const liveRef = useRef<GeminiLiveSesion | null>(null)
  const grabadorRef = useRef<GrabadorPantalla | null>(null)
  const invitacionRef = useRef<ReproduccionInvitacion | null>(null)
  const sesionParam = parametros.get('sesion')

  useEffect(
    () => () => {
      void liveRef.current?.detener()
    },
    [],
  )

  useEffect(() => {
    invitacionRef.current = invitacion
    // Mientras la reproducción está aceptada, las acciones del usuario en el portal se correlacionan con la investigación.
    seleccionarSesionTraza(invitacion?.estadoInvitacion === 'ACEPTADA' && !terminada ? invitacion.sesionNumero : null)
  }, [invitacion, terminada])

  useEffect(() => {
    if (sesionParam && /^\d+$/.test(sesionParam)) void abrir(Number(sesionParam))
    else void cargar()
    // Solo debe reaccionar al parámetro de la URL (notificaciones y aviso de Inicio).
  }, [sesionParam])

  if (!usuario) return null
  const nombre = primerNombre(usuario.nombreCompleto)

  async function cargar() {
    setInvitacion(null)
    try {
      setInvitaciones(await listarReproducciones())
    } catch (e) {
      setInvitaciones([])
      setError(mensajeError(e, 'No fue posible cargar tus solicitudes.'))
    }
  }

  async function abrir(sesion: number) {
    setError('')
    setMensaje('')
    setTerminada(false)
    setTurnos([])
    setErrorPantalla('')
    setErrorMarcado(false)
    setConsiente(false)
    setRechazando(false)
    try {
      setInvitacion(await obtenerReproduccion(sesion))
    } catch (e) {
      setInvitacion(null)
      setError(mensajeError(e, 'La solicitud ya no está disponible.'))
      void cargar()
    }
  }

  async function ejecutar(accion: () => Promise<void>, defecto: string) {
    if (procesando) return
    setProcesando(true)
    setError('')
    try {
      await accion()
    } catch (e) {
      setError(mensajeError(e, defecto))
    } finally {
      setProcesando(false)
    }
  }

  const aceptar = () =>
    ejecutar(async () => {
      await responderReproduccion(invitacion!.sesionNumero, true, true)
      setInvitacion({ ...invitacion!, estadoInvitacion: 'ACEPTADA' })
      setMensaje('Gracias. Cuando quieras, comparte la ventana donde ocurre el error y sigue las indicaciones del asistente.')
    }, 'No fue posible aceptar la solicitud.')

  const rechazar = () =>
    ejecutar(async () => {
      await responderReproduccion(invitacion!.sesionNumero, false, false, motivo.trim())
      setParametros({}, { replace: true })
      setMensaje('Avisamos a TI que no puedes realizar la sesión. Seguirán atendiendo tu ticket con la información disponible.')
      await cargar()
    }, 'No fue posible responder la solicitud.')

  function registrar(tipo: string, contenido: string) {
    const actual = invitacionRef.current
    if (!actual || !contenido.trim()) return Promise.resolve()
    return registrarEventoReproduccion(actual.sesionNumero, tipo, contenido.trim().slice(0, 12000))
  }

  function agregarTurno(rol: 'usuario' | 'agente', texto: string) {
    setTurnos(actual => [...actual, { rol, texto }])
    void registrar(rol === 'usuario' ? 'TRANSCRIPCION_USUARIO' : 'TRANSCRIPCION_AGENTE', texto).catch(() =>
      setEstadoLive('Una parte de la conversación no pudo guardarse; puedes continuar.'),
    )
  }

  // El asistente registra los pasos que ve y el error exacto; el usuario no tiene que copiarlo a mano.
  async function atenderFuncion(nombre: string, argumentos: Record<string, unknown>) {
    const texto = String(argumentos.descripcion ?? argumentos.mensaje ?? '')
      .trim()
      .slice(0, 1000)
    if (!texto) return 'La descripción está vacía; no se registró.'
    if (nombre === 'registrar_paso') {
      await registrar('PASO_OBSERVADO', texto)
      return 'Paso registrado.'
    }
    if (nombre === 'registrar_error') {
      await registrar('ERROR_OBSERVADO', texto)
      setErrorPantalla(texto)
      setErrorMarcado(true)
      setMensaje('El asistente registró el error que aparece en tu pantalla. Si ya terminaste, presiona "Terminar".')
      return 'Error registrado.'
    }
    return 'Función no disponible.'
  }

  // La grabación (consentida) llega a TI como evidencia y adjunto del ticket.
  async function guardarGrabacion() {
    const grabador = grabadorRef.current
    const actual = invitacionRef.current
    grabadorRef.current = null
    if (!grabador || !actual) return
    const video = await grabador.detener()
    if (!video) return
    try {
      await subirGrabacionReproduccion(actual.sesionNumero, video, grabador.segundos)
    } catch (e) {
      setError(mensajeError(e, 'No fue posible enviar la grabación de tu pantalla.'))
    }
  }

  const iniciarLive = () =>
    ejecutar(async () => {
      const token = await crearTokenReproduccion(invitacion!.sesionNumero)
      if (!token.disponible) throw new Error(token.mensaje || 'La asistencia por voz no está disponible en este momento.')
      const live = new GeminiLiveSesion({
        onEstado: setEstadoLive,
        onTranscripcion: agregarTurno,
        onFuncion: atenderFuncion,
        onError: setError,
        onPantallaFinalizada: () => {
          void guardarGrabacion()
          liveRef.current = null
          setLiveActivo(false)
          setEstadoLive('Dejaste de compartir la pantalla. Puedes volver a compartirla o terminar.')
        },
        onDesconexion: () => {
          void guardarGrabacion()
          liveRef.current = null
          setLiveActivo(false)
          setEstadoLive('La conexión de voz terminó. Puedes volver a compartir la pantalla para continuar.')
        },
      })
      liveRef.current = live
      try {
        const stream = await live.iniciar(token)
        if (videoRef.current) {
          videoRef.current.srcObject = stream
          void videoRef.current.play()
        }
        const grabador = new GrabadorPantalla()
        if (grabador.iniciar(stream)) grabadorRef.current = grabador
        await registrar('INICIO_LIVE', 'El usuario empezó a compartir su pantalla para reproducir el error.')
        setSilenciado(false)
        setLiveActivo(true)
        setEstadoLive(
          live.tieneMicrofono
            ? 'Compartiendo pantalla · cuéntale al asistente qué estás haciendo'
            : 'Compartiendo pantalla sin micrófono · escribe tus respuestas abajo',
        )
      } catch (e) {
        await live.detener()
        liveRef.current = null
        throw e
      }
    }, 'No fue posible compartir la pantalla.')

  async function detenerLive() {
    await liveRef.current?.detener()
    liveRef.current = null
    setLiveActivo(false)
    await guardarGrabacion()
    if (videoRef.current) videoRef.current.srcObject = null
  }

  function enviarTexto() {
    const texto = textoLive.trim()
    if (!texto || !liveRef.current) return
    liveRef.current.enviarTexto(texto)
    agregarTurno('usuario', texto)
    setTextoLive('')
  }

  function alternarMicrofono() {
    const nuevo = !silenciado
    liveRef.current?.silenciarMicrofono(nuevo)
    setSilenciado(nuevo)
  }

  const marcarError = () =>
    ejecutar(async () => {
      const texto = errorPantalla.trim()
      if (!texto) throw new Error('Escribe el mensaje de error tal como aparece en tu pantalla.')
      await registrar('ERROR_OBSERVADO', texto.slice(0, 1000))
      setErrorMarcado(true)
      setMensaje('Error registrado. ¡Gracias! Si ya terminaste, presiona "Terminar".')
    }, 'No fue posible registrar el error.')

  const terminar = () =>
    ejecutar(async () => {
      await detenerLive()
      await finalizarReproduccion(invitacion!.sesionNumero)
      seleccionarSesionTraza(null)
      setTerminada(true)
      setParametros({}, { replace: true })
      setMensaje('Listo. TI ya tiene la información de tu sesión y continuará con tu ticket.')
    }, 'No fue posible terminar la sesión.')

  // Al cerrar sesión se detiene la sesión de pantalla y la reproducción deja de marcarse.
  async function antesDeSalir() {
    await detenerLive()
    seleccionarSesionTraza(null)
  }

  return (
    <MarcoPortal
      antesDeSalir={antesDeSalir}
      menu="usuario"
      clase="repro-shell"
      ayuda={{ titulo: 'Tú tienes el control', detalle: 'Empieza, pausa o termina cuando quieras' }}
      sinFrase
      barra={
        <div className="repro-topbar__titulo">
          <span>
            <Icono nombre="pantalla" size={19} />
          </span>
          <div>
            <strong>Mostrar el error a TI</strong>
            <small>Sesión guiada de pantalla y voz</small>
          </div>
        </div>
      }
    >
      <main className="repro-contenido">
        {(error || mensaje) && (
          <div className={`repro-aviso ${error ? 'repro-aviso--error' : ''}`} role={error ? 'alert' : 'status'}>
            <Icono nombre={error ? 'alerta' : 'check'} size={17} />
            <span>{error || mensaje}</span>
          </div>
        )}

        {terminada ? (
          <section className="repro-tarjeta repro-final">
            <Icono nombre="check" size={34} />
            <h1>¡Gracias, {nombre}!</h1>
            <p>
              TI recibió lo que mostraste y la grabación de tu pantalla. El asistente ya está revisando el caso y te avisaremos cuando haya
              una solución para validar.
            </p>
            <button className="repro-btn repro-btn--primario" onClick={() => navigate('/mis-tickets')}>
              Ir a Mis Tickets
            </button>
          </section>
        ) : !invitacion ? (
          <section className="repro-tarjeta">
            <h1>Solicitudes de TI</h1>
            <p className="repro-tarjeta__texto">Cuando TI necesite ver cómo ocurre un error, te enviará una solicitud aquí.</p>
            {invitaciones === null ? (
              <p className="repro-vacio">Cargando...</p>
            ) : invitaciones.length === 0 ? (
              <p className="repro-vacio">No tienes solicitudes pendientes.</p>
            ) : (
              <div className="repro-lista">
                {invitaciones.map(x => (
                  <button key={x.sesionNumero} onClick={() => setParametros({ sesion: String(x.sesionNumero) })}>
                    <strong>
                      {x.incidenciaNumero} · {x.tituloTicket}
                    </strong>
                    <span>
                      {x.operadorTI} · {x.estadoInvitacion === 'ACEPTADA' ? 'aceptada, lista para continuar' : 'pendiente de tu respuesta'}
                    </span>
                    <small>Disponible hasta {horaLimite(x.invitacionExpira)}</small>
                  </button>
                ))}
              </div>
            )}
          </section>
        ) : invitacion.estadoInvitacion === 'PENDIENTE' ? (
          <section className="repro-tarjeta">
            <span className="repro-etiqueta">
              <Icono nombre="escudo" size={14} /> Solicitud de {invitacion.operadorTI}
            </span>
            <h1>¿Nos muestras cómo ocurre el error?</h1>
            <p className="repro-tarjeta__texto">
              Ticket <strong>{invitacion.incidenciaNumero}</strong> · {invitacion.tituloTicket}. TI necesita ver los pasos exactos para
              encontrar la causa más rápido.
            </p>
            <ul className="repro-puntos">
              <li>
                <Icono nombre="pantalla" size={16} />
                Compartirás la ventana o pantalla que tú elijas y conversarás con un asistente de voz que te guiará paso a paso.
              </li>
              <li>
                <Icono nombre="stop" size={16} />
                Puedes pausar o terminar cuando quieras. El navegador siempre te mostrará que estás compartiendo.
              </li>
              <li>
                <Icono nombre="escudo" size={16} />
                Se grabará la ventana que compartas (sin audio) junto con la conversación escrita y el error que marques. La grabación queda
                en tu ticket y solo TI la usa para resolverlo.
              </li>
              <li>
                <Icono nombre="alertaTriangulo" size={16} />
                No muestres contraseñas, códigos de verificación ni datos personales que no tengan relación con el error.
              </li>
            </ul>
            <label className="repro-consentimiento">
              <input type="checkbox" checked={consiente} onChange={e => setConsiente(e.target.checked)} />
              <span>
                Acepto compartir mi pantalla y mi voz, y que se grabe la pantalla compartida, para que TI analice el error de mi ticket.
              </span>
            </label>
            {rechazando && (
              <label className="repro-campo">
                ¿Quieres contarnos por qué? <small>opcional</small>
                <textarea
                  value={motivo}
                  onChange={e => setMotivo(e.target.value)}
                  maxLength={500}
                  rows={2}
                  placeholder="Ej. Ya no ocurre el error / Prefiero que me llamen"
                />
              </label>
            )}
            <div className="repro-botones">
              {rechazando ? (
                <>
                  <button className="repro-btn" onClick={() => setRechazando(false)} disabled={procesando}>
                    Volver
                  </button>
                  <button className="repro-btn repro-btn--peligro" onClick={() => void rechazar()} disabled={procesando}>
                    Confirmar que no puedo
                  </button>
                </>
              ) : (
                <>
                  <button className="repro-btn" onClick={() => setRechazando(true)} disabled={procesando}>
                    Ahora no
                  </button>
                  <button className="repro-btn repro-btn--primario" onClick={() => void aceptar()} disabled={procesando || !consiente}>
                    <Icono nombre="check" size={16} />
                    Aceptar y continuar
                  </button>
                </>
              )}
            </div>
            <small className="repro-vence">Esta solicitud está disponible hasta {horaLimite(invitacion.invitacionExpira)}.</small>
          </section>
        ) : (
          <section className="repro-sesion">
            <div className="repro-tarjeta repro-visor">
              <div className="repro-visor__pantalla">
                <video ref={videoRef} muted playsInline />
                {!liveActivo && (
                  <div className="repro-visor__vacio">
                    <Icono nombre="pantalla" size={34} />
                    <strong>Tu pantalla no se está compartiendo</strong>
                    <span>Abre el sistema donde ocurre el error y luego presiona "Compartir pantalla".</span>
                  </div>
                )}
              </div>
              <p className="repro-visor__estado">
                <span className={liveActivo ? 'repro-pulso' : ''} />
                {estadoLive}
              </p>
              <div className="repro-botones">
                {!liveActivo ? (
                  <button className="repro-btn repro-btn--primario" onClick={() => void iniciarLive()} disabled={procesando}>
                    <Icono nombre="pantalla" size={16} />
                    {turnos.length ? 'Volver a compartir pantalla' : 'Compartir pantalla y empezar'}
                  </button>
                ) : (
                  <>
                    <button className="repro-btn" onClick={() => void detenerLive()}>
                      <Icono nombre="stop" size={15} />
                      Pausar
                    </button>
                    <button className="repro-btn" onClick={alternarMicrofono} aria-pressed={silenciado}>
                      <Icono nombre="microfono" size={15} />
                      {silenciado ? 'Activar micrófono' : 'Silenciar micrófono'}
                    </button>
                  </>
                )}
                <button className="repro-btn repro-btn--peligro" onClick={() => void terminar()} disabled={procesando}>
                  Terminar
                </button>
              </div>
              {liveActivo && (
                <form
                  className="repro-fila"
                  onSubmit={e => {
                    e.preventDefault()
                    enviarTexto()
                  }}
                >
                  <input
                    value={textoLive}
                    onChange={e => setTextoLive(e.target.value)}
                    maxLength={500}
                    placeholder="También puedes escribirle al asistente"
                  />
                  <button className="repro-btn" disabled={!textoLive.trim()}>
                    <Icono nombre="enviar" size={15} />
                    Enviar
                  </button>
                </form>
              )}
            </div>
            <aside className="repro-columna">
              <div className="repro-tarjeta repro-error">
                <strong>
                  <Icono nombre="alertaTriangulo" size={16} /> Cuando aparezca el error
                </strong>
                <p>Copia el mensaje tal como aparece y presiona "Marcar error".</p>
                <textarea
                  value={errorPantalla}
                  onChange={e => setErrorPantalla(e.target.value)}
                  maxLength={1000}
                  rows={3}
                  placeholder="Ej. No se encontró stock disponible para completar el proceso."
                />
                <button className="repro-btn repro-btn--alerta" onClick={() => void marcarError()} disabled={procesando}>
                  {errorMarcado ? 'Actualizar error' : 'Marcar error'}
                </button>
              </div>
              <div className="repro-tarjeta repro-conversacion">
                <strong>
                  <Icono nombre="microfono" size={16} /> Conversación
                </strong>
                {turnos.length === 0 ? (
                  <p className="repro-vacio">Aquí verás lo que se conversa durante la sesión.</p>
                ) : (
                  <div>
                    {turnos.slice(-10).map((t, i) => (
                      <p key={i} className={`repro-turno repro-turno--${t.rol}`}>
                        <b>{t.rol === 'usuario' ? 'Tú' : 'Asistente'}</b>
                        {t.texto}
                      </p>
                    ))}
                  </div>
                )}
              </div>
            </aside>
          </section>
        )}
      </main>
    </MarcoPortal>
  )
}
