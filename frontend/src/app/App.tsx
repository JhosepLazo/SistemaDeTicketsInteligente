/**
 * Archivo: App.tsx
 * Objetivo: Definir las rutas públicas y protegidas de la aplicación.
 * Responsabilidad: Esperar la comprobación inicial de sesión y dirigir al usuario según su autenticación.
 * Dependencias: React Router, AutenticacionContext, LoginPage e InicioPage.
 * Flujo: main.tsx -> App -> comprobación de sesión -> ruta pública o protegida.
 * Consideraciones: La autorización definitiva de cada operación permanece en el backend.
 */

import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import type { ReactNode } from 'react'
import { AutenticacionProvider, useAutenticacion } from '../features/autenticacion/context/AutenticacionContext'
import LoginPage from '../features/autenticacion/pages/LoginPage'
import InicioPage from '../pages/InicioPage'

function RutaProtegida({ children }: { children: ReactNode }) {
  const { estado } = useAutenticacion()
  return estado === 'autenticado' ? children : <Navigate to="/login" replace />
}

function RutaPublica({ children }: { children: ReactNode }) {
  const { estado } = useAutenticacion()
  return estado === 'autenticado' ? <Navigate to="/inicio" replace /> : children
}

function RutasAplicacion() {
  const { estado } = useAutenticacion()

  if (estado === 'comprobandoSesion') {
    return <main role="status" aria-live="polite">Comprobando sesión...</main>
  }

  return (
    <Routes>
      <Route path="/login" element={<RutaPublica><LoginPage /></RutaPublica>} />
      <Route path="/inicio" element={<RutaProtegida><InicioPage /></RutaProtegida>} />
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
