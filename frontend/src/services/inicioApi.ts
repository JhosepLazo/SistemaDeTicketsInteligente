/**
 * Archivo: inicioApi.ts
 * Objetivo: Obtener el panel de inicio del colaborador y el panel operativo de TI.
 * Responsabilidad: Consultar los resúmenes que muestran InicioUsuarioPage e InicioTIPage.
 * Dependencias: api.ts (cliente único de la API).
 * Flujo: InicioUsuarioPage / InicioTIPage -> inicioApi -> /api/inicio/usuario y /api/inicio/ti.
 * Consideraciones: La identidad y el perfil salen de la cookie en el backend; el frontend nunca los envía.
 */

import { crearApi } from './api'

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

export interface InicioTIResumen {
  pendientes: number
  pendientesDesdeAyer: number
  enAtencion: number
  enProgresoHoy: number
  requierenAccion: number
  sinAsignar: number
  prioridadAlta: number
  ticketsActivos: number
  misAsignados: number
}

export interface InicioTITicketPrioritario {
  incidenciaNumero: string
  usuarioSolicitante: string
  titulo: string
  tipo: string
  tipoDescripcion: string
  estado: string
  estadoDescripcion: string
  prioridad: number | null
  usuarioTI: string
  responsable: string
  ultimaFechaModif: string
  slaMinutosRestantes: number | null
  tipoAtencion: string
  accion: string
}

export interface InicioTIRecordatorios {
  aprobacionesPendientes: number
  slaPorVencer: number
  ticketsReabiertos: number
}

export interface InicioTITicketActivo {
  incidenciaNumero: string
  usuarioSolicitante: string
  titulo: string
  tipo: string
  tipoDescripcion: string
  prioridad: number | null
  areaTI: string
  grupoSoporte: 'SOFTWARE' | 'HARDWARE' | 'OTRO'
  estado: string
  estadoDescripcion: string
  usuarioTI: string
  responsable: string
  ultimaFechaModif: string
}

export interface InicioTIActividad {
  incidenciaNumero: string
  titulo: string
  estado: string
  estadoDescripcion: string
  actor: string
  fechaCambio: string
}

export interface InicioTIRespuesta {
  resumen: InicioTIResumen
  requierenAtencion: InicioTITicketPrioritario[]
  recordatorios: InicioTIRecordatorios
  ticketsActivos: InicioTITicketActivo[]
  actividadReciente: InicioTIActividad[]
}

const api = crearApi()
const apiTI = crearApi({ sinPermiso: 'Tu perfil no tiene acceso al inicio operativo de TI.' })

export const obtenerInicioUsuario = () =>
  api<InicioUsuarioRespuesta>('/api/inicio/usuario', { error: 'No fue posible cargar la información de inicio.' })

export const obtenerInicioTI = () =>
  apiTI<InicioTIRespuesta>('/api/inicio/ti', { error: 'No fue posible cargar la información operativa de TI.' })
