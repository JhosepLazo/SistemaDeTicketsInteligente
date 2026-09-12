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