/**
 * Archivo: FichasPanel.tsx
 * Objetivo: Mostrar y, para un administrador, mantener la ficha estructurada de cada tipo de ticket.
 * Responsabilidad: Listar los campos de la ficha del tipo elegido (bloque, orden, pregunta, tipo de dato, obligatoriedad, largos y estado)
 *   y crear o modificar un campo.
 * Dependencias: configuracionTIApi (obtenerFichasTI y guardarCampoFichaTI), CabeceraPanel e Icono.
 * Flujo: Maestros TI -> pestaña Fichas -> guardar campo -> Usp_TI_Guardar_CampoPlantilla (auditado) -> recarga.
 * Consideraciones: Un campo no se borra: se inactiva, para que las fichas ya registradas conserven su pregunta. Los cambios aplican a los
 *   tickets nuevos. Solo ADM guarda; el backend y el procedimiento lo exigen.
 */

import { useEffect, useMemo, useState } from 'react'
import { guardarCampoFichaTI, obtenerFichasTI, type CampoFichaTI, type TipoTI } from '../../services/configuracionTIApi'
import CabeceraPanel from './CabeceraPanel'
import Icono from '../../components/Icono'

type FormularioCampo = Omit<CampoFichaTI, 'tipoDescripcion' | 'ultimoUsuario' | 'ultimaFechaModif'>

const TIPOS_DATO: [FormularioCampo['tipoDato'], string][] = [
  ['TEXTO', 'Texto corto'],
  ['TEXTO_LARGO', 'Texto largo'],
  ['FECHA', 'Fecha'],
  ['SI_NO', 'Sí / No'],
]

const campoVacio = (tipo: string, orden: number): FormularioCampo => ({
  tipo,
  campo: '',
  bloque: '',
  orden,
  pregunta: '',
  ayuda: '',
  tipoDato: 'TEXTO',
  obligatorio: false,
  longitudMinima: 0,
  longitudMaxima: 500,
  estado: 'A',
})

