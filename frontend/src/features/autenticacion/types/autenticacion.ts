export interface SolicitudInicioSesion {
  usuario: string
  contrasena: string
}

export interface RespuestaInicioSesion {
  autenticado: boolean
  usuario?: string
  nombreCompleto?: string
  area?: string
  perfil?: string
  mensaje?: string
}
