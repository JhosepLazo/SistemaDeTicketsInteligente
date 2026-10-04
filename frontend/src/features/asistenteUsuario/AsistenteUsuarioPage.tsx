/**
 * Asistente TI para colaboradores: orientación basada en conocimiento publicado y tickets propios.
 * La conversación vive únicamente en memoria durante la sesión de esta pantalla.
 */

import { useCallback, useEffect, useRef, useState, type FormEvent, type KeyboardEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import MostrarErrorLive, { type EvidenciaParaTicket } from './MostrarErrorLive'
import { primerNombre, useAutenticacion } from '../autenticacion/AutenticacionContext'
import {
  consultarAsistente,
  type AsistenteFuente,
  type AsistenteMensajeHistorial,
  type AsistenteUsuarioAccion,
} from '../../services/asistenteUsuarioApi'
import { obtenerMisTickets, type MisTicketItem } from '../../services/misTicketsApi'
import MarcoPortal from '../../components/MarcoPortal'
import Icono from '../../components/Icono'
import './AsistenteUsuarioPage.css'

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
  contenido:
    'Hola. Puedo ayudarte a encontrar soluciones publicadas por TI, revisar el estado de tus tickets y orientarte antes de registrar una incidencia. Cuéntame qué necesitas resolver.',
  modo: 'CONOCIMIENTO',
}

const consultasRapidas = ['No puedo ingresar a un sistema', 'Mi equipo está lento', 'Consultar el estado de mis tickets']

