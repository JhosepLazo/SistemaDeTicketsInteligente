/**
 * Archivo: geminiLiveApi.ts
 * Objetivo: Conectar el navegador con Gemini Live usando el token efímero emitido por el backend.
 * Responsabilidad: Compartir pantalla, transmitir audio PCM/video y reproducir la respuesta de voz sin exponer la API key permanente.
 * Dependencias: WebSocket de Gemini Live, Web Audio y el token que entrega asistenteTIApi / asistenteUsuarioApi / reproduccionApi.
 * Flujo: AsistenteTIPage / MostrarErrorLive / ReproduccionUsuarioPage -> GeminiLiveSesion -> Gemini Live.
 * Consideraciones: Live solo observa y conversa; no llama endpoints de cambio ni posee autoridad para ejecutar acciones productivas.
 *   Sus funciones (registrar_paso, registrar_error) solo guardan evidencia mediante el callback onFuncion.
 *   Con token restringido, modelo, instrucción y funciones vienen fijados desde el backend y el navegador solo abre la conexión.
 */

import type { AgenteTILiveTokenRespuesta } from './asistenteTIApi'

export interface GeminiLiveCallbacks {
  onEstado?: (estado: string) => void
  onTranscripcion?: (rol: 'usuario' | 'agente', texto: string) => void
  onError?: (mensaje: string) => void
  onPantallaFinalizada?: () => void
  /** El proveedor cerró la conexión (límite de sesión o red); la captura local se detiene. */
  onDesconexion?: () => void
  /** El modelo llamó una función Live; devuelve el texto de resultado que se le responde. */
  onFuncion?: (nombre: string, argumentos: Record<string, unknown>) => Promise<string> | string
}

// Captura PCM en el hilo de audio; ScriptProcessorNode está obsoleto y bloquea el hilo principal.
const CODIGO_WORKLET = `
class CapturaPcm extends AudioWorkletProcessor {
  constructor() { super(); this.buffer = new Float32Array(4096); this.posicion = 0 }
  process(entradas) {
    const canal = entradas[0] && entradas[0][0]
    if (canal) {
      for (let i = 0; i < canal.length; i++) {
        this.buffer[this.posicion++] = canal[i]
        if (this.posicion === this.buffer.length) { this.port.postMessage(this.buffer.slice(0)); this.posicion = 0 }
      }
    }
    return true
  }
}
registerProcessor('captura-pcm', CapturaPcm)
`

export class GeminiLiveSesion {
  private socket: WebSocket | null = null
  private pantalla: MediaStream | null = null
  private microfono: MediaStream | null = null
  private audioEntrada: AudioContext | null = null
  private audioSalida: AudioContext | null = null
  private procesador: ScriptProcessorNode | null = null
  private worklet: AudioWorkletNode | null = null
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
    // 5 cuadros por segundo hacen fluida la grabación para TI; a Gemini se le sigue enviando 1 cuadro por segundo.
    this.pantalla = await navigator.mediaDevices.getDisplayMedia({ video: { frameRate: { ideal: 5, max: 5 } }, audio: false })
    this.pantalla.getVideoTracks()[0]?.addEventListener('ended', () => {
      if (!this.deteniendo) {
        this.callbacks.onPantallaFinalizada?.()
        void this.detener()
      }
    })

    try {
      this.microfono = await navigator.mediaDevices.getUserMedia({
        audio: { echoCancellation: true, noiseSuppression: true },
        video: false,
      })
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
    this.microfono?.getAudioTracks().forEach(track => {
      track.enabled = !silenciado
    })
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
    this.worklet?.port.close()
    this.worklet?.disconnect()
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
    this.worklet = null
    this.origenMicrofono = null
    this.callbacks.onEstado?.('Sesión Live finalizada')
  }

