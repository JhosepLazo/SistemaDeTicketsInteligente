/**
 * Archivo: reportesTIApi.ts
 * Objetivo: Obtener los reportes de gestión de TI para un rango de fechas y filtros.
 * Responsabilidad: Armar la consulta con los filtros elegidos y devolver indicadores, gráficos y detalle exportable, además de los
 *   indicadores del agente (investigaciones, aprobaciones, ejecuciones, clasificación, fichas, modelos y comparativo con TI).
 * Dependencias: api.ts (cliente único de la API).
 * Flujo: ReportesTIPage -> reportesTIApi -> /api/reportes/ti y /api/reportes/ti/agente.
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

export interface ConteoTI {
  valor: string
  cantidad: number
}

/** Indicadores del agente para el período: lo que investigó, propuso, ejecutó y cuánto acertó frente a TI. */
export interface MetricasAgenteTI {
  investigaciones: {
    total: number
    automaticas: number
    conDiagnostico: number
    conAccionPropuesta: number
    solucionValidada: number
    canceladas: number
    confianzaPromedio: number | null
    minutosPromedioDiagnostico: number | null
  }
  estadosInvestigacion: ConteoTI[]
  aprobaciones: {
    origen: string
    solicitadas: number
    aprobadas: number
    rechazadas: number
    canceladas: number
    pendientes: number
    vencidas: number
    horasPromedioRespuesta: number | null
  }[]
  motivosRechazo: ConteoTI[]
  ejecuciones: { tipoEjecutor: string; estado: string; cantidad: number; filasAfectadas: number }[]
  clasificacion: {
    propuestas: number
    comparadas: number
    coincideTipo: number
    coincideSubTipo: number
    coincideItem: number
    confianzaPromedio: number | null
  }
  tipos: {
    tipo: string
    tipoDescripcion: string
    total: number
    resueltos: number
    reabiertos: number
    desdeAsistente: number
    minutosPromedioPrimeraRespuesta: number | null
    horasPromedioResolucion: number | null
    calificacionPromedio: number | null
  }[]
  fichas: { requerimientos: number; conFichaCompleta: number; devueltosRecopilacion: number }
  conocimiento: { conocimientoCodigo: string; titulo: string; vecesEvidencia: number; ticketsResueltosSinReapertura: number }[]
  modelos: {
    modelo: string
    llamadas: number
    tokensEntrada: number
    tokensSalida: number
    duracionPromedioMs: number | null
    fallidas: number
  }[]
  comparativo: {
    sesionNumero: number
    incidenciaNumero: string
    titulo: string
    estadoTicket: string
    estadoSesion: string
    causaAgente: string
    confianza: number | null
    accionCodigo: string
    decision: string
    solucionValidada: boolean
    causaRaizTI: string
    solucionTI: string
    tipoResolucion: string
    reabierto: boolean
    fechaDiagnostico: string | null
  }[]
}

export const obtenerMetricasAgenteTI = (desde: string, hasta: string) =>
  api<MetricasAgenteTI>(`/api/reportes/ti/agente?${new URLSearchParams({ desde, hasta }).toString()}`, {
    error: 'No fue posible obtener los indicadores del agente.',
  })
