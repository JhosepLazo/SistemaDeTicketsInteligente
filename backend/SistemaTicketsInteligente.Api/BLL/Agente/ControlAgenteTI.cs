/**
 * Archivo: ControlAgenteTI.cs
 * Objetivo: Entregar los parámetros operativos que TI cambia sin desplegar: el interruptor del agente, los umbrales y los plazos.
 * Responsabilidad: Leer TI_Parametro con una caché corta para que todo el backend (cola, investigación, decisión y mantenimiento)
 *   respete el mismo modo y los mismos valores, y olvidarla cuando un ADM los cambia.
 * Dependencias: BaseDatos (Usp_TI_Obtener_ParametrosAgente) e IMemoryCache.
 * Flujo: ConfiguracionTIBLL guarda -> Invalidar(); AsistenteTIBLL, ProcesadorAgenteTI y MantenimientoAgenteTI -> ObtenerAsync.
 * Consideraciones: Sin el script 35 instalado se usan los valores de antes (ASISTIDO, confianza 60, sin vencimientos ni autocierre).
 *   Los procedimientos vuelven a comprobar el modo antes de ejecutar: la caché nunca habilita algo que la base niega.
 */

using System.Globalization;
using Microsoft.Extensions.Caching.Memory;

namespace SistemaTicketsInteligente.Api.BLL.Agente;

public sealed class ControlAgenteTI(BaseDatos baseDatos, IMemoryCache cache, ILogger<ControlAgenteTI> logger)
{
    private const string Clave = "agente-ti:parametros";
    private static readonly TimeSpan Vigencia = TimeSpan.FromSeconds(30);
    private static readonly ParametrosAgenteTI ValoresAnteriores = new("ASISTIDO", "MUY_BAJO", 60m, null, null);

    public async Task<ParametrosAgenteTI> ObtenerAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(Clave, out ParametrosAgenteTI? vigentes) && vigentes is not null) return vigentes;
        Dictionary<string, string> valores;
        try
        {
            valores = await baseDatos.LeerAsync("dbo.Usp_TI_Obtener_ParametrosAgente", _ => { },
                async lector => (await lector.ListaAsync(f => (Parametro: f.Texto("Parametro"), Valor: f.Texto("Valor")), ct))
                    .ToDictionary(x => x.Parametro, x => x.Valor, StringComparer.OrdinalIgnoreCase), ct);
        }
        catch (SqlException ex) when (ex.Number == 2812)
        {
            logger.LogWarning("Los parámetros del agente no están instalados (script 35); se usan los valores anteriores.");
            return ValoresAnteriores;
        }

        var parametros = new ParametrosAgenteTI(
            Texto(valores, "AGENTE_MODO", ValoresAnteriores.Modo),
            Texto(valores, "AGENTE_RIESGO_MAXIMO_AUTONOMO", ValoresAnteriores.RiesgoMaximoAutonomo),
            Entero(valores, "AGENTE_CONFIANZA_MINIMA_PROPUESTA") ?? ValoresAnteriores.ConfianzaMinimaPropuesta,
            Entero(valores, "APROBACION_VIGENCIA_HORAS"),
            Entero(valores, "TICKET_AUTOCIERRE_PV_DIAS"));
        cache.Set(Clave, parametros, Vigencia);
        return parametros;
    }

    /// <summary>Un ADM cambió un parámetro: la próxima lectura va a la base.</summary>
    public void Invalidar() => cache.Remove(Clave);

    private static string Texto(Dictionary<string, string> valores, string clave, string porDefecto) =>
        valores.TryGetValue(clave, out var valor) && !string.IsNullOrWhiteSpace(valor) ? valor.Trim().ToUpperInvariant() : porDefecto;

    private static int? Entero(Dictionary<string, string> valores, string clave) =>
        valores.TryGetValue(clave, out var valor) && int.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numero) ? numero : null;
}
