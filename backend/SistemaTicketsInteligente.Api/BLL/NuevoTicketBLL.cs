/**
 * Archivo: NuevoTicketBLL.cs
 * Objetivo: Registrar el ticket del colaborador con sus adjuntos y su ficha y, si mostró el error en pantalla, iniciar la investigación del agente.
 * Responsabilidad: Validar el formulario, la ficha del tipo de ticket y los archivos, guardarlos, registrar ticket, ficha y adjuntos en una
 *   sola transacción y preparar la investigación automática cuando llega evidencia del Asistente TI.
 * Dependencias: BaseDatos (Usp_TI_Obtener_DatosNuevoTicket, Usp_TI_Obtener_PlantillaFicha, Usp_TI_Registrar_Incidencia,
 *   Usp_TI_Registrar_IncidenciaAdjunto, Usp_TI_Agente_PrepararInvestigacionAutomatica), Archivos, RedactorDatosSensibles y ColaAgenteTI.
 * Flujo: NuevoTicketController -> NuevoTicketBLL -> archivos en uploads/incidencias -> Stored Procedures -> investigación guardada -> cola del agente.
 * Consideraciones: Si algo falla, la transacción se revierte y se borran los archivos ya guardados. Los videos se reconocen
 *   por su contenido, no por su nombre; los secretos de la evidencia se ocultan antes de guardarla. La ficha se valida aquí y otra vez
 *   en el procedimiento, que es la regla final. Un ticket con evidencia del asistente se registra con canal ASISTENTE.
 */

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SistemaTicketsInteligente.Api.BLL;

