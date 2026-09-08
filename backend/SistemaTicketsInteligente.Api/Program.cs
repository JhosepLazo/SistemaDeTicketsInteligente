/**
    Archivo: Program.cs
    Objetivo: Iniciar y configurar la aplicación web ASP.NET Core.
    Responsabilidad: Registrar controladores, construir la aplicación, mapear las rutas HTTP y ejecutar el servidor.
    Dependencias: ASP.NET Core y la configuración declarada en el archivo del proyecto.
    Flujo: Inicio del proceso -> registro de servicios -> construcción de la aplicación -> atención HTTP.
    Consideraciones: Mantiene una configuración mínima; esta cabecera no agrega servicios ni funcionalidades.
*/

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();

app.Run();
