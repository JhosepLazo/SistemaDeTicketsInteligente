/**
 * Archivo: Program.cs
 * Objetivo: Configurar y ejecutar la API ASP.NET Core del Sistema de Tickets Inteligente.
 * Responsabilidad: Registrar dependencias, autenticación, autorización, protección de datos, CORS, rate limiting y manejo general de errores.
 * Dependencias: BLL, DAO, ASP.NET Core, Data Protection y appsettings.json.
 * Flujo: Inicio de aplicación -> configuración -> middleware -> Controllers.
 * Consideraciones: Las claves que protegen la cookie se guardan fuera del repositorio para conservar sesiones válidas entre reinicios; la identidad corporativa es opcional por ambiente y no se almacenan secretos en este archivo.
 */

using System.Threading.RateLimiting;
using System.IO.Compression;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Data.SqlClient;
using SistemaTicketsInteligente.Api.BLL;
using SistemaTicketsInteligente.Api.DAO;
using SistemaTicketsInteligente.Api.Observabilidad;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddResponseCompression(opciones =>
{
    opciones.EnableForHttps = true;
    opciones.Providers.Add<BrotliCompressionProvider>();
    opciones.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(opciones => opciones.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(opciones => opciones.Level = CompressionLevel.Fastest);

var conexionesRequeridas = new[] { "CnnSistemaTickets", "CnnGestionTi", "CnnSeguridad", "CnnSpring" };
foreach (var nombreConexion in conexionesRequeridas)
{
    var valor = builder.Configuration.GetConnectionString(nombreConexion);
    if (string.IsNullOrWhiteSpace(valor)) throw new InvalidOperationException($"No se configuró la conexión '{nombreConexion}'.");

    if (builder.Environment.IsEnvironment("Empresa") &&
        (valor.Contains("SERVIDOR_EMPRESA", StringComparison.OrdinalIgnoreCase) ||
         valor.Contains("USUARIO_EMPRESA", StringComparison.OrdinalIgnoreCase) ||
         valor.Contains("CLAVE_EMPRESA", StringComparison.OrdinalIgnoreCase)))
        throw new InvalidOperationException($"La conexión empresarial '{nombreConexion}' todavía contiene marcadores. Configúrala mediante variables de entorno antes de iniciar.");
}

var cadenaConexion = builder.Configuration.GetConnectionString("CnnSistemaTickets")!;
if (builder.Environment.IsEnvironment("Diagnostico"))
{
    foreach (var nombre in conexionesRequeridas)
        if (!new SqlConnectionStringBuilder(builder.Configuration.GetConnectionString(nombre)).InitialCatalog.EndsWith("_TEST", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El entorno Diagnostico solo admite bases terminadas en _TEST.");
}
var rutaClavesSesion = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SistemaTicketsInteligente", "DataProtectionKeys");
Directory.CreateDirectory(rutaClavesSesion);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(rutaClavesSesion))
    .SetApplicationName("SistemaTicketsInteligente");

builder.Services.AddSingleton(new ConexionSqlServer(cadenaConexion));
builder.Services.AddScoped<IdentidadCorporativaDAO>();
builder.Services.AddScoped<AutenticacionDAO>();
builder.Services.AddScoped<AutenticacionBLL>();
builder.Services.AddScoped<InicioUsuarioDAO>();
builder.Services.AddScoped<InicioUsuarioBLL>();
builder.Services.AddScoped<InicioTIDAO>();
builder.Services.AddScoped<InicioTIBLL>();
builder.Services.AddScoped<NuevoTicketDAO>();
builder.Services.AddScoped<NuevoTicketBLL>();
builder.Services.AddScoped<MisTicketsUsuarioDAO>();
builder.Services.AddScoped<MisTicketsUsuarioBLL>();
builder.Services.AddScoped<EdicionTicketUsuarioDAO>();
builder.Services.AddScoped<EdicionTicketUsuarioBLL>();
builder.Services.AddScoped<GestionTicketsTIDAO>();
builder.Services.AddScoped<GestionTicketsTIBLL>();
builder.Services.AddScoped<GestionOperativaTIDAO>();
builder.Services.AddScoped<GestionOperativaTIBLL>();
builder.Services.AddScoped<BaseConocimientoTIDAO>();
builder.Services.AddScoped<BaseConocimientoTIBLL>();
builder.Services.AddScoped<ReportesTIDAO>();
builder.Services.AddScoped<ReportesTIBLL>();
builder.Services.AddScoped<ConfiguracionTIDAO>();
builder.Services.AddScoped<ConfiguracionTIBLL>();
builder.Services.AddScoped<NotificacionesDAO>();
builder.Services.AddScoped<NotificacionesBLL>();
builder.Services.AddScoped<RecursosSoporteDAO>();
builder.Services.AddScoped<RecursosSoporteBLL>();
builder.Services.AddScoped<AsistenteTIDAO>();
builder.Services.AddScoped<AsistenteUsuarioBLL>();
builder.Services.AddScoped<InvestigadorAgenteTI>();
builder.Services.AddScoped<AsistenteTIBLL>();
builder.Services.AddScoped<ReproduccionUsuarioBLL>();
builder.Services.AddSingleton<AgenteCodigoClient>();
builder.Services.AddHostedService<SqlTrazaListener>();
builder.Services.AddHttpClient<OpenAIAsistenteClient>(cliente => cliente.Timeout = TimeSpan.FromSeconds(90));
builder.Services.AddHttpClient<GeminiLiveClient>(cliente => cliente.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddMemoryCache();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opciones =>
    {
        opciones.Cookie.Name = "SistemaTicketsInteligente.Auth";
        opciones.Cookie.HttpOnly = true;
        opciones.Cookie.SameSite = SameSiteMode.Lax;
        opciones.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        opciones.ExpireTimeSpan = TimeSpan.FromHours(8);
        opciones.SlidingExpiration = true;
        opciones.Events.OnRedirectToLogin = contexto => { contexto.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        opciones.Events.OnRedirectToAccessDenied = contexto => { contexto.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
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
        politica.WithOrigins(origenes).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    });
});

var app = builder.Build();
app.UseResponseCompression();

app.UseExceptionHandler(aplicacionError =>
{
    aplicacionError.Run(async contexto =>
    {
        var excepcion = contexto.Features.Get<IExceptionHandlerFeature>()?.Error;
        var errorSqlFuncional = excepcion is SqlException sqlFuncional && sqlFuncional.Number is >= 50000 and <= 50999;
        var baseDatosNoDisponible = excepcion is SqlException && !errorSqlFuncional;
        contexto.Response.StatusCode = errorSqlFuncional ? StatusCodes.Status409Conflict : baseDatosNoDisponible ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status500InternalServerError;
        contexto.Response.ContentType = "application/json";
        await contexto.Response.WriteAsJsonAsync(new
        {
            mensaje = errorSqlFuncional ? excepcion!.Message : baseDatosNoDisponible ? "La base de datos no se encuentra disponible. Intenta nuevamente en unos momentos." : "Se produjo un error al procesar la solicitud.",
            idSeguimiento = contexto.TraceIdentifier
        });
    });
});

app.UseRouting();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.UseMiddleware<CorrelacionMiddleware>();

app.MapControllers();
app.MapGet("/api/salud", async (ConexionSqlServer conexion, CancellationToken cancellationToken) =>
{
    try
    {
        await conexion.VerificarAsync(cancellationToken);
        return Results.Ok(new { estado = "ok", baseDatos = "disponible" });
    }
    catch (SqlException)
    {
        return Results.Json(new { estado = "degradado", baseDatos = "no disponible" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.Run();
