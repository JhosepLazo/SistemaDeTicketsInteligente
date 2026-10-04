/**
 * Archivo: ReproduccionUsuarioBLL.cs
 * Objetivo: Permitir que el colaborador reproduzca su error, por invitación de TI, desde su propio portal.
 * Responsabilidad: Validar la invitación y el consentimiento, emitir la sesión Live del colaborador, recibir solo evidencia
 *   observacional (eventos y grabación) y, al terminar, encolar la investigación automática.
 * Dependencias: BaseDatos (Usp_TI_Reproduccion_*), AlmacenGrabaciones, GeminiLiveClient y ColaAgenteTI.
 * Flujo: Colaborador -> ReproduccionUsuarioController -> ReproduccionUsuarioBLL -> Stored Procedures / Gemini Live -> cola del agente.
 * Consideraciones: El colaborador nunca ve el diagnóstico, el expediente ni las decisiones; su identidad sale de la cookie.
 *   Con el consentimiento V2 la pantalla se graba para TI.
 */

namespace SistemaTicketsInteligente.Api.BLL.Agente;

public sealed class ReproduccionUsuarioBLL(BaseDatos baseDatos, AlmacenGrabaciones grabaciones, GeminiLiveClient geminiLive, ColaAgenteTI cola)
{
    /// <summary>Versión del texto de consentimiento mostrado en el portal; cambia si el texto cambia.</summary>
    public const string VersionConsentimiento = "REPRODUCCION_V2";

    private static readonly HashSet<string> TiposPermitidos = new(StringComparer.Ordinal)
    {
        "INICIO_LIVE", "FIN_LIVE", "TRANSCRIPCION_USUARIO", "TRANSCRIPCION_AGENTE", "ERROR_OBSERVADO", "PASO_OBSERVADO", "NOTA_USUARIO"
    };

    public Task<List<ReproduccionInvitacion>> ListarAsync(string usuario, CancellationToken ct) => ListarInvitacionesAsync(usuario, null, ct);

    public async Task<ReproduccionInvitacion> ObtenerAsync(string usuario, long sesion, CancellationToken ct) =>
        (await ListarInvitacionesAsync(usuario, sesion, ct)).FirstOrDefault()
        ?? throw new KeyNotFoundException("La invitación no existe, venció o ya no está disponible.");

    public Task ResponderAsync(string usuario, long sesion, ResponderReproduccionSolicitud solicitud, CancellationToken ct)
    {
        if (solicitud.Aceptar && !solicitud.AceptaConsentimiento) throw new ArgumentException("Para compartir tu pantalla debes aceptar el consentimiento.");
        var motivo = solicitud.Motivo?.Trim();
        if (motivo is { Length: > 500 }) throw new ArgumentException("El motivo no puede superar los 500 caracteres.");
        return baseDatos.EjecutarAsync("dbo.Usp_TI_Reproduccion_Responder", p =>
        {
            Sesion(p, usuario, sesion);
            p.Add("@lAceptar", SqlDbType.Bit).Value = solicitud.Aceptar;
            p.Add("@cConsentimientoVersion", SqlDbType.VarChar, 30).Value = BaseDatos.Opcional(solicitud.Aceptar ? VersionConsentimiento : null);
            p.Add("@cMotivo", SqlDbType.NVarChar, 500).Value = BaseDatos.Opcional(motivo);
        }, ct);
    }

    public async Task<AgenteTILiveTokenRespuesta> CrearTokenAsync(string usuario, long sesion, CancellationToken ct)
    {
        var invitacion = await ObtenerAceptadaAsync(usuario, sesion, ct);
        return await geminiLive.CrearTokenUsuarioFinalAsync(RedactorDatosSensibles.RedactarParaIA($"Ticket {invitacion.IncidenciaNumero}: {invitacion.TituloTicket}"), ct);
    }

    public Task RegistrarEventoAsync(string usuario, long sesion, RegistrarEventoReproduccionSolicitud solicitud, CancellationToken ct)
    {
        var tipo = solicitud.Tipo?.Trim().ToUpperInvariant() ?? string.Empty;
        var contenido = solicitud.Contenido?.Trim() ?? string.Empty;
        if (!TiposPermitidos.Contains(tipo)) throw new ArgumentException("El tipo de evidencia no es válido.");
        if (contenido.Length is < 1 or > 12000) throw new ArgumentException("El contenido de la evidencia no es válido.");
        return baseDatos.EjecutarAsync("dbo.Usp_TI_Reproduccion_RegistrarEvento", p =>
        {
            Sesion(p, usuario, sesion);
            p.Add("@cTipo", SqlDbType.VarChar, 40).Value = tipo;
            // Lo que el colaborador dicte o muestre (contraseñas, tokens, tarjetas) nunca se guarda.
            p.Add("@cContenido", SqlDbType.NVarChar, -1).Value = RedactorDatosSensibles.RedactarSecretos(contenido);
        }, ct);
    }

    /// <summary>Guarda la grabación de la reproducción (consentida) como evidencia para TI y adjunto del ticket.</summary>
    public async Task<int> SubirGrabacionAsync(string usuario, long sesion, IFormFile archivo, int? duracionSegundos, CancellationToken ct)
    {
        await ObtenerAceptadaAsync(usuario, sesion, ct);
        return await grabaciones.RegistrarAsync(usuario, sesion, usuarioFinal: true, archivo, duracionSegundos, ct);
    }

    public async Task FinalizarAsync(string usuario, long sesion, CancellationToken ct)
    {
        await baseDatos.EjecutarAsync("dbo.Usp_TI_Reproduccion_Finalizar", p => Sesion(p, usuario, sesion), ct);
        // Con lo que el colaborador mostró, el agente investiga en segundo plano y avisa al responsable TI.
        cola.Encolar(new TrabajoAgenteTI(sesion, null, null, $"reproduccion-{sesion}"));
    }

    private async Task<ReproduccionInvitacion> ObtenerAceptadaAsync(string usuario, long sesion, CancellationToken ct)
    {
        var invitacion = await ObtenerAsync(usuario, sesion, ct);
        return invitacion.EstadoInvitacion == "ACEPTADA" ? invitacion : throw new InvalidOperationException("Primero acepta la invitación y el consentimiento.");
    }

    private Task<List<ReproduccionInvitacion>> ListarInvitacionesAsync(string usuario, long? sesion, CancellationToken ct) =>
        baseDatos.LeerAsync("dbo.Usp_TI_Reproduccion_Listar", p =>
        {
            p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;
            p.Add("@nSesionNumero", SqlDbType.BigInt).Value = BaseDatos.Opcional(sesion);
        }, lector => lector.ListaAsync(f => new ReproduccionInvitacion
        {
            SesionNumero = f.Largo("SesionNumero"), IncidenciaNumero = f.Texto("IncidenciaNumero"), TituloTicket = f.Texto("TituloTicket"),
            OperadorTI = f.Texto("OperadorTI"), EstadoInvitacion = f.Texto("EstadoInvitacion"),
            FechaInvitacion = f.Fecha("FechaInvitacion"), InvitacionExpira = f.Fecha("InvitacionExpira")
        }, ct), ct);

    private static void Sesion(SqlParameterCollection p, string usuario, long sesion)
    {
        p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;
        p.Add("@nSesionNumero", SqlDbType.BigInt).Value = sesion;
    }
}
