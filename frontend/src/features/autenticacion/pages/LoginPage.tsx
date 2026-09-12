import { FormEvent, useState } from 'react'
import './LoginPage.css'

export default function LoginPage() {
  const [mensaje, setMensaje] = useState('')

  function manejarEnvio(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    setMensaje('La interfaz está lista. La autenticación se habilitará al definir la identidad corporativa.')
  }

  return (
    <main className="login-layout">
      <section className="login-card" aria-labelledby="login-title">
        <p className="login-kicker">Gestión de TI</p>
        <h1 id="login-title">Sistema de Tickets Inteligente</h1>
        <p className="login-description">Ingresa con tus credenciales corporativas.</p>

        <form onSubmit={manejarEnvio}>
          <label htmlFor="usuario">Usuario</label>
          <input id="usuario" name="usuario" autoComplete="username" maxLength={20} required />

          <label htmlFor="contrasena">Contraseña</label>
          <input id="contrasena" name="contrasena" type="password" autoComplete="current-password" required />

          <button type="submit">Ingresar</button>
        </form>

        {mensaje && <p className="login-message" role="status">{mensaje}</p>}
      </section>
    </main>
  )
}
