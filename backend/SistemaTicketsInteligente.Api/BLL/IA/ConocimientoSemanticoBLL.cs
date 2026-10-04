/**
 * Archivo: ConocimientoSemanticoBLL.cs
 * Objetivo: Encontrar guías y casos resueltos por significado, no solo por palabras coincidentes.
 * Responsabilidad: Mantener vectorizado el corpus autorizado (solo recalcula lo que cambió), vectorizar la consulta y ordenar por similitud coseno.
 * Dependencias: BaseDatos (Usp_TI_Conocimiento_Corpus y Usp_TI_Conocimiento_GuardarVector), OpenAIAsistenteClient (embeddings),
 *   IMemoryCache e IConfiguration.
 * Flujo: consulta -> corpus en caché -> vectores faltantes -> similitud -> resultados por encima del umbral.
 * Consideraciones: Los textos se envían al proveedor sin datos personales ni secretos. Si el proveedor no ofrece embeddings
 *   (por ejemplo Groq) o falla, devuelve una lista vacía y los asistentes siguen con la búsqueda por palabras.
 */

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace SistemaTicketsInteligente.Api.BLL.IA;

public sealed class ConocimientoSemanticoBLL(BaseDatos baseDatos, OpenAIAsistenteClient ia, IMemoryCache cache, IConfiguration configuration,
    ILogger<ConocimientoSemanticoBLL> logger)
{
    // Una sola indexación a la vez: evita vectorizar el mismo corpus en paralelo y gastar cuota del proveedor.
    private static readonly SemaphoreSlim Indexacion = new(1, 1);

    public bool Disponible => ia.SoportaEmbeddings;

    /// <param name="soloUsuario">true: solo artículos visibles para colaboradores; false: corpus de TI (incluye tickets resueltos).</param>
    /// <param name="excluir">Códigos que no deben aparecer (por ejemplo, el propio ticket investigado).</param>
    public async Task<List<ConocimientoSimilar>> BuscarAsync(string consulta, bool soloUsuario, int maximo, IReadOnlyCollection<string>? excluir, CancellationToken ct)
    {
        consulta = consulta?.Trim() ?? string.Empty;
        if (!Disponible || consulta.Length < 5) return [];

        try
        {
            var modelo = ia.ModeloEmbeddingsActivo;
            var corpus = await ObtenerCorpusIndexadoAsync(soloUsuario, modelo, ct);
            if (corpus.Count == 0) return [];

            var vectorConsulta = await ia.GenerarEmbeddingsAsync([$"task: search result | query: {RedactorDatosSensibles.RedactarParaIA(Limitar(consulta, 1500))}"], ct);
            if (vectorConsulta is null) return [];

            var umbral = Math.Clamp(configuration.GetValue<double?>("Conocimiento:UmbralSimilitud") ?? 0.55, 0.0, 0.99);
            return corpus
                .Where(x => x.Vector is not null && (excluir is null || !excluir.Contains(x.Codigo, StringComparer.OrdinalIgnoreCase)))
                .Select(x => (Item: x, Similitud: Coseno(vectorConsulta[0], x.Vector!)))
                .Where(x => x.Similitud >= umbral)
                .OrderByDescending(x => x.Similitud)
                .Take(Math.Clamp(maximo, 1, 20))
                .Select(x => new ConocimientoSimilar
                {
                    Origen = x.Item.Origen, Codigo = x.Item.Codigo, Titulo = x.Item.Titulo,
                    Similitud = Math.Round((decimal)x.Similitud * 100m, 1), Extracto = Limitar(x.Item.Texto, 700)
                })
                .ToList();
        }
        catch (Exception ex) when (ex is Microsoft.Data.SqlClient.SqlException or InvalidOperationException)
        {
            // Sin el script 31 o con la base no disponible, se sigue con la búsqueda por palabras.
            logger.LogWarning(ex, "La búsqueda semántica no está disponible.");
            return [];
        }
    }

    private async Task<List<ConocimientoItem>> ObtenerCorpusIndexadoAsync(bool soloUsuario, string modelo, CancellationToken ct)
    {
        var clave = $"conocimiento-semantico:{(soloUsuario ? "usuario" : "ti")}:{modelo}";
        if (cache.TryGetValue(clave, out List<ConocimientoItem>? enCache) && enCache is not null) return enCache;

        await Indexacion.WaitAsync(ct);
        try
        {
            if (cache.TryGetValue(clave, out enCache) && enCache is not null) return enCache;

            var corpus = await ObtenerCorpusAsync(soloUsuario, modelo, ct);
            var maximoPorVuelta = Math.Clamp(configuration.GetValue<int?>("Conocimiento:MaximoIndexacionPorConsulta") ?? 200, 10, 2000);
            var pendientes = new List<(ConocimientoItem Item, string Texto, string Huella)>();
            foreach (var item in corpus)
            {
                var texto = RedactorDatosSensibles.RedactarParaIA($"title: {item.Titulo} | text: {item.Texto}");
                var huella = Huella($"{modelo}|{OpenAIAsistenteClient.DimensionesEmbedding}|{texto}");
                if (item.Vector is null || item.Vector.Length != OpenAIAsistenteClient.DimensionesEmbedding || !string.Equals(item.Huella, huella, StringComparison.OrdinalIgnoreCase))
                {
                    item.Vector = null;
                    pendientes.Add((item, texto, huella));
                }
            }

            var completo = true;
            if (pendientes.Count > 0)
            {
                var lote = pendientes.Take(maximoPorVuelta).ToList();
                var vectores = await ia.GenerarEmbeddingsAsync(lote.Select(x => x.Texto).ToList(), ct);
                if (vectores is null) completo = false;
                else
                {
                    for (var i = 0; i < lote.Count; i++)
                    {
                        lote[i].Item.Vector = vectores[i];
                        lote[i].Item.Huella = lote[i].Huella;
                        await GuardarVectorAsync(lote[i].Item, modelo, vectores[i], ct);
                    }
                    completo = lote.Count == pendientes.Count;
                    logger.LogInformation("Se vectorizaron {Cantidad} elementos de conocimiento ({Alcance}).", lote.Count, soloUsuario ? "colaborador" : "TI");
                }
            }

            // Si quedó trabajo pendiente, la caché dura poco para continuar en la siguiente consulta.
            cache.Set(clave, corpus, completo ? TimeSpan.FromMinutes(10) : TimeSpan.FromMinutes(1));
            return corpus;
        }
        finally
        {
            Indexacion.Release();
        }
    }

    // Artículos y tickets resueltos autorizados, con el vector guardado para el modelo actual (si existe).
    private Task<List<ConocimientoItem>> ObtenerCorpusAsync(bool soloUsuario, string modelo, CancellationToken ct) =>
        baseDatos.LeerAsync("dbo.Usp_TI_Conocimiento_Corpus", p =>
        {
            p.Add("@lSoloUsuario", SqlDbType.Bit).Value = soloUsuario;
            p.Add("@cModelo", SqlDbType.VarChar, 60).Value = modelo;
        }, lector => lector.ListaAsync(f => new ConocimientoItem
        {
            Origen = f.Texto("Origen"), Codigo = f.Texto("Codigo"), Titulo = f.Texto("Titulo"), Texto = f.Texto("Texto"), Linea = f.Texto("Linea"),
            Item = f.Texto("Item"), Huella = f.Texto("Huella"),
            Vector = f["Vector"] is byte[] bytes && bytes.Length % 4 == 0 ? MemoryMarshal.Cast<byte, float>(bytes).ToArray() : null
        }, ct), ct, segundosMaximos: 60);

    private Task GuardarVectorAsync(ConocimientoItem item, string modelo, float[] vector, CancellationToken ct) =>
        baseDatos.EjecutarAsync("dbo.Usp_TI_Conocimiento_GuardarVector", p =>
        {
            p.Add("@cOrigen", SqlDbType.Char, 1).Value = item.Origen;
            p.Add("@cCodigo", SqlDbType.VarChar, 20).Value = item.Codigo;
            p.Add("@cModelo", SqlDbType.VarChar, 60).Value = modelo;
            p.Add("@cHuella", SqlDbType.Char, 64).Value = item.Huella;
            p.Add("@nDimensiones", SqlDbType.SmallInt).Value = (short)vector.Length;
            p.Add("@bVector", SqlDbType.VarBinary, -1).Value = MemoryMarshal.AsBytes(vector.AsSpan()).ToArray();
        }, ct);

    private static double Coseno(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;
        double producto = 0, normaA = 0, normaB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            producto += a[i] * b[i];
            normaA += a[i] * a[i];
            normaB += b[i] * b[i];
        }
        return normaA == 0 || normaB == 0 ? 0 : producto / (Math.Sqrt(normaA) * Math.Sqrt(normaB));
    }

    private static string Huella(string texto) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto)));

    private static string Limitar(string? texto, int maximo)
    {
        var valor = texto?.Trim() ?? string.Empty;
        return valor.Length <= maximo ? valor : valor[..maximo];
    }
}
