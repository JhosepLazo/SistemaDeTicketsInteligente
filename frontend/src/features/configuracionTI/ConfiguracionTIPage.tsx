/**
 * Archivo: ConfiguracionTIPage.tsx
 * Objetivo: Administrar los maestros funcionales indispensables del sistema de incidencias TI.
 * Responsabilidad: Mantener catálogos operativos, matriz Ítem/Categoría, SLA y usuarios corporativos desde una vista común para el equipo TI,
 *   y mostrar el control del agente y las fichas de los tipos de ticket.
 * Dependencias: AutenticacionContext, configuracionTIApi, MarcoPortal, Icono, CabeceraPanel, ControlAgentePanel, FichasPanel y
 *   ConfiguracionTIPage.css.
 * Flujo: /configuracion-ti -> ConfiguracionTIPage -> configuracionTIApi -> API /api/configuracion-ti.
 * Consideraciones: TEC, SUP y ADM mantienen los maestros. El control del agente y las fichas los consulta todo TI y solo los cambia un ADM.
 *   Los registros se activan/inactivan para conservar la integridad histórica.
 */

import { useEffect, useMemo, useState } from 'react'
import { useAutenticacion } from '../autenticacion/AutenticacionContext'
import {
  guardarAreaTI,
  guardarCategoriaTI,
  guardarItemTI,
  guardarLineaTI,
  guardarMatrizTI,
  guardarSlaTI,
  guardarSubTipoTI,
  guardarTipoTI,
  obtenerConfiguracionTI,
  sincronizarCargosCorporativos,
  sincronizarUsuarioCorporativo,
  type ConfiguracionTIRespuesta,
} from '../../services/configuracionTIApi'
import MarcoPortal from '../../components/MarcoPortal'
import IconoBase, { type PropsIcono } from '../../components/Icono'
import CabeceraPanel from './CabeceraPanel'
import ControlAgentePanel from './ControlAgentePanel'
import FichasPanel from './FichasPanel'
import './ConfiguracionTIPage.css'

// Esta pantalla dibuja sus íconos a 19 px.
const Icono = (props: PropsIcono) => <IconoBase size={19} {...props} />

type Seccion = 'CATALOGOS' | 'MATRIZ' | 'USUARIOS' | 'AGENTE' | 'FICHAS'
type Catalogo = 'AREA' | 'LINEA' | 'ITEM' | 'TIPO' | 'CATEGORIA' | 'SUBTIPO'
type FormularioCatalogo = {
  codigo: string
  descripcion: string
  relacion: string
  relacion2: string
  abreviatura: string
  telefono: string
  estado: string
}

const formularioCatalogoVacio: FormularioCatalogo = {
  codigo: '',
  descripcion: '',
  relacion: '',
  relacion2: '',
  abreviatura: '',
  telefono: '',
  estado: 'A',
}

function nombreCatalogo(catalogo: Catalogo) {
  if (catalogo === 'AREA') return 'área'
  if (catalogo === 'LINEA') return 'línea'
  if (catalogo === 'ITEM') return 'ítem'
  if (catalogo === 'TIPO') return 'tipo'
  if (catalogo === 'CATEGORIA') return 'categoría'
  return 'subtipo'
}

