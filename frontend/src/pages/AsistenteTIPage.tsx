/**
 * Archivo: AsistenteTIPage.tsx
 * Objetivo: Proporcionar la consola del Agente de Ingeniería Autónomo para operadores TI.
 * Responsabilidad: Iniciar investigación, observar/reproducir mediante Live, mostrar evidencia, diagnóstico, expediente y decisión human-in-the-loop.
 * Consideraciones: La pantalla compartida es evidencia de usuario; los cambios solo se solicitan después del diagnóstico y nunca se ejecutan desde el cliente.
 */

import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import NotificacionesCampana from '../components/NotificacionesCampana'
import { useAutenticacion } from '../features/autenticacion/context/AutenticacionContext'
import {
  analizarInvestigacionTI,
  crearInvestigacionTI,
  crearTokenLiveTI,
  finalizarObservacionTI,
  grabarInformacionTI,
  obtenerInvestigacionTI,
  realizarCambioTI,
  registrarEventoInvestigacionTI,
  type AgenteTIContextoInvestigacion,
  type AgenteTIDiagnosticoRespuesta,
  type AgenteTISesion,
} from '../features/asistente/services/asistenteTIService'
import { GeminiLiveSesion } from '../features/asistente/services/geminiLiveTIService'
import './InicioPage.css'
import './AsistenteTIPage.css'

type IconoNombre = 'inicio'|'agente'|'gestion'|'conocimiento'|'reporte'|'configuracion'|'salir'|'pantalla'|'microfono'|'codigo'|'buscar'|'archivo'|'escudo'|'check'|'alerta'|'flecha'|'stop'|'datos'|'reloj'

function Icono({ nombre, size=20 }: { nombre:IconoNombre; size?:number }) {
  const trazos:Record<IconoNombre,ReactNode>={
    inicio:<><path d="M3 11.5 12 4l9 7.5"/><path d="M5.5 10.5V20h13v-9.5"/></>,
    agente:<><path d="M12 3 5 6v5c0 4.8 2.9 8.1 7 10 4.1-1.9 7-5.2 7-10V6l-7-3Z"/><path d="m12 7 .9 2.2L15 10l-2.1.8L12 13l-.9-2.2L9 10l2.1-.8L12 7Z"/></>,
    gestion:<><rect x="4" y="3" width="16" height="18" rx="2"/><path d="M8 8h8M8 12h6M8 16h4"/></>,
    conocimiento:<><path d="M4 5.5A3.5 3.5 0 0 1 7.5 2H11v17H7.5A3.5 3.5 0 0 0 4 22V5.5Z"/><path d="M20 5.5A3.5 3.5 0 0 0 16.5 2H13v17h3.5A3.5 3.5 0 0 1 20 22V5.5Z"/></>,
    reporte:<><path d="M4 20V10M10 20V4M16 20v-7M22 20H2"/></>,
    configuracion:<><circle cx="12" cy="12" r="3"/><path d="M19 12a7 7 0 0 0-.1-1l2-1.5-2-3.4-2.4 1a8 8 0 0 0-1.7-1L14.5 3h-5L9 6.1a8 8 0 0 0-1.7 1l-2.4-1-2 3.4 2 1.5a7 7 0 0 0 0 2l-2 1.5 2 3.4 2.4-1a8 8 0 0 0 1.7 1l.5 3.1h5l.5-3.1a8 8 0 0 0 1.7-1l2.4 1 2-3.4-2-1.5a7 7 0 0 0 .1-1Z"/></>,
    salir:<><path d="M10 5H5v14h5"/><path d="m14 8 4 4-4 4M18 12H9"/></>,
    pantalla:<><rect x="3" y="4" width="18" height="13" rx="2"/><path d="M8 21h8M12 17v4"/></>,
    microfono:<><rect x="9" y="3" width="6" height="11" rx="3"/><path d="M5 11a7 7 0 0 0 14 0M12 18v3"/></>,
    codigo:<><path d="m8 9-4 3 4 3M16 9l4 3-4 3M14 5l-4 14"/></>,
    buscar:<><circle cx="10.5" cy="10.5" r="6.5"/><path d="m16 16 5 5"/></>,
    archivo:<><path d="M6 2h8l4 4v16H6z"/><path d="M14 2v5h5M9 12h6M9 16h6"/></>,
    escudo:<><path d="M12 3 5 6v5c0 4.8 2.9 8.1 7 10 4.1-1.9 7-5.2 7-10V6l-7-3Z"/><path d="m9 12 2 2 4-5"/></>,
    check:<><circle cx="12" cy="12" r="9"/><path d="m8 12 2.5 2.5L16 9"/></>,
    alerta:<><path d="M12 3 2.5 20h19L12 3Z"/><path d="M12 9v5M12 17h.01"/></>,
    flecha:<><path d="M5 12h14M15 8l4 4-4 4"/></>,
    stop:<><rect x="6" y="6" width="12" height="12" rx="1"/></>,
    datos:<><ellipse cx="12" cy="5" rx="8" ry="3"/><path d="M4 5v6c0 1.7 3.6 3 8 3s8-1.3 8-3V5M4 11v6c0 1.7 3.6 3 8 3s8-1.3 8-3v-6"/></>,
    reloj:<><circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/></>,
  }
  return <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">{trazos[nombre]}</svg>
}

