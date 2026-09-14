/**
 * Archivo: vite.config.ts
 * Objetivo: Configurar el entorno de desarrollo y construcción del frontend React.
 * Responsabilidad: Habilitar el complemento de React, establecer el puerto local y redirigir las solicitudes /api hacia el backend.
 * Dependencias: Vite y @vitejs/plugin-react.
 * Flujo: npm script -> Vite -> aplicación React y proxy local hacia ASP.NET Core.
 * Consideraciones: El proxy solo facilita el desarrollo local y no sustituye la configuración del entorno de despliegue.
 */

import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
    },
  },
})
