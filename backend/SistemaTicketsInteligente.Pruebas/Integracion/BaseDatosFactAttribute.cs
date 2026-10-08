/**
 * Archivo: BaseDatosFactAttribute.cs
 * Objetivo: Marcar las pruebas que necesitan la base GestionSistemas instalada y omitirlas cuando no está disponible.
 * Responsabilidad: Correr la prueba solo con PRUEBAS_BASE_DATOS=1 (lectura) o, si Escribe = true, solo con PRUEBAS_FLUJOS=1.
 * Dependencias: xUnit.
 * Flujo: descubrimiento de pruebas -> variable de entorno -> prueba ejecutada u omitida con el motivo.
 * Consideraciones: Las pruebas que escriben (crean tickets) están pensadas para el contenedor desechable del CI, nunca para una base
 *   con datos que se quieran conservar.
 */

namespace SistemaTicketsInteligente.Pruebas.Integracion;

public sealed class BaseDatosFactAttribute : FactAttribute
{
    /// <summary>La prueba crea o modifica datos: exige PRUEBAS_FLUJOS=1.</summary>
    public bool Escribe { get; set; }

    public override string? Skip
    {
        get
        {
            if (base.Skip is not null) return base.Skip;
            if (Escribe && Environment.GetEnvironmentVariable("PRUEBAS_FLUJOS") != "1")
                return "Crea datos: requiere PRUEBAS_FLUJOS=1 y una base desechable (contenedor del CI).";
            if (!Escribe && Environment.GetEnvironmentVariable("PRUEBAS_BASE_DATOS") != "1")
                return "Lee la base: requiere PRUEBAS_BASE_DATOS=1 y GestionSistemas instalada (00 a 40).";
            return null;
        }
        set => base.Skip = value;
    }
}
