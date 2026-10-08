/**
 * Archivo: ConfiguracionTIBLL.Agente.cs
 * Objetivo: Administrar desde Configuración TI el control del agente y las fichas de los tipos de ticket.
 * Responsabilidad: Mostrar y guardar los parámetros operativos (interruptor, techo de riesgo, umbral, vigencia, autocierre), la política
 *   de autonomía por tipo y acción, los atributos de las acciones del catálogo y los campos de cada ficha.
 * Dependencias: BaseDatos (Usp_TI_Obtener_ControlAgente, Usp_TI_Guardar_ParametroTI, Usp_TI_Guardar_PoliticaAutonomia,
 *   Usp_TI_Guardar_AccionCatalogo, Usp_TI_Obtener_PlantillasTI, Usp_TI_Guardar_CampoPlantilla) y ControlAgenteTI.
 * Flujo: ConfiguracionTIController -> ConfiguracionTIBLL -> Stored Procedures -> ControlAgenteTI.Invalidar.
 * Consideraciones: Cualquier operador TI consulta; solo un ADM cambia (el controlador y cada procedimiento lo exigen). Cada cambio queda
 *   auditado con su valor anterior.
 */

namespace SistemaTicketsInteligente.Api.BLL;

public sealed partial class ConfiguracionTIBLL
{
    private static readonly HashSet<string> TiposDatoFicha = new(StringComparer.Ordinal) { "TEXTO", "TEXTO_LARGO", "FECHA", "SI_NO" };

    public Task<ControlAgenteTIRespuesta> ObtenerControlAgenteAsync(string usuario, string area, CancellationToken ct)
    {
        var (usuarioValido, areaValida) = (Validacion.Usuario(usuario), Validacion.Area(area));
        return baseDatos.LeerAsync("dbo.Usp_TI_Obtener_ControlAgente", p => Identidad(p, usuarioValido, areaValida), async lector => new ControlAgenteTIRespuesta
        {
            Parametros = await lector.ListaAsync(f => new ParametroTI
            {
                Parametro = f.Texto("Parametro"), Valor = f.Texto("Valor"), Descripcion = f.Texto("Descripcion"),
                UltimoUsuario = f.Texto("UltimoUsuario"), UltimaFechaModif = f.FechaNula("UltimaFechaModif")
            }, ct),
            Politica = await lector.ListaAsync(f => new PoliticaAutonomiaTI
            {
                Tipo = f.Texto("Tipo"), TipoDescripcion = f.Texto("TipoDescripcion"), AccionCodigo = f.Texto("AccionCodigo"), AccionNombre = f.Texto("AccionNombre"),
                Modo = f.Texto("Modo"), ConfianzaMinima = f.DecimalNulo("ConfianzaMinima"), Estado = f.Texto("Estado"), Configurada = f.Booleano("Configurada")
            }, ct),
            Acciones = await lector.ListaAsync(f => new AccionCatalogoTI
            {
                AccionCodigo = f.Texto("AccionCodigo"), Nombre = f.Texto("Nombre"), Descripcion = f.Texto("Descripcion"), Tipo = f.Texto("Tipo"),
                NivelRiesgo = f.Texto("NivelRiesgo"), RequiereAprobacion = f.Booleano("RequiereAprobacion"), Reversible = f.Booleano("Reversible"),
                Estado = f.Texto("Estado"), TieneEjecutor = f.Booleano("TieneEjecutor"), Procedimiento = f.Texto("Procedimiento"),
                MaximoFilas = f.Entero("MaximoFilas"), ParametrosDescripcion = f.Texto("ParametrosDescripcion")
            }, ct)
        }, ct);
    }

