/**
 * Archivo: GestionTicketsTIBLL.cs
 * Objetivo: Gestionar la bandeja de tickets del operador TI: consultar, clasificar, asignar, pedir información, resolver,
 *   declarar No Procede, responder aprobaciones y descargar adjuntos.
 * Responsabilidad: Validar cada acción con la identidad y el área del operador y ejecutar el procedimiento correspondiente.
 * Dependencias: BaseDatos (Usp_TI_Obtener_GestionTicketsTI, Usp_TI_Obtener_DetalleGestionTicketTI, Usp_TI_Clasificar_Ticket,
 *   Usp_TI_Asignar_Ticket, Usp_TI_Solicitar_InformacionTicket, Usp_TI_Resolver_TicketTI, Usp_TI_NoProcede_Ticket,
 *   Usp_TI_Responder_AprobacionTicket, Usp_TI_Obtener_AdjuntoTicketTI) y Archivos.
 * Flujo: GestionTicketsTIController -> GestionTicketsTIBLL -> Stored Procedures.
 * Consideraciones: Los avances técnicos se registran en GestionOperativaTIBLL, que exige minutos y área causante.
 */

namespace SistemaTicketsInteligente.Api.BLL;

public sealed class GestionTicketsTIBLL(BaseDatos baseDatos)
{
    public Task<GestionTicketsTIRespuesta> ObtenerAsync(string usuario, string area, CancellationToken ct)
    {
        var (usuarioValido, areaValida) = (Validacion.Usuario(usuario), Validacion.Area(area));
        return baseDatos.LeerAsync("dbo.Usp_TI_Obtener_GestionTicketsTI", p => Identidad(p, usuarioValido, areaValida), async lector =>
        {
            var respuesta = new GestionTicketsTIRespuesta();
            respuesta.Resumen = await lector.FilaAsync(f => new GestionTicketsTIResumen
            {
                Pendientes = f.Entero("Pendientes"), EnAtencion = f.Entero("EnAtencion"), PorVencer = f.Entero("PorVencer"), Reabiertos = f.Entero("Reabiertos"),
                SinAsignar = f.Entero("SinAsignar"), MisAsignados = f.Entero("MisAsignados"), PrioridadAlta = f.Entero("PrioridadAlta"),
                AprobacionesPendientes = f.Entero("AprobacionesPendientes"), Total = f.Entero("Total")
            }, ct) ?? new();
            respuesta.Tickets = await lector.ListaAsync(f => new GestionTicketsTIItem
            {
                IncidenciaNumero = f.Texto("IncidenciaNumero"), UsuarioSolicitante = f.Texto("UsuarioSolicitante"), Solicitante = f.Texto("Solicitante"),
                Titulo = f.Texto("Titulo"), Detalle = f.Texto("Detalle"), AreaSolicitante = f.Texto("AreaSolicitante"), AreaDescripcion = f.Texto("AreaDescripcion"),
                Linea = f.Texto("Linea"), LineaDescripcion = f.Texto("LineaDescripcion"), Tipo = f.Texto("Tipo"), TipoDescripcion = f.Texto("TipoDescripcion"),
                Estado = f.Texto("Estado"), EstadoDescripcion = f.Texto("EstadoDescripcion"), Prioridad = f.EnteroNulo("Prioridad"),
                Impacto = f.EnteroNulo("Impacto"), Complejidad = f.EnteroNulo("Complejidad"), UsuarioTI = f.Texto("UsuarioTI"), Responsable = f.Texto("Responsable"),
                FechaRegistro = f.Fecha("FechaRegistro"), UltimaFechaModif = f.Fecha("UltimaFechaModif"), SlaMinutosRestantes = f.EnteroNulo("SlaMinutosRestantes"),
                TieneAprobacionPendiente = f.Booleano("TieneAprobacionPendiente"), Accion = f.Texto("Accion")
            }, ct);
            var catalogos = respuesta.Catalogos;
            catalogos.Estados = await lector.ListaAsync(Catalogo, ct);
            catalogos.Areas = await lector.ListaAsync(Catalogo, ct);
            catalogos.Operadores = await lector.ListaAsync(Catalogo, ct);
            catalogos.Lineas = await lector.ListaAsync(f => new GestionTicketsTILinea { Codigo = f.Texto("Codigo"), Descripcion = f.Texto("Descripcion"), Area = f.Texto("Area") }, ct);
            catalogos.Items = await lector.ListaAsync(f => new GestionTicketsTIItemCatalogo { Codigo = f.Texto("Codigo"), Descripcion = f.Texto("Descripcion"), Linea = f.Texto("Linea") }, ct);
            catalogos.Tipos = await lector.ListaAsync(Catalogo, ct);
            catalogos.Categorias = await lector.ListaAsync(Catalogo, ct);
            catalogos.SubTipos = await lector.ListaAsync(f => new GestionTicketsTISubTipo
            {
                Codigo = f.Texto("Codigo"), Descripcion = f.Texto("Descripcion"), Tipo = f.Texto("Tipo"), Categoria = f.Texto("Categoria")
            }, ct);
            return respuesta;
        }, ct);
    }

