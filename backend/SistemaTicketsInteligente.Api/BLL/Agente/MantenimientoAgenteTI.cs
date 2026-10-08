/**
 * Archivo: MantenimientoAgenteTI.cs
 * Objetivo: Mantener coherente el trabajo del agente y de los tickets aunque la API se reinicie o un proceso quede a medias.
 * Responsabilidad: Cada 10 minutos (y poco después de iniciar): cerrar como error las ejecuciones que quedaron en proceso, volver a
 *   encolar las investigaciones automáticas que nadie terminó y, si TI lo configuró, cerrar los tickets que esperan demasiado la
 *   validación del usuario.
 * Dependencias: BaseDatos (Usp_TI_Agente_ReconciliarEjecuciones, Usp_TI_Agente_ListarInvestigacionesPendientes,
 *   Usp_TI_Cerrar_TicketsSinValidacion), ColaAgenteTI, ControlAgenteTI e IConfiguration (AgenteTI:Mantenimiento).
 * Flujo: temporizador -> procedimientos de mantenimiento -> cola del agente.
 * Consideraciones: Nunca vuelve a ejecutar una acción: una ejecución interrumpida queda como error para que TI verifique el estado real.
 *   Una ejecución tarda como máximo 60 s; a los 15 minutos sin resultado se considera interrumpida. Una investigación encolada se
 *   retoma si su marca tiene más de 10 minutos. Con el agente APAGADO no se retoma nada; las marcas esperan a que TI lo encienda.
 */

namespace SistemaTicketsInteligente.Api.BLL.Agente;

public sealed class MantenimientoAgenteTI(BaseDatos baseDatos, ColaAgenteTI cola, ControlAgenteTI control, IConfiguration configuration,
    ILogger<MantenimientoAgenteTI> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(10);
    private const int MinutosEjecucionInterrumpida = 15;
    private const int MinutosInvestigacionPendiente = 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("AgenteTI:Mantenimiento", true)) return;
        try
        {
            // Primero deja que la API termine de iniciar.
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            using var temporizador = new PeriodicTimer(Intervalo);
            do await EjecutarAsync(stoppingToken);
            while (await temporizador.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task EjecutarAsync(CancellationToken ct)
    {
        try
        {
            await baseDatos.EjecutarAsync("dbo.Usp_TI_Agente_ReconciliarEjecuciones", p => p.Add("@nMinutos", SqlDbType.Int).Value = MinutosEjecucionInterrumpida, ct);

            var parametros = await control.ObtenerAsync(ct);
            if (cola.Habilitada && !parametros.Apagado)
            {
                var pendientes = await baseDatos.LeerAsync("dbo.Usp_TI_Agente_ListarInvestigacionesPendientes",
                    p => p.Add("@nMinutos", SqlDbType.Int).Value = MinutosInvestigacionPendiente,
                    lector => lector.ListaAsync(f => f.Largo("SesionNumero"), ct), ct);
                foreach (var sesion in pendientes) cola.Encolar(new TrabajoAgenteTI(sesion, $"recuperada-{sesion}"));
                if (pendientes.Count > 0) logger.LogInformation("Se retomaron {Cantidad} investigaciones automáticas pendientes.", pendientes.Count);
            }

            if (parametros.AutocierreValidacionDias is int dias and > 0)
                await baseDatos.EjecutarAsync("dbo.Usp_TI_Cerrar_TicketsSinValidacion", p => p.Add("@nDias", SqlDbType.Int).Value = dias, ct);
        }
        // Sin los scripts 35 instalados, o con la base no disponible, se reintenta en el siguiente ciclo.
        catch (Exception ex) when (ex is SqlException or InvalidOperationException)
        {
            logger.LogWarning("El mantenimiento del agente no pudo completarse. Tipo {Tipo}.", ex.GetType().Name);
        }
    }
}
