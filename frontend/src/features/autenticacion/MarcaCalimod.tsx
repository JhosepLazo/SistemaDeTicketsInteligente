/**
 * Archivo: MarcaCalimod.tsx
 * Objetivo: Dibujar la marca de CALIMOD en la pantalla de acceso.
 * Responsabilidad: Entregar el isotipo en SVG con el tamaño y la clase indicados.
 * Dependencias: React.
 * Flujo: LoginPage -> MarcaCalimod -> SVG.
 * Consideraciones: Usa currentColor para tomar el color del contenedor.
 */

export default function MarcaCalimod({ className = '', size = 55 }) {
  return (
    <svg
      className={className}
      width={size}
      viewBox="0 5 90 65"
      xmlns="http://www.w3.org/2000/svg"
      role="img"
      aria-label="Calimod"
      focusable="false"
      fill="currentColor"
    >
      <path d="M2 58L31 7l14 24-8 14-6-11-14 24H2Z" />
      <path d="M35 58L63 11l28 47H75L63 37 51 58H35Z" />
    </svg>
  )
}
