/**
 * Archivo: ControlAgentePanel.tsx
 * Objetivo: Mostrar y, para un administrador, cambiar cuánto puede hacer el agente de TI.
 * Responsabilidad: Administrar el interruptor del agente, el techo de riesgo autónomo, el umbral de confianza para proponer, la vigencia
 *   de las aprobaciones, el autocierre de tickets sin validar, la política de autonomía por tipo y acción y los atributos de las acciones.
 * Dependencias: configuracionTIApi (control del agente), CabeceraPanel e Icono.
 * Flujo: Maestros TI -> pestaña Agente y autonomía -> guardar -> procedimiento con auditoría -> recarga.
 * Consideraciones: Cualquier perfil TI consulta; solo ADM guarda (el backend lo exige). AUTONOMA solo existe para solicitudes (SOL) y
 *   exige confianza mínima de 60 a 100; bajar el techo de riesgo devuelve a APROBACION las políticas que lo superen.
 */

import { useEffect, useState, type ReactElement } from 'react'
import {
  NIVELES_RIESGO,
  guardarAccionCatalogoTI,
  guardarParametroAgenteTI,
  guardarPoliticaAutonomiaTI,
  obtenerControlAgenteTI,
  type AccionCatalogoTI,
  type ControlAgenteTI,
  type ModoPolitica,
  type ParametroTI,
  type PoliticaAutonomiaTI,
} from '../../services/configuracionTIApi'
import CabeceraPanel from './CabeceraPanel'
import Icono from '../../components/Icono'

const MODOS_AGENTE: [string, string][] = [
  ['APAGADO', 'Apagado: no investiga, no propone ni ejecuta.'],
  ['SOMBRA', 'Sombra: investiga y propone; nunca ejecuta.'],
  ['ASISTIDO', 'Asistido: ejecuta solo lo que TI aprueba.'],
  ['AUTONOMO', 'Autónomo: además ejecuta sin humano lo que la política libera.'],
]

const DEFINICION_PARAMETROS: Record<string, { titulo: string; ayuda: string; opcional?: boolean; minimo?: number; maximo?: number }> = {
  AGENTE_MODO: { titulo: 'Interruptor del agente', ayuda: 'Se aplica de inmediato a las investigaciones nuevas y a las ejecuciones.' },
  AGENTE_RIESGO_MAXIMO_AUTONOMO: {
    titulo: 'Techo de riesgo autónomo',
    ayuda: 'Ninguna acción con riesgo mayor se ejecuta sin humano. Al bajarlo, las políticas que lo superen vuelven a APROBACION.',
  },
  AGENTE_CONFIANZA_MINIMA_PROPUESTA: {
    titulo: 'Confianza mínima para proponer (%)',
    ayuda: 'Por debajo de este valor el agente informa el diagnóstico sin proponer una acción.',
    minimo: 1,
    maximo: 100,
  },
  APROBACION_VIGENCIA_HORAS: {
    titulo: 'Vigencia de una aprobación (horas)',
    ayuda: 'Vacío: no vence. Una aprobación vencida obliga a pedirla de nuevo antes de ejecutar.',
    opcional: true,
    minimo: 1,
    maximo: 720,
  },
  TICKET_AUTOCIERRE_PV_DIAS: {
    titulo: 'Autocierre sin validación (días)',
    ayuda: 'Vacío: desactivado. Un ticket en PV sin respuesta del colaborador se cierra pasado este plazo.',
    opcional: true,
    minimo: 1,
    maximo: 90,
  },
}

function fechaHora(fecha: string | null) {
  return fecha ? new Intl.DateTimeFormat('es-PE', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(fecha)) : ''
}

