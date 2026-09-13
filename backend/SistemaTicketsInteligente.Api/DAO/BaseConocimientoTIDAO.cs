/**
 * Archivo: BaseConocimientoTIDAO.cs
 * Objetivo: Ejecutar los Stored Procedures del módulo Base de Conocimiento para operadores TI.
 * Responsabilidad: Consultar resumen, artículos, catálogos y detalle; además persistir creación, edición, validación e inactivación sin aplicar reglas funcionales.
 * Dependencias: ConexionSqlServer, Microsoft.Data.SqlClient, BaseConocimientoTIDTO y procedimientos dbo.Usp_TI_* de Base de Conocimiento.
 * Flujo: BaseConocimientoTIBLL -> BaseConocimientoTIDAO -> Stored Procedures -> SQL Server.
 * Consideraciones: No contiene SQL ad-hoc; todas las operaciones utilizan parámetros tipados y los errores funcionales 502xx se propagan como InvalidOperationException.
 */

using System.Data;
using Microsoft.Data.SqlClient;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.DAO;

public sealed class BaseConocimientoTIDAO
{
    private readonly ConexionSqlServer conexionSqlServer;

    public BaseConocimientoTIDAO(ConexionSqlServer conexionSqlServer)
    {
        this.conexionSqlServer = conexionSqlServer;
    }

