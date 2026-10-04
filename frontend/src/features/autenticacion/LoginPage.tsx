/**
 * Archivo: LoginPage.tsx
 * Objetivo: Implementar la pantalla de acceso al Sistema de Tickets Inteligente.
 * Responsabilidad: Capturar credenciales, solicitar el inicio de sesión y presentar estados de carga, ayuda y errores al usuario.
 * Dependencias: AutenticacionContext, React, LoginPage.css e identidad visual de CALIMOD.
 * Flujo: Usuario -> LoginPage -> AutenticacionContext -> autenticacionApi -> API /api/autenticacion.
 * Consideraciones: No valida contraseñas localmente ni conserva credenciales; la autenticación efectiva pertenece al backend.
 */

import { useEffect, useState } from 'react'
import type { FormEvent, KeyboardEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAutenticacion } from './AutenticacionContext'
import './LoginPage.css'

const claveUsuarioRecordado = 'sti.usuarioRecordado'

function IconoGestion() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <rect x="3.5" y="4.5" width="17" height="12.5" rx="2" />
      <path d="M9 20h6M12 17v3" />
    </svg>
  )
}

function IconoAsistencia() {
  return (
    <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
      <path d="M12 2l1.9 6.1L20 10l-6.1 1.9L12 18l-1.9-6.1L4 10l6.1-1.9L12 2z" />
    </svg>
  )
}

function IconoSeguimiento() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <path d="M5 20V11M10 20V6M15 20v-8M20 20V3" />
    </svg>
  )
}

function IconoUsuario() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <circle cx="12" cy="8" r="3.6" />
      <path d="M5 20c.8-3.6 3.2-5.5 7-5.5s6.2 1.9 7 5.5" />
    </svg>
  )
}

function IconoUsuarios() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <circle cx="9" cy="8" r="3" />
      <circle cx="17" cy="9" r="2.4" />
      <path d="M3.5 20c.6-3.4 2.8-5.2 6.2-5.2s5.6 1.8 6.2 5.2" />
      <path d="M15.8 14.5c2.8.2 4.3 1.7 4.7 4.5" />
    </svg>
  )
}

function IconoCandado() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <rect x="5" y="10" width="14" height="11" rx="2" />
      <path d="M8 10V7a4 4 0 1 1 8 0v3" />
    </svg>
  )
}

function IconoVer() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <path d="M2 12s3.5-6 10-6 10 6 10 6-3.5 6-10 6-10-6-10-6z" />
      <circle cx="12" cy="12" r="2.7" />
    </svg>
  )
}

function IconoOcultar() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <path d="m3 3 18 18" />
      <path d="M10.5 10.7a2.6 2.6 0 0 0 3.7 3.7" />
      <path d="M9.8 5.2A10.4 10.4 0 0 1 12 5c6.5 0 10 7 10 7a18.2 18.2 0 0 1-3.1 4.1" />
      <path d="M6.2 6.2A18.5 18.5 0 0 0 2 12s3.5 7 10 7a10.8 10.8 0 0 0 4-.8" />
    </svg>
  )
}

function IconoEscudo() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <path d="M12 3l7 3v5c0 5-3.4 8.8-7 10-3.6-1.2-7-5-7-10V6l7-3z" />
      <path d="m9.4 12.4 1.8 1.8 3.7-4.2" />
    </svg>
  )
}

function IconoSoporte() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <path d="M4 13a8 8 0 0 1 16 0" />
      <path d="M4 13v3a2 2 0 0 0 2 2h1v-6H6a2 2 0 0 0-2 2z" />
      <path d="M20 13v3a2 2 0 0 1-2 2h-1v-6h1a2 2 0 0 1 2 2z" />
      <path d="M12 20h3" />
    </svg>
  )
}

function IconoRecuperar() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <circle cx="12" cy="12" r="7" />
      <path d="M12 8v8M8 12h8" />
    </svg>
  )
}

function IconoTeclado() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" aria-hidden="true">
      <rect x="2.5" y="6" width="19" height="12" rx="2" />
      <path d="M6 10h1M10 10h1M14 10h1M18 10h1M6 14h1M10 14h7" />
    </svg>
  )
}

function IconoSol() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" aria-hidden="true">
      <circle cx="12" cy="12" r="3.5" />
      <path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4" />
    </svg>
  )
}

function MarcaCalimod({ className = '', size = 55 }) {
  return (
    <svg
      className={className}
      width={size}
      viewBox="0 5 90 65"
      xmlns="http://www.w3.org/2000/svg"
      role="img"
      aria-label="Calimod"
      focusable="false"
      fill="currentColor"
    >
      <path d="M2 58L31 7l14 24-8 14-6-11-14 24H2Z" />
      <path d="M35 58L63 11l28 47H75L63 37 51 58H35Z" />
    </svg>
  )
}

