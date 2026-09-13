/**
 * Archivo: App.tsx
 * Objetivo: Definir las rutas públicas y protegidas de la aplicación.
 * Responsabilidad: Esperar la comprobación inicial de sesión y dirigir al usuario al Inicio correspondiente según su perfil autenticado.
 * Dependencias: React Router, AutenticacionContext, LoginPage, InicioPage e InicioTIPage.
 * Flujo: main.tsx -> App -> comprobación de sesión -> selección de Inicio -> ruta pública o protegida.
 * Consideraciones: La selección visual por perfil se realiza en frontend; la autorización definitiva de cada endpoint permanece en el backend.
 */

import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import type { ReactNode } from 'react'
import { AutenticacionProvider, useAutenticacion } from '../features/autenticacion/context/AutenticacionContext'
import LoginPage from '../features/autenticacion/pages/LoginPage'
import InicioPage from '../pages/InicioPage'
import InicioTIPage from '../pages/InicioTIPage'

function RutaProtegida({ children }: { children: ReactNode }) {
  const { estado } = useAutenticacion()
  return estado === 'autenticado' ? children : <Navigate to="/login" replace />
}

function RutaPublica({ children }: { children: ReactNode }) {
  const { estado } = useAutenticacion()
  return estado === 'autenticado' ? <Navigate to="/inicio" replace /> : children
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
