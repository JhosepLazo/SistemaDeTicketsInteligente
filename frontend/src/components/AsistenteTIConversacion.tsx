import { useRef, useState } from 'react'
import { Send, Check, UserPlus } from 'lucide-react'
import { consultarAsistenteTI, confirmarAccionTI, type AsistenteTIRespuesta } from '../features/asistente/services/asistenteTIService'

type Mensaje = { rol: 'usuario' | 'asistente'; contenido: string; respuesta?: AsistenteTIRespuesta; confirmado?: boolean }

export default function AsistenteTIConversacion() {
  const [mensajes, setMensajes] = useState<Mensaje[]>([])
  const [texto, setTexto] = useState('')
  const [ocupado, setOcupado] = useState(false)
  const [error, setError] = useState('')
  const bloqueo = useRef(false)
  async function enviar() {
    if (bloqueo.current || texto.trim().length < 3) return
    bloqueo.current = true; setOcupado(true); setError('')
    const contenido = texto.trim()
    try {
      const respuesta = await consultarAsistenteTI(contenido, mensajes.map(m => ({ rol: m.rol, contenido: m.contenido })).slice(-10))
      setMensajes(actual => [...actual, { rol: 'usuario', contenido }, { rol: 'asistente', contenido: respuesta.respuesta, respuesta }])
      setTexto('')
    } catch (e) { setError(e instanceof Error ? e.message : 'No se pudo consultar al asistente.') }
    finally { bloqueo.current = false; setOcupado(false) }
  }
  async function confirmar(indice: number) {
    const accion = mensajes[indice].respuesta?.accion
    if (!accion?.tokenConfirmacion || bloqueo.current) return
    if (!window.confirm(`¿Confirmas ${accion.titulo} para ${accion.usuario?.usuario}?`)) return
    bloqueo.current = true; setOcupado(true); setError('')
    try {
      const resultado = await confirmarAccionTI(accion.tokenConfirmacion)
      setMensajes(actual => actual.map((m, i) => i === indice ? { ...m, confirmado: true, contenido: `${m.contenido}\n\n${resultado.mensaje}` } : m))
    } catch (e) { setError(e instanceof Error ? e.message : 'No se pudo confirmar la acción.') }
    finally { bloqueo.current = false; setOcupado(false) }
  }
  return <section className="agente-chat" aria-label="Consulta TI">
    <header><h2>Consulta TI</h2><button className="agente-btn agente-btn--secundario" disabled={ocupado || !mensajes.length} onClick={() => { setMensajes([]); setError('') }}>Nueva conversación</button></header>
    <div className="agente-chat__mensajes" aria-live="polite">
      {!mensajes.length && <div className="agente-sugerencias">{['Ver áreas activas', 'Ver categorías', 'Consultar SLA', 'Crear usuario'].map(s => <button className="agente-btn agente-btn--secundario" key={s} onClick={() => setTexto(s)}>{s}</button>)}</div>}
      {mensajes.map((m, i) => <article key={i} className={`agente-chat__mensaje agente-chat__mensaje--${m.rol}`}><strong>{m.rol === 'usuario' ? 'Tú' : 'Asistente TI'}</strong><p>{m.contenido}</p>
        {!!m.respuesta?.fuentes.length && <small>{m.respuesta.fuentes.join(' · ')}</small>}
        {m.respuesta?.accion?.usuario && <dl className="agente-chat__propuesta"><dt>Usuario</dt><dd>{m.respuesta.accion.usuario.usuario}</dd><dt>Área</dt><dd>{m.respuesta.accion.usuario.areaDescripcion}</dd><dt>Perfil</dt><dd>{m.respuesta.accion.usuario.perfilDescripcion}</dd><dt>Correo</dt><dd>{m.respuesta.accion.usuario.correo || 'Sin correo'}</dd></dl>}
        {m.respuesta?.accion?.tokenConfirmacion && <button className="agente-btn agente-btn--primario" disabled={ocupado || m.confirmado || (!!m.respuesta.accion.expiraEn && Date.parse(m.respuesta.accion.expiraEn) < Date.now())} onClick={() => void confirmar(i)}>{m.confirmado ? <Check size={16}/> : <UserPlus size={16}/>} {m.confirmado ? 'Confirmado' : 'Confirmar usuario'}</button>}
      </article>)}
    </div>
    {error && <p className="agente-aviso agente-aviso--error" role="alert">{error}</p>}
    <form className="agente-chat__entrada" onSubmit={e => { e.preventDefault(); void enviar() }}><label htmlFor="consulta-ti">Consulta</label><textarea id="consulta-ti" value={texto} onChange={e => setTexto(e.target.value)} maxLength={1200} disabled={ocupado} rows={3}/><button className="agente-btn agente-btn--primario" disabled={ocupado || texto.trim().length < 3}><Send size={17}/>{ocupado ? 'Consultando...' : 'Enviar'}</button></form>
  </section>
}