    public async Task<GestionTicketTIDetalle> ObtenerDetalleAsync(string usuario, string area, string incidenciaNumero, CancellationToken ct)
    {
        var (usuarioValido, areaValida, incidencia) = (Validacion.Usuario(usuario), Validacion.Area(area), Validacion.Incidencia(incidenciaNumero));
        return await baseDatos.LeerAsync("dbo.Usp_TI_Obtener_DetalleGestionTicketTI", p => Ticket(p, usuarioValido, areaValida, incidencia), async lector =>
        {
            var detalle = await lector.FilaAsync(f => new GestionTicketTIDetalle
            {
                IncidenciaNumero = f.Texto("IncidenciaNumero"), UsuarioSolicitante = f.Texto("UsuarioSolicitante"), Solicitante = f.Texto("Solicitante"),
                CorreoSolicitante = f.Texto("CorreoSolicitante"), AreaSolicitante = f.Texto("AreaSolicitante"), AreaSolicitanteDescripcion = f.Texto("AreaSolicitanteDescripcion"),
                AreaTI = f.Texto("AreaTI"), AreaTIDescripcion = f.Texto("AreaTIDescripcion"), UsuarioTI = f.Texto("UsuarioTI"), Responsable = f.Texto("Responsable"),
                UsuarioAsigno = f.Texto("UsuarioAsigno"), Linea = f.Texto("Linea"), LineaDescripcion = f.Texto("LineaDescripcion"), Item = f.Texto("Item"),
                ItemDescripcion = f.Texto("ItemDescripcion"), Tipo = f.Texto("Tipo"), TipoDescripcion = f.Texto("TipoDescripcion"), SubTipo = f.Texto("SubTipo"),
                SubTipoDescripcion = f.Texto("SubTipoDescripcion"), Categoria = f.Texto("Categoria"), CategoriaDescripcion = f.Texto("CategoriaDescripcion"),
                Estado = f.Texto("Estado"), EstadoDescripcion = f.Texto("EstadoDescripcion"), AreaCausante = f.Texto("AreaCausante"),
                AreaCausanteDescripcion = f.Texto("AreaCausanteDescripcion"), Titulo = f.Texto("Titulo"), Detalle = f.Texto("Detalle"), MensajeError = f.Texto("MensajeError"),
                FechaRegistro = f.Fecha("FechaRegistro"), FechaAsignacion = f.FechaNula("FechaAsignacion"), FechaAtencion = f.FechaNula("FechaAtencion"),
                FechaCierre = f.FechaNula("FechaCierre"), SlaObjetivoMinutos = f.EnteroNulo("SlaObjetivoMinutos"), Prioridad = f.EnteroNulo("Prioridad"),
                Impacto = f.EnteroNulo("Impacto"), Complejidad = f.EnteroNulo("Complejidad"), CanalRegistro = f.Texto("CanalRegistro"), CausaRaiz = f.Texto("CausaRaiz"),
                SolucionTecnica = f.Texto("SolucionTecnica"), RespuestaUsuario = f.Texto("RespuestaUsuario"), TipoResolucion = f.Texto("TipoResolucion"),
                Calificacion = f.ByteNulo("Calificacion"), ComentarioCalificacion = f.Texto("ComentarioCalificacion"), UltimaFechaModif = f.Fecha("UltimaFechaModif"),
                SlaMinutosRestantes = f.EnteroNulo("SlaMinutosRestantes")
            }, ct);
            if (detalle is null) return null;
            detalle.HistorialEstados = await lector.ListaAsync(f => new GestionTicketTIEstado
            {
                Secuencia = f.Entero("Secuencia"), Estado = f.Texto("Estado"), EstadoDescripcion = f.Texto("EstadoDescripcion"), Actor = f.Texto("Actor"),
                FechaCambio = f.Fecha("FechaCambio"), Observacion = f.Texto("Observacion")
            }, ct);
            detalle.Avances = await lector.ListaAsync(f => new GestionTicketTIAvance
            {
                Secuencia = f.Entero("Secuencia"), UsuarioTI = f.Texto("UsuarioTI"), Responsable = f.Texto("Responsable"), FechaAvance = f.Fecha("FechaAvance"),
                Detalle = f.Texto("Detalle"), TiempoUtilizado = f.DecimalNulo("TiempoUtilizado"), PorcentajeAvance = f.DecimalNulo("PorcentajeAvance")
            }, ct);
            detalle.Mensajes = await lector.ListaAsync(f => new GestionTicketTIMensaje
            {
                Secuencia = f.Entero("Secuencia"), TipoAutor = f.Texto("TipoAutor"), Autor = f.Texto("Autor"), Contenido = f.Texto("Contenido"),
                FechaMensaje = f.Fecha("FechaMensaje"), EsInterno = f.Booleano("EsInterno")
            }, ct);
            detalle.Adjuntos = await lector.ListaAsync(f => new GestionTicketTIAdjunto
            {
                Secuencia = f.Entero("Secuencia"), MensajeSecuencia = f.EnteroNulo("MensajeSecuencia"), NombreOriginal = f.Texto("NombreOriginal"),
                TipoMime = f.Texto("TipoMime"), TamanoBytes = f.Largo("TamanoBytes"), FechaRegistro = f.Fecha("FechaRegistro"), UsuarioRegistro = f.Texto("UsuarioRegistro")
            }, ct);
            detalle.Documentos = await lector.ListaAsync(f => new GestionTicketTIDocumento
            {
                Secuencia = f.Entero("Secuencia"), CompaniaSocio = f.Texto("CompaniaSocio"), TipoDocumento = f.Texto("TipoDocumento"),
                NumeroDocumento = f.Texto("NumeroDocumento"), Descripcion = f.Texto("Descripcion")
            }, ct);
            detalle.Aprobaciones = await lector.ListaAsync(f => new GestionTicketTIAprobacion
            {
                Secuencia = f.Entero("Secuencia"), AccionCodigo = f.Texto("AccionCodigo"), AccionNombre = f.Texto("AccionNombre"), NivelRiesgo = f.Texto("NivelRiesgo"),
                Estado = f.Texto("Estado"), Justificacion = f.Texto("Justificacion"), ComentarioRespuesta = f.Texto("ComentarioRespuesta"),
                UsuarioSolicitante = f.Texto("UsuarioSolicitante"), Solicitante = f.Texto("Solicitante"), UsuarioAprobador = f.Texto("UsuarioAprobador"),
                Aprobador = f.Texto("Aprobador"), FechaSolicitud = f.Fecha("FechaSolicitud"), FechaRespuesta = f.FechaNula("FechaRespuesta"),
                ParametrosJson = f.Texto("ParametrosJson")
            }, ct);
            return detalle;
        }, ct) ?? throw new KeyNotFoundException("El ticket indicado no existe.");
    }