  private async conectar(configuracion: AgenteTILiveTokenRespuesta) {
    const url = `${configuracion.webSocketUrl}?access_token=${encodeURIComponent(configuracion.token)}`
    this.socket = new WebSocket(url)
    // Gemini Live envía sus mensajes JSON como tramas binarias; se reciben como ArrayBuffer y se decodifican en UTF-8.
    this.socket.binaryType = 'arraybuffer'

    await new Promise<void>((resolve, reject) => {
      if (!this.socket) return reject(new Error('No fue posible crear la conexión Live.'))
      const timeout = window.setTimeout(() => reject(new Error('Gemini Live no confirmó la conexión dentro del tiempo esperado.')), 12000)

      this.socket.onopen = () => {
        this.callbacks.onEstado?.('Conectado. Configurando observación multimodal…')
        // Con token restringido el servidor ignora este setup y aplica el fijado en el token.
        const setup = configuracion.restringido
          ? { model: `models/${configuracion.modelo}` }
          : {
              model: `models/${configuracion.modelo}`,
              generationConfig: { responseModalities: ['AUDIO'] },
              systemInstruction: { parts: [{ text: configuracion.instruccionSistema }] },
              tools: configuracion.herramientas ?? undefined,
              inputAudioTranscription: {},
              outputAudioTranscription: {},
              contextWindowCompression: { slidingWindow: {} },
            }
        this.socket?.send(JSON.stringify({ setup }))
      }

      this.socket.onmessage = event => {
        let respuesta: RespuestaLive
        try {
          respuesta = JSON.parse(
            typeof event.data === 'string' ? event.data : new TextDecoder().decode(event.data as ArrayBuffer),
          ) as RespuestaLive
        } catch {
          this.callbacks.onError?.('Se recibió una respuesta Live que no pudo interpretarse.')
          return
        }
        try {
          if (respuesta.setupComplete && !this.listo) {
            window.clearTimeout(timeout)
            this.listo = true
            this.iniciarVideo()
            void this.iniciarMicrofono()
            this.enviarTexto('La pantalla ya está compartida. Guíame para reproducir el problema exactamente hasta que aparezca el error.')
            this.callbacks.onEstado?.('Live activo · pantalla y conversación en tiempo real')
            resolve()
          }
          this.procesarRespuesta(respuesta)
        } catch (error) {
          console.error('Error procesando la respuesta Live', error)
          this.callbacks.onError?.('No se pudo procesar una respuesta de Gemini Live; la sesión continúa.')
        }
      }

      this.socket.onerror = () => {
        window.clearTimeout(timeout)
        reject(new Error('Se produjo un error en la conexión con Gemini Live.'))
      }
      this.socket.onclose = evento => {
        window.clearTimeout(timeout)
        const estabaListo = this.listo
        this.listo = false
        // Gemini explica en el motivo de cierre por qué rechazó la sesión (token vencido, configuración inválida, cuota).
        const motivo = evento.reason ? ` Motivo: ${evento.reason}` : ''
        if (!estabaListo) reject(new Error(`Gemini Live cerró la conexión antes de iniciar la observación.${motivo}`))
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

  private async iniciarMicrofono() {
    if (!this.microfono || !this.listo) return
    const AudioContextCtor =
      window.AudioContext || (window as typeof window & { webkitAudioContext?: typeof AudioContext }).webkitAudioContext
    if (!AudioContextCtor) return
    this.audioEntrada = new AudioContextCtor()
    this.origenMicrofono = this.audioEntrada.createMediaStreamSource(this.microfono)

    if (this.audioEntrada.audioWorklet) {
      const url = URL.createObjectURL(new Blob([CODIGO_WORKLET], { type: 'application/javascript' }))
      try {
        await this.audioEntrada.audioWorklet.addModule(url)
        if (!this.audioEntrada || !this.origenMicrofono) return
        this.worklet = new AudioWorkletNode(this.audioEntrada, 'captura-pcm', { numberOfInputs: 1, numberOfOutputs: 1, channelCount: 1 })
        this.worklet.port.onmessage = evento => this.enviarAudio(evento.data as Float32Array)
        this.origenMicrofono.connect(this.worklet)
        // La salida es silencio; conectarla al destino garantiza que el navegador procese el nodo.
        this.worklet.connect(this.audioEntrada.destination)
        return
      } catch {
        // Navegadores sin AudioWorklet utilizable continúan con el procesador clásico.
      } finally {
        URL.revokeObjectURL(url)
      }
    }

    if (!this.audioEntrada || !this.origenMicrofono) return
    this.procesador = this.audioEntrada.createScriptProcessor(4096, 1, 1)
    this.procesador.onaudioprocess = evento => this.enviarAudio(evento.inputBuffer.getChannelData(0))
    this.origenMicrofono.connect(this.procesador)
    this.procesador.connect(this.audioEntrada.destination)
  }

  private enviarAudio(entrada: Float32Array) {
    if (!this.listo || !this.socket || this.socket.readyState !== WebSocket.OPEN || !this.audioEntrada) return
    const muestras = resamplear(entrada, this.audioEntrada.sampleRate, 16000)
    const pcm = convertirPcm16(muestras)
    this.socket.send(
      JSON.stringify({ realtimeInput: { audio: { data: base64DesdeArrayBuffer(pcm.buffer), mimeType: 'audio/pcm;rate=16000' } } }),
    )
  }

  private async atenderFunciones(llamadas: Array<{ id?: string; name?: string; args?: Record<string, unknown> }>) {
    const respuestas = []
    for (const llamada of llamadas) {
      if (!llamada.name) continue
      let resultado = 'Función no disponible.'
      try {
        resultado = this.callbacks.onFuncion ? await this.callbacks.onFuncion(llamada.name, llamada.args ?? {}) : resultado
      } catch (error) {
        resultado = error instanceof Error ? `No se pudo registrar: ${error.message}` : 'No se pudo registrar la evidencia.'
      }
      respuestas.push({ id: llamada.id, name: llamada.name, response: { resultado } })
    }
    if (respuestas.length === 0 || !this.socket || this.socket.readyState !== WebSocket.OPEN) return
    this.socket.send(JSON.stringify({ toolResponse: { functionResponses: respuestas } }))
  }

  private procesarRespuesta(respuesta: RespuestaLive) {
    if (respuesta.toolCall?.functionCalls?.length) void this.atenderFunciones(respuesta.toolCall.functionCalls)
    // Gemini avisa antes de cerrar la conexión (límite de ~10 minutos); la evidencia ya registrada no se pierde.
    if (respuesta.goAway)
      this.callbacks.onEstado?.(
        'La sesión de voz se cerrará en unos segundos por el límite de tiempo de Gemini. Lo registrado se conserva; luego podrás reanudarla.',
      )
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
    const AudioContextCtor =
      window.AudioContext || (window as typeof window & { webkitAudioContext?: typeof AudioContext }).webkitAudioContext
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
  toolCall?: { functionCalls?: Array<{ id?: string; name?: string; args?: Record<string, unknown> }> }
  goAway?: { timeLeft?: string }
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
