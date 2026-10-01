/** Ejecuta una petición y convierte fallos de red del navegador en un mensaje funcional. */
export async function peticionHttp(url: string, opciones: RequestInit | undefined, mensajeConexion: string): Promise<Response> {
  try {
    return await fetch(url, opciones)
  } catch {
    throw new Error(mensajeConexion)
  }
}