    public Task ClasificarAsync(string usuario, string area, string incidenciaNumero, ClasificarTicketTISolicitud s, CancellationToken ct)
    {
        var linea = Validacion.CodigoExacto(s.Linea, 3, "Selecciona una línea válida.");
        var item = Validacion.Codigo(s.Item, 20, "Selecciona un item válido.");
        var tipo = Validacion.CodigoExacto(s.Tipo, 3, "Selecciona un tipo válido.");
        var subTipo = Validacion.CodigoExacto(s.SubTipo, 3, "Selecciona un subtipo válido.");
        var categoria = Validacion.Codigo(s.Categoria, 20, "Selecciona una categoría válida.");
        var areaCausante = string.IsNullOrWhiteSpace(s.AreaCausante) ? null : Validacion.CodigoExacto(s.AreaCausante, 3, "Selecciona un área causante válida.");
        Validacion.Nivel(s.Prioridad, "La prioridad");
        Validacion.Nivel(s.Impacto, "El impacto");
        Validacion.Nivel(s.Complejidad, "La complejidad");
        return EjecutarAccionAsync("dbo.Usp_TI_Clasificar_Ticket", usuario, area, incidenciaNumero, p =>
        {
            p.Add("@cLinea", SqlDbType.Char, 3).Value = linea;
            p.Add("@cItem", SqlDbType.VarChar, 20).Value = item;
            p.Add("@cTipo", SqlDbType.Char, 3).Value = tipo;
            p.Add("@cSubTipo", SqlDbType.Char, 3).Value = subTipo;
            p.Add("@cCategoria", SqlDbType.VarChar, 20).Value = categoria;
            p.Add("@cAreaCausante", SqlDbType.Char, 3).Value = BaseDatos.Opcional(areaCausante);
            p.Add("@nPrioridad", SqlDbType.Int).Value = BaseDatos.Opcional(s.Prioridad);
            p.Add("@nImpacto", SqlDbType.Int).Value = BaseDatos.Opcional(s.Impacto);
            p.Add("@nComplejidad", SqlDbType.Int).Value = BaseDatos.Opcional(s.Complejidad);
        }, ct);
    }

