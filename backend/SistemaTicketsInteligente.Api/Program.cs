/**
 * Archivo: Program.cs
 * Objetivo: Configurar y ejecutar la API ASP.NET Core del Sistema de Tickets Inteligente.
 * Responsabilidad: Registrar dependencias, autenticación, revalidación de la sesión, protección CSRF, autorización, protección de datos,
 *   CORS, rate limiting y manejo general de errores.
 * Dependencias: BaseDatos, BLL, ASP.NET Core, Data Protection, Antiforgery y appsettings.json.
 * Flujo: Inicio de aplicación -> configuración -> middleware -> Controllers -> BLL -> Stored Procedures.
 * Consideraciones: Las claves que protegen la cookie se guardan fuera del repositorio para conservar sesiones válidas entre reinicios; la identidad
 *   corporativa es opcional por ambiente y no se almacenan secretos en este archivo. En el ambiente Empresa las cookies viajan solo por
 *   HTTPS, los hosts permitidos deben estar definidos y el agente usa identidades SQL propias de lectura y de escritura.
 */

using System.Threading.RateLimiting;
using System.IO.Compression;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.ResponseCompression;

var builder = WebApplication.CreateBuilder(args);
var esEmpresa = builder.Environment.IsEnvironment("Empresa");

builder.Services.AddControllers();
builder.Services.AddResponseCompression(opciones =>
{
    opciones.EnableForHttps = true;
    opciones.Providers.Add<BrotliCompressionProvider>();
    opciones.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(opciones => opciones.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(opciones => opciones.Level = CompressionLevel.Fastest);

// Las identidades del agente son obligatorias en Empresa; en desarrollo, sin ellas se usa la conexión de la API.
var conexionesRequeridas = new List<string> { "CnnSistemaTickets", "CnnGestionTi", "CnnSeguridad", "CnnSpring" };
if (esEmpresa) conexionesRequeridas.AddRange(["CnnAgenteLectura", "CnnAgenteEscritura"]);
foreach (var nombreConexion in conexionesRequeridas)
{
    var valor = builder.Configuration.GetConnectionString(nombreConexion);
    if (string.IsNullOrWhiteSpace(valor)) throw new InvalidOperationException($"No se configuró la conexión '{nombreConexion}'.");

    if (esEmpresa &&
        (valor.Contains("SERVIDOR_EMPRESA", StringComparison.OrdinalIgnoreCase) ||
         valor.Contains("USUARIO_EMPRESA", StringComparison.OrdinalIgnoreCase) ||
         valor.Contains("CLAVE_EMPRESA", StringComparison.OrdinalIgnoreCase)))
        throw new InvalidOperationException($"La conexión empresarial '{nombreConexion}' todavía contiene marcadores. Configúrala mediante variables de entorno antes de iniciar.");
}
// Con "*" la API acepta cualquier encabezado Host; en Empresa debe listar los nombres reales del servidor.
var hostsPermitidos = builder.Configuration["AllowedHosts"] ?? string.Empty;
if (esEmpresa && (hostsPermitidos.Trim() is "" or "*" || hostsPermitidos.Contains("HOST_EMPRESA", StringComparison.OrdinalIgnoreCase)))
    throw new InvalidOperationException("Define AllowedHosts con el nombre del servidor de la empresa (variable de entorno AllowedHosts) antes de iniciar.");

var cadenaConexion = builder.Configuration.GetConnectionString("CnnSistemaTickets")!;
var cadenaAgenteLectura = builder.Configuration.GetConnectionString("CnnAgenteLectura");
var cadenaAgenteEscritura = builder.Configuration.GetConnectionString("CnnAgenteEscritura");
if (builder.Environment.IsEnvironment("Diagnostico"))
{
    foreach (var nombre in conexionesRequeridas.Append("CnnAgenteLectura").Append("CnnAgenteEscritura"))
    {
        var valor = builder.Configuration.GetConnectionString(nombre);
        if (!string.IsNullOrWhiteSpace(valor) && !new SqlConnectionStringBuilder(valor).InitialCatalog.EndsWith("_TEST", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El entorno Diagnostico solo admite bases terminadas en _TEST.");
    }
}
var rutaClavesSesion = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SistemaTicketsInteligente", "DataProtectionKeys");
Directory.CreateDirectory(rutaClavesSesion);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(rutaClavesSesion))
    .SetApplicationName("SistemaTicketsInteligente");

// Una sola puerta a SQL Server: cada BLL llama a sus Stored Procedures mediante BaseDatos.
builder.Services.AddSingleton(new BaseDatos(cadenaConexion, cadenaAgenteLectura, cadenaAgenteEscritura));
builder.Services.AddScoped<IdentidadCorporativa>();

// Módulos del colaborador y de TI.
builder.Services.AddScoped<AutenticacionBLL>();
builder.Services.AddScoped<InicioBLL>();
builder.Services.AddScoped<NotificacionesBLL>();
builder.Services.AddScoped<NuevoTicketBLL>();
builder.Services.AddScoped<RecursosSoporteBLL>();
builder.Services.AddScoped<MisTicketsUsuarioBLL>();
builder.Services.AddScoped<AsistenteUsuarioBLL>();
builder.Services.AddScoped<GestionTicketsTIBLL>();
builder.Services.AddScoped<GestionOperativaTIBLL>();
builder.Services.AddScoped<BaseConocimientoTIBLL>();
builder.Services.AddScoped<ReportesTIBLL>();
builder.Services.AddScoped<ConfiguracionTIBLL>();

// Agente de Ingeniería e IA.
builder.Services.AddSingleton<ControlAgenteTI>();
builder.Services.AddScoped<ConocimientoSemanticoBLL>();
builder.Services.AddScoped<AlmacenGrabaciones>();
builder.Services.AddSingleton<ReplicaTecnicaBLL>();
builder.Services.AddSingleton<AgenteCodigoClient>();
builder.Services.AddScoped<InvestigadorAgenteTI>();
builder.Services.AddScoped<AsistenteTIBLL>();
builder.Services.AddScoped<ReproduccionUsuarioBLL>();
builder.Services.AddHostedService<SqlTrazaListener>();
// Investigación automática en segundo plano cuando el usuario termina de mostrar su error.
builder.Services.AddSingleton<ColaAgenteTI>();
builder.Services.AddHostedService<ProcesadorAgenteTI>();
// Retoma investigaciones pendientes tras un reinicio, cierra ejecuciones interrumpidas y aplica el autocierre configurado.
builder.Services.AddHostedService<MantenimientoAgenteTI>();
builder.Services.AddHttpClient<OpenAIAsistenteClient>(cliente => cliente.Timeout = TimeSpan.FromSeconds(90));
builder.Services.AddHttpClient<GeminiLiveClient>(cliente => cliente.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddHttpClient<AnalizadorGrabacionClient>(cliente => cliente.Timeout = TimeSpan.FromSeconds(180));
builder.Services.AddMemoryCache();

// En Empresa la cookie solo viaja por HTTPS aunque un proxy termine el TLS; en desarrollo sigue a la petición.
var politicaCookieSegura = esEmpresa ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opciones =>
    {
        opciones.Cookie.Name = "SistemaTicketsInteligente.Auth";
        opciones.Cookie.HttpOnly = true;
        opciones.Cookie.SameSite = SameSiteMode.Lax;
        opciones.Cookie.SecurePolicy = politicaCookieSegura;
        opciones.ExpireTimeSpan = TimeSpan.FromHours(8);
        opciones.SlidingExpiration = true;
        opciones.Events.OnRedirectToLogin = contexto => { contexto.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        opciones.Events.OnRedirectToAccessDenied = contexto => { contexto.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
        // Un usuario desactivado, o con otro perfil o área, pierde la sesión en minutos aunque su cookie siga vigente.
        opciones.Events.OnValidatePrincipal = async contexto =>
        {
            var autenticacion = contexto.HttpContext.RequestServices.GetRequiredService<AutenticacionBLL>();
            if (contexto.Principal is null || !await autenticacion.SesionVigenteAsync(contexto.Principal, contexto.HttpContext.RequestAborted))
            {
                contexto.RejectPrincipal();
                await contexto.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });

// Token antifalsificación: GET /api/autenticacion/token-csrf lo entrega y el frontend lo envía en el encabezado X-CSRF-TOKEN.
builder.Services.AddAntiforgery(opciones =>
{
    opciones.HeaderName = "X-CSRF-TOKEN";
    opciones.Cookie.Name = "SistemaTicketsInteligente.Csrf";
    opciones.Cookie.HttpOnly = true;
    opciones.Cookie.SameSite = SameSiteMode.Lax;
    opciones.Cookie.SecurePolicy = politicaCookieSegura;
});

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(opciones =>
{
    opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opciones.AddPolicy("Login", contexto => RateLimitPartition.GetFixedWindowLimiter(
        contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocido",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    opciones.AddPolicy("Asistente", contexto => RateLimitPartition.GetTokenBucketLimiter(
        contexto.User.Identity?.Name ?? contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocido",
        _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 12,
            TokensPerPeriod = 12,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    foreach (var (nombre, limite) in new[] { ("Agente", 100), ("AgenteEventos", 300) })
        opciones.AddPolicy(nombre, contexto => RateLimitPartition.GetFixedWindowLimiter(
            contexto.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocido",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = limite, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
});

builder.Services.AddCors(opciones =>
{
    opciones.AddPolicy("Frontend", politica =>
    {
        var origenes = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        politica.WithOrigins(origenes).AllowAnyHeader().AllowAnyMethod().AllowCredentials().WithExposedHeaders("X-Correlation-ID", "X-Csrf-Invalido");
    });
});

var app = builder.Build();
app.UseResponseCompression();

app.UseExceptionHandler(aplicacionError =>
{
    aplicacionError.Run(async contexto =>
    {
        var excepcion = contexto.Features.Get<IExceptionHandlerFeature>()?.Error;
        var errorSqlFuncional = excepcion is SqlException sqlFuncional && BaseDatos.EsErrorDeNegocio(sqlFuncional);
        // Check, clave foránea o clave única: la operación no cumple una regla de integridad; la base sí está disponible.
        var errorIntegridad = excepcion is SqlException { Number: 547 or 2601 or 2627 };
        var baseDatosNoDisponible = excepcion is SqlException && !errorSqlFuncional && !errorIntegridad;
        if (excepcion is not null && !errorSqlFuncional && excepcion is not OperationCanceledException)
            app.Logger.LogError(excepcion, "Error no controlado en {Metodo} {Ruta}. Correlación {Correlacion}", contexto.Request.Method, contexto.Request.Path.Value, contexto.TraceIdentifier);

        contexto.Response.StatusCode = errorSqlFuncional || errorIntegridad ? StatusCodes.Status409Conflict
            : baseDatosNoDisponible ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status500InternalServerError;
        contexto.Response.ContentType = "application/json";
        // El manejador limpia los encabezados de la respuesta fallida: la correlación se vuelve a informar.
        contexto.Response.Headers["X-Correlation-ID"] = contexto.TraceIdentifier;
        await contexto.Response.WriteAsJsonAsync(new
        {
            mensaje = errorSqlFuncional ? excepcion!.Message
                : errorIntegridad ? "La operación no cumple las reglas de integridad del sistema. Revisa los datos e intenta nuevamente."
                : baseDatosNoDisponible ? "La base de datos no se encuentra disponible. Intenta nuevamente en unos momentos."
                : "Se produjo un error al procesar la solicitud.",
            idSeguimiento = contexto.TraceIdentifier
        });
    });
});

app.UseRouting();
app.UseCors("Frontend");
app.UseAuthentication();
// La correlación se abre con el usuario ya identificado y antes de los rechazos (CSRF, límite, autorización): toda respuesta la informa.
app.UseMiddleware<CorrelacionMiddleware>();
// Protección CSRF: toda operación que modifica datos exige el token de la sesión en X-CSRF-TOKEN. El inicio de sesión queda
// exento porque todavía no hay sesión; SameSite=Lax ya impide que otro sitio envíe la cookie en un POST.
app.Use(async (contexto, siguiente) =>
{
    var metodo = contexto.Request.Method;
    var modifica = !(HttpMethods.IsGet(metodo) || HttpMethods.IsHead(metodo) || HttpMethods.IsOptions(metodo) || HttpMethods.IsTrace(metodo));
    if (modifica && contexto.Request.Path.StartsWithSegments("/api") && !contexto.Request.Path.StartsWithSegments("/api/autenticacion/iniciar-sesion")
        && !await contexto.RequestServices.GetRequiredService<IAntiforgery>().IsRequestValidAsync(contexto))
    {
        contexto.Response.StatusCode = StatusCodes.Status400BadRequest;
        contexto.Response.Headers["X-Csrf-Invalido"] = "1";
        await contexto.Response.WriteAsJsonAsync(new { mensaje = "La protección de la sesión venció. Vuelve a intentar la operación." });
        return;
    }
    await siguiente(contexto);
});
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/api/salud", async (BaseDatos baseDatos, CancellationToken cancellationToken) =>
{
    try
    {
        await baseDatos.VerificarAsync(cancellationToken);
        return Results.Ok(new { estado = "ok", baseDatos = "disponible" });
    }
    catch (SqlException)
    {
        return Results.Json(new { estado = "degradado", baseDatos = "no disponible" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.Run();

/// <summary>Punto de entrada; parcial y público para que las pruebas de integración levanten la API en memoria.</summary>
public partial class Program;
