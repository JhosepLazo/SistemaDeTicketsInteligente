/**
 * Archivo: EndpointsApi.cs
 * Objetivo: Enumerar los endpoints reales de la API con los perfiles que admiten sus atributos de autorización.
 * Responsabilidad: Leer los metadatos de enrutamiento (método, plantilla, [Authorize], [AllowAnonymous]) y armar una ruta llamable que cumpla
 *   las restricciones de cada parámetro.
 * Dependencias: EndpointDataSource de ASP.NET Core.
 * Flujo: FabricaApi.Services -> Listar -> MatrizAutorizacionPruebas y ConsultasPermitidasPruebas.
 * Consideraciones: Varios [Authorize] en un endpoint (controlador y acción) se cumplen todos a la vez: los perfiles se intersecan.
 */

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

namespace SistemaTicketsInteligente.Pruebas.Integracion;

public static class EndpointsApi
{
    public static readonly string[] Perfiles = ["USR", "TEC", "SUP", "ADM"];

    /// <param name="Plantilla">Ruta declarada, por ejemplo api/gestion-tickets/{incidenciaNumero}/asignar.</param>
    /// <param name="Ruta">Ruta llamable con valores de ejemplo en los parámetros.</param>
    /// <param name="Permitidos">Perfiles admitidos por los atributos; null si el endpoint no exige sesión.</param>
    public sealed record Operacion(string Metodo, string Plantilla, string Ruta, bool TieneParametros, string[]? Permitidos)
    {
        public string Clave => $"{Metodo} {Plantilla}";
    }

    public static List<Operacion> Listar(IServiceProvider servicios)
    {
        var operaciones = new List<Operacion>();
        foreach (var endpoint in servicios.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>())
        {
            var plantilla = endpoint.RoutePattern.RawText?.TrimStart('/') ?? string.Empty;
            if (!plantilla.StartsWith("api/", StringComparison.Ordinal)) continue;
            var ruta = string.Join("/", endpoint.RoutePattern.PathSegments.Select(Segmento));
            foreach (var metodo in endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["GET"])
                operaciones.Add(new Operacion(metodo, plantilla, ruta, endpoint.RoutePattern.Parameters.Count > 0, Permitidos(endpoint)));
        }
        return operaciones;
    }

    private static string[]? Permitidos(Endpoint endpoint)
    {
        var autorizaciones = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
        if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null || autorizaciones.Count == 0) return null;
        IEnumerable<string> permitidos = Perfiles;
        foreach (var roles in autorizaciones.Select(x => x.Roles).Where(x => !string.IsNullOrWhiteSpace(x)))
            permitidos = permitidos.Intersect(roles!.Split(',', StringSplitOptions.TrimEntries));
        return permitidos.ToArray();
    }

    // Valores que cumplen las restricciones de la ruta ({x:long}, {x:int}); la autorización corta la petición antes de usarlos.
    private static string Segmento(RoutePatternPathSegment segmento) => string.Concat(segmento.Parts.Select(parte => parte switch
    {
        RoutePatternLiteralPart literal => literal.Content,
        RoutePatternParameterPart parametro when parametro.ParameterPolicies.Count > 0 => "1",
        RoutePatternParameterPart => "INC-000001",
        _ => string.Empty
    }));
}