    public Task AsignarAsync(string usuario, string area, string incidenciaNumero, AsignarTicketTISolicitud s, CancellationToken ct)
    {
        var responsable = Validacion.Requerido(s.UsuarioTI, 20, "Selecciona un responsable válido.");
        return EjecutarAccionAsync("dbo.Usp_TI_Asignar_Ticket", usuario, area, incidenciaNumero, p => p.Add("@cUsuarioTI", SqlDbType.VarChar, 20).Value = responsable, ct);
    }

    public Task SolicitarInformacionAsync(string usuario, string area, string incidenciaNumero, SolicitarInformacionTISolicitud s, CancellationToken ct)
    {
        var mensaje = Validacion.Texto(s.Mensaje, 10, 1000, "La solicitud de información");
        return EjecutarAccionAsync("dbo.Usp_TI_Solicitar_InformacionTicket", usuario, area, incidenciaNumero, p => p.Add("@cMensaje", SqlDbType.NVarChar, 1000).Value = mensaje, ct);
    }

    public Task ResolverAsync(string usuario, string area, string incidenciaNumero, ResolverTicketTISolicitud s, CancellationToken ct)
    {
        var causaRaiz = Validacion.Texto(s.CausaRaiz, 5, 4000, "La causa raíz");
        var solucion = Validacion.Texto(s.Solucion, 5, 4000, "La solución");
        var respuestaUsuario = Validacion.Texto(s.RespuestaUsuario, 5, 4000, "La respuesta al usuario");
        var tipoResolucion = s.TipoResolucion.Trim().ToUpperInvariant();
        if (tipoResolucion.Length > 20) throw new ArgumentException("El tipo de resolución no puede superar los 20 caracteres.");
        return EjecutarAccionAsync("dbo.Usp_TI_Resolver_TicketTI", usuario, area, incidenciaNumero, p =>
        {
            p.Add("@cCausaRaiz", SqlDbType.NVarChar, -1).Value = causaRaiz;
            p.Add("@cSolucion", SqlDbType.NVarChar, -1).Value = solucion;
            p.Add("@cRespuestaUsuario", SqlDbType.NVarChar, -1).Value = respuestaUsuario;
            p.Add("@cTipoResolucion", SqlDbType.VarChar, 20).Value = tipoResolucion;
        }, ct);
    }

