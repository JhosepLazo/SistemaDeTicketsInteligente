/**
 * Archivo: BaseConocimientoTIBLL.cs
 * Objetivo: Gestionar los artículos de la Base de Conocimiento: consultarlos, crearlos, editarlos y llevarlos por su ciclo
 *   (borrador, validación, activo, inactivo).
 * Responsabilidad: Validar identidad, código, contenido y clasificación, y ejecutar los procedimientos del módulo.
 * Dependencias: BaseDatos (Usp_TI_Obtener_BaseConocimientoTI, Usp_TI_Obtener_DetalleBaseConocimientoTI, Usp_TI_Crear_BaseConocimientoTI,
 *   Usp_TI_Actualizar_BaseConocimientoTI, Usp_TI_EnviarValidacion_BaseConocimientoTI, Usp_TI_Validar_BaseConocimientoTI,
 *   Usp_TI_Inactivar_BaseConocimientoTI).
 * Flujo: BaseConocimientoTIController -> BaseConocimientoTIBLL -> Stored Procedures.
 * Consideraciones: La coherencia entre línea, item, tipo, subtipo y categoría se valida definitivamente en los procedimientos.
 */

namespace SistemaTicketsInteligente.Api.BLL;

public sealed class BaseConocimientoTIBLL(BaseDatos baseDatos)
{
    public Task<BaseConocimientoTIRespuesta> ObtenerAsync(CancellationToken ct) =>
        baseDatos.LeerAsync("dbo.Usp_TI_Obtener_BaseConocimientoTI", _ => { }, async lector =>
        {
            var respuesta = new BaseConocimientoTIRespuesta();
            respuesta.Resumen = await lector.FilaAsync(f => new BaseConocimientoTIResumen
            {
                Total = f.Entero("Total"), Activos = f.Entero("Activos"), Borradores = f.Entero("Borradores"), PendientesValidacion = f.Entero("PendientesValidacion"),
                PorRevisar = f.Entero("PorRevisar"), CandidatosDesdeTickets = f.Entero("CandidatosDesdeTickets")
            }, ct) ?? new();
            respuesta.Articulos = await lector.ListaAsync(f => new BaseConocimientoTIItem
            {
                ConocimientoCodigo = f.Texto("ConocimientoCodigo"), Titulo = f.Texto("Titulo"), Problema = f.Texto("Problema"), Solucion = f.Texto("Solucion"),
                Estado = f.Texto("Estado"), EstadoDescripcion = f.Texto("EstadoDescripcion"), Linea = f.Texto("Linea"), LineaDescripcion = f.Texto("LineaDescripcion"),
                Item = f.Texto("Item"), ItemDescripcion = f.Texto("ItemDescripcion"), Tipo = f.Texto("Tipo"), TipoDescripcion = f.Texto("TipoDescripcion"),
                SubTipo = f.Texto("SubTipo"), SubTipoDescripcion = f.Texto("SubTipoDescripcion"), Categoria = f.Texto("Categoria"),
                CategoriaDescripcion = f.Texto("CategoriaDescripcion"), IncidenciaOrigen = f.Texto("IncidenciaOrigen"), Validador = f.Texto("Validador"),
                FechaCreacion = f.Fecha("FechaCreacion"), FechaValidacion = f.FechaNula("FechaValidacion"), FechaRevision = f.FechaNula("FechaRevision"),
                RequiereRevision = f.Booleano("RequiereRevision")
            }, ct);
            var catalogos = respuesta.Catalogos;
            catalogos.Lineas = await lector.ListaAsync(Catalogo, ct);
            catalogos.Items = await lector.ListaAsync(f => new BaseConocimientoTIItemCatalogo { Codigo = f.Texto("Codigo"), Linea = f.Texto("Linea"), Descripcion = f.Texto("Descripcion") }, ct);
            catalogos.Tipos = await lector.ListaAsync(Catalogo, ct);
            catalogos.Categorias = await lector.ListaAsync(Catalogo, ct);
            catalogos.SubTipos = await lector.ListaAsync(f => new BaseConocimientoTISubTipoCatalogo
            {
                Codigo = f.Texto("Codigo"), Tipo = f.Texto("Tipo"), Categoria = f.Texto("Categoria"), Descripcion = f.Texto("Descripcion")
            }, ct);
            catalogos.TicketsOrigen = await lector.ListaAsync(f => new BaseConocimientoTITicketOrigen
            {
                IncidenciaNumero = f.Texto("IncidenciaNumero"), Titulo = f.Texto("Titulo"), Detalle = f.Texto("Detalle"), MensajeError = f.Texto("MensajeError"),
                Linea = f.Texto("Linea"), Item = f.Texto("Item"), Tipo = f.Texto("Tipo"), SubTipo = f.Texto("SubTipo"), Categoria = f.Texto("Categoria"),
                CausaRaiz = f.Texto("CausaRaiz"), SolucionTecnica = f.Texto("SolucionTecnica"), FechaCierre = f.FechaNula("FechaCierre")
            }, ct);
            return respuesta;
        }, ct);

