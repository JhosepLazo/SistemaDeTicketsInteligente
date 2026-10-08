/**
 * Archivo: VistaAgente.tsx
 * Objetivo: Mostrar en Reportes TI cómo trabajó el agente en el período y cuánto coincidió con TI.
 * Responsabilidad: Presentar investigaciones, aprobaciones por origen, motivos de rechazo, ejecuciones, acierto de clasificación, fichas de
 *   requerimiento, uso de modelos, conocimiento útil y el comparativo caso por caso entre la causa del agente y la causa confirmada por TI.
 * Dependencias: reportesTIApi (obtenerMetricasAgenteTI) e Icono.
 * Flujo: ReportesTIPage (pestaña Agente) -> /api/reportes/ti/agente -> Usp_TI_Obtener_MetricasAgente y Usp_TI_Obtener_ComparativoAgente.
 * Consideraciones: Son los datos para decidir cuánta autonomía conceder (modo sombra antes de autónomo). Solo usa el rango de fechas;
 *   los demás filtros del reporte no aplican a estos indicadores.
 */

import { useEffect, useState } from 'react'
import { obtenerMetricasAgenteTI, type MetricasAgenteTI } from '../../services/reportesTIApi'
import Icono, { type NombreIcono } from '../../components/Icono'

function numero(valor: number | null | undefined, decimales = 1) {
  return valor === null || valor === undefined ? '—' : new Intl.NumberFormat('es-PE', { maximumFractionDigits: decimales }).format(valor)
}
function porcentaje(parte: number, total: number) {
  return total > 0 ? `${Math.round((parte / total) * 100)}%` : '—'
}
const ORIGEN: Record<string, string> = { AGENTE: 'Propuestas del agente', MANUAL: 'Solicitudes manuales de TI' }
const EJECUTOR: Record<string, string> = { T: 'Aprobada por TI', I: 'Autónoma del agente' }

