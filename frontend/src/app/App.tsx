/**
 * Archivo: App.tsx
 * Objetivo: Definir las rutas públicas y protegidas de la aplicación.
 * Responsabilidad: Esperar la comprobación inicial de sesión y dirigir al usuario a las vistas permitidas según su perfil autenticado.
 * Dependencias: React Router, AutenticacionContext, LoginPage, InicioPage, InicioTIPage, NuevoTicketPage, MisTicketsUsuarioPage y GestionTicketsTIPage.
 * Flujo: main.tsx -> App -> comprobación de sesión -> validación de perfil -> ruta pública o protegida.
 * Consideraciones: La navegación visual aplica una primera separación por perfil; la autorización definitiva de cada endpoint permanece en el backend.
 */

import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import type { ReactNode } from 'react'
import { AutenticacionProvider, useAutenticacion } from '../features/autenticacion/context/AutenticacionContext'
import LoginPage from '../features/autenticacion/pages/LoginPage'
import InicioPage from '../pages/InicioPage'
import InicioTIPage from '../pages/InicioTIPage'
import NuevoTicketPage from '../pages/NuevoTicketPage'
import MisTicketsUsuarioPage from '../pages/MisTicketsUsuarioPage'
import GestionTicketsTIPage from '../pages/GestionTicketsTIPage'

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
  const perfilesTI = ['TEC', 'SUP', 'ADM']

  return usuario && perfilesTI.includes(usuario.perfil) ? <InicioTIPage /> : <InicioPage />
}

function RutasAplicacion() {
  const { estado } = useAutenticacion()

  if (estado === 'comprobandoSesion') {
    return <main role="status" aria-live="polite">Comprobando sesión...</main>
  }

  return (
    <Routes>
      <Route path="/login" element={<RutaPublica><LoginPage /></RutaPublica>} />
      <Route path="/inicio" element={<RutaProtegida><InicioSegunPerfil /></RutaProtegida>} />
      <Route path="/nuevo-ticket" element={<RutaProtegida><RutaUsuario><NuevoTicketPage /></RutaUsuario></RutaProtegida>} />
      <Route path="/mis-tickets" element={<RutaProtegida><RutaUsuario><MisTicketsUsuarioPage /></RutaUsuario></RutaProtegida>} />
      <Route path="/gestion-tickets" element={<RutaProtegida><RutaTI><GestionTicketsTIPage /></RutaTI></RutaProtegida>} />
      <Route path="*" element={<Navigate to={estado === 'autenticado' ? '/inicio' : '/login'} replace />} />
    </Routes>
  )
}

export default function App() {
  return (
    <BrowserRouter>
      <AutenticacionProvider>
        <RutasAplicacion />
      </AutenticacionProvider>
    </BrowserRouter>
  )
}
