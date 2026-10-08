/**
 * Archivo: FabricaApi.cs
 * Objetivo: Levantar la API completa en memoria para las pruebas de integración.
 * Responsabilidad: Usar el ambiente Pruebas (solo appsettings.json y variables de entorno), apagar los trabajos en segundo plano del agente,
 *   reemplazar el login por ManejadorAutenticacionPrueba y entregar clientes con identidad y token CSRF.
 * Dependencias: WebApplicationFactory, Program y ManejadorAutenticacionPrueba.
 * Flujo: prueba -> Cliente(usuario, área, perfil) -> pipeline real (correlación, CSRF, límites, autorización) -> controlador.
 * Consideraciones: La conexión es la de appsettings.json (base local) o la de la variable ConnectionStrings__CnnSistemaTickets en el CI.
 *   Las pruebas que no leen la base se detienen antes de llegar a ella (401, 403 o 400 de CSRF).
 */

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SistemaTicketsInteligente.Pruebas.Integracion;

public sealed class FabricaApi : WebApplicationFactory<Program>
{
    public const string EncabezadoCsrf = "X-CSRF-TOKEN";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Pruebas");
        builder.UseSetting("AgenteTI:Mantenimiento", "false");
        builder.UseSetting("AgenteTI:InvestigacionAutomatica", "false");
        builder.ConfigureTestServices(servicios =>
        {
            // Sin trabajos en segundo plano: una prueba no debe investigar, reconciliar ni cerrar tickets por su cuenta.
            var propios = servicios
                .Where(x => !x.IsKeyedService && x.ServiceType == typeof(IHostedService) && x.ImplementationType?.Assembly == typeof(Program).Assembly)
                .ToList();
            foreach (var servicio in propios) servicios.Remove(servicio);

            servicios.AddAuthentication(ManejadorAutenticacionPrueba.Esquema)
                .AddScheme<AuthenticationSchemeOptions, ManejadorAutenticacionPrueba>(ManejadorAutenticacionPrueba.Esquema, _ => { });
        });
    }

    /// <summary>Cliente con cookies propias; sin perfil es anónimo.</summary>
    public HttpClient Cliente(string? usuario = null, string area = "TIC", string? perfil = null)
    {
        var cliente = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        if (usuario is not null && perfil is not null) cliente.DefaultRequestHeaders.Add(ManejadorAutenticacionPrueba.Encabezado, $"{usuario}|{area}|{perfil}");
        return cliente;
    }

    /// <summary>Pide el token CSRF de la sesión del cliente y lo deja en sus encabezados, como hace services/api.ts.</summary>
    public static async Task<HttpClient> ConTokenCsrfAsync(HttpClient cliente)
    {
        var respuesta = await cliente.GetFromJsonAsync<JsonElement>("api/autenticacion/token-csrf");
        cliente.DefaultRequestHeaders.Remove(EncabezadoCsrf);
        cliente.DefaultRequestHeaders.Add(EncabezadoCsrf, respuesta.GetProperty("token").GetString());
        return cliente;
    }
}