export default function ConfiguracionTIPage() {
  const { usuario } = useAutenticacion()
  const [datos, setDatos] = useState<ConfiguracionTIRespuesta | null>(null)
  const [cargando, setCargando] = useState(true)
  const [procesando, setProcesando] = useState(false)
  const [error, setError] = useState('')
  const [mensaje, setMensaje] = useState('')
  const [seccion, setSeccion] = useState<Seccion>('CATALOGOS')
  const [catalogo, setCatalogo] = useState<Catalogo>('AREA')
  const [formulario, setFormulario] = useState<FormularioCatalogo>(formularioCatalogoVacio)
  const [matriz, setMatriz] = useState({ item: '', categoria: '', prioridad: 3, impacto: 3, complejidad: 3, estado: 'A' })
  const [sla, setSla] = useState({ prioridad: 3, slaObjetivoMinutos: 1440, estado: 'A' })
  const [usuarioForm, setUsuarioForm] = useState({ usuario: '', area: '', perfil: 'USR', correo: '', estado: 'A' })

  async function cargar() {
    setCargando(true)
    setError('')
    try {
      const respuesta = await obtenerConfiguracionTI()
      setDatos(respuesta)
      if (!usuarioForm.area && respuesta.areas.length) setUsuarioForm(valor => ({ ...valor, area: respuesta.areas[0].area }))
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No fue posible cargar la configuración TI.')
    } finally {
      setCargando(false)
    }
  }

  useEffect(() => {
    void cargar()
  }, [])

  async function ejecutar(accion: () => Promise<unknown>, exito: string): Promise<boolean> {
    setProcesando(true)
    setError('')
    setMensaje('')
    try {
      await accion()
      setMensaje(exito)
      await cargar()
      return true
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No fue posible completar la operación.')
      return false
    } finally {
      setProcesando(false)
    }
  }

  const filasCatalogo = useMemo(() => {
    if (!datos) return [] as { codigo: string; descripcion: string; relacion: string; estado: string; original: Record<string, unknown> }[]
    if (catalogo === 'AREA')
      return datos.areas.map(x => ({
        codigo: x.area,
        descripcion: x.descripcion,
        relacion: x.telefono,
        estado: x.estado,
        original: x as unknown as Record<string, unknown>,
      }))
    if (catalogo === 'LINEA')
      return datos.lineas.map(x => ({
        codigo: x.linea,
        descripcion: x.descripcion,
        relacion: x.area,
        estado: x.estado,
        original: x as unknown as Record<string, unknown>,
      }))
    if (catalogo === 'ITEM')
      return datos.items.map(x => ({
        codigo: x.item,
        descripcion: x.descripcion,
        relacion: x.linea,
        estado: x.estado,
        original: x as unknown as Record<string, unknown>,
      }))
    if (catalogo === 'TIPO')
      return datos.tipos.map(x => ({
        codigo: x.tipo,
        descripcion: x.descripcion,
        relacion: x.abreviatura,
        estado: x.estado,
        original: x as unknown as Record<string, unknown>,
      }))
    if (catalogo === 'CATEGORIA')
      return datos.categorias.map(x => ({
        codigo: x.categoria,
        descripcion: x.descripcion,
        relacion: x.abreviatura,
        estado: x.estado,
        original: x as unknown as Record<string, unknown>,
      }))
    return datos.subTipos.map(x => ({
      codigo: x.subTipo,
      descripcion: x.descripcion,
      relacion: `${x.tipo} · ${x.categoria}`,
      estado: x.estado,
      original: x as unknown as Record<string, unknown>,
    }))
  }, [catalogo, datos])

  function editarCatalogo(original: Record<string, unknown>) {
    const valor = (campo: string) => String(original[campo] ?? '')
    if (catalogo === 'AREA')
      setFormulario({
        ...formularioCatalogoVacio,
        codigo: valor('area'),
        descripcion: valor('descripcion'),
        telefono: valor('telefono'),
        estado: valor('estado'),
      })
    if (catalogo === 'LINEA')
      setFormulario({
        ...formularioCatalogoVacio,
        codigo: valor('linea'),
        descripcion: valor('descripcion'),
        relacion: valor('area'),
        estado: valor('estado'),
      })
    if (catalogo === 'ITEM')
      setFormulario({
        ...formularioCatalogoVacio,
        codigo: valor('item'),
        descripcion: valor('descripcion'),
        relacion: valor('linea'),
        estado: valor('estado'),
      })
    if (catalogo === 'TIPO')
      setFormulario({
        ...formularioCatalogoVacio,
        codigo: valor('tipo'),
        descripcion: valor('descripcion'),
        abreviatura: valor('abreviatura'),
        estado: valor('estado'),
      })
    if (catalogo === 'CATEGORIA')
      setFormulario({
        ...formularioCatalogoVacio,
        codigo: valor('categoria'),
        descripcion: valor('descripcion'),
        abreviatura: valor('abreviatura'),
        estado: valor('estado'),
      })
    if (catalogo === 'SUBTIPO')
      setFormulario({
        ...formularioCatalogoVacio,
        codigo: valor('subTipo'),
        descripcion: valor('descripcion'),
        relacion: valor('tipo'),
        relacion2: valor('categoria'),
        abreviatura: valor('abreviatura'),
        estado: valor('estado'),
      })
  }

  function guardarCatalogo() {
    const base = { descripcion: formulario.descripcion, estado: formulario.estado }
    if (catalogo === 'AREA') return guardarAreaTI({ area: formulario.codigo, telefono: formulario.telefono, ...base })
    if (catalogo === 'LINEA') return guardarLineaTI({ linea: formulario.codigo, area: formulario.relacion, ...base })
    if (catalogo === 'ITEM') return guardarItemTI({ item: formulario.codigo, linea: formulario.relacion, ...base })
    if (catalogo === 'TIPO') return guardarTipoTI({ tipo: formulario.codigo, abreviatura: formulario.abreviatura, ...base })
    if (catalogo === 'CATEGORIA') return guardarCategoriaTI({ categoria: formulario.codigo, abreviatura: formulario.abreviatura, ...base })
    return guardarSubTipoTI({
      subTipo: formulario.codigo,
      tipo: formulario.relacion,
      categoria: formulario.relacion2,
      abreviatura: formulario.abreviatura,
      ...base,
    })
  }

  async function guardarCatalogoYLimpiar() {
    const guardado = await ejecutar(guardarCatalogo, 'El catálogo fue actualizado.')
    if (guardado) setFormulario(formularioCatalogoVacio)
  }

  if (!usuario) return null

  return (
    <MarcoPortal
      menu="ti"
      activa="configuracion"
      clase="config-ti-page"
      rol={usuario.perfil}
      ayuda={{ icono: 'i', titulo: 'Catálogos operativos', detalle: 'Disponible para todo el equipo TI' }}
      barra={
        <div className="config-ti-contexto-top">
          <span>
            <Icono nombre="configuracion" size={18} />
          </span>
          <div>
            <strong>Maestros TI</strong>
            <small>Catálogos y reglas de clasificación</small>
          </div>
        </div>
      }
    >
      <main className="inicio-contenido config-ti-contenido">
        <section className="inicio-hero config-ti-hero">
          <div className="inicio-hero__contenido">
            <h1>Maestros TI</h1>
            <p className="inicio-hero__resumen">Administra los datos que sostienen la clasificación de los tickets.</p>
            <p className="inicio-hero__detalle">
              Selecciona un maestro, consulta sus registros y crea o modifica la información desde el mismo flujo.
            </p>
          </div>
          <div className="inicio-hero__acciones config-ti-hero__acciones">
            <button type="button" onClick={() => void cargar()} disabled={cargando}>
              <Icono nombre="actualizar" /> {cargando ? 'Actualizando...' : 'Actualizar datos'}
            </button>
          </div>
          <div className="inicio-hero__firma">
            <span>Personas</span>
            <span>que avanzan</span>
            <strong>CALIMOD</strong>
          </div>
        </section>

        {error && (
          <div className="config-ti-alerta config-ti-alerta--error" role="alert">
            <Icono nombre="alerta" />
            <span>{error}</span>
          </div>
        )}
        {mensaje && (
          <div className="config-ti-alerta config-ti-alerta--ok" role="status">
            <Icono nombre="check" />
            <span>{mensaje}</span>
          </div>
        )}

        <div className="config-ti-tabs" role="tablist" aria-label="Secciones de Maestros TI">
          <button type="button" className={seccion === 'CATALOGOS' ? 'activo' : ''} onClick={() => setSeccion('CATALOGOS')}>
            <Icono nombre="catalogos" size={16} />
            Maestros
          </button>
          <button type="button" className={seccion === 'MATRIZ' ? 'activo' : ''} onClick={() => setSeccion('MATRIZ')}>
            <Icono nombre="matriz" size={16} />
            Categoría por ítem y SLA
          </button>
          <button type="button" className={seccion === 'USUARIOS' ? 'activo' : ''} onClick={() => setSeccion('USUARIOS')}>
            <Icono nombre="usuarios" size={16} />
            Usuarios TI
          </button>
          <button type="button" className={seccion === 'AGENTE' ? 'activo' : ''} onClick={() => setSeccion('AGENTE')}>
            <Icono nombre="agente" size={16} />
            Agente y autonomía
          </button>
          <button type="button" className={seccion === 'FICHAS' ? 'activo' : ''} onClick={() => setSeccion('FICHAS')}>
            <Icono nombre="lista" size={16} />
            Fichas de tickets
          </button>
        </div>

        {cargando ? (
          <section className="inicio-estado-carga">
            <span className="inicio-spinner" /> Cargando configuración TI...
          </section>
        ) : (
          datos && (
            <>
              {seccion === 'CATALOGOS' && (
                <>
                  <nav className="config-ti-submodulos" aria-label="Maestros disponibles">
                    {(
                      [
                        ['AREA', 'Áreas'],
                        ['LINEA', 'Líneas'],
                        ['ITEM', 'Ítems'],
                        ['TIPO', 'Tipos'],
                        ['CATEGORIA', 'Categorías'],
                        ['SUBTIPO', 'Subtipos'],
                      ] as [Catalogo, string][]
                    ).map(([valor, etiqueta]) => (
                      <button
                        key={valor}
                        type="button"
                        className={catalogo === valor ? 'activo' : ''}
                        onClick={() => {
                          setCatalogo(valor)
                          setFormulario(formularioCatalogoVacio)
                        }}
                      >
                        {etiqueta}
                        <span>
                          {valor === 'AREA'
                            ? datos.areas.length
                            : valor === 'LINEA'
                              ? datos.lineas.length
                              : valor === 'ITEM'
                                ? datos.items.length
                                : valor === 'TIPO'
                                  ? datos.tipos.length
                                  : valor === 'CATEGORIA'
                                    ? datos.categorias.length
                                    : datos.subTipos.length}
                        </span>
                      </button>
                    ))}
                  </nav>
                  <section className="config-ti-dos-columnas">
                    <article className="config-ti-panel">
                      <CabeceraPanel
                        icono="catalogos"
                        titulo={`${nombreCatalogo(catalogo).charAt(0).toUpperCase()}${nombreCatalogo(catalogo).slice(1)}s`}
                        subtitulo="Selecciona una fila para editarla o utiliza el formulario para registrar una nueva."
                      />
                      <div className="config-ti-panel__cuerpo config-ti-panel__cuerpo--tabla">
                        <div className="config-ti-tabla">
                          <table>
                            <thead>
                              <tr>
                                <th>Código</th>
                                <th>Descripción</th>
                                <th>Relación</th>
                                <th>Estado</th>
                              </tr>
                            </thead>
                            <tbody>
                              {filasCatalogo.map(x => (
                                <tr key={`${catalogo}-${x.codigo}-${x.relacion}`} onClick={() => editarCatalogo(x.original)}>
                                  <td>
                                    <strong>{x.codigo}</strong>
                                  </td>
                                  <td>{x.descripcion}</td>
                                  <td>{x.relacion || '—'}</td>
                                  <td>
                                    <span className={`config-ti-estado config-ti-estado--${x.estado === 'A' ? 'activo' : 'inactivo'}`}>
                                      {x.estado === 'A' ? 'Activo' : 'Inactivo'}
                                    </span>
                                  </td>
                                </tr>
                              ))}
                            </tbody>
                          </table>
                        </div>
                      </div>
                    </article>

                    <article className="config-ti-panel config-ti-panel--formulario">
                      <CabeceraPanel
                        icono="configuracion"
                        titulo={`${formulario.codigo ? 'Editar' : 'Registrar'} ${nombreCatalogo(catalogo)}`}
                        subtitulo="Completa únicamente los datos necesarios para mantener este maestro."
                      />
                      <div className="config-ti-panel__cuerpo">
                        <form
                          className="config-ti-form"
                          onSubmit={e => {
                            e.preventDefault()
                            void guardarCatalogoYLimpiar()
                          }}
                        >
                          <label>
                            Código
                            <input
                              required
                              value={formulario.codigo}
                              disabled={Boolean(formulario.codigo && filasCatalogo.some(x => x.codigo === formulario.codigo))}
                              onChange={e => setFormulario(v => ({ ...v, codigo: e.target.value.toUpperCase() }))}
                            />
                          </label>
                          <label>
                            Descripción
                            <input
                              required
                              value={formulario.descripcion}
                              onChange={e => setFormulario(v => ({ ...v, descripcion: e.target.value }))}
                            />
                          </label>
                          {catalogo === 'AREA' && (
                            <label>
                              Teléfono
                              <input value={formulario.telefono} onChange={e => setFormulario(v => ({ ...v, telefono: e.target.value }))} />
                            </label>
                          )}
                          {catalogo === 'LINEA' && (
                            <label>
                              Área
                              <select
                                required
                                value={formulario.relacion}
                                onChange={e => setFormulario(v => ({ ...v, relacion: e.target.value }))}
                              >
                                <option value="">Seleccionar</option>
                                {datos.areas
                                  .filter(x => x.estado === 'A')
                                  .map(x => (
                                    <option key={x.area} value={x.area}>
                                      {x.descripcion}
                                    </option>
                                  ))}
                              </select>
                            </label>
                          )}
                          {catalogo === 'ITEM' && (
                            <label>
                              Línea
                              <select
                                required
                                value={formulario.relacion}
                                onChange={e => setFormulario(v => ({ ...v, relacion: e.target.value }))}
                              >
                                <option value="">Seleccionar</option>
                                {datos.lineas
                                  .filter(x => x.estado === 'A')
                                  .map(x => (
                                    <option key={x.linea} value={x.linea}>
                                      {x.descripcion}
                                    </option>
                                  ))}
                              </select>
                            </label>
                          )}
                          {catalogo === 'SUBTIPO' && (
                            <>
                              <label>
                                Tipo
                                <select
                                  required
                                  value={formulario.relacion}
                                  onChange={e => setFormulario(v => ({ ...v, relacion: e.target.value }))}
                                >
                                  <option value="">Seleccionar</option>
                                  {datos.tipos
                                    .filter(x => x.estado === 'A')
                                    .map(x => (
                                      <option key={x.tipo} value={x.tipo}>
                                        {x.descripcion}
                                      </option>
                                    ))}
                                </select>
                              </label>
                              <label>
                                Categoría
                                <select
                                  required
                                  value={formulario.relacion2}
                                  onChange={e => setFormulario(v => ({ ...v, relacion2: e.target.value }))}
                                >
                                  <option value="">Seleccionar</option>
                                  {datos.categorias
                                    .filter(x => x.estado === 'A')
                                    .map(x => (
                                      <option key={x.categoria} value={x.categoria}>
                                        {x.descripcion}
                                      </option>
                                    ))}
                                </select>
                              </label>
                            </>
                          )}
                          {['TIPO', 'CATEGORIA', 'SUBTIPO'].includes(catalogo) && (
                            <label>
                              Abreviatura
                              <input
                                value={formulario.abreviatura}
                                onChange={e => setFormulario(v => ({ ...v, abreviatura: e.target.value.toUpperCase() }))}
                              />
                            </label>
                          )}
                          <label>
                            Estado
                            <select value={formulario.estado} onChange={e => setFormulario(v => ({ ...v, estado: e.target.value }))}>
                              <option value="A">Activo</option>
                              <option value="I">Inactivo</option>
                            </select>
                          </label>
                          <div className="config-ti-form__acciones">
                            <button type="button" onClick={() => setFormulario(formularioCatalogoVacio)}>
                              Limpiar
                            </button>
                            <button className="principal" disabled={procesando}>
                              Guardar
                            </button>
                          </div>
                        </form>
                      </div>
                    </article>
                  </section>
                </>
              )}

              {seccion === 'MATRIZ' && (
                <section className="config-ti-dos-columnas">
                  <article className="config-ti-panel">
                    <CabeceraPanel
                      icono="matriz"
                      titulo="Matriz Ítem / Categoría"
                      subtitulo="Define de forma centralizada prioridad, impacto y complejidad."
                    />
                    <div className="config-ti-panel__cuerpo">
                      <p className="config-ti-ayuda">
                        El operador TI no asigna estos valores manualmente. La clasificación se obtiene de esta matriz para mantener
                        criterios uniformes.
                      </p>
                      <form
                        className="config-ti-form"
                        onSubmit={e => {
                          e.preventDefault()
                          void ejecutar(() => guardarMatrizTI(matriz), 'La regla de clasificación fue actualizada.')
                        }}
                      >
                        <label>
                          Ítem
                          <select required value={matriz.item} onChange={e => setMatriz(v => ({ ...v, item: e.target.value }))}>
                            <option value="">Seleccionar</option>
                            {datos.items
                              .filter(x => x.estado === 'A')
                              .map(x => (
                                <option key={x.item} value={x.item}>
                                  {x.descripcion}
                                </option>
                              ))}
                          </select>
                        </label>
                        <label>
                          Categoría
                          <select required value={matriz.categoria} onChange={e => setMatriz(v => ({ ...v, categoria: e.target.value }))}>
                            <option value="">Seleccionar</option>
                            {datos.categorias
                              .filter(x => x.estado === 'A')
                              .map(x => (
                                <option key={x.categoria} value={x.categoria}>
                                  {x.descripcion}
                                </option>
                              ))}
                          </select>
                        </label>
                        {(['prioridad', 'impacto', 'complejidad'] as const).map(campo => (
                          <label key={campo}>
                            {campo.charAt(0).toUpperCase() + campo.slice(1)}
                            <select value={matriz[campo]} onChange={e => setMatriz(v => ({ ...v, [campo]: Number(e.target.value) }))}>
                              {[1, 2, 3, 4, 5].map(n => (
                                <option key={n} value={n}>
                                  {n}
                                </option>
                              ))}
                            </select>
                          </label>
                        ))}
                        <label>
                          Estado
                          <select value={matriz.estado} onChange={e => setMatriz(v => ({ ...v, estado: e.target.value }))}>
                            <option value="A">Activo</option>
                            <option value="I">Inactivo</option>
                          </select>
                        </label>
                        <div className="config-ti-form__acciones">
                          <button className="principal" disabled={procesando}>
                            Guardar regla
                          </button>
                        </div>
                      </form>
                      <div className="config-ti-reglas">
                        {datos.matriz.map(x => (
                          <button
                            type="button"
                            key={`${x.item}-${x.categoria}`}
                            onClick={() =>
                              setMatriz({
                                item: x.item,
                                categoria: x.categoria,
                                prioridad: x.prioridad || 3,
                                impacto: x.impacto || 3,
                                complejidad: x.complejidad || 3,
                                estado: x.estado,
                              })
                            }
                          >
                            <strong>
                              {x.item} / {x.categoria}
                            </strong>
                            <span>
                              P{x.prioridad || '-'} · I{x.impacto || '-'} · C{x.complejidad || '-'} · {x.estado}
                            </span>
                          </button>
                        ))}
                      </div>
                    </div>
                  </article>

                  <article className="config-ti-panel">
                    <CabeceraPanel
                      icono="actualizar"
                      titulo="Objetivos SLA"
                      subtitulo="Configura el tiempo objetivo que corresponde a cada prioridad."
                    />
                    <div className="config-ti-panel__cuerpo">
                      <p className="config-ti-ayuda">
                        El SLA se aplica automáticamente según la prioridad obtenida de la matriz de clasificación.
                      </p>
                      <form
                        className="config-ti-form"
                        onSubmit={e => {
                          e.preventDefault()
                          void ejecutar(() => guardarSlaTI(sla), 'El SLA fue actualizado.')
                        }}
                      >
                        <label>
                          Prioridad
                          <select value={sla.prioridad} onChange={e => setSla(v => ({ ...v, prioridad: Number(e.target.value) }))}>
                            {[1, 2, 3, 4, 5].map(n => (
                              <option key={n} value={n}>
                                {n}
                              </option>
                            ))}
                          </select>
                        </label>
                        <label>
                          Minutos objetivo
                          <input
                            type="number"
                            min="1"
                            required
                            value={sla.slaObjetivoMinutos}
                            onChange={e => setSla(v => ({ ...v, slaObjetivoMinutos: Number(e.target.value) }))}
                          />
                        </label>
                        <label>
                          Estado
                          <select value={sla.estado} onChange={e => setSla(v => ({ ...v, estado: e.target.value }))}>
                            <option value="A">Activo</option>
                            <option value="I">Inactivo</option>
                          </select>
                        </label>
                        <div className="config-ti-form__acciones">
                          <button className="principal" disabled={procesando}>
                            Guardar SLA
                          </button>
                        </div>
                      </form>
                      <div className="config-ti-reglas">
                        {datos.sla.map(x => (
                          <button
                            type="button"
                            key={x.prioridad}
                            onClick={() => setSla({ prioridad: x.prioridad, slaObjetivoMinutos: x.slaObjetivoMinutos, estado: x.estado })}
                          >
                            <strong>Prioridad {x.prioridad}</strong>
                            <span>
                              {x.slaObjetivoMinutos} min · {x.estado === 'A' ? 'Activo' : 'Inactivo'}
                            </span>
                          </button>
                        ))}
                      </div>
                    </div>
                  </article>
                </section>
              )}

              {seccion === 'USUARIOS' && (
                <section className="config-ti-dos-columnas config-ti-dos-columnas--usuarios">
                  <article className="config-ti-panel config-ti-panel--formulario">
                    <CabeceraPanel
                      icono="usuarios"
                      titulo="Usuarios e identidad corporativa"
                      subtitulo="Spring aporta identidad; Gestión TI mantiene área, perfil y habilitación."
                      accion={
                        <button
                          className="config-ti-boton-secundario"
                          type="button"
                          onClick={() =>
                            void ejecutar(async () => {
                              const r = await sincronizarCargosCorporativos()
                              return r
                            }, 'Los cargos corporativos fueron sincronizados.')
                          }
                          disabled={procesando}
                        >
                          <Icono nombre="actualizar" size={16} />
                          Sincronizar cargos
                        </button>
                      }
                    />
                    <div className="config-ti-panel__cuerpo">
                      <form
                        className="config-ti-form"
                        onSubmit={e => {
                          e.preventDefault()
                          void ejecutar(() => sincronizarUsuarioCorporativo(usuarioForm), 'El usuario corporativo fue sincronizado.')
                        }}
                      >
                        <label>
                          Usuario Spring
                          <input
                            required
                            value={usuarioForm.usuario}
                            onChange={e => setUsuarioForm(v => ({ ...v, usuario: e.target.value.toUpperCase() }))}
                          />
                        </label>
                        <label>
                          Área
                          <select required value={usuarioForm.area} onChange={e => setUsuarioForm(v => ({ ...v, area: e.target.value }))}>
                            {datos.areas
                              .filter(x => x.estado === 'A')
                              .map(x => (
                                <option key={x.area} value={x.area}>
                                  {x.descripcion}
                                </option>
                              ))}
                          </select>
                        </label>
                        <label>
                          Perfil
                          <select value={usuarioForm.perfil} onChange={e => setUsuarioForm(v => ({ ...v, perfil: e.target.value }))}>
                            <option value="USR">Usuario</option>
                            <option value="TEC">Operador TI</option>
                            <option value="SUP">Supervisor</option>
                            <option value="ADM">Administrador</option>
                          </select>
                        </label>
                        <label>
                          Correo local opcional
                          <input
                            type="email"
                            value={usuarioForm.correo}
                            onChange={e => setUsuarioForm(v => ({ ...v, correo: e.target.value }))}
                          />
                        </label>
                        <label>
                          Estado
                          <select value={usuarioForm.estado} onChange={e => setUsuarioForm(v => ({ ...v, estado: e.target.value }))}>
                            <option value="A">Activo</option>
                            <option value="I">Inactivo</option>
                          </select>
                        </label>
                        <div className="config-ti-form__acciones">
                          <button className="principal" disabled={procesando}>
                            Sincronizar usuario
                          </button>
                        </div>
                      </form>
                    </div>
                  </article>

                  <article className="config-ti-panel">
                    <CabeceraPanel
                      icono="usuarios"
                      titulo="Usuarios configurados"
                      subtitulo="Consulta el área, perfil y origen de identidad de cada usuario."
                    />
                    <div className="config-ti-panel__cuerpo config-ti-panel__cuerpo--tabla">
                      <div className="config-ti-tabla">
                        <table>
                          <thead>
                            <tr>
                              <th>Usuario</th>
                              <th>Nombre</th>
                              <th>Área</th>
                              <th>Perfil</th>
                              <th>Origen</th>
                            </tr>
                          </thead>
                          <tbody>
                            {datos.usuarios.map(x => (
                              <tr
                                key={x.usuario}
                                onClick={() =>
                                  setUsuarioForm({
                                    usuario: x.usuario,
                                    area: x.area,
                                    perfil: x.perfil,
                                    correo: x.correo,
                                    estado: x.estado,
                                  })
                                }
                              >
                                <td>
                                  <strong>{x.usuario}</strong>
                                </td>
                                <td>{x.nombreCompleto}</td>
                                <td>{x.area}</td>
                                <td>
                                  <span className="config-ti-perfil">{x.perfil}</span>
                                </td>
                                <td>{x.fuenteIdentidad}</td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    </div>
                  </article>
                </section>
              )}

              {seccion === 'AGENTE' && <ControlAgentePanel esAdministrador={usuario.perfil === 'ADM'} />}
              {seccion === 'FICHAS' && <FichasPanel tipos={datos.tipos} esAdministrador={usuario.perfil === 'ADM'} />}
            </>
          )
        )}
      </main>
    </MarcoPortal>
  )
}
