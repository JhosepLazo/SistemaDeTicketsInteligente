/**
 * Archivo: GuardiasRuta.tsx
 * Objetivo: Decidir qué rutas puede abrir el usuario según su sesión y su perfil.
 * Responsabilidad: Enviar al login a quien no tiene sesión, al inicio a quien ya la tiene y entra al login, y al inicio a quien abre una
 *   pantalla de otro perfil (colaborador USR o equipo TI: TEC, SUP, ADM).
 * Dependencias: React Router y AutenticacionContext.
 * Flujo: App -> RutaProtegida / RutaPublica -> RutaUsuario / RutaTI -> página.
 * Consideraciones: Es solo la separación visual; la autorización definitiva de cada endpoint está en el backend.
 */

import type { ReactNode } from 'react'
import { Navigate } from 'react-router-dom'
import { useAutenticacion } from './AutenticacionContext'

const PERFILES_TI = ['TEC', 'SUP', 'ADM']

export function RutaProtegida({ children }: { children: ReactNode }) {
  const { estado } = useAutenticacion()
  return estado === 'autenticado' ? children : <Navigate to="/login" replace />
}

export function RutaPublica({ children }: { children: ReactNode }) {
  const { estado } = useAutenticacion()
  return estado === 'autenticado' ? <Navigate to="/inicio" replace /> : children
}

export function RutaUsuario({ children }: { children: ReactNode }) {
  const { usuario } = useAutenticacion()
  return usuario?.perfil === 'USR' ? children : <Navigate to="/inicio" replace />
}

export function RutaTI({ children }: { children: ReactNode }) {
  const { usuario } = useAutenticacion()
  return usuario && PERFILES_TI.includes(usuario.perfil) ? children : <Navigate to="/inicio" replace />
}
