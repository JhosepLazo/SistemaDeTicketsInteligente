/**
 * Archivo: autenticacion.ts
 * Objetivo: Definir los contratos TypeScript compartidos por el flujo de autenticación.
 * Responsabilidad: Representar la solicitud de credenciales y la identidad segura devuelta por el backend.
 * Dependencias: TypeScript; no depende de componentes ni de APIs del navegador.
 * Flujo: LoginPage y AutenticacionContext <-> autenticacionService <-> backend.
 * Consideraciones: Los contratos no incluyen cookies ni hashes porque esos datos permanecen bajo control del backend.
 */

export interface SolicitudInicioSesion {
  usuario: string
  contrasena: string
}

export interface RespuestaInicioSesion {
  usuario: string
  nombreCompleto: string
  area: string
  perfil: string
}

export type EstadoAutenticacion = 'comprobandoSesion' | 'autenticado' | 'noAutenticado'
