/*
 * Archivo: ConfiguracionTIBLL.cs
 * Objetivo: Aplicar las reglas de mantenimiento del módulo Configuración TI.
 * Responsabilidad: Normalizar catálogos, validar matriz/SLA, sincronizar identidad corporativa y guardar formatos en una ubicación controlada.
 * Dependencias: ConfiguracionTIDAO, IdentidadCorporativaDAO y ConfiguracionTIDTO.
 * Flujo: ConfiguracionTIController -> ConfiguracionTIBLL -> DAO -> SQL Server / Spring.
 * Consideraciones: No implementa IA; no elimina registros históricos y nunca almacena contraseñas corporativas.
 */

using SistemaTicketsInteligente.Api.DAO;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.BLL;

public sealed class ConfiguracionTIBLL
{
    private const long MaximoFormatoBytes = 15 * 1024 * 1024;
    private static readonly HashSet<string> ExtensionesFormato = new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".doc", ".docx", ".xls", ".xlsx" };
    private readonly ConfiguracionTIDAO configuracionTIDAO;
    private readonly IdentidadCorporativaDAO identidadCorporativaDAO;

    public ConfiguracionTIBLL(ConfiguracionTIDAO configuracionTIDAO, IdentidadCorporativaDAO identidadCorporativaDAO)
    {
        this.configuracionTIDAO = configuracionTIDAO;
        this.identidadCorporativaDAO = identidadCorporativaDAO;
    }

    public Task<ConfiguracionTIRespuesta> ObtenerAsync(string usuario, CancellationToken ct = default) => configuracionTIDAO.ObtenerAsync(ValidarUsuario(usuario), ct);

    public Task GuardarAreaAsync(string usuario, GuardarAreaTISolicitud s, CancellationToken ct = default)
    {
        s.Area = Codigo(s.Area, 3, "área"); s.Descripcion = Texto(s.Descripcion, 2, 60, "descripción"); s.Estado = Estado(s.Estado);
        return configuracionTIDAO.GuardarAreaAsync(ValidarUsuario(usuario), s, ct);
    }

    public Task GuardarLineaAsync(string usuario, GuardarLineaTISolicitud s, CancellationToken ct = default)
    {
        s.Linea = Codigo(s.Linea, 3, "línea"); s.Area = Codigo(s.Area, 3, "área"); s.Descripcion = Texto(s.Descripcion, 2, 60, "descripción"); s.Estado = Estado(s.Estado);
        return configuracionTIDAO.GuardarLineaAsync(ValidarUsuario(usuario), s, ct);
    }

    public Task GuardarItemAsync(string usuario, GuardarItemTISolicitud s, CancellationToken ct = default)
    {
        s.Item = CodigoVariable(s.Item, 20, "ítem"); s.Linea = Codigo(s.Linea, 3, "línea"); s.Descripcion = Texto(s.Descripcion, 2, 255, "descripción"); s.Estado = Estado(s.Estado);
        return configuracionTIDAO.GuardarItemAsync(ValidarUsuario(usuario), s, ct);
    }

    public Task GuardarTipoAsync(string usuario, GuardarTipoTISolicitud s, CancellationToken ct = default)
    {
        s.Tipo = Codigo(s.Tipo, 3, "tipo"); s.Descripcion = Texto(s.Descripcion, 2, 60, "descripción"); s.Abreviatura = Opcional(s.Abreviatura, 60, "abreviatura"); s.Estado = Estado(s.Estado);
        return configuracionTIDAO.GuardarTipoAsync(ValidarUsuario(usuario), s, ct);
    }

    public Task GuardarCategoriaAsync(string usuario, GuardarCategoriaTISolicitud s, CancellationToken ct = default)
    {
        s.Categoria = CodigoVariable(s.Categoria, 20, "categoría"); s.Descripcion = Texto(s.Descripcion, 2, 60, "descripción"); s.Abreviatura = Opcional(s.Abreviatura, 3, "abreviatura"); s.Estado = Estado(s.Estado);
        return configuracionTIDAO.GuardarCategoriaAsync(ValidarUsuario(usuario), s, ct);
    }

    public Task GuardarSubTipoAsync(string usuario, GuardarSubTipoTISolicitud s, CancellationToken ct = default)
    {
        s.Tipo = Codigo(s.Tipo, 3, "tipo"); s.SubTipo = Codigo(s.SubTipo, 3, "subtipo"); s.Categoria = CodigoVariable(s.Categoria, 20, "categoría"); s.Descripcion = Texto(s.Descripcion, 2, 60, "descripción"); s.Abreviatura = Opcional(s.Abreviatura, 60, "abreviatura"); s.Estado = Estado(s.Estado);
        return configuracionTIDAO.GuardarSubTipoAsync(ValidarUsuario(usuario), s, ct);
    }

    public Task GuardarMatrizAsync(string usuario, GuardarMatrizTISolicitud s, CancellationToken ct = default)
    {
        s.Item = CodigoVariable(s.Item, 20, "ítem"); s.Categoria = CodigoVariable(s.Categoria, 20, "categoría"); s.Estado = Estado(s.Estado);
        ValidarNivel(s.Prioridad, "prioridad"); ValidarNivel(s.Impacto, "impacto"); ValidarNivel(s.Complejidad, "complejidad");
        return configuracionTIDAO.GuardarMatrizAsync(ValidarUsuario(usuario), s, ct);
    }

    public Task GuardarSlaAsync(string usuario, GuardarSlaTISolicitud s, CancellationToken ct = default)
    {
        ValidarNivel(s.Prioridad, "prioridad");
        if (s.SlaObjetivoMinutos <= 0 || s.SlaObjetivoMinutos > 43200) throw new ArgumentException("El SLA debe estar entre 1 minuto y 30 días.");
        s.Estado = Estado(s.Estado);
        return configuracionTIDAO.GuardarSlaAsync(ValidarUsuario(usuario), s, ct);
    }

    public async Task SincronizarUsuarioCorporativoAsync(string usuarioAdmin, SincronizarUsuarioCorporativoSolicitud s, CancellationToken ct = default)
    {
        if (!identidadCorporativaDAO.Disponible) throw new InvalidOperationException("La conexión corporativa Spring no está habilitada en este ambiente.");

        s.Usuario = CodigoVariable(s.Usuario, 20, "usuario");
        s.Area = Codigo(s.Area, 3, "área");
        s.Perfil = Codigo(s.Perfil, 3, "perfil");
        s.Estado = Estado(s.Estado);
        s.Correo = Opcional(s.Correo, 100, "correo");

        var corporativo = await identidadCorporativaDAO.ObtenerUsuarioAsync(s.Usuario, ct)
            ?? throw new KeyNotFoundException("El usuario no existe en el origen corporativo Spring.");

        await configuracionTIDAO.RegistrarUsuarioCorporativoAsync(ValidarUsuario(usuarioAdmin), corporativo, s, ct);
    }

    public async Task<int> SincronizarCargosCorporativosAsync(string usuarioAdmin, CancellationToken ct = default)
    {
        if (!identidadCorporativaDAO.Disponible) throw new InvalidOperationException("La conexión corporativa Spring no está habilitada en este ambiente.");
        var usuario = ValidarUsuario(usuarioAdmin);
        var cargos = await identidadCorporativaDAO.ObtenerCargosAsync(ct);
        var procesados = 0;

        foreach (var cargo in cargos)
        {
            var codigo = cargo.Cargo.Trim().ToUpperInvariant();
            if (codigo.Length is < 1 or > 3) continue;
            cargo.Cargo = codigo;
            cargo.Descripcion = Texto(cargo.Descripcion, 1, 60, "descripción de cargo");
            await configuracionTIDAO.SincronizarCargoAsync(usuario, cargo, ct);
            procesados++;
        }

        return procesados;
    }

    public async Task GuardarFormatoAsync(string usuario, GuardarFormatoSoporteSolicitud s, CancellationToken ct = default)
    {
        var usuarioNormalizado = ValidarUsuario(usuario);
        s.FormatoCodigo = CodigoVariable(s.FormatoCodigo, 20, "código de formato");
        s.Titulo = Texto(s.Titulo, 3, 120, "título");
        s.Descripcion = Opcional(s.Descripcion, 500, "descripción");
        s.TipoTicket = string.IsNullOrWhiteSpace(s.TipoTicket) ? null : Codigo(s.TipoTicket, 3, "tipo de ticket");
        s.Estado = Estado(s.Estado);

        var existente = (await configuracionTIDAO.ObtenerAsync(usuarioNormalizado, ct)).Formatos.FirstOrDefault(x => x.FormatoCodigo == s.FormatoCodigo);
        if (s.Archivo is null && existente is null) throw new ArgumentException("Adjunta el archivo del formato que deseas registrar.");

        string nombreOriginal;
        string rutaRelativa;
        string tipoMime;

        if (s.Archivo is null)
        {
            nombreOriginal = existente!.NombreOriginal;
            tipoMime = existente.TipoMime;
            throw new ArgumentException("Para modificar un formato existente adjunta nuevamente el archivo. Así se evita conservar rutas ambiguas o archivos obsoletos.");
        }

        ValidarFormato(s.Archivo);
        var extension = Path.GetExtension(s.Archivo.FileName).ToLowerInvariant();
        var nombreFisico = $"{s.FormatoCodigo.ToLowerInvariant()}-{Guid.NewGuid():N}{extension}";
        var carpeta = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "formatos");
        Directory.CreateDirectory(carpeta);
        var rutaFisica = Path.Combine(carpeta, nombreFisico);
        await using (var destino = File.Create(rutaFisica)) await s.Archivo.CopyToAsync(destino, ct);

        nombreOriginal = Path.GetFileName(s.Archivo.FileName);
        tipoMime = s.Archivo.ContentType;
        rutaRelativa = $"uploads/formatos/{nombreFisico}";

        try
        {
            await configuracionTIDAO.GuardarFormatoAsync(usuarioNormalizado, s.FormatoCodigo, s.Titulo, s.Descripcion, nombreOriginal, rutaRelativa, tipoMime, s.TipoTicket, s.Estado, ct);
        }
        catch
        {
            if (File.Exists(rutaFisica)) File.Delete(rutaFisica);
            throw;
        }
    }

    public Task ActualizarVisibilidadConocimientoAsync(string usuario, string conocimientoCodigo, bool visible, CancellationToken ct = default) =>
        configuracionTIDAO.ActualizarVisibilidadConocimientoAsync(ValidarUsuario(usuario), CodigoVariable(conocimientoCodigo, 20, "artículo"), visible, ct);

    private static void ValidarFormato(IFormFile archivo)
    {
        var nombre = Path.GetFileName(archivo.FileName);
        var extension = Path.GetExtension(nombre);
        if (archivo.Length <= 0 || archivo.Length > MaximoFormatoBytes) throw new ArgumentException("El formato debe tener contenido y no superar 15 MB.");
        if (!ExtensionesFormato.Contains(extension)) throw new ArgumentException("Solo se permiten formatos PDF, Word o Excel.");
    }

    private static string ValidarUsuario(string valor) => CodigoVariable(valor, 20, "usuario autenticado");
    private static string Estado(string valor)
    {
        var estado = valor.Trim().ToUpperInvariant();
        if (estado is not ("A" or "I")) throw new ArgumentException("El estado debe ser A o I.");
        return estado;
    }
    private static string Codigo(string valor, int longitud, string nombre)
    {
        var codigo = valor.Trim().ToUpperInvariant();
        if (codigo.Length != longitud) throw new ArgumentException($"El código de {nombre} debe tener {longitud} caracteres.");
        return codigo;
    }
    private static string CodigoVariable(string valor, int maximo, string nombre)
    {
        var codigo = valor.Trim().ToUpperInvariant();
        if (codigo.Length == 0 || codigo.Length > maximo) throw new ArgumentException($"El {nombre} no es válido.");
        return codigo;
    }
    private static string Texto(string valor, int minimo, int maximo, string nombre)
    {
        var texto = valor.Trim();
        if (texto.Length < minimo || texto.Length > maximo) throw new ArgumentException($"La {nombre} debe contener entre {minimo} y {maximo} caracteres.");
        return texto;
    }
    private static string? Opcional(string? valor, int maximo, string nombre)
    {
        var texto = valor?.Trim();
        if (string.IsNullOrEmpty(texto)) return null;
        if (texto.Length > maximo) throw new ArgumentException($"El campo {nombre} no puede superar {maximo} caracteres.");
        return texto;
    }
    private static void ValidarNivel(int valor, string nombre)
    {
        if (valor is < 1 or > 5) throw new ArgumentException($"La {nombre} debe estar entre 1 y 5.");
    }
}
