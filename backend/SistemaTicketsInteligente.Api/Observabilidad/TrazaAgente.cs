using System.Collections.Concurrent;
using System.Diagnostics;
using System.Data;
using Microsoft.Data.SqlClient;

namespace SistemaTicketsInteligente.Api.Observabilidad;

public sealed class TrazaAgente
{
    public static readonly AsyncLocal<TrazaAgente?> Actual = new();
    public Guid Correlacion { get; init; } = Guid.NewGuid();
    public long? Sesion { get; init; }
    public ConcurrentQueue<string> Pasos { get; } = new();
    public ConcurrentDictionary<Guid, long> Comandos { get; } = new();
}

// Captura nombres y tiempos reales; nunca texto SQL libre, parametros ni credenciales.
public sealed class SqlTrazaListener : IHostedService, IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>
{
    private readonly List<IDisposable> suscripciones = [];
    public Task StartAsync(CancellationToken ct) { suscripciones.Add(DiagnosticListener.AllListeners.Subscribe(this)); return Task.CompletedTask; }
    public Task StopAsync(CancellationToken ct) { foreach (var s in suscripciones) s.Dispose(); return Task.CompletedTask; }
    public void OnNext(DiagnosticListener listener)
    {
        if (listener.Name == "SqlClientDiagnosticListener") suscripciones.Add(listener.Subscribe(this, nombre => nombre.Contains("WriteCommand", StringComparison.Ordinal)));
    }
    public void OnNext(KeyValuePair<string, object?> evento)
    {
        var traza = TrazaAgente.Actual.Value;
        if (traza is null || evento.Value is null || traza.Pasos.Count >= 40) return;
        var tipo = evento.Value.GetType();
        if (tipo.GetProperty("Command")?.GetValue(evento.Value) is not SqlCommand comando || comando.CommandType != CommandType.StoredProcedure) return;
        if (tipo.GetProperty("OperationId")?.GetValue(evento.Value) is not Guid operacion) return;
        if (evento.Key.EndsWith("Before", StringComparison.Ordinal))
        {
            traza.Comandos[operacion] = Stopwatch.GetTimestamp();
            var metodos = new StackTrace().GetFrames().Select(f => f.GetMethod()).Where(m => m?.DeclaringType?.Namespace is string ns && (ns.EndsWith(".BLL") || ns.EndsWith(".DAO")))
                .Select(m => NombreMetodo(m!)).Distinct().Take(4);
            foreach (var metodo in metodos) traza.Pasos.Enqueue("METODO " + metodo);
        }
        else if (traza.Comandos.TryRemove(operacion, out var inicio))
        {
            var resultado = evento.Key.EndsWith("Error", StringComparison.Ordinal) ? "ERROR" : "OK";
            traza.Pasos.Enqueue($"SP {comando.CommandText} | {Stopwatch.GetElapsedTime(inicio).TotalMilliseconds:0.0} ms | {resultado}");
        }
    }
    // Los métodos async se compilan como <Metodo>d__N.MoveNext dentro de una clase anidada; se recupera el nombre real.
    private static string NombreMetodo(System.Reflection.MethodBase metodo)
    {
        var tipo = metodo.DeclaringType!;
        if (tipo.Name.StartsWith('<') && tipo.DeclaringType is not null)
        {
            var fin = tipo.Name.IndexOf('>');
            return $"{tipo.DeclaringType.Name}.{(fin > 1 ? tipo.Name[1..fin] : metodo.Name)}";
        }
        return $"{tipo.Name}.{metodo.Name}";
    }

    public void OnCompleted() { }
    public void OnError(Exception error) { }
}
