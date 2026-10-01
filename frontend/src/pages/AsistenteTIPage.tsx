/** Asistente operativo para perfiles TI con acciones revisables y confirmación explícita. */

import { useEffect, useRef, useState, type FormEvent, type KeyboardEvent, type ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import NotificacionesCampana from '../components/NotificacionesCampana'
import { useAutenticacion } from '../features/autenticacion/context/AutenticacionContext'
import type { AsistenteMensajeHistorial } from '../features/asistente/services/asistenteUsuarioService'
import { confirmarAccionTI, consultarAsistenteTI, type AsistenteTIAccion } from '../features/asistente/services/asistenteTIService'
import './InicioPage.css'
import './AsistenteUsuarioPage.css'
import './AsistenteTIPage.css'

type IconoNombre = 'inicio'|'asistente'|'gestion'|'conocimiento'|'reporte'|'configuracion'|'salir'|'enviar'|'escudo'|'usuarios'|'check'|'alerta'|'flecha'|'limpiar'|'actividad'

function Icono({ nombre, size=20 }: { nombre:IconoNombre; size?:number }) {
  const trazos:Record<IconoNombre,ReactNode>={
    inicio:<><path d="M3 11.5 12 4l9 7.5"/><path d="M5.5 10.5V20h13v-9.5"/></>,
    asistente:<><path d="m12 3 1.2 3.1L16 7.5l-2.8 1.4L12 12l-1.2-3.1L8 7.5l2.8-1.4L12 3Z"/><path d="m5 13 .8 2.2L8 16l-2.2.8L5 19l-.8-2.2L2 16l2.2-.8L5 13Z"/></>,
    gestion:<><rect x="4" y="3" width="16" height="18" rx="2"/><path d="M8 8h8M8 12h6M8 16h4"/></>,
    conocimiento:<><path d="M4 5.5A3.5 3.5 0 0 1 7.5 2H11v17H7.5A3.5 3.5 0 0 0 4 22V5.5Z"/><path d="M20 5.5A3.5 3.5 0 0 0 16.5 2H13v17h3.5A3.5 3.5 0 0 1 20 22V5.5Z"/></>,
    reporte:<><path d="M4 20V10M10 20V4M16 20v-7M22 20H2"/></>,
    configuracion:<><circle cx="12" cy="12" r="3"/><path d="M19 12a7 7 0 0 0-.1-1l2-1.5-2-3.4-2.4 1a8 8 0 0 0-1.7-1L14.5 3h-5L9 6.1a8 8 0 0 0-1.7 1l-2.4-1-2 3.4 2 1.5a7 7 0 0 0 0 2l-2 1.5 2 3.4 2.4-1a8 8 0 0 0 1.7 1l.5 3.1h5l.5-3.1a8 8 0 0 0 1.7-1l2.4 1 2-3.4-2-1.5a7 7 0 0 0 .1-1Z"/></>,
    salir:<><path d="M10 5H5v14h5"/><path d="m14 8 4 4-4 4M18 12H9"/></>,
    enviar:<><path d="m3 11 18-8-7 18-3-7-8-3Z"/><path d="m11 14 4-4"/></>,
    escudo:<><path d="M12 3 5 6v5c0 4.8 2.9 8.1 7 10 4.1-1.9 7-5.2 7-10V6l-7-3Z"/><path d="m9 12 2 2 4-5"/></>,
    usuarios:<><circle cx="9" cy="8" r="3"/><path d="M3 20c.6-4 2.6-6 6-6s5.4 2 6 6M16 6a3 3 0 0 1 0 6M17 14c2.4.5 3.7 2.5 4 6"/></>,
    check:<><circle cx="12" cy="12" r="9"/><path d="m8 12 2.5 2.5L16 9"/></>,
    alerta:<><circle cx="12" cy="12" r="9"/><path d="M12 7v6M12 17h.01"/></>,
    flecha:<><path d="M5 12h14M15 8l4 4-4 4"/></>,
    limpiar:<><path d="m4 15 8-8 5 5-8 8H4v-5Z"/><path d="m10 9 5 5M13 20h7"/></>,
    actividad:<><path d="M3 12h4l2-5 4 10 2-5h6"/></>,
  }
  return <svg className="inicio-icono" width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">{trazos[nombre]}</svg>
}

interface Mensaje {
  id:number
  rol:'usuario'|'asistente'
  contenido:string
  modo?:'IA'|'CONOCIMIENTO'
  fuentes?:string[]
  sugerencias?:string[]
  accion?:AsistenteTIAccion|null
  accionEstado?:'confirmando'|'completada'
}

const inicial:Mensaje={id:0,rol:'asistente',contenido:'Puedo consultar la configuración operativa y preparar acciones seguras. Para crear un usuario, indícame su usuario Spring, área y perfil. Antes de ejecutar te mostraré un resumen para confirmar.',modo:'CONOCIMIENTO'}
const rapidas=['Crear un usuario','Ver áreas disponibles','Ver perfiles disponibles','Ver SLA activos']
const primerNombre=(nombre:string)=>nombre.trim().split(/\s+/)[0]||nombre

export default function AsistenteTIPage(){
  const navigate=useNavigate(); const {usuario,cerrarSesion}=useAutenticacion()
  const [mensajes,setMensajes]=useState<Mensaje[]>([inicial]); const [consulta,setConsulta]=useState(''); const [enviando,setEnviando]=useState(false); const [error,setError]=useState('')
  const secuencia=useRef(1); const editor=useRef<HTMLTextAreaElement>(null); const final=useRef<HTMLDivElement>(null)

  useEffect(()=>{final.current?.scrollIntoView({behavior:'smooth',block:'nearest'})},[mensajes,enviando])
  useEffect(()=>{const atajo=(e:globalThis.KeyboardEvent)=>{if((e.ctrlKey||e.metaKey)&&e.key.toLowerCase()==='k'){e.preventDefault();editor.current?.focus()}};window.addEventListener('keydown',atajo);return()=>window.removeEventListener('keydown',atajo)},[])
  if(!usuario)return null

  async function enviar(forzado?:string){
    const texto=(forzado??consulta).trim();if(texto.length<3||enviando)return
    const historial:AsistenteMensajeHistorial[]=mensajes.filter(x=>x.id!==0).slice(-10).map(x=>({rol:x.rol,contenido:x.contenido}))
    setMensajes(v=>[...v,{id:secuencia.current++,rol:'usuario',contenido:texto}]);setConsulta('');setError('');setEnviando(true)
    try{const r=await consultarAsistenteTI(texto,historial);setMensajes(v=>[...v,{id:secuencia.current++,rol:'asistente',contenido:r.respuesta,modo:r.modo,fuentes:r.fuentes,sugerencias:r.sugerencias,accion:r.accion}])}
    catch(e){setError(e instanceof Error?e.message:'No fue posible completar la consulta.')}
    finally{setEnviando(false);setTimeout(()=>editor.current?.focus(),0)}
  }
  async function confirmar(id:number,accion:AsistenteTIAccion){
    if(!accion.tokenConfirmacion)return
    setError('');setMensajes(v=>v.map(x=>x.id===id?{...x,accionEstado:'confirmando' as const}:x))
    try{const r=await confirmarAccionTI(accion.tokenConfirmacion);setMensajes(v=>[...v.map(x=>x.id===id?{...x,accionEstado:'completada' as const}:x),{id:secuencia.current++,rol:'asistente',contenido:r.mensaje,modo:'CONOCIMIENTO',fuentes:['Spring','Gestión TI']}])}
    catch(e){setMensajes(v=>v.map(x=>x.id===id?{...x,accionEstado:undefined}:x));setError(e instanceof Error?e.message:'No fue posible ejecutar la acción.')}
  }
  async function salir(){await cerrarSesion();navigate('/login',{replace:true})}
  function submit(e:FormEvent){e.preventDefault();void enviar()}
  function tecla(e:KeyboardEvent<HTMLTextAreaElement>){if(e.key==='Enter'&&!e.shiftKey){e.preventDefault();void enviar()}}
  const nombre=primerNombre(usuario.nombreCompleto)

  return <div className="inicio-shell asistente-shell asistente-ti-shell">
    <aside className="inicio-sidebar" aria-label="Navegación principal"><div className="inicio-marca"><span className="inicio-marca__nombre">CALIMOD</span><span className="inicio-marca__sistema">Sistema inteligente<br/>de incidencias TI</span></div><nav className="inicio-menu">
      <button className="inicio-menu__item" onClick={()=>navigate('/inicio')}><Icono nombre="inicio"/><span>Inicio</span></button><button className="inicio-menu__item inicio-menu__item--activo" aria-current="page"><Icono nombre="asistente"/><span>Asistente TI</span></button><button className="inicio-menu__item" onClick={()=>navigate('/gestion-tickets')}><Icono nombre="gestion"/><span>Gestión de Tickets</span></button><button className="inicio-menu__item" onClick={()=>navigate('/base-conocimiento')}><Icono nombre="conocimiento"/><span>Base de Conocimiento</span></button><button className="inicio-menu__item" onClick={()=>navigate('/reportes')}><Icono nombre="reporte"/><span>Reportes</span></button><button className="inicio-menu__item" onClick={()=>navigate('/configuracion-ti')}><Icono nombre="configuracion"/><span>Maestros TI</span></button>
    </nav><div className="inicio-sidebar__mensaje"><span>Decisiones claras, acciones controladas.</span><strong>CALIMOD</strong></div><div className="inicio-sidebar__pie"><span className="inicio-sidebar__ayuda-icono">i</span><span>Acciones protegidas</span><small>Toda modificación requiere confirmación</small></div></aside>
    <section className="inicio-principal"><header className="inicio-topbar asistente-topbar"><div className="asistente-topbar__titulo"><span><Icono nombre="asistente" size={19}/></span><div><strong>Asistente operativo TI</strong><small>Conocimiento y acciones controladas</small></div></div><div className="inicio-topbar__usuario"><NotificacionesCampana/><div className="inicio-avatar">{nombre[0]?.toUpperCase()}</div><div className="inicio-identidad"><strong>{nombre}</strong><span>{usuario.perfil}</span></div><button className="inicio-salir" onClick={()=>void salir()} title="Cerrar sesión"><Icono nombre="salir"/></button></div></header>
      <main className="asistente-contenido"><header className="asistente-cabecera"><div><span className="asistente-cabecera__etiqueta"><Icono nombre="escudo" size={15}/> Operación asistida</span><h1>¿Qué necesitas gestionar, {nombre}?</h1><p>Consulta la configuración y prepara cambios sin escribir SQL ni perder trazabilidad.</p></div><button className="asistente-limpiar" title="Iniciar nueva conversación" onClick={()=>{setMensajes([inicial]);setError('');setConsulta('')}} disabled={mensajes.length===1}><Icono nombre="limpiar" size={17}/>Nueva conversación</button></header>
        <div className="asistente-layout"><section className="asistente-chat"><div className="asistente-mensajes" aria-live="polite">
          {mensajes.map(m=><article className={`asistente-mensaje asistente-mensaje--${m.rol}`} key={m.id}>{m.rol==='asistente'&&<div className="asistente-mensaje__avatar"><Icono nombre="asistente" size={18}/></div>}<div className="asistente-mensaje__cuerpo"><div className="asistente-mensaje__meta"><strong>{m.rol==='asistente'?'Asistente TI':'Tú'}</strong>{m.modo&&<span className={`asistente-modo asistente-modo--${m.modo.toLowerCase()}`}>{m.modo==='IA'?'IA conectada':'Datos internos'}</span>}</div><p>{m.contenido}</p>
            {m.accion&&<section className={`asistente-ti-accion ${m.accionEstado==='completada'?'asistente-ti-accion--completada':''}`}><header><span><Icono nombre={m.accionEstado==='completada'?'check':'usuarios'} size={18}/></span><div><strong>{m.accion.titulo}</strong><small>{m.accionEstado==='completada'?'Acción ejecutada y registrada':'Revisión obligatoria antes de ejecutar'}</small></div></header>{m.accion.usuario&&<dl><div><dt>Usuario Spring</dt><dd>{m.accion.usuario.usuario}{m.accion.usuario.yaExiste&&<em>Ya configurado</em>}</dd></div><div><dt>Área</dt><dd>{m.accion.usuario.area} · {m.accion.usuario.areaDescripcion}</dd></div><div><dt>Perfil</dt><dd>{m.accion.usuario.perfil} · {m.accion.usuario.perfilDescripcion}</dd></div><div><dt>Correo</dt><dd>{m.accion.usuario.correo||'Se utilizará el correo de Spring'}</dd></div><div><dt>Estado</dt><dd>Activo</dd></div></dl>}{m.accion.faltantes.length>0&&<div className="asistente-ti-faltantes"><Icono nombre="alerta" size={16}/>Falta completar: {m.accion.faltantes.join(', ')}</div>}{m.accion.tokenConfirmacion&&m.accionEstado!=='completada'&&<footer><span><Icono nombre="escudo" size={14}/>Propuesta firmada, válida por 10 minutos.</span><button onClick={()=>void confirmar(m.id,m.accion!)} disabled={m.accionEstado==='confirmando'}>{m.accionEstado==='confirmando'?'Sincronizando...':'Confirmar y sincronizar'}<Icono nombre="flecha" size={15}/></button></footer>}</section>}
            {!!m.fuentes?.length&&<div className="asistente-ti-fuentes">{m.fuentes.map(x=><span key={x}>{x}</span>)}</div>}{!!m.sugerencias?.length&&<div className="asistente-sugerencias">{m.sugerencias.map(x=><button key={x} onClick={()=>void enviar(x)}>{x}</button>)}</div>}
          </div></article>)}{enviando&&<div className="asistente-pensando"><span><i/><i/><i/></span>Validando configuración autorizada...</div>}<div ref={final}/></div>
          {error&&<div className="asistente-error" role="alert"><Icono nombre="alerta" size={18}/><span>{error}</span><button onClick={()=>setError('')} aria-label="Cerrar mensaje">×</button></div>}
          <form className="asistente-editor" onSubmit={submit}><textarea ref={editor} value={consulta} onChange={e=>setConsulta(e.target.value)} onKeyDown={tecla} maxLength={1200} rows={2} placeholder="Ej. Crea el usuario JPEREZ en el área 021 con perfil USR..." disabled={enviando}/><div className="asistente-editor__pie"><span><b>{consulta.length}</b>/1200 · Enter para enviar</span><button title="Enviar consulta" aria-label="Enviar consulta" disabled={consulta.trim().length<3||enviando}><Icono nombre="enviar" size={19}/></button></div></form><p className="asistente-privacidad"><Icono nombre="escudo" size={14}/>La IA prepara propuestas; solo una confirmación explícita puede ejecutar cambios.</p>
        </section><aside className="asistente-contexto"><section className="asistente-contexto__seccion"><header><span><Icono nombre="actividad" size={18}/></span><div><h2>Acciones rápidas</h2><p>Consultas y operaciones disponibles</p></div></header><div className="asistente-rapidas">{rapidas.map(x=><button key={x} onClick={()=>void enviar(x)} disabled={enviando}><span>{x}</span><Icono nombre="flecha" size={15}/></button>)}</div></section>
          <section className="asistente-ti-capacidades"><h2>Capacidades habilitadas</h2><ul><li><Icono nombre="check" size={15}/><span><strong>Usuarios corporativos</strong>Alta o actualización desde Spring.</span></li><li><Icono nombre="check" size={15}/><span><strong>Configuración vigente</strong>Áreas, líneas, categorías, SLA y perfiles reales.</span></li><li><Icono nombre="escudo" size={15}/><span><strong>Ejecución protegida</strong>Token firmado y confirmación manual.</span></li></ul></section>
          <section className="asistente-confianza"><Icono nombre="escudo" size={20}/><div><strong>Límite operativo</strong><p>No crea identidades en Spring, no genera contraseñas y nunca ejecuta instrucciones libres sobre SQL Server.</p></div></section>
        </aside></div>
      </main>
    </section>
  </div>
}