    public async Task GuardarParametroAsync(string usuario, string area, GuardarParametroTISolicitud s, CancellationToken ct)
    {
        var (usuarioValido, areaValida) = (Validacion.Usuario(usuario), Validacion.Area(area));
        var parametro = Validacion.Codigo(s.Parametro, 50, "Selecciona un parámetro válido.");
        var valor = Validacion.Opcional(s.Valor, 200, "El valor")?.ToUpperInvariant();
        await baseDatos.EjecutarAsync("dbo.Usp_TI_Guardar_ParametroTI", p =>
        {
            Identidad(p, usuarioValido, areaValida);
            p.Add("@cParametro", SqlDbType.VarChar, 50).Value = parametro;
            p.Add("@cValor", SqlDbType.NVarChar, 200).Value = BaseDatos.Opcional(valor);
            p.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = TrazaAgente.CorrelacionActual;
        }, ct);
        control.Invalidar();
    }

    public async Task GuardarPoliticaAsync(string usuario, string area, GuardarPoliticaAutonomiaSolicitud s, CancellationToken ct)
    {
        var (usuarioValido, areaValida) = (Validacion.Usuario(usuario), Validacion.Area(area));
        var tipo = Exacto(s.Tipo, "tipo");
        var accion = Variable(s.AccionCodigo, 50, "código de acción");
        var modo = s.Modo.Trim().ToUpperInvariant();
        if (modo is not ("AUTONOMA" or "APROBACION" or "PROHIBIDA")) throw new ArgumentException("El modo debe ser AUTONOMA, APROBACION o PROHIBIDA.");
        if (modo == "AUTONOMA" && s.ConfianzaMinima is not (>= 60 and <= 100)) throw new ArgumentException("Indica la confianza mínima del diagnóstico (60 a 100).");
        var estado = Validacion.Estado(s.Estado);
        await baseDatos.EjecutarAsync("dbo.Usp_TI_Guardar_PoliticaAutonomia", p =>
        {
            Identidad(p, usuarioValido, areaValida);
            p.Add("@cTipo", SqlDbType.Char, 3).Value = tipo;
            p.Add("@cAccionCodigo", SqlDbType.VarChar, 50).Value = accion;
            p.Add("@cModo", SqlDbType.VarChar, 20).Value = modo;
            var confianza = p.Add("@nConfianzaMinima", SqlDbType.Decimal);
            confianza.Precision = 5;
            confianza.Scale = 2;
            confianza.Value = modo == "AUTONOMA" ? s.ConfianzaMinima!.Value : DBNull.Value;
            p.Add("@cEstado", SqlDbType.VarChar, 2).Value = estado;
            p.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = TrazaAgente.CorrelacionActual;
        }, ct);
        control.Invalidar();
    }

    public async Task GuardarAccionAsync(string usuario, string area, GuardarAccionCatalogoSolicitud s, CancellationToken ct)
    {
        var (usuarioValido, areaValida) = (Validacion.Usuario(usuario), Validacion.Area(area));
        var accion = Variable(s.AccionCodigo, 50, "código de acción");
        var riesgo = s.NivelRiesgo.Trim().ToUpperInvariant();
        if (PoliticaAutonomia.Nivel(riesgo) == 0) throw new ArgumentException("El nivel de riesgo debe ser MUY_BAJO, BAJO, MEDIO, ALTO o MUY_ALTO.");
        var estado = Validacion.Estado(s.Estado);
        await baseDatos.EjecutarAsync("dbo.Usp_TI_Guardar_AccionCatalogo", p =>
        {
            Identidad(p, usuarioValido, areaValida);
            p.Add("@cAccionCodigo", SqlDbType.VarChar, 50).Value = accion;
            p.Add("@cNivelRiesgo", SqlDbType.VarChar, 20).Value = riesgo;
            p.Add("@lRequiereAprobacion", SqlDbType.Bit).Value = s.RequiereAprobacion;
            p.Add("@lReversible", SqlDbType.Bit).Value = s.Reversible;
            p.Add("@cEstado", SqlDbType.VarChar, 2).Value = estado;
            p.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = TrazaAgente.CorrelacionActual;
        }, ct);
        control.Invalidar();
    }

