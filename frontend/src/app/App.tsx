/**
 * Archivo: App.tsx
 * Objetivo: Definir las rutas públicas y protegidas de la aplicación.
 * Responsabilidad: Esperar la comprobación inicial de sesión y dirigir al usuario a las vistas permitidas según su perfil autenticado.
 * Dependencias: React Router, AutenticacionContext y páginas funcionales del portal.
 * Flujo: main.tsx -> App -> comprobación de sesión -> validación de perfil -> ruta pública o protegida.
 * Consideraciones: La navegación visual aplica una primera separación por perfil; la autorización definitiva de cada endpoint permanece en el backend.
 */

import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { Component, lazy, Suspense, type ErrorInfo, type ReactNode } from 'react'
import { AutenticacionProvider, useAutenticacion } from '../features/autenticacion/context/AutenticacionContext'
import LoginPage from '../features/autenticacion/pages/LoginPage'

const InicioPage = lazy(() => import('../pages/InicioPage'))
const AsistenteUsuarioPage = lazy(() => import('../pages/AsistenteUsuarioPage'))
const AsistenteTIPage = lazy(() => import('../pages/AsistenteTIPage'))
const InicioTIPage = lazy(() => import('../pages/InicioTIPage'))
const NuevoTicketPage = lazy(() => import('../pages/NuevoTicketPage'))
const MisTicketsUsuarioPage = lazy(() => import('../pages/MisTicketsUsuarioPage'))
const GestionTicketsTIPage = lazy(() => import('../pages/GestionTicketsTIPage'))
const BaseConocimientoTIPage = lazy(() => import('../pages/BaseConocimientoTIPage'))
const ReportesTIPage = lazy(() => import('../pages/ReportesTIPage'))
const ConfiguracionTIPage = lazy(() => import('../pages/ConfiguracionTIPage'))

class LimiteErrores extends Component<{ children: ReactNode }, { fallo: boolean }> {
  state = { fallo: false }

  static getDerivedStateFromError() {
    return { fallo: true }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('No fue posible renderizar el módulo solicitado.', error, info.componentStack)
  }

  render() {
    if (!this.state.fallo) return this.props.children

    return <main className="app-error" role="alert">
      <span className="app-error__marca">CALIMOD</span>
      <div className="app-error__icono" aria-hidden="true">!</div>
      <h1>No pudimos abrir este módulo</h1>
      <p>La sesión y tus datos permanecen protegidos. Recarga la aplicación para continuar.</p>
      <button type="button" onClick={() => window.location.reload()}>Recargar aplicación</button>
    </main>
  }
}

function CargandoModulo() {
  return <main className="app-cargando" role="status" aria-live="polite"><span className="app-cargando__marca">CALIMOD</span><span className="app-cargando__indicador" aria-hidden="true"/><strong>Preparando tu espacio de trabajo</strong></main>
}

function RutaProtegida({ children }: { children: ReactNode }) {
  const { estado } = useAutenticacion()
  return estado === 'autenticado' ? children : <Navigate to="/login" replace />
}

function RutaPublica({ children }: { children: ReactNode }) {
  const { estado } = useAutenticacion()
  return estado === 'autenticado' ? <Navigate to="/inicio" replace /> : children
}

function RutaUsuario({ children }: { children: ReactNode }) {
  const { usuario } = useAutenticacion()
  return usuario?.perfil === 'USR' ? children : <Navigate to="/inicio" replace />
}

function RutaTI({ children }: { children: ReactNode }) {
  const { usuario } = useAutenticacion()
  return usuario && ['TEC', 'SUP', 'ADM'].includes(usuario.perfil) ? children : <Navigate to="/inicio" replace />
}

function InicioSegunPerfil() {
  const { usuario } = useAutenticacion()
  return usuario && ['TEC', 'SUP', 'ADM'].includes(usuario.perfil) ? <InicioTIPage /> : <InicioPage />
}

function RutasAplicacion() {
  const { estado } = useAutenticacion()

  if (estado === 'comprobandoSesion') return <CargandoModulo />

  return <Routes>
    <Route path="/login" element={<RutaPublica><LoginPage /></RutaPublica>} />
    <Route path="/inicio" element={<RutaProtegida><InicioSegunPerfil /></RutaProtegida>} />
    <Route path="/asistente" element={<RutaProtegida><RutaUsuario><AsistenteUsuarioPage /></RutaUsuario></RutaProtegida>} />
    <Route path="/asistente-ti" element={<RutaProtegida><RutaTI><AsistenteTIPage /></RutaTI></RutaProtegida>} />
    <Route path="/nuevo-ticket" element={<RutaProtegida><RutaUsuario><NuevoTicketPage /></RutaUsuario></RutaProtegida>} />
    <Route path="/mis-tickets" element={<RutaProtegida><RutaUsuario><MisTicketsUsuarioPage /></RutaUsuario></RutaProtegida>} />
    <Route path="/gestion-tickets" element={<RutaProtegida><RutaTI><GestionTicketsTIPage /></RutaTI></RutaProtegida>} />
    <Route path="/base-conocimiento" element={<RutaProtegida><RutaTI><BaseConocimientoTIPage /></RutaTI></RutaProtegida>} />
    <Route path="/reportes" element={<RutaProtegida><RutaTI><ReportesTIPage /></RutaTI></RutaProtegida>} />
    <Route path="/configuracion-ti" element={<RutaProtegida><RutaTI><ConfiguracionTIPage /></RutaTI></RutaProtegida>} />
    <Route path="*" element={<Navigate to={estado === 'autenticado' ? '/inicio' : '/login'} replace />} />
  </Routes>
}

export default function App() {
  return <LimiteErrores><BrowserRouter><AutenticacionProvider><Suspense fallback={<CargandoModulo />}><RutasAplicacion /></Suspense></AutenticacionProvider></BrowserRouter></LimiteErrores>
}
