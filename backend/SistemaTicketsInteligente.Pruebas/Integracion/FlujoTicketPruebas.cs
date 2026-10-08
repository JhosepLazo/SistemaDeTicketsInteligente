/**
 * Archivo: FlujoTicketPruebas.cs
 * Objetivo: Recorrer de punta a punta el ciclo de un ticket a través de la API real y la base real.
 * Responsabilidad: Registrar un ticket como colaborador, clasificarlo, asignarlo, pedir información, responderla, resolverlo, validarlo y
 *   calificarlo; comprobar cada estado (la máquina de estados de la base rechaza transiciones no permitidas) y que un mensaje interno de TI
 *   nunca llega al colaborador.
 * Dependencias: FabricaApi, BaseDatosFactAttribute, los usuarios USR001 y TEC001 del seed y una conexión con permiso de escritura directa
 *   (PRUEBAS_CNN_ADMIN o la conexión de appsettings) para leer el catálogo y sembrar la nota interna.
 * Flujo: colaborador -> TI -> colaborador -> TI -> colaborador, cada paso por HTTP con su token CSRF.
 * Consideraciones: Crea un ticket: se ejecuta solo con PRUEBAS_FLUJOS=1, pensado para el contenedor desechable del CI. Ningún flujo de la
 *   aplicación crea mensajes internos todavía; por eso la nota se siembra directamente.
 */

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SistemaTicketsInteligente.Pruebas.Integracion;

public sealed class FlujoTicketPruebas(FabricaApi fabrica) : IClassFixture<FabricaApi>
{
    private sealed record Clasificacion(string Linea, string Item, string Tipo, string SubTipo, string Categoria);

    [BaseDatosFact(Escribe = true)]
    public async Task TicketRecorreElCicloCompletoSinMostrarLoInterno()
    {
        using var colaborador = await FabricaApi.ConTokenCsrfAsync(fabrica.Cliente("USR001", "CMP", "USR"));
        using var tecnico = await FabricaApi.ConTokenCsrfAsync(fabrica.Cliente("TEC001", "TIC", "TEC"));
        var clasificacion = await ClasificacionValidaAsync();

        // 1. El colaborador registra el ticket desde el portal.
        using var formulario = new MultipartFormDataContent
        {
            { new StringContent(clasificacion.Linea), "Linea" },
            { new StringContent(clasificacion.Tipo), "Tipo" },
            { new StringContent("Prueba automatizada del ciclo completo"), "Titulo" },
            { new StringContent("Al grabar la orden de compra el sistema muestra un error y no avanza."), "Detalle" },
            { new StringContent("Error de prueba automatizada"), "MensajeError" }
        };
        var creado = await LeerAsync(await colaborador.PostAsync("api/tickets/nuevo", formulario), HttpStatusCode.Created);
        var numero = creado.GetProperty("incidenciaNumero").GetString()!;
        Assert.Equal("NV", await EstadoAsync(colaborador, numero));

        // 2. TI clasifica y asigna.
        await LeerAsync(await tecnico.PostAsJsonAsync($"api/gestion-tickets/{numero}/clasificar", new
        {
            linea = clasificacion.Linea, item = clasificacion.Item, tipo = clasificacion.Tipo, subTipo = clasificacion.SubTipo, categoria = clasificacion.Categoria
        }));
        await LeerAsync(await tecnico.PostAsJsonAsync($"api/gestion-tickets/{numero}/asignar", new { usuarioTI = "TEC001" }));
        Assert.Equal("DG", await EstadoAsync(colaborador, numero));

        // 3. TI pide información; el colaborador la ve, pero no la nota interna.
        const string pedido = "Indica el número de la orden de compra afectada.";
        await LeerAsync(await tecnico.PostAsJsonAsync($"api/gestion-tickets/{numero}/solicitar-informacion", new { mensaje = pedido }));
        var notaInterna = $"Nota interna de prueba {Guid.NewGuid():N}";
        await SembrarNotaInternaAsync(numero, notaInterna);

        var vistaColaborador = await colaborador.GetStringAsync($"api/mis-tickets/{numero}");
        Assert.Contains(pedido, vistaColaborador);
        Assert.DoesNotContain(notaInterna, vistaColaborador);
        Assert.Equal("RC", JsonDocument.Parse(vistaColaborador).RootElement.GetProperty("estado").GetString());
        var vistaTI = await tecnico.GetFromJsonAsync<JsonElement>($"api/gestion-tickets/{numero}");
        Assert.Contains(vistaTI.GetProperty("mensajes").EnumerateArray(),
            x => x.GetProperty("contenido").GetString() == notaInterna && x.GetProperty("esInterno").GetBoolean());

        // 4. El colaborador responde y TI resuelve.
        using var respuesta = new MultipartFormDataContent { { new StringContent("La orden afectada es la OC-2026-000123."), "Contenido" } };
        await LeerAsync(await colaborador.PostAsync($"api/mis-tickets/{numero}/responder-observacion", respuesta));
        Assert.NotEqual("RC", await EstadoAsync(colaborador, numero));
        await LeerAsync(await tecnico.PostAsJsonAsync($"api/gestion-tickets/{numero}/resolver", new
        {
            causaRaiz = "La orden tenía un proveedor inactivo.",
            solucion = "Se reactivó el proveedor y se reprocesó la orden.",
            respuestaUsuario = "Ya puedes grabar la orden de compra.",
            tipoResolucion = "CORRECCION"
        }));
        Assert.Equal("PV", await EstadoAsync(colaborador, numero));

        // 5. El colaborador valida y califica: el ticket queda resuelto.
        await LeerAsync(await colaborador.PostAsJsonAsync($"api/mis-tickets/{numero}/validar-solucion", new { solucionada = true, comentario = "Funciona." }));
        await LeerAsync(await colaborador.PostAsJsonAsync($"api/mis-tickets/{numero}/calificar", new { calificacion = 5, comentario = "Rápido." }));
        var final = await tecnico.GetFromJsonAsync<JsonElement>($"api/gestion-tickets/{numero}");
        Assert.Equal("RS", final.GetProperty("estado").GetString());
        var historial = final.GetProperty("historialEstados").EnumerateArray().Select(x => x.GetProperty("estado").GetString()).ToList();
        Assert.Equal(new[] { "NV", "DG", "RC", "PV", "RS" }, historial.Where(x => x is "NV" or "DG" or "RC" or "PV" or "RS").Distinct());
    }

