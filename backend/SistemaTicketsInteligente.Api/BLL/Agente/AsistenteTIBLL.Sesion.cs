/**
 * Archivo: AsistenteTIBLL.Sesion.cs
 * Objetivo: Administrar la sesión de investigación del agente y la evidencia que TI recopila antes de investigar.
 * Responsabilidad: Crear, consultar y listar investigaciones; recibir la observación Live (token, eventos, cierre y grabaciones);
 *   reabrir la observación, vincular el ticket, reasignar, invitar al colaborador y cancelar.
 * Dependencias: BaseDatos (Usp_TI_Agente_CrearSesion, ObtenerContexto, Listar, ListarPorTicket, Catalogos, RegistrarEvento,
 *   FinalizarObservacion, ReabrirObservacion, Vincular, Reasignar, InvitarUsuario, CancelarInvitacion y Cancelar),
 *   GeminiLiveClient, AlmacenGrabaciones y AgenteCodigoClient.
 * Flujo: AsistenteTIController -> AsistenteTIBLL -> Stored Procedures del agente.
 * Consideraciones: SUP y ADM pueden leer investigaciones ajenas; las operaciones que actúan sobre la sesión exigen ser su responsable.
 *   Cada procedimiento vuelve a validar la identidad (usuario y área de la cookie).
 */

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SistemaTicketsInteligente.Api.BLL.Agente;

public sealed partial class AsistenteTIBLL
{
    // Tickets nuevos (INC-000000) y tickets sincronizados del sistema legado (TKT-00000000).
    private static readonly Regex FormatoIncidencia = new(@"^[A-Z]{3}-\d{6,8}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private const string MensajeFormatoIncidencia = "La incidencia debe tener un formato válido, por ejemplo INC-000523 o TKT-00042342.";

    public async Task<AgenteTISesion> CrearInvestigacionAsync(string usuario, string area, CrearInvestigacionTISolicitud solicitud, CancellationToken ct)
    {
        solicitud.Descripcion = solicitud.Descripcion?.Trim() ?? string.Empty;
        solicitud.IncidenciaNumero = solicitud.IncidenciaNumero?.Trim().ToUpperInvariant();
        if (solicitud.Descripcion.Length < 5) throw new ArgumentException("Describe brevemente el problema que debe investigar el agente.");
        if (solicitud.Descripcion.Length > 1200) throw new ArgumentException("La descripción no puede superar los 1200 caracteres.");
        if (!string.IsNullOrWhiteSpace(solicitud.IncidenciaNumero) && !FormatoIncidencia.IsMatch(solicitud.IncidenciaNumero))
            throw new ArgumentException(MensajeFormatoIncidencia);

        return await baseDatos.LeerAsync("dbo.Usp_TI_Agente_CrearSesion", p =>
        {
            Identidad(p, usuario, area);
            p.Add("@cIncidenciaNumero", SqlDbType.VarChar, 12).Value = BaseDatos.Opcional(solicitud.IncidenciaNumero);
            p.Add("@cDescripcion", SqlDbType.NVarChar, 1200).Value = solicitud.Descripcion;
            p.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
        }, lector => lector.FilaAsync(f => new AgenteTISesion
        {
            SesionNumero = f.Largo("SesionNumero"), IncidenciaNumero = f.Texto("IncidenciaNumero"), IdCorrelacion = f.Identificador("IdCorrelacion"),
            DescripcionInicial = f.Texto("DescripcionInicial"), Estado = f.Texto("Estado"), FechaInicio = f.Fecha("FechaInicio")
        }, ct), ct) ?? throw new InvalidOperationException("No fue posible crear la sesión de investigación.");
    }

    public Task<AgenteTIContextoInvestigacion> ObtenerInvestigacionAsync(string usuario, string area, long sesionNumero, CancellationToken ct) =>
        ObtenerContextoAsync(usuario, area, sesionNumero, ct);

    public Task<List<AgenteTISesion>> ListarAsync(string usuario, string area, bool todas, CancellationToken ct) =>
        baseDatos.LeerAsync("dbo.Usp_TI_Agente_Listar", p =>
        {
            Identidad(p, usuario, area);
            p.Add("@lTodas", SqlDbType.Bit).Value = todas;
        }, lector => lector.ListaAsync(f => new AgenteTISesion
        {
            SesionNumero = f.Largo("SesionNumero"), IncidenciaNumero = f.Texto("IncidenciaNumero"), IdCorrelacion = f.Identificador("IdCorrelacion"),
            Estado = f.Texto("Estado"), DescripcionInicial = f.Texto("DescripcionInicial"), FechaInicio = f.Fecha("FechaInicio"),
            SolucionValidada = f.Booleano("SolucionValidada"), ConocimientoCodigo = f.Texto("ConocimientoCodigo"), Confianza = f.DecimalNulo("Confianza"),
            AccionCodigo = f.Texto("AccionCodigo"), UsuarioTI = f.Texto("UsuarioTI"), NombreOperador = f.Texto("NombreOperador"), EsPropietario = f.Booleano("EsPropietario")
        }, ct), ct);

    public Task<List<AgenteTIInvestigacionTicket>> ListarPorTicketAsync(string usuario, string area, string incidenciaNumero, CancellationToken ct)
    {
        var incidencia = incidenciaNumero?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!FormatoIncidencia.IsMatch(incidencia)) throw new ArgumentException(MensajeFormatoIncidencia);
        return baseDatos.LeerAsync("dbo.Usp_TI_Agente_ListarPorTicket", p =>
        {
            Identidad(p, usuario, area);
            p.Add("@cIncidenciaNumero", SqlDbType.VarChar, 12).Value = incidencia;
        }, lector => lector.ListaAsync(f => new AgenteTIInvestigacionTicket
        {
            SesionNumero = f.Largo("SesionNumero"), Estado = f.Texto("Estado"), FechaInicio = f.Fecha("FechaInicio"), FechaDiagnostico = f.FechaNula("FechaDiagnostico"),
            Confianza = f.DecimalNulo("Confianza"), Diagnostico = f.Texto("Diagnostico"), AccionCodigo = f.Texto("AccionCodigo"), UsuarioTI = f.Texto("UsuarioTI"),
            NombreOperador = f.Texto("NombreOperador"), InformeDisponible = f.Booleano("InformeDisponible"), PuedeAbrir = f.Booleano("PuedeAbrir")
        }, ct), ct);
    }

