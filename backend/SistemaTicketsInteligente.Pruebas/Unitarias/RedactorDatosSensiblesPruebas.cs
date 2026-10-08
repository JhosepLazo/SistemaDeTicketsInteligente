/**
 * Archivo: RedactorDatosSensiblesPruebas.cs
 * Objetivo: Comprobar que ningún secreto llega a la base ni a un proveedor de IA, y que los datos personales no salen hacia la IA.
 * Responsabilidad: Cubrir credenciales, tokens, claves de proveedores, tarjetas (con y sin Luhn), correos, teléfonos y documentos, y que los
 *   textos legítimos (números de ticket, frases con "clave") se conservan.
 * Dependencias: RedactorDatosSensibles.
 * Flujo: texto con datos -> RedactarSecretos / RedactarParaIA -> marcadores legibles.
 * Consideraciones: Las claves de ejemplo se arman en tiempo de ejecución para que ningún escáner de secretos las confunda con reales.
 */

namespace SistemaTicketsInteligente.Pruebas.Unitarias;

public sealed class RedactorDatosSensiblesPruebas
{
    [Theory]
    [InlineData("password: Secreta123", "password: [SECRETO OCULTO]")]
    [InlineData("Contraseña=Hola.2026", "Contraseña=[SECRETO OCULTO]")]
    [InlineData("pwd = \"con espacios\" listo", "pwd = [SECRETO OCULTO] listo")]
    [InlineData("api_key: abc123", "api_key: [SECRETO OCULTO]")]
    [InlineData("mi contraseña es Verano2026", "mi contraseña es [SECRETO OCULTO]")]
    [InlineData("el password era qwerty", "el password era [SECRETO OCULTO]")]
    public void CredencialesSeOcultan(string texto, string esperado) => Assert.Equal(esperado, RedactorDatosSensibles.RedactarSecretos(texto));

    [Fact]
    public void TokenBearerSeOculta()
    {
        var resultado = RedactorDatosSensibles.RedactarSecretos("Authorization: Bearer " + new string('a', 40));

        Assert.Equal("Authorization: Bearer [SECRETO OCULTO]", resultado);
    }

    [Theory]
    [InlineData("sk-", 24)]
    [InlineData("AIza", 35)]
    [InlineData("ghp_", 36)]
    [InlineData("xoxb-", 20)]
    public void ClavesDeProveedoresSeOcultan(string prefijo, int largo)
    {
        var clave = prefijo + new string('Q', largo);

        var resultado = RedactorDatosSensibles.RedactarSecretos($"La clave quedó en {clave} ayer", out var ocultados);

        Assert.DoesNotContain(clave, resultado);
        Assert.Contains("[SECRETO OCULTO]", resultado);
        Assert.Equal(1, ocultados);
    }

    [Fact]
    public void TarjetaValidaSeOcultaConservandoLosUltimosDigitos()
    {
        var resultado = RedactorDatosSensibles.RedactarSecretos("Pagó con 4111 1111 1111 1111 hoy");

        Assert.Equal("Pagó con [TARJETA ****1111] hoy", resultado);
    }

    [Theory]
    [InlineData("Documento 4111111111111112 rechazado")]
    [InlineData("Revisar INC-2026000123 y TKT-000045")]
    [InlineData("La clave es revisar el lote primero")]
    [InlineData("El pin del almacén está bloqueado")]
    public void TextoLegitimoSeConserva(string texto) => Assert.Equal(texto, RedactorDatosSensibles.RedactarSecretos(texto));

    [Fact]
    public void SecretosNoOcultanDatosPersonales()
    {
        const string texto = "Escribir a ana.perez@calimod.com o al 987 654 321";

        Assert.Equal(texto, RedactorDatosSensibles.RedactarSecretos(texto));
    }

    [Theory]
    [InlineData("Escribir a ana.perez@calimod.com", "Escribir a [CORREO]")]
    [InlineData("Llamar al 987 654 321 hoy", "Llamar al [TELEFONO] hoy")]
    [InlineData("Llamar al +51 987-654-321", "Llamar al [TELEFONO]")]
    [InlineData("DNI: 12345678 del usuario", "DNI: [DOCUMENTO] del usuario")]
    [InlineData("RUC 20123456789", "RUC [DOCUMENTO]")]
    public void DatosPersonalesSeOcultanAntesDeIrALaIA(string texto, string esperado) =>
        Assert.Equal(esperado, RedactorDatosSensibles.RedactarParaIA(texto));

    [Fact]
    public void ParaIAOcultaSecretosYDatosPersonalesYLosCuenta()
    {
        var resultado = RedactorDatosSensibles.RedactarParaIA("token: abc123 enviado a ana@calimod.com", out var ocultados);

        Assert.Equal("token: [SECRETO OCULTO] enviado a [CORREO]", resultado);
        Assert.Equal(2, ocultados);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TextoVacioDevuelveVacio(string? texto)
    {
        Assert.Equal(string.Empty, RedactorDatosSensibles.RedactarSecretos(texto));
        Assert.Equal(string.Empty, RedactorDatosSensibles.RedactarParaIA(texto));
    }
}
