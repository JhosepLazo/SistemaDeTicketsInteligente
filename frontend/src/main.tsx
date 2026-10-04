/**
 * Archivo: main.tsx
 * Objetivo: Iniciar la aplicación React en el navegador.
 * Responsabilidad: Localizar el elemento raíz, validar su existencia y montar la aplicación.
 * Dependencias: React, React DOM, App.tsx e index.css.
 * Flujo: index.html -> main.tsx -> App.tsx.
 * Consideraciones: Detiene el inicio con un error explícito si el documento no contiene el elemento root esperado.
 */

import { createRoot } from 'react-dom/client'
import App from './App'
import './index.css'

const contenedor = document.getElementById('root')

if (!contenedor) {
  throw new Error('No se encontró el elemento raíz de la aplicación.')
}

createRoot(contenedor).render(<App />)