    public Task NoProcedeAsync(string usuario, string area, string incidenciaNumero, NoProcedeTicketTISolicitud s, CancellationToken ct)
    {
        var motivo = Validacion.Texto(s.Motivo, 10, 1000, "El motivo de No Procede");
        return EjecutarAccionAsync("dbo.Usp_TI_NoProcede_Ticket", usuario, area, incidenciaNumero, p => p.Add("@cMotivo", SqlDbType.NVarChar, 1000).Value = motivo, ct);
    }

    public Task ResponderAprobacionAsync(string usuario, string area, string incidenciaNumero, int secuencia, ResponderAprobacionTISolicitud s, CancellationToken ct)
    {
        if (secuencia <= 0) throw new ArgumentException("La solicitud de aprobación indicada no es válida.");
        var comentario = Validacion.Opcional(s.Comentario, 1000, "El comentario");
        if (!s.Aprobar && comentario is null) throw new ArgumentException("Indica el motivo del rechazo.");
        return EjecutarAccionAsync("dbo.Usp_TI_Responder_AprobacionTicket", usuario, area, incidenciaNumero, p =>
        {
            p.Add("@nSecuencia", SqlDbType.Int).Value = secuencia;
            p.Add("@lAprobar", SqlDbType.Bit).Value = s.Aprobar;
            p.Add("@cComentario", SqlDbType.NVarChar, 1000).Value = BaseDatos.Opcional(comentario);
        }, ct);
    }

    public async Task<ArchivoDescarga> ObtenerArchivoAsync(string usuario, string area, string incidenciaNumero, int secuencia, CancellationToken ct)
    {
        var (usuarioValido, areaValida, incidencia) = (Validacion.Usuario(usuario), Validacion.Area(area), Validacion.Incidencia(incidenciaNumero));
        if (secuencia <= 0) throw new ArgumentException("El adjunto indicado no es válido.");
        return await baseDatos.LeerAsync("dbo.Usp_TI_Obtener_AdjuntoTicketTI", p =>
        {
            Ticket(p, usuarioValido, areaValida, incidencia);
            p.Add("@nSecuencia", SqlDbType.Int).Value = secuencia;
        }, lector => lector.FilaAsync(f => Archivos.Descarga(f, Archivos.Incidencias, "La ruta del adjunto no es válida.",
            "El archivo asociado al ticket ya no se encuentra disponible."), ct), ct)
            ?? throw new KeyNotFoundException("El archivo indicado no existe.");
    }

    // Todas las acciones sobre un ticket reciben la identidad del operador, el ticket y una correlación para la auditoría.
    private Task EjecutarAccionAsync(string procedimiento, string usuario, string area, string incidenciaNumero, Action<SqlParameterCollection> parametros, CancellationToken ct)
    {
        var (usuarioValido, areaValida, incidencia) = (Validacion.Usuario(usuario), Validacion.Area(area), Validacion.Incidencia(incidenciaNumero));
        return baseDatos.EjecutarAsync(procedimiento, p =>
        {
            Ticket(p, usuarioValido, areaValida, incidencia);
            parametros(p);
            p.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
        }, ct);
    }

    private static void Identidad(SqlParameterCollection p, string usuario, string area)
    {
        p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;
        p.Add("@cArea", SqlDbType.Char, 3).Value = area;
    }

    private static void Ticket(SqlParameterCollection p, string usuario, string area, string incidencia)
    {
        Identidad(p, usuario, area);
        p.Add("@cIncidenciaNumero", SqlDbType.VarChar, 12).Value = incidencia;
    }

    private static GestionTicketsTICatalogoItem Catalogo(SqlDataReader f) => new() { Codigo = f.Texto("Codigo"), Descripcion = f.Texto("Descripcion") };
}