export default function ControlAgentePanel({ esAdministrador }: { esAdministrador: boolean }) {
  const [control, setControl] = useState<ControlAgenteTI | null>(null)
  const [cargando, setCargando] = useState(true)
  const [procesando, setProcesando] = useState(false)
  const [error, setError] = useState('')
  const [mensaje, setMensaje] = useState('')

  async function cargar() {
    setCargando(true)
    try {
      setControl(await obtenerControlAgenteTI())
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No fue posible cargar el control del agente.')
    } finally {
      setCargando(false)
    }
  }
  useEffect(() => {
    void cargar()
  }, [])

  async function guardar(accion: () => Promise<unknown>, exito: string) {
    setProcesando(true)
    setError('')
    setMensaje('')
    try {
      await accion()
      setMensaje(exito)
      await cargar()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No fue posible guardar el cambio.')
    } finally {
      setProcesando(false)
    }
  }

  if (cargando && !control)
    return (
      <section className="inicio-estado-carga">
        <span className="inicio-spinner" /> Cargando control del agente...
      </section>
    )
  if (!control) return error ? <div className="config-ti-alerta config-ti-alerta--error">{error}</div> : null
  const bloqueado = !esAdministrador || procesando

  return (
    <section className="config-ti-agente">
      {!esAdministrador && (
        <p className="config-ti-ayuda">
          <Icono nombre="info" size={16} /> Puedes consultar esta configuración; solo un administrador puede cambiarla.
        </p>
      )}
      {error && (
        <div className="config-ti-alerta config-ti-alerta--error" role="alert">
          <Icono nombre="alerta" size={19} />
          <span>{error}</span>
        </div>
      )}
      {mensaje && (
        <div className="config-ti-alerta config-ti-alerta--ok" role="status">
          <Icono nombre="check" size={19} />
          <span>{mensaje}</span>
        </div>
      )}

      <article className="config-ti-panel">
        <CabeceraPanel icono="agente" titulo="Interruptor y límites" subtitulo="Cuánto puede hacer el agente y con qué umbrales." />
        <div className="config-ti-panel__cuerpo config-ti-parametros">
          {control.parametros.map(parametro => (
            <FilaParametro
              key={parametro.parametro}
              parametro={parametro}
              bloqueado={bloqueado}
              onGuardar={valor => guardar(() => guardarParametroAgenteTI(parametro.parametro, valor), 'El parámetro fue actualizado.')}
            />
          ))}
        </div>
      </article>

      <article className="config-ti-panel">
        <CabeceraPanel
          icono="escudo"
          titulo="Política de autonomía"
          subtitulo="Por tipo de ticket y acción: autónoma, con aprobación de TI o prohibida."
        />
        <div className="config-ti-panel__cuerpo config-ti-panel__cuerpo--tabla">
          <p className="config-ti-ayuda">
            Una acción autónoma además debe ser reversible, no exigir aprobación en el catálogo, no superar el techo de riesgo y venir de un
            diagnóstico con la confianza mínima; el agente debe estar en modo Autónomo.
          </p>
          <div className="config-ti-tabla">
            <table>
              <thead>
                <tr>
                  <th>Tipo</th>
                  <th>Acción</th>
                  <th>Modo</th>
                  <th>Confianza mínima</th>
                  <th>Estado</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {control.politica.map(politica => (
                  <FilaPolitica
                    key={`${politica.tipo}-${politica.accionCodigo}`}
                    politica={politica}
                    bloqueado={bloqueado}
                    onGuardar={x => guardar(() => guardarPoliticaAutonomiaTI(x), 'La política de autonomía fue actualizada.')}
                  />
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </article>

      <article className="config-ti-panel">
        <CabeceraPanel
          icono="lista"
          titulo="Catálogo de acciones"
          subtitulo="Riesgo, aprobación y reversibilidad de cada acción que el agente puede proponer."
        />
        <div className="config-ti-panel__cuerpo config-ti-panel__cuerpo--tabla">
          <div className="config-ti-tabla">
            <table>
              <thead>
                <tr>
                  <th>Acción</th>
                  <th>Ejecutor</th>
                  <th>Riesgo</th>
                  <th>Requiere aprobación</th>
                  <th>Reversible</th>
                  <th>Estado</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {control.acciones.map(accion => (
                  <FilaAccion
                    key={accion.accionCodigo}
                    accion={accion}
                    bloqueado={bloqueado}
                    onGuardar={x => guardar(() => guardarAccionCatalogoTI(x), 'La acción fue actualizada.')}
                  />
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </article>
    </section>
  )
}

function FilaParametro({
  parametro,
  bloqueado,
  onGuardar,
}: {
  parametro: ParametroTI
  bloqueado: boolean
  onGuardar: (valor: string | null) => void
}) {
  const [valor, setValor] = useState(parametro.valor ?? '')
  useEffect(() => setValor(parametro.valor ?? ''), [parametro.valor])
  const definicion = DEFINICION_PARAMETROS[parametro.parametro] ?? { titulo: parametro.parametro, ayuda: parametro.descripcion }
  const cambiado = valor !== (parametro.valor ?? '')

  let control: ReactElement
  if (parametro.parametro === 'AGENTE_MODO')
    control = (
      <select value={valor} disabled={bloqueado} onChange={e => setValor(e.target.value)}>
        {MODOS_AGENTE.map(([codigo, texto]) => (
          <option key={codigo} value={codigo}>
            {texto}
          </option>
        ))}
      </select>
    )
  else if (parametro.parametro === 'AGENTE_RIESGO_MAXIMO_AUTONOMO')
    control = (
      <select value={valor} disabled={bloqueado} onChange={e => setValor(e.target.value)}>
        {NIVELES_RIESGO.map(nivel => (
          <option key={nivel} value={nivel}>
            {nivel}
          </option>
        ))}
      </select>
    )
  else
    control = (
      <input
        type="number"
        min={definicion.minimo}
        max={definicion.maximo}
        required={!definicion.opcional}
        placeholder={definicion.opcional ? 'Sin valor' : undefined}
        value={valor}
        disabled={bloqueado}
        onChange={e => setValor(e.target.value)}
      />
    )

  return (
    <form
      className="config-ti-parametro"
      onSubmit={e => {
        e.preventDefault()
        onGuardar(valor.trim() || null)
      }}
    >
      <label>
        <strong>{definicion.titulo}</strong>
        {control}
        <small>{definicion.ayuda}</small>
        {parametro.ultimoUsuario && (
          <small>
            Último cambio: {parametro.ultimoUsuario} · {fechaHora(parametro.ultimaFechaModif)}
          </small>
        )}
      </label>
      <button className="principal" disabled={bloqueado || !cambiado}>
        Guardar
      </button>
    </form>
  )
}

function FilaPolitica({
  politica,
  bloqueado,
  onGuardar,
}: {
  politica: PoliticaAutonomiaTI
  bloqueado: boolean
  onGuardar: (x: { tipo: string; accionCodigo: string; modo: ModoPolitica; confianzaMinima: number | null; estado: string }) => void
}) {
  const [modo, setModo] = useState<ModoPolitica>(politica.modo)
  const [confianza, setConfianza] = useState(politica.confianzaMinima ?? 80)
  const [estado, setEstado] = useState(politica.estado)
  useEffect(() => {
    setModo(politica.modo)
    setConfianza(politica.confianzaMinima ?? 80)
    setEstado(politica.estado)
  }, [politica])
  const autonomaPermitida = politica.tipo === 'SOL'

  return (
    <tr>
      <td>
        <strong>{politica.tipo}</strong> {politica.tipoDescripcion}
      </td>
      <td>
        <strong>{politica.accionCodigo}</strong> {politica.accionNombre}
        {!politica.configurada && <small className="config-ti-politica__defecto">Valor por defecto</small>}
      </td>
      <td>
        <select value={modo} disabled={bloqueado} onChange={e => setModo(e.target.value as ModoPolitica)}>
          <option value="APROBACION">Con aprobación de TI</option>
          <option value="AUTONOMA" disabled={!autonomaPermitida}>
            Autónoma{autonomaPermitida ? '' : ' (solo SOL)'}
          </option>
          <option value="PROHIBIDA">Prohibida</option>
        </select>
      </td>
      <td>
        {modo === 'AUTONOMA' ? (
          <input
            type="number"
            min={60}
            max={100}
            value={confianza}
            disabled={bloqueado}
            onChange={e => setConfianza(Number(e.target.value))}
          />
        ) : (
          '—'
        )}
      </td>
      <td>
        <select value={estado} disabled={bloqueado} onChange={e => setEstado(e.target.value)}>
          <option value="A">Activa</option>
          <option value="I">Inactiva</option>
        </select>
      </td>
      <td>
        <button
          type="button"
          className="principal"
          disabled={bloqueado}
          onClick={() =>
            onGuardar({
              tipo: politica.tipo,
              accionCodigo: politica.accionCodigo,
              modo,
              confianzaMinima: modo === 'AUTONOMA' ? confianza : null,
              estado,
            })
          }
        >
          Guardar
        </button>
      </td>
    </tr>
  )
}

function FilaAccion({
  accion,
  bloqueado,
  onGuardar,
}: {
  accion: AccionCatalogoTI
  bloqueado: boolean
  onGuardar: (x: { accionCodigo: string; nivelRiesgo: string; requiereAprobacion: boolean; reversible: boolean; estado: string }) => void
}) {
  const [valores, setValores] = useState({
    nivelRiesgo: accion.nivelRiesgo,
    requiereAprobacion: accion.requiereAprobacion,
    reversible: accion.reversible,
    estado: accion.estado,
  })
  useEffect(
    () =>
      setValores({
        nivelRiesgo: accion.nivelRiesgo,
        requiereAprobacion: accion.requiereAprobacion,
        reversible: accion.reversible,
        estado: accion.estado,
      }),
    [accion],
  )

  return (
    <tr>
      <td>
        <strong>{accion.accionCodigo}</strong> {accion.nombre}
        <small className="config-ti-politica__defecto">{accion.tipo === 'E' ? 'Ejecución' : 'Lectura'}</small>
      </td>
      <td>{accion.tieneEjecutor ? accion.procedimiento : 'Sin ejecutor'}</td>
      <td>
        <select value={valores.nivelRiesgo} disabled={bloqueado} onChange={e => setValores(v => ({ ...v, nivelRiesgo: e.target.value }))}>
          {NIVELES_RIESGO.map(nivel => (
            <option key={nivel} value={nivel}>
              {nivel}
            </option>
          ))}
        </select>
      </td>
      <td>
        <input
          type="checkbox"
          checked={valores.requiereAprobacion}
          disabled={bloqueado}
          onChange={e => setValores(v => ({ ...v, requiereAprobacion: e.target.checked }))}
          aria-label="Requiere aprobación"
        />
      </td>
      <td>
        <input
          type="checkbox"
          checked={valores.reversible}
          disabled={bloqueado}
          onChange={e => setValores(v => ({ ...v, reversible: e.target.checked }))}
          aria-label="Reversible"
        />
      </td>
      <td>
        <select value={valores.estado} disabled={bloqueado} onChange={e => setValores(v => ({ ...v, estado: e.target.value }))}>
          <option value="A">Activa</option>
          <option value="I">Inactiva</option>
        </select>
      </td>
      <td>
        <button
          type="button"
          className="principal"
          disabled={bloqueado}
          onClick={() => onGuardar({ accionCodigo: accion.accionCodigo, ...valores })}
        >
          Guardar
        </button>
      </td>
    </tr>
  )
}