type Transcripcion = { rol:'usuario'|'agente'; texto:string }
const estadosFinales=['INFORME_GRABADO','CAMBIO_VALIDADO','CANCELADO']
const primerNombre=(nombre:string)=>nombre.trim().split(/\s+/)[0]||nombre

export default function AsistenteTIPage(){
  const navigate=useNavigate()
  const {usuario,cerrarSesion}=useAutenticacion()
  const [incidencia,setIncidencia]=useState('')
  const [descripcion,setDescripcion]=useState('')
  const [sesion,setSesion]=useState<AgenteTISesion|null>(null)
  const [contexto,setContexto]=useState<AgenteTIContextoInvestigacion|null>(null)
  const [diagnostico,setDiagnostico]=useState<AgenteTIDiagnosticoRespuesta|null>(null)
  const [transcripciones,setTranscripciones]=useState<Transcripcion[]>([])
  const transcripcionesRef=useRef<Transcripcion[]>([])
  const [liveActivo,setLiveActivo]=useState(false)
  const [liveEstado,setLiveEstado]=useState('Listo para iniciar observación')
  const [procesando,setProcesando]=useState(false)
  const [error,setError]=useState('')
  const [mensaje,setMensaje]=useState('')
  const videoRef=useRef<HTMLVideoElement>(null)
  const liveRef=useRef<GeminiLiveSesion|null>(null)

  useEffect(()=>()=>{void liveRef.current?.detener()},[])
  if(!usuario)return null
  const nombre=primerNombre(usuario.nombreCompleto)
  const finalizada=!!sesion&&estadosFinales.includes(sesion.estado)

  async function refrescar(sesionNumero=sesion?.sesionNumero){
    if(!sesionNumero)return
    const actual=await obtenerInvestigacionTI(sesionNumero)
    setContexto(actual);setSesion(actual.sesion)
  }

  async function iniciarInvestigacion(){
    if(descripcion.trim().length<5||procesando)return
    setProcesando(true);setError('');setMensaje('');setDiagnostico(null);setTranscripciones([]);transcripcionesRef.current=[]
    try{
      const creada=await crearInvestigacionTI(incidencia,descripcion.trim())
      setSesion(creada);setMensaje(`Investigación AGT-${String(creada.sesionNumero).padStart(6,'0')} creada. Puedes reproducir el problema por Live o investigar con la evidencia del ticket.`)
      await refrescar(creada.sesionNumero)
    }catch(e){setError(e instanceof Error?e.message:'No fue posible iniciar la investigación.')}
    finally{setProcesando(false)}
  }

  function agregarTranscripcion(rol:'usuario'|'agente',texto:string){
    const limpio=texto.trim();if(!limpio)return
    const ultima=transcripcionesRef.current.at(-1)
    if(ultima?.rol===rol&&ultima.texto===limpio)return
    const nueva=[...transcripcionesRef.current,{rol,texto:limpio}].slice(-80)
    transcripcionesRef.current=nueva;setTranscripciones(nueva)
    if(sesion)void registrarEventoInvestigacionTI(sesion.sesionNumero,rol==='usuario'?'TRANSCRIPCION_USUARIO':'TRANSCRIPCION_AGENTE','LIVE',limpio).catch(()=>undefined)
  }

  async function iniciarLive(){
    if(!sesion||liveActivo||procesando)return
    setProcesando(true);setError('');setMensaje('')
    try{
      const token=await crearTokenLiveTI(sesion.sesionNumero)
      if(!token.disponible)throw new Error(token.mensaje)
      await registrarEventoInvestigacionTI(sesion.sesionNumero,'INICIO_LIVE','LIVE','El operador inició una sesión multimodal para observar la reproducción del problema.')
      const live=new GeminiLiveSesion({
        onEstado:setLiveEstado,
        onTranscripcion:agregarTranscripcion,
        onError:setError,
        onPantallaFinalizada:()=>void cerrarObservacion(false),
      })
      liveRef.current=live
      const stream=await live.iniciar(token)
      if(videoRef.current){videoRef.current.srcObject=stream;void videoRef.current.play()}
      setLiveActivo(true);setLiveEstado('Live activo · reproduce el proceso hasta el error')
    }catch(e){setError(e instanceof Error?e.message:'No fue posible iniciar Live.');await liveRef.current?.detener();liveRef.current=null}
    finally{setProcesando(false)}
  }

  async function cerrarObservacion(detener=true){
    if(!sesion)return
    if(detener)await liveRef.current?.detener()
    liveRef.current=null;setLiveActivo(false)
    if(videoRef.current)videoRef.current.srcObject=null
    const texto=transcripcionesRef.current.map(x=>`${x.rol==='usuario'?'Usuario':'Agente'}: ${x.texto}`).join('\n')
    const posibleError=[...transcripcionesRef.current].reverse().find(x=>/error|fall|excep|no puede|no se puede|incorrect/i.test(x.texto))?.texto||''
    try{
      await registrarEventoInvestigacionTI(sesion.sesionNumero,'FIN_LIVE','LIVE','La etapa de observación Live finalizó y el agente continuará con investigación técnica.')
      await finalizarObservacionTI(sesion.sesionNumero,'Sesión Live finalizada con reproducción guiada del flujo del usuario.',texto||'No se obtuvo transcripción; la pantalla fue compartida durante la sesión.',posibleError)
      await refrescar();setLiveEstado('Observación finalizada · lista para investigar');setMensaje('La reproducción humana terminó. El agente ya puede analizar ticket, conocimiento, auditoría y telemetría disponible.')
    }catch(e){setError(e instanceof Error?e.message:'No fue posible finalizar la observación.')}
  }

  async function investigar(){
    if(!sesion||procesando||finalizada)return
    setProcesando(true);setError('');setMensaje('')
    try{
      if(liveActivo)await cerrarObservacion()
      const r=await analizarInvestigacionTI(sesion.sesionNumero)
      setDiagnostico(r);await refrescar();setMensaje('Investigación completada. Revisa la evidencia, la causa probable y la acción propuesta antes de decidir.')
    }catch(e){setError(e instanceof Error?e.message:'No fue posible completar la investigación.')}
    finally{setProcesando(false)}
  }

  async function grabar(){
    if(!sesion||procesando)return
    setProcesando(true);setError('');setMensaje('')
    try{
      const blob=await grabarInformacionTI(sesion.sesionNumero)
      const url=URL.createObjectURL(blob);const enlace=document.createElement('a')
      enlace.href=url;enlace.download=`${sesion.incidenciaNumero||`AGT-${String(sesion.sesionNumero).padStart(6,'0')}`}-investigacion.md`;enlace.click();URL.revokeObjectURL(url)
      await refrescar();setMensaje('Expediente Markdown grabado y descargado. El agente finalizó sin realizar cambios.')
    }catch(e){setError(e instanceof Error?e.message:'No fue posible grabar la información.')}
    finally{setProcesando(false)}
  }

  async function realizarCambio(){
    if(!sesion||procesando)return
    if(!window.confirm('TI autorizará la acción estructurada propuesta. El archivo .md no se ejecutará. ¿Deseas continuar?'))return
    setProcesando(true);setError('');setMensaje('')
    try{
      const r=await realizarCambioTI(sesion.sesionNumero)
      setMensaje(r.mensaje);await refrescar()
      if(r.estado==='PENDIENTE_APROBACION')setMensaje(`${r.mensaje} Solicitud #${r.solicitudAprobacionSecuencia}. Debe revisarse desde Gestión de Tickets.`)
    }catch(e){setError(e instanceof Error?e.message:'No fue posible procesar el cambio.')}
    finally{setProcesando(false)}
  }

  async function nuevaInvestigacion(){
    if(liveActivo)await liveRef.current?.detener()
    liveRef.current=null;setLiveActivo(false);setSesion(null);setContexto(null);setDiagnostico(null);setIncidencia('');setDescripcion('');setTranscripciones([]);transcripcionesRef.current=[];setMensaje('');setError('');setLiveEstado('Listo para iniciar observación')
  }

  async function salir(){await cerrarSesion();navigate('/login',{replace:true})}

  const diag=diagnostico??(contexto?.sesion.diagnostico?{
    sesionNumero:contexto.sesion.sesionNumero,estado:contexto.sesion.estado,diagnostico:contexto.sesion.diagnostico,causaProbable:contexto.sesion.causaProbable,solucionPropuesta:contexto.sesion.solucionPropuesta,confianza:contexto.sesion.confianza??0,evidencias:[],trazaTecnica:[],accion:contexto.sesion.accionCodigo?{accionCodigo:contexto.sesion.accionCodigo,nombre:contexto.acciones.find(x=>x.accionCodigo===contexto.sesion.accionCodigo)?.nombre||contexto.sesion.accionCodigo,nivelRiesgo:contexto.sesion.nivelRiesgo,requiereAprobacion:contexto.acciones.find(x=>x.accionCodigo===contexto.sesion.accionCodigo)?.requiereAprobacion??true,parametrosJson:contexto.sesion.parametrosJson||'{}'}:null,informeDisponible:contexto.sesion.informeDisponible,limitacion:''
  }:null)
  const progreso=useMemo(()=>obtenerProgreso(sesion?.estado||''),[sesion?.estado])

  return <div className="inicio-shell agente-ti-shell">
    <aside className="inicio-sidebar" aria-label="Navegación principal"><div className="inicio-marca"><span className="inicio-marca__nombre">CALIMOD</span><span className="inicio-marca__sistema">Sistema inteligente<br/>de incidencias TI</span></div><nav className="inicio-menu">
      <button className="inicio-menu__item" onClick={()=>navigate('/inicio')}><Icono nombre="inicio"/><span>Inicio</span></button><button className="inicio-menu__item inicio-menu__item--activo"><Icono nombre="agente"/><span>Asistente TI</span></button><button className="inicio-menu__item" onClick={()=>navigate('/gestion-tickets')}><Icono nombre="gestion"/><span>Gestión de Tickets</span></button><button className="inicio-menu__item" onClick={()=>navigate('/base-conocimiento')}><Icono nombre="conocimiento"/><span>Base de Conocimiento</span></button><button className="inicio-menu__item" onClick={()=>navigate('/reportes')}><Icono nombre="reporte"/><span>Reportes</span></button><button className="inicio-menu__item" onClick={()=>navigate('/configuracion-ti')}><Icono nombre="configuracion"/><span>Maestros TI</span></button>
    </nav><div className="agente-sidebar-info"><Icono nombre="escudo" size={16}/><div><strong>Human-in-the-loop</strong><small>El agente investiga. TI decide y autoriza.</small></div></div></aside>

    <section className="inicio-principal"><header className="inicio-topbar agente-topbar"><div className="agente-topbar__titulo"><span><Icono nombre="agente" size={19}/></span><div><strong>Agente de Ingeniería Autónomo</strong><small>Observación · Investigación · Diagnóstico · Ejecución controlada</small></div></div><div className="inicio-topbar__usuario"><NotificacionesCampana/><div className="inicio-avatar">{nombre[0]?.toUpperCase()}</div><div className="inicio-identidad"><strong>{nombre}</strong><span>{usuario.perfil}</span></div><button className="inicio-salir" onClick={()=>void salir()}><Icono nombre="salir"/></button></div></header>
      <main className="agente-contenido">
        <header className="agente-hero"><div><span className="agente-eyebrow"><Icono nombre="escudo" size={14}/> Investigación autónoma bajo control TI</span><h1>{sesion?`Investigación AGT-${String(sesion.sesionNumero).padStart(6,'0')}`:'Investiga la incidencia de extremo a extremo'}</h1><p>{sesion?'El agente conserva evidencia, correlación y límites de autoridad durante todo el proceso.':'Reproduce el error por Live o inicia con un ticket existente. El agente documentará lo comprobable y nunca inventará el trazado técnico faltante.'}</p></div>{sesion&&<button className="agente-btn agente-btn--secundario" onClick={()=>void nuevaInvestigacion()} disabled={procesando}>Nueva investigación</button>}</header>

        {(error||mensaje)&&<div className={`agente-aviso ${error?'agente-aviso--error':'agente-aviso--ok'}`} role={error?'alert':'status'}><Icono nombre={error?'alerta':'check'} size={17}/><span>{error||mensaje}</span>{error&&<button onClick={()=>setError('')}>×</button>}</div>}

        {!sesion?<section className="agente-inicio-card"><div className="agente-inicio-card__icono"><Icono nombre="buscar" size={27}/></div><div className="agente-inicio-card__titulo"><h2>Crear expediente de investigación</h2><p>Asocia el ticket cuando exista. Si todavía no existe, el agente puede recopilar la evidencia Live y documentarla sin ejecutar cambios.</p></div><label><span>Incidencia <small>opcional</small></span><input value={incidencia} onChange={e=>setIncidencia(e.target.value.toUpperCase())} maxLength={10} placeholder="INC-000523"/></label><label><span>Problema a investigar</span><textarea value={descripcion} onChange={e=>setDescripcion(e.target.value)} maxLength={1200} rows={4} placeholder="Ej. El usuario no puede generar el picking después de aprobar la requisición..."/></label><button className="agente-btn agente-btn--primario" onClick={()=>void iniciarInvestigacion()} disabled={descripcion.trim().length<5||procesando}><Icono nombre="flecha" size={17}/>{procesando?'Creando expediente...':'Iniciar investigación'}</button></section>:
        <>
          <section className="agente-progreso" aria-label="Progreso de investigación">{['RECOPILAR','OBSERVAR','INVESTIGAR','DIAGNOSTICAR','DECIDIR'].map((paso,i)=><div key={paso} className={i<progreso?'agente-progreso__paso agente-progreso__paso--ok':i===progreso?'agente-progreso__paso agente-progreso__paso--actual':'agente-progreso__paso'}><span>{i<progreso?<Icono nombre="check" size={13}/>:i+1}</span><strong>{paso}</strong></div>)}</section>

          <div className="agente-grid">
            <section className="agente-panel agente-live"><header><div><span className="agente-panel__icono"><Icono nombre="pantalla" size={19}/></span><div><h2>Observación Live</h2><p>Pantalla + voz para reproducir exactamente el proceso del usuario</p></div></div><span className={`agente-estado ${liveActivo?'agente-estado--live':''}`}>{liveActivo?'EN VIVO':'OBSERVACIÓN'}</span></header>
              <div className="agente-live__visor"><video ref={videoRef} muted playsInline/><div className={liveActivo?'agente-live__placeholder agente-live__placeholder--oculto':'agente-live__placeholder'}><Icono nombre="pantalla" size={34}/><strong>{contexto?.sesion.procesoObservado?'Reproducción registrada':'Comparte la pantalla cuando estés listo'}</strong><span>La imagen se procesa durante Live; el expediente conserva eventos y transcripción necesarios para el diagnóstico.</span></div></div>
              <div className="agente-live__estado"><span className={liveActivo?'agente-pulso':''}/><p>{liveEstado}</p></div>
              <div className="agente-live__acciones">{!liveActivo?<button className="agente-btn agente-btn--live" onClick={()=>void iniciarLive()} disabled={procesando||finalizada}><Icono nombre="pantalla" size={17}/>Compartir pantalla y conversar</button>:<button className="agente-btn agente-btn--stop" onClick={()=>void cerrarObservacion()}><Icono nombre="stop" size={15}/>Finalizar reproducción</button>}<button className="agente-btn agente-btn--primario" onClick={()=>void investigar()} disabled={procesando||finalizada}><Icono nombre="buscar" size={17}/>{procesando?'Investigando...':'Investigar ahora'}</button></div>
              <div className="agente-transcripcion"><div className="agente-transcripcion__titulo"><Icono nombre="microfono" size={15}/><strong>Conversación Live</strong><span>{transcripciones.length} eventos</span></div>{transcripciones.length===0?<p className="agente-vacio">La transcripción aparecerá aquí durante la reproducción.</p>:<div className="agente-transcripcion__lista">{transcripciones.slice(-12).map((t,i)=><div key={`${i}-${t.texto}`} className={`agente-transcripcion__item agente-transcripcion__item--${t.rol}`}><b>{t.rol==='usuario'?'Usuario':'Agente'}</b><span>{t.texto}</span></div>)}</div>}</div>
            </section>

            <aside className="agente-columna">
              <section className="agente-panel agente-contexto"><header><div><span className="agente-panel__icono"><Icono nombre="datos" size={18}/></span><div><h2>Contexto correlacionado</h2><p>Identidad y fuentes autorizadas</p></div></div></header><dl><div><dt>Incidencia</dt><dd>{contexto?.ticket.incidenciaNumero||'Sin ticket asociado'}</dd></div><div><dt>Correlation ID</dt><dd className="agente-mono">{sesion.idCorrelacion}</dd></div><div><dt>Documentos</dt><dd>{contexto?.documentos.length??0}</dd></div><div><dt>Conocimiento</dt><dd>{contexto?.conocimientos.length??0} referencias</dd></div><div><dt>Auditoría</dt><dd>{contexto?.auditoria.length??0} eventos</dd></div><div><dt>Estado</dt><dd><span className="agente-chip">{sesion.estado.replaceAll('_',' ')}</span></dd></div></dl></section>
              <section className="agente-panel agente-regla"><Icono nombre="escudo" size={21}/><div><strong>Separación de autoridad</strong><p>Live observa. El investigador diagnostica. Solo el backend puede ejecutar una acción catalogada después de permisos y aprobación.</p></div></section>
            </aside>
          </div>

          <section className="agente-panel agente-diagnostico"><header><div><span className="agente-panel__icono"><Icono nombre="codigo" size={19}/></span><div><h2>Investigación técnica</h2><p>Evidencia verificada y reproducción técnica disponible</p></div></div>{diag&&<div className="agente-confianza"><span>{Math.round(diag.confianza)}%</span><small>confianza diagnóstica</small></div>}</header>
            {!diag?<div className="agente-diagnostico__espera"><div><Icono nombre="buscar" size={25}/></div><strong>Esperando investigación</strong><p>Al iniciar el análisis, el agente combinará el flujo observado con ticket, documentos, conocimiento, auditoría y telemetría autorizada.</p></div>:<div className="agente-diagnostico__contenido"><div className="agente-hallazgo"><span>DIAGNÓSTICO</span><h3>{diag.diagnostico}</h3></div><div className="agente-dos-columnas"><article><span>CAUSA PROBABLE</span><p>{diag.causaProbable}</p></article><article><span>SOLUCIÓN PROPUESTA</span><p>{diag.solucionPropuesta}</p></article></div>{diag.limitacion&&<div className="agente-limitacion"><Icono nombre="alerta" size={17}/><span>{diag.limitacion}</span></div>}
              <div className="agente-evidencia-grid"><article><header><Icono nombre="archivo" size={16}/><strong>Evidencia</strong><span>{diag.evidencias.length}</span></header>{diag.evidencias.length===0?<p className="agente-vacio">Sin evidencia adicional.</p>:diag.evidencias.slice(0,8).map((e,i)=><div className="agente-evidencia" key={`${e.referencia}-${i}`}><b>{e.tipoFuente}</b><strong>{e.referencia}</strong><p>{e.descripcion}</p></div>)}</article><article><header><Icono nombre="codigo" size={16}/><strong>Traza técnica</strong><span>{diag.trazaTecnica.length}</span></header>{diag.trazaTecnica.length===0?<p className="agente-vacio">No hay trazas instrumentadas. El agente no inventará un recorrido de código.</p>:diag.trazaTecnica.map((t,i)=><div className="agente-traza" key={i}><span>{i+1}</span><code>{t}</code></div>)}</article></div>
              {diag.accion&&<section className="agente-accion-propuesta"><div><span className="agente-panel__icono"><Icono nombre="escudo" size={18}/></span><div><small>ACCIÓN PROPUESTA</small><strong>{diag.accion.accionCodigo} · {diag.accion.nombre}</strong><p>Riesgo {diag.accion.nivelRiesgo} · {diag.accion.requiereAprobacion?'requiere aprobación':'sin aprobación adicional según catálogo'}</p></div></div><code>{diag.accion.parametrosJson}</code></section>}
              <footer className="agente-decision"><div><strong>Decisión de TI</strong><p>El .md documenta la investigación; nunca se interpreta como instrucción ejecutable.</p></div><div className="agente-decision__botones"><button className="agente-btn agente-btn--secundario" onClick={()=>void grabar()} disabled={procesando||finalizada}><Icono nombre="archivo" size={16}/>Grabar información</button><button className="agente-btn agente-btn--cambio" onClick={()=>void realizarCambio()} disabled={procesando||finalizada||!diag.accion}><Icono nombre="escudo" size={16}/>Realizar cambio</button></div></footer>
            </div>}
          </section>
        </>}
      </main>
    </section>
  </div>
}

function obtenerProgreso(estado:string){
  if(!estado)return 0
  if(['RECOPILANDO'].includes(estado))return 0
  if(['OBSERVANDO','LISTO_INVESTIGAR'].includes(estado))return 1
  if(['INVESTIGANDO'].includes(estado))return 2
  if(['PENDIENTE_TI'].includes(estado))return 4
  if(['PENDIENTE_APROBACION','LISTO_EJECUCION','EJECUTANDO','SIN_EJECUTOR','INFORME_GRABADO','CAMBIO_VALIDADO','ERROR_EJECUCION'].includes(estado))return 5
  return 2
}
