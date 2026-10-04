/**
 * Archivo: MisTicketsUsuarioBLL.cs
 * Objetivo: Permitir al colaborador seguir sus tickets: consultarlos, responder a TI, validar la solución, calificar,
 *   descargar adjuntos y corregir un ticket antes de que TI lo procese.
 * Responsabilidad: Validar cada operación sobre tickets propios, guardar adjuntos de forma segura y ejecutar los procedimientos.
 * Dependencias: BaseDatos (Usp_TI_Obtener_MisTicketsUsuario, Usp_TI_Obtener_DetalleTicketUsuario, Usp_TI_Responder_ObservacionTicket,
 *   Usp_TI_Registrar_AdjuntoMensajeUsuario, Usp_TI_Validar_SolucionTicket, Usp_TI_Calificar_TicketUsuario,
 *   Usp_TI_Obtener_AdjuntoTicketUsuario, Usp_TI_Editar_TicketUsuario) y Archivos.
 * Flujo: MisTicketsUsuarioController -> MisTicketsUsuarioBLL -> Stored Procedures.
 * Consideraciones: Cada procedimiento verifica que el ticket pertenezca al usuario de la cookie. La edición temprana tiene
 *   backend completo pero todavía no tiene pantalla (pendiente).
 */

namespace SistemaTicketsInteligente.Api.BLL;

