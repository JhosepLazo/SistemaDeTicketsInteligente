/**
 * Archivo: inicioUsuarioService.ts
 * Objetivo: Comunicar el módulo Inicio con el endpoint protegido que devuelve el dashboard del usuario.
 * Responsabilidad: Solicitar el resumen personal y exponer contratos TypeScript simples para la vista.
 * Dependencias: Fetch API y sesión autenticada mediante cookie HttpOnly.
 * Flujo: InicioPage -> inicioUsuarioService -> API /api/inicio/usuario.
 * Consideraciones: No almacena datos de sesión ni permite enviar un usuario arbitrario; la identidad se obtiene en el backend desde la cookie autenticada.
 */

export interface InicioUsuarioResumen {
  ticketsActivos: number
  enAtencion: number
  requierenAtencion: number
  resueltos30Dias: number
  pendientesCalificacion: number
}

export interface InicioUsuarioTicket {
  incidenciaNumero: string
  titulo: string
  estado: string
  estadoDescripcion: string
  responsable: string
  accion: string
  ultimaFechaModif: string
}

export interface InicioUsuarioAccionPendiente {
  incidenciaNumero: string
  titulo: string
  tipoAccion: string
  descripcion: string
  ultimaFechaModif: string
}

export interface InicioUsuarioActividad {
  incidenciaNumero: string
  titulo: string
  estadoDescripcion: string
  actor: string
  fechaCambio: string
}

export interface InicioUsuarioRespuesta {
  resumen: InicioUsuarioResumen
  requierenAtencion: InicioUsuarioTicket[]
  ticketsRecientes: InicioUsuarioTicket[]
  accionesPendientes: InicioUsuarioAccionPendiente[]
  actividadReciente: InicioUsuarioActividad[]
}

export async function obtenerInicioUsuario(): Promise<InicioUsuarioRespuesta> {
  let respuesta: Response

  try {
    respuesta = await fetch('/api/inicio/usuario', { method: 'GET', credentials: 'include' })
  } catch {
    throw new Error('No fue posible comunicarse con el sistema. Intenta nuevamente en unos momentos.')
  }

  if (respuesta.status === 401) throw new Error('Tu sesión ya no se encuentra disponible. Vuelve a iniciar sesión.')
  if (!respuesta.ok) throw new Error('No fue posible cargar la información de inicio.')

  return respuesta.json() as Promise<InicioUsuarioRespuesta>
}
