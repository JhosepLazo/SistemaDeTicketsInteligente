/**
 * Archivo: NotificacionesCampana.tsx
 * Objetivo: Mostrar notificaciones operativas persistidas para el usuario autenticado.
 * Responsabilidad: Consultar avisos, mostrar pendientes y navegar al módulo relacionado al marcar una notificación como leída.
 * Dependencias: React, React Router y notificacionesService.
 * Flujo: Topbar -> NotificacionesCampana -> notificacionesService -> API -> navegación interna.
 * Consideraciones: No reemplaza indicadores propios de cada módulo; concentra únicamente eventos que requieren atención o seguimiento.
 */

import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { marcarNotificacionLeida, obtenerNotificaciones, type NotificacionesRespuesta } from '../features/notificaciones/services/notificacionesService'
import './NotificacionesCampana.css'

export default function NotificacionesCampana() {
  const navigate = useNavigate()
  const contenedorRef = useRef<HTMLDivElement>(null)
  const [abierta, setAbierta] = useState(false)
  const [datos, setDatos] = useState<NotificacionesRespuesta>({ noLeidas: 0, notificaciones: [] })

  async function cargar() {
    try { setDatos(await obtenerNotificaciones()) } catch { /* La campana no bloquea el resto del módulo. */ }
  }

  useEffect(() => { void cargar() }, [])
  useEffect(() => {
    const cerrar = (e: MouseEvent) => { if (!contenedorRef.current?.contains(e.target as Node)) setAbierta(false) }
    document.addEventListener('mousedown', cerrar)
    return () => document.removeEventListener('mousedown', cerrar)
  }, [])

  async function abrirNotificacion(id: number, ruta: string, incidenciaNumero: string) {
    try { await marcarNotificacionLeida(id); await cargar() } finally {
      setAbierta(false)
      if (ruta) {
        const separador = ruta.includes('?') ? '&' : '?'
        const destino = incidenciaNumero ? `${ruta}${separador}ticket=${encodeURIComponent(incidenciaNumero)}` : ruta
        navigate(destino)
      }
    }
  }

  return <div className="notificaciones" ref={contenedorRef}>
    <button className="notificaciones__boton" type="button" onClick={() => { setAbierta(v => !v); void cargar() }} aria-label="Notificaciones">
      <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9"/><path d="M10 21h4"/></svg>
      {datos.noLeidas > 0 && <span>{datos.noLeidas > 9 ? '9+' : datos.noLeidas}</span>}
    </button>
    {abierta && <section className="notificaciones__panel">
      <header><strong>Notificaciones</strong><small>{datos.noLeidas} pendientes</small></header>
      <div className="notificaciones__lista">
        {datos.notificaciones.length === 0 ? <p className="notificaciones__vacio">No tienes notificaciones recientes.</p> : datos.notificaciones.map(item =>
          <button key={item.notificacionNumero} className={item.leida ? '' : 'no-leida'} onClick={() => void abrirNotificacion(item.notificacionNumero, item.ruta, item.incidenciaNumero)}>
            <strong>{item.titulo}</strong><span>{item.mensaje}</span><small>{new Date(item.fecha).toLocaleString('es-PE')}</small>
          </button>)}
      </div>
    </section>}
  </div>
}
