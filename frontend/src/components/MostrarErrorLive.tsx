/**
 * Archivo: MostrarErrorLive.tsx
 * Objetivo: Permitir que el colaborador muestre su error compartiendo pantalla antes de registrar el ticket.
 * Responsabilidad: Pedir consentimiento, conducir la sesión Live, reunir los pasos y el error que registra el asistente
 *   y convertirlos en un borrador de ticket para TI.
 * Consideraciones: La pantalla no se guarda; la evidencia vive en memoria hasta que el colaborador la lleva a su ticket.
 *   Las funciones del modelo Live solo agregan evidencia: no cambian nada en los sistemas.
 */

import { useEffect, useRef, useState } from 'react'
import { GeminiLiveSesion } from '../features/asistente/services/geminiLiveTIService'
import { GrabadorPantalla } from '../features/asistente/services/grabadorPantalla'
import {
  crearTokenLiveColaborador,
  prepararBorradorEvidencia,
  type AsistenteUsuarioAccion,
  type EvidenciaReproduccion,
} from '../features/asistente/services/asistenteUsuarioService'
import '../pages/ReproduccionUsuarioPage.css'
import './MostrarErrorLive.css'

/** Lo que viaja al formulario del ticket: la grabación como adjunto y la evidencia que activa la investigación automática. */
export interface EvidenciaParaTicket {
  grabaciones: File[]
  evidencia: EvidenciaReproduccion
}

interface Props {
  descripcion: string
  onCerrar: () => void
  onBorrador: (accion: AsistenteUsuarioAccion, extra: EvidenciaParaTicket) => void
}

type Turno = { rol: 'usuario' | 'agente'; texto: string }

