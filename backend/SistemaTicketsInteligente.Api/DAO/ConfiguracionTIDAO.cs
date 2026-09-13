/*
 * Archivo: ConfiguracionTIDAO.cs
 * Objetivo: Ejecutar los Stored Procedures del módulo restringido Configuración TI.
 * Responsabilidad: Consultar todos los catálogos configurables y persistir cambios explícitos de maestros, matriz, SLA, usuarios corporativos, formatos y visibilidad de conocimiento.
 * Dependencias: ConexionSqlServer, Microsoft.Data.SqlClient, ConfiguracionTIDTO e IdentidadCorporativaDTO.
 * Flujo: ConfiguracionTIBLL -> ConfiguracionTIDAO -> Stored Procedures -> SQL Server.
 * Consideraciones: No elimina maestros; utiliza parámetros tipados y convierte errores funcionales 50400-50499 en conflictos controlados.
 */

using System.Data;
using Microsoft.Data.SqlClient;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.DAO;

public sealed class ConfiguracionTIDAO
{
    private readonly ConexionSqlServer conexionSqlServer;

    public ConfiguracionTIDAO(ConexionSqlServer conexionSqlServer)
    {
        this.conexionSqlServer = conexionSqlServer;
    }

    public async Task<ConfiguracionTIRespuesta> ObtenerAsync(string usuario, CancellationToken cancellationToken = default)
    {
        var respuesta = new ConfiguracionTIRespuesta();
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Obtener_ConfiguracionTI", conexion) { CommandType = CommandType.StoredProcedure };
        comando.Parameters.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;
        await conexion.OpenAsync(cancellationToken);

        try
        {
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            while (await lector.ReadAsync(cancellationToken)) respuesta.Areas.Add(new ConfiguracionTIArea { Area = LeerCadena(lector, "Area"), Descripcion = LeerCadena(lector, "Descripcion"), Estado = LeerCadena(lector, "Estado"), Telefono = LeerCadena(lector, "Telefono") });
            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken)) respuesta.Lineas.Add(new ConfiguracionTILinea { Linea = LeerCadena(lector, "Linea"), Area = LeerCadena(lector, "Area"), Descripcion = LeerCadena(lector, "Descripcion"), Estado = LeerCadena(lector, "Estado") });
            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken)) respuesta.Items.Add(new ConfiguracionTIItem { Item = LeerCadena(lector, "Item"), Linea = LeerCadena(lector, "Linea"), Descripcion = LeerCadena(lector, "Descripcion"), Estado = LeerCadena(lector, "Estado") });
            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken)) respuesta.Tipos.Add(new ConfiguracionTITipo { Tipo = LeerCadena(lector, "Tipo"), Descripcion = LeerCadena(lector, "Descripcion"), Abreviatura = LeerCadena(lector, "Abreviatura"), Estado = LeerCadena(lector, "Estado") });
            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken)) respuesta.Categorias.Add(new ConfiguracionTICategoria { Categoria = LeerCadena(lector, "Categoria"), Descripcion = LeerCadena(lector, "Descripcion"), Abreviatura = LeerCadena(lector, "Abreviatura"), Estado = LeerCadena(lector, "Estado") });
            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken)) respuesta.SubTipos.Add(new ConfiguracionTISubTipo { Tipo = LeerCadena(lector, "Tipo"), SubTipo = LeerCadena(lector, "SubTipo"), Categoria = LeerCadena(lector, "Categoria"), Descripcion = LeerCadena(lector, "Descripcion"), Abreviatura = LeerCadena(lector, "Abreviatura"), Estado = LeerCadena(lector, "Estado") });
            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken)) respuesta.Matriz.Add(new ConfiguracionTIMatriz { Item = LeerCadena(lector, "Item"), Categoria = LeerCadena(lector, "Categoria"), Prioridad = LeerEnteroNullable(lector, "Prioridad"), Impacto = LeerEnteroNullable(lector, "Impacto"), Complejidad = LeerEnteroNullable(lector, "Complejidad"), Estado = LeerCadena(lector, "Estado") });
            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken)) respuesta.Sla.Add(new ConfiguracionTISla { Prioridad = LeerEntero(lector, "Prioridad"), SlaObjetivoMinutos = LeerEntero(lector, "SlaObjetivoMinutos"), Estado = LeerCadena(lector, "Estado") });
            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken)) respuesta.Usuarios.Add(new ConfiguracionTIUsuario { Usuario = LeerCadena(lector, "Usuario"), NombreCompleto = LeerCadena(lector, "NombreCompleto"), Area = LeerCadena(lector, "Area"), Cargo = LeerCadena(lector, "Cargo"), Perfil = LeerCadena(lector, "Perfil"), Correo = LeerCadena(lector, "Correo"), Estado = LeerCadena(lector, "Estado"), FuenteIdentidad = LeerCadena(lector, "FuenteIdentidad"), Documento = LeerCadena(lector, "Documento"), EstadoCorporativo = LeerCadena(lector, "EstadoCorporativo"), UltimaSincronizacion = LeerFechaNullable(lector, "UltimaSincronizacion") });
            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken)) respuesta.Formatos.Add(new ConfiguracionTIFormato { FormatoCodigo = LeerCadena(lector, "FormatoCodigo"), Titulo = LeerCadena(lector, "Titulo"), Descripcion = LeerCadena(lector, "Descripcion"), NombreOriginal = LeerCadena(lector, "NombreOriginal"), TipoMime = LeerCadena(lector, "TipoMime"), TipoTicket = LeerCadena(lector, "TipoTicket"), Estado = LeerCadena(lector, "Estado") });
            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken)) respuesta.Conocimientos.Add(new ConfiguracionTIConocimiento { ConocimientoCodigo = LeerCadena(lector, "ConocimientoCodigo"), Titulo = LeerCadena(lector, "Titulo"), Estado = LeerCadena(lector, "Estado"), VisibleUsuario = LeerBooleano(lector, "VisibleUsuario") });
        }
        catch (SqlException ex) when (EsErrorFuncional(ex))
        {
            throw new InvalidOperationException(ex.Message, ex);
        }

        return respuesta;
    }

    public Task GuardarAreaAsync(string usuario, GuardarAreaTISolicitud s, CancellationToken ct = default) => EjecutarAsync("dbo.Usp_TI_Guardar_Area", usuario, c =>
    {
        c.Parameters.Add("@cArea", SqlDbType.Char, 3).Value = s.Area;
        c.Parameters.Add("@cDescripcion", SqlDbType.VarChar, 60).Value = s.Descripcion;
        c.Parameters.Add("@cTelefono", SqlDbType.VarChar, 20).Value = ValorOpcional(s.Telefono);
        c.Parameters.Add("@cEstado", SqlDbType.VarChar, 2).Value = s.Estado;
    }, ct);

    public Task GuardarLineaAsync(string usuario, GuardarLineaTISolicitud s, CancellationToken ct = default) => EjecutarAsync("dbo.Usp_TI_Guardar_Linea", usuario, c =>
    {
        c.Parameters.Add("@cLinea", SqlDbType.Char, 3).Value = s.Linea; c.Parameters.Add("@cArea", SqlDbType.Char, 3).Value = s.Area; c.Parameters.Add("@cDescripcion", SqlDbType.VarChar, 60).Value = s.Descripcion; c.Parameters.Add("@cEstado", SqlDbType.VarChar, 2).Value = s.Estado;
    }, ct);

    public Task GuardarItemAsync(string usuario, GuardarItemTISolicitud s, CancellationToken ct = default) => EjecutarAsync("dbo.Usp_TI_Guardar_Item", usuario, c =>
    {
        c.Parameters.Add("@cItem", SqlDbType.VarChar, 20).Value = s.Item; c.Parameters.Add("@cLinea", SqlDbType.Char, 3).Value = s.Linea; c.Parameters.Add("@cDescripcion", SqlDbType.VarChar, 255).Value = s.Descripcion; c.Parameters.Add("@cEstado", SqlDbType.VarChar, 2).Value = s.Estado;
    }, ct);

    public Task GuardarTipoAsync(string usuario, GuardarTipoTISolicitud s, CancellationToken ct = default) => EjecutarAsync("dbo.Usp_TI_Guardar_Tipo", usuario, c =>
    {
        c.Parameters.Add("@cTipo", SqlDbType.Char, 3).Value = s.Tipo; c.Parameters.Add("@cDescripcion", SqlDbType.VarChar, 60).Value = s.Descripcion; c.Parameters.Add("@cAbreviatura", SqlDbType.VarChar, 60).Value = ValorOpcional(s.Abreviatura); c.Parameters.Add("@cEstado", SqlDbType.VarChar, 2).Value = s.Estado;
    }, ct);

    public Task GuardarCategoriaAsync(string usuario, GuardarCategoriaTISolicitud s, CancellationToken ct = default) => EjecutarAsync("dbo.Usp_TI_Guardar_Categoria", usuario, c =>
    {
        c.Parameters.Add("@cCategoria", SqlDbType.VarChar, 20).Value = s.Categoria; c.Parameters.Add("@cDescripcion", SqlDbType.VarChar, 60).Value = s.Descripcion; c.Parameters.Add("@cAbreviatura", SqlDbType.VarChar, 3).Value = ValorOpcional(s.Abreviatura); c.Parameters.Add("@cEstado", SqlDbType.VarChar, 2).Value = s.Estado;
    }, ct);

    public Task GuardarSubTipoAsync(string usuario, GuardarSubTipoTISolicitud s, CancellationToken ct = default) => EjecutarAsync("dbo.Usp_TI_Guardar_SubTipo", usuario, c =>
    {
        c.Parameters.Add("@cTipo", SqlDbType.Char, 3).Value = s.Tipo; c.Parameters.Add("@cSubTipo", SqlDbType.Char, 3).Value = s.SubTipo; c.Parameters.Add("@cCategoria", SqlDbType.VarChar, 20).Value = s.Categoria; c.Parameters.Add("@cDescripcion", SqlDbType.VarChar, 60).Value = s.Descripcion; c.Parameters.Add("@cAbreviatura", SqlDbType.VarChar, 60).Value = ValorOpcional(s.Abreviatura); c.Parameters.Add("@cEstado", SqlDbType.VarChar, 2).Value = s.Estado;
    }, ct);

    public Task GuardarMatrizAsync(string usuario, GuardarMatrizTISolicitud s, CancellationToken ct = default) => EjecutarAsync("dbo.Usp_TI_Guardar_MatrizClasificacion", usuario, c =>
    {
        c.Parameters.Add("@cItem", SqlDbType.VarChar, 20).Value = s.Item; c.Parameters.Add("@cCategoria", SqlDbType.VarChar, 20).Value = s.Categoria; c.Parameters.Add("@nPrioridad", SqlDbType.Int).Value = s.Prioridad; c.Parameters.Add("@nImpacto", SqlDbType.Int).Value = s.Impacto; c.Parameters.Add("@nComplejidad", SqlDbType.Int).Value = s.Complejidad; c.Parameters.Add("@cEstado", SqlDbType.VarChar, 2).Value = s.Estado;
    }, ct);

    public Task GuardarSlaAsync(string usuario, GuardarSlaTISolicitud s, CancellationToken ct = default) => EjecutarAsync("dbo.Usp_TI_Guardar_ParametroSLA", usuario, c =>
    {
        c.Parameters.Add("@nPrioridad", SqlDbType.TinyInt).Value = s.Prioridad; c.Parameters.Add("@nSlaObjetivoMinutos", SqlDbType.Int).Value = s.SlaObjetivoMinutos; c.Parameters.Add("@cEstado", SqlDbType.VarChar, 2).Value = s.Estado;
    }, ct);

    public Task RegistrarUsuarioCorporativoAsync(string usuarioAdmin, UsuarioCorporativo corporativo, SincronizarUsuarioCorporativoSolicitud s, CancellationToken ct = default) => EjecutarAsync("dbo.Usp_TI_Registrar_UsuarioCorporativo", usuarioAdmin, c =>
    {
        var cargo = corporativo.Cargo.Trim().ToUpperInvariant();
        if (cargo.Length > 3) cargo = string.Empty;
        var estadoCorporativo = string.Join('/', new[] { corporativo.Estado, corporativo.EstadoEmpleado }.Where(x => !string.IsNullOrWhiteSpace(x)));

        c.Parameters.Add("@cUsuario", SqlDbType.VarChar, 20).Value = corporativo.Usuario;
        c.Parameters.Add("@cNombreCompleto", SqlDbType.VarChar, 255).Value = corporativo.NombreCompleto;
        c.Parameters.Add("@cCargo", SqlDbType.Char, 3).Value = string.IsNullOrWhiteSpace(cargo) ? DBNull.Value : cargo;
        c.Parameters.Add("@cCargoDescripcion", SqlDbType.VarChar, 60).Value = string.IsNullOrWhiteSpace(cargo) ? DBNull.Value : cargo;
        c.Parameters.Add("@cDocumento", SqlDbType.VarChar, 20).Value = ValorOpcional(corporativo.Documento);
        c.Parameters.Add("@cEstadoCorporativo", SqlDbType.VarChar, 20).Value = string.IsNullOrWhiteSpace(estadoCorporativo) ? DBNull.Value : estadoCorporativo[..Math.Min(estadoCorporativo.Length, 20)];
        c.Parameters.Add("@cArea", SqlDbType.Char, 3).Value = s.Area;
        c.Parameters.Add("@cPerfil", SqlDbType.Char, 3).Value = s.Perfil;
        c.Parameters.Add("@cCorreo", SqlDbType.VarChar, 100).Value = ValorOpcional(string.IsNullOrWhiteSpace(s.Correo) ? corporativo.Correo : s.Correo);
        c.Parameters.Add("@cEstado", SqlDbType.VarChar, 2).Value = s.Estado;
    }, ct, parametroUsuario: "@cUsuarioAdmin");

    public Task SincronizarCargoAsync(string usuarioAdmin, CargoCorporativo cargo, CancellationToken ct = default) => EjecutarAsync("dbo.Usp_TI_Sincronizar_CargoCorporativo", usuarioAdmin, c =>
    {
        c.Parameters.Add("@cCargo", SqlDbType.Char, 3).Value = cargo.Cargo; c.Parameters.Add("@cDescripcion", SqlDbType.VarChar, 60).Value = cargo.Descripcion;
    }, ct, parametroUsuario: "@cUsuarioAdmin");

    public Task GuardarFormatoAsync(string usuario, string codigo, string titulo, string? descripcion, string nombreOriginal, string rutaArchivo, string tipoMime, string? tipoTicket, string estado, CancellationToken ct = default) => EjecutarAsync("dbo.Usp_TI_Guardar_FormatoSoporte", usuario, c =>
    {
        c.Parameters.Add("@cFormatoCodigo", SqlDbType.VarChar, 20).Value = codigo; c.Parameters.Add("@cTitulo", SqlDbType.NVarChar, 120).Value = titulo; c.Parameters.Add("@cDescripcion", SqlDbType.NVarChar, 500).Value = ValorOpcional(descripcion); c.Parameters.Add("@cNombreOriginal", SqlDbType.NVarChar, 260).Value = nombreOriginal; c.Parameters.Add("@cRutaArchivo", SqlDbType.NVarChar, 1000).Value = rutaArchivo; c.Parameters.Add("@cTipoMime", SqlDbType.VarChar, 100).Value = tipoMime; c.Parameters.Add("@cTipoTicket", SqlDbType.Char, 3).Value = ValorOpcional(tipoTicket); c.Parameters.Add("@cEstado", SqlDbType.VarChar, 2).Value = estado;
    }, ct);

    public Task ActualizarVisibilidadConocimientoAsync(string usuario, string conocimientoCodigo, bool visibleUsuario, CancellationToken ct = default) => EjecutarAsync("dbo.Usp_TI_Actualizar_VisibilidadConocimiento", usuario, c =>
    {
        c.Parameters.Add("@cConocimientoCodigo", SqlDbType.VarChar, 20).Value = conocimientoCodigo; c.Parameters.Add("@lVisibleUsuario", SqlDbType.Bit).Value = visibleUsuario;
    }, ct);

    private async Task EjecutarAsync(string procedimiento, string usuario, Action<SqlCommand> configurar, CancellationToken ct, string parametroUsuario = "@cUsuario")
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand(procedimiento, conexion) { CommandType = CommandType.StoredProcedure };
        comando.Parameters.Add(parametroUsuario, SqlDbType.VarChar, 20).Value = usuario;
        configurar(comando);
        await conexion.OpenAsync(ct);

        try
        {
            await comando.ExecuteNonQueryAsync(ct);
        }
        catch (SqlException ex) when (EsErrorFuncional(ex))
        {
            throw new InvalidOperationException(ex.Message, ex);
        }
    }

    private static object ValorOpcional(string? valor) => string.IsNullOrWhiteSpace(valor) ? DBNull.Value : valor.Trim();
    private static bool EsErrorFuncional(SqlException ex) => ex.Number is >= 50400 and <= 50499;
    private static string LeerCadena(SqlDataReader lector, string columna) => lector[columna] is DBNull ? string.Empty : lector[columna].ToString()?.Trim() ?? string.Empty;
    private static int LeerEntero(SqlDataReader lector, string columna) => lector[columna] is DBNull ? 0 : Convert.ToInt32(lector[columna]);
    private static int? LeerEnteroNullable(SqlDataReader lector, string columna) => lector[columna] is DBNull ? null : Convert.ToInt32(lector[columna]);
    private static bool LeerBooleano(SqlDataReader lector, string columna) => lector[columna] is not DBNull && Convert.ToBoolean(lector[columna]);
    private static DateTime? LeerFechaNullable(SqlDataReader lector, string columna) => lector[columna] is DBNull ? null : Convert.ToDateTime(lector[columna]);
}
