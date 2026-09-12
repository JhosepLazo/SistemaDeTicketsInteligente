/**
 * Archivo: AutenticacionContext.tsx
 * Objetivo: Mantener el estado global mínimo de la sesión autenticada en React.
 * Responsabilidad: Recuperar la cookie al iniciar, autenticar, cerrar sesión y exponer la identidad segura.
 * Dependencias: React, autenticacionService y contratos de autenticación.
 * Flujo: App -> AutenticacionProvider -> autenticacionService -> API.
 * Consideraciones: La sesión vive en la cookie HttpOnly; este contexto no persiste tokens ni credenciales.
 */

import { createContext, useCallback, useContext, useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import {
  cerrarSesion as cerrarSesionServicio,
  iniciarSesion as iniciarSesionServicio,
  obtenerSesion,
} from '../services/autenticacionService'
import type {
  EstadoAutenticacion,
  RespuestaInicioSesion,
  SolicitudInicioSesion,
} from '../types/autenticacion'

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

  async function iniciarSesion(solicitud: SolicitudInicioSesion) {
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
      setUsuario(null)
      setEstado('noAutenticado')
    }
  }

  return (
    <AutenticacionContext.Provider value={{
      estado,
      usuario,
      mensajeSesion,
      iniciarSesion,
      cerrarSesion,
      comprobarSesion,
    }}>
      {children}
    </AutenticacionContext.Provider>
  )
}

export function useAutenticacion() {
  const contexto = useContext(AutenticacionContext)
  if (!contexto) throw new Error('useAutenticacion debe utilizarse dentro de AutenticacionProvider.')
  return contexto
}
