/**
 * Archivo: FichaTicketPruebas.cs
 * Objetivo: Comprobar la validación de la ficha que el colaborador completa al registrar un requerimiento.
 * Responsabilidad: Aceptar una ficha completa y normalizarla; rechazar campos ajenos, valores que no son texto, obligatorios vacíos,
 *   largos fuera de rango, fechas inválidas y respuestas SI/NO incorrectas.
 * Dependencias: NuevoTicketBLL.NormalizarFicha y CampoFichaTicket.
 * Flujo: plantilla del tipo + JSON del navegador -> NormalizarFicha -> JSON normalizado o ArgumentException con la pregunta.
 * Consideraciones: Usp_TI_Registrar_Incidencia aplica las mismas reglas en la base; esta validación da el mensaje claro antes.
 */

namespace SistemaTicketsInteligente.Pruebas.Unitarias;

public sealed class FichaTicketPruebas
{
    private static readonly CampoFichaTicket[] Plantilla =
    [
        new() { Tipo = "REQ", Campo = "OBJETIVO_NEGOCIO", Orden = 10, Pregunta = "¿Qué objetivo de negocio persigue?", TipoDato = "TEXTO_LARGO", Obligatorio = true, LongitudMinima = 10, LongitudMaxima = 2000 },
        new() { Tipo = "REQ", Campo = "VALIDA_JEFATURA", Orden = 20, Pregunta = "¿Tu jefatura validó el pedido?", TipoDato = "SI_NO", Obligatorio = true, LongitudMinima = 2, LongitudMaxima = 2 },
        new() { Tipo = "REQ", Campo = "FECHA_LIMITE", Orden = 30, Pregunta = "¿Para qué fecha lo necesitas?", TipoDato = "FECHA", Obligatorio = false, LongitudMinima = 10, LongitudMaxima = 10 },
        new() { Tipo = "REQ", Campo = "EJEMPLO", Orden = 40, Pregunta = "Da un ejemplo del caso normal.", TipoDato = "TEXTO", Obligatorio = false, LongitudMinima = 5, LongitudMaxima = 300 },
        new() { Tipo = "SOL", Campo = "ACCESO", Orden = 10, Pregunta = "¿Qué acceso necesitas?", TipoDato = "TEXTO", Obligatorio = true, LongitudMinima = 3, LongitudMaxima = 200 }
    ];

    private static string? Normalizar(string tipo, string? ficha) => NuevoTicketBLL.NormalizarFicha(Plantilla, tipo, ficha);

    [Fact]
    public void FichaCompletaSeNormaliza()
    {
        var resultado = Normalizar("REQ", """
            {"objetivo_negocio":"  Reducir el tiempo de cierre mensual  ","VALIDA_JEFATURA":"si","FECHA_LIMITE":"2026-12-15","EJEMPLO":""}
            """);

        var ficha = JsonDocument.Parse(resultado!).RootElement;
        Assert.Equal("Reducir el tiempo de cierre mensual", ficha.GetProperty("OBJETIVO_NEGOCIO").GetString());
        Assert.Equal("SI", ficha.GetProperty("VALIDA_JEFATURA").GetString());
        Assert.Equal("2026-12-15", ficha.GetProperty("FECHA_LIMITE").GetString());
        Assert.False(ficha.TryGetProperty("EJEMPLO", out _), "Las respuestas vacías no se guardan.");
    }

    [Theory]
    [InlineData("INC", null)]
    [InlineData("INC", "")]
    [InlineData("INC", "{}")]
    public void TipoSinPlantillaNoGuardaFicha(string tipo, string? ficha) => Assert.Null(Normalizar(tipo, ficha));

    [Theory]
    [InlineData("REQ", null, "Completa la ficha: ¿Qué objetivo")]
    [InlineData("REQ", """{"OBJETIVO_NEGOCIO":"Reducir el cierre mensual"}""", "Completa la ficha: ¿Tu jefatura")]
    [InlineData("REQ", """{"OBJETIVO_NEGOCIO":"   ","VALIDA_JEFATURA":"SI"}""", "Completa la ficha: ¿Qué objetivo")]
    [InlineData("REQ", """{"OBJETIVO_NEGOCIO":"Corto","VALIDA_JEFATURA":"SI"}""", "Debe tener entre 10 y 2000")]
    [InlineData("REQ", """{"OBJETIVO_NEGOCIO":"Reducir el cierre mensual","VALIDA_JEFATURA":"Tal vez"}""", "¿Tu jefatura")]
    [InlineData("REQ", """{"OBJETIVO_NEGOCIO":"Reducir el cierre mensual","VALIDA_JEFATURA":"OK"}""", "¿Tu jefatura")]
    [InlineData("REQ", """{"OBJETIVO_NEGOCIO":"Reducir el cierre mensual","VALIDA_JEFATURA":"SI","FECHA_LIMITE":"2026-02-30"}""", "¿Para qué fecha")]
    [InlineData("REQ", """{"OBJETIVO_NEGOCIO":"Reducir el cierre mensual","VALIDA_JEFATURA":"SI","FECHA_LIMITE":"15/12/2026"}""", "¿Para qué fecha")]
    [InlineData("REQ", """{"OBJETIVO_NEGOCIO":"Reducir el cierre mensual","VALIDA_JEFATURA":"SI","ACCESO":"ERP"}""", "no corresponden al tipo")]
    [InlineData("REQ", """{"OBJETIVO_NEGOCIO":"Reducir el cierre mensual","VALIDA_JEFATURA":"SI","CAMPO_INVENTADO":"x"}""", "no corresponden al tipo")]
    [InlineData("REQ", """{"OBJETIVO_NEGOCIO":12345678901,"VALIDA_JEFATURA":"SI"}""", "formato válido")]
    [InlineData("REQ", """{"OBJETIVO_NEGOCIO":["a","b"],"VALIDA_JEFATURA":"SI"}""", "formato válido")]
    [InlineData("REQ", """["OBJETIVO_NEGOCIO"]""", "formato válido")]
    [InlineData("REQ", "{no es json", "formato válido")]
    [InlineData("INC", """{"OBJETIVO_NEGOCIO":"Reducir el cierre mensual"}""", "no corresponden al tipo")]
    public void FichaInvalidaSeRechazaConLaPreguntaAfectada(string tipo, string? ficha, string fragmento)
    {
        var error = Assert.Throws<ArgumentException>(() => Normalizar(tipo, ficha));

        Assert.Contains(fragmento, error.Message);
    }

    [Fact]
    public void RespuestaDemasiadoLargaSeRechaza()
    {
        var ficha = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["OBJETIVO_NEGOCIO"] = new string('a', 2001),
            ["VALIDA_JEFATURA"] = "NO"
        });

        var error = Assert.Throws<ArgumentException>(() => Normalizar("REQ", ficha));

        Assert.Contains("2000", error.Message);
    }
}
