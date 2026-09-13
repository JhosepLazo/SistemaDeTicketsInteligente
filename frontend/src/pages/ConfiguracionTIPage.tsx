/**
 * Archivo: ConfiguracionTIPage.tsx
 * Objetivo: Implementar la administración funcional mínima requerida para mantener el sistema sin depender de cambios directos en base de datos.
 * Responsabilidad: Gestionar catálogos, matriz de clasificación, SLA, usuarios corporativos, formatos y publicación de conocimiento desde una sola vista restringida.
 * Dependencias: AutenticacionContext, configuracionTIService, React Router, InicioPage.css y ConfiguracionTIPage.css.
 * Flujo: /configuracion-ti -> ConfiguracionTIPage -> configuracionTIService -> API /api/configuracion-ti.
 * Consideraciones: Solo SUP/ADM acceden al módulo. Los registros se activan/inactivan; no se eliminan y no existe ninguna función de IA en esta pantalla.
 */

import { useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAutenticacion } from '../features/autenticacion/context/AutenticacionContext'
import {
  actualizarVisibilidadConocimiento,
  guardarAreaTI, guardarCategoriaTI, guardarFormatoSoporte, guardarItemTI, guardarLineaTI, guardarMatrizTI,
  guardarSlaTI, guardarSubTipoTI, guardarTipoTI, obtenerConfiguracionTI, sincronizarCargosCorporativos,
  sincronizarUsuarioCorporativo, type ConfiguracionTIRespuesta,
} from '../features/configuracionTI/services/configuracionTIService'
import NotificacionesCampana from '../components/NotificacionesCampana'
import './InicioPage.css'
import './ConfiguracionTIPage.css'

type Seccion = 'CATALOGOS' | 'MATRIZ' | 'USUARIOS' | 'RECURSOS'
type Catalogo = 'AREA' | 'LINEA' | 'ITEM' | 'TIPO' | 'CATEGORIA' | 'SUBTIPO'

const vacioCatalogo = { codigo:'', descripcion:'', relacion:'', relacion2:'', abreviatura:'', telefono:'', estado:'A' }

