/**
 * Archivo: CorrelacionMiddleware.cs
 * Objetivo: Correlacionar cada petición con la investigación del agente que el usuario está reproduciendo.
 * Responsabilidad: Asignar un X-Correlation-ID, usarlo como identificador de la petición (registros, error y auditoría), abrir la traza
 *   de la petición y, si pertenece a una investigación, guardarla como evidencia.
 * Dependencias: BaseDatos (Usp_TI_Agente_Correlacion y Usp_TI_Agente_Telemetria), TrazaAgente y la cabecera X-Agente-Sesion.
 * Flujo: Petición -> resolver sesión -> ejecutar la API -> registrar la traza TRAZA_BACKEND de la investigación.
 * Consideraciones: Las consultas de la campana, del asistente y de la autenticación no se guardan: no forman parte del proceso reproducido.
 *   Un fallo al guardar la traza nunca afecta la respuesta al usuario.
 */

using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;

namespace SistemaTicketsInteligente.Api.Comun;

public sealed class CorrelacionMiddleware(RequestDelegate next)
{
    private static readonly string[] RutasSinTraza = ["/api/asistente/", "/api/autenticacion", "/api/notificaciones", "/api/reproducciones"];

    public async Task InvokeAsync(HttpContext contexto, BaseDatos baseDatos, ILogger<CorrelacionMiddleware> logger)
    {
        var usuario = contexto.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var area = contexto.User.FindFirstValue("Area");
        Guid? correlacion = null;
        long? sesion = null;
        if (usuario is not null && area is not null && long.TryParse(contexto.Request.Headers["X-Agente-Sesion"], out var numero) && numero > 0)
        {
            try
            {
                correlacion = await baseDatos.EscalarAsync("dbo.Usp_TI_Agente_Correlacion", p =>
                {
                    p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;
                    p.Add("@cArea", SqlDbType.Char, 3).Value = area;
                    p.Add("@nSesionNumero", SqlDbType.BigInt).Value = numero;
                }, contexto.RequestAborted) is Guid valor ? valor : null;
                if (correlacion.HasValue) sesion = numero;
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogWarning("No se pudo vincular la traza de investigacion. Tipo {Tipo}.", ex.GetType().Name); }
        }

        var traza = new TrazaAgente { Correlacion = correlacion ?? Guid.NewGuid(), Sesion = sesion };
        TrazaAgente.Actual.Value = traza;
        // Un solo identificador por petición: el idSeguimiento de un error y la auditoría usan este mismo valor.
        var requestId = contexto.TraceIdentifier;
        contexto.TraceIdentifier = traza.Correlacion.ToString();
        contexto.Response.Headers["X-Correlation-ID"] = traza.Correlacion.ToString();
        var inicio = Stopwatch.GetTimestamp();
        var ruta = contexto.Request.Path.Value ?? "";
        var endpoint = contexto.GetEndpoint()?.DisplayName ?? ruta;
        traza.Pasos.Enqueue($"API {contexto.Request.Method} {ruta} | {endpoint}");
        string? excepcion = null;
        try { await next(contexto); }
        catch (Exception ex) { excepcion = ex.GetType().Name; throw; }
        finally
        {
            TrazaAgente.Actual.Value = null;
            var duracion = Stopwatch.GetElapsedTime(inicio).TotalMilliseconds;
            var resultado = excepcion is null ? contexto.Response.StatusCode.ToString() : excepcion;
            traza.Pasos.Enqueue($"RESULTADO {resultado} | {duracion:0.0} ms");
            logger.LogInformation("API {Metodo} {Ruta} {Resultado} {Duracion} ms Correlacion {Correlacion}", contexto.Request.Method, ruta, resultado, duracion, traza.Correlacion);
            if (sesion.HasValue && usuario is not null && !RutasSinTraza.Any(x => ruta.StartsWith(x, StringComparison.Ordinal)))
                await GuardarTrazaAsync(baseDatos, logger, usuario, sesion.Value, traza, requestId, duracion, resultado);
        }
    }

    private static async Task GuardarTrazaAsync(BaseDatos baseDatos, ILogger logger, string usuario, long sesion, TrazaAgente traza, string requestId, double duracion, string resultado)
    {
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            await baseDatos.EjecutarAsync("dbo.Usp_TI_Agente_Telemetria", p =>
            {
                p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;
                p.Add("@nSesionNumero", SqlDbType.BigInt).Value = sesion;
                p.Add("@cContenido", SqlDbType.NVarChar, -1).Value = string.Join("\n", traza.Pasos);
                p.Add("@cDatosJson", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(new { correlacion = traza.Correlacion, requestId, duracionMs = duracion, resultado });
            }, limite.Token);
        }
        catch (Exception ex) { logger.LogWarning("No se pudo persistir la traza {Correlacion}. Tipo {Tipo}.", traza.Correlacion, ex.GetType().Name); }
    }
}
