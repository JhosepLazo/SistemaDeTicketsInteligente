/**
 * Archivo: vite.config.ts
 * Objetivo: Configurar el entorno de desarrollo y construcción del frontend React.
 * Responsabilidad: Habilitar el complemento de React, establecer el puerto local, redirigir las solicitudes /api hacia el backend y
 *   configurar las pruebas (Vitest con jsdom).
 * Dependencias: Vite, @vitejs/plugin-react y Vitest.
 * Flujo: npm script -> Vite -> aplicación React y proxy local hacia ASP.NET Core; npm test -> Vitest -> archivos .test.ts y .test.tsx de src.
 * Consideraciones: El proxy solo facilita el desarrollo local y no sustituye la configuración del entorno de despliegue.
 */

/// <reference types="vitest/config" />
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
  test: {
    environment: 'jsdom',
    include: ['src/**/*.test.{ts,tsx}'],
  },
})
