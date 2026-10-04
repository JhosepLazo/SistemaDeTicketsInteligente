/**
 * Archivo: ConfiguracionTIBLL.cs
 * Objetivo: Administrar los maestros de TI: áreas, líneas, ítems, tipos, categorías, subtipos, matriz de clasificación, SLA,
 *   usuarios y cargos corporativos, formatos de soporte y visibilidad de artículos.
 * Responsabilidad: Validar cada maestro y ejecutar su procedimiento; sincronizar usuarios y cargos desde Spring.
 * Dependencias: BaseDatos (Usp_TI_Obtener_ConfiguracionTI y Usp_TI_Guardar_*), IdentidadCorporativa y Archivos.
 * Flujo: ConfiguracionTIController -> ConfiguracionTIBLL -> Stored Procedures (y Spring para la sincronización).
 * Consideraciones: Los textos opcionales vacíos se guardan como NULL. Formatos y visibilidad de artículos tienen backend
 *   completo pero no pantalla (pendiente): sin la visibilidad, ningún artículo llega al colaborador.
 */

namespace SistemaTicketsInteligente.Api.BLL;

public sealed class ConfiguracionTIBLL(BaseDatos baseDatos, IdentidadCorporativa identidadCorporativa)
{
    private const long MaximoFormatoBytes = 15 * 1024 * 1024;
    private static readonly HashSet<string> ExtensionesFormato = new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".doc", ".docx", ".xls", ".xlsx" };

    public Task<ConfiguracionTIRespuesta> ObtenerAsync(string usuario, CancellationToken ct)
    {
        var usuarioValido = Validacion.Usuario(usuario);
        return baseDatos.LeerAsync("dbo.Usp_TI_Obtener_ConfiguracionTI", p => p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuarioValido, async lector =>
        {
            var r = new ConfiguracionTIRespuesta();
            r.Areas = await lector.ListaAsync(f => new ConfiguracionTIArea { Area = f.Texto("Area"), Descripcion = f.Texto("Descripcion"), Estado = f.Texto("Estado"), Telefono = f.Texto("Telefono") }, ct);
            r.Lineas = await lector.ListaAsync(f => new ConfiguracionTILinea { Linea = f.Texto("Linea"), Area = f.Texto("Area"), Descripcion = f.Texto("Descripcion"), Estado = f.Texto("Estado") }, ct);
            r.Items = await lector.ListaAsync(f => new ConfiguracionTIItem { Item = f.Texto("Item"), Linea = f.Texto("Linea"), Descripcion = f.Texto("Descripcion"), Estado = f.Texto("Estado") }, ct);
            r.Tipos = await lector.ListaAsync(f => new ConfiguracionTITipo { Tipo = f.Texto("Tipo"), Descripcion = f.Texto("Descripcion"), Abreviatura = f.Texto("Abreviatura"), Estado = f.Texto("Estado") }, ct);
            r.Categorias = await lector.ListaAsync(f => new ConfiguracionTICategoria { Categoria = f.Texto("Categoria"), Descripcion = f.Texto("Descripcion"), Abreviatura = f.Texto("Abreviatura"), Estado = f.Texto("Estado") }, ct);
            r.SubTipos = await lector.ListaAsync(f => new ConfiguracionTISubTipo
            {
                Tipo = f.Texto("Tipo"), SubTipo = f.Texto("SubTipo"), Categoria = f.Texto("Categoria"), Descripcion = f.Texto("Descripcion"),
                Abreviatura = f.Texto("Abreviatura"), Estado = f.Texto("Estado")
            }, ct);
            r.Matriz = await lector.ListaAsync(f => new ConfiguracionTIMatriz
            {
                Item = f.Texto("Item"), Categoria = f.Texto("Categoria"), Prioridad = f.EnteroNulo("Prioridad"), Impacto = f.EnteroNulo("Impacto"),
                Complejidad = f.EnteroNulo("Complejidad"), Estado = f.Texto("Estado")
            }, ct);
            r.Sla = await lector.ListaAsync(f => new ConfiguracionTISla { Prioridad = f.Entero("Prioridad"), SlaObjetivoMinutos = f.Entero("SlaObjetivoMinutos"), Estado = f.Texto("Estado") }, ct);
            r.Usuarios = await lector.ListaAsync(f => new ConfiguracionTIUsuario
            {
                Usuario = f.Texto("Usuario"), NombreCompleto = f.Texto("NombreCompleto"), Area = f.Texto("Area"), Cargo = f.Texto("Cargo"), Perfil = f.Texto("Perfil"),
                Correo = f.Texto("Correo"), Estado = f.Texto("Estado"), FuenteIdentidad = f.Texto("FuenteIdentidad"), Documento = f.Texto("Documento"),
                EstadoCorporativo = f.Texto("EstadoCorporativo"), UltimaSincronizacion = f.FechaNula("UltimaSincronizacion")
            }, ct);
            r.Formatos = await lector.ListaAsync(f => new ConfiguracionTIFormato
            {
                FormatoCodigo = f.Texto("FormatoCodigo"), Titulo = f.Texto("Titulo"), Descripcion = f.Texto("Descripcion"), NombreOriginal = f.Texto("NombreOriginal"),
                TipoMime = f.Texto("TipoMime"), TipoTicket = f.Texto("TipoTicket"), Estado = f.Texto("Estado")
            }, ct);
            r.Conocimientos = await lector.ListaAsync(f => new ConfiguracionTIConocimiento
            {
                ConocimientoCodigo = f.Texto("ConocimientoCodigo"), Titulo = f.Texto("Titulo"), Estado = f.Texto("Estado"), VisibleUsuario = f.Booleano("VisibleUsuario")
            }, ct);
            return r;
        }, ct);
    }

    public Task GuardarAreaAsync(string usuario, GuardarAreaTISolicitud s, CancellationToken ct)
    {
        var (area, descripcion, estado) = (Exacto(s.Area, "área"), Validacion.Texto(s.Descripcion, 2, 60, "La descripción"), Validacion.Estado(s.Estado));
        return GuardarAsync("dbo.Usp_TI_Guardar_Area", usuario, p =>
        {
            p.Add("@cArea", SqlDbType.Char, 3).Value = area;
            p.Add("@cDescripcion", SqlDbType.VarChar, 60).Value = descripcion;
            p.Add("@cTelefono", SqlDbType.VarChar, 20).Value = Opcional(s.Telefono);
            p.Add("@cEstado", SqlDbType.VarChar, 2).Value = estado;
        }, ct);
    }

    public Task GuardarLineaAsync(string usuario, GuardarLineaTISolicitud s, CancellationToken ct)
    {
        var (linea, area, descripcion, estado) = (Exacto(s.Linea, "línea"), Exacto(s.Area, "área"), Validacion.Texto(s.Descripcion, 2, 60, "La descripción"), Validacion.Estado(s.Estado));
        return GuardarAsync("dbo.Usp_TI_Guardar_Linea", usuario, p =>
        {
            p.Add("@cLinea", SqlDbType.Char, 3).Value = linea;
            p.Add("@cArea", SqlDbType.Char, 3).Value = area;
            p.Add("@cDescripcion", SqlDbType.VarChar, 60).Value = descripcion;
            p.Add("@cEstado", SqlDbType.VarChar, 2).Value = estado;
        }, ct);
    }

    public Task GuardarItemAsync(string usuario, GuardarItemTISolicitud s, CancellationToken ct)
    {
        var (item, linea, descripcion, estado) = (Variable(s.Item, 20, "ítem"), Exacto(s.Linea, "línea"), Validacion.Texto(s.Descripcion, 2, 255, "La descripción"), Validacion.Estado(s.Estado));
        return GuardarAsync("dbo.Usp_TI_Guardar_Item", usuario, p =>
        {
            p.Add("@cItem", SqlDbType.VarChar, 20).Value = item;
            p.Add("@cLinea", SqlDbType.Char, 3).Value = linea;
            p.Add("@cDescripcion", SqlDbType.VarChar, 255).Value = descripcion;
            p.Add("@cEstado", SqlDbType.VarChar, 2).Value = estado;
        }, ct);
    }

    public Task GuardarTipoAsync(string usuario, GuardarTipoTISolicitud s, CancellationToken ct)
    {
        var (tipo, descripcion, estado) = (Exacto(s.Tipo, "tipo"), Validacion.Texto(s.Descripcion, 2, 60, "La descripción"), Validacion.Estado(s.Estado));
        var abreviatura = Validacion.Opcional(s.Abreviatura, 60, "El campo abreviatura");
        return GuardarAsync("dbo.Usp_TI_Guardar_Tipo", usuario, p =>
        {
            p.Add("@cTipo", SqlDbType.Char, 3).Value = tipo;
            p.Add("@cDescripcion", SqlDbType.VarChar, 60).Value = descripcion;
            p.Add("@cAbreviatura", SqlDbType.VarChar, 60).Value = Opcional(abreviatura);
            p.Add("@cEstado", SqlDbType.VarChar, 2).Value = estado;
        }, ct);
    }

    public Task GuardarCategoriaAsync(string usuario, GuardarCategoriaTISolicitud s, CancellationToken ct)
    {
        var (categoria, descripcion, estado) = (Variable(s.Categoria, 20, "categoría"), Validacion.Texto(s.Descripcion, 2, 60, "La descripción"), Validacion.Estado(s.Estado));
        var abreviatura = Validacion.Opcional(s.Abreviatura, 3, "El campo abreviatura");
        return GuardarAsync("dbo.Usp_TI_Guardar_Categoria", usuario, p =>
        {
            p.Add("@cCategoria", SqlDbType.VarChar, 20).Value = categoria;
            p.Add("@cDescripcion", SqlDbType.VarChar, 60).Value = descripcion;
            p.Add("@cAbreviatura", SqlDbType.VarChar, 3).Value = Opcional(abreviatura);
            p.Add("@cEstado", SqlDbType.VarChar, 2).Value = estado;
        }, ct);
    }

    public Task GuardarSubTipoAsync(string usuario, GuardarSubTipoTISolicitud s, CancellationToken ct)
    {
        var (tipo, subTipo, categoria) = (Exacto(s.Tipo, "tipo"), Exacto(s.SubTipo, "subtipo"), Variable(s.Categoria, 20, "categoría"));
        var (descripcion, estado) = (Validacion.Texto(s.Descripcion, 2, 60, "La descripción"), Validacion.Estado(s.Estado));
        var abreviatura = Validacion.Opcional(s.Abreviatura, 60, "El campo abreviatura");
        return GuardarAsync("dbo.Usp_TI_Guardar_SubTipo", usuario, p =>
        {
            p.Add("@cTipo", SqlDbType.Char, 3).Value = tipo;
            p.Add("@cSubTipo", SqlDbType.Char, 3).Value = subTipo;
            p.Add("@cCategoria", SqlDbType.VarChar, 20).Value = categoria;
            p.Add("@cDescripcion", SqlDbType.VarChar, 60).Value = descripcion;
            p.Add("@cAbreviatura", SqlDbType.VarChar, 60).Value = Opcional(abreviatura);
            p.Add("@cEstado", SqlDbType.VarChar, 2).Value = estado;
        }, ct);
    }

    public Task GuardarMatrizAsync(string usuario, GuardarMatrizTISolicitud s, CancellationToken ct)
    {
        var (item, categoria, estado) = (Variable(s.Item, 20, "ítem"), Variable(s.Categoria, 20, "categoría"), Validacion.Estado(s.Estado));
        Validacion.Nivel(s.Prioridad, "La prioridad");
        Validacion.Nivel(s.Impacto, "El impacto");
        Validacion.Nivel(s.Complejidad, "La complejidad");
        return GuardarAsync("dbo.Usp_TI_Guardar_MatrizClasificacion", usuario, p =>
        {
            p.Add("@cItem", SqlDbType.VarChar, 20).Value = item;
            p.Add("@cCategoria", SqlDbType.VarChar, 20).Value = categoria;
            p.Add("@nPrioridad", SqlDbType.Int).Value = s.Prioridad;
            p.Add("@nImpacto", SqlDbType.Int).Value = s.Impacto;
            p.Add("@nComplejidad", SqlDbType.Int).Value = s.Complejidad;
            p.Add("@cEstado", SqlDbType.VarChar, 2).Value = estado;
        }, ct);
    }

    public Task GuardarSlaAsync(string usuario, GuardarSlaTISolicitud s, CancellationToken ct)
    {
        Validacion.Nivel(s.Prioridad, "La prioridad");
        if (s.SlaObjetivoMinutos is <= 0 or > 43200) throw new ArgumentException("El SLA debe estar entre 1 minuto y 30 días.");
        var estado = Validacion.Estado(s.Estado);
        return GuardarAsync("dbo.Usp_TI_Guardar_ParametroSLA", usuario, p =>
        {
            p.Add("@nPrioridad", SqlDbType.TinyInt).Value = s.Prioridad;
            p.Add("@nSlaObjetivoMinutos", SqlDbType.Int).Value = s.SlaObjetivoMinutos;
            p.Add("@cEstado", SqlDbType.VarChar, 2).Value = estado;
        }, ct);
    }

    /// <summary>Trae de Spring la identidad del usuario y le asigna el área y el perfil locales.</summary>
    public async Task SincronizarUsuarioCorporativoAsync(string usuarioAdmin, SincronizarUsuarioCorporativoSolicitud s, CancellationToken ct)
    {
        if (!identidadCorporativa.Disponible) throw new InvalidOperationException("La conexión corporativa Spring no está habilitada en este ambiente.");
        var (usuario, area, perfil, estado) = (Variable(s.Usuario, 20, "usuario"), Exacto(s.Area, "área"), Exacto(s.Perfil, "perfil"), Validacion.Estado(s.Estado));
        var correo = Validacion.Opcional(s.Correo, 100, "El campo correo");
        var corporativo = await identidadCorporativa.ObtenerUsuarioAsync(usuario, ct) ?? throw new KeyNotFoundException("El usuario no existe en el origen corporativo Spring.");
        var cargo = corporativo.Cargo.Trim().ToUpperInvariant();
        if (cargo.Length > 3) cargo = string.Empty;
        var estadoCorporativo = string.Join('/', new[] { corporativo.Estado, corporativo.EstadoEmpleado }.Where(x => !string.IsNullOrWhiteSpace(x)));
        await GuardarAsync("dbo.Usp_TI_Registrar_UsuarioCorporativo", usuarioAdmin, p =>
        {
            p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = corporativo.Usuario;
            p.Add("@cNombreCompleto", SqlDbType.VarChar, 255).Value = corporativo.NombreCompleto;
            p.Add("@cCargo", SqlDbType.Char, 3).Value = BaseDatos.Opcional(cargo);
            p.Add("@cCargoDescripcion", SqlDbType.VarChar, 60).Value = BaseDatos.Opcional(cargo);
            p.Add("@cDocumento", SqlDbType.VarChar, 20).Value = Opcional(corporativo.Documento);
            p.Add("@cEstadoCorporativo", SqlDbType.VarChar, 20).Value = BaseDatos.Opcional(estadoCorporativo.Length > 20 ? estadoCorporativo[..20] : estadoCorporativo);
            p.Add("@cArea", SqlDbType.Char, 3).Value = area;
            p.Add("@cPerfil", SqlDbType.Char, 3).Value = perfil;
            p.Add("@cCorreo", SqlDbType.VarChar, 100).Value = Opcional(string.IsNullOrWhiteSpace(correo) ? corporativo.Correo : correo);
            p.Add("@cEstado", SqlDbType.VarChar, 2).Value = estado;
        }, ct, parametroUsuario: "@cUsuarioAdmin");
    }

    public async Task<int> SincronizarCargosCorporativosAsync(string usuarioAdmin, CancellationToken ct)
    {
        if (!identidadCorporativa.Disponible) throw new InvalidOperationException("La conexión corporativa Spring no está habilitada en este ambiente.");
        Validacion.Usuario(usuarioAdmin);
        var procesados = 0;
        foreach (var cargo in await identidadCorporativa.ObtenerCargosAsync(ct))
        {
            var codigo = cargo.Cargo.Trim().ToUpperInvariant();
            if (codigo.Length is < 1 or > 3) continue;
            var descripcion = Validacion.Texto(cargo.Descripcion, 1, 60, "La descripción de cargo");
            await GuardarAsync("dbo.Usp_TI_Sincronizar_CargoCorporativo", usuarioAdmin, p =>
            {
                p.Add("@cCargo", SqlDbType.Char, 3).Value = codigo;
                p.Add("@cDescripcion", SqlDbType.VarChar, 60).Value = descripcion;
            }, ct, parametroUsuario: "@cUsuarioAdmin");
            procesados++;
        }
        return procesados;
    }

    /// <summary>Registra un formato descargable (pendiente: aún sin pantalla). Siempre exige el archivo para no dejar rutas obsoletas.</summary>
    public async Task GuardarFormatoAsync(string usuario, GuardarFormatoSoporteSolicitud s, CancellationToken ct)
    {
        var codigo = Variable(s.FormatoCodigo, 20, "código de formato");
        var titulo = Validacion.Texto(s.Titulo, 3, 120, "El título");
        var descripcion = Validacion.Opcional(s.Descripcion, 500, "El campo descripción");
        var tipoTicket = string.IsNullOrWhiteSpace(s.TipoTicket) ? null : Exacto(s.TipoTicket, "tipo de ticket");
        var estado = Validacion.Estado(s.Estado);
        if (s.Archivo is null)
        {
            var existe = (await ObtenerAsync(usuario, ct)).Formatos.Any(x => x.FormatoCodigo == codigo);
            throw new ArgumentException(existe
                ? "Para modificar un formato existente adjunta nuevamente el archivo. Así se evita conservar rutas ambiguas o archivos obsoletos."
                : "Adjunta el archivo del formato que deseas registrar.");
        }
        var extension = Path.GetExtension(s.Archivo.FileName).ToLowerInvariant();
        if (s.Archivo.Length is <= 0 or > MaximoFormatoBytes) throw new ArgumentException("El formato debe tener contenido y no superar 15 MB.");
        if (!ExtensionesFormato.Contains(extension)) throw new ArgumentException("Solo se permiten formatos PDF, Word o Excel.");

        var guardado = await Archivos.GuardarAsync(s.Archivo, Archivos.Formatos, extension, s.Archivo.ContentType, ct, prefijoNombre: codigo.ToLowerInvariant() + "-");
        try
        {
            await GuardarAsync("dbo.Usp_TI_Guardar_FormatoSoporte", usuario, p =>
            {
                p.Add("@cFormatoCodigo", SqlDbType.VarChar, 20).Value = codigo;
                p.Add("@cTitulo", SqlDbType.NVarChar, 120).Value = titulo;
                p.Add("@cDescripcion", SqlDbType.NVarChar, 500).Value = Opcional(descripcion);
                p.Add("@cNombreOriginal", SqlDbType.NVarChar, 260).Value = guardado.NombreOriginal;
                p.Add("@cRutaArchivo", SqlDbType.NVarChar, 1000).Value = guardado.RutaArchivo;
                p.Add("@cTipoMime", SqlDbType.VarChar, 100).Value = guardado.TipoMime;
                p.Add("@cTipoTicket", SqlDbType.Char, 3).Value = Opcional(tipoTicket);
                p.Add("@cEstado", SqlDbType.VarChar, 2).Value = estado;
            }, ct);
        }
        catch
        {
            Archivos.Eliminar(Archivos.RutaFisica(guardado.RutaArchivo));
            throw;
        }
    }

    /// <summary>Publica o retira un artículo para los colaboradores (pendiente: aún sin pantalla).</summary>
    public Task ActualizarVisibilidadConocimientoAsync(string usuario, string conocimientoCodigo, bool visible, CancellationToken ct)
    {
        var codigo = Variable(conocimientoCodigo, 20, "artículo");
        return GuardarAsync("dbo.Usp_TI_Actualizar_VisibilidadConocimiento", usuario, p =>
        {
            p.Add("@cConocimientoCodigo", SqlDbType.VarChar, 20).Value = codigo;
            p.Add("@lVisibleUsuario", SqlDbType.Bit).Value = visible;
        }, ct);
    }

    private Task GuardarAsync(string procedimiento, string usuario, Action<SqlParameterCollection> parametros, CancellationToken ct, string parametroUsuario = "@cUsuario")
    {
        var usuarioValido = Validacion.Usuario(usuario);
        return baseDatos.EjecutarAsync(procedimiento, p =>
        {
            p.Add(parametroUsuario, SqlDbType.VarChar, 20).Value = usuarioValido;
            parametros(p);
        }, ct);
    }

    // En Configuración los opcionales se guardan recortados.
    private static object Opcional(string? valor) => BaseDatos.Opcional(valor?.Trim());
    private static string Exacto(string valor, string nombre) => Validacion.CodigoExacto(valor, 3, $"El código de {nombre} debe tener 3 caracteres.");
    private static string Variable(string valor, int maximo, string nombre) => Validacion.Codigo(valor, maximo, $"El {nombre} no es válido.");
}
