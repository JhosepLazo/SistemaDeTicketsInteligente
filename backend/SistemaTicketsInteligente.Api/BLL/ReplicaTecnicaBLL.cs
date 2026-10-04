/**
 * Archivo: ReplicaTecnicaBLL.cs
 * Objetivo: Permitir que el agente replique técnicamente lo que el usuario hizo: encontrar en el código fuente y en la base de datos
 *   del sistema dónde se genera el error y comprobar con datos reales la condición que lo produce.
 * Responsabilidad: Resolver los sistemas investigables configurados (código + base de solo lectura) y ofrecer búsquedas y lecturas acotadas.
 * Dependencias: IConfiguration (AgenteTI:Sistemas y ConnectionStrings), IWebHostEnvironment, Microsoft.Data.SqlClient y ValidadorConsultaSoloLectura.
 * Flujo: InvestigadorAgenteTI -> herramienta interna -> ReplicaTecnicaBLL -> archivo de código o SQL de solo lectura -> filas acotadas.
 * Consideraciones: Nunca modifica datos ni código. Toda lectura de base corre en una transacción que se revierte, con tiempo y filas
 *   limitados; los archivos de configuración y secretos se excluyen; las columnas sensibles se rechazan u ocultan.
 */

using System.Collections.Concurrent;
using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.BLL;

/// <summary>Sistema empresarial que el agente puede investigar (sección AgenteTI:Sistemas de la configuración).</summary>
public sealed class SistemaInvestigable
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    /// <summary>Líneas de ticket (TI_Linea) que corresponden a este sistema.</summary>
    public List<string> Lineas { get; set; } = [];
    /// <summary>Nombre de la cadena en ConnectionStrings; debe usar un login de solo lectura en producción.</summary>
    public string ConexionLectura { get; set; } = string.Empty;
    /// <summary>Carpeta del código fuente. "@repositorio" usa el repositorio de este portal.</summary>
    public string RutaCodigo { get; set; } = string.Empty;
    /// <summary>Permite que el agente ejecute SELECT propios (validados) además de leer metadatos.</summary>
    public bool ConsultasLibres { get; set; }
    /// <summary>Fragmentos de nombre de columna protegidos (por ejemplo "Sueldo" protege SueldoActualLocal).</summary>
    public List<string> ColumnasSensibles { get; set; } = [];
    public List<string> ExtensionesCodigo { get; set; } = [];
}

public sealed record SistemaDisponible(string Codigo, string Nombre, bool TieneCodigo, bool TieneBaseDatos, bool PermiteConsultas, IReadOnlyList<string> Lineas);