export default function LoginPage() {
  const ambiente = import.meta.env.DEV ? 'Desarrollo' : 'Producción'
  const navigate = useNavigate()
  const { iniciarSesion, mensajeSesion } = useAutenticacion()

  const [usuario, setUsuario] = useState('')
  const [contrasena, setContrasena] = useState('')
  const [mensaje, setMensaje] = useState('')
  const [tipoMensaje, setTipoMensaje] = useState<'ok' | 'error' | 'info'>('info')
  const [cargando, setCargando] = useState(false)
  const [mostrarContrasena, setMostrarContrasena] = useState(false)
  const [recordarme, setRecordarme] = useState(false)
  const [capsLockActivo, setCapsLockActivo] = useState(false)

  useEffect(() => {
    const usuarioGuardado = localStorage.getItem(claveUsuarioRecordado)
    if (!usuarioGuardado) return

    setUsuario(usuarioGuardado)
    setRecordarme(true)
  }, [])

  useEffect(() => {
    if (!mensajeSesion) return
    setTipoMensaje('error')
    setMensaje(mensajeSesion)
  }, [mensajeSesion])

  function manejarCapsLock(evento: KeyboardEvent<HTMLInputElement>) {
    setCapsLockActivo(evento.getModifierState('CapsLock'))
  }

  function mostrarAyuda() {
    setTipoMensaje('info')
    setMensaje('Si olvidaste tu acceso, comunícate con el área TI para solicitar el restablecimiento de tu cuenta.')
  }

  async function manejarEnvio(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()

    setMensaje('')
    setTipoMensaje('info')
    setCargando(true)

    try {
      await iniciarSesion({
        usuario: usuario.trim(),
        contrasena,
      })

      if (recordarme) localStorage.setItem(claveUsuarioRecordado, usuario.trim())
      else localStorage.removeItem(claveUsuarioRecordado)

      setContrasena('')
      setCapsLockActivo(false)
      navigate('/inicio', { replace: true })
    } catch (error) {
      setTipoMensaje('error')
      setMensaje(error instanceof Error ? error.message : 'No fue posible iniciar sesión.')
    } finally {
      setCargando(false)
    }
  }

  return (
    <main className="login-page">
      <section className="login-shell">
        <div className="login-ambient login-ambient-one" aria-hidden="true" />
        <div className="login-ambient login-ambient-two" aria-hidden="true" />

        <section className="login-hero" aria-label="Calimod Store">
          <div className="login-hero-overlay" />

          <header className="login-brand">
            <img className="login-brand-logo" src="/images/logo-calimod.png" alt="Calimod" />

            <div className="login-brand-text">
              <p className="login-brand-name">Calimod Store</p>
              <p className="login-brand-subtitle">Sistema Inteligente de Tickets</p>
            </div>
          </header>

          <p className="login-hero-slogan">Personas que mueven tu marca</p>

          <div className="login-hero-quote" aria-hidden="true">
            <div>
              <span />
              <p>
                Más
                <br />
                que calzado,
                <br />
                personas
                <br />
                que avanzan
              </p>
            </div>

            <div>
              <p>
                Estilo
                <br />
                que te acompaña
                <br />
                siempre
              </p>
              <span />
            </div>
          </div>

          <div className="login-hero-content">
            <h1>Bienvenido</h1>

            <p className="login-hero-description">Accede de forma segura al portal inteligente de soporte de Calimod Store.</p>

            <p className="login-hero-text">
              Un entorno confiable, eficiente y colaborativo para gestionar tus tickets, solicitudes y operaciones.
            </p>

            <div className="login-feature-list">
              <article className="login-feature-item">
                <div className="login-feature-icon">
                  <IconoGestion />
                </div>

                <div>
                  <h2>Gestión centralizada</h2>
                  <p>Todo tu soporte en un solo lugar.</p>
                </div>
              </article>

              <article className="login-feature-item">
                <div className="login-feature-icon">
                  <IconoAsistencia />
                </div>

                <div>
                  <h2>Asistencia inteligente</h2>
                  <p>Respuestas más rápidas para tus tiendas, equipos y operaciones.</p>
                </div>
              </article>

              <article className="login-feature-item">
                <div className="login-feature-icon">
                  <IconoSeguimiento />
                </div>

                <div>
                  <h2>Seguimiento en tiempo real</h2>
                  <p>Mantente informado en cada etapa del proceso.</p>
                </div>
              </article>
            </div>
          </div>

          <footer className="login-hero-footer">
            <div className="login-support">
              <div className="login-support-icon">
                <IconoSoporte />
              </div>

              <div>
                <strong>¿Necesitas ayuda?</strong>

                <button type="button" className="login-link-button login-link-light" onClick={mostrarAyuda}>
                  Centro de soporte →
                </button>
              </div>
            </div>

            <p className="login-footer-legend">Confianza · Personas · Siempre Contigo</p>
          </footer>
        </section>

        <section className="login-panel">
          <div className="login-panel-halo" aria-hidden="true" />
          <div className="login-panel-orbit login-panel-orbit-one" aria-hidden="true" />
          <div className="login-panel-orbit login-panel-orbit-two" aria-hidden="true" />

          <div className="login-panel-topbar">
            <span className="login-theme-icon" aria-hidden="true">
              <IconoSol />
            </span>

            <span className="login-topbar-divider" />

            <span className="login-language">
              Español
              <span>⌄</span>
            </span>
          </div>

          <section className="login-card" aria-labelledby="login-title">
            <div className="login-card-light" aria-hidden="true" />

            <div className="login-card-emblem" aria-hidden="true">
              <div className="login-card-emblem-orbit">
                <div className="login-card-emblem-inner">
                  <MarcaCalimod />
                </div>
              </div>
            </div>

            <div className="login-card-heading">
              <span className="login-card-eyebrow">Acceso seguro</span>
              <h2 id="login-title">Iniciar sesión</h2>

              <p className="login-card-subtitle">Accede con tu usuario y contraseña</p>
            </div>

            <div className="login-card-notice">
              <div className="login-card-notice-icon">
                <IconoUsuarios />
              </div>

              <div>
                <strong>Solo personal autorizado</strong>

                <p>Este sistema es exclusivo para colaboradores de Calimod Store (tiendas, oficina, operaciones y socios autorizados).</p>
              </div>
            </div>

            <form className="login-form" onSubmit={manejarEnvio}>
              <div className="login-form-group">
                <label htmlFor="usuario">Usuario</label>

                <div className="login-input-wrapper">
                  <span className="login-input-icon">
                    <IconoUsuario />
                  </span>

                  <input
                    id="usuario"
                    name="usuario"
                    value={usuario}
                    onChange={evento => setUsuario(evento.target.value)}
                    autoComplete="username"
                    maxLength={20}
                    placeholder="Ingresa tu usuario"
                    required
                  />
                </div>
              </div>

              <div className="login-form-group">
                <label htmlFor="contrasena">Contraseña</label>

                <div className="login-input-wrapper">
                  <span className="login-input-icon">
                    <IconoCandado />
                  </span>

                  <input
                    id="contrasena"
                    name="contrasena"
                    type={mostrarContrasena ? 'text' : 'password'}
                    value={contrasena}
                    onChange={evento => setContrasena(evento.target.value)}
                    onKeyDown={manejarCapsLock}
                    onKeyUp={manejarCapsLock}
                    autoComplete="current-password"
                    placeholder="Ingresa tu contraseña"
                    required
                  />

                  <button
                    type="button"
                    className="login-password-toggle"
                    onClick={() => setMostrarContrasena(!mostrarContrasena)}
                    aria-label={mostrarContrasena ? 'Ocultar contraseña' : 'Mostrar contraseña'}
                  >
                    {mostrarContrasena ? <IconoOcultar /> : <IconoVer />}
                  </button>
                </div>
              </div>

              {capsLockActivo && (
                <p className="login-capslock" role="status">
                  Bloq Mayús está activado.
                </p>
              )}

              <div className="login-form-options">
                <label className="login-checkbox">
                  <input type="checkbox" checked={recordarme} onChange={evento => setRecordarme(evento.target.checked)} />

                  <span className="login-checkbox-control" aria-hidden="true">
                    <svg viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="2.2">
                      <path d="m3 8 3 3 7-7" />
                    </svg>
                  </span>

                  <span>Recordar usuario</span>
                </label>

                <button type="button" className="login-link-button" onClick={mostrarAyuda}>
                  ¿Olvidaste tu contraseña?
                </button>
              </div>

              <button type="submit" className="login-submit-button" disabled={cargando}>
                <span>{cargando ? 'Ingresando...' : 'Ingresar'}</span>

                {!cargando && (
                  <svg
                    className="login-submit-arrow"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    aria-hidden="true"
                  >
                    <path d="M5 12h14" />
                    <path d="m13 6 6 6-6 6" />
                  </svg>
                )}
              </button>

              <div className="login-divider" aria-hidden="true">
                <span />
                <small>o</small>
                <span />
              </div>

              <button type="button" className="login-secondary-button" onClick={mostrarAyuda}>
                <IconoRecuperar />

                <span>
                  <strong>Recuperar acceso</strong>
                  <small>Solicita ayuda para restablecer tu cuenta</small>
                </span>
              </button>

              <div className="login-security-note">
                <span className="login-security-icon">
                  <IconoEscudo />
                </span>

                <p>Tu acceso está protegido mediante una sesión segura y tu contraseña no se almacena en texto plano.</p>
              </div>
            </form>

            {mensaje && (
              <p className={`login-message login-message-${tipoMensaje}`} role="status">
                {mensaje}
              </p>
            )}

            <footer className="login-card-footer">
              <span className="login-enter-note">
                <IconoTeclado />
                Presiona Enter para continuar
              </span>

              <span className="login-environment">
                <span className="login-environment-dot" />
                Ambiente: {ambiente}
                <span className="login-footer-separator">|</span>
                Versión 1.0
              </span>
            </footer>
          </section>
        </section>
      </section>
    </main>
  )
}
