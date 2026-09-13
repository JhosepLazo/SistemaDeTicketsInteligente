/**
 * Archivo: ConfiguracionTIPage.tsx
 * Objetivo: Administrar la configuración funcional indispensable del sistema de incidencias TI.
 * Responsabilidad: Mantener catálogos operativos, matriz Ítem/Categoría, SLA y usuarios corporativos desde una sola vista restringida.
 * Dependencias: AutenticacionContext, configuracionTIService, React Router, InicioPage.css y ConfiguracionTIPage.css.
 * Flujo: /configuracion-ti -> ConfiguracionTIPage -> configuracionTIService -> API /api/configuracion-ti.
 * Consideraciones: Solo SUP/ADM acceden al módulo. Los registros se activan/inactivan; no se eliminan y no se incluyen funciones de IA.
 */

import { useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAutenticacion } from '../features/autenticacion/context/AutenticacionContext'
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
} from '../features/configuracionTI/services/configuracionTIService'
import NotificacionesCampana from '../components/NotificacionesCampana'
import './InicioPage.css'
import './ConfiguracionTIPage.css'

type Seccion = 'CATALOGOS' | 'MATRIZ' | 'USUARIOS'
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
  codigo: '', descripcion: '', relacion: '', relacion2: '', abreviatura: '', telefono: '', estado: 'A',
}

