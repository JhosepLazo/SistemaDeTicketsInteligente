/**
 * Archivo: geminiLiveTIService.ts
 * Objetivo: Conectar el navegador con Gemini Live usando el token efímero emitido por el backend.
 * Responsabilidad: Compartir pantalla, transmitir audio PCM/video y reproducir la respuesta de voz sin exponer la API key permanente.
 * Consideraciones: Live solo observa y conversa; no llama endpoints de cambio ni posee autoridad para ejecutar acciones productivas.
 */

import type { AgenteTILiveTokenRespuesta } from './asistenteTIService'

export interface GeminiLiveCallbacks {
  onEstado?: (estado: string) => void
  onTranscripcion?: (rol: 'usuario' | 'agente', texto: string) => void
  onError?: (mensaje: string) => void
  onPantallaFinalizada?: () => void
  /** El proveedor cerró la conexión (límite de sesión o red); la captura local se detiene. */
  onDesconexion?: () => void
}

export class GeminiLiveSesion {
  private socket: WebSocket | null = null
  private pantalla: MediaStream | null = null
  private microfono: MediaStream | null = null
  private audioEntrada: AudioContext | null = null
  private audioSalida: AudioContext | null = null
  private procesador: ScriptProcessorNode | null = null
  private origenMicrofono: MediaStreamAudioSourceNode | null = null
  private intervaloVideo: number | null = null
  private listo = false
  private siguienteAudio = 0
  private deteniendo = false
  // Gemini transcribe por fragmentos; se acumulan y se entregan completos al cerrar cada turno.
  private turnoUsuario = ''
  private turnoAgente = ''

  constructor(private readonly callbacks: GeminiLiveCallbacks = {}) {}

  async iniciar(configuracion: AgenteTILiveTokenRespuesta): Promise<MediaStream> {
    if (!configuracion.disponible || !configuracion.token) throw new Error(configuracion.mensaje || 'La sesión Live no está disponible.')
    if (!navigator.mediaDevices?.getDisplayMedia) throw new Error('El navegador no permite compartir pantalla mediante MediaDevices.')

    this.deteniendo = false
    this.pantalla = await navigator.mediaDevices.getDisplayMedia({ video: { frameRate: 1 }, audio: false })
    this.pantalla.getVideoTracks()[0]?.addEventListener('ended', () => {
      if (!this.deteniendo) {
        this.callbacks.onPantallaFinalizada?.()
        void this.detener()
      }
    })

    try {
      this.microfono = await navigator.mediaDevices.getUserMedia({ audio: { echoCancellation: true, noiseSuppression: true }, video: false })
    } catch {
      this.microfono = null
      this.callbacks.onEstado?.('Pantalla compartida; micrófono no disponible. Puedes continuar observando y usar texto.')
    }

    await this.conectar(configuracion)
    return this.pantalla
  }

  enviarTexto(texto: string) {
    const contenido = texto.trim()
    if (!contenido || !this.socket || this.socket.readyState !== WebSocket.OPEN || !this.listo) return
    this.socket.send(JSON.stringify({ realtimeInput: { text: contenido } }))
  }

  silenciarMicrofono(silenciado: boolean) {
    this.microfono?.getAudioTracks().forEach(track => { track.enabled = !silenciado })
  }

  get tieneMicrofono() {
    return !!this.microfono
  }

  async detener() {
    this.deteniendo = true
    this.listo = false
    this.cerrarTurno('usuario')
    this.cerrarTurno('agente')
    if (this.intervaloVideo !== null) window.clearInterval(this.intervaloVideo)
    this.intervaloVideo = null
    this.procesador?.disconnect()
    this.origenMicrofono?.disconnect()
    this.microfono?.getTracks().forEach(track => track.stop())
    this.pantalla?.getTracks().forEach(track => track.stop())
    this.socket?.close(1000, 'Sesión Live finalizada')
    await this.audioEntrada?.close().catch(() => undefined)
    await this.audioSalida?.close().catch(() => undefined)
    this.socket = null
    this.microfono = null
    this.pantalla = null
    this.audioEntrada = null
    this.audioSalida = null
    this.procesador = null
    this.origenMicrofono = null
    this.callbacks.onEstado?.('Sesión Live finalizada')
  }

