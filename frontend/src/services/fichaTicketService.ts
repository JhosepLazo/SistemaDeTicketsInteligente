/**
 * Archivo: fichaTicketService.ts
 * Objetivo: Reunir los tipos y las reglas de la ficha estructurada de un tipo de ticket (por ejemplo, la del requerimiento).
 * Responsabilidad: Elegir los campos del tipo, validar las respuestas con las mismas reglas que el backend y armar el JSON que viaja
 *   con el ticket.
 * Dependencias: Ninguna (sin red).
 * Flujo: NuevoTicketPage -> validarFicha / fichaJson -> nuevoTicketApi; MisTickets y Gestión de Tickets muestran DatoFichaTicket.
 * Consideraciones: Refleja NuevoTicketBLL.NormalizarFicha y Usp_TI_Registrar_Incidencia: respuestas de texto, obligatorias completas,
 *   largos permitidos, SI o NO y fechas aaaa-mm-dd. El backend vuelve a validar todo.
 */

/** Campo de la ficha de un tipo de ticket (TI_PlantillaCampo). */
export interface CampoFichaTicket {
  tipo: string
  campo: string
  bloque: string
  orden: number
  pregunta: string
  ayuda: string
  tipoDato: 'TEXTO' | 'TEXTO_LARGO' | 'FECHA' | 'SI_NO'
  obligatorio: boolean
  longitudMinima: number
  longitudMaxima: number
}

/** Respuesta registrada en la ficha de un ticket (TI_IncidenciaDato). */
export interface DatoFichaTicket {
  campo: string
  bloque: string
  orden: number
  pregunta: string
  tipoDato: string
  valor: string
  fuente: string
  fechaRegistro: string
}

export type RespuestasFicha = Record<string, string>

/** Campos del tipo elegido, en el orden de la plantilla. */
export const camposDelTipo = (campos: CampoFichaTicket[], tipo: string) =>
  campos.filter(x => x.tipo === tipo).sort((a, b) => a.orden - b.orden)

/** Agrupa campos o respuestas por bloque, conservando el orden. */
export function agruparPorBloque<T extends { bloque: string }>(lista: T[]) {
  const grupos = new Map<string, T[]>()
  for (const elemento of lista) grupos.set(elemento.bloque, [...(grupos.get(elemento.bloque) ?? []), elemento])
  return [...grupos.entries()]
}

function fechaValida(valor: string) {
  const partes = /^(\d{4})-(\d{2})-(\d{2})$/.exec(valor)
  if (!partes) return false
  const [anio, mes, dia] = partes.slice(1).map(Number)
  const fecha = new Date(Date.UTC(anio, mes - 1, dia))
  return fecha.getUTCFullYear() === anio && fecha.getUTCMonth() === mes - 1 && fecha.getUTCDate() === dia
}

function normalizar(campo: CampoFichaTicket, valor: string | undefined) {
  const texto = (valor ?? '').trim()
  return campo.tipoDato === 'SI_NO' ? texto.toUpperCase() : texto
}

/** Primer problema de la ficha, con la pregunta afectada; vacío si está completa. */
export function validarFicha(campos: CampoFichaTicket[], tipo: string, respuestas: RespuestasFicha) {
  const plantilla = camposDelTipo(campos, tipo)
  for (const campo of plantilla) {
    const valor = normalizar(campo, respuestas[campo.campo])
    if (!valor) continue
    if (valor.length < campo.longitudMinima || valor.length > campo.longitudMaxima)
      return `Revisa la respuesta de la ficha: ${campo.pregunta} Debe tener entre ${Math.max(campo.longitudMinima, 1)} y ${campo.longitudMaxima} caracteres.`
    if ((campo.tipoDato === 'SI_NO' && valor !== 'SI' && valor !== 'NO') || (campo.tipoDato === 'FECHA' && !fechaValida(valor)))
      return `Revisa la respuesta de la ficha: ${campo.pregunta}`
  }
  const faltante = plantilla.find(x => x.obligatorio && !normalizar(x, respuestas[x.campo]))
  return faltante ? `Completa la ficha: ${faltante.pregunta}` : ''
}

/** JSON con las respuestas no vacías del tipo elegido; undefined si el tipo no tiene ficha o no se respondió nada. */
export function fichaJson(campos: CampoFichaTicket[], tipo: string, respuestas: RespuestasFicha) {
  const datos: RespuestasFicha = {}
  for (const campo of camposDelTipo(campos, tipo)) {
    const valor = normalizar(campo, respuestas[campo.campo])
    if (valor) datos[campo.campo] = valor
  }
  return Object.keys(datos).length > 0 ? JSON.stringify(datos) : undefined
}

/** Cantidad de campos del tipo respondidos y total, para el resumen. */
export function avanceFicha(campos: CampoFichaTicket[], tipo: string, respuestas: RespuestasFicha) {
  const plantilla = camposDelTipo(campos, tipo)
  return { respondidos: plantilla.filter(x => normalizar(x, respuestas[x.campo])).length, total: plantilla.length }
}
