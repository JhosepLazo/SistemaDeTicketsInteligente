/**
 * Archivo: ProteccionSesionPruebas.cs
 * Objetivo: Comprobar la protección CSRF y la correlación de cada respuesta.
 * Responsabilidad: Verificar que una operación sin token, o con el token de otra sesión, se rechaza con 400 y X-Csrf-Invalido; que un token
 *   válido deja pasar la petición hasta la autorización; que el inicio de sesión está exento; y que toda respuesta trae X-Correlation-ID.
 * Dependencias: FabricaApi y ManejadorAutenticacionPrueba.
 * Flujo: cliente con identidad -> token-csrf -> operación -> código y encabezados esperados.
 * Consideraciones: Ninguna de estas peticiones llega a la base de datos.
 */

namespace SistemaTicketsInteligente.Pruebas.Integracion;

public sealed class ProteccionSesionPruebas(FabricaApi fabrica) : IClassFixture<FabricaApi>
{
    private const string RutaSoloAdministrador = "api/configuracion-ti/agente/parametros";

    [Fact]
    public async Task OperacionSinTokenCsrfSeRechaza()
    {
        using var cliente = fabrica.Cliente("CSRF01", "TIC", "ADM");

        using var respuesta = await cliente.PostAsJsonAsync(RutaSoloAdministrador, new { parametro = "AGENTE_MODO", valor = "APAGADO" });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("1", Assert.Single(respuesta.Headers.GetValues("X-Csrf-Invalido")));
    }

    [Fact]
    public async Task TokenDeOtraSesionSeRechaza()
    {
        // El mismo navegador (mismas cookies) cambia de usuario: el token emitido para el primero no sirve al segundo.
        using var cliente = await FabricaApi.ConTokenCsrfAsync(fabrica.Cliente("CSRF02", "TIC", "ADM"));
        cliente.DefaultRequestHeaders.Remove(ManejadorAutenticacionPrueba.Encabezado);
        cliente.DefaultRequestHeaders.Add(ManejadorAutenticacionPrueba.Encabezado, "CSRF03|TIC|ADM");

        using var respuesta = await cliente.PostAsJsonAsync(RutaSoloAdministrador, new { parametro = "AGENTE_MODO", valor = "APAGADO" });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.True(respuesta.Headers.Contains("X-Csrf-Invalido"));
    }

    [Fact]
    public async Task TokenFalsificadoSeRechaza()
    {
        using var cliente = await FabricaApi.ConTokenCsrfAsync(fabrica.Cliente("CSRF04", "TIC", "ADM"));
        cliente.DefaultRequestHeaders.Remove(FabricaApi.EncabezadoCsrf);
        cliente.DefaultRequestHeaders.Add(FabricaApi.EncabezadoCsrf, "token-inventado");

        using var respuesta = await cliente.PostAsJsonAsync(RutaSoloAdministrador, new { parametro = "AGENTE_MODO", valor = "APAGADO" });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.True(respuesta.Headers.Contains("X-Csrf-Invalido"));
    }

    [Fact]
    public async Task TokenValidoLlegaHastaLaAutorizacion()
    {
        // Un técnico con token válido supera el CSRF; lo detiene la autorización (la ruta es solo de ADM), nunca el CSRF.
        using var cliente = await FabricaApi.ConTokenCsrfAsync(fabrica.Cliente("CSRF05", "TIC", "TEC"));

        using var respuesta = await cliente.PostAsJsonAsync(RutaSoloAdministrador, new { parametro = "AGENTE_MODO", valor = "APAGADO" });

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.False(respuesta.Headers.Contains("X-Csrf-Invalido"));
    }

    [Fact]
    public async Task InicioDeSesionNoExigeToken()
    {
        using var cliente = fabrica.Cliente();

        // Datos incompletos: la BLL responde antes de consultar la base.
        using var respuesta = await cliente.PostAsJsonAsync("api/autenticacion/iniciar-sesion", new { usuario = "", contrasena = "" });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.False(respuesta.Headers.Contains("X-Csrf-Invalido"));
    }

    [Theory]
    [InlineData("GET", "api/autenticacion/token-csrf", "CORR01|TIC|TEC", HttpStatusCode.OK)]
    [InlineData("GET", "api/gestion-tickets", "CORR02|CMP|USR", HttpStatusCode.Forbidden)]
    [InlineData("GET", "api/mis-tickets", null, HttpStatusCode.Unauthorized)]
    [InlineData("POST", "api/autenticacion/cerrar-sesion", "CORR03|TIC|TEC", HttpStatusCode.BadRequest)]
    public async Task CadaRespuestaInformaSuCorrelacion(string metodo, string ruta, string? identidad, HttpStatusCode esperado)
    {
        using var cliente = fabrica.Cliente();
        if (identidad is not null) cliente.DefaultRequestHeaders.Add(ManejadorAutenticacionPrueba.Encabezado, identidad);

        using var respuesta = await cliente.SendAsync(new HttpRequestMessage(new HttpMethod(metodo), ruta));

        Assert.Equal(esperado, respuesta.StatusCode);
        Assert.True(Guid.TryParse(Assert.Single(respuesta.Headers.GetValues("X-Correlation-ID")), out _));
    }
}