export default function VistaAgente({ desde, hasta }: { desde: string; hasta: string }) {
  const [datos, setDatos] = useState<MetricasAgenteTI | null>(null)
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    let vigente = true
    setCargando(true)
    setError('')
    obtenerMetricasAgenteTI(desde, hasta)
      .then(valor => vigente && setDatos(valor))
      .catch(e => vigente && setError(e instanceof Error ? e.message : 'No fue posible obtener los indicadores del agente.'))
      .finally(() => vigente && setCargando(false))
    return () => {
      vigente = false
    }
  }, [desde, hasta])

  if (cargando) return <div className="reportes-ti-vacio">Calculando indicadores del agente...</div>
  if (error || !datos) return <div className="reportes-ti-vacio">{error || 'Sin datos del agente para el período.'}</div>
  const { investigaciones: inv, clasificacion: cla } = datos

  return (
    <section className="reportes-ti-agente">
      <div className="reportes-ti-agente__kpis">
        <Kpi titulo="Investigaciones" valor={inv.total} detalle={`${inv.automaticas} automáticas`} />
        <Kpi titulo="Con diagnóstico" valor={inv.conDiagnostico} detalle={porcentaje(inv.conDiagnostico, inv.total)} />
        <Kpi titulo="Con acción propuesta" valor={inv.conAccionPropuesta} detalle={porcentaje(inv.conAccionPropuesta, inv.total)} />
        <Kpi titulo="Solución validada" valor={inv.solucionValidada} detalle={`${inv.canceladas} canceladas`} />
        <Kpi titulo="Confianza promedio" valor={`${numero(inv.confianzaPromedio)}%`} detalle="De los diagnósticos emitidos" />
        <Kpi titulo="Minutos a diagnóstico" valor={numero(inv.minutosPromedioDiagnostico)} detalle="Promedio desde el inicio" />
      </div>

      <div className="reportes-ti-agente__grid">
        <Panel icono="aprobacion" titulo="Aprobaciones" subtitulo="Qué se aprobó, rechazó o venció, por origen.">
          <Tabla
            columnas={['Origen', 'Solicitadas', 'Aprobadas', 'Rechazadas', 'Vencidas', 'Pendientes', 'Horas a respuesta']}
            filas={datos.aprobaciones.map(x => [
              ORIGEN[x.origen] ?? x.origen,
              x.solicitadas,
              `${x.aprobadas} (${porcentaje(x.aprobadas, x.solicitadas)})`,
              x.rechazadas,
              x.vencidas,
              x.pendientes,
              numero(x.horasPromedioRespuesta),
            ])}
          />
          {datos.motivosRechazo.length > 0 && (
            <ul className="reportes-ti-agente__lista">
              {datos.motivosRechazo.map(x => (
                <li key={x.valor}>
                  <span>{x.valor}</span>
                  <b>{x.cantidad}</b>
                </li>
              ))}
            </ul>
          )}
        </Panel>

        <Panel icono="proceso" titulo="Ejecuciones" subtitulo="Cambios realizados por el agente con aprobación o de forma autónoma.">
          <Tabla
            columnas={['Decisión', 'Estado', 'Cantidad', 'Filas afectadas']}
            filas={datos.ejecuciones.map(x => [
              EJECUTOR[x.tipoEjecutor] ?? x.tipoEjecutor,
              x.estado === 'OK' ? 'Correcta' : x.estado === 'ER' ? 'Con error' : 'En proceso',
              x.cantidad,
              x.filasAfectadas,
            ])}
          />
          <ul className="reportes-ti-agente__lista">
            {datos.estadosInvestigacion.map(x => (
              <li key={x.valor}>
                <span>{x.valor}</span>
                <b>{x.cantidad}</b>
              </li>
            ))}
          </ul>
        </Panel>

        <Panel icono="categoria" titulo="Clasificación propuesta" subtitulo="Coincidencia de la IA con la clasificación final de TI.">
          <ul className="reportes-ti-agente__lista">
            <li>
              <span>Propuestas / comparadas con TI</span>
              <b>
                {cla.propuestas} / {cla.comparadas}
              </b>
            </li>
            <li>
              <span>Acierto en el tipo</span>
              <b>{porcentaje(cla.coincideTipo, cla.comparadas)}</b>
            </li>
            <li>
              <span>Acierto en el subtipo</span>
              <b>{porcentaje(cla.coincideSubTipo, cla.comparadas)}</b>
            </li>
            <li>
              <span>Acierto en el ítem</span>
              <b>{porcentaje(cla.coincideItem, cla.comparadas)}</b>
            </li>
            <li>
              <span>Confianza promedio</span>
              <b>{numero(cla.confianzaPromedio)}%</b>
            </li>
          </ul>
        </Panel>

        <Panel icono="lista" titulo="Fichas de requerimiento" subtitulo="Completitud de la recopilación estructurada.">
          <ul className="reportes-ti-agente__lista">
            <li>
              <span>Requerimientos registrados</span>
              <b>{datos.fichas.requerimientos}</b>
            </li>
            <li>
              <span>Con ficha completa</span>
              <b>{porcentaje(datos.fichas.conFichaCompleta, datos.fichas.requerimientos)}</b>
            </li>
            <li>
              <span>Devueltos por datos faltantes</span>
              <b>{datos.fichas.devueltosRecopilacion}</b>
            </li>
          </ul>
        </Panel>
      </div>

      <Panel icono="tipo" titulo="Por tipo de ticket" subtitulo="Tiempos, reaperturas y calificación de cada tipo.">
        <Tabla
          columnas={[
            'Tipo',
            'Total',
            'Resueltos',
            'Reabiertos',
            'Desde el asistente',
            'Min. primera respuesta',
            'Horas a resolución',
            'Calificación',
          ]}
          filas={datos.tipos.map(x => [
            `${x.tipo} · ${x.tipoDescripcion}`,
            x.total,
            x.resueltos,
            x.reabiertos,
            x.desdeAsistente,
            numero(x.minutosPromedioPrimeraRespuesta),
            numero(x.horasPromedioResolucion),
            numero(x.calificacionPromedio),
          ])}
        />
      </Panel>

      <div className="reportes-ti-agente__grid">
        <Panel icono="asistente" titulo="Modelos de IA" subtitulo="Llamadas, tokens y tiempo de respuesta por modelo.">
          <Tabla
            columnas={['Modelo', 'Llamadas', 'Fallidas', 'Tokens entrada', 'Tokens salida', 'ms promedio']}
            filas={datos.modelos.map(x => [
              x.modelo,
              x.llamadas,
              x.fallidas,
              x.tokensEntrada,
              x.tokensSalida,
              numero(x.duracionPromedioMs, 0),
            ])}
          />
        </Panel>
        <Panel
          icono="conocimiento"
          titulo="Conocimiento útil"
          subtitulo="Artículos citados como evidencia y tickets resueltos sin reapertura."
        >
          <Tabla
            columnas={['Artículo', 'Veces citado', 'Resueltos sin reapertura']}
            filas={datos.conocimiento.map(x => [
              `${x.conocimientoCodigo} · ${x.titulo}`,
              x.vecesEvidencia,
              x.ticketsResueltosSinReapertura,
            ])}
          />
        </Panel>
      </div>

      <Panel
        icono="revisar"
        titulo="Agente frente a TI"
        subtitulo="Causa diagnosticada por el agente y causa raíz confirmada por TI, caso por caso."
      >
        <Tabla
          columnas={['Ticket', 'Causa del agente', 'Confianza', 'Acción', 'Decisión', 'Causa raíz de TI', 'Validada', 'Reabierto']}
          filas={datos.comparativo.map(x => [
            `${x.incidenciaNumero} · ${x.titulo}`,
            x.causaAgente || '—',
            x.confianza === null ? '—' : `${numero(x.confianza, 0)}%`,
            x.accionCodigo || '—',
            x.decision || x.estadoSesion,
            x.causaRaizTI || '—',
            x.solucionValidada ? 'Sí' : 'No',
            x.reabierto ? 'Sí' : 'No',
          ])}
        />
      </Panel>
    </section>
  )
}

function Kpi({ titulo, valor, detalle }: { titulo: string; valor: number | string; detalle: string }) {
  return (
    <article>
      <small>{titulo}</small>
      <strong>{valor}</strong>
      <span>{detalle}</span>
    </article>
  )
}

function Panel({
  icono,
  titulo,
  subtitulo,
  children,
}: {
  icono: NombreIcono
  titulo: string
  subtitulo: string
  children: React.ReactNode
}) {
  return (
    <article className="reportes-ti-panel reportes-ti-agente__panel">
      <div className="reportes-ti-panel__cabecera">
        <div>
          <span className="reportes-ti-panel__icono">
            <Icono nombre={icono} size={19} />
          </span>
          <div>
            <h2>{titulo}</h2>
            <p>{subtitulo}</p>
          </div>
        </div>
      </div>
      {children}
    </article>
  )
}

function Tabla({ columnas, filas }: { columnas: string[]; filas: (string | number)[][] }) {
  if (filas.length === 0) return <div className="reportes-ti-vacio">Sin registros en el período.</div>
  return (
    <div className="reportes-ti-agente__tabla">
      <table>
        <thead>
          <tr>
            {columnas.map(x => (
              <th key={x}>{x}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {filas.map((fila, i) => (
            <tr key={i}>
              {fila.map((celda, j) => (
                <td key={j}>{celda}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
