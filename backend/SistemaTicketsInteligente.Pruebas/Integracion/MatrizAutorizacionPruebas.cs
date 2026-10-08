/**
 * Archivo: MatrizAutorizacionPruebas.cs
 * Objetivo: Comprobar la matriz de autorización de la API: cada endpoint frente a cada perfil y frente a una petición anónima.
 * Responsabilidad: Declarar qué perfiles atiende cada módulo, verificar que los atributos de los controladores coinciden y comprobar por HTTP
 *   que un perfil no permitido recibe 403 y una petición anónima 401 (o 400 de CSRF si modifica datos).
 * Dependencias: FabricaApi, EndpointsApi y ManejadorAutenticacionPrueba.
 * Flujo: endpoints registrados -> matriz esperada -> metadatos -> peticiones denegadas.
 * Consideraciones: La matriz de este archivo es la fuente de verdad: un endpoint nuevo sin fila o con otros roles hace fallar la prueba.
 *   Las combinaciones permitidas se ejercitan contra la base en ConsultasPermitidasPruebas.
 */

namespace SistemaTicketsInteligente.Pruebas.Integracion;

public sealed class MatrizAutorizacionPruebas(FabricaApi fabrica) : IClassFixture<FabricaApi>
{
    private static readonly string[] Todos = EndpointsApi.Perfiles;
    private static readonly string[] TI = ["TEC", "SUP", "ADM"];
    private static readonly string[] Colaborador = ["USR"];

    // Prefijo de ruta -> perfiles que lo atienden (gana el prefijo más largo).
    private static readonly (string Prefijo, string[] Perfiles)[] Matriz =
    [
        ("api/asistente/ti", TI),
        ("api/asistente/usuario", Colaborador),
        ("api/autenticacion", Todos),
        ("api/base-conocimiento", TI),
        ("api/configuracion-ti", TI),
        ("api/gestion-operativa-ti", TI),
        ("api/gestion-tickets", TI),
        ("api/inicio/usuario", Colaborador),
        ("api/inicio/ti", TI),
        ("api/mis-tickets", Colaborador),
        ("api/notificaciones", Todos),
        ("api/recursos-soporte", Todos),
        ("api/reportes/ti", TI),
        ("api/reproducciones", Todos),
        ("api/tickets/nuevo", Colaborador)
    ];

    // El control del agente, la política de autonomía, el catálogo de acciones y las fichas solo los cambia un administrador.
    private static readonly HashSet<string> SoloAdministrador =
    [
        "POST api/configuracion-ti/agente/parametros",
        "POST api/configuracion-ti/agente/politica",
        "POST api/configuracion-ti/agente/acciones",
        "POST api/configuracion-ti/fichas"
    ];

    private static readonly HashSet<string> Anonimos = ["POST api/autenticacion/iniciar-sesion", "GET api/salud"];

    [Fact]
    public void CadaEndpointTieneLosPerfilesDeLaMatriz()
    {
        var errores = new List<string>();
        foreach (var operacion in EndpointsApi.Listar(fabrica.Services))
        {
            if (Anonimos.Contains(operacion.Clave))
            {
                if (operacion.Permitidos is not null) errores.Add($"{operacion.Clave} debería admitir peticiones anónimas.");
                continue;
            }
            if (operacion.Permitidos is null)
            {
                errores.Add($"{operacion.Clave} no exige sesión.");
                continue;
            }
            var esperados = Esperados(operacion);
            if (esperados is null) errores.Add($"{operacion.Clave} no tiene fila en la matriz de autorización.");
            else if (!esperados.Order().SequenceEqual(operacion.Permitidos.Order()))
                errores.Add($"{operacion.Clave}: esperado [{string.Join(",", esperados)}], configurado [{string.Join(",", operacion.Permitidos)}].");
        }

        Assert.True(errores.Count == 0, string.Join(Environment.NewLine, errores));
    }

    [Fact]
    public async Task PerfilNoPermitidoRecibeProhibido()
    {
        var errores = new List<string>();
        var numero = 0;
        foreach (var operacion in EndpointsApi.Listar(fabrica.Services).Where(x => x.Permitidos is not null))
        {
            foreach (var perfil in Todos.Except(Esperados(operacion) ?? Todos))
            {
                // Un usuario distinto por petición: los límites por usuario no deben interferir con la matriz.
                using var cliente = fabrica.Cliente($"PRUEBA{++numero:000}", "TIC", perfil);
                if (operacion.Metodo != "GET") await FabricaApi.ConTokenCsrfAsync(cliente);
                using var respuesta = await cliente.SendAsync(Peticion(operacion));
                if (respuesta.StatusCode != HttpStatusCode.Forbidden) errores.Add($"{operacion.Clave} con {perfil}: {(int)respuesta.StatusCode}.");
            }
        }

        Assert.True(numero > 0, "No se probó ninguna combinación denegada.");
        Assert.True(errores.Count == 0, string.Join(Environment.NewLine, errores));
    }

    [Fact]
    public async Task PeticionAnonimaEsRechazada()
    {
        var errores = new List<string>();
        using var cliente = fabrica.Cliente();
        foreach (var operacion in EndpointsApi.Listar(fabrica.Services).Where(x => !Anonimos.Contains(x.Clave)))
        {
            using var respuesta = await cliente.SendAsync(Peticion(operacion));
            // Sin sesión no hay token CSRF: una operación que modifica datos se corta antes, con 400 y X-Csrf-Invalido.
            var esperado = operacion.Metodo == "GET" ? HttpStatusCode.Unauthorized : HttpStatusCode.BadRequest;
            if (respuesta.StatusCode != esperado) errores.Add($"{operacion.Clave}: {(int)respuesta.StatusCode}, se esperaba {(int)esperado}.");
            else if (esperado == HttpStatusCode.BadRequest && !respuesta.Headers.Contains("X-Csrf-Invalido"))
                errores.Add($"{operacion.Clave}: 400 sin X-Csrf-Invalido.");
        }

        Assert.True(errores.Count == 0, string.Join(Environment.NewLine, errores));
    }

    private static string[]? Esperados(EndpointsApi.Operacion operacion) => SoloAdministrador.Contains(operacion.Clave)
        ? ["ADM"]
        : Matriz.Where(x => operacion.Plantilla.StartsWith(x.Prefijo, StringComparison.Ordinal)).OrderByDescending(x => x.Prefijo.Length)
            .Select(x => x.Perfiles).FirstOrDefault();

    private static HttpRequestMessage Peticion(EndpointsApi.Operacion operacion) => new(new HttpMethod(operacion.Metodo), operacion.Ruta)
    {
        Content = operacion.Metodo == "GET" ? null : JsonContent.Create(new { })
    };
}
