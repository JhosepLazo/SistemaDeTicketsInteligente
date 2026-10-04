/**
 * Archivo: RecursosSoporteBLL.cs
 * Objetivo: Ofrecer al colaborador formatos descargables y artículos de ayuda publicados mientras registra su ticket.
 * Responsabilidad: Consultar el autoservicio vigente y entregar un formato solo desde la carpeta autorizada.
 * Dependencias: BaseDatos (Usp_TI_Obtener_RecursosSoporteUsuario y Usp_TI_Obtener_FormatoSoporteUsuario) y Archivos.
 * Flujo: NuevoTicketPage -> RecursosSoporteController -> RecursosSoporteBLL -> Stored Procedures / uploads/formatos.
 * Consideraciones: Solo se expone contenido marcado como visible y vigente; el frontend nunca conoce rutas físicas.
 */

namespace SistemaTicketsInteligente.Api.BLL;

public sealed class RecursosSoporteBLL(BaseDatos baseDatos)
{
    public Task<RecursosSoporteRespuesta> ObtenerAsync(CancellationToken ct) =>
        baseDatos.LeerAsync("dbo.Usp_TI_Obtener_RecursosSoporteUsuario", _ => { }, async lector =>
        {
            var respuesta = new RecursosSoporteRespuesta();
            respuesta.Formatos = await lector.ListaAsync(f => new RecursoFormato
            {
                FormatoCodigo = f.Texto("FormatoCodigo"), Titulo = f.Texto("Titulo"), Descripcion = f.Texto("Descripcion"),
                NombreOriginal = f.Texto("NombreOriginal"), TipoMime = f.Texto("TipoMime"), TipoTicket = f.Texto("TipoTicket")
            }, ct);
            respuesta.Articulos = await lector.ListaAsync(f => new RecursoArticulo
            {
                ConocimientoCodigo = f.Texto("ConocimientoCodigo"), Titulo = f.Texto("Titulo"), Problema = f.Texto("Problema"),
                Sintomas = f.Texto("Sintomas"), Solucion = f.Texto("Solucion"), Procedimiento = f.Texto("Procedimiento"), Tipo = f.Texto("Tipo")
            }, ct);
            return respuesta;
        }, ct);

    public async Task<ArchivoDescarga> ObtenerArchivoAsync(string formatoCodigo, CancellationToken ct)
    {
        var codigo = Validacion.Codigo(formatoCodigo, 20, "El formato indicado no es válido.");
        return await baseDatos.LeerAsync("dbo.Usp_TI_Obtener_FormatoSoporteUsuario", p => p.Add("@cFormatoCodigo", SqlDbType.VarChar, 20).Value = codigo,
            lector => lector.FilaAsync(f => Archivos.Descarga(f, Archivos.Formatos, "La ruta del formato no es válida.", "El archivo del formato no se encuentra disponible."), ct), ct)
            ?? throw new KeyNotFoundException("El formato ya no se encuentra disponible.");
    }
}
