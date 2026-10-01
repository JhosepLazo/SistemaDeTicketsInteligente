/*
 * Archivo: ReportesTIDAO.cs
 * Objetivo: Ejecutar el Stored Procedure que alimenta el módulo Reportes para operadores TI.
 * Responsabilidad: Enviar filtros tipados, leer los múltiples conjuntos de resultados y mapearlos a DTO sin incorporar reglas funcionales.
 * Dependencias: ConexionSqlServer, Microsoft.Data.SqlClient, ReportesTIDTO y dbo.Usp_TI_Obtener_ReportesTI.
 * Flujo: ReportesTIBLL -> ReportesTIDAO -> Stored Procedure -> SQL Server.
 * Consideraciones: Una sola ejecución retorna todo el tablero para mantener consistencia entre indicadores, gráficos, tablas y exportación.
 */

using System.Data;
using Microsoft.Data.SqlClient;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.DAO;

public sealed class ReportesTIDAO
{
    private readonly ConexionSqlServer conexionSqlServer;

    public ReportesTIDAO(ConexionSqlServer conexionSqlServer)
    {
        this.conexionSqlServer = conexionSqlServer;
    }

    public async Task<ReportesTIRespuesta> ObtenerAsync(ReportesTIFiltros filtros, CancellationToken cancellationToken = default)
    {
        var respuesta = new ReportesTIRespuesta();

        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Obtener_ReportesTI", conexion) { CommandType = CommandType.StoredProcedure };
        comando.Parameters.Add("@dFechaInicio", SqlDbType.Date).Value = filtros.FechaInicio.Date;
        comando.Parameters.Add("@dFechaFin", SqlDbType.Date).Value = filtros.FechaFin.Date;
        comando.Parameters.Add("@cArea", SqlDbType.Char, 3).Value = ValorDb(filtros.Area);
        comando.Parameters.Add("@cEstado", SqlDbType.Char, 2).Value = ValorDb(filtros.Estado);
        comando.Parameters.Add("@cPrioridad", SqlDbType.VarChar, 10).Value = ValorDb(filtros.Prioridad);
        comando.Parameters.Add("@cTipo", SqlDbType.Char, 3).Value = ValorDb(filtros.Tipo);
        comando.Parameters.Add("@cUsuarioTI", SqlDbType.VarChar, 20).Value = ValorDb(filtros.UsuarioTI);

        await conexion.OpenAsync(cancellationToken);

        try
        {
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            if (await lector.ReadAsync(cancellationToken))
            {
                respuesta.Resumen = new ReportesTIResumen
                {
                    Total = LeerEntero(lector, "Total"),
                    Resueltos = LeerEntero(lector, "Resueltos"),
                    Reabiertos = LeerEntero(lector, "Reabiertos"),
                    TiempoPromedioHoras = LeerDecimalNullable(lector, "TiempoPromedioHoras"),
                    Satisfaccion = LeerDecimalNullable(lector, "Satisfaccion"),
                    CumplimientoSla = LeerDecimalNullable(lector, "CumplimientoSla"),
                    TotalAnterior = LeerEntero(lector, "TotalAnterior"),
                    ResueltosAnterior = LeerEntero(lector, "ResueltosAnterior"),
                    TiempoPromedioHorasAnterior = LeerDecimalNullable(lector, "TiempoPromedioHorasAnterior"),
                    SatisfaccionAnterior = LeerDecimalNullable(lector, "SatisfaccionAnterior")
                };
            }

            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken))
            {
                respuesta.Evolucion.Add(new ReportesTIEvolucion
                {
                    Fecha = LeerFecha(lector, "Fecha"),
                    Cantidad = LeerEntero(lector, "Cantidad")
                });
            }

            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken))
            {
                respuesta.Estados.Add(new ReportesTIEstado
                {
                    Estado = LeerCadena(lector, "Estado"),
                    EstadoDescripcion = LeerCadena(lector, "EstadoDescripcion"),
                    Cantidad = LeerEntero(lector, "Cantidad")
                });
            }

            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken))
            {
                respuesta.Areas.Add(new ReportesTIArea
                {
                    Area = LeerCadena(lector, "Area"),
                    AreaDescripcion = LeerCadena(lector, "AreaDescripcion"),
                    Cantidad = LeerEntero(lector, "Cantidad")
                });
            }

            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken))
            {
                respuesta.Avances.Add(new ReportesTIAvance
                {
                    IncidenciaNumero = LeerCadena(lector, "IncidenciaNumero"),
                    Titulo = LeerCadena(lector, "Titulo"),
                    AreaDescripcion = LeerCadena(lector, "AreaDescripcion"),
                    Estado = LeerCadena(lector, "Estado"),
                    EstadoDescripcion = LeerCadena(lector, "EstadoDescripcion"),
                    Responsable = LeerCadena(lector, "Responsable"),
                    PorcentajeAvance = LeerDecimalNullable(lector, "PorcentajeAvance"),
                    FechaUltimoAvance = LeerFechaNullable(lector, "FechaUltimoAvance"),
                    AvancesRegistrados = LeerEntero(lector, "AvancesRegistrados"),
                    MinutosRegistrados = LeerDecimalNullable(lector, "MinutosRegistrados") ?? 0
                });
            }

            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken))
            {
                respuesta.AvancePorUsuario.Add(new ReportesTIUsuario
                {
                    Usuario = LeerCadena(lector, "Usuario"),
                    Responsable = LeerCadena(lector, "Responsable"),
                    TicketsAsignados = LeerEntero(lector, "TicketsAsignados"),
                    TicketsResueltos = LeerEntero(lector, "TicketsResueltos"),
                    TicketsEnCurso = LeerEntero(lector, "TicketsEnCurso"),
                    AvancesRegistrados = LeerEntero(lector, "AvancesRegistrados"),
                    MinutosRegistrados = LeerDecimalNullable(lector, "MinutosRegistrados") ?? 0,
                    PorcentajePromedio = LeerDecimalNullable(lector, "PorcentajePromedio")
                });
            }

            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken))
            {
                respuesta.TiemposPorPrioridad.Add(new ReportesTIPrioridadTiempo
                {
                    Prioridad = LeerCadena(lector, "Prioridad"),
                    Tickets = LeerEntero(lector, "Tickets"),
                    TiempoPromedioHoras = LeerDecimalNullable(lector, "TiempoPromedioHoras"),
                    TiempoMasRapidoHoras = LeerDecimalNullable(lector, "TiempoMasRapidoHoras"),
                    TiempoMasLargoHoras = LeerDecimalNullable(lector, "TiempoMasLargoHoras")
                });
            }

            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken))
            {
                respuesta.TicketsPrioridadAlta.Add(new ReportesTITicketDestacado
                {
                    IncidenciaNumero = LeerCadena(lector, "IncidenciaNumero"),
                    Titulo = LeerCadena(lector, "Titulo"),
                    AreaDescripcion = LeerCadena(lector, "AreaDescripcion"),
                    Estado = LeerCadena(lector, "Estado"),
                    EstadoDescripcion = LeerCadena(lector, "EstadoDescripcion"),
                    Prioridad = LeerEnteroNullable(lector, "Prioridad"),
                    Responsable = LeerCadena(lector, "Responsable"),
                    FechaRegistro = LeerFecha(lector, "FechaRegistro"),
                    TiempoAbiertoHoras = LeerEntero(lector, "TiempoAbiertoHoras")
                });
            }

            await lector.NextResultAsync(cancellationToken);
            await CargarCatalogoAsync(lector, respuesta.Catalogos.Areas, cancellationToken);
            await lector.NextResultAsync(cancellationToken);
            await CargarCatalogoAsync(lector, respuesta.Catalogos.Estados, cancellationToken);
            await lector.NextResultAsync(cancellationToken);
            await CargarCatalogoAsync(lector, respuesta.Catalogos.Tipos, cancellationToken);
            await lector.NextResultAsync(cancellationToken);
            await CargarCatalogoAsync(lector, respuesta.Catalogos.Operadores, cancellationToken);

            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken))
            {
                respuesta.DetalleExportacion.Add(new ReportesTIDetalleExportacion
                {
                    IncidenciaNumero = LeerCadena(lector, "IncidenciaNumero"),
                    Solicitante = LeerCadena(lector, "Solicitante"),
                    Titulo = LeerCadena(lector, "Titulo"),
                    AreaDescripcion = LeerCadena(lector, "AreaDescripcion"),
                    TipoDescripcion = LeerCadena(lector, "TipoDescripcion"),
                    EstadoDescripcion = LeerCadena(lector, "EstadoDescripcion"),
                    Prioridad = LeerEnteroNullable(lector, "Prioridad"),
                    Responsable = LeerCadena(lector, "Responsable"),
                    FechaRegistro = LeerFecha(lector, "FechaRegistro"),
                    FechaCierre = LeerFechaNullable(lector, "FechaCierre"),
                    Calificacion = LeerByteNullable(lector, "Calificacion")
                });
            }

            await lector.DisposeAsync();
            await CargarEsfuerzoAsync(conexion, filtros, respuesta.Resumen, cancellationToken);
            return respuesta;
        }
        catch (SqlException ex) when (ex.Number is >= 50300 and <= 50399)
        {
            throw new InvalidOperationException(ex.Message, ex);
        }
    }

    private static async Task CargarEsfuerzoAsync(SqlConnection conexion, ReportesTIFiltros filtros, ReportesTIResumen resumen, CancellationToken cancellationToken)
    {
        await using var comando = new SqlCommand("dbo.Usp_TI_Obtener_EsfuerzoOperativoTI", conexion) { CommandType = CommandType.StoredProcedure };
        comando.Parameters.Add("@dFechaInicio", SqlDbType.Date).Value = filtros.FechaInicio.Date;
        comando.Parameters.Add("@dFechaFin", SqlDbType.Date).Value = filtros.FechaFin.Date;
        comando.Parameters.Add("@cArea", SqlDbType.Char, 3).Value = ValorDb(filtros.Area);
        comando.Parameters.Add("@cEstado", SqlDbType.Char, 2).Value = ValorDb(filtros.Estado);
        comando.Parameters.Add("@cPrioridad", SqlDbType.VarChar, 10).Value = ValorDb(filtros.Prioridad);
        comando.Parameters.Add("@cTipo", SqlDbType.Char, 3).Value = ValorDb(filtros.Tipo);
        comando.Parameters.Add("@cUsuarioTI", SqlDbType.VarChar, 20).Value = ValorDb(filtros.UsuarioTI);

        try
        {
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
            if (!await lector.ReadAsync(cancellationToken)) return;

            resumen.HorasEfectivas = LeerDecimalNullable(lector, "HorasEfectivas") ?? 0;
            resumen.TicketsConEsfuerzo = LeerEntero(lector, "TicketsConEsfuerzo");
        }
        catch (SqlException ex) when (ex.Number is 50450 or 50451)
        {
            throw new InvalidOperationException(ex.Message, ex);
        }
    }

    private static object ValorDb(string? valor) => string.IsNullOrWhiteSpace(valor) ? DBNull.Value : valor.Trim();

    private static async Task CargarCatalogoAsync(SqlDataReader lector, ICollection<ReportesTICatalogo> destino, CancellationToken cancellationToken)
    {
        while (await lector.ReadAsync(cancellationToken))
        {
            destino.Add(new ReportesTICatalogo
            {
                Codigo = LeerCadena(lector, "Codigo"),
                Descripcion = LeerCadena(lector, "Descripcion")
            });
        }
    }

    private static string LeerCadena(SqlDataReader lector, string columna) => lector[columna] is DBNull ? string.Empty : lector[columna].ToString()?.Trim() ?? string.Empty;
    private static int LeerEntero(SqlDataReader lector, string columna) => lector[columna] is DBNull ? 0 : Convert.ToInt32(lector[columna]);
    private static int? LeerEnteroNullable(SqlDataReader lector, string columna) => lector[columna] is DBNull ? null : Convert.ToInt32(lector[columna]);
    private static byte? LeerByteNullable(SqlDataReader lector, string columna) => lector[columna] is DBNull ? null : Convert.ToByte(lector[columna]);
    private static decimal? LeerDecimalNullable(SqlDataReader lector, string columna) => lector[columna] is DBNull ? null : Convert.ToDecimal(lector[columna]);
    private static DateTime LeerFecha(SqlDataReader lector, string columna) => Convert.ToDateTime(lector[columna]);
    private static DateTime? LeerFechaNullable(SqlDataReader lector, string columna) => lector[columna] is DBNull ? null : Convert.ToDateTime(lector[columna]);
}