export default function MostrarErrorLive({ descripcion, onCerrar, onBorrador }: Props) {
  const [consiente, setConsiente] = useState(false)
  const [iniciado, setIniciado] = useState(false)
  const [liveActivo, setLiveActivo] = useState(false)
  const [estado, setEstado] = useState('Abre el sistema donde ocurre el error y luego comparte tu pantalla.')
  const [turnos, setTurnos] = useState<Turno[]>([])
  const [pasos, setPasos] = useState<string[]>([])
  const [errorPantalla, setErrorPantalla] = useState('')
  const [textoLive, setTextoLive] = useState('')
  const [silenciado, setSilenciado] = useState(false)
  const [procesando, setProcesando] = useState(false)
  const [error, setError] = useState('')
  const liveRef = useRef<GeminiLiveSesion | null>(null)
  const grabadorRef = useRef<GrabadorPantalla | null>(null)
  // Cada vez que se comparte la pantalla se graba un tramo; al ticket viajan los dos últimos.
  const tramosRef = useRef<Blob[]>([])
  const videoRef = useRef<HTMLVideoElement>(null)

  useEffect(() => () => { void liveRef.current?.detener() }, [])

  // El asistente registra lo que ve: cada paso y el mensaje exacto del error.
  function atenderFuncion(nombre: string, argumentos: Record<string, unknown>) {
    const texto = String(argumentos.descripcion ?? argumentos.mensaje ?? '').trim().slice(0, 300)
    if (!texto) return 'La descripción está vacía; no se registró.'
    if (nombre === 'registrar_paso') { setPasos(actual => [...actual, texto].slice(-30)); return 'Paso registrado.' }
    if (nombre === 'registrar_error') { setErrorPantalla(String(argumentos.mensaje ?? texto).slice(0, 1000)); setEstado('El asistente registró el mensaje de error. Si ya terminaste, prepara tu ticket.'); return 'Error registrado.' }
    return 'Función no disponible.'
  }

  async function compartir() {
    if (procesando) return
    setProcesando(true); setError('')
    try {
      const token = await crearTokenLiveColaborador(descripcion)
      if (!token.disponible) throw new Error(token.mensaje || 'La asistencia por voz no está disponible en este momento.')
      const live = new GeminiLiveSesion({
        onEstado: setEstado,
        onTranscripcion: (rol, texto) => setTurnos(actual => [...actual, { rol, texto }].slice(-40)),
        onFuncion: atenderFuncion,
        onError: setError,
        onPantallaFinalizada: () => { liveRef.current = null; setLiveActivo(false); setEstado('Dejaste de compartir la pantalla. Puedes volver a compartirla o preparar tu ticket.') },
        onDesconexion: () => { liveRef.current = null; setLiveActivo(false); setEstado('La conexión de voz terminó. Puedes volver a compartir la pantalla o preparar tu ticket.') },
      })
      liveRef.current = live
      try {
        const stream = await live.iniciar(token)
        if (videoRef.current) { videoRef.current.srcObject = stream; void videoRef.current.play() }
        const grabador = new GrabadorPantalla()
        if (grabador.iniciar(stream)) grabadorRef.current = grabador
        setIniciado(true); setLiveActivo(true); setSilenciado(false)
        setEstado(live.tieneMicrofono ? 'Compartiendo pantalla · cuéntale al asistente qué estás haciendo' : 'Compartiendo pantalla sin micrófono · escribe abajo')
      } catch (e) { await live.detener(); liveRef.current = null; throw e }
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No fue posible compartir la pantalla.')
    } finally { setProcesando(false) }
  }

  async function pausar() {
    await liveRef.current?.detener(); liveRef.current = null; setLiveActivo(false)
    if (videoRef.current) videoRef.current.srcObject = null
    const grabador = grabadorRef.current
    grabadorRef.current = null
    const tramo = await grabador?.detener()
    if (tramo) tramosRef.current = [...tramosRef.current, tramo].slice(-2)
  }

  function enviarTexto() {
    const texto = textoLive.trim()
    if (!texto || !liveRef.current) return
    liveRef.current.enviarTexto(texto); setTurnos(actual => [...actual, { rol: 'usuario' as const, texto }].slice(-40)); setTextoLive('')
  }

  function alternarMicrofono() { const nuevo = !silenciado; liveRef.current?.silenciarMicrofono(nuevo); setSilenciado(nuevo) }

  async function prepararTicket() {
    if (procesando) return
    setProcesando(true); setError('')
    try {
      await pausar()
      const evidencia: EvidenciaReproduccion = {
        descripcion, pasos, mensajeError: errorPantalla.trim(),
        conversacion: turnos.map(t => ({ rol: t.rol === 'usuario' ? 'usuario' as const : 'asistente' as const, contenido: t.texto.slice(0, 1000) })),
      }
      const borrador = await prepararBorradorEvidencia(evidencia)
      const grabaciones = tramosRef.current.map((tramo, i) => new File([tramo], `grabacion-pantalla-${i + 1}.webm`, { type: 'video/webm' }))
      onBorrador(borrador, { grabaciones, evidencia })
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No fue posible preparar el ticket.')
    } finally { setProcesando(false) }
  }

  async function cerrar() { await pausar(); onCerrar() }

  return <div className="mostrar-error" role="dialog" aria-modal="true" aria-label="Mostrar el error en pantalla">
    <div className="mostrar-error__panel">
      <header className="mostrar-error__cabecera">
        <div><strong>Mostrar el error en pantalla</strong><small>El asistente te guía y prepara tu ticket con los pasos y el error exacto</small></div>
        <button type="button" onClick={() => void cerrar()} aria-label="Cerrar">×</button>
      </header>

      {error && <div className="repro-aviso repro-aviso--error" role="alert"><span>{error}</span></div>}

      {!iniciado ? <section className="repro-tarjeta">
        <ul className="repro-puntos">
          <li>Compartirás solo la ventana o pantalla que elijas y conversarás con un asistente de voz que te guiará paso a paso.</li>
          <li>Puedes pausar o cerrar cuando quieras; el navegador siempre te mostrará que estás compartiendo.</li>
          <li>Se grabará la ventana que compartas (sin audio) para adjuntarla a tu ticket. Al final verás el borrador y decidirás si lo envías.</li>
          <li>No muestres contraseñas, códigos de verificación ni datos personales que no tengan relación con el error.</li>
        </ul>
        <label className="repro-consentimiento"><input type="checkbox" checked={consiente} onChange={e => setConsiente(e.target.checked)}/><span>Acepto compartir mi pantalla y mi voz, y que se grabe la pantalla compartida, para preparar mi ticket.</span></label>
        <div className="repro-botones">
          <button className="repro-btn" type="button" onClick={onCerrar}>Ahora no</button>
          <button className="repro-btn repro-btn--primario" type="button" onClick={() => void compartir()} disabled={!consiente || procesando}>{procesando ? 'Conectando...' : 'Compartir pantalla y empezar'}</button>
        </div>
      </section>
      : <section className="mostrar-error__sesion">
        <div className="repro-tarjeta">
          <div className="repro-visor__pantalla"><video ref={videoRef} muted playsInline/>{!liveActivo && <div className="repro-visor__vacio"><strong>Tu pantalla no se está compartiendo</strong><span>Puedes volver a compartirla o preparar tu ticket con lo registrado.</span></div>}</div>
          <p className="repro-visor__estado"><span className={liveActivo ? 'repro-pulso' : ''}/>{estado}</p>
          <div className="repro-botones">
            {!liveActivo ? <button className="repro-btn" type="button" onClick={() => void compartir()} disabled={procesando}>Volver a compartir</button>
              : <><button className="repro-btn" type="button" onClick={() => void pausar()}>Pausar</button><button className="repro-btn" type="button" onClick={alternarMicrofono} aria-pressed={silenciado}>{silenciado ? 'Activar micrófono' : 'Silenciar micrófono'}</button></>}
            <button className="repro-btn repro-btn--primario" type="button" onClick={() => void prepararTicket()} disabled={procesando || (pasos.length === 0 && !errorPantalla.trim() && turnos.length === 0)}>{procesando ? 'Preparando...' : 'Terminar y preparar ticket'}</button>
          </div>
          {liveActivo && <form className="repro-fila" onSubmit={e => { e.preventDefault(); enviarTexto() }}><input value={textoLive} onChange={e => setTextoLive(e.target.value)} maxLength={500} placeholder="También puedes escribirle al asistente"/><button className="repro-btn" disabled={!textoLive.trim()}>Enviar</button></form>}
        </div>
        <aside className="repro-columna">
          <div className="repro-tarjeta mostrar-error__pasos"><strong>Pasos registrados</strong>{pasos.length === 0 ? <p className="repro-vacio">El asistente anotará aquí cada paso que vea en tu pantalla.</p> : <ol>{pasos.map((p, i) => <li key={i}>{p}</li>)}</ol>}</div>
          <div className="repro-tarjeta repro-error"><strong>Mensaje de error</strong><p>El asistente lo copia cuando aparece; puedes corregirlo.</p><textarea value={errorPantalla} onChange={e => setErrorPantalla(e.target.value)} maxLength={1000} rows={3} placeholder="Ej. No se encontró stock disponible para completar el proceso."/></div>
          <div className="repro-tarjeta repro-conversacion"><strong>Conversación</strong>{turnos.length === 0 ? <p className="repro-vacio">Aquí verás lo que se conversa.</p> : <div>{turnos.slice(-8).map((t, i) => <p key={i} className={`repro-turno repro-turno--${t.rol}`}><b>{t.rol === 'usuario' ? 'Tú' : 'Asistente'}</b>{t.texto}</p>)}</div>}</div>
        </aside>
      </section>}
    </div>
  </div>
}
