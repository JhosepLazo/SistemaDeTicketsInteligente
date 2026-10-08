/**
 * Archivo: IconoLogin.tsx
 * Objetivo: Reunir los íconos propios de la pantalla de acceso.
 * Responsabilidad: Entregar el SVG de cada ícono del login por su nombre, con el trazo y relleno con que fue diseñado.
 * Dependencias: React.
 * Flujo: LoginPage -> <IconoLogin nombre="..." /> -> SVG.
 * Consideraciones: Estos íconos tienen grosores propios (1,7 y 1,8) y uno relleno, distintos de components/Icono; por eso viven aquí y
 *   no en el ícono común. Son decorativos (aria-hidden).
 */

import type { ReactElement } from 'react'

const iconos = {
  gestion: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <rect x="3.5" y="4.5" width="17" height="12.5" rx="2" />
      <path d="M9 20h6M12 17v3" />
    </svg>
  ),
  asistencia: (
    <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
      <path d="M12 2l1.9 6.1L20 10l-6.1 1.9L12 18l-1.9-6.1L4 10l6.1-1.9L12 2z" />
    </svg>
  ),
  seguimiento: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <path d="M5 20V11M10 20V6M15 20v-8M20 20V3" />
    </svg>
  ),
  usuario: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <circle cx="12" cy="8" r="3.6" />
      <path d="M5 20c.8-3.6 3.2-5.5 7-5.5s6.2 1.9 7 5.5" />
    </svg>
  ),
  usuarios: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <circle cx="9" cy="8" r="3" />
      <circle cx="17" cy="9" r="2.4" />
      <path d="M3.5 20c.6-3.4 2.8-5.2 6.2-5.2s5.6 1.8 6.2 5.2" />
      <path d="M15.8 14.5c2.8.2 4.3 1.7 4.7 4.5" />
    </svg>
  ),
  candado: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <rect x="5" y="10" width="14" height="11" rx="2" />
      <path d="M8 10V7a4 4 0 1 1 8 0v3" />
    </svg>
  ),
  ver: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <path d="M2 12s3.5-6 10-6 10 6 10 6-3.5 6-10 6-10-6-10-6z" />
      <circle cx="12" cy="12" r="2.7" />
    </svg>
  ),
  ocultar: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <path d="m3 3 18 18" />
      <path d="M10.5 10.7a2.6 2.6 0 0 0 3.7 3.7" />
      <path d="M9.8 5.2A10.4 10.4 0 0 1 12 5c6.5 0 10 7 10 7a18.2 18.2 0 0 1-3.1 4.1" />
      <path d="M6.2 6.2A18.5 18.5 0 0 0 2 12s3.5 7 10 7a10.8 10.8 0 0 0 4-.8" />
    </svg>
  ),
  escudo: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <path d="M12 3l7 3v5c0 5-3.4 8.8-7 10-3.6-1.2-7-5-7-10V6l7-3z" />
      <path d="m9.4 12.4 1.8 1.8 3.7-4.2" />
    </svg>
  ),
  soporte: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <path d="M4 13a8 8 0 0 1 16 0" />
      <path d="M4 13v3a2 2 0 0 0 2 2h1v-6H6a2 2 0 0 0-2 2z" />
      <path d="M20 13v3a2 2 0 0 1-2 2h-1v-6h1a2 2 0 0 1 2 2z" />
      <path d="M12 20h3" />
    </svg>
  ),
  recuperar: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <circle cx="12" cy="12" r="7" />
      <path d="M12 8v8M8 12h8" />
    </svg>
  ),
  teclado: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" aria-hidden="true">
      <rect x="2.5" y="6" width="19" height="12" rx="2" />
      <path d="M6 10h1M10 10h1M14 10h1M18 10h1M6 14h1M10 14h7" />
    </svg>
  ),
  sol: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" aria-hidden="true">
      <circle cx="12" cy="12" r="3.5" />
      <path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4" />
    </svg>
  ),
} satisfies Record<string, ReactElement>

export type NombreIconoLogin = keyof typeof iconos

export default function IconoLogin({ nombre }: { nombre: NombreIconoLogin }) {
  return iconos[nombre]
}