  private async conectar(configuracion: AgenteTILiveTokenRespuesta) {
    const url = `${configuracion.webSocketUrl}?access_token=${encodeURIComponent(configuracion.token)}`
    this.socket = new WebSocket(url)

    await new Promise<void>((resolve, reject) => {
      if (!this.socket) return reject(new Error('No fue posible crear la conexión Live.'))
      const timeout = window.setTimeout(() => reject(new Error('Gemini Live no confirmó la conexión dentro del tiempo esperado.')), 12000)

      this.socket.onopen = () => {
        this.callbacks.onEstado?.('Conectado. Configurando observación multimodal…')
        this.socket?.send(JSON.stringify({
          setup: {
            model: `models/${configuracion.modelo}`,
            responseModalities: ['AUDIO'],
            systemInstruction: { parts: [{ text: configuracion.instruccionSistema }] },
            inputAudioTranscription: {},
            outputAudioTranscription: {},
          },
        }))
      }

      this.socket.onmessage = event => {
        try {
          const respuesta = JSON.parse(String(event.data)) as RespuestaLive
          if (respuesta.setupComplete && !this.listo) {
            window.clearTimeout(timeout)
            this.listo = true
            this.iniciarVideo()
            this.iniciarMicrofono()
            this.enviarTexto('La pantalla ya está compartida. Guíame para reproducir el problema exactamente hasta que aparezca el error.')
            this.callbacks.onEstado?.('Live activo · pantalla y conversación en tiempo real')
            resolve()
          }
          this.procesarRespuesta(respuesta)
        } catch {
          this.callbacks.onError?.('Se recibió una respuesta Live que no pudo interpretarse.')
        }
      }

      this.socket.onerror = () => {
        window.clearTimeout(timeout)
        reject(new Error('Se produjo un error en la conexión con Gemini Live.'))
      }
      this.socket.onclose = () => {
        window.clearTimeout(timeout)
        const estabaListo = this.listo
        this.listo = false
        if (!estabaListo) reject(new Error('Gemini Live cerró la conexión antes de iniciar la observación.'))
        else if (!this.deteniendo) {
          this.callbacks.onDesconexion?.()
          void this.detener()
        }
      }
    })
  }

  private iniciarVideo() {
    if (!this.pantalla) return
    const video = document.createElement('video')
    video.srcObject = this.pantalla
    video.muted = true
    void video.play()
    const canvas = document.createElement('canvas')
    const contexto = canvas.getContext('2d', { alpha: false })
    if (!contexto) return

    this.intervaloVideo = window.setInterval(() => {
      if (!this.listo || !this.socket || this.socket.readyState !== WebSocket.OPEN || video.videoWidth === 0) return
      const escala = Math.min(1, 1280 / video.videoWidth)
      canvas.width = Math.max(1, Math.round(video.videoWidth * escala))
      canvas.height = Math.max(1, Math.round(video.videoHeight * escala))
      contexto.drawImage(video, 0, 0, canvas.width, canvas.height)
      const dataUrl = canvas.toDataURL('image/jpeg', 0.72)
      this.socket.send(JSON.stringify({ realtimeInput: { video: { data: dataUrl.split(',')[1], mimeType: 'image/jpeg' } } }))
    }, 1000)
  }

  private iniciarMicrofono() {
    if (!this.microfono || !this.listo) return
    const AudioContextCtor = window.AudioContext || (window as typeof window & { webkitAudioContext?: typeof AudioContext }).webkitAudioContext
    if (!AudioContextCtor) return
    this.audioEntrada = new AudioContextCtor()
    this.origenMicrofono = this.audioEntrada.createMediaStreamSource(this.microfono)
    this.procesador = this.audioEntrada.createScriptProcessor(4096, 1, 1)
    this.procesador.onaudioprocess = evento => {
      if (!this.listo || !this.socket || this.socket.readyState !== WebSocket.OPEN || !this.audioEntrada) return
      const entrada = evento.inputBuffer.getChannelData(0)
      const muestras = resamplear(entrada, this.audioEntrada.sampleRate, 16000)
      const pcm = convertirPcm16(muestras)
      this.socket.send(JSON.stringify({ realtimeInput: { audio: { data: base64DesdeArrayBuffer(pcm.buffer), mimeType: 'audio/pcm;rate=16000' } } }))
    }
    this.origenMicrofono.connect(this.procesador)
    this.procesador.connect(this.audioEntrada.destination)
  }

