/**
 * Archivo: LoginPage.tsx
 * Objetivo: Implementar la pantalla de acceso al Sistema de Tickets Inteligente.
 * Responsabilidad: Capturar credenciales, solicitar el inicio de sesión y presentar estados de carga, ayuda y errores al usuario.
 * Dependencias: AutenticacionContext, React, IconoLogin, MarcaCalimod, LoginPage.css e identidad visual de CALIMOD.
 * Flujo: Usuario -> LoginPage -> AutenticacionContext -> autenticacionApi -> API /api/autenticacion.
 * Consideraciones: No valida contraseñas localmente ni conserva credenciales; la autenticación efectiva pertenece al backend.
 */

import { useEffect, useState } from 'react'
import type { FormEvent, KeyboardEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAutenticacion } from './AutenticacionContext'
import IconoLogin from './IconoLogin'
import MarcaCalimod from './MarcaCalimod'
import './LoginPage.css'

const claveUsuarioRecordado = 'sti.usuarioRecordado'

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
                  <IconoLogin nombre="gestion" />
                </div>

                <div>
                  <h2>Gestión centralizada</h2>
                  <p>Todo tu soporte en un solo lugar.</p>
                </div>
              </article>

              <article className="login-feature-item">
                <div className="login-feature-icon">
                  <IconoLogin nombre="asistencia" />
                </div>

                <div>
                  <h2>Asistencia inteligente</h2>
                  <p>Respuestas más rápidas para tus tiendas, equipos y operaciones.</p>
                </div>
              </article>

              <article className="login-feature-item">
                <div className="login-feature-icon">
                  <IconoLogin nombre="seguimiento" />
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
                <IconoLogin nombre="soporte" />
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
              <IconoLogin nombre="sol" />
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
                <IconoLogin nombre="usuarios" />
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
                    <IconoLogin nombre="usuario" />
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
                    <IconoLogin nombre="candado" />
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
                    {mostrarContrasena ? <IconoLogin nombre="ocultar" /> : <IconoLogin nombre="ver" />}
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
                <IconoLogin nombre="recuperar" />

                <span>
                  <strong>Recuperar acceso</strong>
                  <small>Solicita ayuda para restablecer tu cuenta</small>
                </span>
              </button>

              <div className="login-security-note">
                <span className="login-security-icon">
                  <IconoLogin nombre="escudo" />
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
                <IconoLogin nombre="teclado" />
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
