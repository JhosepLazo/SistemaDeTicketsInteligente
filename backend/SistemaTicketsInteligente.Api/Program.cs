/**
 * Archivo: Program.cs
 * Objetivo: Configurar y ejecutar la API ASP.NET Core del Sistema de Tickets Inteligente.
 * Responsabilidad: Registrar dependencias, autenticación, autorización, CORS, rate limiting y manejo general de errores.
 * Dependencias: BLL, DAO, ASP.NET Core y appsettings.json.
 * Flujo: Inicio de aplicación -> configuración -> middleware -> Controllers.
 * Consideraciones: Mantiene únicamente configuración transversal; no contiene reglas de negocio ni acceso SQL.
 */

using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using SistemaTicketsInteligente.Api.BLL;
using SistemaTicketsInteligente.Api.DAO;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var cadenaConexion = builder.Configuration.GetConnectionString("CnnGestionTi")
    ?? throw new InvalidOperationException("No se configuró la conexión 'CnnGestionTi'.");

builder.Services.AddSingleton(new ConexionSqlServer(cadenaConexion));
builder.Services.AddScoped<AutenticacionDAO>();
builder.Services.AddScoped<AutenticacionBLL>();
builder.Services.AddScoped<InicioUsuarioDAO>();
builder.Services.AddScoped<InicioUsuarioBLL>();

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
