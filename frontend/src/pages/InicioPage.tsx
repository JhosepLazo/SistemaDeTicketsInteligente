/**
 * Archivo: InicioPage.tsx
 * Objetivo: Probar el acceso a una ruta autenticada antes de desarrollar el módulo Inicio definitivo.
 * Responsabilidad: Mostrar la identidad segura de la sesión y permitir cerrar sesión.
 * Dependencias: React Router y AutenticacionContext.
 * Flujo: Ruta protegida /inicio -> InicioPage -> cierre de sesión -> /login.
 * Consideraciones: Es una vista temporal y no contiene dashboard ni módulos funcionales.
 */

import { useNavigate } from 'react-router-dom'
import { useAutenticacion } from '../features/autenticacion/context/AutenticacionContext'

export default function InicioPage() {
  const navigate = useNavigate()
  const { usuario, cerrarSesion } = useAutenticacion()

  if (!usuario) return null

  async function manejarCierreSesion() {
    await cerrarSesion()
    navigate('/login', { replace: true })
  }

  return (
    <main>
      <h1>Sistema Inteligente de Tickets</h1>
      <p>Bienvenido, {usuario.nombreCompleto}</p>
      <dl>
        <dt>Usuario</dt>
        <dd>{usuario.usuario}</dd>
        <dt>Área</dt>
        <dd>{usuario.area}</dd>
        <dt>Perfil</dt>
        <dd>{usuario.perfil}</dd>
      </dl>
      <button type="button" onClick={manejarCierreSesion}>Cerrar sesión</button>
    </main>
  )
}
