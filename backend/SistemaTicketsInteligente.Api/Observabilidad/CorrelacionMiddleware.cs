using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using SistemaTicketsInteligente.Api.DAO;

namespace SistemaTicketsInteligente.Api.Observabilidad;

public sealed class CorrelacionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext contexto, AsistenteTIDAO dao, ILogger<CorrelacionMiddleware> logger)
    {
        var usuario = contexto.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var area = contexto.User.FindFirstValue("Area");
        Guid? correlacion = null;
        long? sesion = null;
        if (usuario is not null && area is not null && long.TryParse(contexto.Request.Headers["X-Agente-Sesion"], out var numero) && numero > 0)
        {
            try
            {
                correlacion = await dao.ResolverCorrelacionAsync(usuario, area, numero, contexto.RequestAborted);
                if (correlacion.HasValue) sesion = numero;
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogWarning("No se pudo vincular la traza de investigacion. Tipo {Tipo}.", ex.GetType().Name); }
        }
        var traza = new TrazaAgente { Correlacion = correlacion ?? Guid.NewGuid(), Sesion = sesion };
        TrazaAgente.Actual.Value = traza;
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
            // Las consultas periódicas de la campana no forman parte del proceso reproducido y solo agregarían ruido a la traza.
            if (sesion.HasValue && usuario is not null && !ruta.StartsWith("/api/asistente/", StringComparison.Ordinal) && !ruta.StartsWith("/api/autenticacion", StringComparison.Ordinal)
                && !ruta.StartsWith("/api/notificaciones", StringComparison.Ordinal) && !ruta.StartsWith("/api/reproducciones", StringComparison.Ordinal))
            {
                using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                try { await dao.RegistrarTelemetriaAsync(usuario, sesion.Value, string.Join("\n", traza.Pasos), JsonSerializer.Serialize(new { correlacion = traza.Correlacion, requestId = contexto.TraceIdentifier, duracionMs = duracion, resultado }), limite.Token); }
                catch (Exception ex) { logger.LogWarning("No se pudo persistir la traza {Correlacion}. Tipo {Tipo}.", traza.Correlacion, ex.GetType().Name); }
            }
        }
    }
}
