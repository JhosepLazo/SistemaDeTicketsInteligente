/*
 * Archivo: reportesTIService.ts
 * Objetivo: Comunicar el módulo Reportes TI con su endpoint protegido.
 * Responsabilidad: Exponer contratos TypeScript, construir filtros de consulta y devolver el tablero consolidado al frontend.
 * Dependencias: Fetch API y sesión autenticada mediante cookie HttpOnly.
 * Flujo: ReportesTIPage -> reportesTIService -> API /api/reportes/ti.
 * Consideraciones: El módulo es de solo lectura; la exportación se genera en el navegador a partir del detalle autorizado recibido desde la API.
 */

import { rechazarSiSesionExpirada } from '../../autenticacion/services/sesionExpirada'

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

export interface ReportesTIEvolucion { fecha: string; cantidad: number }
export interface ReportesTIEstado { estado: string; estadoDescripcion: string; cantidad: number }
export interface ReportesTIArea { area: string; areaDescripcion: string; cantidad: number }
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
export interface ReportesTIPrioridadTiempo { prioridad: string; tickets: number; tiempoPromedioHoras: number | null; tiempoMasRapidoHoras: number | null; tiempoMasLargoHoras: number | null }
export interface ReportesTITicketDestacado { incidenciaNumero: string; titulo: string; areaDescripcion: string; estado: string; estadoDescripcion: string; prioridad: number | null; responsable: string; fechaRegistro: string; tiempoAbiertoHoras: number }
export interface ReportesTIDetalleExportacion { incidenciaNumero: string; solicitante: string; titulo: string; areaDescripcion: string; tipoDescripcion: string; estadoDescripcion: string; prioridad: number | null; responsable: string; fechaRegistro: string; fechaCierre: string | null; calificacion: number | null }
export interface ReportesTICatalogo { codigo: string; descripcion: string }
export interface ReportesTICatalogos { areas: ReportesTICatalogo[]; estados: ReportesTICatalogo[]; tipos: ReportesTICatalogo[]; operadores: ReportesTICatalogo[] }

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

async function procesarRespuesta(respuesta: Response) {
  rechazarSiSesionExpirada(respuesta)
  if (respuesta.status === 403) throw new Error('Tu perfil no tiene acceso a Reportes TI.')
  if (respuesta.ok) return

  try {
    const error = await respuesta.json() as { mensaje?: string }
    throw new Error(error.mensaje || 'No fue posible generar el reporte.')
  } catch (error) {
    if (error instanceof Error && error.message !== 'Unexpected end of JSON input') throw error
    throw new Error('No fue posible generar el reporte.')
  }
}

export async function obtenerReportesTI(filtros: ReportesTIFiltros): Promise<ReportesTIRespuesta> {
  const parametros = new URLSearchParams({ fechaInicio: filtros.fechaInicio, fechaFin: filtros.fechaFin })
  if (filtros.area) parametros.set('area', filtros.area)
  if (filtros.estado) parametros.set('estado', filtros.estado)
  if (filtros.prioridad) parametros.set('prioridad', filtros.prioridad)
  if (filtros.tipo) parametros.set('tipo', filtros.tipo)
  if (filtros.usuarioTI) parametros.set('usuarioTI', filtros.usuarioTI)

  let respuesta: Response
  try { respuesta = await fetch(`/api/reportes/ti?${parametros.toString()}`, { credentials: 'include' }) }
  catch { throw new Error('No fue posible comunicarse con el sistema. Intenta nuevamente.') }

  await procesarRespuesta(respuesta)
  return respuesta.json() as Promise<ReportesTIRespuesta>
}
