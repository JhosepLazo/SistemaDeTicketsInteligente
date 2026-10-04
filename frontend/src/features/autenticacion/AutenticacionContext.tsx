/**
 * Archivo: AutenticacionContext.tsx
 * Objetivo: Mantener el estado global mínimo de la sesión autenticada en React.
 * Responsabilidad: Recuperar la cookie al iniciar, autenticar, cerrar sesión y exponer la identidad segura.
 * Dependencias: React, autenticacionApi y api.ts (aviso de sesión vencida y traza del agente).
 * Flujo: App -> AutenticacionProvider -> autenticacionApi -> API.
 * Consideraciones: La sesión vive en la cookie HttpOnly; este contexto no persiste tokens ni credenciales.
 */

import { createContext, useCallback, useContext, useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import { EVENTO_SESION_EXPIRADA, seleccionarSesionTraza } from '../../services/api'
import {
  cerrarSesion as cerrarSesionServicio,
  iniciarSesion as iniciarSesionServicio,
  obtenerSesion,
  type EstadoAutenticacion,
  type RespuestaInicioSesion,
  type SolicitudInicioSesion,
} from '../../services/autenticacionApi'

interface ValorAutenticacion {
  estado: EstadoAutenticacion
  usuario: RespuestaInicioSesion | null
  mensajeSesion: string
  iniciarSesion: (solicitud: SolicitudInicioSesion) => Promise<RespuestaInicioSesion>
  cerrarSesion: () => Promise<void>
  comprobarSesion: () => Promise<void>
}

const AutenticacionContext = createContext<ValorAutenticacion | null>(null)

export function AutenticacionProvider({ children }: { children: ReactNode }) {
  const [estado, setEstado] = useState<EstadoAutenticacion>('comprobandoSesion')
  const [usuario, setUsuario] = useState<RespuestaInicioSesion | null>(null)
  const [mensajeSesion, setMensajeSesion] = useState('')

  const comprobarSesion = useCallback(async () => {
    setEstado('comprobandoSesion')

    try {
      const sesion = await obtenerSesion()
      setUsuario(sesion)
      setEstado(sesion ? 'autenticado' : 'noAutenticado')
      setMensajeSesion('')
    } catch (error) {
      setUsuario(null)
      setEstado('noAutenticado')
      setMensajeSesion(error instanceof Error ? error.message : 'No fue posible comprobar la sesión.')
    }
  }, [])

  useEffect(() => {
    void comprobarSesion()
  }, [comprobarSesion])

  useEffect(() => {
    function manejarSesionExpirada() {
      seleccionarSesionTraza(null)
      setUsuario(null)
      setEstado('noAutenticado')
      setMensajeSesion('Tu sesión venció. Inicia sesión nuevamente para continuar.')
    }

    window.addEventListener(EVENTO_SESION_EXPIRADA, manejarSesionExpirada)
    return () => window.removeEventListener(EVENTO_SESION_EXPIRADA, manejarSesionExpirada)
  }, [])

  async function iniciarSesion(solicitud: SolicitudInicioSesion) {
    seleccionarSesionTraza(null)
    const sesion = await iniciarSesionServicio(solicitud)
    setUsuario(sesion)
    setEstado('autenticado')
    setMensajeSesion('')
    return sesion
  }

  async function cerrarSesion() {
    try {
      await cerrarSesionServicio()
      setMensajeSesion('')
    } catch (error) {
      setMensajeSesion(error instanceof Error ? error.message : 'No fue posible cerrar la sesión correctamente.')
    } finally {
      seleccionarSesionTraza(null)
      setUsuario(null)
      setEstado('noAutenticado')
    }
  }

  return (
    <AutenticacionContext.Provider
      value={{
        estado,
        usuario,
        mensajeSesion,
        iniciarSesion,
        cerrarSesion,
        comprobarSesion,
      }}
    >
      {children}
    </AutenticacionContext.Provider>
  )
}

/** Primer nombre de la persona, para saludos y la barra superior. */
export function primerNombre(nombreCompleto: string) {
  return nombreCompleto.trim().split(/\s+/)[0] || nombreCompleto
}

export function useAutenticacion() {
  const contexto = useContext(AutenticacionContext)
  if (!contexto) throw new Error('useAutenticacion debe utilizarse dentro de AutenticacionProvider.')
  return contexto
}
