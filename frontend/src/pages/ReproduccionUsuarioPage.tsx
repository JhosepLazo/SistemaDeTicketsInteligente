/**
 * Archivo: ReproduccionUsuarioPage.tsx
 * Objetivo: Permitir que el colaborador muestre a TI cómo ocurre su error, compartiendo pantalla y voz por invitación.
 * Responsabilidad: Explicar y registrar el consentimiento, conducir la sesión Live guiada y enviar solo evidencia observacional.
 * Consideraciones: El usuario decide cuándo empezar, pausar y terminar; la pantalla no se graba y el diagnóstico permanece exclusivamente en TI.
 */

import { useEffect, useRef, useState, type ReactNode } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import NotificacionesCampana from '../components/NotificacionesCampana'
import { useAutenticacion } from '../features/autenticacion/context/AutenticacionContext'
import { GeminiLiveSesion } from '../features/asistente/services/geminiLiveTIService'
import {
  crearTokenReproduccion,
  finalizarReproduccion,
  listarReproducciones,
  obtenerReproduccion,
  registrarEventoReproduccion,
  responderReproduccion,
  type ReproduccionInvitacion,
} from '../features/reproduccion/services/reproduccionUsuarioService'
import { seleccionarSesionTraza } from '../shared/services/observabilidadAgente'
import './InicioPage.css'
import './ReproduccionUsuarioPage.css'

type NombreIcono = 'inicio' | 'asistente' | 'nuevo' | 'tickets' | 'salir' | 'pantalla' | 'microfono' | 'escudo' | 'alerta' | 'check' | 'stop' | 'enviar'

function Icono({ nombre, size = 20 }: { nombre: NombreIcono; size?: number }) {
  const trazos: Record<NombreIcono, ReactNode> = {
    inicio: <><path d="M3 11.5 12 4l9 7.5"/><path d="M5.5 10.5V20h13v-9.5"/><path d="M9.5 20v-6h5v6"/></>,
    asistente: <><path d="m12 3 1.2 3.1L16 7.5l-2.8 1.4L12 12l-1.2-3.1L8 7.5l2.8-1.4L12 3Z"/><path d="m5 13 .8 2.2L8 16l-2.2.8L5 19l-.8-2.2L2 16l2.2-.8L5 13Z"/></>,
    nuevo: <><circle cx="12" cy="12" r="9"/><path d="M12 8v8M8 12h8"/></>,
    tickets: <><path d="M5 4h14a1 1 0 0 1 1 1v14a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V5a1 1 0 0 1 1-1Z"/><path d="M8 9h8M8 13h6M8 17h4"/></>,
    salir: <><path d="M10 5H5v14h5"/><path d="m14 8 4 4-4 4M18 12H9"/></>,
    pantalla: <><rect x="3" y="4" width="18" height="13" rx="2"/><path d="M8 21h8M12 17v4"/></>,
    microfono: <><rect x="9" y="3" width="6" height="11" rx="3"/><path d="M5 11a7 7 0 0 0 14 0M12 18v3"/></>,
    escudo: <><path d="M12 3 5 6v5c0 4.8 2.9 8.1 7 10 4.1-1.9 7-5.2 7-10V6l-7-3Z"/><path d="m9 12 2 2 4-5"/></>,
    alerta: <><path d="M12 3 2.5 20h19L12 3Z"/><path d="M12 9v5M12 17h.01"/></>,
    check: <><circle cx="12" cy="12" r="9"/><path d="m8 12 2.5 2.5L16 9"/></>,
    stop: <><rect x="6" y="6" width="12" height="12" rx="1"/></>,
    enviar: <><path d="m3 11 18-8-7 18-3-7-8-3Z"/><path d="m11 14 4-4"/></>,
  }
  return <svg className="inicio-icono" width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">{trazos[nombre]}</svg>
}