    /// <summary>Áreas y operadores TI para filtrar y reasignar investigaciones.</summary>
    public Task<AgenteTICatalogos> ObtenerCatalogosAsync(string usuario, string area, CancellationToken ct) =>
        baseDatos.LeerAsync("dbo.Usp_TI_Agente_Catalogos", p => Identidad(p, usuario, area), async lector => new AgenteTICatalogos
        {
            Areas = await lector.ListaAsync(Catalogo, ct),
            Operadores = await lector.ListaAsync(Catalogo, ct)
        }, ct);

    public async Task<AgenteTILiveTokenRespuesta> CrearTokenLiveAsync(string usuario, string area, long sesionNumero, CancellationToken ct)
    {
        var contexto = await ObtenerPropiaAsync(usuario, area, sesionNumero, ct);
        if (contexto.Sesion.Estado is not ("RECOPILANDO" or "OBSERVANDO" or "LISTO_INVESTIGAR"))
            throw new InvalidOperationException("La investigación ya está finalizada y no admite una nueva sesión Live.");
        var resumen = new StringBuilder();
        resumen.AppendLine($"Problema reportado: {Limitar(contexto.Sesion.DescripcionInicial, 600)}");
        if (!string.IsNullOrWhiteSpace(contexto.Ticket.IncidenciaNumero))
            resumen.AppendLine($"Ticket {contexto.Ticket.IncidenciaNumero}: {Limitar(contexto.Ticket.Titulo, 200)}. Mensaje de error registrado: {Limitar(contexto.Ticket.MensajeError, 300)}");
        return await geminiLive.CrearTokenAsync(RedactorDatosSensibles.RedactarParaIA(resumen.ToString()), ct);
    }

