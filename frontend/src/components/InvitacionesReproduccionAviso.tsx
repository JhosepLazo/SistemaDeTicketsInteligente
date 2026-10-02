/**
 * Archivo: InvitacionesReproduccionAviso.tsx
 * Objetivo: Avisar al colaborador en su Inicio que TI le pidió mostrar un error en una sesión guiada.
 * Consideraciones: No muestra nada si no hay solicitudes vigentes o si la consulta falla; la campana sigue siendo el canal principal.
 */

import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { listarReproducciones, type ReproduccionInvitacion } from '../features/reproduccion/services/reproduccionUsuarioService'
import '../pages/ReproduccionUsuarioPage.css'

export default function InvitacionesReproduccionAviso() {
  const navigate = useNavigate()
  const [invitaciones, setInvitaciones] = useState<ReproduccionInvitacion[]>([])

  useEffect(() => {
    let activo = true
    listarReproducciones().then(x => { if (activo) setInvitaciones(x) }).catch(() => undefined)
    return () => { activo = false }
  }, [])

  const primera = invitaciones[0]
  if (!primera) return null

  return <section className="repro-invitacion" role="status">
    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><rect x="3" y="4" width="18" height="13" rx="2"/><path d="M8 21h8M12 17v4"/></svg>
    <div>
      <strong>TI te pide mostrar cómo ocurre un error{invitaciones.length > 1 ? ` (${invitaciones.length} solicitudes)` : ''}</strong>
      <span>{primera.incidenciaNumero} · {primera.tituloTicket} — solicitado por {primera.operadorTI}</span>
    </div>
    <button className="repro-btn repro-btn--primario" type="button" onClick={() => navigate(invitaciones.length > 1 ? '/reproducir' : `/reproducir?sesion=${primera.sesionNumero}`)}>Ver solicitud</button>
  </section>
}