    public Task<List<CampoFichaTI>> ObtenerFichasAsync(string usuario, string area, CancellationToken ct)
    {
        var (usuarioValido, areaValida) = (Validacion.Usuario(usuario), Validacion.Area(area));
        return baseDatos.LeerAsync("dbo.Usp_TI_Obtener_PlantillasTI", p => Identidad(p, usuarioValido, areaValida), lector => lector.ListaAsync(f => new CampoFichaTI
        {
            Tipo = f.Texto("Tipo"), TipoDescripcion = f.Texto("TipoDescripcion"), Campo = f.Texto("Campo"), Bloque = f.Texto("Bloque"), Orden = f.Entero("Orden"),
            Pregunta = f.Texto("Pregunta"), Ayuda = f.Texto("Ayuda"), TipoDato = f.Texto("TipoDato"), Obligatorio = f.Booleano("Obligatorio"),
            LongitudMinima = f.Entero("LongitudMinima"), LongitudMaxima = f.Entero("LongitudMaxima"), Estado = f.Texto("Estado"),
            UltimoUsuario = f.Texto("UltimoUsuario"), UltimaFechaModif = f.FechaNula("UltimaFechaModif")
        }, ct), ct);
    }

    public Task GuardarCampoFichaAsync(string usuario, string area, GuardarCampoFichaSolicitud s, CancellationToken ct)
    {
        var (usuarioValido, areaValida) = (Validacion.Usuario(usuario), Validacion.Area(area));
        var tipo = Exacto(s.Tipo, "tipo");
        var campo = Variable(s.Campo, 40, "código del campo");
        var bloque = Validacion.Texto(s.Bloque, 2, 60, "El bloque");
        var pregunta = Validacion.Texto(s.Pregunta, 5, 300, "La pregunta");
        var ayuda = Validacion.Opcional(s.Ayuda, 500, "La ayuda");
        var tipoDato = s.TipoDato.Trim().ToUpperInvariant();
        if (!TiposDatoFicha.Contains(tipoDato)) throw new ArgumentException("El tipo de dato debe ser TEXTO, TEXTO_LARGO, FECHA o SI_NO.");
        if (s.Orden is < 0 or > 9999) throw new ArgumentException("El orden debe estar entre 0 y 9999.");
        if (tipoDato is "TEXTO" or "TEXTO_LARGO" && (s.LongitudMaxima is < 1 or > 4000 || s.LongitudMinima < 0 || s.LongitudMinima > s.LongitudMaxima))
            throw new ArgumentException("El largo máximo debe estar entre 1 y 4000 y el mínimo no puede superarlo.");
        var estado = Validacion.Estado(s.Estado);
        return baseDatos.EjecutarAsync("dbo.Usp_TI_Guardar_CampoPlantilla", p =>
        {
            Identidad(p, usuarioValido, areaValida);
            p.Add("@cTipo", SqlDbType.Char, 3).Value = tipo;
            p.Add("@cCampo", SqlDbType.VarChar, 40).Value = campo;
            p.Add("@cBloque", SqlDbType.NVarChar, 60).Value = bloque;
            p.Add("@nOrden", SqlDbType.Int).Value = s.Orden;
            p.Add("@cPregunta", SqlDbType.NVarChar, 300).Value = pregunta;
            p.Add("@cAyuda", SqlDbType.NVarChar, 500).Value = BaseDatos.Opcional(ayuda);
            p.Add("@cTipoDato", SqlDbType.VarChar, 15).Value = tipoDato;
            p.Add("@lObligatorio", SqlDbType.Bit).Value = s.Obligatorio;
            p.Add("@nLongitudMinima", SqlDbType.Int).Value = s.LongitudMinima;
            p.Add("@nLongitudMaxima", SqlDbType.Int).Value = s.LongitudMaxima;
            p.Add("@cEstado", SqlDbType.VarChar, 2).Value = estado;
            p.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = TrazaAgente.CorrelacionActual;
        }, ct);
    }

    private static void Identidad(SqlParameterCollection p, string usuario, string area)
    {
        p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;
        p.Add("@cArea", SqlDbType.Char, 3).Value = area;
    }
}
