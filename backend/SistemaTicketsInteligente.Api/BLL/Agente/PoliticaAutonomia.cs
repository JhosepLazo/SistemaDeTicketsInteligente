/**
 * Archivo: PoliticaAutonomia.cs
 * Objetivo: Decidir, con reglas fijas y verificables, si una acción propuesta por el agente puede ejecutarse sin humano.
 * Responsabilidad: Aplicar la regla compuesta del plan de mejoras sobre los datos de la investigación y devolver todos los motivos que
 *   obligan a pedir la decisión de TI. El modelo de IA no interviene: solo propone; esta clase decide.
 * Dependencias: AgenteTIDatosAutonomia (Usp_TI_Agente_DatosAutonomia) e InvestigadorAgenteTI.ValidarParametros.
 * Flujo: investigación automática -> diagnóstico con acción -> Evaluar -> (permitida) simulación -> PrepararCambio autónomo -> ejecutor.
 * Consideraciones: Lógica pura, sin base de datos, para probarla caso por caso. Las condiciones son: 1) solicitud (SOL) clasificada por TI;
 *   2) acción de ejecución activa que no exige aprobación; 3) política AUTONOMA vigente para ese tipo y acción; 4) parámetros válidos
 *   contra el esquema del ejecutor; 5) precondiciones verificadas justo antes (la cumple el flujo: simulación revertida y el propio
 *   ejecutor dentro de su transacción); 6) acción reversible y riesgo no mayor al techo; 7) interruptor en AUTONOMO; 8) solicitante
 *   colaborador (USR). Además la confianza debe alcanzar el mínimo de la política. PrepararCambio vuelve a comprobarlas en la base.
 */

namespace SistemaTicketsInteligente.Api.BLL.Agente;

public static class PoliticaAutonomia
{
    /// <summary>Escala ordenada de riesgo (CK_TI_Accion_NivelRiesgo).</summary>
    public static readonly IReadOnlyList<string> NivelesRiesgo = ["MUY_BAJO", "BAJO", "MEDIO", "ALTO", "MUY_ALTO"];

    /// <summary>Posición del nivel en la escala (1 a 5); 0 si el valor no pertenece a la escala.</summary>
    public static int Nivel(string? riesgo)
    {
        var indice = NivelesRiesgo.ToList().IndexOf(riesgo?.Trim().ToUpperInvariant() ?? string.Empty);
        return indice + 1;
    }

    public static ResultadoPoliticaAutonomia Evaluar(AgenteTIDatosAutonomia datos)
    {
        var motivos = new List<string>();
        if (datos.ModoAgente != "AUTONOMO") motivos.Add("El interruptor del agente no está en modo AUTONOMO.");
        if (datos.EstadoSesion != "PENDIENTE_TI" || datos.SolicitudPendiente) motivos.Add("La investigación no espera una decisión o ya tiene una en curso.");
        if (datos.TipoTicket != "SOL" || string.IsNullOrWhiteSpace(datos.SubTipo))
            motivos.Add("Solo una solicitud (SOL) clasificada por TI puede ejecutarse sin humano.");
        if (string.IsNullOrWhiteSpace(datos.AccionCodigo) || datos.AccionTipo != "E" || datos.AccionEstado != "A" || datos.RequiereAprobacion)
            motivos.Add("La acción no es una acción de ejecución activa que pueda prescindir de la aprobación.");
        if (datos.ModoPolitica != "AUTONOMA" || datos.EstadoPolitica != "A")
            motivos.Add("La política de TI no libera esta acción para este tipo de ticket.");
        if (!datos.TieneEjecutor || string.IsNullOrWhiteSpace(datos.ParametrosEsquemaJson))
            motivos.Add("La acción no tiene un ejecutor con esquema de parámetros.");
        else if (!InvestigadorAgenteTI.ValidarParametros(datos.ParametrosEsquemaJson, datos.ParametrosJson, out _, out var motivo))
            motivos.Add($"Los parámetros propuestos no cumplen el esquema del ejecutor: {motivo}");
        if (!datos.Reversible) motivos.Add("La acción no es reversible.");
        var riesgo = Nivel(datos.NivelRiesgo);
        if (riesgo == 0 || riesgo > Nivel(datos.RiesgoMaximo)) motivos.Add("El riesgo de la acción supera el techo de riesgo autónomo configurado por TI.");
        if (datos.PerfilSolicitante != "USR") motivos.Add("El solicitante del ticket no es un colaborador (USR).");
        if (datos.ConfianzaMinima is null || datos.Confianza is null || datos.Confianza < datos.ConfianzaMinima)
            motivos.Add("La confianza del diagnóstico no alcanza el mínimo que fijó la política.");
        return new ResultadoPoliticaAutonomia(motivos.Count == 0, motivos);
    }
}