    public Task RegistrarEventoAsync(string usuario, long sesionNumero, RegistrarEventoAgenteTISolicitud solicitud, CancellationToken ct)
    {
        var tipo = solicitud.Tipo?.Trim().ToUpperInvariant() ?? string.Empty;
        var fuente = solicitud.Fuente?.Trim().ToUpperInvariant() ?? string.Empty;
        var contenido = solicitud.Contenido?.Trim() ?? string.Empty;
        if (fuente is not ("LIVE" or "USUARIO") || tipo is not ("INICIO_LIVE" or "FIN_LIVE" or "TRANSCRIPCION_USUARIO" or "TRANSCRIPCION_AGENTE" or "ERROR_OBSERVADO" or "PASO_OBSERVADO" or "NOTA_USUARIO"))
            throw new ArgumentException("El navegador solo puede registrar observaciones; la telemetría se obtiene del servidor.");
        if (contenido.Length is < 1 or > 12000) throw new ArgumentException("El contenido del evento no es válido.");
        if (!string.IsNullOrWhiteSpace(solicitud.DatosJson))
        {
            if (solicitud.DatosJson.Length > 12000) throw new ArgumentException("Los datos del evento exceden el tamaño permitido.");
            try { using var documento = JsonDocument.Parse(solicitud.DatosJson); }
            catch (JsonException) { throw new ArgumentException("Los datos técnicos del evento no tienen formato JSON válido."); }
        }
        return baseDatos.EjecutarAsync("dbo.Usp_TI_Agente_RegistrarEvento", p =>
        {
            Sesion(p, usuario, sesionNumero);
            p.Add("@cTipo", SqlDbType.VarChar, 40).Value = tipo;
            p.Add("@cFuente", SqlDbType.VarChar, 30).Value = fuente;
            // Contraseñas, tokens o tarjetas dictadas o visibles durante Live nunca se guardan.
            p.Add("@cContenido", SqlDbType.NVarChar, -1).Value = RedactorDatosSensibles.RedactarSecretos(contenido).Trim();
            p.Add("@cDatosJson", SqlDbType.NVarChar, -1).Value = BaseDatos.Opcional(solicitud.DatosJson);
        }, ct);
    }

    public Task FinalizarObservacionAsync(string usuario, long sesionNumero, FinalizarObservacionAgenteTISolicitud solicitud, CancellationToken ct) =>
        baseDatos.EjecutarAsync("dbo.Usp_TI_Agente_FinalizarObservacion", p =>
        {
            Sesion(p, usuario, sesionNumero);
            p.Add("@cResumenObservacion", SqlDbType.NVarChar, -1).Value = Limitar(solicitud.ResumenObservacion, 12000);
            p.Add("@cProcesoObservado", SqlDbType.NVarChar, -1).Value = Limitar(solicitud.ProcesoObservado, 24000);
            p.Add("@cErrorObservado", SqlDbType.NVarChar, 1000).Value = BaseDatos.Opcional(Limitar(solicitud.ErrorObservado, 1000));
        }, ct);

    /// <summary>Guarda la grabación de pantalla de la observación TI como evidencia (y adjunto del ticket si está vinculado).</summary>
    public async Task<int> SubirGrabacionAsync(string usuario, string area, long sesion, IFormFile archivo, int? duracionSegundos, CancellationToken ct)
    {
        await ObtenerPropiaAsync(usuario, area, sesion, ct);
        return await grabaciones.RegistrarAsync(usuario, sesion, usuarioFinal: false, archivo, duracionSegundos, ct);
    }

    /// <summary>Ruta física y tipo de una grabación de la investigación, verificando que quien la pide puede consultar la investigación.</summary>
    public async Task<(string Ruta, string TipoMime, string Nombre)> ObtenerGrabacionAsync(string usuario, string area, long sesion, int eventoSecuencia, CancellationToken ct)
    {
        var contexto = await ObtenerContextoAsync(usuario, area, sesion, ct);
        var evento = contexto.Eventos.FirstOrDefault(x => x.Secuencia == eventoSecuencia && x.OrigenServidor && x.Tipo == "GRABACION_PANTALLA")
            ?? throw new KeyNotFoundException("La grabación indicada no existe en esta investigación.");
        using var documento = JsonDocument.Parse(evento.DatosJson);
        var raiz = documento.RootElement;
        var ruta = raiz.TryGetProperty("ruta", out var r) ? r.GetString() : null;
        if (string.IsNullOrWhiteSpace(ruta)) throw new KeyNotFoundException("La grabación no tiene un archivo asociado.");
        var tipo = raiz.TryGetProperty("tipoMime", out var t) ? t.GetString() : null;
        var nombre = raiz.TryGetProperty("nombreOriginal", out var n) ? n.GetString() : null;
        return (AlmacenGrabaciones.Resolver(ruta), string.IsNullOrWhiteSpace(tipo) ? "video/webm" : tipo, string.IsNullOrWhiteSpace(nombre) ? "grabacion.webm" : nombre);
    }

