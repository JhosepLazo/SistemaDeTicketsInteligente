/**
 * Archivo: Program.cs
 * Objetivo: Configurar y ejecutar la API ASP.NET Core del Sistema de Tickets Inteligente.
 * Responsabilidad: Registrar dependencias, autenticación, autorización, protección de datos, CORS, rate limiting y manejo general de errores.
 * Dependencias: BLL, DAO, ASP.NET Core, Data Protection y appsettings.json.
 * Flujo: Inicio de aplicación -> configuración -> middleware -> Controllers.
 * Consideraciones: Las claves que protegen la cookie se guardan fuera del repositorio para conservar sesiones válidas entre reinicios; la identidad corporativa es opcional por ambiente y no se almacenan secretos en este archivo.
 */

using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using SistemaTicketsInteligente.Api.BLL;
using SistemaTicketsInteligente.Api.DAO;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var cadenaConexion = builder.Configuration.GetConnectionString("CnnGestionTi")
    ?? throw new InvalidOperationException("No se configuró la conexión 'CnnGestionTi'.");

// Mantiene las claves de cifrado de la cookie fuera del proyecto para que reiniciar la API no invalide una sesión vigente.
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
builder.Services.AddScoped<GestionTicketsTIDAO>();
builder.Services.AddScoped<GestionTicketsTIBLL>();
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

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opciones =>
    {
        opciones.Cookie.Name = "SistemaTicketsInteligente.Auth";
        opciones.Cookie.HttpOnly = true;
        opciones.Cookie.SameSite = SameSiteMode.Lax;
        opciones.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        opciones.ExpireTimeSpan = TimeSpan.FromHours(8);
        opciones.SlidingExpiration = true;
        opciones.Events.OnRedirectToLogin = contexto =>
        {
            contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        opciones.Events.OnRedirectToAccessDenied = contexto =>
        {
            contexto.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(opciones =>
{
    opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opciones.AddPolicy("Login", contexto => RateLimitPartition.GetFixedWindowLimiter(
        contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocido",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
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

app.UseExceptionHandler(aplicacionError =>
{
    aplicacionError.Run(async contexto =>
    {
        contexto.Response.StatusCode = StatusCodes.Status500InternalServerError;
        contexto.Response.ContentType = "application/json";
        await contexto.Response.WriteAsJsonAsync(new { mensaje = "Se produjo un error al procesar la solicitud." });
    });
});

app.UseRouting();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/api/salud", () => Results.Ok(new { estado = "ok" }));

app.Run();