type Turno = { rol: 'usuario' | 'agente'; texto: string }
const primerNombre = (nombre: string) => nombre.trim().split(/\s+/)[0] || nombre
const mensajeError = (e: unknown, defecto: string) => e instanceof Error ? e.message : defecto
const horaLimite = (valor: string) => new Date(valor).toLocaleString('es-PE', { dateStyle: 'short', timeStyle: 'short' })

export default function ReproduccionUsuarioPage() {
  const navigate = useNavigate()
  const [parametros, setParametros] = useSearchParams()
  const { usuario, cerrarSesion } = useAutenticacion()
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
  const invitacionRef = useRef<ReproduccionInvitacion | null>(null)
  const sesionParam = parametros.get('sesion')

  useEffect(() => () => { void liveRef.current?.detener() }, [])

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
    try { setInvitaciones(await listarReproducciones()) } catch (e) { setInvitaciones([]); setError(mensajeError(e, 'No fue posible cargar tus solicitudes.')) }
  }

  async function abrir(sesion: number) {
    setError(''); setMensaje(''); setTerminada(false); setTurnos([]); setErrorPantalla(''); setErrorMarcado(false); setConsiente(false); setRechazando(false)
    try { setInvitacion(await obtenerReproduccion(sesion)) }
    catch (e) { setInvitacion(null); setError(mensajeError(e, 'La solicitud ya no está disponible.')); void cargar() }
  }

  async function ejecutar(accion: () => Promise<void>, defecto: string) {
    if (procesando) return
    setProcesando(true); setError('')
    try { await accion() } catch (e) { setError(mensajeError(e, defecto)) } finally { setProcesando(false) }
  }

  const aceptar = () => ejecutar(async () => {
    await responderReproduccion(invitacion!.sesionNumero, true, true)
    setInvitacion({ ...invitacion!, estadoInvitacion: 'ACEPTADA' })
    setMensaje('Gracias. Cuando quieras, comparte la ventana donde ocurre el error y sigue las indicaciones del asistente.')
  }, 'No fue posible aceptar la solicitud.')

  const rechazar = () => ejecutar(async () => {
    await responderReproduccion(invitacion!.sesionNumero, false, false, motivo.trim())
    setParametros({}, { replace: true }); setMensaje('Avisamos a TI que no puedes realizar la sesión. Seguirán atendiendo tu ticket con la información disponible.')
    await cargar()
  }, 'No fue posible responder la solicitud.')

  function registrar(tipo: string, contenido: string) {
    const actual = invitacionRef.current
    if (!actual || !contenido.trim()) return Promise.resolve()
    return registrarEventoReproduccion(actual.sesionNumero, tipo, contenido.trim().slice(0, 12000))
  }

  function agregarTurno(rol: 'usuario' | 'agente', texto: string) {
    setTurnos(actual => [...actual, { rol, texto }])
    void registrar(rol === 'usuario' ? 'TRANSCRIPCION_USUARIO' : 'TRANSCRIPCION_AGENTE', texto).catch(() => setEstadoLive('Una parte de la conversación no pudo guardarse; puedes continuar.'))
  }

  // El asistente registra los pasos que ve y el error exacto; el usuario no tiene que copiarlo a mano.
  async function atenderFuncion(nombre: string, argumentos: Record<string, unknown>) {
    const texto = String(argumentos.descripcion ?? argumentos.mensaje ?? '').trim().slice(0, 1000)
    if (!texto) return 'La descripción está vacía; no se registró.'
    if (nombre === 'registrar_paso') { await registrar('PASO_OBSERVADO', texto); return 'Paso registrado.' }
    if (nombre === 'registrar_error') {
      await registrar('ERROR_OBSERVADO', texto)
      setErrorPantalla(texto); setErrorMarcado(true); setMensaje('El asistente registró el error que aparece en tu pantalla. Si ya terminaste, presiona "Terminar".')
      return 'Error registrado.'
    }
    return 'Función no disponible.'
  }

  const iniciarLive = () => ejecutar(async () => {
    const token = await crearTokenReproduccion(invitacion!.sesionNumero)
    if (!token.disponible) throw new Error(token.mensaje || 'La asistencia por voz no está disponible en este momento.')
    const live = new GeminiLiveSesion({
      onEstado: setEstadoLive,
      onTranscripcion: agregarTurno,
      onFuncion: atenderFuncion,
      onError: setError,
      onPantallaFinalizada: () => { liveRef.current = null; setLiveActivo(false); setEstadoLive('Dejaste de compartir la pantalla. Puedes volver a compartirla o terminar.') },
      onDesconexion: () => { liveRef.current = null; setLiveActivo(false); setEstadoLive('La conexión de voz terminó. Puedes volver a compartir la pantalla para continuar.') },
    })
    liveRef.current = live
    try {
      const stream = await live.iniciar(token)
      if (videoRef.current) { videoRef.current.srcObject = stream; void videoRef.current.play() }
      await registrar('INICIO_LIVE', 'El usuario empezó a compartir su pantalla para reproducir el error.')
      setSilenciado(false); setLiveActivo(true)
      setEstadoLive(live.tieneMicrofono ? 'Compartiendo pantalla · cuéntale al asistente qué estás haciendo' : 'Compartiendo pantalla sin micrófono · escribe tus respuestas abajo')
    } catch (e) { await live.detener(); liveRef.current = null; throw e }
  }, 'No fue posible compartir la pantalla.')

  async function detenerLive() {
    await liveRef.current?.detener(); liveRef.current = null; setLiveActivo(false)
    if (videoRef.current) videoRef.current.srcObject = null
  }

  function enviarTexto() {
    const texto = textoLive.trim()
    if (!texto || !liveRef.current) return
    liveRef.current.enviarTexto(texto); agregarTurno('usuario', texto); setTextoLive('')
  }

  function alternarMicrofono() { const nuevo = !silenciado; liveRef.current?.silenciarMicrofono(nuevo); setSilenciado(nuevo) }

  const marcarError = () => ejecutar(async () => {
    const texto = errorPantalla.trim()
    if (!texto) throw new Error('Escribe el mensaje de error tal como aparece en tu pantalla.')
    await registrar('ERROR_OBSERVADO', texto.slice(0, 1000)); setErrorMarcado(true); setMensaje('Error registrado. ¡Gracias! Si ya terminaste, presiona "Terminar".')
  }, 'No fue posible registrar el error.')

  const terminar = () => ejecutar(async () => {
    await detenerLive()
    await finalizarReproduccion(invitacion!.sesionNumero)
    seleccionarSesionTraza(null); setTerminada(true); setParametros({}, { replace: true })
    setMensaje('Listo. TI ya tiene la información de tu sesión y continuará con tu ticket.')
  }, 'No fue posible terminar la sesión.')

  async function salir() { await detenerLive(); seleccionarSesionTraza(null); await cerrarSesion(); navigate('/login', { replace: true }) }

  return <div className="inicio-shell repro-shell">
    <aside className="inicio-sidebar" aria-label="Navegación principal">
      <div className="inicio-marca"><span className="inicio-marca__nombre">CALIMOD</span><span className="inicio-marca__sistema">Sistema inteligente<br/>de incidencias TI</span></div>
      <nav className="inicio-menu">
        <button className="inicio-menu__item" type="button" onClick={() => navigate('/inicio')}><Icono nombre="inicio"/><span>Inicio</span></button>
        <button className="inicio-menu__item" type="button" onClick={() => navigate('/asistente')}><Icono nombre="asistente"/><span>Asistente TI</span></button>
        <button className="inicio-menu__item" type="button" onClick={() => navigate('/nuevo-ticket')}><Icono nombre="nuevo"/><span>Nuevo Ticket</span></button>
        <button className="inicio-menu__item" type="button" onClick={() => navigate('/mis-tickets')}><Icono nombre="tickets"/><span>Mis Tickets</span></button>
      </nav>
      <div className="inicio-sidebar__pie"><span className="inicio-sidebar__ayuda-icono">?</span><span>Tú tienes el control</span><small>Empieza, pausa o termina cuando quieras</small></div>
    </aside>

    <section className="inicio-principal">
      <header className="inicio-topbar">
        <div className="repro-topbar__titulo"><span><Icono nombre="pantalla" size={19}/></span><div><strong>Mostrar el error a TI</strong><small>Sesión guiada de pantalla y voz</small></div></div>
        <div className="inicio-topbar__usuario"><NotificacionesCampana/><div className="inicio-avatar">{nombre[0]?.toUpperCase()}</div><div className="inicio-identidad"><strong>{nombre}</strong><span>Colaborador</span></div><button className="inicio-salir" type="button" onClick={() => void salir()} title="Cerrar sesión" aria-label="Cerrar sesión"><Icono nombre="salir" size={18}/></button></div>
      </header>

      <main className="repro-contenido">
        {(error || mensaje) && <div className={`repro-aviso ${error ? 'repro-aviso--error' : ''}`} role={error ? 'alert' : 'status'}><Icono nombre={error ? 'alerta' : 'check'} size={17}/><span>{error || mensaje}</span></div>}

        {terminada ? <section className="repro-tarjeta repro-final"><Icono nombre="check" size={34}/><h1>¡Gracias, {nombre}!</h1><p>TI recibió lo que mostraste y continuará con tu ticket. Te avisaremos cuando haya una solución para validar.</p><button className="repro-btn repro-btn--primario" onClick={() => navigate('/mis-tickets')}>Ir a Mis Tickets</button></section>
        : !invitacion ? <section className="repro-tarjeta">
            <h1>Solicitudes de TI</h1>
            <p className="repro-tarjeta__texto">Cuando TI necesite ver cómo ocurre un error, te enviará una solicitud aquí.</p>
            {invitaciones === null ? <p className="repro-vacio">Cargando...</p> : invitaciones.length === 0 ? <p className="repro-vacio">No tienes solicitudes pendientes.</p> :
              <div className="repro-lista">{invitaciones.map(x => <button key={x.sesionNumero} onClick={() => setParametros({ sesion: String(x.sesionNumero) })}><strong>{x.incidenciaNumero} · {x.tituloTicket}</strong><span>{x.operadorTI} · {x.estadoInvitacion === 'ACEPTADA' ? 'aceptada, lista para continuar' : 'pendiente de tu respuesta'}</span><small>Disponible hasta {horaLimite(x.invitacionExpira)}</small></button>)}</div>}
          </section>
        : invitacion.estadoInvitacion === 'PENDIENTE' ? <section className="repro-tarjeta">
            <span className="repro-etiqueta"><Icono nombre="escudo" size={14}/> Solicitud de {invitacion.operadorTI}</span>
            <h1>¿Nos muestras cómo ocurre el error?</h1>
            <p className="repro-tarjeta__texto">Ticket <strong>{invitacion.incidenciaNumero}</strong> · {invitacion.tituloTicket}. TI necesita ver los pasos exactos para encontrar la causa más rápido.</p>
            <ul className="repro-puntos">
              <li><Icono nombre="pantalla" size={16}/>Compartirás la ventana o pantalla que tú elijas y conversarás con un asistente de voz que te guiará paso a paso.</li>
              <li><Icono nombre="stop" size={16}/>Puedes pausar o terminar cuando quieras. El navegador siempre te mostrará que estás compartiendo.</li>
              <li><Icono nombre="escudo" size={16}/>No se graba video: solo se guarda la conversación escrita y el error que marques, y solo TI lo usa para resolver tu ticket.</li>
              <li><Icono nombre="alerta" size={16}/>No muestres contraseñas, códigos de verificación ni datos personales que no tengan relación con el error.</li>
            </ul>
            <label className="repro-consentimiento"><input type="checkbox" checked={consiente} onChange={e => setConsiente(e.target.checked)}/><span>Acepto compartir mi pantalla y mi voz durante esta sesión para que TI analice el error de mi ticket.</span></label>
            {rechazando && <label className="repro-campo">¿Quieres contarnos por qué? <small>opcional</small><textarea value={motivo} onChange={e => setMotivo(e.target.value)} maxLength={500} rows={2} placeholder="Ej. Ya no ocurre el error / Prefiero que me llamen"/></label>}
            <div className="repro-botones">
              {rechazando ? <><button className="repro-btn" onClick={() => setRechazando(false)} disabled={procesando}>Volver</button><button className="repro-btn repro-btn--peligro" onClick={() => void rechazar()} disabled={procesando}>Confirmar que no puedo</button></>
                : <><button className="repro-btn" onClick={() => setRechazando(true)} disabled={procesando}>Ahora no</button><button className="repro-btn repro-btn--primario" onClick={() => void aceptar()} disabled={procesando || !consiente}><Icono nombre="check" size={16}/>Aceptar y continuar</button></>}
            </div>
            <small className="repro-vence">Esta solicitud está disponible hasta {horaLimite(invitacion.invitacionExpira)}.</small>
          </section>
        : <section className="repro-sesion">
            <div className="repro-tarjeta repro-visor">
              <div className="repro-visor__pantalla"><video ref={videoRef} muted playsInline/>{!liveActivo && <div className="repro-visor__vacio"><Icono nombre="pantalla" size={34}/><strong>Tu pantalla no se está compartiendo</strong><span>Abre el sistema donde ocurre el error y luego presiona "Compartir pantalla".</span></div>}</div>
              <p className="repro-visor__estado"><span className={liveActivo ? 'repro-pulso' : ''}/>{estadoLive}</p>
              <div className="repro-botones">
                {!liveActivo ? <button className="repro-btn repro-btn--primario" onClick={() => void iniciarLive()} disabled={procesando}><Icono nombre="pantalla" size={16}/>{turnos.length ? 'Volver a compartir pantalla' : 'Compartir pantalla y empezar'}</button>
                  : <><button className="repro-btn" onClick={() => void detenerLive()}><Icono nombre="stop" size={15}/>Pausar</button><button className="repro-btn" onClick={alternarMicrofono} aria-pressed={silenciado}><Icono nombre="microfono" size={15}/>{silenciado ? 'Activar micrófono' : 'Silenciar micrófono'}</button></>}
                <button className="repro-btn repro-btn--peligro" onClick={() => void terminar()} disabled={procesando}>Terminar</button>
              </div>
              {liveActivo && <form className="repro-fila" onSubmit={e => { e.preventDefault(); enviarTexto() }}><input value={textoLive} onChange={e => setTextoLive(e.target.value)} maxLength={500} placeholder="También puedes escribirle al asistente"/><button className="repro-btn" disabled={!textoLive.trim()}><Icono nombre="enviar" size={15}/>Enviar</button></form>}
            </div>
            <aside className="repro-columna">
              <div className="repro-tarjeta repro-error"><strong><Icono nombre="alerta" size={16}/> Cuando aparezca el error</strong><p>Copia el mensaje tal como aparece y presiona "Marcar error".</p><textarea value={errorPantalla} onChange={e => setErrorPantalla(e.target.value)} maxLength={1000} rows={3} placeholder="Ej. No se encontró stock disponible para completar el proceso."/><button className="repro-btn repro-btn--alerta" onClick={() => void marcarError()} disabled={procesando}>{errorMarcado ? 'Actualizar error' : 'Marcar error'}</button></div>
              <div className="repro-tarjeta repro-conversacion"><strong><Icono nombre="microfono" size={16}/> Conversación</strong>{turnos.length === 0 ? <p className="repro-vacio">Aquí verás lo que se conversa durante la sesión.</p> : <div>{turnos.slice(-10).map((t, i) => <p key={i} className={`repro-turno repro-turno--${t.rol}`}><b>{t.rol === 'usuario' ? 'Tú' : 'Asistente'}</b>{t.texto}</p>)}</div>}</div>
            </aside>
          </section>}
      </main>
    </section>
  </div>
}
