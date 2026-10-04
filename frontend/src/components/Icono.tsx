/**
 * Archivo: Icono.tsx
 * Objetivo: Dibujar los íconos del portal desde un único juego de trazos.
 * Responsabilidad: Entregar el SVG de cada ícono por su nombre, con el mismo grosor y estilo en todas las pantallas.
 * Dependencias: React.
 * Flujo: Página o componente -> <Icono nombre="..." /> -> SVG.
 * Consideraciones: Los nombres con sufijo (asistenteDestellos, alertaTriangulo, libroAbierto...) son variantes que algunas
 *   pantallas usan a propósito. enLinea quita la clase inicio-icono (display: block) donde el ícono debe fluir con el texto.
 */

import type { ReactNode } from 'react'

const trazos = {
  actividad: <path d="M3 12h4l2-5 4 10 2-5h6" />,
  actualizar: (
    <>
      {' '}
      <path d="M20 7v5h-5" /> <path d="M19 12a7 7 0 1 1-2-5" />{' '}
    </>
  ),
  agente: (
    <>
      {' '}
      <path d="M12 3 5 6v5c0 4.8 2.9 8.1 7 10 4.1-1.9 7-5.2 7-10V6l-7-3Z" />{' '}
      <path d="m12 7 .9 2.2L15 10l-2.1.8L12 13l-.9-2.2L9 10l2.1-.8L12 7Z" />{' '}
    </>
  ),
  alerta: (
    <>
      {' '}
      <circle cx="12" cy="12" r="9" /> <path d="M12 7v6M12 17h.01" />{' '}
    </>
  ),
  alertaTriangulo: (
    <>
      {' '}
      <path d="M12 3 2.5 20h19L12 3Z" /> <path d="M12 9v5M12 17h.01" />{' '}
    </>
  ),
  alertaTrianguloFino: (
    <>
      {' '}
      <path d="M12 3 2.8 20h18.4L12 3Z" /> <path d="M12 9v5M12 17h.01" />{' '}
    </>
  ),
  aprobacion: (
    <>
      {' '}
      <path d="M5 4h14v16H5z" /> <path d="M8 9h8M8 13h5" /> <path d="m14.5 16 1.5 1.5 3-3" />{' '}
    </>
  ),
  archivo: (
    <>
      {' '}
      <path d="M6 3h8l4 4v14H6V3Z" /> <path d="M14 3v5h5" />{' '}
    </>
  ),
  archivoAlto: (
    <>
      {' '}
      <path d="M6 2h8l4 4v16H6z" /> <path d="M14 2v5h5M9 12h6M9 16h6" />{' '}
    </>
  ),
  archivoLineas: (
    <>
      {' '}
      <path d="M6 3h8l4 4v14H6V3Z" /> <path d="M14 3v5h5M9 13h6M9 17h4" />{' '}
    </>
  ),
  area: (
    <>
      {' '}
      <path d="M4 20V10M9 20V6M14 20v-8M19 20V4" />{' '}
    </>
  ),
  asistente: (
    <>
      {' '}
      <path d="m12 3 1.2 3.1L16 7.5l-2.8 1.4L12 12l-1.2-3.1L8 7.5l2.8-1.4L12 3Z" />{' '}
      <path d="m5 13 .8 2.2L8 16l-2.2.8L5 19l-.8-2.2L2 16l2.2-.8L5 13Z" />{' '}
    </>
  ),
  asistenteDestellos: (
    <>
      {' '}
      <path d="m12 3 1.2 3.1L16 7.5l-2.8 1.4L12 12l-1.2-3.1L8 7.5l2.8-1.4L12 3Z" />{' '}
      <path d="m5 13 .8 2.2L8 16l-2.2.8L5 19l-.8-2.2L2 16l2.2-.8L5 13Z" />{' '}
      <path d="m19 12 .7 1.8L21.5 15l-1.8.7L19 17.5l-.7-1.8-1.8-.7 1.8-.7L19 12Z" />{' '}
    </>
  ),
  audifonos: (
    <>
      {' '}
      <path d="M4 14v-2a8 8 0 0 1 16 0v2" /> <path d="M4 14h3v6H5a1 1 0 0 1-1-1v-5ZM20 14h-3v6h2a1 1 0 0 0 1-1v-5Z" />{' '}
    </>
  ),
  bandera: (
    <>
      {' '}
      <path d="M5 21V4" /> <path d="M5 5h10l-2 3 2 3H5" />{' '}
    </>
  ),
  borrador: (
    <>
      {' '}
      <path d="M6 3h8l4 4v14H6V3Z" /> <path d="M14 3v5h5M9 13h6M9 17h4" />{' '}
    </>
  ),
  buscar: (
    <>
      {' '}
      <circle cx="11" cy="11" r="6" /> <path d="m16 16 4 4" />{' '}
    </>
  ),
  buscarAmplio: (
    <>
      {' '}
      <circle cx="10.5" cy="10.5" r="6.5" /> <path d="m16 16 5 5" />{' '}
    </>
  ),
  calendario: (
    <>
      {' '}
      <rect x="4" y="5" width="16" height="15" rx="2" /> <path d="M8 3v4M16 3v4M4 10h16" />{' '}
    </>
  ),
  campana: (
    <>
      {' '}
      <path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9" /> <path d="M10 21h4" />{' '}
    </>
  ),
  carpeta: <path d="M3 7h6l2 2h10v10H3V7Z" />,
  catalogos: (
    <>
      {' '}
      <rect x="4" y="4" width="6" height="6" rx="1" /> <rect x="14" y="4" width="6" height="6" rx="1" />{' '}
      <rect x="4" y="14" width="6" height="6" rx="1" /> <rect x="14" y="14" width="6" height="6" rx="1" />{' '}
    </>
  ),
  categoria: (
    <>
      {' '}
      <rect x="3" y="3" width="7" height="7" rx="1" /> <rect x="14" y="3" width="7" height="7" rx="1" />{' '}
      <rect x="3" y="14" width="7" height="7" rx="1" /> <rect x="14" y="14" width="7" height="7" rx="1" />{' '}
    </>
  ),
  cerrar: <path d="m6 6 12 12M18 6 6 18" />,
  cerrarPequeno: <path d="M7 7l10 10M17 7 7 17" />,
  chat: (
    <>
      {' '}
      <path d="M4 5h16v11H9l-5 4V5Z" /> <path d="M8 9h8M8 12h5" />{' '}
    </>
  ),
  check: (
    <>
      {' '}
      <circle cx="12" cy="12" r="9" /> <path d="m8 12 2.5 2.5L16 9" />{' '}
    </>
  ),
  clip: <path d="m8 12.5 6.7-6.7a3 3 0 0 1 4.3 4.3l-8.3 8.3a5 5 0 0 1-7.1-7.1l8-8" />,
  codigo: (
    <>
      {' '}
      <path d="m8 9-4 3 4 3M16 9l4 3-4 3M14 5l-4 14" />{' '}
    </>
  ),
  configuracion: (
    <>
      {' '}
      <circle cx="12" cy="12" r="3" />{' '}
      <path d="M19 12a7 7 0 0 0-.1-1l2-1.5-2-3.4-2.4 1a8 8 0 0 0-1.7-1L14.5 3h-5L9 6.1a8 8 0 0 0-1.7 1l-2.4-1-2 3.4 2 1.5a7 7 0 0 0 0 2l-2 1.5 2 3.4 2.4-1a8 8 0 0 0 1.7 1l.5 3.1h5l.5-3.1a8 8 0 0 0 1.7-1l2.4 1 2-3.4-2-1.5a7 7 0 0 0 .1-1Z" />{' '}
    </>
  ),
  conocimiento: (
    <path d="M4 5.5A3.5 3.5 0 0 1 7.5 2H11v18H7.5A3.5 3.5 0 0 0 4 23V5.5ZM20 5.5A3.5 3.5 0 0 0 16.5 2H13v18h3.5A3.5 3.5 0 0 1 20 23V5.5Z" />
  ),
  datos: (
    <>
      {' '}
      <ellipse cx="12" cy="5" rx="8" ry="3" /> <path d="M4 5v6c0 1.7 3.6 3 8 3s8-1.3 8-3V5M4 11v6c0 1.7 3.6 3 8 3s8-1.3 8-3v-6" />{' '}
    </>
  ),
  descargar: (
    <>
      {' '}
      <path d="M12 3v12M8 11l4 4 4-4" /> <path d="M4 20h16" />{' '}
    </>
  ),
  documento: (
    <>
      {' '}
      <path d="M6 3h8l4 4v14H6V3Z" /> <path d="M14 3v5h5M9 13h6M9 17h4" />{' '}
    </>
  ),
  edificio: (
    <>
      {' '}
      <path d="M4 21h16M6 21V7l6-3 6 3v14" /> <path d="M9 10h.01M12 10h.01M15 10h.01M9 14h.01M12 14h.01M15 14h.01" />{' '}
    </>
  ),
  editar: (
    <>
      {' '}
      <path d="M4 20h4L19 9l-4-4L4 16v4Z" /> <path d="m13.5 6.5 4 4" />{' '}
    </>
  ),
  engranaje: (
    <>
      {' '}
      <circle cx="12" cy="12" r="3" />{' '}
      <path d="M19 12a7 7 0 0 0-.1-1l2-1.5-2-3.4-2.4 1a8 8 0 0 0-1.7-1L14.5 3h-5L9 6.1a8 8 0 0 0-1.7 1l-2.4-1-2 3.4 2 1.5a7 7 0 0 0 0 2l-2 1.5 2 3.4 2.4-1a8 8 0 0 0 1.7 1l.5 3.1h5l.5-3.1a8 8 0 0 0 1.7-1l2.4 1 2-3.4-2-1.5a7 7 0 0 0 .1-1Z" />{' '}
    </>
  ),
  enviar: (
    <>
      {' '}
      <path d="m3 11 18-8-7 18-3-7-8-3Z" /> <path d="m11 14 4-4" />{' '}
    </>
  ),
  escudo: (
    <>
      {' '}
      <path d="M12 3 5 6v5c0 4.8 2.9 8.1 7 10 4.1-1.9 7-5.2 7-10V6l-7-3Z" /> <path d="m9 12 2 2 4-5" />{' '}
    </>
  ),
  estrella: <path d="m12 3 2.7 5.5 6.1.9-4.4 4.3 1 6.1-5.4-2.9-5.4 2.9 1-6.1-4.4-4.3 6.1-.9L12 3Z" />,
  filtro: <path d="M3 5h18l-7 8v5l-4 2v-7L3 5Z" />,
  flecha: (
    <>
      {' '}
      <path d="M5 12h14M15 8l4 4-4 4" />{' '}
    </>
  ),
  fuego: <path d="M13 3c1 4-2 5-1 8 1-2 3-2 4-4 3 3 4 6 3 9a7 7 0 0 1-14 0c0-3 2-6 5-9 0 3 1 4 3 5-1-4 1-6 0-9Z" />,
  gestion: (
    <>
      {' '}
      <rect x="4" y="4" width="16" height="16" rx="2" /> <path d="M8 9h8M8 13h5M8 17h3" /> <path d="m16 15 1.5 1.5L20 14" />{' '}
    </>
  ),
  grafico: (
    <>
      {' '}
      <path d="M3 20h18M5 17l4-5 4 2 6-8" /> <path d="M17 6h2v2" />{' '}
    </>
  ),
  guardar: (
    <>
      {' '}
      <path d="M5 4h12l2 2v14H5V4Z" /> <path d="M8 4v6h8V4M8 15h8" />{' '}
    </>
  ),
  inactivar: (
    <>
      {' '}
      <circle cx="12" cy="12" r="9" /> <path d="M8 8l8 8" />{' '}
    </>
  ),
  info: (
    <>
      {' '}
      <circle cx="12" cy="12" r="9" /> <path d="M12 11v5M12 8h.01" />{' '}
    </>
  ),
  inicio: (
    <>
      {' '}
      <path d="M3 11.5 12 4l9 7.5" /> <path d="M5.5 10.5V20h13v-9.5" /> <path d="M9.5 20v-6h5v6" />{' '}
    </>
  ),
  libro: (
    <>
      {' '}
      <path d="M5 4h12a2 2 0 0 1 2 2v14H7a2 2 0 0 1-2-2V4Z" /> <path d="M8 8h7M8 12h7" />{' '}
    </>
  ),
  libroAbierto: (
    <>
      {' '}
      <path d="M4 5.5A3.5 3.5 0 0 1 7.5 2H11v17H7.5A3.5 3.5 0 0 0 4 22V5.5Z" />{' '}
      <path d="M20 5.5A3.5 3.5 0 0 0 16.5 2H13v17h3.5A3.5 3.5 0 0 1 20 22V5.5Z" />{' '}
    </>
  ),
  limpiar: (
    <>
      {' '}
      <path d="m4 15 8-8 5 5-8 8H4v-5Z" /> <path d="m10 9 5 5M13 20h7" />{' '}
    </>
  ),
  lista: (
    <>
      {' '}
      <path d="M8 6h12M8 12h12M8 18h12" /> <path d="M4 6h.01M4 12h.01M4 18h.01" />{' '}
    </>
  ),
  luz: (
    <>
      {' '}
      <path d="M9 18h6M10 21h4" /> <path d="M8 14a6 6 0 1 1 8 0c-1 .8-1 1.4-1 2H9c0-.6 0-1.2-1-2Z" />{' '}
    </>
  ),
  mas: (
    <>
      {' '}
      <circle cx="12" cy="5" r="1" fill="currentColor" stroke="none" /> <circle cx="12" cy="12" r="1" fill="currentColor" stroke="none" />{' '}
      <circle cx="12" cy="19" r="1" fill="currentColor" stroke="none" />{' '}
    </>
  ),
  matriz: (
    <>
      {' '}
      <path d="M4 19V9M10 19V5M16 19v-7M22 19H2" /> <path d="m4 9 6-4 6 7 4-4" />{' '}
    </>
  ),
  mensaje: (
    <>
      {' '}
      <path d="M4 5h16v12H8l-4 4V5Z" /> <path d="M8 9h8M8 13h5" />{' '}
    </>
  ),
  microfono: (
    <>
      {' '}
      <rect x="9" y="3" width="6" height="11" rx="3" /> <path d="M5 11a7 7 0 0 0 14 0M12 18v3" />{' '}
    </>
  ),
  modulo: (
    <>
      {' '}
      <rect x="4" y="4" width="16" height="16" rx="2" /> <path d="M8 8h8M8 12h5M8 16h8" />{' '}
    </>
  ),
  nuevo: (
    <>
      {' '}
      <circle cx="12" cy="12" r="9" /> <path d="M12 8v8M8 12h8" />{' '}
    </>
  ),
  ordenar: (
    <>
      {' '}
      <path d="M8 6h11M8 12h8M8 18h5" /> <path d="m3 7 2-2 2 2M5 5v14" />{' '}
    </>
  ),
  pantalla: (
    <>
      {' '}
      <rect x="3" y="4" width="18" height="13" rx="2" /> <path d="M8 21h8M12 17v4" />{' '}
    </>
  ),
  pastel: (
    <>
      {' '}
      <path d="M12 3v9h9A9 9 0 1 1 12 3Z" /> <path d="M15 3.5A8 8 0 0 1 20.5 9H15V3.5Z" />{' '}
    </>
  ),
  proceso: (
    <>
      {' '}
      <circle cx="12" cy="12" r="3" />{' '}
      <path d="M19 12a7 7 0 0 0-.1-1l2-1.5-2-3.4-2.4 1a8 8 0 0 0-1.7-1L14.5 3h-5L9 6.1a8 8 0 0 0-1.7 1l-2.4-1-2 3.4 2 1.5a7 7 0 0 0 0 2l-2 1.5 2 3.4 2.4-1a8 8 0 0 0 1.7 1l.5 3.1h5l.5-3.1a8 8 0 0 0 1.7-1l2.4 1 2-3.4-2-1.5a7 7 0 0 0 .1-1Z" />{' '}
    </>
  ),
  reabrir: (
    <>
      {' '}
      <path d="M20 11a8 8 0 1 0-2.3 5.7" /> <path d="M20 5v6h-6" />{' '}
    </>
  ),
  reloj: (
    <>
      {' '}
      <circle cx="12" cy="12" r="9" /> <path d="M12 7v5l3 2" />{' '}
    </>
  ),
  reporte: (
    <>
      {' '}
      <path d="M4 20V10M10 20V4M16 20v-7M22 20H2" />{' '}
    </>
  ),
  resumen: (
    <>
      {' '}
      <path d="M6 3h9l3 3v15H6V3Z" /> <path d="M9 10h6M9 14h6M9 18h4" />{' '}
    </>
  ),
  revisar: (
    <>
      {' '}
      <path d="M4 4h16v16H4z" /> <path d="M8 9h8M8 13h5" />{' '}
    </>
  ),
  salir: (
    <>
      {' '}
      <path d="M10 5H5v14h5" /> <path d="m14 8 4 4-4 4M18 12H9" />{' '}
    </>
  ),
  stop: (
    <>
      {' '}
      <rect x="6" y="6" width="12" height="12" rx="1" />{' '}
    </>
  ),
  subir: (
    <>
      {' '}
      <path d="M12 16V5M8 9l4-4 4 4" /> <path d="M5 17v3h14v-3" />{' '}
    </>
  ),
  ticket: (
    <>
      {' '}
      <path d="M4 7h16v10H4z" /> <path d="M8 7v3M16 7v3M8 14h8" />{' '}
    </>
  ),
  tickets: (
    <>
      {' '}
      <path d="M5 4h14a1 1 0 0 1 1 1v14a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V5a1 1 0 0 1 1-1Z" /> <path d="M8 9h8M8 13h6M8 17h4" />{' '}
    </>
  ),
  tipo: (
    <>
      {' '}
      <circle cx="12" cy="12" r="9" /> <path d="M8 12h8M12 8v8" />{' '}
    </>
  ),
  usuario: (
    <>
      {' '}
      <circle cx="12" cy="8" r="3" /> <path d="M5 20c.8-4 3.2-6 7-6s6.2 2 7 6" />{' '}
    </>
  ),
  usuarioGrande: (
    <>
      {' '}
      <circle cx="12" cy="8" r="4" /> <path d="M4 21a8 8 0 0 1 16 0" />{' '}
    </>
  ),
  usuarios: (
    <>
      {' '}
      <circle cx="9" cy="8" r="3" /> <path d="M3 20a6 6 0 0 1 12 0" /> <circle cx="17" cy="9" r="2" />{' '}
      <path d="M15 15a5 5 0 0 1 6 5" />{' '}
    </>
  ),
  validar: (
    <>
      {' '}
      <circle cx="12" cy="12" r="9" /> <path d="m8 12 2.5 2.5L16 9" />{' '}
    </>
  ),
} satisfies Record<string, ReactNode>

export type NombreIcono = keyof typeof trazos

export interface PropsIcono {
  nombre: NombreIcono
  size?: number
  /** Sin la clase inicio-icono: el SVG queda en línea con el texto. */
  enLinea?: boolean
}

export default function Icono({ nombre, size = 20, enLinea = false }: PropsIcono) {
  return (
    <svg
      className={enLinea ? undefined : 'inicio-icono'}
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      {trazos[nombre]}
    </svg>
  )
}
