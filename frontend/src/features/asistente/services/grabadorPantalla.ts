/**
 * Archivo: grabadorPantalla.ts
 * Objetivo: Grabar la pantalla compartida durante la reproducción para que TI pueda verla y el agente analizarla.
 * Responsabilidad: Grabar solo el video del stream compartido (sin audio), a baja tasa de bits y con duración máxima.
 * Consideraciones: Solo se usa con consentimiento explícito. 200 kbps mantienen 10 minutos en ~15 MB,
 *   por debajo de los 18 MB que Gemini acepta para analizar la grabación de forma directa.
 */

export class GrabadorPantalla {
  private grabador: MediaRecorder | null = null
  private partes: Blob[] = []
  private inicio = 0
  private duracion = 0
  private limite: number | null = null
  private ultimo: Blob | null = null
  private detenido = false
  private esperando: ((grabacion: Blob | null) => void) | null = null

  static get soportado() {
    return typeof window !== 'undefined' && typeof window.MediaRecorder !== 'undefined'
  }

  /** Empieza a grabar las pistas de video del stream. Devuelve false si el navegador no puede grabar. */
  iniciar(stream: MediaStream, maximoMinutos = 10) {
    const pistas = stream.getVideoTracks()
    if (!GrabadorPantalla.soportado || pistas.length === 0) return false
    const tipo = ['video/webm;codecs=vp9', 'video/webm;codecs=vp8', 'video/webm'].find(x => MediaRecorder.isTypeSupported(x))
    try {
      this.grabador = new MediaRecorder(new MediaStream(pistas), { mimeType: tipo, videoBitsPerSecond: 200_000 })
    } catch {
      this.grabador = null
      return false
    }
    this.partes = []
    this.ultimo = null
    this.detenido = false
    this.grabador.ondataavailable = evento => { if (evento.data.size > 0) this.partes.push(evento.data) }
    // Si el usuario deja de compartir, el navegador detiene la grabación por su cuenta: se conserva para subirla después.
    this.grabador.onstop = () => {
      this.duracion = Math.round((Date.now() - this.inicio) / 1000)
      // El tipo real lo decide el navegador (Safari graba MP4); el servidor lo verifica por la firma del archivo.
      const tipoReal = (this.grabador?.mimeType || 'video/webm').split(';')[0]
      this.ultimo = this.partes.length > 0 ? new Blob(this.partes, { type: tipoReal }) : null
      this.partes = []
      this.detenido = true
      this.esperando?.(this.ultimo)
      this.esperando = null
    }
    this.grabador.start(5000)
    this.inicio = Date.now()
    this.limite = window.setTimeout(() => { if (this.grabador?.state === 'recording') this.grabador.stop() }, maximoMinutos * 60_000)
    return true
  }

  get activo() {
    return this.grabador?.state === 'recording'
  }

  /** Duración en segundos de la última grabación detenida. */
  get segundos() {
    return this.duracion
  }

  /** Detiene la grabación y entrega el video (una sola vez); null si no hubo nada que grabar. */
  detener(): Promise<Blob | null> {
    if (this.limite !== null) { window.clearTimeout(this.limite); this.limite = null }
    const grabador = this.grabador
    if (!grabador) return Promise.resolve(null)
    const entregar = () => {
      const grabacion = this.ultimo
      this.ultimo = null
      this.grabador = null
      this.esperando = null
      return grabacion
    }
    if (this.detenido) return Promise.resolve(entregar())
    // El evento "stop" puede llegar después de que el navegador ya marcó la grabación como inactiva: se espera hasta 3 s.
    return new Promise(resolve => {
      const espera = window.setTimeout(() => resolve(entregar()), 3000)
      this.esperando = () => { window.clearTimeout(espera); resolve(entregar()) }
      if (grabador.state !== 'inactive') grabador.stop()
    })
  }
}