    public async Task<BaseConocimientoTIDetalle> ObtenerDetalleAsync(string codigo, CancellationToken ct)
    {
        var codigoValido = ValidarCodigo(codigo);
        return await baseDatos.LeerAsync("dbo.Usp_TI_Obtener_DetalleBaseConocimientoTI", p => p.Add("@cConocimientoCodigo", SqlDbType.VarChar, 20).Value = codigoValido,
            lector => lector.FilaAsync(f => new BaseConocimientoTIDetalle
            {
                ConocimientoCodigo = f.Texto("ConocimientoCodigo"), Titulo = f.Texto("Titulo"), Problema = f.Texto("Problema"), Sintomas = f.Texto("Sintomas"),
                MensajeError = f.Texto("MensajeError"), Causa = f.Texto("Causa"), Solucion = f.Texto("Solucion"), Procedimiento = f.Texto("Procedimiento"),
                Linea = f.Texto("Linea"), LineaDescripcion = f.Texto("LineaDescripcion"), Item = f.Texto("Item"), ItemDescripcion = f.Texto("ItemDescripcion"),
                Tipo = f.Texto("Tipo"), TipoDescripcion = f.Texto("TipoDescripcion"), SubTipo = f.Texto("SubTipo"), SubTipoDescripcion = f.Texto("SubTipoDescripcion"),
                Categoria = f.Texto("Categoria"), CategoriaDescripcion = f.Texto("CategoriaDescripcion"), IncidenciaOrigen = f.Texto("IncidenciaOrigen"),
                Estado = f.Texto("Estado"), EstadoDescripcion = f.Texto("EstadoDescripcion"), UsuarioValida = f.Texto("UsuarioValida"), Validador = f.Texto("Validador"),
                FechaCreacion = f.Fecha("FechaCreacion"), FechaValidacion = f.FechaNula("FechaValidacion"), FechaRevision = f.FechaNula("FechaRevision"),
                RequiereRevision = f.Booleano("RequiereRevision")
            }, ct), ct)
            ?? throw new KeyNotFoundException("El artículo de conocimiento no existe.");
    }