export default function FichasPanel({ tipos, esAdministrador }: { tipos: TipoTI[]; esAdministrador: boolean }) {
  const [campos, setCampos] = useState<CampoFichaTI[]>([])
  const [tipo, setTipo] = useState('REQ')
  const [formulario, setFormulario] = useState<FormularioCampo>(campoVacio('REQ', 10))
  const [procesando, setProcesando] = useState(false)
  const [error, setError] = useState('')
  const [mensaje, setMensaje] = useState('')

  async function cargar() {
    try {
      setCampos(await obtenerFichasTI())
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No fue posible cargar las fichas.')
    }
  }
  useEffect(() => {
    void cargar()
  }, [])

  const delTipo = useMemo(() => campos.filter(x => x.tipo === tipo).sort((a, b) => a.orden - b.orden), [campos, tipo])
  const siguienteOrden = (delTipo.at(-1)?.orden ?? 0) + 10
  const conLargo = formulario.tipoDato === 'TEXTO' || formulario.tipoDato === 'TEXTO_LARGO'

  function cambiarTipo(valor: string) {
    setTipo(valor)
    const siguiente = (campos.filter(x => x.tipo === valor).reduce((maximo, x) => Math.max(maximo, x.orden), 0) || 0) + 10
    setFormulario(campoVacio(valor, siguiente))
  }

  async function guardar(e: React.FormEvent) {
    e.preventDefault()
    setProcesando(true)
    setError('')
    setMensaje('')
    try {
      await guardarCampoFichaTI({ ...formulario, campo: formulario.campo.trim().toUpperCase(), ayuda: formulario.ayuda.trim() })
      setMensaje(`El campo ${formulario.campo.toUpperCase()} de la ficha ${formulario.tipo} fue guardado.`)
      await cargar()
      setFormulario(campoVacio(tipo, siguienteOrden + 10))
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No fue posible guardar el campo.')
    } finally {
      setProcesando(false)
    }
  }

  const bloqueado = !esAdministrador || procesando

  return (
    <section className="config-ti-dos-columnas">
      <article className="config-ti-panel config-ti-panel--formulario">
        <CabeceraPanel
          icono="lista"
          titulo="Campo de la ficha"
          subtitulo="Lo que el colaborador debe responder al registrar este tipo de ticket."
        />
        <div className="config-ti-panel__cuerpo">
          {!esAdministrador && <p className="config-ti-ayuda">Puedes consultar las fichas; solo un administrador puede cambiarlas.</p>}
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
          <form className="config-ti-form" onSubmit={e => void guardar(e)}>
            <label>
              Tipo de ticket
              <select value={tipo} onChange={e => cambiarTipo(e.target.value)}>
                {tipos
                  .filter(x => x.estado === 'A')
                  .map(x => (
                    <option key={x.tipo} value={x.tipo}>
                      {x.tipo} · {x.descripcion}
                    </option>
                  ))}
              </select>
            </label>
            <label>
              Código del campo
              <input
                required
                maxLength={40}
                pattern="[A-Za-z0-9_]+"
                value={formulario.campo}
                disabled={bloqueado}
                onChange={e => setFormulario(v => ({ ...v, campo: e.target.value.toUpperCase() }))}
                placeholder="Ej. OBJETIVO_NEGOCIO"
              />
            </label>
            <label>
              Bloque
              <input
                required
                minLength={2}
                maxLength={60}
                value={formulario.bloque}
                disabled={bloqueado}
                onChange={e => setFormulario(v => ({ ...v, bloque: e.target.value }))}
                placeholder="Ej. A. Contexto"
              />
            </label>
            <label>
              Orden
              <input
                type="number"
                min={0}
                max={9999}
                value={formulario.orden}
                disabled={bloqueado}
                onChange={e => setFormulario(v => ({ ...v, orden: Number(e.target.value) }))}
              />
            </label>
            <label className="config-ti-form__completo">
              Pregunta
              <input
                required
                minLength={5}
                maxLength={300}
                value={formulario.pregunta}
                disabled={bloqueado}
                onChange={e => setFormulario(v => ({ ...v, pregunta: e.target.value }))}
              />
            </label>
            <label className="config-ti-form__completo">
              Ayuda para responder
              <input
                maxLength={500}
                value={formulario.ayuda}
                disabled={bloqueado}
                onChange={e => setFormulario(v => ({ ...v, ayuda: e.target.value }))}
              />
            </label>
            <label>
              Tipo de dato
              <select
                value={formulario.tipoDato}
                disabled={bloqueado}
                onChange={e => setFormulario(v => ({ ...v, tipoDato: e.target.value as FormularioCampo['tipoDato'] }))}
              >
                {TIPOS_DATO.map(([valor, texto]) => (
                  <option key={valor} value={valor}>
                    {texto}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Obligatorio
              <select
                value={formulario.obligatorio ? 'S' : 'N'}
                disabled={bloqueado}
                onChange={e => setFormulario(v => ({ ...v, obligatorio: e.target.value === 'S' }))}
              >
                <option value="S">Sí</option>
                <option value="N">No</option>
              </select>
            </label>
            {conLargo && (
              <>
                <label>
                  Largo mínimo
                  <input
                    type="number"
                    min={0}
                    max={formulario.longitudMaxima}
                    value={formulario.longitudMinima}
                    disabled={bloqueado}
                    onChange={e => setFormulario(v => ({ ...v, longitudMinima: Number(e.target.value) }))}
                  />
                </label>
                <label>
                  Largo máximo
                  <input
                    type="number"
                    min={1}
                    max={4000}
                    value={formulario.longitudMaxima}
                    disabled={bloqueado}
                    onChange={e => setFormulario(v => ({ ...v, longitudMaxima: Number(e.target.value) }))}
                  />
                </label>
              </>
            )}
            <label>
              Estado
              <select value={formulario.estado} disabled={bloqueado} onChange={e => setFormulario(v => ({ ...v, estado: e.target.value }))}>
                <option value="A">Activo</option>
                <option value="I">Inactivo</option>
              </select>
            </label>
            <div className="config-ti-form__acciones">
              <button type="button" disabled={procesando} onClick={() => setFormulario(campoVacio(tipo, siguienteOrden))}>
                Nuevo campo
              </button>
              <button className="principal" disabled={bloqueado}>
                Guardar campo
              </button>
            </div>
          </form>
        </div>
      </article>

      <article className="config-ti-panel">
        <CabeceraPanel
          icono="tipo"
          titulo={`Ficha del tipo ${tipo}`}
          subtitulo={`${delTipo.filter(x => x.estado === 'A').length} campos activos, ${delTipo.filter(x => x.estado === 'A' && x.obligatorio).length} obligatorios.`}
        />
        <div className="config-ti-panel__cuerpo config-ti-panel__cuerpo--tabla">
          {delTipo.length === 0 ? (
            <p className="config-ti-ayuda">Este tipo no tiene ficha: el ticket se registra solo con título, descripción y adjuntos.</p>
          ) : (
            <div className="config-ti-tabla">
              <table>
                <thead>
                  <tr>
                    <th>Orden</th>
                    <th>Bloque</th>
                    <th>Pregunta</th>
                    <th>Dato</th>
                    <th>Obligatorio</th>
                    <th>Estado</th>
                  </tr>
                </thead>
                <tbody>
                  {delTipo.map(x => (
                    <tr
                      key={x.campo}
                      onClick={() =>
                        setFormulario({
                          tipo: x.tipo,
                          campo: x.campo,
                          bloque: x.bloque,
                          orden: x.orden,
                          pregunta: x.pregunta,
                          ayuda: x.ayuda,
                          tipoDato: x.tipoDato,
                          obligatorio: x.obligatorio,
                          longitudMinima: x.longitudMinima,
                          longitudMaxima: x.longitudMaxima,
                          estado: x.estado,
                        })
                      }
                    >
                      <td>{x.orden}</td>
                      <td>{x.bloque}</td>
                      <td>
                        <strong>{x.pregunta}</strong>
                        <small className="config-ti-politica__defecto">{x.campo}</small>
                      </td>
                      <td>{TIPOS_DATO.find(([valor]) => valor === x.tipoDato)?.[1] ?? x.tipoDato}</td>
                      <td>{x.obligatorio ? 'Sí' : 'No'}</td>
                      <td>{x.estado === 'A' ? 'Activo' : 'Inactivo'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </article>
    </section>
  )
}
