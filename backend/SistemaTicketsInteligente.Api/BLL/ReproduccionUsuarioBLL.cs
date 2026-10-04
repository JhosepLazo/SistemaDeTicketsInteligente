/**
 * Archivo: ReproduccionUsuarioBLL.cs
 * Objetivo: Permitir que el usuario final reproduzca su error, por invitación de TI, desde su propio portal.
 * Responsabilidad: Validar la invitación y el consentimiento, emitir la sesión Live del usuario y recibir únicamente evidencia observacional.
 * Dependencias: AsistenteTIDAO (Usp_TI_Reproduccion_*) y GeminiLiveClient.
 * Flujo: Usuario -> ReproduccionUsuarioController -> ReproduccionUsuarioBLL -> AsistenteTIDAO / Gemini Live.
 * Consideraciones: El usuario nunca accede al diagnóstico, expediente ni decisiones; la identidad proviene de la sesión autenticada.
 *   Con el consentimiento V2 la pantalla se graba para TI; al terminar, el agente investiga automáticamente y avisa al responsable.
 */

using SistemaTicketsInteligente.Api.DAO;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.BLL;

public sealed class ReproduccionUsuarioBLL
{
    /// <summary>Versión del texto de consentimiento mostrado en el portal; cambia si el texto cambia.</summary>
    public const string VersionConsentimiento = "REPRODUCCION_V2";

    private static readonly HashSet<string> TiposPermitidos = new(StringComparer.Ordinal)
    {
        "INICIO_LIVE", "FIN_LIVE", "TRANSCRIPCION_USUARIO", "TRANSCRIPCION_AGENTE", "ERROR_OBSERVADO", "PASO_OBSERVADO", "NOTA_USUARIO"
    };

    private readonly AsistenteTIDAO agenteDAO;
    private readonly GeminiLiveClient geminiLive;
    private readonly ColaAgenteTI cola;

    public ReproduccionUsuarioBLL(AsistenteTIDAO agenteDAO, GeminiLiveClient geminiLive, ColaAgenteTI cola)
    {
        this.agenteDAO = agenteDAO;
        this.geminiLive = geminiLive;
        this.cola = cola;
    }

    /// <summary>Guarda la grabación de la reproducción (consentida) como evidencia para TI y adjunto del ticket.</summary>
    public async Task<int> SubirGrabacionAsync(string usuario, long sesion, IFormFile archivo, int? duracionSegundos, CancellationToken ct)
    {
        var invitacion = await ObtenerAsync(usuario, sesion, ct);
        if (invitacion.EstadoInvitacion != "ACEPTADA") throw new InvalidOperationException("Primero acepta la invitación y el consentimiento.");
        if (duracionSegundos is < 0 or > 7200) duracionSegundos = null;
        var guardada = await AlmacenGrabaciones.GuardarAsync(archivo, sesion, ct);
        try
        {
            return await agenteDAO.RegistrarGrabacionAsync(usuario, sesion, true, guardada.NombreOriginal, guardada.NombreArchivo, guardada.RutaRelativa,
                guardada.TipoMime, guardada.TamanoBytes, duracionSegundos, ct);
        }
        catch
        {
            AlmacenGrabaciones.Eliminar(guardada.RutaFisica);
            throw;
        }
    }

    public Task<List<ReproduccionInvitacion>> ListarAsync(string usuario, CancellationToken ct) => agenteDAO.ListarReproduccionesAsync(usuario, null, ct);

    public async Task<ReproduccionInvitacion> ObtenerAsync(string usuario, long sesion, CancellationToken ct) =>
        (await agenteDAO.ListarReproduccionesAsync(usuario, sesion, ct)).FirstOrDefault()
        ?? throw new KeyNotFoundException("La invitación no existe, venció o ya no está disponible.");

    public Task ResponderAsync(string usuario, long sesion, ResponderReproduccionSolicitud solicitud, CancellationToken ct)
    {
        if (solicitud.Aceptar && !solicitud.AceptaConsentimiento) throw new ArgumentException("Para compartir tu pantalla debes aceptar el consentimiento.");
        var motivo = solicitud.Motivo?.Trim();
        if (motivo is { Length: > 500 }) throw new ArgumentException("El motivo no puede superar los 500 caracteres.");
        return agenteDAO.ResponderReproduccionAsync(usuario, sesion, solicitud.Aceptar, solicitud.Aceptar ? VersionConsentimiento : null, motivo, ct);
    }

    public async Task<AgenteTILiveTokenRespuesta> CrearTokenAsync(string usuario, long sesion, CancellationToken ct)
    {
        var invitacion = await ObtenerAsync(usuario, sesion, ct);
        if (invitacion.EstadoInvitacion != "ACEPTADA") throw new InvalidOperationException("Primero acepta la invitación y el consentimiento.");
        return await geminiLive.CrearTokenUsuarioFinalAsync(RedactorDatosSensibles.RedactarParaIA($"Ticket {invitacion.IncidenciaNumero}: {invitacion.TituloTicket}"), ct);
    }

    public Task RegistrarEventoAsync(string usuario, long sesion, RegistrarEventoReproduccionSolicitud solicitud, CancellationToken ct)
    {
        var tipo = solicitud.Tipo?.Trim().ToUpperInvariant() ?? string.Empty;
        var contenido = solicitud.Contenido?.Trim() ?? string.Empty;
        if (!TiposPermitidos.Contains(tipo)) throw new ArgumentException("El tipo de evidencia no es válido.");
        if (contenido.Length is < 1 or > 12000) throw new ArgumentException("El contenido de la evidencia no es válido.");
        // Lo que el usuario dicte o muestre (contraseñas, tokens, tarjetas) nunca se guarda.
        return agenteDAO.RegistrarEventoReproduccionAsync(usuario, sesion, tipo, RedactorDatosSensibles.RedactarSecretos(contenido), ct);
    }

    public async Task FinalizarAsync(string usuario, long sesion, CancellationToken ct)
    {
        await agenteDAO.FinalizarReproduccionAsync(usuario, sesion, ct);
        // Con lo que el usuario mostró, el agente investiga en segundo plano y avisa al responsable TI.
        cola.Encolar(new TrabajoAgenteTI(sesion, null, null, $"reproduccion-{sesion}"));
    }
}