    public async Task<BaseConocimientoTICreadoRespuesta> CrearAsync(string usuario, GuardarBaseConocimientoTISolicitud solicitud, CancellationToken ct)
    {
        Normalizar(solicitud);
        var usuarioValido = Validacion.Usuario(usuario);
        var codigo = (await baseDatos.EscalarAsync("dbo.Usp_TI_Crear_BaseConocimientoTI", p => ParametrosArticulo(p, usuarioValido, solicitud), ct))?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(codigo)) throw new InvalidOperationException("No fue posible obtener el código del artículo creado.");
        return new BaseConocimientoTICreadoRespuesta { ConocimientoCodigo = codigo };
    }

    public Task ActualizarAsync(string usuario, string codigo, GuardarBaseConocimientoTISolicitud solicitud, CancellationToken ct)
    {
        Normalizar(solicitud);
        var (usuarioValido, codigoValido) = (Validacion.Usuario(usuario), ValidarCodigo(codigo));
        return baseDatos.EjecutarAsync("dbo.Usp_TI_Actualizar_BaseConocimientoTI", p =>
        {
            ParametrosArticulo(p, usuarioValido, solicitud);
            p.Add("@cConocimientoCodigo", SqlDbType.VarChar, 20).Value = codigoValido;
        }, ct);
    }

    public Task EnviarValidacionAsync(string usuario, string codigo, CancellationToken ct) => CambiarEstadoAsync("dbo.Usp_TI_EnviarValidacion_BaseConocimientoTI", usuario, codigo, ct);
    public Task ValidarAsync(string usuario, string codigo, CancellationToken ct) => CambiarEstadoAsync("dbo.Usp_TI_Validar_BaseConocimientoTI", usuario, codigo, ct);
    public Task InactivarAsync(string usuario, string codigo, CancellationToken ct) => CambiarEstadoAsync("dbo.Usp_TI_Inactivar_BaseConocimientoTI", usuario, codigo, ct);

    private Task CambiarEstadoAsync(string procedimiento, string usuario, string codigo, CancellationToken ct)
    {
        var (usuarioValido, codigoValido) = (Validacion.Usuario(usuario), ValidarCodigo(codigo));
        return baseDatos.EjecutarAsync(procedimiento, p =>
        {
            p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuarioValido;
            p.Add("@cConocimientoCodigo", SqlDbType.VarChar, 20).Value = codigoValido;
            p.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
        }, ct);
    }

    private static void ParametrosArticulo(SqlParameterCollection p, string usuario, GuardarBaseConocimientoTISolicitud s)
    {
        p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;
        p.Add("@cTitulo", SqlDbType.NVarChar, 250).Value = s.Titulo;
        p.Add("@cProblema", SqlDbType.NVarChar, -1).Value = s.Problema;
        p.Add("@cSintomas", SqlDbType.NVarChar, -1).Value = s.Sintomas;
        p.Add("@cMensajeError", SqlDbType.NVarChar, 1000).Value = BaseDatos.Opcional(s.MensajeError);
        p.Add("@cCausa", SqlDbType.NVarChar, -1).Value = s.Causa;
        p.Add("@cSolucion", SqlDbType.NVarChar, -1).Value = s.Solucion;
        p.Add("@cProcedimiento", SqlDbType.NVarChar, -1).Value = BaseDatos.Opcional(s.Procedimiento);
        p.Add("@cLinea", SqlDbType.Char, 3).Value = s.Linea;
        p.Add("@cItem", SqlDbType.VarChar, 20).Value = s.Item;
        p.Add("@cTipo", SqlDbType.Char, 3).Value = s.Tipo;
        p.Add("@cSubTipo", SqlDbType.Char, 3).Value = s.SubTipo;
        p.Add("@cCategoria", SqlDbType.VarChar, 20).Value = s.Categoria;
        p.Add("@cIncidenciaOrigen", SqlDbType.VarChar, 12).Value = BaseDatos.Opcional(s.IncidenciaOrigen);
        p.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
    }

    private static string ValidarCodigo(string codigo)
    {
        var valor = codigo.Trim().ToUpperInvariant();
        if (valor.Length is 0 or > 20 || !valor.StartsWith("KB-", StringComparison.Ordinal)) throw new ArgumentException("El código del artículo no es válido.");
        return valor;
    }

    private static void Normalizar(GuardarBaseConocimientoTISolicitud s)
    {
        s.Titulo = Validacion.Texto(s.Titulo, 5, 250, "El título");
        s.Problema = Validacion.Texto(s.Problema, 10, 6000, "El problema");
        s.Sintomas = s.Sintomas.Trim();
        if (s.Sintomas.Length is < 5 or > 6000) throw new ArgumentException("Los síntomas deben contener entre 5 y 6000 caracteres.");
        s.MensajeError = Validacion.Opcional(s.MensajeError, 1000, "El mensaje de error");
        s.Causa = Validacion.Texto(s.Causa, 5, 6000, "La causa");
        s.Solucion = Validacion.Texto(s.Solucion, 5, 6000, "La solución");
        s.Procedimiento = Validacion.Opcional(s.Procedimiento, 8000, "El procedimiento");
        s.Linea = Validacion.CodigoExacto(s.Linea, 3, "Selecciona una línea válida.");
        s.Item = Validacion.Codigo(s.Item, 20, "Selecciona un item válido.");
        s.Tipo = Validacion.CodigoExacto(s.Tipo, 3, "Selecciona un tipo válido.");
        s.SubTipo = Validacion.CodigoExacto(s.SubTipo, 3, "Selecciona un subtipo válido.");
        s.Categoria = Validacion.Codigo(s.Categoria, 20, "Selecciona una categoría válida.");
        s.IncidenciaOrigen = s.IncidenciaOrigen?.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(s.IncidenciaOrigen) && s.IncidenciaOrigen.Length > 12) throw new ArgumentException("El ticket de origen no es válido.");
    }

    private static BaseConocimientoTICatalogo Catalogo(SqlDataReader f) => new() { Codigo = f.Texto("Codigo"), Descripcion = f.Texto("Descripcion") };
}
