/**
 * Asistente TI para colaboradores: orientación basada en conocimiento publicado y tickets propios.
 * La conversación vive únicamente en memoria durante la sesión de esta pantalla.
 */

import { useCallback, useEffect, useRef, useState, type FormEvent, type KeyboardEvent, type ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import NotificacionesCampana from '../components/NotificacionesCampana'
import MostrarErrorLive, { type EvidenciaParaTicket } from '../components/MostrarErrorLive'
import { useAutenticacion } from '../features/autenticacion/context/AutenticacionContext'
import {
  consultarAsistente,
  type AsistenteFuente,
  type AsistenteMensajeHistorial,
  type AsistenteUsuarioAccion,
} from '../features/asistente/services/asistenteUsuarioService'
import { obtenerMisTickets, type MisTicketItem } from '../features/misTickets/services/misTicketsUsuarioService'
import './InicioPage.css'
import './AsistenteUsuarioPage.css'

type NombreIcono = 'inicio' | 'asistente' | 'nuevo' | 'tickets' | 'enviar' | 'salir' | 'escudo' | 'libro' | 'reloj' | 'flecha' | 'limpiar' | 'mensaje' | 'alerta' | 'pantalla'

function Icono({ nombre, size = 20 }: { nombre: NombreIcono; size?: number }) {
  const trazos: Record<NombreIcono, ReactNode> = {
    inicio: <><path d="M3 11.5 12 4l9 7.5"/><path d="M5.5 10.5V20h13v-9.5"/><path d="M9.5 20v-6h5v6"/></>,
    asistente: <><path d="m12 3 1.2 3.1L16 7.5l-2.8 1.4L12 12l-1.2-3.1L8 7.5l2.8-1.4L12 3Z"/><path d="m5 13 .8 2.2L8 16l-2.2.8L5 19l-.8-2.2L2 16l2.2-.8L5 13Z"/><path d="m19 12 .7 1.8L21.5 15l-1.8.7L19 17.5l-.7-1.8-1.8-.7 1.8-.7L19 12Z"/></>,
    nuevo: <><circle cx="12" cy="12" r="9"/><path d="M12 8v8M8 12h8"/></>,
    tickets: <><path d="M5 4h14a1 1 0 0 1 1 1v14a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V5a1 1 0 0 1 1-1Z"/><path d="M8 9h8M8 13h6M8 17h4"/></>,
    enviar: <><path d="m3 11 18-8-7 18-3-7-8-3Z"/><path d="m11 14 4-4"/></>,
    salir: <><path d="M10 5H5v14h5"/><path d="m14 8 4 4-4 4M18 12H9"/></>,
    escudo: <><path d="M12 3 5 6v5c0 4.8 2.9 8.1 7 10 4.1-1.9 7-5.2 7-10V6l-7-3Z"/><path d="m9 12 2 2 4-5"/></>,
    libro: <><path d="M4 5.5A3.5 3.5 0 0 1 7.5 2H11v17H7.5A3.5 3.5 0 0 0 4 22V5.5Z"/><path d="M20 5.5A3.5 3.5 0 0 0 16.5 2H13v17h3.5A3.5 3.5 0 0 1 20 22V5.5Z"/></>,
    reloj: <><circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/></>,
    flecha: <><path d="M5 12h14M15 8l4 4-4 4"/></>,
    limpiar: <><path d="m4 15 8-8 5 5-8 8H4v-5Z"/><path d="m10 9 5 5M13 20h7"/></>,
    mensaje: <><path d="M4 5h16v12H8l-4 4V5Z"/><path d="M8 9h8M8 13h5"/></>,
    alerta: <><circle cx="12" cy="12" r="9"/><path d="M12 7v6M12 17h.01"/></>,
    pantalla: <><rect x="3" y="4" width="18" height="13" rx="2"/><path d="M8 21h8M12 17v4"/></>,
  }
  return <svg className="inicio-icono" width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">{trazos[nombre]}</svg>
}

interface MensajeVista {
  id: number
  rol: 'usuario' | 'asistente'
  contenido: string
  modo?: 'IA' | 'CONOCIMIENTO'
  fuentes?: AsistenteFuente[]
  escalarATicket?: boolean
  sugerencias?: string[]
  accion?: AsistenteUsuarioAccion | null
}

const bienvenida: MensajeVista = {
  id: 0,
  rol: 'asistente',
  contenido: 'Hola. Puedo ayudarte a encontrar soluciones publicadas por TI, revisar el estado de tus tickets y orientarte antes de registrar una incidencia. Cuéntame qué necesitas resolver.',
  modo: 'CONOCIMIENTO',
}

const consultasRapidas = [
  'No puedo ingresar a un sistema',
  'Mi equipo está lento',
  'Consultar el estado de mis tickets',
]

const estadosCerrados = new Set(['RS', 'CA', 'CF', 'NP'])

function primerNombre(nombre: string) {
  return nombre.trim().split(/\s+/)[0] || nombre
}

function fechaRelativa(fecha: string) {
  const valor = new Date(fecha).getTime()
  if (!Number.isFinite(valor)) return 'Actualizado recientemente'
  const horas = Math.max(0, Math.floor((Date.now() - valor) / 3600000))
  if (horas < 1) return 'Actualizado hace menos de 1 h'
  if (horas < 24) return `Actualizado hace ${horas} h`
  const dias = Math.floor(horas / 24)
  return `Actualizado hace ${dias} ${dias === 1 ? 'día' : 'días'}`
}

export default function AsistenteUsuarioPage() {
  const navigate = useNavigate()
  const { usuario, cerrarSesion } = useAutenticacion()
  const [mensajes, setMensajes] = useState<MensajeVista[]>([bienvenida])
  const [consulta, setConsulta] = useState('')
  const [enviando, setEnviando] = useState(false)
  const [error, setError] = useState('')
  const [tickets, setTickets] = useState<MisTicketItem[]>([])
  const [estadoTickets, setEstadoTickets] = useState<'cargando' | 'listo' | 'error'>('cargando')
  const editorRef = useRef<HTMLTextAreaElement>(null)
  const finalRef = useRef<HTMLDivElement>(null)
  const secuenciaRef = useRef(1)
  // Descripción del problema con la que se abre "Mostrar el error"; null cuando el panel está cerrado.
  const [mostrarError, setMostrarError] = useState<string | null>(null)

  const cargarTickets = useCallback(async () => {
    setEstadoTickets('cargando')
    try {
      const datos = await obtenerMisTickets()
      setTickets(datos.tickets.filter(ticket => !estadosCerrados.has(ticket.estado)).slice(0, 4))
      setEstadoTickets('listo')
    } catch {
      setTickets([])
      setEstadoTickets('error')
    }
  }, [])

  useEffect(() => { void cargarTickets() }, [cargarTickets])

  useEffect(() => {
    finalRef.current?.scrollIntoView({ behavior: 'smooth', block: 'nearest' })
  }, [mensajes, enviando])

  useEffect(() => {
    function enfocarEditor(event: globalThis.KeyboardEvent) {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault()
        editorRef.current?.focus()
      }
    }
    window.addEventListener('keydown', enfocarEditor)
    return () => window.removeEventListener('keydown', enfocarEditor)
  }, [])

  if (!usuario) return null

  async function enviar(textoForzado?: string) {
    const texto = (textoForzado ?? consulta).trim()
    if (texto.length < 3 || enviando) return

    const historial: AsistenteMensajeHistorial[] = mensajes
      .filter(item => item.id !== 0)
      .slice(-10)
      .map(item => ({ rol: item.rol, contenido: item.contenido }))
    const mensajeUsuario: MensajeVista = { id: secuenciaRef.current++, rol: 'usuario', contenido: texto }
    setMensajes(actual => [...actual, mensajeUsuario])
    setConsulta('')
    setError('')
    setEnviando(true)

    try {
      const respuesta = await consultarAsistente(texto, historial)
      setMensajes(actual => [...actual, {
        id: secuenciaRef.current++,
        rol: 'asistente',
        contenido: respuesta.respuesta,
        modo: respuesta.modo,
        fuentes: respuesta.fuentes,
        escalarATicket: respuesta.escalarATicket,
        sugerencias: respuesta.sugerencias,
        accion: respuesta.accion,
      }])
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No fue posible completar la consulta.')
    } finally {
      setEnviando(false)
      window.setTimeout(() => editorRef.current?.focus(), 0)
    }
  }

  function manejarEnvio(event: FormEvent) {
    event.preventDefault()
    void enviar()
  }

  function manejarTecla(event: KeyboardEvent<HTMLTextAreaElement>) {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault()
      void enviar()
    }
  }

  function usarSugerencia(sugerencia: string) {
    if (sugerencia === 'Abrir Mis Tickets') {
      navigate('/mis-tickets')
      return
    }
    void enviar(sugerencia)
  }

  function crearTicket(accion?: AsistenteUsuarioAccion | null, extra?: EvidenciaParaTicket) {
    navigate('/nuevo-ticket', accion ? { state: { asistenteBorrador: { titulo: accion.titulo, detalle: accion.detalle, mensajeError: accion.mensajeError ?? '', grabaciones: extra?.grabaciones ?? [], evidencia: extra?.evidencia } } } : undefined)
  }

  // El problema que el colaborador describió en el chat orienta al asistente de voz.
  function abrirMostrarError(mensajeId?: number) {
    const anteriores = mensajeId === undefined ? mensajes : mensajes.filter(x => x.id < mensajeId)
    const descripcion = [...anteriores].reverse().find(x => x.rol === 'usuario')?.contenido ?? ''
    setMostrarError(descripcion)
  }

  async function salir() {
    await cerrarSesion()
    navigate('/login', { replace: true })
  }

  const nombre = primerNombre(usuario.nombreCompleto)

  return <div className="inicio-shell asistente-shell">
    <aside className="inicio-sidebar" aria-label="Navegación principal">
      <div className="inicio-marca"><span className="inicio-marca__nombre">CALIMOD</span><span className="inicio-marca__sistema">Sistema inteligente<br/>de incidencias TI</span></div>
      <nav className="inicio-menu">
        <button className="inicio-menu__item" type="button" onClick={() => navigate('/inicio')}><Icono nombre="inicio"/><span>Inicio</span></button>
        <button className="inicio-menu__item inicio-menu__item--activo" type="button" aria-current="page"><Icono nombre="asistente"/><span>Asistente TI</span></button>
        <button className="inicio-menu__item" type="button" onClick={() => navigate('/nuevo-ticket')}><Icono nombre="nuevo"/><span>Nuevo Ticket</span></button>
        <button className="inicio-menu__item" type="button" onClick={() => navigate('/mis-tickets')}><Icono nombre="tickets"/><span>Mis Tickets</span></button>
      </nav>
      <div className="inicio-sidebar__mensaje"><span>La tecnología también impulsa grandes historias.</span><strong>CALIMOD</strong></div>
      <div className="inicio-sidebar__pie"><span className="inicio-sidebar__ayuda-icono">?</span><span>Orientación segura</span><small>Sin compartir contraseñas ni códigos</small></div>
    </aside>

    <section className="inicio-principal">
      <header className="inicio-topbar asistente-topbar">
        <div className="asistente-topbar__titulo"><span><Icono nombre="asistente" size={19}/></span><div><strong>Asistente TI</strong><small>Orientación con conocimiento corporativo</small></div></div>
        <div className="inicio-topbar__usuario"><NotificacionesCampana/><div className="inicio-avatar">{nombre[0]?.toUpperCase()}</div><div className="inicio-identidad"><strong>{nombre}</strong><span>Colaborador</span></div><button className="inicio-salir" type="button" onClick={() => void salir()} title="Cerrar sesión" aria-label="Cerrar sesión"><Icono nombre="salir" size={18}/></button></div>
      </header>

      <main className="asistente-contenido">
        <header className="asistente-cabecera">
          <div><span className="asistente-cabecera__etiqueta"><Icono nombre="escudo" size={15}/> Asistencia segura</span><h1>¿En qué puedo ayudarte, {nombre}?</h1><p>Consulta soluciones de TI y el estado de tus solicitudes en una sola conversación.</p></div>
          <div className="asistente-cabecera__acciones"><button className="asistente-limpiar asistente-mostrar" type="button" title="Muestra el error compartiendo tu pantalla" onClick={() => abrirMostrarError()}><Icono nombre="pantalla" size={17}/> Mostrar un error</button>
          <button className="asistente-limpiar" type="button" title="Iniciar nueva conversación" onClick={() => { setMensajes([bienvenida]); setError(''); setConsulta('') }} disabled={mensajes.length === 1}><Icono nombre="limpiar" size={17}/> Nueva conversación</button></div>
        </header>

        <div className="asistente-layout">
          <section className="asistente-chat" aria-label="Conversación con el Asistente TI">
            <div className="asistente-mensajes" aria-live="polite">
              {mensajes.map(mensaje => <article className={`asistente-mensaje asistente-mensaje--${mensaje.rol}`} key={mensaje.id}>
                {mensaje.rol === 'asistente' && <div className="asistente-mensaje__avatar"><Icono nombre="asistente" size={18}/></div>}
                <div className="asistente-mensaje__cuerpo">
                  <div className="asistente-mensaje__meta"><strong>{mensaje.rol === 'asistente' ? 'Asistente TI' : 'Tú'}</strong>{mensaje.rol === 'asistente' && mensaje.modo && <span className={`asistente-modo asistente-modo--${mensaje.modo.toLowerCase()}`}>{mensaje.modo === 'IA' ? 'IA conectada' : 'Base corporativa'}</span>}</div>
                  <p>{mensaje.contenido}</p>
                  {!!mensaje.fuentes?.length && <div className="asistente-fuentes"><strong>Fuentes consultadas</strong><div>{mensaje.fuentes.map(fuente => <button type="button" key={`${fuente.tipo}-${fuente.codigo}`} onClick={() => fuente.tipo === 'TICKET' && navigate(`/mis-tickets?ticket=${encodeURIComponent(fuente.codigo)}`)} disabled={fuente.tipo !== 'TICKET'} title={fuente.resumen}><span className={`asistente-fuente__tipo asistente-fuente__tipo--${fuente.tipo.toLowerCase()}`}>{fuente.tipo === 'TICKET' ? 'Ticket' : 'Guía'}</span><span><b>{fuente.codigo}</b>{fuente.titulo}</span></button>)}</div></div>}
                  {mensaje.escalarATicket && <div className="asistente-escalar__opciones"><button className="asistente-escalar" type="button" onClick={() => crearTicket(mensaje.accion)}><Icono nombre="nuevo" size={17}/> Preparar ticket con mi caso <Icono nombre="flecha" size={15}/></button><button className="asistente-escalar asistente-escalar--pantalla" type="button" onClick={() => abrirMostrarError(mensaje.id)}><Icono nombre="pantalla" size={17}/> Mostrar el error en pantalla</button></div>}
                  {!!mensaje.sugerencias?.length && <div className="asistente-sugerencias">{mensaje.sugerencias.map(sugerencia => <button type="button" key={sugerencia} onClick={() => usarSugerencia(sugerencia)}>{sugerencia}</button>)}</div>}
                </div>
              </article>)}
              {enviando && <div className="asistente-pensando" role="status"><span><i/><i/><i/></span>Consultando información autorizada...</div>}
              <div ref={finalRef}/>
            </div>

            {error && <div className="asistente-error" role="alert"><Icono nombre="alerta" size={18}/><span>{error}</span><button type="button" onClick={() => setError('')} aria-label="Cerrar mensaje">×</button></div>}
            <form className="asistente-editor" onSubmit={manejarEnvio}>
              <textarea ref={editorRef} value={consulta} onChange={event => setConsulta(event.target.value)} onKeyDown={manejarTecla} maxLength={1000} rows={2} placeholder="Describe el problema o consulta el estado de un ticket..." aria-label="Escribe tu consulta" disabled={enviando}/>
              <div className="asistente-editor__pie"><span><b>{consulta.length}</b>/1000 · Enter para enviar · Shift + Enter para nueva línea</span><button type="submit" disabled={consulta.trim().length < 3 || enviando} title="Enviar consulta" aria-label="Enviar consulta"><Icono nombre="enviar" size={19}/></button></div>
            </form>
            <p className="asistente-privacidad"><Icono nombre="escudo" size={14}/> No compartas contraseñas, códigos de verificación ni información confidencial.</p>
          </section>

          <aside className="asistente-contexto" aria-label="Contexto y accesos rápidos">
            <section className="asistente-contexto__seccion"><header><span><Icono nombre="mensaje" size={18}/></span><div><h2>Consultas rápidas</h2><p>Empieza con una pregunta frecuente</p></div></header><div className="asistente-rapidas">{consultasRapidas.map(item => <button type="button" key={item} onClick={() => void enviar(item)} disabled={enviando}><span>{item}</span><Icono nombre="flecha" size={15}/></button>)}</div></section>
            <section className="asistente-contexto__seccion asistente-contexto__tickets"><header><span><Icono nombre="tickets" size={18}/></span><div><h2>Tickets activos</h2><p>Contexto disponible para tu consulta</p></div><button type="button" onClick={() => navigate('/mis-tickets')} title="Ver todos" aria-label="Ver todos los tickets"><Icono nombre="flecha" size={16}/></button></header>{estadoTickets === 'cargando' ? <div className="asistente-contexto__vacio">Consultando tus tickets...</div> : estadoTickets === 'error' ? <div className="asistente-contexto__vacio asistente-contexto__vacio--error"><span>No fue posible cargar tus tickets.</span><button type="button" onClick={() => void cargarTickets()}>Reintentar</button></div> : tickets.length === 0 ? <div className="asistente-contexto__vacio">No tienes tickets activos.</div> : <div className="asistente-ticket-lista">{tickets.map(ticket => <button type="button" key={ticket.incidenciaNumero} onClick={() => navigate(`/mis-tickets?ticket=${encodeURIComponent(ticket.incidenciaNumero)}`)}><span><b>{ticket.incidenciaNumero}</b><small>{ticket.estadoDescripcion}</small></span><strong>{ticket.titulo}</strong><em><Icono nombre="reloj" size={13}/>{fechaRelativa(ticket.ultimaFechaModif)}</em></button>)}</div>}</section>
            <section className="asistente-confianza"><Icono nombre="libro" size={20}/><div><strong>Respuestas con respaldo</strong><p>El asistente usa únicamente guías publicadas por TI y tus propios tickets como contexto empresarial.</p></div></section>
          </aside>
        </div>
      </main>
    </section>
    {mostrarError !== null && <MostrarErrorLive descripcion={mostrarError} onCerrar={() => setMostrarError(null)} onBorrador={(accion, extra) => { setMostrarError(null); crearTicket(accion, extra) }}/>}
  </div>
}
