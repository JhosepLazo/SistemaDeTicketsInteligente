/**
 * Archivo: ProcesadorAgenteTI.cs
 * Objetivo: Investigar automáticamente, en segundo plano, cuando el usuario termina de mostrar su error, y avisar a TI.
 * Responsabilidad: Recibir trabajos en una cola en memoria y ejecutarlos uno a uno con un ámbito de servicios propio.
 * Dependencias: Channel, IServiceScopeFactory, AsistenteTIBLL e IConfiguration (AgenteTI:InvestigacionAutomatica).
 * Flujo: ReproduccionUsuarioBLL / NuevoTicketBLL -> ColaAgenteTI.Encolar -> ProcesadorAgenteTI -> AsistenteTIBLL -> notificación TI.
 * Consideraciones: La investigación automática solo lee y diagnostica: nunca ejecuta cambios. Si el servidor se reinicia, los trabajos
 *   pendientes se pierden sin efectos; TI siempre puede pulsar "Investigar ahora" en la consola.
 */

using System.Threading.Channels;

namespace SistemaTicketsInteligente.Api.BLL;

/// <summary>Trabajo en segundo plano: investigar una sesión existente o un ticket nuevo con evidencia mostrada por el colaborador.</summary>
public sealed record TrabajoAgenteTI(long? SesionNumero, string? IncidenciaNumero, string? EvidenciaJson, string Motivo);

public sealed class ColaAgenteTI
{
    private readonly Channel<TrabajoAgenteTI> canal = Channel.CreateBounded<TrabajoAgenteTI>(new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly IConfiguration configuration;
    private readonly ILogger<ColaAgenteTI> logger;

    public ColaAgenteTI(IConfiguration configuration, ILogger<ColaAgenteTI> logger)
    {
        this.configuration = configuration;
        this.logger = logger;
    }

    public bool Habilitada => configuration.GetValue("AgenteTI:InvestigacionAutomatica", true);

    public void Encolar(TrabajoAgenteTI trabajo)
    {
        if (!Habilitada) return;
        if (!canal.Writer.TryWrite(trabajo)) logger.LogWarning("No se pudo encolar la investigación automática ({Motivo}).", trabajo.Motivo);
    }

    public ChannelReader<TrabajoAgenteTI> Lector => canal.Reader;
}

public sealed class ProcesadorAgenteTI : BackgroundService
{
    private readonly ColaAgenteTI cola;
    private readonly IServiceScopeFactory scopes;
    private readonly ILogger<ProcesadorAgenteTI> logger;

    public ProcesadorAgenteTI(ColaAgenteTI cola, IServiceScopeFactory scopes, ILogger<ProcesadorAgenteTI> logger)
    {
        this.cola = cola;
        this.scopes = scopes;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var trabajo in cola.Lector.ReadAllAsync(stoppingToken))
        {
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            limite.CancelAfter(TimeSpan.FromMinutes(8));
            try
            {
                using var scope = scopes.CreateScope();
                var asistente = scope.ServiceProvider.GetRequiredService<AsistenteTIBLL>();
                var sesion = await asistente.InvestigarAutomaticamenteAsync(trabajo, limite.Token);
                logger.LogInformation("Investigación automática {Motivo} completada (sesión {Sesion}).", trabajo.Motivo, sesion);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "La investigación automática {Motivo} falló.", trabajo.Motivo);
            }
        }
    }
}
