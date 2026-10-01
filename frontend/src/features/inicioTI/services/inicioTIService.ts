/**
 * Archivo: inicioTIService.ts
 * Objetivo: Comunicar el módulo Inicio TI con el endpoint protegido que devuelve el dashboard operativo.
 * Responsabilidad: Solicitar la información consolidada del operador TI y exponer contratos TypeScript simples para la vista.
 * Dependencias: Fetch API y sesión autenticada mediante cookie HttpOnly.
 * Flujo: InicioTIPage -> inicioTIService -> API /api/inicio/ti.
 * Consideraciones: No envía usuario, área ni perfil; la identidad y autorización se resuelven en el backend desde la sesión autenticada.
 */

import { rechazarSiSesionExpirada } from '../../autenticacion/services/sesionExpirada'

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

export async function obtenerInicioTI(): Promise<InicioTIRespuesta> {
  let respuesta: Response

  try {
    respuesta = await fetch('/api/inicio/ti', { method: 'GET', credentials: 'include' })
  } catch {
    throw new Error('No fue posible comunicarse con el sistema. Intenta nuevamente en unos momentos.')
  }

  rechazarSiSesionExpirada(respuesta)
  if (respuesta.status === 403) throw new Error('Tu perfil no tiene acceso al inicio operativo de TI.')
  if (!respuesta.ok) throw new Error('No fue posible cargar la información operativa de TI.')

  return respuesta.json() as Promise<InicioTIRespuesta>
}