const estadosCerrados = new Set(['RS', 'CA', 'CF', 'NP'])

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
  const { usuario } = useAutenticacion()
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

  useEffect(() => {
    void cargarTickets()
  }, [cargarTickets])

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
      setMensajes(actual => [
        ...actual,
        {
          id: secuenciaRef.current++,
          rol: 'asistente',
          contenido: respuesta.respuesta,
          modo: respuesta.modo,
          fuentes: respuesta.fuentes,
          escalarATicket: respuesta.escalarATicket,
          sugerencias: respuesta.sugerencias,
          accion: respuesta.accion,
        },
      ])
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
    navigate(
      '/nuevo-ticket',
      accion
        ? {
            state: {
              asistenteBorrador: {
                titulo: accion.titulo,
                detalle: accion.detalle,
                mensajeError: accion.mensajeError ?? '',
                grabaciones: extra?.grabaciones ?? [],
                evidencia: extra?.evidencia,
              },
            },
          }
        : undefined,
    )
  }

  // El problema que el colaborador describió en el chat orienta al asistente de voz.
  function abrirMostrarError(mensajeId?: number) {
    const anteriores = mensajeId === undefined ? mensajes : mensajes.filter(x => x.id < mensajeId)
    const descripcion = [...anteriores].reverse().find(x => x.rol === 'usuario')?.contenido ?? ''
    setMostrarError(descripcion)
  }

  const nombre = primerNombre(usuario.nombreCompleto)

  return (
    <MarcoPortal
      menu="usuario"
      activa="asistente"
      clase="asistente-shell"
      claseBarra="asistente-topbar"
      ayuda={{ titulo: 'Orientación segura', detalle: 'Sin compartir contraseñas ni códigos' }}
      barra={
        <div className="asistente-topbar__titulo">
          <span>
            <Icono nombre="asistenteDestellos" size={19} />
          </span>
          <div>
            <strong>Asistente TI</strong>
            <small>Orientación con conocimiento corporativo</small>
          </div>
        </div>
      }
      capas={
        <>
          {mostrarError !== null && (
            <MostrarErrorLive
              descripcion={mostrarError}
              onCerrar={() => setMostrarError(null)}
              onBorrador={(accion, extra) => {
                setMostrarError(null)
                crearTicket(accion, extra)
              }}
            />
          )}
        </>
      }
    >
      <main className="asistente-contenido">
        <header className="asistente-cabecera">
          <div>
            <span className="asistente-cabecera__etiqueta">
              <Icono nombre="escudo" size={15} /> Asistencia segura
            </span>
            <h1>¿En qué puedo ayudarte, {nombre}?</h1>
            <p>Consulta soluciones de TI y el estado de tus solicitudes en una sola conversación.</p>
          </div>
          <div className="asistente-cabecera__acciones">
            <button
              className="asistente-limpiar asistente-mostrar"
              type="button"
              title="Muestra el error compartiendo tu pantalla"
              onClick={() => abrirMostrarError()}
            >
              <Icono nombre="pantalla" size={17} /> Mostrar un error
            </button>
            <button
              className="asistente-limpiar"
              type="button"
              title="Iniciar nueva conversación"
              onClick={() => {
                setMensajes([bienvenida])
                setError('')
                setConsulta('')
              }}
              disabled={mensajes.length === 1}
            >
              <Icono nombre="limpiar" size={17} /> Nueva conversación
            </button>
          </div>
        </header>

        <div className="asistente-layout">
          <section className="asistente-chat" aria-label="Conversación con el Asistente TI">
            <div className="asistente-mensajes" aria-live="polite">
              {mensajes.map(mensaje => (
                <article className={`asistente-mensaje asistente-mensaje--${mensaje.rol}`} key={mensaje.id}>
                  {mensaje.rol === 'asistente' && (
                    <div className="asistente-mensaje__avatar">
                      <Icono nombre="asistenteDestellos" size={18} />
                    </div>
                  )}
                  <div className="asistente-mensaje__cuerpo">
                    <div className="asistente-mensaje__meta">
                      <strong>{mensaje.rol === 'asistente' ? 'Asistente TI' : 'Tú'}</strong>
                      {mensaje.rol === 'asistente' && mensaje.modo && (
                        <span className={`asistente-modo asistente-modo--${mensaje.modo.toLowerCase()}`}>
                          {mensaje.modo === 'IA' ? 'IA conectada' : 'Base corporativa'}
                        </span>
                      )}
                    </div>
                    <p>{mensaje.contenido}</p>
                    {!!mensaje.fuentes?.length && (
                      <div className="asistente-fuentes">
                        <strong>Fuentes consultadas</strong>
                        <div>
                          {mensaje.fuentes.map(fuente => (
                            <button
                              type="button"
                              key={`${fuente.tipo}-${fuente.codigo}`}
                              onClick={() =>
                                fuente.tipo === 'TICKET' && navigate(`/mis-tickets?ticket=${encodeURIComponent(fuente.codigo)}`)
                              }
                              disabled={fuente.tipo !== 'TICKET'}
                              title={fuente.resumen}
                            >
                              <span className={`asistente-fuente__tipo asistente-fuente__tipo--${fuente.tipo.toLowerCase()}`}>
                                {fuente.tipo === 'TICKET' ? 'Ticket' : 'Guía'}
                              </span>
                              <span>
                                <b>{fuente.codigo}</b>
                                {fuente.titulo}
                              </span>
                            </button>
                          ))}
                        </div>
                      </div>
                    )}
                    {mensaje.escalarATicket && (
                      <div className="asistente-escalar__opciones">
                        <button className="asistente-escalar" type="button" onClick={() => crearTicket(mensaje.accion)}>
                          <Icono nombre="nuevo" size={17} /> Preparar ticket con mi caso <Icono nombre="flecha" size={15} />
                        </button>
                        <button
                          className="asistente-escalar asistente-escalar--pantalla"
                          type="button"
                          onClick={() => abrirMostrarError(mensaje.id)}
                        >
                          <Icono nombre="pantalla" size={17} /> Mostrar el error en pantalla
                        </button>
                      </div>
                    )}
                    {!!mensaje.sugerencias?.length && (
                      <div className="asistente-sugerencias">
                        {mensaje.sugerencias.map(sugerencia => (
                          <button type="button" key={sugerencia} onClick={() => usarSugerencia(sugerencia)}>
                            {sugerencia}
                          </button>
                        ))}
                      </div>
                    )}
                  </div>
                </article>
              ))}
              {enviando && (
                <div className="asistente-pensando" role="status">
                  <span>
                    <i />
                    <i />
                    <i />
                  </span>
                  Consultando información autorizada...
                </div>
              )}
              <div ref={finalRef} />
            </div>

            {error && (
              <div className="asistente-error" role="alert">
                <Icono nombre="alerta" size={18} />
                <span>{error}</span>
                <button type="button" onClick={() => setError('')} aria-label="Cerrar mensaje">
                  ×
                </button>
              </div>
            )}
            <form className="asistente-editor" onSubmit={manejarEnvio}>
              <textarea
                ref={editorRef}
                value={consulta}
                onChange={event => setConsulta(event.target.value)}
                onKeyDown={manejarTecla}
                maxLength={1000}
                rows={2}
                placeholder="Describe el problema o consulta el estado de un ticket..."
                aria-label="Escribe tu consulta"
                disabled={enviando}
              />
              <div className="asistente-editor__pie">
                <span>
                  <b>{consulta.length}</b>/1000 · Enter para enviar · Shift + Enter para nueva línea
                </span>
                <button
                  type="submit"
                  disabled={consulta.trim().length < 3 || enviando}
                  title="Enviar consulta"
                  aria-label="Enviar consulta"
                >
                  <Icono nombre="enviar" size={19} />
                </button>
              </div>
            </form>
            <p className="asistente-privacidad">
              <Icono nombre="escudo" size={14} /> No compartas contraseñas, códigos de verificación ni información confidencial.
            </p>
          </section>

          <aside className="asistente-contexto" aria-label="Contexto y accesos rápidos">
            <section className="asistente-contexto__seccion">
              <header>
                <span>
                  <Icono nombre="mensaje" size={18} />
                </span>
                <div>
                  <h2>Consultas rápidas</h2>
                  <p>Empieza con una pregunta frecuente</p>
                </div>
              </header>
              <div className="asistente-rapidas">
                {consultasRapidas.map(item => (
                  <button type="button" key={item} onClick={() => void enviar(item)} disabled={enviando}>
                    <span>{item}</span>
                    <Icono nombre="flecha" size={15} />
                  </button>
                ))}
              </div>
            </section>
            <section className="asistente-contexto__seccion asistente-contexto__tickets">
              <header>
                <span>
                  <Icono nombre="tickets" size={18} />
                </span>
                <div>
                  <h2>Tickets activos</h2>
                  <p>Contexto disponible para tu consulta</p>
                </div>
                <button type="button" onClick={() => navigate('/mis-tickets')} title="Ver todos" aria-label="Ver todos los tickets">
                  <Icono nombre="flecha" size={16} />
                </button>
              </header>
              {estadoTickets === 'cargando' ? (
                <div className="asistente-contexto__vacio">Consultando tus tickets...</div>
              ) : estadoTickets === 'error' ? (
                <div className="asistente-contexto__vacio asistente-contexto__vacio--error">
                  <span>No fue posible cargar tus tickets.</span>
                  <button type="button" onClick={() => void cargarTickets()}>
                    Reintentar
                  </button>
                </div>
              ) : tickets.length === 0 ? (
                <div className="asistente-contexto__vacio">No tienes tickets activos.</div>
              ) : (
                <div className="asistente-ticket-lista">
                  {tickets.map(ticket => (
                    <button
                      type="button"
                      key={ticket.incidenciaNumero}
                      onClick={() => navigate(`/mis-tickets?ticket=${encodeURIComponent(ticket.incidenciaNumero)}`)}
                    >
                      <span>
                        <b>{ticket.incidenciaNumero}</b>
                        <small>{ticket.estadoDescripcion}</small>
                      </span>
                      <strong>{ticket.titulo}</strong>
                      <em>
                        <Icono nombre="reloj" size={13} />
                        {fechaRelativa(ticket.ultimaFechaModif)}
                      </em>
                    </button>
                  ))}
                </div>
              )}
            </section>
            <section className="asistente-confianza">
              <Icono nombre="libroAbierto" size={20} />
              <div>
                <strong>Respuestas con respaldo</strong>
                <p>El asistente usa únicamente guías publicadas por TI y tus propios tickets como contexto empresarial.</p>
              </div>
            </section>
          </aside>
        </div>
      </main>
    </MarcoPortal>
  )
}
