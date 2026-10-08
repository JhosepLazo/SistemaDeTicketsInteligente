/**
 * Archivo: ProcesadorAgenteTI.cs
 * Objetivo: Investigar automáticamente, en segundo plano, cuando el usuario termina de mostrar su error, y avisar a TI.
 * Responsabilidad: Recibir trabajos en una cola en memoria y ejecutarlos uno a uno con un ámbito de servicios propio.
 * Dependencias: Channel, IServiceScopeFactory, AsistenteTIBLL e IConfiguration (AgenteTI:InvestigacionAutomatica).
 * Flujo: ReproduccionUsuarioBLL / NuevoTicketBLL -> investigación guardada con la marca INVESTIGACION_AUTOMATICA -> ColaAgenteTI.Encolar
 *   -> ProcesadorAgenteTI -> AsistenteTIBLL -> notificación TI.
 * Consideraciones: La cola solo acelera: la fuente de verdad es la base. La investigación y su evidencia ya están guardadas antes de
 *   encolar, y MantenimientoAgenteTI vuelve a encolar las marcadas que nadie terminó (por ejemplo, tras un reinicio de la API).
 *   La investigación automática solo lee y diagnostica; ejecuta una acción únicamente si la política de autonomía lo permite.
 */

using System.Threading.Channels;

namespace SistemaTicketsInteligente.Api.BLL.Agente;

/// <summary>Trabajo en segundo plano: investigar una sesión que ya tiene la evidencia del colaborador.</summary>
public sealed record TrabajoAgenteTI(long SesionNumero, string Motivo);

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
                // Se detiene la API: sin marca de fin, el mantenimiento retoma la investigación al volver a iniciar.
                break;
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("La investigación automática {Motivo} superó los 8 minutos y se detuvo.", trabajo.Motivo);
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<AsistenteTIBLL>()
                    .RegistrarFinAutomaticoAsync(trabajo.SesionNumero, false, "La investigación automática superó su tiempo máximo; puedes reintentar con Investigar ahora.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "La investigación automática {Motivo} falló.", trabajo.Motivo);
            }
        }
    }
}