public sealed class MisTicketsUsuarioBLL(BaseDatos baseDatos)
{
    private const int MaximoAdjuntos = 5;
    private const long MaximoBytesPorAdjunto = 10 * 1024 * 1024;
    private static readonly HashSet<string> ExtensionesPermitidas = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp", ".pdf", ".xls", ".xlsx" };
    private static readonly HashSet<string> TiposMimePermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/webp", "application/pdf", "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/octet-stream"
    };

    public Task<MisTicketsUsuarioRespuesta> ObtenerAsync(string usuario, CancellationToken ct)
    {
        var usuarioValido = Validacion.Usuario(usuario);
        return baseDatos.LeerAsync("dbo.Usp_TI_Obtener_MisTicketsUsuario", p => p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuarioValido, async lector =>
        {
            var respuesta = new MisTicketsUsuarioRespuesta();
            respuesta.Resumen = await lector.FilaAsync(f => new MisTicketsUsuarioResumen
            {
                Activos = f.Entero("Activos"), EnAtencion = f.Entero("EnAtencion"), PendientesRespuesta = f.Entero("PendientesRespuesta"), Resueltos30Dias = f.Entero("Resueltos30Dias")
            }, ct) ?? new();
            respuesta.Tickets = await lector.ListaAsync(f => new MisTicketsUsuarioItem
            {
                IncidenciaNumero = f.Texto("IncidenciaNumero"), Titulo = f.Texto("Titulo"), Detalle = f.Texto("Detalle"), Estado = f.Texto("Estado"),
                EstadoDescripcion = f.Texto("EstadoDescripcion"), Responsable = f.Texto("Responsable"), FechaRegistro = f.Fecha("FechaRegistro"),
                UltimaFechaModif = f.Fecha("UltimaFechaModif"), Prioridad = f.EnteroNulo("Prioridad"), Accion = f.Texto("Accion")
            }, ct);
            return respuesta;
        }, ct);
    }

    public async Task<MisTicketsUsuarioDetalle> ObtenerDetalleAsync(string usuario, string incidenciaNumero, CancellationToken ct)
    {
        var usuarioValido = Validacion.Usuario(usuario);
        var incidencia = Validacion.Incidencia(incidenciaNumero);
        return await baseDatos.LeerAsync("dbo.Usp_TI_Obtener_DetalleTicketUsuario", p =>
        {
            p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuarioValido;
            p.Add("@cIncidenciaNumero", SqlDbType.VarChar, 12).Value = incidencia;
        }, async lector =>
        {
            var detalle = await lector.FilaAsync(f => new MisTicketsUsuarioDetalle
            {
                IncidenciaNumero = f.Texto("IncidenciaNumero"), Titulo = f.Texto("Titulo"), Detalle = f.Texto("Detalle"), MensajeError = f.Texto("MensajeError"),
                Estado = f.Texto("Estado"), EstadoDescripcion = f.Texto("EstadoDescripcion"), Linea = f.Texto("Linea"), LineaDescripcion = f.Texto("LineaDescripcion"),
                Item = f.Texto("Item"), ItemDescripcion = f.Texto("ItemDescripcion"), Tipo = f.Texto("Tipo"), TipoDescripcion = f.Texto("TipoDescripcion"),
                AreaDescripcion = f.Texto("AreaDescripcion"), Responsable = f.Texto("Responsable"), FechaRegistro = f.Fecha("FechaRegistro"),
                FechaAsignacion = f.FechaNula("FechaAsignacion"), FechaAtencion = f.FechaNula("FechaAtencion"), FechaCierre = f.FechaNula("FechaCierre"),
                UltimaFechaModif = f.Fecha("UltimaFechaModif"), Prioridad = f.EnteroNulo("Prioridad"), Calificacion = f.ByteNulo("Calificacion"),
                ComentarioCalificacion = f.Texto("ComentarioCalificacion"), RespuestaUsuario = f.Texto("RespuestaUsuario"), Accion = f.Texto("Accion")
            }, ct);
            if (detalle is null) return null;
            detalle.HistorialEstados = await lector.ListaAsync(f => new MisTicketsUsuarioEstado
            {
                Secuencia = f.Entero("Secuencia"), Estado = f.Texto("Estado"), EstadoDescripcion = f.Texto("EstadoDescripcion"),
                Actor = f.Texto("Actor"), Fecha = f.Fecha("Fecha"), Observacion = f.Texto("Observacion")
            }, ct);
            detalle.Avances = await lector.ListaAsync(f => new MisTicketsUsuarioAvance
            {
                Secuencia = f.Entero("Secuencia"), Responsable = f.Texto("Responsable"), Fecha = f.Fecha("Fecha"), Detalle = f.Texto("Detalle"),
                PorcentajeAvance = f.DecimalNulo("PorcentajeAvance")
            }, ct);
            detalle.Mensajes = await lector.ListaAsync(f => new MisTicketsUsuarioMensaje
            {
                Secuencia = f.Entero("Secuencia"), TipoAutor = f.Texto("TipoAutor"), Autor = f.Texto("Autor"), Contenido = f.Texto("Contenido"), Fecha = f.Fecha("Fecha")
            }, ct);
            detalle.Adjuntos = await lector.ListaAsync(f => new MisTicketsUsuarioAdjunto
            {
                Secuencia = f.Entero("Secuencia"), MensajeSecuencia = f.EnteroNulo("MensajeSecuencia"), NombreOriginal = f.Texto("NombreOriginal"),
                TipoMime = f.Texto("TipoMime"), TamanoBytes = f.Largo("TamanoBytes"), FechaRegistro = f.Fecha("FechaRegistro")
            }, ct);
            detalle.Documentos = await lector.ListaAsync(f => new MisTicketsUsuarioDocumento
            {
                Secuencia = f.Entero("Secuencia"), CompaniaSocio = f.Texto("CompaniaSocio"), TipoDocumento = f.Texto("TipoDocumento"),
                NumeroDocumento = f.Texto("NumeroDocumento"), Descripcion = f.Texto("Descripcion")
            }, ct);
            return detalle;
        }, ct) ?? throw new KeyNotFoundException("El ticket no existe o no pertenece al usuario autenticado.");
    }

    /// <summary>Respuesta del colaborador a la consulta de TI, con sus adjuntos, en una sola transacción.</summary>
    public async Task ResponderObservacionAsync(string usuario, string incidenciaNumero, ResponderObservacionSolicitud solicitud, CancellationToken ct)
    {
        var usuarioValido = Validacion.Usuario(usuario);
        var incidencia = Validacion.Incidencia(incidenciaNumero);
        var contenido = solicitud.Contenido.Trim();
        if (contenido.Length is < 3 or > 2000) throw new ArgumentException("La respuesta debe contener entre 3 y 2000 caracteres.");
        ValidarAdjuntos(solicitud.Adjuntos);

        var idCorrelacion = Guid.NewGuid();
        var carpeta = $"{Archivos.Incidencias}/{idCorrelacion:N}";
        try
        {
            var adjuntos = new List<ArchivoGuardado>();
            foreach (var archivo in solicitud.Adjuntos)
                adjuntos.Add(await Archivos.GuardarAsync(archivo, carpeta, Path.GetExtension(archivo.FileName).ToLowerInvariant(), TipoMime(archivo), ct));

            await baseDatos.TransaccionAsync(async (conexion, transaccion) =>
            {
                int mensajeSecuencia;
                await using (var comando = BaseDatos.Comando(conexion, transaccion, "dbo.Usp_TI_Responder_ObservacionTicket", p =>
                {
                    p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuarioValido;
                    p.Add("@cIncidenciaNumero", SqlDbType.VarChar, 12).Value = incidencia;
                    p.Add("@cContenido", SqlDbType.NVarChar, -1).Value = contenido;
                    p.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = idCorrelacion;
                }))
                    mensajeSecuencia = Convert.ToInt32(await comando.ExecuteScalarAsync(ct));

                foreach (var adjunto in adjuntos)
                {
                    await using var comando = BaseDatos.Comando(conexion, transaccion, "dbo.Usp_TI_Registrar_AdjuntoMensajeUsuario", p =>
                    {
                        p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuarioValido;
                        p.Add("@cIncidenciaNumero", SqlDbType.VarChar, 12).Value = incidencia;
                        p.Add("@nMensajeSecuencia", SqlDbType.Int).Value = mensajeSecuencia;
                        Archivos.Parametros(p, adjunto);
                    });
                    await comando.ExecuteNonQueryAsync(ct);
                }
                return mensajeSecuencia;
            }, ct);
        }
        catch
        {
            Archivos.Eliminar(Archivos.RutaFisica(carpeta));
            throw;
        }
    }

    public Task ValidarSolucionAsync(string usuario, string incidenciaNumero, ValidarSolucionSolicitud solicitud, CancellationToken ct)
    {
        var usuarioValido = Validacion.Usuario(usuario);
        var incidencia = Validacion.Incidencia(incidenciaNumero);
        var comentario = solicitud.Comentario?.Trim();
        if ((comentario?.Length ?? 0) > 1000) throw new ArgumentException("El comentario no puede superar los 1000 caracteres.");
        if (!solicitud.Solucionada && string.IsNullOrWhiteSpace(comentario)) throw new ArgumentException("Indica qué problema continúa para poder reabrir el ticket.");
        return baseDatos.EjecutarAsync("dbo.Usp_TI_Validar_SolucionTicket", p =>
        {
            p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuarioValido;
            p.Add("@cIncidenciaNumero", SqlDbType.VarChar, 12).Value = incidencia;
            p.Add("@lSolucionada", SqlDbType.Bit).Value = solicitud.Solucionada;
            p.Add("@cComentario", SqlDbType.NVarChar, 1000).Value = BaseDatos.Opcional(comentario);
            p.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
        }, ct);
    }

    public Task CalificarAsync(string usuario, string incidenciaNumero, CalificarTicketSolicitud solicitud, CancellationToken ct)
    {
        var usuarioValido = Validacion.Usuario(usuario);
        var incidencia = Validacion.Incidencia(incidenciaNumero);
        if (solicitud.Calificacion is < 1 or > 5) throw new ArgumentException("La calificación debe estar entre 1 y 5 estrellas.");
        var comentario = solicitud.Comentario?.Trim();
        if ((comentario?.Length ?? 0) > 500) throw new ArgumentException("El comentario no puede superar los 500 caracteres.");
        return baseDatos.EjecutarAsync("dbo.Usp_TI_Calificar_TicketUsuario", p =>
        {
            p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuarioValido;
            p.Add("@cIncidenciaNumero", SqlDbType.VarChar, 12).Value = incidencia;
            p.Add("@nCalificacion", SqlDbType.TinyInt).Value = solicitud.Calificacion;
            p.Add("@cComentario", SqlDbType.NVarChar, 500).Value = BaseDatos.Opcional(comentario);
            p.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
        }, ct);
    }

    public async Task<ArchivoDescarga> ObtenerArchivoAsync(string usuario, string incidenciaNumero, int secuencia, CancellationToken ct)
    {
        var usuarioValido = Validacion.Usuario(usuario);
        var incidencia = Validacion.Incidencia(incidenciaNumero);
        if (secuencia <= 0) throw new ArgumentException("El adjunto indicado no es válido.");
        return await baseDatos.LeerAsync("dbo.Usp_TI_Obtener_AdjuntoTicketUsuario", p =>
        {
            p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuarioValido;
            p.Add("@cIncidenciaNumero", SqlDbType.VarChar, 12).Value = incidencia;
            p.Add("@nSecuencia", SqlDbType.Int).Value = secuencia;
        }, lector => lector.FilaAsync(f => Archivos.Descarga(f, Archivos.Incidencias, "La ruta del adjunto no es válida.",
            "El archivo asociado al ticket ya no se encuentra disponible."), ct), ct)
            ?? throw new KeyNotFoundException("El archivo no existe o no pertenece al ticket indicado.");
    }

    /// <summary>Corrige los datos descriptivos de un ticket propio antes de que TI lo procese (pendiente: aún sin pantalla).</summary>
    public Task EditarAsync(string usuario, string incidenciaNumero, EditarTicketUsuarioSolicitud solicitud, CancellationToken ct)
    {
        var usuarioValido = Validacion.Usuario(usuario);
        var incidencia = Validacion.Incidencia(incidenciaNumero);
        var linea = Validacion.CodigoExacto(solicitud.Linea, 3, "El código de línea debe tener 3 caracteres.");
        var tipo = Validacion.CodigoExacto(solicitud.Tipo, 3, "El código de tipo debe tener 3 caracteres.");
        var titulo = Validacion.Texto(solicitud.Titulo, 5, 250, "El título");
        var detalle = Validacion.Texto(solicitud.Detalle, 20, 1000, "El detalle");
        var mensajeError = Validacion.Opcional(solicitud.MensajeError, 1000, "El mensaje de error");
        return baseDatos.EjecutarAsync("dbo.Usp_TI_Editar_TicketUsuario", p =>
        {
            p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuarioValido;
            p.Add("@cIncidenciaNumero", SqlDbType.VarChar, 12).Value = incidencia;
            p.Add("@cLinea", SqlDbType.Char, 3).Value = linea;
            p.Add("@cTipo", SqlDbType.Char, 3).Value = tipo;
            p.Add("@cTitulo", SqlDbType.NVarChar, 250).Value = titulo;
            p.Add("@cDetalle", SqlDbType.NVarChar, -1).Value = detalle;
            p.Add("@cMensajeError", SqlDbType.NVarChar, 1000).Value = BaseDatos.Opcional(mensajeError);
            p.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
        }, ct);
    }

    private static string TipoMime(IFormFile archivo) => string.IsNullOrWhiteSpace(archivo.ContentType) ? "application/octet-stream" : archivo.ContentType;

    private static void ValidarAdjuntos(IReadOnlyCollection<IFormFile> adjuntos)
    {
        if (adjuntos.Count > MaximoAdjuntos) throw new ArgumentException($"Puedes adjuntar como máximo {MaximoAdjuntos} archivos.");
        foreach (var archivo in adjuntos)
        {
            var nombre = Path.GetFileName(archivo.FileName);
            if (archivo.Length <= 0) throw new ArgumentException($"El archivo '{nombre}' está vacío.");
            if (archivo.Length > MaximoBytesPorAdjunto) throw new ArgumentException($"El archivo '{nombre}' supera el límite de 10 MB.");
            if (!ExtensionesPermitidas.Contains(Path.GetExtension(nombre)) || !TiposMimePermitidos.Contains(TipoMime(archivo)))
                throw new ArgumentException($"El archivo '{nombre}' no tiene un formato permitido.");
        }
    }
}
