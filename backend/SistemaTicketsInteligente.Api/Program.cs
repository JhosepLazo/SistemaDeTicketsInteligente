using SistemaTicketsInteligente.Datos.ComponenteDAO.Autenticacion;
using SistemaTicketsInteligente.Datos.ComponenteDAO.Data;
using SistemaTicketsInteligente.Negocio.ComponenteBLL.Autenticacion;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var cadenaConexion = builder.Configuration.GetConnectionString("CnnGestionTi")
    ?? throw new InvalidOperationException("No se configuró la conexión 'CnnGestionTi'.");

builder.Services.AddSingleton(new ConexionSqlServer(cadenaConexion));
builder.Services.AddScoped<AutenticacionDAO>();
builder.Services.AddScoped<AutenticacionBLL>();

builder.Services.AddCors(opciones =>
{
    opciones.AddPolicy("Frontend", politica =>
    {
        var origenes = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

        politica.WithOrigins(origenes)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("Frontend");
app.MapControllers();
app.MapGet("/api/salud", () => Results.Ok(new { estado = "ok" }));

app.Run();
