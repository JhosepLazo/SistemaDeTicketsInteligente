/**
 * Archivo: PoliticaAutonomiaPruebas.cs
 * Objetivo: Comprobar la regla compuesta que decide si el agente puede ejecutar una acción sin humano.
 * Responsabilidad: Partir de un caso que cumple todas las condiciones y romper una por vez: cada ruptura debe denegar con su motivo.
 * Dependencias: PoliticaAutonomia y AgenteTIDatosAutonomia.
 * Flujo: caso permitido -> una condición alterada -> Evaluar -> denegado con el motivo esperado.
 * Consideraciones: Es lógica pura; Usp_TI_Agente_PrepararCambio vuelve a comprobar estas reglas en la base (PruebasFuncionales.sql).
 */

namespace SistemaTicketsInteligente.Pruebas.Unitarias;

public sealed class PoliticaAutonomiaPruebas
{
    private const string EsquemaUsuario =
        """{"type":"object","properties":{"usuario":{"type":"string","minLength":2,"maxLength":20}},"required":["usuario"],"additionalProperties":false}""";

    /// <summary>Solicitud clasificada, acción reversible de riesgo mínimo liberada por TI y agente en AUTONOMO: todo se cumple.</summary>
    private static AgenteTIDatosAutonomia CasoPermitido() => new()
    {
        SesionNumero = 1,
        EstadoSesion = "PENDIENTE_TI",
        Confianza = 92m,
        AccionCodigo = "ACC-007",
        ParametrosJson = """{"usuario":"USR001"}""",
        SolicitudPendiente = false,
        TipoTicket = "SOL",
        SubTipo = "ACC",
        PerfilSolicitante = "USR",
        AccionTipo = "E",
        AccionEstado = "A",
        RequiereAprobacion = false,
        Reversible = true,
        NivelRiesgo = "MUY_BAJO",
        TieneEjecutor = true,
        ParametrosEsquemaJson = EsquemaUsuario,
        ModoPolitica = "AUTONOMA",
        ConfianzaMinima = 80m,
        EstadoPolitica = "A",
        ModoAgente = "AUTONOMO",
        RiesgoMaximo = "MUY_BAJO"
    };

    [Fact]
    public void CasoQueCumpleTodasLasCondicionesSePermite()
    {
        var resultado = PoliticaAutonomia.Evaluar(CasoPermitido());

        Assert.True(resultado.Permitida, string.Join(" | ", resultado.Motivos));
        Assert.Empty(resultado.Motivos);
    }

    public static TheoryData<string, Action<AgenteTIDatosAutonomia>, string> Rupturas => new()
    {
        { "agente en ASISTIDO", x => x.ModoAgente = "ASISTIDO", "interruptor" },
        { "agente en SOMBRA", x => x.ModoAgente = "SOMBRA", "interruptor" },
        { "agente APAGADO", x => x.ModoAgente = "APAGADO", "interruptor" },
        { "sesión ya resuelta", x => x.EstadoSesion = "EJECUTADA", "decisión" },
        { "aprobación pendiente", x => x.SolicitudPendiente = true, "decisión" },
        { "ticket es incidencia", x => x.TipoTicket = "INC", "(SOL)" },
        { "ticket es requerimiento", x => x.TipoTicket = "REQ", "(SOL)" },
        { "solicitud sin clasificar", x => x.SubTipo = " ", "(SOL)" },
        { "sin acción", x => x.AccionCodigo = "", "acción de ejecución" },
        { "acción de lectura", x => x.AccionTipo = "L", "acción de ejecución" },
        { "acción inactiva", x => x.AccionEstado = "I", "acción de ejecución" },
        { "acción exige aprobación", x => x.RequiereAprobacion = true, "acción de ejecución" },
        { "política de aprobación", x => x.ModoPolitica = "APROBACION", "política" },
        { "política prohibida", x => x.ModoPolitica = "PROHIBIDA", "política" },
        { "política inactiva", x => x.EstadoPolitica = "I", "política" },
        { "sin ejecutor", x => x.TieneEjecutor = false, "ejecutor" },
        { "ejecutor sin esquema", x => x.ParametrosEsquemaJson = "", "ejecutor" },
        { "parámetro obligatorio ausente", x => x.ParametrosJson = "{}", "esquema" },
        { "parámetro no declarado", x => x.ParametrosJson = """{"usuario":"USR001","rol":"ADM"}""", "esquema" },
        { "parámetro fuera de largo", x => x.ParametrosJson = """{"usuario":"X"}""", "esquema" },
        { "parámetros que no son JSON", x => x.ParametrosJson = "usuario=USR001", "esquema" },
        { "acción no reversible", x => x.Reversible = false, "reversible" },
        { "riesgo sobre el techo", x => x.NivelRiesgo = "BAJO", "techo" },
        { "riesgo desconocido", x => x.NivelRiesgo = "CRITICO", "techo" },
        { "techo sin configurar", x => x.RiesgoMaximo = "", "techo" },
        { "solicitante de TI", x => x.PerfilSolicitante = "TEC", "(USR)" },
        { "confianza bajo el mínimo", x => x.Confianza = 79.99m, "confianza" },
        { "sin confianza", x => x.Confianza = null, "confianza" },
        { "política sin mínimo", x => x.ConfianzaMinima = null, "confianza" }
    };

    [Theory]
    [MemberData(nameof(Rupturas))]
    public void CadaCondicionIncumplidaDeniegaConSuMotivo(string caso, Action<AgenteTIDatosAutonomia> romper, string fragmentoMotivo)
    {
        var datos = CasoPermitido();
        romper(datos);

        var resultado = PoliticaAutonomia.Evaluar(datos);

        Assert.False(resultado.Permitida, $"Se permitió con {caso}.");
        Assert.Contains(resultado.Motivos, x => x.Contains(fragmentoMotivo, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ConfianzaIgualAlMinimoYRiesgoIgualAlTechoSePermiten()
    {
        var datos = CasoPermitido();
        datos.Confianza = 80m;
        datos.NivelRiesgo = "BAJO";
        datos.RiesgoMaximo = "BAJO";

        Assert.True(PoliticaAutonomia.Evaluar(datos).Permitida);
    }

    [Fact]
    public void VariasRupturasSeInformanTodas()
    {
        var datos = CasoPermitido();
        datos.ModoAgente = "ASISTIDO";
        datos.TipoTicket = "INC";
        datos.Reversible = false;

        var resultado = PoliticaAutonomia.Evaluar(datos);

        Assert.False(resultado.Permitida);
        Assert.Equal(3, resultado.Motivos.Count);
    }

    [Theory]
    [InlineData("MUY_BAJO", 1)]
    [InlineData("bajo", 2)]
    [InlineData(" MEDIO ", 3)]
    [InlineData("ALTO", 4)]
    [InlineData("MUY_ALTO", 5)]
    [InlineData("CRITICO", 0)]
    [InlineData("", 0)]
    [InlineData(null, 0)]
    public void NivelUbicaElRiesgoEnLaEscala(string? riesgo, int esperado) => Assert.Equal(esperado, PoliticaAutonomia.Nivel(riesgo));
}