    public async Task<BaseConocimientoTIRespuesta> ObtenerAsync(CancellationToken cancellationToken = default)
    {
        var respuesta = new BaseConocimientoTIRespuesta();

        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Obtener_BaseConocimientoTI", conexion) { CommandType = CommandType.StoredProcedure };
        await conexion.OpenAsync(cancellationToken);
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

        if (await lector.ReadAsync(cancellationToken))
        {
            respuesta.Resumen = new BaseConocimientoTIResumen
            {
                Total = LeerEntero(lector, "Total"),
                Activos = LeerEntero(lector, "Activos"),
                Borradores = LeerEntero(lector, "Borradores"),
                PendientesValidacion = LeerEntero(lector, "PendientesValidacion"),
                PorRevisar = LeerEntero(lector, "PorRevisar"),
                CandidatosDesdeTickets = LeerEntero(lector, "CandidatosDesdeTickets")
            };
        }

        await lector.NextResultAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken)) respuesta.Articulos.Add(MapearArticulo(lector));

        await lector.NextResultAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken)) respuesta.Catalogos.Lineas.Add(MapearCatalogo(lector));

        await lector.NextResultAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            respuesta.Catalogos.Items.Add(new BaseConocimientoTIItemCatalogo
            {
                Codigo = LeerCadena(lector, "Codigo"),
                Linea = LeerCadena(lector, "Linea"),
                Descripcion = LeerCadena(lector, "Descripcion")
            });
        }

        await lector.NextResultAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken)) respuesta.Catalogos.Tipos.Add(MapearCatalogo(lector));

        await lector.NextResultAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken)) respuesta.Catalogos.Categorias.Add(MapearCatalogo(lector));

        await lector.NextResultAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            respuesta.Catalogos.SubTipos.Add(new BaseConocimientoTISubTipoCatalogo
            {
                Codigo = LeerCadena(lector, "Codigo"),
                Tipo = LeerCadena(lector, "Tipo"),
                Categoria = LeerCadena(lector, "Categoria"),
                Descripcion = LeerCadena(lector, "Descripcion")
            });
        }

        await lector.NextResultAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            respuesta.Catalogos.TicketsOrigen.Add(new BaseConocimientoTITicketOrigen
            {
                IncidenciaNumero = LeerCadena(lector, "IncidenciaNumero"),
                Titulo = LeerCadena(lector, "Titulo"),
                Detalle = LeerCadena(lector, "Detalle"),
                MensajeError = LeerCadena(lector, "MensajeError"),
                Linea = LeerCadena(lector, "Linea"),
                Item = LeerCadena(lector, "Item"),
                Tipo = LeerCadena(lector, "Tipo"),
                SubTipo = LeerCadena(lector, "SubTipo"),
                Categoria = LeerCadena(lector, "Categoria"),
                CausaRaiz = LeerCadena(lector, "CausaRaiz"),
                SolucionTecnica = LeerCadena(lector, "SolucionTecnica"),
                FechaCierre = LeerFechaNullable(lector, "FechaCierre")
            });
        }

        return respuesta;
    }

    public async Task<BaseConocimientoTIDetalle?> ObtenerDetalleAsync(string codigo, CancellationToken cancellationToken = default)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Obtener_DetalleBaseConocimientoTI", conexion) { CommandType = CommandType.StoredProcedure };
        comando.Parameters.Add("@cConocimientoCodigo", SqlDbType.VarChar, 20).Value = codigo;

        await conexion.OpenAsync(cancellationToken);
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
        if (!await lector.ReadAsync(cancellationToken)) return null;

        return new BaseConocimientoTIDetalle
        {
            ConocimientoCodigo = LeerCadena(lector, "ConocimientoCodigo"),
            Titulo = LeerCadena(lector, "Titulo"),
            Problema = LeerCadena(lector, "Problema"),
            Sintomas = LeerCadena(lector, "Sintomas"),
            MensajeError = LeerCadena(lector, "MensajeError"),
            Causa = LeerCadena(lector, "Causa"),
            Solucion = LeerCadena(lector, "Solucion"),
            Procedimiento = LeerCadena(lector, "Procedimiento"),
            Linea = LeerCadena(lector, "Linea"),
            LineaDescripcion = LeerCadena(lector, "LineaDescripcion"),
            Item = LeerCadena(lector, "Item"),
            ItemDescripcion = LeerCadena(lector, "ItemDescripcion"),
            Tipo = LeerCadena(lector, "Tipo"),
            TipoDescripcion = LeerCadena(lector, "TipoDescripcion"),
            SubTipo = LeerCadena(lector, "SubTipo"),
            SubTipoDescripcion = LeerCadena(lector, "SubTipoDescripcion"),
            Categoria = LeerCadena(lector, "Categoria"),
            CategoriaDescripcion = LeerCadena(lector, "CategoriaDescripcion"),
            IncidenciaOrigen = LeerCadena(lector, "IncidenciaOrigen"),
            Estado = LeerCadena(lector, "Estado"),
            EstadoDescripcion = LeerCadena(lector, "EstadoDescripcion"),
            UsuarioValida = LeerCadena(lector, "UsuarioValida"),
            Validador = LeerCadena(lector, "Validador"),
            FechaCreacion = LeerFecha(lector, "FechaCreacion"),
            FechaValidacion = LeerFechaNullable(lector, "FechaValidacion"),
            FechaRevision = LeerFechaNullable(lector, "FechaRevision"),
            RequiereRevision = LeerBooleano(lector, "RequiereRevision")
        };
    }

    public async Task<string> CrearAsync(string usuario, GuardarBaseConocimientoTISolicitud solicitud, Guid idCorrelacion, CancellationToken cancellationToken = default)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = CrearComandoGuardar("dbo.Usp_TI_Crear_BaseConocimientoTI", conexion, usuario, solicitud, idCorrelacion);
        await conexion.OpenAsync(cancellationToken);

        try
        {
            var resultado = await comando.ExecuteScalarAsync(cancellationToken);
            return resultado?.ToString()?.Trim() ?? string.Empty;
        }
        catch (SqlException ex) when (ex.Number is >= 50200 and <= 50299)
        {
            throw new InvalidOperationException(ex.Message, ex);
        }
    }

    public async Task ActualizarAsync(string usuario, string codigo, GuardarBaseConocimientoTISolicitud solicitud, Guid idCorrelacion, CancellationToken cancellationToken = default)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = CrearComandoGuardar("dbo.Usp_TI_Actualizar_BaseConocimientoTI", conexion, usuario, solicitud, idCorrelacion);
        comando.Parameters.Add("@cConocimientoCodigo", SqlDbType.VarChar, 20).Value = codigo;
        await EjecutarAsync(conexion, comando, cancellationToken);
    }

    public Task EnviarValidacionAsync(string usuario, string codigo, Guid idCorrelacion, CancellationToken cancellationToken = default) =>
        EjecutarEstadoAsync("dbo.Usp_TI_EnviarValidacion_BaseConocimientoTI", usuario, codigo, idCorrelacion, cancellationToken);

    public Task ValidarAsync(string usuario, string codigo, Guid idCorrelacion, CancellationToken cancellationToken = default) =>
        EjecutarEstadoAsync("dbo.Usp_TI_Validar_BaseConocimientoTI", usuario, codigo, idCorrelacion, cancellationToken);

    public Task InactivarAsync(string usuario, string codigo, Guid idCorrelacion, CancellationToken cancellationToken = default) =>
        EjecutarEstadoAsync("dbo.Usp_TI_Inactivar_BaseConocimientoTI", usuario, codigo, idCorrelacion, cancellationToken);

    private async Task EjecutarEstadoAsync(string procedimiento, string usuario, string codigo, Guid idCorrelacion, CancellationToken cancellationToken)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand(procedimiento, conexion) { CommandType = CommandType.StoredProcedure };
        comando.Parameters.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;
        comando.Parameters.Add("@cConocimientoCodigo", SqlDbType.VarChar, 20).Value = codigo;
        comando.Parameters.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = idCorrelacion;
        await EjecutarAsync(conexion, comando, cancellationToken);
    }

    private static SqlCommand CrearComandoGuardar(string procedimiento, SqlConnection conexion, string usuario, GuardarBaseConocimientoTISolicitud solicitud, Guid idCorrelacion)
    {
        var comando = new SqlCommand(procedimiento, conexion) { CommandType = CommandType.StoredProcedure };
        comando.Parameters.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;
        comando.Parameters.Add("@cTitulo", SqlDbType.NVarChar, 250).Value = solicitud.Titulo;
        comando.Parameters.Add("@cProblema", SqlDbType.NVarChar, -1).Value = solicitud.Problema;
        comando.Parameters.Add("@cSintomas", SqlDbType.NVarChar, -1).Value = solicitud.Sintomas;
        comando.Parameters.Add("@cMensajeError", SqlDbType.NVarChar, 1000).Value = string.IsNullOrWhiteSpace(solicitud.MensajeError) ? DBNull.Value : solicitud.MensajeError;
        comando.Parameters.Add("@cCausa", SqlDbType.NVarChar, -1).Value = solicitud.Causa;
        comando.Parameters.Add("@cSolucion", SqlDbType.NVarChar, -1).Value = solicitud.Solucion;
        comando.Parameters.Add("@cProcedimiento", SqlDbType.NVarChar, -1).Value = string.IsNullOrWhiteSpace(solicitud.Procedimiento) ? DBNull.Value : solicitud.Procedimiento;
        comando.Parameters.Add("@cLinea", SqlDbType.Char, 3).Value = solicitud.Linea;
        comando.Parameters.Add("@cItem", SqlDbType.VarChar, 20).Value = solicitud.Item;
        comando.Parameters.Add("@cTipo", SqlDbType.Char, 3).Value = solicitud.Tipo;
        comando.Parameters.Add("@cSubTipo", SqlDbType.Char, 3).Value = solicitud.SubTipo;
        comando.Parameters.Add("@cCategoria", SqlDbType.VarChar, 20).Value = solicitud.Categoria;
        comando.Parameters.Add("@cIncidenciaOrigen", SqlDbType.VarChar, 12).Value = string.IsNullOrWhiteSpace(solicitud.IncidenciaOrigen) ? DBNull.Value : solicitud.IncidenciaOrigen;
        comando.Parameters.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = idCorrelacion;
        return comando;
    }

    private static async Task EjecutarAsync(SqlConnection conexion, SqlCommand comando, CancellationToken cancellationToken)
    {
        await conexion.OpenAsync(cancellationToken);
        try
        {
            await comando.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException ex) when (ex.Number is >= 50200 and <= 50299)
        {
            throw new InvalidOperationException(ex.Message, ex);
        }
    }

    private static BaseConocimientoTIItem MapearArticulo(SqlDataReader lector) => new()
    {
        ConocimientoCodigo = LeerCadena(lector, "ConocimientoCodigo"),
        Titulo = LeerCadena(lector, "Titulo"),
        Problema = LeerCadena(lector, "Problema"),
        Solucion = LeerCadena(lector, "Solucion"),
        Estado = LeerCadena(lector, "Estado"),
        EstadoDescripcion = LeerCadena(lector, "EstadoDescripcion"),
        Linea = LeerCadena(lector, "Linea"),
        LineaDescripcion = LeerCadena(lector, "LineaDescripcion"),
        Item = LeerCadena(lector, "Item"),
        ItemDescripcion = LeerCadena(lector, "ItemDescripcion"),
        Tipo = LeerCadena(lector, "Tipo"),
        TipoDescripcion = LeerCadena(lector, "TipoDescripcion"),
        SubTipo = LeerCadena(lector, "SubTipo"),
        SubTipoDescripcion = LeerCadena(lector, "SubTipoDescripcion"),
        Categoria = LeerCadena(lector, "Categoria"),
        CategoriaDescripcion = LeerCadena(lector, "CategoriaDescripcion"),
        IncidenciaOrigen = LeerCadena(lector, "IncidenciaOrigen"),
        Validador = LeerCadena(lector, "Validador"),
        FechaCreacion = LeerFecha(lector, "FechaCreacion"),
        FechaValidacion = LeerFechaNullable(lector, "FechaValidacion"),
        FechaRevision = LeerFechaNullable(lector, "FechaRevision"),
        RequiereRevision = LeerBooleano(lector, "RequiereRevision")
    };

    private static BaseConocimientoTICatalogo MapearCatalogo(SqlDataReader lector) => new()
    {
        Codigo = LeerCadena(lector, "Codigo"),
        Descripcion = LeerCadena(lector, "Descripcion")
    };

    private static string LeerCadena(SqlDataReader lector, string columna) => lector[columna] is DBNull ? string.Empty : lector[columna].ToString()?.Trim() ?? string.Empty;
    private static int LeerEntero(SqlDataReader lector, string columna) => lector[columna] is DBNull ? 0 : Convert.ToInt32(lector[columna]);
    private static bool LeerBooleano(SqlDataReader lector, string columna) => lector[columna] is not DBNull && Convert.ToBoolean(lector[columna]);
    private static DateTime LeerFecha(SqlDataReader lector, string columna) => Convert.ToDateTime(lector[columna]);
    private static DateTime? LeerFechaNullable(SqlDataReader lector, string columna) => lector[columna] is DBNull ? null : Convert.ToDateTime(lector[columna]);
}