  private procesarRespuesta(respuesta: RespuestaLive) {
    const contenido = respuesta.serverContent
    if (!contenido) return
    const textoUsuario = contenido.inputTranscription?.text
    const textoAgente = contenido.outputTranscription?.text
    if (textoUsuario) {
      this.cerrarTurno('agente')
      this.turnoUsuario += textoUsuario
    }
    if (textoAgente) {
      this.cerrarTurno('usuario')
      this.turnoAgente += textoAgente
    }
    if (contenido.turnComplete || contenido.interrupted) {
      this.cerrarTurno('usuario')
      this.cerrarTurno('agente')
    }

    for (const parte of contenido.modelTurn?.parts ?? []) {
      if (!parte.inlineData?.data) continue
      const frecuencia = Number(parte.inlineData.mimeType?.match(/rate=(\d+)/)?.[1] ?? 24000)
      this.reproducirPcm(parte.inlineData.data, frecuencia)
    }
  }

  private cerrarTurno(rol: 'usuario' | 'agente') {
    const texto = (rol === 'usuario' ? this.turnoUsuario : this.turnoAgente).replace(/\s+/g, ' ').trim()
    if (rol === 'usuario') this.turnoUsuario = ''
    else this.turnoAgente = ''
    if (texto) this.callbacks.onTranscripcion?.(rol, texto)
  }

  private reproducirPcm(base64: string, frecuencia: number) {
    const bytes = Uint8Array.from(atob(base64), caracter => caracter.charCodeAt(0))
    const vista = new DataView(bytes.buffer)
    const cantidad = Math.floor(bytes.byteLength / 2)
    const AudioContextCtor = window.AudioContext || (window as typeof window & { webkitAudioContext?: typeof AudioContext }).webkitAudioContext
    if (!AudioContextCtor || cantidad === 0) return
    this.audioSalida ??= new AudioContextCtor()
    const buffer = this.audioSalida.createBuffer(1, cantidad, frecuencia)
    const canal = buffer.getChannelData(0)
    for (let i = 0; i < cantidad; i++) canal[i] = vista.getInt16(i * 2, true) / 32768
    const fuente = this.audioSalida.createBufferSource()
    fuente.buffer = buffer
    fuente.connect(this.audioSalida.destination)
    const inicio = Math.max(this.audioSalida.currentTime, this.siguienteAudio)
    fuente.start(inicio)
    this.siguienteAudio = inicio + buffer.duration
  }
}

interface RespuestaLive {
  setupComplete?: Record<string, never>
  serverContent?: {
    inputTranscription?: { text?: string }
    outputTranscription?: { text?: string }
    turnComplete?: boolean
    interrupted?: boolean
    modelTurn?: { parts?: Array<{ inlineData?: { data?: string; mimeType?: string } }> }
  }
}

function resamplear(entrada: Float32Array, origen: number, destino: number) {
  if (origen === destino) return entrada
  const proporcion = origen / destino
  const longitud = Math.max(1, Math.round(entrada.length / proporcion))
  const salida = new Float32Array(longitud)
  for (let i = 0; i < longitud; i++) {
    const posicion = i * proporcion
    const base = Math.floor(posicion)
    const siguiente = Math.min(base + 1, entrada.length - 1)
    const fraccion = posicion - base
    salida[i] = entrada[base] * (1 - fraccion) + entrada[siguiente] * fraccion
  }
  return salida
}

function convertirPcm16(entrada: Float32Array) {
  const salida = new Int16Array(entrada.length)
  for (let i = 0; i < entrada.length; i++) {
    const muestra = Math.max(-1, Math.min(1, entrada[i]))
    salida[i] = muestra < 0 ? muestra * 0x8000 : muestra * 0x7fff
  }
  return salida
}

function base64DesdeArrayBuffer(buffer: ArrayBuffer) {
  const bytes = new Uint8Array(buffer)
  let binario = ''
  const tamano = 0x8000
  for (let i = 0; i < bytes.length; i += tamano) binario += String.fromCharCode(...bytes.subarray(i, Math.min(i + tamano, bytes.length)))
  return btoa(binario)
}