    private static async Task<JsonElement> LeerAsync(HttpResponseMessage respuesta, HttpStatusCode? esperado = null)
    {
        using (respuesta)
        {
            var cuerpo = await respuesta.Content.ReadAsStringAsync();
            Assert.True(esperado is null ? respuesta.IsSuccessStatusCode : respuesta.StatusCode == esperado,
                $"{respuesta.RequestMessage?.Method} {respuesta.RequestMessage?.RequestUri}: {(int)respuesta.StatusCode} {cuerpo}");
            return cuerpo.Length == 0 ? default : JsonDocument.Parse(cuerpo).RootElement.Clone();
        }
    }

    private static async Task<string?> EstadoAsync(HttpClient colaborador, string numero) =>
        (await colaborador.GetFromJsonAsync<JsonElement>($"api/mis-tickets/{numero}")).GetProperty("estado").GetString();

    private string CadenaAdministracion => Environment.GetEnvironmentVariable("PRUEBAS_CNN_ADMIN")
        ?? fabrica.Services.GetRequiredService<IConfiguration>().GetConnectionString("CnnSistemaTickets")!;

    /// <summary>
    /// Una combinación que Usp_TI_Clasificar_Ticket acepta (matriz Item/Categoría completa) para un tipo sin ficha obligatoria. Se prefiere INC;
    /// tras la sincronización con el legado la matriz completa puede estar solo en los tipos 001 a 003.
    /// </summary>
    private async Task<Clasificacion> ClasificacionValidaAsync()
    {
        await using var conexion = new SqlConnection(CadenaAdministracion);
        await conexion.OpenAsync();
        await using var comando = new SqlCommand("""
            Select Top (1) i.Linea, i.Item, s.Tipo, s.SubTipo, s.Categoria
            From dbo.TI_ItemCategoria ic
            Join dbo.TI_Item i on i.Item = ic.Item and i.Estado = 'A'
            Join dbo.TI_Linea l on l.Linea = i.Linea and l.Estado = 'A'
            Join dbo.TI_SubTipo s on s.Categoria = ic.Categoria and s.Estado = 'A'
            Join dbo.TI_Tipo t on t.Tipo = s.Tipo and t.Estado = 'A'
            Where ic.Estado = 'A' and ic.Prioridad Is Not Null and ic.Impacto Is Not Null and ic.Complejidad Is Not Null
                and Not Exists (Select 1 From dbo.TI_PlantillaCampo pc Where pc.Tipo = s.Tipo and pc.Obligatorio = 1 and pc.Estado = 'A')
            Order By Case When s.Tipo = 'INC' Then 0 Else 1 End, s.Tipo, i.Linea, i.Item, s.SubTipo
            """, conexion);
        await using var lector = await comando.ExecuteReaderAsync();
        Assert.True(await lector.ReadAsync(), "La base no tiene una combinación clasificable sin ficha obligatoria.");
        return new Clasificacion(lector.GetString(0), lector.GetString(1), lector.GetString(2), lector.GetString(3), lector.GetString(4));
    }

    private async Task SembrarNotaInternaAsync(string numero, string contenido)
    {
        await using var conexion = new SqlConnection(CadenaAdministracion);
        await conexion.OpenAsync();
        await using var comando = new SqlCommand("""
            Declare @nSecuencia int = (Select IsNull(Max(Secuencia), 0) + 1 From dbo.TI_IncidenciaMensaje Where IncidenciaNumero = @cIncidenciaNumero)
            Insert dbo.TI_IncidenciaMensaje (IncidenciaNumero, Secuencia, UsuarioAutor, TipoAutor, Contenido, FechaMensaje, EsInterno)
            Values (@cIncidenciaNumero, @nSecuencia, 'TEC001', 'T', @cContenido, SysDateTime(), 1)
            """, conexion);
        comando.Parameters.Add("@cIncidenciaNumero", System.Data.SqlDbType.VarChar, 12).Value = numero;
        comando.Parameters.Add("@cContenido", System.Data.SqlDbType.NVarChar, 2000).Value = contenido;
        await comando.ExecuteNonQueryAsync();
    }
}