public sealed class ReplicaTecnicaBLL
{
    private const int MaximoArchivos = 8000;
    private const long MaximoBytesArchivo = 600_000;
    private static readonly string[] CarpetasExcluidas = ["bin", "obj", "node_modules", ".git", ".vs", ".idea", "dist", "build", "packages", "uploads", "legado", "coverage", "wwwroot"];
    private static readonly string[] ExtensionesPorDefecto = [".cs", ".ts", ".tsx", ".js", ".jsx", ".sql", ".vb", ".java", ".py", ".cshtml", ".razor", ".aspx", ".php"];
    // Configuraciones y credenciales nunca se leen, aunque estén dentro del repositorio.
    private static readonly Regex ArchivoProtegido = new(@"(appsettings[^\\/]*\.json|\.config$|\.env|\.pfx$|\.pem$|\.key$|secret|credencial|password|launchsettings)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly string[] SensiblesPorDefecto = ["Clave", "Correo", "Telefono", "Documento", "Anexo"];
    private static readonly string[] FragmentosSensibles = ["password", "contrasena", "contraseña", "passwd", "token", "secret", "apikey", "api_key", "hash"];
    private static readonly Regex NombreObjeto = new(@"^[\w\[\]\.# ]{1,256}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ILogger<ReplicaTecnicaBLL> logger;
    private readonly List<SistemaResuelto> sistemas;

    public ReplicaTecnicaBLL(IConfiguration configuration, IWebHostEnvironment environment, ILogger<ReplicaTecnicaBLL> logger)
    {
        this.logger = logger;
        sistemas = [];
        foreach (var sistema in configuration.GetSection("AgenteTI:Sistemas").Get<List<SistemaInvestigable>>() ?? [])
        {
            if (string.IsNullOrWhiteSpace(sistema.Codigo)) continue;
            string? raiz = null;
            if (string.Equals(sistema.RutaCodigo, "@repositorio", StringComparison.OrdinalIgnoreCase))
            {
                var directorio = new DirectoryInfo(environment.ContentRootPath);
                while (directorio is not null && !File.Exists(Path.Combine(directorio.FullName, "SistemaTicketsInteligente.sln"))) directorio = directorio.Parent;
                raiz = directorio?.FullName;
            }
            else if (!string.IsNullOrWhiteSpace(sistema.RutaCodigo) && Directory.Exists(sistema.RutaCodigo)) raiz = Path.GetFullPath(sistema.RutaCodigo);

            var cadena = string.IsNullOrWhiteSpace(sistema.ConexionLectura) ? null : configuration.GetConnectionString(sistema.ConexionLectura);
            var baseDatos = string.Empty;
            if (!string.IsNullOrWhiteSpace(cadena))
            {
                try { baseDatos = new SqlConnectionStringBuilder(cadena).InitialCatalog; }
                catch (ArgumentException) { cadena = null; }
            }
            sistemas.Add(new SistemaResuelto(sistema, raiz, string.IsNullOrWhiteSpace(cadena) ? null : cadena, baseDatos));
        }
    }

    public IReadOnlyList<SistemaDisponible> Disponibles => sistemas
        .Select(x => new SistemaDisponible(x.Config.Codigo, x.Config.Nombre, x.Raiz is not null, x.Cadena is not null, x.Cadena is not null && x.Config.ConsultasLibres, x.Config.Lineas))
        .ToList();

    public bool HayCodigo => sistemas.Any(x => x.Raiz is not null);
    public bool HayBaseDatos => sistemas.Any(x => x.Cadena is not null);
    public bool HayConsultas => sistemas.Any(x => x.Cadena is not null && x.Config.ConsultasLibres);

    public IReadOnlyList<string> SistemasDeLinea(string? linea) => string.IsNullOrWhiteSpace(linea) ? [] :
        sistemas.Where(x => x.Config.Lineas.Contains(linea.Trim(), StringComparer.OrdinalIgnoreCase)).Select(x => x.Config.Codigo).ToList();

    /// <summary>Códigos de sistema válidos para una herramienta (para el enum del esquema que recibe el modelo).</summary>
    public IReadOnlyList<string> CodigosPara(string herramienta) => herramienta switch
    {
        "DIAG_CODIGO_BUSCAR" or "DIAG_CODIGO_LEER" => sistemas.Where(x => x.Raiz is not null).Select(x => x.Config.Codigo).ToList(),
        "DIAG_BD_CONSULTAR" => sistemas.Where(x => x.Cadena is not null && x.Config.ConsultasLibres).Select(x => x.Config.Codigo).ToList(),
        _ => sistemas.Where(x => x.Cadena is not null).Select(x => x.Config.Codigo).ToList()
    };

    // ------------------------------------------------------------------ Código fuente

    public AgenteTIHerramientaResultado BuscarCodigo(string codigoSistema, string texto, int maximo)
    {
        var sistema = Resolver(codigoSistema);
        if (sistema.Raiz is null) throw new InvalidOperationException($"El sistema {sistema.Config.Codigo} no tiene código fuente configurado.");
        var extensiones = sistema.Config.ExtensionesCodigo.Count > 0 ? sistema.Config.ExtensionesCodigo.ToArray() : ExtensionesPorDefecto;
        var resultado = new AgenteTIHerramientaResultado();

        // Del texto completo a fragmentos sin datos variables: el mensaje en pantalla suele incluir números o documentos concretos.
        foreach (var fragmento in Fragmentos(texto))
        {
            var buscado = Normalizar(fragmento);
            foreach (var archivo in Archivos(sistema.Raiz, extensiones))
            {
                var numero = 0;
                try
                {
                    foreach (var linea in File.ReadLines(archivo))
                    {
                        numero++;
                        if (!Normalizar(linea).Contains(buscado, StringComparison.Ordinal)) continue;
                        resultado.Filas.Add(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["Archivo"] = Path.GetRelativePath(sistema.Raiz, archivo).Replace('\\', '/'), ["Linea"] = numero,
                            ["Codigo"] = Limitar(RedactorDatosSensibles.RedactarSecretos(linea.Trim()), 240), ["Coincidencia"] = fragmento
                        });
                        if (resultado.Filas.Count >= maximo) { resultado.Truncado = true; return resultado; }
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* archivo bloqueado: se omite */ }
            }
            if (resultado.Filas.Count > 0) break;
        }
        return resultado;
    }

    public AgenteTIHerramientaResultado LeerCodigo(string codigoSistema, string archivo, int desde)
    {
        var sistema = Resolver(codigoSistema);
        if (sistema.Raiz is null) throw new InvalidOperationException($"El sistema {sistema.Config.Codigo} no tiene código fuente configurado.");
        var raiz = sistema.Raiz.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var ruta = Path.GetFullPath(Path.Combine(raiz, archivo.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar)));
        if (!ruta.StartsWith(raiz, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("El archivo está fuera del código fuente del sistema.");
        if (ArchivoProtegido.IsMatch(ruta) || ruta.Split(Path.DirectorySeparatorChar).Any(x => CarpetasExcluidas.Contains(x, StringComparer.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Ese archivo está protegido (configuración, credenciales o compilados) y no puede leerse.");
        if (!File.Exists(ruta)) throw new InvalidOperationException("El archivo no existe en el código fuente del sistema.");

        var resultado = new AgenteTIHerramientaResultado();
        var numero = 0;
        foreach (var linea in File.ReadLines(ruta))
        {
            numero++;
            if (numero < desde) continue;
            if (numero >= desde + 80) { resultado.Truncado = true; break; }
            resultado.Filas.Add(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Linea"] = numero, ["Codigo"] = Limitar(RedactorDatosSensibles.RedactarSecretos(linea.TrimEnd()), 300)
            });
        }
        if (resultado.Filas.Count == 0) throw new InvalidOperationException($"El archivo tiene {numero} líneas; la línea {desde} no existe.");
        return resultado;
    }

    // ------------------------------------------------------------------ Base de datos (solo lectura)

    public async Task<AgenteTIHerramientaResultado> BuscarEnBaseDatosAsync(string codigoSistema, string texto, int maximo, CancellationToken ct)
    {
        var sistema = ResolverBase(codigoSistema);
        var fragmentos = Fragmentos(texto).Take(4).ToList();
        if (fragmentos.Count == 0) throw new InvalidOperationException("Indica al menos 3 caracteres para buscar en la base.");

        // sys.messages ya no se recorre: son ~345 mil mensajes en todos los idiomas y tardaba segundos; los sistemas investigables
        // lanzan sus errores con THROW/RAISERROR dentro de los procedimientos, que sí se buscan.
        var catalogo = await CatalogoAsync(sistema, ct);
        var resultado = catalogo.Modulos is not null
            ? BuscarEnCatalogo(catalogo.Modulos, fragmentos, maximo)
            : await BuscarModulosEnServidorAsync(sistema, fragmentos, maximo, ct);

        // Columnas y tablas solo cuando lo buscado parece un nombre (Usp_..., Pedido.Serie), no una frase de error.
        if (fragmentos.Any(EsNombre) && resultado.Filas.Count < maximo)
        {
            var columnas = await BuscarColumnasAsync(sistema, fragmentos, maximo - resultado.Filas.Count, ct);
            resultado.Filas.AddRange(columnas.Filas);
            resultado.Truncado |= columnas.Truncado;
        }
        return resultado;
    }

    // ---- Catálogo de definiciones en memoria
    // Buscar con CharIndex sobre sys.sql_modules relee del disco todas las definiciones en cada llamada; con el servidor cargado eso
    // superaba el tiempo máximo y además pesa sobre la base de producción. Se leen una vez por sistema y se buscan aquí; el catálogo
    // se recarga solo si cambió algún objeto (última fecha de modificación o cantidad de objetos con definición).
    private const int MaximoCaracteresCatalogo = 12_000_000;
    private const int SegundosCatalogo = 60;
    private readonly ConcurrentDictionary<string, CatalogoModulos> catalogos = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> cargasCatalogo = new(StringComparer.OrdinalIgnoreCase);

    private sealed record ModuloBase(string Nombre, string Tipo, string Definicion, string Plegada);
    /// <summary>Modulos es null cuando las definiciones no caben en memoria; entonces se busca en el servidor.</summary>
    private sealed record CatalogoModulos(string Huella, IReadOnlyList<ModuloBase>? Modulos);

    private async Task<CatalogoModulos> CatalogoAsync(SistemaResuelto sistema, CancellationToken ct)
    {
        const string sqlHuella = """
            Select Huella = Convert(nvarchar(30), Max(modify_date), 126) + N'|' + Convert(nvarchar(12), Count(*))
            From sys.objects Where type In ('P', 'RF', 'V', 'TR', 'FN', 'IF', 'TF', 'R')
            """;
        var huella = (await LeerAsync(sistema, sqlHuella, null, 1, ct)).Filas.FirstOrDefault()?["Huella"] as string ?? string.Empty;
        if (catalogos.TryGetValue(sistema.Config.Codigo, out var actual) && actual.Huella == huella) return actual;

        var carga = cargasCatalogo.GetOrAdd(sistema.Config.Codigo, _ => new SemaphoreSlim(1, 1));
        await carga.WaitAsync(ct);
        try
        {
            // Otra investigación pudo cargarlo mientras se esperaba.
            if (catalogos.TryGetValue(sistema.Config.Codigo, out actual) && actual.Huella == huella) return actual;
            const string sqlModulos = """
                Select Nombre = Object_Schema_Name(object_id) + N'.' + Object_Name(object_id),
                    Tipo = Convert(nvarchar(10), ObjectPropertyEx(object_id, 'BaseType')), Definicion = definition
                From sys.sql_modules Where definition Is Not Null
                """;
            var modulos = await EnLecturaAsync(sistema, sqlModulos, null, SegundosCatalogo, async lector =>
            {
                var lista = new List<ModuloBase>();
                long caracteres = 0;
                while (await lector.ReadAsync(ct))
                {
                    var definicion = lector.GetString(2);
                    caracteres += definicion.Length;
                    if (caracteres > MaximoCaracteresCatalogo) return null;
                    lista.Add(new ModuloBase(lector.IsDBNull(0) ? "(sin nombre)" : lector.GetString(0), TipoObjeto(lector.IsDBNull(1) ? null : lector.GetString(1)),
                        definicion, Plegar(definicion)));
                }
                return lista.OrderBy(x => x.Nombre, StringComparer.OrdinalIgnoreCase).ToList();
            }, ct);
            logger.LogInformation("Catálogo de definiciones de {Sistema}: {Detalle}.", sistema.Config.Codigo,
                modulos is null ? "supera el máximo para memoria, se buscará en el servidor" : $"{modulos.Count} objeto(s) en memoria");
            var nuevo = new CatalogoModulos(huella, modulos);
            catalogos[sistema.Config.Codigo] = nuevo;
            return nuevo;
        }
        finally
        {
            carga.Release();
        }
    }

    /// <summary>Como antes en SQL: se devuelve el texto más completo que tuvo coincidencias (el mensaje entero antes que un tramo).</summary>
    private static AgenteTIHerramientaResultado BuscarEnCatalogo(IReadOnlyList<ModuloBase> modulos, IReadOnlyList<string> fragmentos, int maximo)
    {
        var resultado = new AgenteTIHerramientaResultado();
        var limite = Math.Min(maximo, 20);
        foreach (var fragmento in fragmentos)
        {
            var buscado = Plegar(fragmento);
            foreach (var modulo in modulos)
            {
                var posicion = modulo.Plegada.IndexOf(buscado, StringComparison.Ordinal);
                if (posicion < 0) continue;
                if (resultado.Filas.Count >= limite) { resultado.Truncado = true; break; }
                var inicio = Math.Max(0, posicion - 120);
                resultado.Filas.Add(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Origen"] = "OBJETO", ["Nombre"] = modulo.Nombre, ["Tipo"] = modulo.Tipo,
                    ["Linea"] = modulo.Definicion.AsSpan(0, posicion).Count('\n') + 1,
                    ["Fragmento"] = Limitar(RedactorDatosSensibles.RedactarSecretos(modulo.Definicion.Substring(inicio, Math.Min(320, modulo.Definicion.Length - inicio)).Trim()), 300),
                    ["Coincidencia"] = fragmento
                });
            }
            if (resultado.Filas.Count > 0) break;
        }
        return resultado;
    }

    // Respaldo para catálogos que no caben en memoria: una sola pasada con todos los fragmentos y sin joins ni Order By,
    // para que la consulta no pida reserva de memoria (RESOURCE_SEMAPHORE) en servidores con poca RAM.
    private static async Task<AgenteTIHerramientaResultado> BuscarModulosEnServidorAsync(SistemaResuelto sistema, IReadOnlyList<string> fragmentos, int maximo, CancellationToken ct)
    {
        const string sql = """
            Select Top (200) Origen = 'OBJETO', Nombre = Object_Schema_Name(m.object_id) + N'.' + Object_Name(m.object_id),
                Tipo = Convert(nvarchar(10), ObjectPropertyEx(m.object_id, 'BaseType')), Orden = x.Orden,
                Linea = Len(Left(m.definition, p.Posicion)) - Len(Replace(Left(m.definition, p.Posicion), Char(10), N'')) + 1,
                Fragmento = Substring(m.definition, Case When p.Posicion > 120 Then p.Posicion - 120 Else 1 End, 320)
            From sys.sql_modules m
            Cross Apply (Select Orden = Min(f.Orden) From (Values (1, @t1), (2, @t2), (3, @t3), (4, @t4)) f (Orden, Texto)
                Where f.Texto Is Not Null and CharIndex(f.Texto, m.definition) > 0) x
            Cross Apply (Select Posicion = CharIndex(Choose(x.Orden, @t1, @t2, @t3, @t4), m.definition)) p
            Where x.Orden Is Not Null
            """;
        var filas = await LeerAsync(sistema, sql, c => ParametrosFragmentos(c, fragmentos), 400, ct, segundos: SegundosBusqueda);
        return MejorCoincidencia(filas, fragmentos, Math.Min(maximo, 20), tipoObjeto: true);
    }

    private static async Task<AgenteTIHerramientaResultado> BuscarColumnasAsync(SistemaResuelto sistema, IReadOnlyList<string> fragmentos, int maximo, CancellationToken ct)
    {
        const string sql = """
            Select Top (200) Origen = 'COLUMNA', Nombre = Object_Schema_Name(c.object_id) + N'.' + Object_Name(c.object_id) + N'.' + c.name,
                Tipo = Type_Name(c.user_type_id), Orden = x.Orden
            From sys.columns c
            Cross Apply (Select Orden = Min(f.Orden) From (Values (1, @t1, @n1), (2, @t2, @n2), (3, @t3, @n3), (4, @t4, @n4)) f (Orden, Texto, EsNombre)
                Where f.EsNombre = 1 and (CharIndex(f.Texto, c.name) > 0 or CharIndex(f.Texto, Object_Name(c.object_id)) > 0)) x
            Where x.Orden Is Not Null and (ObjectProperty(c.object_id, 'IsUserTable') = 1 or ObjectProperty(c.object_id, 'IsView') = 1)
            """;
        var filas = await LeerAsync(sistema, sql, c => ParametrosFragmentos(c, fragmentos), 400, ct, segundos: SegundosBusqueda);
        return MejorCoincidencia(filas, fragmentos, Math.Min(maximo, 15), tipoObjeto: false);
    }

    private static void ParametrosFragmentos(SqlCommand comando, IReadOnlyList<string> fragmentos)
    {
        for (var i = 0; i < 4; i++)
        {
            comando.Parameters.Add($"@t{i + 1}", SqlDbType.NVarChar, 400).Value = i < fragmentos.Count ? fragmentos[i] : DBNull.Value;
            comando.Parameters.Add($"@n{i + 1}", SqlDbType.Bit).Value = i < fragmentos.Count && EsNombre(fragmentos[i]);
        }
    }

    // Solo las filas del fragmento más completo con coincidencias, ordenadas por nombre (el orden se aplica aquí y no en SQL).
    private static AgenteTIHerramientaResultado MejorCoincidencia(AgenteTIHerramientaResultado encontrados, IReadOnlyList<string> fragmentos, int limite, bool tipoObjeto)
    {
        var resultado = new AgenteTIHerramientaResultado { Truncado = encontrados.Truncado };
        if (encontrados.Filas.Count == 0 || limite <= 0) return resultado;
        static int Orden(Dictionary<string, object?> fila) => Convert.ToInt32(fila["Orden"], CultureInfo.InvariantCulture);
        var mejor = encontrados.Filas.Min(Orden);
        var filas = encontrados.Filas.Where(x => Orden(x) == mejor).OrderBy(x => x["Nombre"] as string, StringComparer.OrdinalIgnoreCase).ToList();
        if (filas.Count > limite) resultado.Truncado = true;
        foreach (var fila in filas.Take(limite))
        {
            fila.Remove("Orden");
            fila["Coincidencia"] = fragmentos[mejor - 1];
            if (tipoObjeto) fila["Tipo"] = TipoObjeto(fila["Tipo"] as string);
            resultado.Filas.Add(fila);
        }
        return resultado;
    }

    private static bool EsNombre(string texto) => Regex.IsMatch(texto, @"^[\w\.]+$");

    /// <summary>Minúsculas y sin tildes, carácter por carácter: las posiciones coinciden con el texto original.</summary>
    private static string Plegar(string texto) => string.Create(texto.Length, texto, static (destino, origen) =>
    {
        for (var i = 0; i < origen.Length; i++) destino[i] = Plegar(origen[i]);
    });

    private static char Plegar(char c) => char.ToLowerInvariant(c) switch
    {
        'á' or 'à' or 'ä' or 'â' => 'a',
        'é' or 'è' or 'ë' or 'ê' => 'e',
        'í' or 'ì' or 'ï' or 'î' => 'i',
        'ó' or 'ò' or 'ö' or 'ô' => 'o',
        'ú' or 'ù' or 'ü' or 'û' => 'u',
        'ñ' => 'n',
        var otro => otro
    };

    private static string TipoObjeto(string? tipo) => tipo?.Trim() switch
    {
        "P" or "PC" => "PROCEDIMIENTO",
        "V" => "VISTA",
        "FN" or "IF" or "TF" or "FS" or "FT" => "FUNCION",
        "TR" or "TA" => "TRIGGER",
        var otro => otro ?? string.Empty
    };

    public async Task<AgenteTIHerramientaResultado> DefinicionAsync(string codigoSistema, string objeto, int desde, CancellationToken ct)
    {
        var sistema = ResolverBase(codigoSistema);
        if (!NombreObjeto.IsMatch(objeto)) throw new InvalidOperationException("El nombre del objeto no es válido.");
        var datos = await LeerAsync(sistema, "Select Definicion = Object_Definition(Object_Id(@objeto)), Tipo = (Select type_desc From sys.objects Where object_id = Object_Id(@objeto))",
            c => c.Parameters.Add("@objeto", SqlDbType.NVarChar, 256).Value = objeto, 1, ct, sinRecortar: true);
        var definicion = datos.Filas.FirstOrDefault()?["Definicion"] as string;
        if (string.IsNullOrEmpty(definicion)) throw new InvalidOperationException($"El objeto {objeto} no existe, no tiene definición visible o está cifrado.");

        var resultado = new AgenteTIHerramientaResultado();
        var lineas = definicion.Replace("\r\n", "\n").Split('\n');
        if (desde > lineas.Length) throw new InvalidOperationException($"{objeto} tiene {lineas.Length} líneas; la línea {desde} no existe.");
        for (var i = desde - 1; i < lineas.Length && i < desde - 1 + 120; i++)
            resultado.Filas.Add(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Linea"] = i + 1, ["Codigo"] = Limitar(RedactorDatosSensibles.RedactarSecretos(lineas[i].TrimEnd()), 300)
            });
        resultado.Truncado = desde - 1 + 120 < lineas.Length;
        return resultado;
    }

    public Task<AgenteTIHerramientaResultado> EstructuraAsync(string codigoSistema, string tabla, CancellationToken ct)
    {
        var sistema = ResolverBase(codigoSistema);
        if (!NombreObjeto.IsMatch(tabla)) throw new InvalidOperationException("El nombre de la tabla no es válido.");
        const string sql = """
            Declare @id int = Object_Id(@tabla);
            If @id Is Null Throw 51001, 'La tabla o vista indicada no existe.', 1;
            Select Orden = c.column_id, Columna = Convert(nvarchar(260), c.name) Collate Database_Default, Tipo = Convert(nvarchar(130), Type_Name(c.user_type_id)) Collate Database_Default,
                Largo = Case When Type_Name(c.user_type_id) In ('nvarchar','nchar') and c.max_length > 0 Then c.max_length / 2 Else c.max_length End,
                Nulo = c.is_nullable,
                EsClave = Case When Exists (Select 1 From sys.index_columns ic Join sys.indexes i On i.object_id = ic.object_id and i.index_id = ic.index_id
                    Where i.is_primary_key = 1 and ic.object_id = @id and ic.column_id = c.column_id) Then 1 Else 0 End,
                Referencia = Convert(nvarchar(800), (Select Top (1) Object_Schema_Name(f.referenced_object_id) + '.' + Object_Name(f.referenced_object_id) + '.' + Col_Name(f.referenced_object_id, f.referenced_column_id)
                    From sys.foreign_key_columns f Where f.parent_object_id = @id and f.parent_column_id = c.column_id)) Collate Database_Default
            From sys.columns c Where c.object_id = @id
            Union All
            Select 9999, Convert(nvarchar(260), '(filas aproximadas)') Collate Database_Default, Convert(nvarchar(130), Sum(p.rows)) Collate Database_Default, Null, Null, Null, Null
            From sys.partitions p Where p.object_id = @id and p.index_id In (0, 1)
            Order By 1;
            """;
        return LeerAsync(sistema, sql, c => c.Parameters.Add("@tabla", SqlDbType.NVarChar, 256).Value = tabla, 400, ct);
    }

    public async Task<AgenteTIHerramientaResultado> ConsultarAsync(string codigoSistema, string sql, int maximo, CancellationToken ct)
    {
        var sistema = ResolverBase(codigoSistema);
        if (!sistema.Config.ConsultasLibres) throw new InvalidOperationException($"El sistema {sistema.Config.Codigo} no permite consultas del agente; usa la estructura y los procedimientos.");
        var validacion = ValidadorConsultaSoloLectura.Validar(sql, columna => EsSensible(sistema, columna), sistema.BaseDatos);
        if (!validacion.Valida) throw new InvalidOperationException($"Consulta rechazada: {validacion.Motivo}");
        logger.LogInformation("El agente ejecuta una consulta de solo lectura en {Sistema}.", sistema.Config.Codigo);
        return await LeerAsync(sistema, sql, null, Math.Clamp(maximo, 1, 50), ct, ocultar: columna => EsSensible(sistema, columna));
    }

    private const int SegundosLectura = 30;
    // Recorrer las definiciones de todos los objetos puede tardar más que una lectura puntual si el servidor está cargado.
    private const int SegundosBusqueda = 45;

    private static Task<AgenteTIHerramientaResultado> LeerAsync(SistemaResuelto sistema, string sql, Action<SqlCommand>? parametros, int maximo, CancellationToken ct,
        Func<string, bool>? ocultar = null, bool sinRecortar = false, int segundos = SegundosLectura) =>
        EnLecturaAsync(sistema, sql, parametros, segundos, async lector =>
        {
            var resultado = new AgenteTIHerramientaResultado();
            do
            {
                while (await lector.ReadAsync(ct))
                {
                    if (resultado.Filas.Count >= maximo) { resultado.Truncado = true; break; }
                    var fila = new Dictionary<string, object?>(lector.FieldCount, StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < lector.FieldCount; i++)
                    {
                        var nombre = string.IsNullOrWhiteSpace(lector.GetName(i)) ? $"Columna{i + 1}" : lector.GetName(i);
                        fila[nombre] = ocultar is not null && ocultar(nombre) ? "[OCULTO]" : Valor(lector.GetValue(i), sinRecortar);
                    }
                    resultado.Filas.Add(fila);
                }
            } while (!resultado.Truncado && await lector.NextResultAsync(ct));
            return resultado;
        }, ct);

    // Toda lectura de base: transacción que siempre se revierte, espera de bloqueos acotada y tiempo máximo por consulta.
    private static async Task<T> EnLecturaAsync<T>(SistemaResuelto sistema, string sql, Action<SqlCommand>? parametros, int segundos,
        Func<SqlDataReader, Task<T>> leer, CancellationToken ct)
    {
        try
        {
            await using var conexion = new SqlConnection(sistema.Cadena);
            await conexion.OpenAsync(ct);
            await using (var preparar = new SqlCommand("Set Lock_Timeout 3000;", conexion)) await preparar.ExecuteNonQueryAsync(ct);
            await using var transaccion = (SqlTransaction)await conexion.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            try
            {
                await using var comando = new SqlCommand(sql, conexion, transaccion) { CommandTimeout = segundos };
                parametros?.Invoke(comando);
                await using var lector = await comando.ExecuteReaderAsync(ct);
                var resultado = await leer(lector);
                // Si se dejó de leer antes del final (límite de filas o catálogo demasiado grande), se cancela en vez de drenar el resto.
                comando.Cancel();
                return resultado;
            }
            finally
            {
                try { await transaccion.RollbackAsync(CancellationToken.None); } catch (Exception) { /* la conexión pudo cerrarse */ }
            }
        }
        // Tiempo agotado o servidor sin memoria (701, 802, 8645, 8651): reintentar con otra variante no ayuda y consume la investigación.
        catch (SqlException ex) when (ex.Number is -2 or 701 or 802 or 8645 or 8651)
        {
            throw new TimeoutException(ex.Number == -2
                ? $"La base de {sistema.Config.Codigo} no respondió en {segundos} s; el servidor está lento u ocupado."
                : $"El servidor de base de datos de {sistema.Config.Codigo} no tiene memoria disponible en este momento (error {ex.Number}).", ex);
        }
        catch (SqlException ex)
        {
            // El mensaje de SQL (columna inválida, objeto inexistente) le permite al agente corregir su consulta.
            throw new InvalidOperationException($"SQL Server rechazó la lectura: {ex.Message}", ex);
        }
    }

    private static object? Valor(object valor, bool sinRecortar) => valor switch
    {
        DBNull => null,
        DateTime fecha => fecha.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        DateTimeOffset fecha => fecha.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
        Guid guid => guid.ToString(),
        byte[] => "[binario omitido]",
        string texto => sinRecortar ? texto : Limitar(RedactorDatosSensibles.RedactarSecretos(texto.Trim()), 300),
        _ => valor
    };

    private SistemaResuelto Resolver(string codigo) =>
        sistemas.FirstOrDefault(x => string.Equals(x.Config.Codigo, codigo?.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"El sistema {codigo} no está configurado como investigable.");

    private SistemaResuelto ResolverBase(string codigo)
    {
        var sistema = Resolver(codigo);
        return sistema.Cadena is null ? throw new InvalidOperationException($"El sistema {sistema.Config.Codigo} no tiene base de datos de lectura configurada.") : sistema;
    }

    // Por defecto se protegen nombres exactos (Clave, Correo...) y fragmentos de credenciales; lo configurado protege toda columna que lo contenga.
    private static bool EsSensible(SistemaResuelto sistema, string columna) =>
        SensiblesPorDefecto.Contains(columna, StringComparer.OrdinalIgnoreCase)
        || FragmentosSensibles.Concat(sistema.Config.ColumnasSensibles).Any(x => !string.IsNullOrWhiteSpace(x) && columna.Contains(x, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> Archivos(string raiz, IReadOnlyCollection<string> extensiones)
    {
        var pendientes = new Stack<string>();
        pendientes.Push(raiz);
        var cantidad = 0;
        while (pendientes.Count > 0)
        {
            var carpeta = pendientes.Pop();
            IEnumerable<string> archivos, subcarpetas;
            try
            {
                archivos = Directory.EnumerateFiles(carpeta).OrderBy(x => x, StringComparer.Ordinal).ToList();
                subcarpetas = Directory.EnumerateDirectories(carpeta).OrderByDescending(x => x, StringComparer.Ordinal).ToList();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }

            foreach (var archivo in archivos)
            {
                if (!extensiones.Contains(Path.GetExtension(archivo), StringComparer.OrdinalIgnoreCase) || ArchivoProtegido.IsMatch(Path.GetFileName(archivo))) continue;
                FileInfo info;
                try { info = new FileInfo(archivo); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
                if (info.Length > MaximoBytesArchivo) continue;
                if (++cantidad > MaximoArchivos) yield break;
                yield return archivo;
            }
            foreach (var subcarpeta in subcarpetas)
                if (!CarpetasExcluidas.Contains(Path.GetFileName(subcarpeta), StringComparer.OrdinalIgnoreCase)) pendientes.Push(subcarpeta);
        }
    }

    /// <summary>
    /// El texto completo y, si no aparece, sus tramos más largos sin las palabras que llevan números o comillas (los datos variables
    /// del mensaje, como "T003" o "0000173534"): así "…punto de emisión T003." también encuentra "…punto de emisión " + @punto.
    /// </summary>
    public static IReadOnlyList<string> Fragmentos(string texto)
    {
        var limpio = Regex.Replace(texto ?? string.Empty, @"\s+", " ").Trim();
        if (limpio.Length > 200) limpio = limpio[..200];
        var fragmentos = new List<string>();
        if (limpio.Length >= 3) fragmentos.Add(limpio);
        fragmentos.AddRange(Regex.Split(limpio, @"\S*[\d""'«»“”]\S*|[:;,()\[\]{}<>|]")
            .Select(x => x.Trim(' ', '.', '-'))
            .Where(x => x.Length >= 12 && !string.Equals(x, limpio, StringComparison.Ordinal))
            .OrderByDescending(x => x.Length)
            .Take(3));
        return fragmentos.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string Normalizar(string texto)
    {
        var descompuesto = texto.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString();
    }

    private static string Limitar(string texto, int maximo) => texto.Length <= maximo ? texto : texto[..maximo];

    private sealed record SistemaResuelto(SistemaInvestigable Config, string? Raiz, string? Cadena, string BaseDatos);
}