export default function ConfiguracionTIPage() {
  const navigate = useNavigate()
  const { usuario, cerrarSesion } = useAutenticacion()
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

  useEffect(() => { void cargar() }, [])

  async function ejecutar(accion: () => Promise<unknown>, exito: string) {
    setProcesando(true)
    setError('')
    setMensaje('')
    try {
      await accion()
      setMensaje(exito)
      await cargar()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No fue posible completar la operación.')
    } finally {
      setProcesando(false)
    }
  }

  const filasCatalogo = useMemo(() => {
    if (!datos) return [] as { codigo: string; descripcion: string; relacion: string; estado: string; original: Record<string, unknown> }[]
    if (catalogo === 'AREA') return datos.areas.map(x => ({ codigo: x.area, descripcion: x.descripcion, relacion: x.telefono, estado: x.estado, original: x as unknown as Record<string, unknown> }))
    if (catalogo === 'LINEA') return datos.lineas.map(x => ({ codigo: x.linea, descripcion: x.descripcion, relacion: x.area, estado: x.estado, original: x as unknown as Record<string, unknown> }))
    if (catalogo === 'ITEM') return datos.items.map(x => ({ codigo: x.item, descripcion: x.descripcion, relacion: x.linea, estado: x.estado, original: x as unknown as Record<string, unknown> }))
    if (catalogo === 'TIPO') return datos.tipos.map(x => ({ codigo: x.tipo, descripcion: x.descripcion, relacion: x.abreviatura, estado: x.estado, original: x as unknown as Record<string, unknown> }))
    if (catalogo === 'CATEGORIA') return datos.categorias.map(x => ({ codigo: x.categoria, descripcion: x.descripcion, relacion: x.abreviatura, estado: x.estado, original: x as unknown as Record<string, unknown> }))
    return datos.subTipos.map(x => ({ codigo: x.subTipo, descripcion: x.descripcion, relacion: `${x.tipo} · ${x.categoria}`, estado: x.estado, original: x as unknown as Record<string, unknown> }))
  }, [catalogo, datos])

  function editarCatalogo(original: Record<string, unknown>) {
    const valor = (campo: string) => String(original[campo] ?? '')
    if (catalogo === 'AREA') setFormulario({ ...formularioCatalogoVacio, codigo: valor('area'), descripcion: valor('descripcion'), telefono: valor('telefono'), estado: valor('estado') })
    if (catalogo === 'LINEA') setFormulario({ ...formularioCatalogoVacio, codigo: valor('linea'), descripcion: valor('descripcion'), relacion: valor('area'), estado: valor('estado') })
    if (catalogo === 'ITEM') setFormulario({ ...formularioCatalogoVacio, codigo: valor('item'), descripcion: valor('descripcion'), relacion: valor('linea'), estado: valor('estado') })
    if (catalogo === 'TIPO') setFormulario({ ...formularioCatalogoVacio, codigo: valor('tipo'), descripcion: valor('descripcion'), abreviatura: valor('abreviatura'), estado: valor('estado') })
    if (catalogo === 'CATEGORIA') setFormulario({ ...formularioCatalogoVacio, codigo: valor('categoria'), descripcion: valor('descripcion'), abreviatura: valor('abreviatura'), estado: valor('estado') })
    if (catalogo === 'SUBTIPO') setFormulario({ ...formularioCatalogoVacio, codigo: valor('subTipo'), descripcion: valor('descripcion'), relacion: valor('tipo'), relacion2: valor('categoria'), abreviatura: valor('abreviatura'), estado: valor('estado') })
  }

  function guardarCatalogo() {
    const base = { descripcion: formulario.descripcion, estado: formulario.estado }
    if (catalogo === 'AREA') return guardarAreaTI({ area: formulario.codigo, telefono: formulario.telefono, ...base })
    if (catalogo === 'LINEA') return guardarLineaTI({ linea: formulario.codigo, area: formulario.relacion, ...base })
    if (catalogo === 'ITEM') return guardarItemTI({ item: formulario.codigo, linea: formulario.relacion, ...base })
    if (catalogo === 'TIPO') return guardarTipoTI({ tipo: formulario.codigo, abreviatura: formulario.abreviatura, ...base })
    if (catalogo === 'CATEGORIA') return guardarCategoriaTI({ categoria: formulario.codigo, abreviatura: formulario.abreviatura, ...base })
    return guardarSubTipoTI({ subTipo: formulario.codigo, tipo: formulario.relacion, categoria: formulario.relacion2, abreviatura: formulario.abreviatura, ...base })
  }

  if (!usuario) return null

  return <div className="inicio-shell config-ti-page">
    <aside className="inicio-sidebar" aria-label="Navegación principal">
      <div className="inicio-marca"><span className="inicio-marca__nombre">CALIMOD</span><span className="inicio-marca__sistema">Sistema inteligente<br />de incidencias TI</span></div>
      <nav className="inicio-menu">
        <button className="inicio-menu__item" type="button" onClick={() => navigate('/inicio')}>⌂ <span>Inicio</span></button>
        <button className="inicio-menu__item" type="button" onClick={() => navigate('/gestion-tickets')}>▤ <span>Gestión de Tickets</span></button>
        <button className="inicio-menu__item" type="button" onClick={() => navigate('/base-conocimiento')}>▣ <span>Base de Conocimiento</span></button>
        <button className="inicio-menu__item" type="button" onClick={() => navigate('/reportes')}>▥ <span>Reportes</span></button>
        <button className="inicio-menu__item inicio-menu__item--activo" type="button" aria-current="page">⚙ <span>Configuración TI</span></button>
      </nav>
      <div className="inicio-sidebar__pie"><small>Acceso restringido</small><strong>SUP / ADM</strong></div>
    </aside>

    <section className="inicio-principal">
      <header className="inicio-topbar">
        <div className="config-ti-titulo-top"><strong>Configuración TI</strong><span>Catálogos y reglas operativas</span></div>
        <div className="inicio-topbar__usuario"><NotificacionesCampana /><div className="inicio-avatar">{usuario.nombreCompleto.charAt(0).toUpperCase()}</div><div className="inicio-identidad"><strong>{usuario.nombreCompleto.split(' ')[0]}</strong><span>{usuario.perfil}</span></div><button className="inicio-salir" type="button" onClick={() => void cerrarSesion().then(() => navigate('/login', { replace: true }))}>Salir</button></div>
      </header>

      <main className="inicio-contenido config-ti-contenido">
        <section className="config-ti-hero"><div><span>Administración funcional</span><h1>Configuración TI</h1><p>Centraliza únicamente la configuración que sostiene la clasificación, atención, SLA e identidad de los usuarios del sistema.</p></div><button type="button" onClick={() => void cargar()} disabled={cargando}>Actualizar</button></section>
        {error && <div className="config-ti-alerta error" role="alert">{error}</div>}
        {mensaje && <div className="config-ti-alerta ok" role="status">{mensaje}</div>}

        <div className="config-ti-tabs" role="tablist">
          <button type="button" className={seccion === 'CATALOGOS' ? 'activo' : ''} onClick={() => setSeccion('CATALOGOS')}>Catálogos</button>
          <button type="button" className={seccion === 'MATRIZ' ? 'activo' : ''} onClick={() => setSeccion('MATRIZ')}>Matriz y SLA</button>
          <button type="button" className={seccion === 'USUARIOS' ? 'activo' : ''} onClick={() => setSeccion('USUARIOS')}>Usuarios</button>
        </div>

        {cargando ? <div className="config-ti-panel">Cargando configuración...</div> : datos && <>
          {seccion === 'CATALOGOS' && <section className="config-ti-dos-columnas">
            <article className="config-ti-panel">
              <header><div><h2>Maestros operativos</h2><p>Áreas, líneas, ítems, tipos, categorías y subtipos en un solo mantenimiento.</p></div><select value={catalogo} onChange={e => { setCatalogo(e.target.value as Catalogo); setFormulario(formularioCatalogoVacio) }}><option value="AREA">Áreas</option><option value="LINEA">Líneas</option><option value="ITEM">Ítems</option><option value="TIPO">Tipos</option><option value="CATEGORIA">Categorías</option><option value="SUBTIPO">Subtipos</option></select></header>
              <div className="config-ti-tabla"><table><thead><tr><th>Código</th><th>Descripción</th><th>Relación</th><th>Estado</th></tr></thead><tbody>{filasCatalogo.map(x => <tr key={`${catalogo}-${x.codigo}-${x.relacion}`} onClick={() => editarCatalogo(x.original)}><td><strong>{x.codigo}</strong></td><td>{x.descripcion}</td><td>{x.relacion || '—'}</td><td>{x.estado === 'A' ? 'Activo' : 'Inactivo'}</td></tr>)}</tbody></table></div>
            </article>

            <article className="config-ti-panel">
              <h2>{formulario.codigo ? 'Editar' : 'Registrar'} {catalogo.toLowerCase()}</h2>
              <form className="config-ti-form" onSubmit={e => { e.preventDefault(); void ejecutar(guardarCatalogo, 'El catálogo fue actualizado.').then(() => setFormulario(formularioCatalogoVacio)) }}>
                <label>Código<input required value={formulario.codigo} disabled={Boolean(formulario.codigo && filasCatalogo.some(x => x.codigo === formulario.codigo))} onChange={e => setFormulario(v => ({ ...v, codigo: e.target.value.toUpperCase() }))} /></label>
                <label>Descripción<input required value={formulario.descripcion} onChange={e => setFormulario(v => ({ ...v, descripcion: e.target.value }))} /></label>
                {catalogo === 'AREA' && <label>Teléfono<input value={formulario.telefono} onChange={e => setFormulario(v => ({ ...v, telefono: e.target.value }))} /></label>}
                {catalogo === 'LINEA' && <label>Área<select required value={formulario.relacion} onChange={e => setFormulario(v => ({ ...v, relacion: e.target.value }))}><option value="">Seleccionar</option>{datos.areas.filter(x => x.estado === 'A').map(x => <option key={x.area} value={x.area}>{x.descripcion}</option>)}</select></label>}
                {catalogo === 'ITEM' && <label>Línea<select required value={formulario.relacion} onChange={e => setFormulario(v => ({ ...v, relacion: e.target.value }))}><option value="">Seleccionar</option>{datos.lineas.filter(x => x.estado === 'A').map(x => <option key={x.linea} value={x.linea}>{x.descripcion}</option>)}</select></label>}
                {catalogo === 'SUBTIPO' && <><label>Tipo<select required value={formulario.relacion} onChange={e => setFormulario(v => ({ ...v, relacion: e.target.value }))}><option value="">Seleccionar</option>{datos.tipos.filter(x => x.estado === 'A').map(x => <option key={x.tipo} value={x.tipo}>{x.descripcion}</option>)}</select></label><label>Categoría<select required value={formulario.relacion2} onChange={e => setFormulario(v => ({ ...v, relacion2: e.target.value }))}><option value="">Seleccionar</option>{datos.categorias.filter(x => x.estado === 'A').map(x => <option key={x.categoria} value={x.categoria}>{x.descripcion}</option>)}</select></label></>}
                {['TIPO', 'CATEGORIA', 'SUBTIPO'].includes(catalogo) && <label>Abreviatura<input value={formulario.abreviatura} onChange={e => setFormulario(v => ({ ...v, abreviatura: e.target.value.toUpperCase() }))} /></label>}
                <label>Estado<select value={formulario.estado} onChange={e => setFormulario(v => ({ ...v, estado: e.target.value }))}><option value="A">Activo</option><option value="I">Inactivo</option></select></label>
                <div className="config-ti-form__acciones"><button type="button" onClick={() => setFormulario(formularioCatalogoVacio)}>Limpiar</button><button className="principal" disabled={procesando}>Guardar</button></div>
              </form>
            </article>
          </section>}

          {seccion === 'MATRIZ' && <section className="config-ti-dos-columnas">
            <article className="config-ti-panel"><h2>Matriz Ítem / Categoría</h2><p className="config-ti-ayuda">Define prioridad, impacto y complejidad de forma centralizada. El operador TI no debe asignar estos valores manualmente.</p><form className="config-ti-form" onSubmit={e => { e.preventDefault(); void ejecutar(() => guardarMatrizTI(matriz), 'La regla de clasificación fue actualizada.') }}><label>Ítem<select required value={matriz.item} onChange={e => setMatriz(v => ({ ...v, item: e.target.value }))}><option value="">Seleccionar</option>{datos.items.filter(x => x.estado === 'A').map(x => <option key={x.item} value={x.item}>{x.descripcion}</option>)}</select></label><label>Categoría<select required value={matriz.categoria} onChange={e => setMatriz(v => ({ ...v, categoria: e.target.value }))}><option value="">Seleccionar</option>{datos.categorias.filter(x => x.estado === 'A').map(x => <option key={x.categoria} value={x.categoria}>{x.descripcion}</option>)}</select></label>{(['prioridad', 'impacto', 'complejidad'] as const).map(campo => <label key={campo}>{campo.charAt(0).toUpperCase() + campo.slice(1)}<select value={matriz[campo]} onChange={e => setMatriz(v => ({ ...v, [campo]: Number(e.target.value) }))}>{[1, 2, 3, 4, 5].map(n => <option key={n} value={n}>{n}</option>)}</select></label>)}<label>Estado<select value={matriz.estado} onChange={e => setMatriz(v => ({ ...v, estado: e.target.value }))}><option value="A">Activo</option><option value="I">Inactivo</option></select></label><button className="principal" disabled={procesando}>Guardar regla</button></form><div className="config-ti-reglas">{datos.matriz.map(x => <button type="button" key={`${x.item}-${x.categoria}`} onClick={() => setMatriz({ item: x.item, categoria: x.categoria, prioridad: x.prioridad || 3, impacto: x.impacto || 3, complejidad: x.complejidad || 3, estado: x.estado })}><strong>{x.item} / {x.categoria}</strong><span>P{x.prioridad || '-'} · I{x.impacto || '-'} · C{x.complejidad || '-'} · {x.estado}</span></button>)}</div></article>
            <article className="config-ti-panel"><h2>Objetivos SLA</h2><p className="config-ti-ayuda">El SLA se aplica automáticamente según la prioridad obtenida de la matriz.</p><form className="config-ti-form" onSubmit={e => { e.preventDefault(); void ejecutar(() => guardarSlaTI(sla), 'El SLA fue actualizado.') }}><label>Prioridad<select value={sla.prioridad} onChange={e => setSla(v => ({ ...v, prioridad: Number(e.target.value) }))}>{[1, 2, 3, 4, 5].map(n => <option key={n} value={n}>{n}</option>)}</select></label><label>Minutos objetivo<input type="number" min="1" required value={sla.slaObjetivoMinutos} onChange={e => setSla(v => ({ ...v, slaObjetivoMinutos: Number(e.target.value) }))} /></label><label>Estado<select value={sla.estado} onChange={e => setSla(v => ({ ...v, estado: e.target.value }))}><option value="A">Activo</option><option value="I">Inactivo</option></select></label><button className="principal" disabled={procesando}>Guardar SLA</button></form><div className="config-ti-reglas">{datos.sla.map(x => <button type="button" key={x.prioridad} onClick={() => setSla({ prioridad: x.prioridad, slaObjetivoMinutos: x.slaObjetivoMinutos, estado: x.estado })}><strong>Prioridad {x.prioridad}</strong><span>{x.slaObjetivoMinutos} min · {x.estado}</span></button>)}</div></article>
          </section>}

          {seccion === 'USUARIOS' && <section className="config-ti-dos-columnas">
            <article className="config-ti-panel"><header><div><h2>Usuarios e identidad corporativa</h2><p>Spring aporta identidad; este sistema conserva área, perfil y habilitación funcional.</p></div><button type="button" onClick={() => void ejecutar(async () => { const r = await sincronizarCargosCorporativos(); return r }, 'Los cargos corporativos fueron sincronizados.')} disabled={procesando}>Sincronizar cargos</button></header><form className="config-ti-form" onSubmit={e => { e.preventDefault(); void ejecutar(() => sincronizarUsuarioCorporativo(usuarioForm), 'El usuario corporativo fue sincronizado.') }}><label>Usuario Spring<input required value={usuarioForm.usuario} onChange={e => setUsuarioForm(v => ({ ...v, usuario: e.target.value.toUpperCase() }))} /></label><label>Área<select required value={usuarioForm.area} onChange={e => setUsuarioForm(v => ({ ...v, area: e.target.value }))}>{datos.areas.filter(x => x.estado === 'A').map(x => <option key={x.area} value={x.area}>{x.descripcion}</option>)}</select></label><label>Perfil<select value={usuarioForm.perfil} onChange={e => setUsuarioForm(v => ({ ...v, perfil: e.target.value }))}><option value="USR">Usuario</option><option value="TEC">Operador TI</option><option value="SUP">Supervisor</option><option value="ADM">Administrador</option></select></label><label>Correo local opcional<input type="email" value={usuarioForm.correo} onChange={e => setUsuarioForm(v => ({ ...v, correo: e.target.value }))} /></label><label>Estado<select value={usuarioForm.estado} onChange={e => setUsuarioForm(v => ({ ...v, estado: e.target.value }))}><option value="A">Activo</option><option value="I">Inactivo</option></select></label><button className="principal" disabled={procesando}>Sincronizar usuario</button></form></article>
            <article className="config-ti-panel"><h2>Usuarios configurados</h2><div className="config-ti-tabla"><table><thead><tr><th>Usuario</th><th>Nombre</th><th>Área</th><th>Perfil</th><th>Origen</th></tr></thead><tbody>{datos.usuarios.map(x => <tr key={x.usuario} onClick={() => setUsuarioForm({ usuario: x.usuario, area: x.area, perfil: x.perfil, correo: x.correo, estado: x.estado })}><td><strong>{x.usuario}</strong></td><td>{x.nombreCompleto}</td><td>{x.area}</td><td>{x.perfil}</td><td>{x.fuenteIdentidad}</td></tr>)}</tbody></table></div></article>
          </section>}
        </>}
      </main>
    </section>
  </div>
}
