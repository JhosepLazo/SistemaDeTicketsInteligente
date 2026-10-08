/**
 * Archivo: ConsultasPermitidasPruebas.cs
 * Objetivo: Comprobar contra la base real que cada perfil permitido obtiene las consultas de su módulo.
 * Responsabilidad: Llamar a cada consulta GET sin parámetros de ruta con un usuario del seed de cada perfil admitido y exigir 200: así se
 *   verifican juntos controlador, BLL, procedimiento, permisos SQL (Rol_TI_Api) y lectura de columnas por nombre. También comprueba que la
 *   ficha obligatoria de un requerimiento se exige antes de registrar nada.
 * Dependencias: FabricaApi, EndpointsApi, BaseDatosFactAttribute y los usuarios USR001, TEC001, SUP001 y ADM001 de 09_InsertDeDatos.sql.
 * Flujo: endpoints GET -> perfiles permitidos -> petición con identidad del seed -> 200.
 * Consideraciones: Solo lee. Se ejecuta con PRUEBAS_BASE_DATOS=1.
 */

namespace SistemaTicketsInteligente.Pruebas.Integracion;

public sealed class ConsultasPermitidasPruebas(FabricaApi fabrica) : IClassFixture<FabricaApi>
{
    private static readonly Dictionary<string, (string Usuario, string Area)> UsuariosSeed = new()
    {
        ["USR"] = ("USR001", "CMP"),
        ["TEC"] = ("TEC001", "TIC"),
        ["SUP"] = ("SUP001", "TIC"),
        ["ADM"] = ("ADM001", "TIC")
    };

    // Consultas que exigen filtros en la cadena de consulta.
    private static readonly Dictionary<string, string> Filtros = new()
    {
        ["api/reportes/ti"] = "?fechaInicio=2026-01-01&fechaFin=2026-12-31",
        ["api/reportes/ti/agente"] = "?desde=2026-01-01&hasta=2026-12-31"
    };

    [BaseDatosFact]
    public async Task CadaPerfilPermitidoObtieneLasConsultasDeSuModulo()
    {
        var errores = new List<string>();
        var consultas = EndpointsApi.Listar(fabrica.Services).Where(x => x.Metodo == "GET" && !x.TieneParametros && x.Permitidos is not null).ToList();
        foreach (var consulta in consultas)
        {
            foreach (var perfil in consulta.Permitidos!)
            {
                var (usuario, area) = UsuariosSeed[perfil];
                using var cliente = fabrica.Cliente(usuario, area, perfil);
                using var respuesta = await cliente.GetAsync(consulta.Ruta + Filtros.GetValueOrDefault(consulta.Plantilla, string.Empty));
                if (respuesta.StatusCode != HttpStatusCode.OK)
                    errores.Add($"{consulta.Clave} con {perfil}: {(int)respuesta.StatusCode} {await respuesta.Content.ReadAsStringAsync()}");
            }
        }

        Assert.NotEmpty(consultas);
        Assert.True(errores.Count == 0, string.Join(Environment.NewLine, errores));
    }

    [BaseDatosFact]
    public async Task RequerimientoSinFichaNoSeRegistra()
    {
        using var cliente = await FabricaApi.ConTokenCsrfAsync(fabrica.Cliente("USR001", "CMP", "USR"));
        var datos = await cliente.GetFromJsonAsync<JsonElement>("api/tickets/nuevo/datos");
        var linea = datos.GetProperty("lineas")[0].GetProperty("codigo").GetString()!;
        var sustento = new ByteArrayContent("%PDF-1.4\n%prueba\n"u8.ToArray());
        sustento.Headers.ContentType = new("application/pdf");
        using var formulario = new MultipartFormDataContent
        {
            { new StringContent(linea), "Linea" },
            { new StringContent("REQ"), "Tipo" },
            { new StringContent("Nuevo reporte de ventas por zona"), "Titulo" },
            { new StringContent("Necesitamos un reporte de ventas por zona para el cierre mensual."), "Detalle" },
            { sustento, "Adjuntos", "sustento.pdf" }
        };

        using var respuesta = await cliente.PostAsync("api/tickets/nuevo", formulario);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("Completa la ficha", await respuesta.Content.ReadAsStringAsync());
    }
}