export default function ConfiguracionTIPage(){
  const navigate=useNavigate(); const {usuario,finalizarSesion}=useAutenticacion()
  const [datos,setDatos]=useState<ConfiguracionTIRespuesta|null>(null); const [cargando,setCargando]=useState(true); const [procesando,setProcesando]=useState(false)
  const [error,setError]=useState(''); const [mensaje,setMensaje]=useState(''); const [seccion,setSeccion]=useState<Seccion>('CATALOGOS'); const [catalogo,setCatalogo]=useState<Catalogo>('AREA')
  const [form,setForm]=useState(vacioCatalogo); const [matriz,setMatriz]=useState({item:'',categoria:'',prioridad:3,impacto:3,complejidad:3,estado:'A'}); const [sla,setSla]=useState({prioridad:3,slaObjetivoMinutos:1440,estado:'A'})
  const [usuarioForm,setUsuarioForm]=useState({usuario:'',area:'',perfil:'USR',correo:'',estado:'A'}); const [formato,setFormato]=useState({codigo:'',titulo:'',descripcion:'',tipoTicket:'',estado:'A',archivo:null as File|null})

  async function cargar(){setCargando(true);setError('');try{const d=await obtenerConfiguracionTI();setDatos(d);if(!usuarioForm.area&&d.areas.length)setUsuarioForm(v=>({...v,area:d.areas[0].area}))}catch(e){setError(e instanceof Error?e.message:'No fue posible cargar la configuración.')}finally{setCargando(false)}}
  useEffect(()=>{void cargar()},[])

  async function ejecutar(accion:()=>Promise<unknown>,exito:string){setProcesando(true);setError('');setMensaje('');try{await accion();setMensaje(exito);await cargar()}catch(e){setError(e instanceof Error?e.message:'No fue posible completar la operación.')}finally{setProcesando(false)}}

  const filasCatalogo=useMemo(()=>{
    if(!datos)return [] as {codigo:string;descripcion:string;relacion:string;estado:string;original:unknown}[]
    if(catalogo==='AREA')return datos.areas.map(x=>({codigo:x.area,descripcion:x.descripcion,relacion:x.telefono,estado:x.estado,original:x}))
    if(catalogo==='LINEA')return datos.lineas.map(x=>({codigo:x.linea,descripcion:x.descripcion,relacion:x.area,estado:x.estado,original:x}))
    if(catalogo==='ITEM')return datos.items.map(x=>({codigo:x.item,descripcion:x.descripcion,relacion:x.linea,estado:x.estado,original:x}))
    if(catalogo==='TIPO')return datos.tipos.map(x=>({codigo:x.tipo,descripcion:x.descripcion,relacion:x.abreviatura,estado:x.estado,original:x}))
    if(catalogo==='CATEGORIA')return datos.categorias.map(x=>({codigo:x.categoria,descripcion:x.descripcion,relacion:x.abreviatura,estado:x.estado,original:x}))
    return datos.subTipos.map(x=>({codigo:x.subTipo,descripcion:x.descripcion,relacion:`${x.tipo} · ${x.categoria}`,estado:x.estado,original:x}))
  },[catalogo,datos])

  function editarCatalogo(original:any){
    if(catalogo==='AREA')setForm({codigo:original.area,descripcion:original.descripcion,relacion:'',relacion2:'',abreviatura:'',telefono:original.telefono||'',estado:original.estado})
    if(catalogo==='LINEA')setForm({codigo:original.linea,descripcion:original.descripcion,relacion:original.area,relacion2:'',abreviatura:'',telefono:'',estado:original.estado})
    if(catalogo==='ITEM')setForm({codigo:original.item,descripcion:original.descripcion,relacion:original.linea,relacion2:'',abreviatura:'',telefono:'',estado:original.estado})
    if(catalogo==='TIPO')setForm({codigo:original.tipo,descripcion:original.descripcion,relacion:'',relacion2:'',abreviatura:original.abreviatura||'',telefono:'',estado:original.estado})
    if(catalogo==='CATEGORIA')setForm({codigo:original.categoria,descripcion:original.descripcion,relacion:'',relacion2:'',abreviatura:original.abreviatura||'',telefono:'',estado:original.estado})
    if(catalogo==='SUBTIPO')setForm({codigo:original.subTipo,descripcion:original.descripcion,relacion:original.tipo,relacion2:original.categoria,abreviatura:original.abreviatura||'',telefono:'',estado:original.estado})
  }

  function guardarCatalogo(){
    const base={descripcion:form.descripcion,estado:form.estado}
    if(catalogo==='AREA')return guardarAreaTI({area:form.codigo,telefono:form.telefono,...base})
    if(catalogo==='LINEA')return guardarLineaTI({linea:form.codigo,area:form.relacion,...base})
    if(catalogo==='ITEM')return guardarItemTI({item:form.codigo,linea:form.relacion,...base})
    if(catalogo==='TIPO')return guardarTipoTI({tipo:form.codigo,abreviatura:form.abreviatura,...base})
    if(catalogo==='CATEGORIA')return guardarCategoriaTI({categoria:form.codigo,abreviatura:form.abreviatura,...base})
    return guardarSubTipoTI({subTipo:form.codigo,tipo:form.relacion,categoria:form.relacion2,abreviatura:form.abreviatura,...base})
  }

  if(!usuario)return null
  return <div className="inicio-shell config-ti-page">
    <aside className="inicio-sidebar">
      <div className="inicio-marca"><span className="inicio-marca__nombre">CALIMOD</span><span className="inicio-marca__sistema">Sistema inteligente<br/>de incidencias TI</span></div>
      <nav className="inicio-menu">
        <button className="inicio-menu__item" onClick={()=>navigate('/inicio')}>⌂ <span>Inicio</span></button>
        <button className="inicio-menu__item" onClick={()=>navigate('/gestion-tickets')}>▤ <span>Gestión de Tickets</span></button>
        <button className="inicio-menu__item" onClick={()=>navigate('/base-conocimiento')}>▣ <span>Base de Conocimiento</span></button>
        <button className="inicio-menu__item" onClick={()=>navigate('/reportes')}>▥ <span>Reportes</span></button>
        <button className="inicio-menu__item inicio-menu__item--activo">⚙ <span>Configuración TI</span></button>
      </nav>
      <div className="inicio-sidebar__pie"><small>Configuración restringida</small><strong>SUP / ADM</strong></div>
    </aside>
    <section className="inicio-principal">
      <header className="inicio-topbar"><div className="config-ti-titulo-top"><strong>Configuración TI</strong><span>Catálogos y reglas operativas</span></div><div className="inicio-topbar__usuario"><NotificacionesCampana/><div className="inicio-avatar">{usuario.nombreCompleto.charAt(0)}</div><div className="inicio-identidad"><strong>{usuario.nombreCompleto.split(' ')[0]}</strong><span>{usuario.perfil}</span></div><button className="inicio-salir" onClick={()=>void finalizarSesion().then(()=>navigate('/login',{replace:true}))}>Salir</button></div></header>
      <main className="inicio-contenido config-ti-contenido">
        <section className="config-ti-hero"><div><span>Administración funcional</span><h1>Configuración TI</h1><p>Mantén únicamente los datos que sostienen clasificación, atención, SLA e identidad empresarial.</p></div><button onClick={()=>void cargar()} disabled={cargando}>Actualizar</button></section>
        {error&&<div className="config-ti-alerta error">{error}</div>}{mensaje&&<div className="config-ti-alerta ok">{mensaje}</div>}
        <div className="config-ti-tabs">{([['CATALOGOS','Catálogos'],['MATRIZ','Matriz y SLA'],['USUARIOS','Usuarios'],['RECURSOS','Recursos']] as [Seccion,string][]).map(x=><button key={x[0]} className={seccion===x[0]?'activo':''} onClick={()=>setSeccion(x[0])}>{x[1]}</button>)}</div>

        {cargando?<div className="config-ti-panel">Cargando configuración...</div>:datos&&<>
          {seccion==='CATALOGOS'&&<section className="config-ti-dos-columnas">
            <article className="config-ti-panel"><header><div><h2>Maestros operativos</h2><p>Inactiva registros cuando ya no deban usarse; no se elimina historial.</p></div><select value={catalogo} onChange={e=>{setCatalogo(e.target.value as Catalogo);setForm(vacioCatalogo)}}><option value="AREA">Áreas</option><option value="LINEA">Líneas</option><option value="ITEM">Ítems</option><option value="TIPO">Tipos</option><option value="CATEGORIA">Categorías</option><option value="SUBTIPO">Subtipos</option></select></header>
              <div className="config-ti-tabla"><table><thead><tr><th>Código</th><th>Descripción</th><th>Relación</th><th>Estado</th></tr></thead><tbody>{filasCatalogo.map(x=><tr key={`${catalogo}-${x.codigo}-${x.relacion}`} onClick={()=>editarCatalogo(x.original)}><td><strong>{x.codigo}</strong></td><td>{x.descripcion}</td><td>{x.relacion||'—'}</td><td>{x.estado==='A'?'Activo':'Inactivo'}</td></tr>)}</tbody></table></div>
            </article>
            <article className="config-ti-panel"><h2>{form.codigo?'Editar':'Registrar'} {catalogo.toLowerCase()}</h2><form onSubmit={e=>{e.preventDefault();void ejecutar(guardarCatalogo,'El catálogo fue actualizado.').then(()=>setForm(vacioCatalogo))}} className="config-ti-form">
              <label>Código<input required value={form.codigo} disabled={Boolean(form.codigo&&filasCatalogo.some(x=>x.codigo===form.codigo))} onChange={e=>setForm(v=>({...v,codigo:e.target.value.toUpperCase()}))}/></label>
              <label>Descripción<input required value={form.descripcion} onChange={e=>setForm(v=>({...v,descripcion:e.target.value}))}/></label>
              {catalogo==='AREA'&&<label>Teléfono<input value={form.telefono} onChange={e=>setForm(v=>({...v,telefono:e.target.value}))}/></label>}
              {catalogo==='LINEA'&&<label>Área<select required value={form.relacion} onChange={e=>setForm(v=>({...v,relacion:e.target.value}))}><option value="">Seleccionar</option>{datos.areas.filter(x=>x.estado==='A').map(x=><option key={x.area} value={x.area}>{x.descripcion}</option>)}</select></label>}
              {catalogo==='ITEM'&&<label>Línea<select required value={form.relacion} onChange={e=>setForm(v=>({...v,relacion:e.target.value}))}><option value="">Seleccionar</option>{datos.lineas.filter(x=>x.estado==='A').map(x=><option key={x.linea} value={x.linea}>{x.descripcion}</option>)}</select></label>}
              {catalogo==='SUBTIPO'&&<><label>Tipo<select required value={form.relacion} onChange={e=>setForm(v=>({...v,relacion:e.target.value}))}><option value="">Seleccionar</option>{datos.tipos.filter(x=>x.estado==='A').map(x=><option key={x.tipo} value={x.tipo}>{x.descripcion}</option>)}</select></label><label>Categoría<select required value={form.relacion2} onChange={e=>setForm(v=>({...v,relacion2:e.target.value}))}><option value="">Seleccionar</option>{datos.categorias.filter(x=>x.estado==='A').map(x=><option key={x.categoria} value={x.categoria}>{x.descripcion}</option>)}</select></label></>}
              {['TIPO','CATEGORIA','SUBTIPO'].includes(catalogo)&&<label>Abreviatura<input value={form.abreviatura} onChange={e=>setForm(v=>({...v,abreviatura:e.target.value.toUpperCase()}))}/></label>}
              <label>Estado<select value={form.estado} onChange={e=>setForm(v=>({...v,estado:e.target.value}))}><option value="A">Activo</option><option value="I">Inactivo</option></select></label>
              <div className="config-ti-form__acciones"><button type="button" onClick={()=>setForm(vacioCatalogo)}>Limpiar</button><button className="principal" disabled={procesando}>Guardar</button></div>
            </form></article>
          </section>}

          {seccion==='MATRIZ'&&<section className="config-ti-dos-columnas">
            <article className="config-ti-panel"><h2>Matriz Ítem / Categoría</h2><p className="config-ti-ayuda">La prioridad, impacto y complejidad de Gestión de Tickets salen de esta matriz. El operador ya no debe decidirlos manualmente.</p><form className="config-ti-form" onSubmit={e=>{e.preventDefault();void ejecutar(()=>guardarMatrizTI(matriz),'La regla de clasificación fue actualizada.')}}><label>Ítem<select required value={matriz.item} onChange={e=>setMatriz(v=>({...v,item:e.target.value}))}><option value="">Seleccionar</option>{datos.items.filter(x=>x.estado==='A').map(x=><option key={x.item} value={x.item}>{x.descripcion}</option>)}</select></label><label>Categoría<select required value={matriz.categoria} onChange={e=>setMatriz(v=>({...v,categoria:e.target.value}))}><option value="">Seleccionar</option>{datos.categorias.filter(x=>x.estado==='A').map(x=><option key={x.categoria} value={x.categoria}>{x.descripcion}</option>)}</select></label>{(['prioridad','impacto','complejidad'] as const).map(c=><label key={c}>{c.charAt(0).toUpperCase()+c.slice(1)}<select value={matriz[c]} onChange={e=>setMatriz(v=>({...v,[c]:Number(e.target.value)}))}>{[1,2,3,4,5].map(n=><option key={n} value={n}>{n}</option>)}</select></label>)}<label>Estado<select value={matriz.estado} onChange={e=>setMatriz(v=>({...v,estado:e.target.value}))}><option value="A">Activo</option><option value="I">Inactivo</option></select></label><button className="principal" disabled={procesando}>Guardar regla</button></form><div className="config-ti-reglas">{datos.matriz.map(x=><button key={`${x.item}-${x.categoria}`} onClick={()=>setMatriz({item:x.item,categoria:x.categoria,prioridad:x.prioridad||3,impacto:x.impacto||3,complejidad:x.complejidad||3,estado:x.estado})}><strong>{x.item} / {x.categoria}</strong><span>P{x.prioridad||'-'} · I{x.impacto||'-'} · C{x.complejidad||'-'} · {x.estado}</span></button>)}</div></article>
            <article className="config-ti-panel"><h2>Objetivos SLA</h2><p className="config-ti-ayuda">Se aplican automáticamente al clasificar según prioridad.</p><form className="config-ti-form" onSubmit={e=>{e.preventDefault();void ejecutar(()=>guardarSlaTI(sla),'El SLA fue actualizado.')}}><label>Prioridad<select value={sla.prioridad} onChange={e=>setSla(v=>({...v,prioridad:Number(e.target.value)}))}>{[1,2,3,4,5].map(n=><option key={n}>{n}</option>)}</select></label><label>Minutos objetivo<input type="number" min="1" required value={sla.slaObjetivoMinutos} onChange={e=>setSla(v=>({...v,slaObjetivoMinutos:Number(e.target.value)}))}/></label><label>Estado<select value={sla.estado} onChange={e=>setSla(v=>({...v,estado:e.target.value}))}><option value="A">Activo</option><option value="I">Inactivo</option></select></label><button className="principal" disabled={procesando}>Guardar SLA</button></form><div className="config-ti-reglas">{datos.sla.map(x=><button key={x.prioridad} onClick={()=>setSla({prioridad:x.prioridad,slaObjetivoMinutos:x.slaObjetivoMinutos,estado:x.estado})}><strong>Prioridad {x.prioridad}</strong><span>{x.slaObjetivoMinutos} min · {x.estado}</span></button>)}</div></article>
          </section>}

          {seccion==='USUARIOS'&&<section className="config-ti-dos-columnas"><article className="config-ti-panel"><header><div><h2>Identidad corporativa</h2><p>Spring aporta identidad; Gestión TI mantiene área y perfil.</p></div><button onClick={()=>void ejecutar(async()=>{const r=await sincronizarCargosCorporativos();setMensaje(`${r.procesados} cargos sincronizados.`)},'Cargos sincronizados.')} disabled={procesando}>Sincronizar cargos</button></header><form className="config-ti-form" onSubmit={e=>{e.preventDefault();void ejecutar(()=>sincronizarUsuarioCorporativo(usuarioForm),'El usuario corporativo fue sincronizado.')}}><label>Usuario Spring<input required value={usuarioForm.usuario} onChange={e=>setUsuarioForm(v=>({...v,usuario:e.target.value.toUpperCase()}))}/></label><label>Área<select required value={usuarioForm.area} onChange={e=>setUsuarioForm(v=>({...v,area:e.target.value}))}>{datos.areas.filter(x=>x.estado==='A').map(x=><option key={x.area} value={x.area}>{x.descripcion}</option>)}</select></label><label>Perfil<select value={usuarioForm.perfil} onChange={e=>setUsuarioForm(v=>({...v,perfil:e.target.value}))}><option value="USR">Usuario</option><option value="TEC">Operador TI</option><option value="SUP">Supervisor</option><option value="ADM">Administrador</option></select></label><label>Correo local opcional<input type="email" value={usuarioForm.correo} onChange={e=>setUsuarioForm(v=>({...v,correo:e.target.value}))}/></label><label>Estado<select value={usuarioForm.estado} onChange={e=>setUsuarioForm(v=>({...v,estado:e.target.value}))}><option value="A">Activo</option><option value="I">Inactivo</option></select></label><button className="principal" disabled={procesando}>Sincronizar usuario</button></form></article><article className="config-ti-panel"><h2>Usuarios configurados</h2><div className="config-ti-tabla"><table><thead><tr><th>Usuario</th><th>Nombre</th><th>Área</th><th>Perfil</th><th>Origen</th></tr></thead><tbody>{datos.usuarios.map(x=><tr key={x.usuario} onClick={()=>setUsuarioForm({usuario:x.usuario,area:x.area,perfil:x.perfil,correo:x.correo,estado:x.estado})}><td><strong>{x.usuario}</strong></td><td>{x.nombreCompleto}</td><td>{x.area}</td><td>{x.perfil}</td><td>{x.fuenteIdentidad}</td></tr>)}</tbody></table></div></article></section>}

          {seccion==='RECURSOS'&&<section className="config-ti-dos-columnas"><article className="config-ti-panel"><h2>Formatos frecuentes</h2><form className="config-ti-form" onSubmit={e=>{e.preventDefault();if(!formato.archivo){setError('Selecciona un archivo.');return}const fd=new FormData();fd.append('FormatoCodigo',formato.codigo);fd.append('Titulo',formato.titulo);fd.append('Descripcion',formato.descripcion);fd.append('TipoTicket',formato.tipoTicket);fd.append('Estado',formato.estado);fd.append('Archivo',formato.archivo);void ejecutar(()=>guardarFormatoSoporte(fd),'El formato fue publicado.')}}><label>Código<input required value={formato.codigo} onChange={e=>setFormato(v=>({...v,codigo:e.target.value.toUpperCase()}))}/></label><label>Título<input required value={formato.titulo} onChange={e=>setFormato(v=>({...v,titulo:e.target.value}))}/></label><label>Descripción<textarea value={formato.descripcion} onChange={e=>setFormato(v=>({...v,descripcion:e.target.value}))}/></label><label>Tipo de ticket<select value={formato.tipoTicket} onChange={e=>setFormato(v=>({...v,tipoTicket:e.target.value}))}><option value="">Todos</option>{datos.tipos.filter(x=>x.estado==='A').map(x=><option key={x.tipo} value={x.tipo}>{x.descripcion}</option>)}</select></label><label>Archivo<input type="file" accept=".pdf,.doc,.docx,.xls,.xlsx" onChange={e=>setFormato(v=>({...v,archivo:e.target.files?.[0]||null}))}/></label><button className="principal" disabled={procesando}>Publicar formato</button></form><div className="config-ti-reglas">{datos.formatos.map(x=><div key={x.formatoCodigo}><strong>{x.titulo}</strong><span>{x.nombreOriginal} · {x.estado}</span></div>)}</div></article><article className="config-ti-panel"><h2>Ayuda visible para usuarios</h2><p className="config-ti-ayuda">Solo artículos validados y activos deben publicarse. Esto reemplaza tutoriales estáticos sin adelantar el Asistente TI.</p><div className="config-ti-conocimiento">{datos.conocimientos.map(x=><label key={x.conocimientoCodigo}><span><strong>{x.titulo}</strong><small>{x.conocimientoCodigo} · {x.estado}</small></span><input type="checkbox" checked={x.visibleUsuario} disabled={x.estado!=='A'||procesando} onChange={e=>void ejecutar(()=>actualizarVisibilidadConocimiento(x.conocimientoCodigo,e.target.checked),'La visibilidad del artículo fue actualizada.')}/></label>)}</div></article></section>}
        </>}
      </main>
    </section>
  </div>
}