    /// <summary>Vuelve a abrir la observación de una investigación ya diagnosticada; el diagnóstico anterior queda como contexto.</summary>
    public async Task ReabrirObservacionAsync(string usuario, string area, long sesion, CancellationToken ct)
    {
        await ObtenerPropiaAsync(usuario, area, sesion, ct);
        await baseDatos.EjecutarAsync("dbo.Usp_TI_Agente_ReabrirObservacion", p => Sesion(p, usuario, area, sesion), ct);
    }

    /// <summary>Referencias del código fuente del sistema relacionadas con la evidencia (búsqueda estática).</summary>
    public async Task<List<AgenteTICodigoReferencia>> BuscarCodigoAsync(string usuario, string area, long sesion, CancellationToken ct) =>
        codigoClient.Buscar(await ObtenerContextoAsync(usuario, area, sesion, ct));

    public async Task VincularAsync(string usuario, string area, long sesion, VincularIncidenciaTISolicitud solicitud, CancellationToken ct)
    {
        var incidencia = solicitud.IncidenciaNumero?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!FormatoIncidencia.IsMatch(incidencia)) throw new ArgumentException(MensajeFormatoIncidencia);
        await ObtenerContextoAsync(usuario, area, sesion, ct);
        await baseDatos.EjecutarAsync("dbo.Usp_TI_Agente_Vincular", p =>
        {
            Sesion(p, usuario, area, sesion);
            p.Add("@cIncidenciaNumero", SqlDbType.VarChar, 12).Value = incidencia;
        }, ct);
    }

    public Task ReasignarAsync(string usuario, string area, long sesion, ReasignarInvestigacionTISolicitud solicitud, CancellationToken ct)
    {
        var nuevo = solicitud.NuevoUsuario?.Trim().ToUpperInvariant() ?? string.Empty;
        if (nuevo.Length is < 2 or > 20) throw new ArgumentException("Selecciona el operador TI que recibirá la investigación.");
        return baseDatos.EjecutarAsync("dbo.Usp_TI_Agente_Reasignar", p =>
        {
            Sesion(p, usuario, area, sesion);
            p.Add("@cNuevoUsuario", SqlDbType.VarChar, 20).Value = nuevo;
        }, ct);
    }

    /// <summary>Invita al colaborador del ticket a reproducir el error desde su portal (ReproduccionUsuarioBLL).</summary>
    public async Task<AgenteTIInvitacionRespuesta> InvitarUsuarioAsync(string usuario, string area, long sesion, CancellationToken ct)
    {
        await ObtenerPropiaAsync(usuario, area, sesion, ct);
        return await baseDatos.LeerAsync("dbo.Usp_TI_Agente_InvitarUsuario", p => Sesion(p, usuario, area, sesion),
            lector => lector.FilaAsync(f => new AgenteTIInvitacionRespuesta(f.Texto("UsuarioInvitado"), f.Texto("NombreInvitado"), f.Fecha("InvitacionExpira")), ct), ct)
            ?? throw new InvalidOperationException("No fue posible registrar la invitación.");
    }

    public async Task CancelarInvitacionAsync(string usuario, string area, long sesion, CancellationToken ct)
    {
        await ObtenerPropiaAsync(usuario, area, sesion, ct);
        await baseDatos.EjecutarAsync("dbo.Usp_TI_Agente_CancelarInvitacion", p => Sesion(p, usuario, area, sesion), ct);
    }

    public async Task CancelarAsync(string usuario, string area, long sesion, CancellationToken ct)
    {
        await ObtenerContextoAsync(usuario, area, sesion, ct);
        await baseDatos.EjecutarAsync("dbo.Usp_TI_Agente_Cancelar", p => Sesion(p, usuario, area, sesion), ct);
    }

    // SUP/ADM pueden leer investigaciones ajenas; las operaciones que actúan sobre la sesión exigen ser su responsable.
    private async Task<AgenteTIContextoInvestigacion> ObtenerPropiaAsync(string usuario, string area, long sesion, CancellationToken ct)
    {
        var contexto = await ObtenerContextoAsync(usuario, area, sesion, ct);
        if (!contexto.Sesion.EsPropietario)
            throw new InvalidOperationException($"Esta investigación pertenece a {contexto.Sesion.NombreOperador}. Solo puedes consultarla; para actuar sobre ella debe reasignarse.");
        return contexto;
    }

    /// <summary>
    /// Todo lo que el agente sabe de la sesión: la sesión y su ticket, documentos, mensajes, conocimiento relacionado,
    /// acciones autorizadas, auditoría y eventos (en ese orden de conjuntos de resultados).
    /// </summary>
    private Task<AgenteTIContextoInvestigacion> ObtenerContextoAsync(string usuario, string area, long sesion, CancellationToken ct) =>
        baseDatos.LeerAsync("dbo.Usp_TI_Agente_ObtenerContexto", p => Sesion(p, usuario, area, sesion), async lector =>
        {
            var contexto = await lector.FilaAsync(f => new AgenteTIContextoInvestigacion
            {
                Sesion = new AgenteTISesion
                {
                    SolucionValidada = f.Booleano("SolucionValidada"), ConocimientoCodigo = f.Texto("ConocimientoCodigo"),
                    SesionNumero = f.Largo("SesionNumero"), IncidenciaNumero = f.Texto("IncidenciaNumero"), IdCorrelacion = f.Identificador("IdCorrelacion"),
                    DescripcionInicial = f.Texto("DescripcionInicial"), Estado = f.Texto("Estado"), ResumenObservacion = f.Texto("ResumenObservacion"),
                    ProcesoObservado = f.Texto("ProcesoObservado"), ErrorObservado = f.Texto("ErrorObservado"), Diagnostico = f.Texto("Diagnostico"),
                    CausaProbable = f.Texto("CausaProbable"), SolucionPropuesta = f.Texto("SolucionPropuesta"), Confianza = f.DecimalNulo("Confianza"),
                    AccionCodigo = f.Texto("AccionCodigo"), NivelRiesgo = f.Texto("NivelRiesgo"), ParametrosJson = f.Texto("ParametrosJson"),
                    Decision = f.Texto("Decision"), SolicitudAprobacionSecuencia = f.EnteroNulo("SolicitudAprobacionSecuencia"),
                    FechaInicio = f.Fecha("FechaInicio"), FechaDiagnostico = f.FechaNula("FechaDiagnostico"), FechaDecision = f.FechaNula("FechaDecision"),
                    InformeDisponible = f.Booleano("InformeDisponible"), UsuarioTI = f.Texto("UsuarioTI"), NombreOperador = f.Texto("NombreOperador"),
                    EsPropietario = f.Booleano("EsPropietario"),
                    // Columnas del script 33; sin él, solo el responsable o un supervisor (EsPropietario) actúan.
                    PuedeTomar = f.TieneColumna("PuedeTomar") && f.Booleano("PuedeTomar"),
                    UsuarioTITicket = f.TieneColumna("UsuarioTITicket") ? f.Texto("UsuarioTITicket") : string.Empty,
                    UsuarioInvitado = f.Texto("UsuarioInvitado"), NombreInvitado = f.Texto("NombreInvitado"), EstadoInvitacion = f.Texto("EstadoInvitacion"),
                    InvitacionExpira = f.FechaNula("InvitacionExpira"), EvidenciasJson = f.Texto("EvidenciasJson"), InformeMarkdown = f.Texto("InformeMarkdown")
                },
                Ticket = new AgenteTITicketContexto
                {
                    IncidenciaNumero = f.Texto("IncidenciaNumero"), Titulo = f.Texto("Titulo"), Detalle = f.Texto("Detalle"), MensajeError = f.Texto("MensajeError"),
                    Estado = f.Texto("EstadoIncidencia"), Linea = f.Texto("Linea"), Item = f.Texto("Item"), Tipo = f.Texto("Tipo"), SubTipo = f.Texto("SubTipo"),
                    Categoria = f.Texto("Categoria"), UsuarioSolicitante = f.Texto("UsuarioSolicitante"), FechaRegistro = f.FechaNula("FechaRegistro")
                }
            }, ct) ?? throw new KeyNotFoundException("No se encontró la sesión de investigación.");

            contexto.Documentos = await lector.ListaAsync(f => new AgenteTIDocumento
            {
                CompaniaSocio = f.Texto("CompaniaSocio"), TipoDocumento = f.Texto("TipoDocumento"), NumeroDocumento = f.Texto("NumeroDocumento"), Descripcion = f.Texto("Descripcion")
            }, ct);
            contexto.Mensajes = await lector.ListaAsync(f => new AgenteTIMensaje
            {
                TipoAutor = f.Texto("TipoAutor"), Autor = f.Texto("Autor"), Contenido = f.Texto("Contenido"), FechaMensaje = f.Fecha("FechaMensaje"), EsInterno = f.Booleano("EsInterno")
            }, ct);
            contexto.Conocimientos = await lector.ListaAsync(f => new AgenteTIConocimiento
            {
                ConocimientoCodigo = f.Texto("ConocimientoCodigo"), Titulo = f.Texto("Titulo"), Problema = f.Texto("Problema"), Sintomas = f.Texto("Sintomas"),
                Causa = f.Texto("Causa"), Solucion = f.Texto("Solucion"), Procedimiento = f.Texto("Procedimiento"), PuntajeContextual = f.Entero("PuntajeContextual")
            }, ct);
            contexto.Acciones = await lector.ListaAsync(f => new AgenteTIAccionDisponible
            {
                AccionCodigo = f.Texto("AccionCodigo"), Nombre = f.Texto("Nombre"), Descripcion = f.Texto("Descripcion"), Tipo = f.Texto("Tipo"),
                NivelRiesgo = f.Texto("NivelRiesgo"), RequiereAprobacion = f.Booleano("RequiereAprobacion"), TieneEjecutor = f.Booleano("TieneEjecutor"),
                ParametrosDescripcion = f.Texto("ParametrosDescripcion")
            }, ct);
            contexto.Auditoria = await lector.ListaAsync(f => new AgenteTIAuditoria
            {
                Entidad = f.Texto("Entidad"), Registro = f.Texto("Registro"), Evento = f.Texto("Evento"), Resultado = f.Texto("Resultado"),
                DetalleJson = f.Texto("DetalleJson"), IdCorrelacion = f.Identificador("IdCorrelacion"), Fecha = f.Fecha("Fecha")
            }, ct);
            contexto.Eventos = await lector.ListaAsync(f => new AgenteTIEvento
            {
                OrigenServidor = f.Booleano("OrigenServidor"), Secuencia = f.Entero("Secuencia"), Tipo = f.Texto("Tipo"), Fuente = f.Texto("Fuente"),
                Contenido = f.Texto("Contenido"), DatosJson = f.Texto("DatosJson"), Fecha = f.Fecha("Fecha")
            }, ct);
            return contexto;
        }, ct);

    private static AgenteTICatalogoItem Catalogo(SqlDataReader f) => new(f.Texto("Codigo"), f.Texto("Descripcion"));

    // Parámetros que comparten los procedimientos del agente: la identidad del operador y la sesión.
    private static void Identidad(SqlParameterCollection p, string usuario, string area)
    {
        p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;
        p.Add("@cArea", SqlDbType.Char, 3).Value = area;
    }

    private static void Sesion(SqlParameterCollection p, string usuario, string area, long sesion)
    {
        Identidad(p, usuario, area);
        p.Add("@nSesionNumero", SqlDbType.BigInt).Value = sesion;
    }

    private static void Sesion(SqlParameterCollection p, string usuario, long sesion)
    {
        p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;
        p.Add("@nSesionNumero", SqlDbType.BigInt).Value = sesion;
    }
}
