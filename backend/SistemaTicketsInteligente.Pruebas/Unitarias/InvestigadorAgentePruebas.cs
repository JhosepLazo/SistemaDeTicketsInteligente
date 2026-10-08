/**
 * Archivo: InvestigadorAgentePruebas.cs
 * Objetivo: Comprobar los controles deterministas que el backend aplica a lo que propone el modelo durante una investigación.
 * Responsabilidad: Validar los parámetros de herramientas y acciones contra el esquema del catálogo, descartar hallazgos con referencias
 *   inventadas y normalizar la confianza informada por el modelo.
 * Dependencias: InvestigadorAgenteTI (métodos estáticos) y AgenteTIHallazgo.
 * Flujo: propuesta del modelo -> ValidarParametros / HallazgosVerificados / NormalizarConfianza -> dato aceptado o rechazado.
 * Consideraciones: El modelo solo propone; si estos controles fallan, la herramienta o la acción no se ejecutan.
 */

namespace SistemaTicketsInteligente.Pruebas.Unitarias;

public sealed class InvestigadorAgentePruebas
{
    private const string Esquema = """
        {"type":"object","properties":{
            "usuario":{"type":"string","minLength":2,"maxLength":20},
            "dias":{"type":"integer","minimum":1,"maximum":30}
        },"required":["usuario"],"additionalProperties":false}
        """;

    [Theory]
    [InlineData("""{"usuario":"  USR001 "}""", """{"usuario":"USR001"}""")]
    [InlineData("""{"dias":7,"usuario":"USR001"}""", """{"usuario":"USR001","dias":7}""")]
    [InlineData("""{"usuario":"USR001","dias":5.0}""", """{"usuario":"USR001","dias":5}""")]
    [InlineData("""{"usuario":"USR001","dias":null}""", """{"usuario":"USR001"}""")]
    public void ParametrosValidosSeNormalizanEnElOrdenDelEsquema(string argumentos, string esperado)
    {
        var valido = InvestigadorAgenteTI.ValidarParametros(Esquema, argumentos, out var normalizados, out var motivo);

        Assert.True(valido, motivo);
        Assert.Equal(esperado, normalizados);
    }

    [Theory]
    [InlineData("{}", "Falta el parámetro obligatorio \"usuario\"")]
    [InlineData("", "Falta el parámetro obligatorio \"usuario\"")]
    [InlineData("""{"usuario":"USR001","rol":"ADM"}""", "\"rol\" no existe")]
    [InlineData("""{"usuario":12}""", "debe ser texto")]
    [InlineData("""{"usuario":"X"}""", "entre 2 y 20")]
    [InlineData("""{"usuario":"USUARIO_DEMASIADO_LARGO_PARA_EL_CAMPO"}""", "entre 2 y 20")]
    [InlineData("""{"usuario":"USR001","dias":"7"}""", "número entero")]
    [InlineData("""{"usuario":"USR001","dias":2.5}""", "número entero")]
    [InlineData("""{"usuario":"USR001","dias":0}""", "entre 1 y 30")]
    [InlineData("""{"usuario":"USR001","dias":31}""", "entre 1 y 30")]
    [InlineData("""["usuario"]""", "objeto JSON")]
    [InlineData("""{"usuario":""", "JSON válido")]
    public void ParametrosInvalidosSeRechazan(string argumentos, string fragmento)
    {
        var valido = InvestigadorAgenteTI.ValidarParametros(Esquema, argumentos, out _, out var motivo);

        Assert.False(valido);
        Assert.Contains(fragmento, motivo);
    }

    [Fact]
    public void TipoNoSoportadoEnElEsquemaSeRechaza()
    {
        const string esquema = """{"type":"object","properties":{"filtro":{"type":"object"}}}""";

        var valido = InvestigadorAgenteTI.ValidarParametros(esquema, """{"filtro":{"a":1}}""", out _, out var motivo);

        Assert.False(valido);
        Assert.Contains("no está soportado", motivo);
    }

    [Fact]
    public void HallazgosConReferenciasInventadasSeDescartan()
    {
        AgenteTIHallazgo[] hallazgos =
        [
            new() { Fuente = "HERRAMIENTA", Referencia = "DIAG_TICKET_ESTADO", Descripcion = "El ticket quedó en PA." },
            new() { Fuente = "CODIGO_FUENTE", Referencia = "NuevoTicketBLL.cs:120", Descripcion = "La validación corta antes del SP." },
            new() { Fuente = "BASE_DATOS", Referencia = "Usp_TI_Registrar_Incidencia", Descripcion = "El SP exige la ficha." },
            new() { Fuente = "CODIGO_FUENTE", Referencia = "ArchivoInexistente.cs:10", Descripcion = "Inventado por el modelo." },
            new() { Fuente = "HERRAMIENTA", Referencia = "DIAG_TICKET_ESTADO", Descripcion = "" },
            new() { Fuente = "HERRAMIENTA", Referencia = "", Descripcion = "Sin referencia." },
            new() { Fuente = "HERRAMIENTA", Referencia = new string('x', 201), Descripcion = "Referencia desmedida." }
        ];
        const string fuentes = "Se leyó NuevoTicketBLL.cs completo y la definición de Usp_TI_Registrar_Incidencia.";

        var verificados = InvestigadorAgenteTI.HallazgosVerificados(hallazgos, ["DIAG_TICKET_ESTADO"], fuentes);

        Assert.Equal(new[] { "DIAG_TICKET_ESTADO", "NuevoTicketBLL.cs:120", "Usp_TI_Registrar_Incidencia" }, verificados.Select(x => x.Referencia));
    }

    [Theory]
    [InlineData(0.85, 85)]
    [InlineData(1, 100)]
    [InlineData(72, 72)]
    [InlineData(140, 100)]
    [InlineData(-5, 0)]
    public void ConfianzaSeLlevaALaEscalaDeCeroACien(double informada, double esperada) =>
        Assert.Equal((decimal)esperada, InvestigadorAgenteTI.NormalizarConfianza((decimal)informada));
}
