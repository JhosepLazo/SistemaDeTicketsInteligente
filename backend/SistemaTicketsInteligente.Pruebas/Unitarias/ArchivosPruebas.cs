/**
 * Archivo: ArchivosPruebas.cs
 * Objetivo: Comprobar que una ruta guardada en la base no puede usarse para descargar archivos fuera de la carpeta permitida.
 * Responsabilidad: Aceptar rutas dentro de uploads/ y rechazar recorridos con "..", rutas absolutas y carpetas vecinas con el mismo prefijo.
 * Dependencias: Archivos.RutaSegura.
 * Flujo: ruta relativa registrada -> RutaSegura -> ruta física o InvalidOperationException.
 * Consideraciones: RutaSegura resuelve contra el directorio actual, igual que la API en ejecución.
 */

namespace SistemaTicketsInteligente.Pruebas.Unitarias;

public sealed class ArchivosPruebas
{
    private const string Mensaje = "Ruta no permitida.";

    [Fact]
    public void RutaDentroDeLaCarpetaSeResuelve()
    {
        var ruta = Archivos.RutaSegura($"{Archivos.Incidencias}/abc/evidencia.pdf", Archivos.Incidencias, Mensaje);

        Assert.Equal(Archivos.RutaFisica($"{Archivos.Incidencias}/abc/evidencia.pdf"), ruta);
        Assert.StartsWith(Archivos.RutaFisica(Archivos.Incidencias), ruta);
    }

    [Theory]
    [InlineData("uploads/incidencias/../../appsettings.json")]
    [InlineData("uploads/incidencias/abc/../../formatos/plantilla.xlsx")]
    [InlineData("uploads/incidenciasX/evidencia.pdf")]
    [InlineData("uploads/incidencias")]
    [InlineData("../uploads/incidencias/evidencia.pdf")]
    public void RutaFueraDeLaCarpetaSeRechaza(string ruta)
    {
        var error = Assert.Throws<InvalidOperationException>(() => Archivos.RutaSegura(ruta, Archivos.Incidencias, Mensaje));

        Assert.Equal(Mensaje, error.Message);
    }

    [Fact]
    public void RutaAbsolutaSeRechaza()
    {
        var absoluta = Path.Combine(Path.GetTempPath(), "fuera.txt");

        Assert.Throws<InvalidOperationException>(() => Archivos.RutaSegura(absoluta, Archivos.Incidencias, Mensaje));
    }
}