public sealed class NuevoTicketBLL(BaseDatos baseDatos, ColaAgenteTI cola, IConfiguration configuration, ILogger<NuevoTicketBLL> logger)
{
    private const int MaximoAdjuntos = 5;
    private const long MaximoBytesPorAdjunto = 10 * 1024 * 1024;
    // La grabación de pantalla que el colaborador hizo con el Asistente TI viaja como adjunto de video.
    private const int MaximoVideos = 2;
    private const long MaximoBytesVideo = 40 * 1024 * 1024;
    private static readonly HashSet<string> ExtensionesPermitidas = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp", ".pdf", ".xls", ".xlsx" };
    private static readonly HashSet<string> TiposMimePermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/webp", "application/pdf", "application/vnd.ms-excel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    };

    public async Task<NuevoTicketDatosRespuesta> ObtenerDatosAsync(string usuario, CancellationToken ct)
    {
        var usuarioValido = Validacion.Usuario(usuario);
        var datos = await baseDatos.LeerAsync("dbo.Usp_TI_Obtener_DatosNuevoTicket", p => p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuarioValido, async lector =>
        {
            var datos = await lector.FilaAsync(f => new NuevoTicketDatosRespuesta
            {
                Usuario = f.Texto("Usuario"), NombreCompleto = f.Texto("NombreCompleto"), Area = f.Texto("Area"), AreaDescripcion = f.Texto("AreaDescripcion")
            }, ct);
            if (datos is null) return null;
            datos.Lineas = await lector.ListaAsync(Catalogo, ct);
            datos.Tipos = await lector.ListaAsync(Catalogo, ct);
            return datos;
        }, ct) ?? throw new InvalidOperationException("No fue posible obtener los datos del usuario autenticado.");
        datos.Ficha = await ObtenerPlantillaAsync(null, ct);
        return datos;
    }

    public async Task<NuevoTicketCreadoRespuesta> CrearAsync(string usuario, CrearNuevoTicketSolicitud solicitud, CancellationToken ct)
    {
        var usuarioValido = Validacion.Usuario(usuario);
        ValidarSolicitud(solicitud);
        ValidarAdjuntos(solicitud.Adjuntos, solicitud.Tipo);
        var ficha = NormalizarFicha(await ObtenerPlantillaAsync(solicitud.Tipo, ct), solicitud.Tipo, solicitud.FichaJson);
        // Si el colaborador mostró el error en pantalla al Asistente TI, el ticket nace con canal ASISTENTE y el agente lo investiga.
        var evidencia = EvidenciaValida(solicitud.EvidenciaAsistenteJson);

        var idCorrelacion = TrazaAgente.CorrelacionActual;
        var carpeta = $"{Archivos.Incidencias}/{Guid.NewGuid():N}";
        try
        {
            var adjuntos = new List<ArchivoGuardado>();
            foreach (var archivo in solicitud.Adjuntos)
            {
                var (extension, tipoMime) = EsVideo(archivo)
                    ? await Archivos.TipoVideoAsync(archivo, ct) ?? throw new ArgumentException($"El archivo '{Path.GetFileName(archivo.FileName)}' no es una grabación de video válida.")
                    : (Path.GetExtension(archivo.FileName).ToLowerInvariant(), archivo.ContentType);
                adjuntos.Add(await Archivos.GuardarAsync(archivo, carpeta, extension, tipoMime, ct));
            }

            var creado = await baseDatos.TransaccionAsync(async (conexion, transaccion) =>
            {
                NuevoTicketCreadoRespuesta registrado;
                await using (var comando = BaseDatos.Comando(conexion, transaccion, "dbo.Usp_TI_Registrar_Incidencia", p =>
                {
                    p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuarioValido;
                    p.Add("@cLinea", SqlDbType.Char, 3).Value = solicitud.Linea;
                    p.Add("@cTipo", SqlDbType.Char, 3).Value = solicitud.Tipo;
                    p.Add("@cTitulo", SqlDbType.NVarChar, 250).Value = solicitud.Titulo;
                    p.Add("@cDetalle", SqlDbType.NVarChar, -1).Value = solicitud.Detalle;
                    p.Add("@cMensajeError", SqlDbType.NVarChar, 1000).Value = BaseDatos.Opcional(solicitud.MensajeError);
                    p.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = idCorrelacion;
                    p.Add("@cCanalRegistro", SqlDbType.VarChar, 20).Value = evidencia is null ? "PORTAL" : "ASISTENTE";
                    p.Add("@cFichaJson", SqlDbType.NVarChar, -1).Value = BaseDatos.Opcional(ficha);
                }))
                await using (var lector = await comando.ExecuteReaderAsync(ct))
                {
                    if (!await lector.ReadAsync(ct)) throw new InvalidOperationException("No se obtuvo el número de la incidencia registrada.");
                    registrado = new NuevoTicketCreadoRespuesta { IncidenciaNumero = lector.Texto("IncidenciaNumero"), FechaRegistro = lector.Fecha("FechaRegistro") };
                }
                foreach (var adjunto in adjuntos)
                {
                    await using var comando = BaseDatos.Comando(conexion, transaccion, "dbo.Usp_TI_Registrar_IncidenciaAdjunto", p =>
                    {
                        p.Add("@cIncidenciaNumero", SqlDbType.VarChar, 12).Value = registrado.IncidenciaNumero;
                        p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuarioValido;
                        Archivos.Parametros(p, adjunto);
                    });
                    await comando.ExecuteNonQueryAsync(ct);
                }
                return registrado;
            }, ct);

            if (evidencia is not null) await PrepararInvestigacionAutomaticaAsync(creado.IncidenciaNumero, evidencia, ct);
            return creado;
        }
        catch
        {
            Archivos.Eliminar(Archivos.RutaFisica(carpeta));
            throw;
        }
    }

    /// <summary>
    /// Valida la ficha del tipo de ticket con las mismas reglas que Usp_TI_Registrar_Incidencia: un texto por campo activo, obligatorios
    /// completos, largos permitidos, SI o NO y fechas aaaa-mm-dd. Devuelve el JSON con los campos respondidos, o null si no hay ficha.
    /// </summary>
    public static string? NormalizarFicha(IReadOnlyCollection<CampoFichaTicket> campos, string tipo, string? fichaJson)
    {
        var plantilla = campos.Where(x => string.Equals(x.Tipo, tipo, StringComparison.OrdinalIgnoreCase)).ToList();
        JsonObject? datos;
        try { datos = string.IsNullOrWhiteSpace(fichaJson) ? new JsonObject() : JsonNode.Parse(fichaJson) as JsonObject; }
        catch (Exception ex) when (ex is JsonException or ArgumentException) { datos = null; }
        if (datos is null) throw new ArgumentException("La ficha del ticket no tiene un formato válido.");

        var salida = new JsonObject();
        foreach (var (clave, nodo) in datos)
        {
            var campo = plantilla.FirstOrDefault(x => string.Equals(x.Campo, clave, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException("La ficha contiene campos que no corresponden al tipo de ticket.");
            if (nodo is not null && nodo.GetValueKind() != JsonValueKind.String) throw new ArgumentException("La ficha del ticket no tiene un formato válido.");
            var valor = nodo?.GetValue<string>().Trim() ?? string.Empty;
            if (valor.Length == 0) continue;
            if (campo.TipoDato == "SI_NO") valor = valor.ToUpperInvariant();
            if (valor.Length < campo.LongitudMinima || valor.Length > campo.LongitudMaxima)
                throw new ArgumentException($"Revisa la respuesta de la ficha: {campo.Pregunta} Debe tener entre {Math.Max(campo.LongitudMinima, 1)} y {campo.LongitudMaxima} caracteres.");
            if ((campo.TipoDato == "SI_NO" && valor is not ("SI" or "NO"))
                || (campo.TipoDato == "FECHA" && !DateOnly.TryParseExact(valor, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)))
                throw new ArgumentException($"Revisa la respuesta de la ficha: {campo.Pregunta}");
            salida[campo.Campo] = valor;
        }
        var faltante = plantilla.Where(x => x.Obligatorio && !salida.ContainsKey(x.Campo)).OrderBy(x => x.Orden).FirstOrDefault();
        if (faltante is not null) throw new ArgumentException($"Completa la ficha: {faltante.Pregunta}");
        return salida.Count == 0 ? null : salida.ToJsonString();
    }

    private Task<List<CampoFichaTicket>> ObtenerPlantillaAsync(string? tipo, CancellationToken ct) =>
        baseDatos.LeerAsync("dbo.Usp_TI_Obtener_PlantillaFicha", p => p.Add("@cTipo", SqlDbType.Char, 3).Value = BaseDatos.Opcional(tipo),
            lector => lector.ListaAsync(f => new CampoFichaTicket
            {
                Tipo = f.Texto("Tipo"), Campo = f.Texto("Campo"), Bloque = f.Texto("Bloque"), Orden = f.Entero("Orden"), Pregunta = f.Texto("Pregunta"),
                Ayuda = f.Texto("Ayuda"), TipoDato = f.Texto("TipoDato"), Obligatorio = f.Booleano("Obligatorio"),
                LongitudMinima = f.Entero("LongitudMinima"), LongitudMaxima = f.Entero("LongitudMaxima")
            }, ct), ct);

    // La investigación y la evidencia quedan guardadas antes de encolar: un reinicio de la API ya no pierde lo que mostró el colaborador.
    private async Task PrepararInvestigacionAutomaticaAsync(string incidencia, string evidencia, CancellationToken ct)
    {
        try
        {
            var sesion = await baseDatos.EscalarAsync("dbo.Usp_TI_Agente_PrepararInvestigacionAutomatica", p =>
            {
                p.Add("@cIncidenciaNumero", SqlDbType.VarChar, 12).Value = incidencia;
                p.Add("@cEvidenciaJson", SqlDbType.NVarChar, -1).Value = evidencia;
                p.Add("@cUsuarioPreferido", SqlDbType.VarChar, 20).Value = BaseDatos.Opcional(configuration["AgenteTI:OperadorAutomatico"]);
                // Identidad propia de la investigación: correlaciona su telemetría y es la clave de idempotencia de su ejecución.
                p.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
            }, ct);
            if (sesion is not null and not DBNull) cola.Encolar(new TrabajoAgenteTI(Convert.ToInt64(sesion, CultureInfo.InvariantCulture), $"ticket-{incidencia}"));
        }
        catch (Exception ex) when (ex is InvalidOperationException or SqlException)
        {
            // El ticket ya quedó registrado: sin operador TI disponible, TI puede investigarlo manualmente.
            logger.LogWarning("No se pudo preparar la investigación automática del ticket {Incidencia}: {Motivo}", incidencia, ex.Message);
        }
    }

    private static NuevoTicketCatalogoItem Catalogo(SqlDataReader f) => new() { Codigo = f.Texto("Codigo"), Descripcion = f.Texto("Descripcion") };

    private static bool EsVideo(IFormFile archivo) =>
        archivo.ContentType.StartsWith("video/webm", StringComparison.OrdinalIgnoreCase) || archivo.ContentType.StartsWith("video/mp4", StringComparison.OrdinalIgnoreCase);

    private static void ValidarSolicitud(CrearNuevoTicketSolicitud solicitud)
    {
        solicitud.Linea = solicitud.Linea.Trim();
        solicitud.Tipo = solicitud.Tipo.Trim().ToUpperInvariant();
        solicitud.Titulo = solicitud.Titulo.Trim();
        solicitud.Detalle = solicitud.Detalle.Trim();
        solicitud.MensajeError = solicitud.MensajeError?.Trim();
        if (solicitud.Linea.Length != 3) throw new ArgumentException("Selecciona el sistema o módulo afectado.");
        if (solicitud.Tipo.Length != 3) throw new ArgumentException("Selecciona el tipo de ticket.");
        if (solicitud.Titulo.Length is < 5 or > 250) throw new ArgumentException("El título debe contener entre 5 y 250 caracteres.");
        if (solicitud.Detalle.Length is < 20 or > 1000) throw new ArgumentException("La descripción debe contener entre 20 y 1000 caracteres.");
        if ((solicitud.MensajeError?.Length ?? 0) > 1000) throw new ArgumentException("El mensaje de error no puede superar los 1000 caracteres.");
        if ((solicitud.FichaJson?.Length ?? 0) > 120000) throw new ArgumentException("La ficha del ticket excede el tamaño permitido.");
    }

    private static void ValidarAdjuntos(IReadOnlyCollection<IFormFile> adjuntos, string tipo)
    {
        if (adjuntos.Count > MaximoAdjuntos) throw new ArgumentException($"Puedes adjuntar como máximo {MaximoAdjuntos} archivos.");
        if (string.Equals(tipo, "REQ", StringComparison.OrdinalIgnoreCase) && adjuntos.Count == 0) throw new ArgumentException("Los requerimientos deben incluir al menos un archivo de sustento.");
        if (adjuntos.Count(EsVideo) > MaximoVideos) throw new ArgumentException($"Puedes adjuntar como máximo {MaximoVideos} grabaciones de pantalla.");
        if (adjuntos.Sum(x => x.Length) > 90L * 1024 * 1024) throw new ArgumentException("El total de archivos adjuntos no puede superar los 90 MB.");
        foreach (var archivo in adjuntos)
        {
            var nombre = Path.GetFileName(archivo.FileName);
            if (archivo.Length <= 0) throw new ArgumentException($"El archivo '{nombre}' está vacío.");
            if (EsVideo(archivo))
            {
                if (archivo.Length > MaximoBytesVideo) throw new ArgumentException($"La grabación '{nombre}' supera el límite de 40 MB.");
                continue;
            }
            if (archivo.Length > MaximoBytesPorAdjunto) throw new ArgumentException($"El archivo '{nombre}' supera el límite de 10 MB.");
            if (!ExtensionesPermitidas.Contains(Path.GetExtension(nombre)) || !TiposMimePermitidos.Contains(archivo.ContentType))
                throw new ArgumentException($"El archivo '{nombre}' no tiene un formato permitido.");
        }
    }

    // Los secretos se ocultan en cada texto (no sobre el JSON completo, para no romper su estructura).
    private static string? EvidenciaValida(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 60000) return null;
        try { return JsonNode.Parse(json) is JsonObject evidencia ? RedactarTextos(evidencia)!.ToJsonString() : null; }
        catch (JsonException) { return null; }
    }

    private static JsonNode? RedactarTextos(JsonNode? nodo)
    {
        switch (nodo)
        {
            case JsonObject objeto:
                foreach (var clave in objeto.Select(x => x.Key).ToList()) objeto[clave] = RedactarTextos(objeto[clave]?.DeepClone());
                return objeto;
            case JsonArray lista:
                for (var i = 0; i < lista.Count; i++) lista[i] = RedactarTextos(lista[i]?.DeepClone());
                return lista;
            case JsonValue valor when valor.TryGetValue<string>(out var texto):
                return JsonValue.Create(RedactorDatosSensibles.RedactarSecretos(texto));
            default:
                return nodo;
        }
    }
}
