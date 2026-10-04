/**
 * Archivo: reportesTIApi.ts
 * Objetivo: Obtener los reportes de gestión de TI para un rango de fechas y filtros.
 * Responsabilidad: Armar la consulta con los filtros elegidos y devolver indicadores, gráficos y detalle exportable.
 * Dependencias: api.ts (cliente único de la API).
 * Flujo: ReportesTIPage -> reportesTIApi -> /api/reportes/ti.
 * Consideraciones: Los filtros vacíos no se envían; el backend limita el rango a 366 días.
 */

import { crearApi } from './api'

export interface ReportesTIFiltros {
  fechaInicio: string
  fechaFin: string
  area: string
  estado: string
  prioridad: string
  tipo: string
  usuarioTI: string
}

export interface ReportesTIResumen {
  total: number
  resueltos: number
  reabiertos: number
  tiempoPromedioHoras: number | null
  satisfaccion: number | null
  cumplimientoSla: number | null
  totalAnterior: number
  resueltosAnterior: number
  tiempoPromedioHorasAnterior: number | null
  satisfaccionAnterior: number | null
  horasEfectivas: number
  ticketsConEsfuerzo: number
}

export interface ReportesTIEvolucion {
  fecha: string
  cantidad: number
}

export interface ReportesTIEstado {
  estado: string
  estadoDescripcion: string
  cantidad: number
}

export interface ReportesTIArea {
  area: string
  areaDescripcion: string
  cantidad: number
}

export interface ReportesTIAvance {
  incidenciaNumero: string
  titulo: string
  areaDescripcion: string
  estado: string
  estadoDescripcion: string
  responsable: string
  porcentajeAvance: number | null
  fechaUltimoAvance: string | null
  avancesRegistrados: number
  minutosRegistrados: number
}

export interface ReportesTIUsuario {
  usuario: string
  responsable: string
  ticketsAsignados: number
  ticketsResueltos: number
  ticketsEnCurso: number
  avancesRegistrados: number
  minutosRegistrados: number
  porcentajePromedio: number | null
}

export interface ReportesTIPrioridadTiempo {
  prioridad: string
  tickets: number
  tiempoPromedioHoras: number | null
  tiempoMasRapidoHoras: number | null
  tiempoMasLargoHoras: number | null
}

export interface ReportesTITicketDestacado {
  incidenciaNumero: string
  titulo: string
  areaDescripcion: string
  estado: string
  estadoDescripcion: string
  prioridad: number | null
  responsable: string
  fechaRegistro: string
  tiempoAbiertoHoras: number
}

export interface ReportesTIDetalleExportacion {
  incidenciaNumero: string
  solicitante: string
  titulo: string
  areaDescripcion: string
  tipoDescripcion: string
  estadoDescripcion: string
  prioridad: number | null
  responsable: string
  fechaRegistro: string
  fechaCierre: string | null
  calificacion: number | null
}

export interface ReportesTICatalogo {
  codigo: string
  descripcion: string
}

export interface ReportesTICatalogos {
  areas: ReportesTICatalogo[]
  estados: ReportesTICatalogo[]
  tipos: ReportesTICatalogo[]
  operadores: ReportesTICatalogo[]
}

export interface ReportesTIRespuesta {
  resumen: ReportesTIResumen
  evolucion: ReportesTIEvolucion[]
  estados: ReportesTIEstado[]
  areas: ReportesTIArea[]
  avances: ReportesTIAvance[]
  avancePorUsuario: ReportesTIUsuario[]
  tiemposPorPrioridad: ReportesTIPrioridadTiempo[]
  ticketsPrioridadAlta: ReportesTITicketDestacado[]
  catalogos: ReportesTICatalogos
  detalleExportacion: ReportesTIDetalleExportacion[]
}

const api = crearApi({
  conexion: 'No fue posible comunicarse con el sistema. Intenta nuevamente.',
  sinPermiso: 'Tu perfil no tiene acceso a Reportes TI.',
})

export function obtenerReportesTI(filtros: ReportesTIFiltros) {
  const parametros = new URLSearchParams({ fechaInicio: filtros.fechaInicio, fechaFin: filtros.fechaFin })
  if (filtros.area) parametros.set('area', filtros.area)
  if (filtros.estado) parametros.set('estado', filtros.estado)
  if (filtros.prioridad) parametros.set('prioridad', filtros.prioridad)
  if (filtros.tipo) parametros.set('tipo', filtros.tipo)
  if (filtros.usuarioTI) parametros.set('usuarioTI', filtros.usuarioTI)
  return api<ReportesTIRespuesta>(`/api/reportes/ti?${parametros.toString()}`, { error: 'No fue posible generar el reporte.' })
}
