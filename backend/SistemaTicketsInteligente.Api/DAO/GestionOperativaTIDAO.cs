/*
 * Archivo: GestionOperativaTIDAO.cs
 * Objetivo: Ejecutar Stored Procedures de avance detallado, aprobación manual y mesa de ayuda.
 * Responsabilidad: Consultar catálogos pequeños y persistir las acciones complementarias sin duplicar la bandeja principal de Gestión de Tickets.
 * Dependencias: ConexionSqlServer, Microsoft.Data.SqlClient y GestionOperativaTIDTO.
 * Flujo: GestionOperativaTIBLL -> GestionOperativaTIDAO -> Stored Procedures -> SQL Server.
 * Consideraciones: Convierte errores funcionales 50200-50499 en excepciones controladas; no contiene reglas de negocio.
 */

using System.Data;
using Microsoft.Data.SqlClient;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.DAO;

public sealed class GestionOperativaTIDAO
{
    private readonly ConexionSqlServer conexionSqlServer;

    public GestionOperativaTIDAO(ConexionSqlServer conexionSqlServer)
    {
        this.conexionSqlServer = conexionSqlServer;
    }

    public async Task<GestionOperativaTIDatos> ObtenerDatosAsync(string usuario, string area, CancellationToken ct = default)
    {
        var respuesta = new GestionOperativaTIDatos();
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Obtener_DatosGestionOperativaTI", conexion) { CommandType = CommandType.StoredProcedure };
        AgregarIdentidad(comando, usuario, area);
        await conexion.OpenAsync(ct);

        try
        {
            await using var lector = await comando.ExecuteReaderAsync(ct);
            while (await lector.ReadAsync(ct)) respuesta.Usuarios.Add(new GestionOperativaUsuario { Codigo = LeerCadena(lector, "Codigo"), Descripcion = LeerCadena(lector, "Descripcion"), Area = LeerCadena(lector, "Area") });
            await lector.NextResultAsync(ct);
            while (await lector.ReadAsync(ct)) respuesta.AccionesAprobacion.Add(new GestionOperativaAccion { AccionCodigo = LeerCadena(lector, "AccionCodigo"), Nombre = LeerCadena(lector, "Nombre"), NivelRiesgo = LeerCadena(lector, "NivelRiesgo") });
            await lector.NextResultAsync(ct);
            while (await lector.ReadAsync(ct)) respuesta.Lineas.Add(new GestionTicketsTICatalogoItem { Codigo = LeerCadena(lector, "Codigo"), Descripcion = LeerCadena(lector, "Descripcion") });
            await lector.NextResultAsync(ct);
            while (await lector.ReadAsync(ct)) respuesta.Tipos.Add(new GestionTicketsTICatalogoItem { Codigo = LeerCadena(lector, "Codigo"), Descripcion = LeerCadena(lector, "Descripcion") });
        }
        catch (SqlException ex) when (EsErrorFuncional(ex)) { throw new InvalidOperationException(ex.Message, ex); }

        return respuesta;
    }

    public Task RegistrarAvanceAsync(string usuario, string area, string incidenciaNumero, RegistrarAvanceDetalladoSolicitud s, Guid idCorrelacion, CancellationToken ct = default) =>
        EjecutarAccionAsync("dbo.Usp_TI_Registrar_AvanceTicket", usuario, area, incidenciaNumero, c =>
        {
            c.Parameters.Add("@cDetalle", SqlDbType.NVarChar, -1).Value = s.Detalle;
            c.Parameters.Add("@lVisibleUsuario", SqlDbType.Bit).Value = s.VisibleUsuario;
            c.Parameters.Add("@nTiempoUtilizado", SqlDbType.Decimal).Value = s.TiempoUtilizadoMinutos;
            c.Parameters["@nTiempoUtilizado"].Precision = 8;
            c.Parameters["@nTiempoUtilizado"].Scale = 2;
            c.Parameters.Add("@cAreaCausante", SqlDbType.Char, 3).Value = s.AreaCausante;
            c.Parameters.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = idCorrelacion;
        }, ct);

    public Task SolicitarAprobacionAsync(string usuario, string area, string incidenciaNumero, SolicitarAprobacionOperativaSolicitud s, Guid idCorrelacion, CancellationToken ct = default) =>
        EjecutarAccionAsync("dbo.Usp_TI_Solicitar_AprobacionTicket", usuario, area, incidenciaNumero, c =>
        {
            c.Parameters.Add("@cAccionCodigo", SqlDbType.VarChar, 50).Value = s.AccionCodigo;
            c.Parameters.Add("@cJustificacion", SqlDbType.NVarChar, 1000).Value = s.Justificacion;
            c.Parameters.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = idCorrelacion;
        }, ct);

    public async Task<TicketMesaAyudaCreado> CrearTicketPorUsuarioAsync(string usuario, string area, CrearTicketMesaAyudaSolicitud s, Guid idCorrelacion, CancellationToken ct = default)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand("dbo.Usp_TI_Crear_TicketPorUsuario", conexion) { CommandType = CommandType.StoredProcedure };
        comando.Parameters.Add("@cUsuarioTI", SqlDbType.VarChar, 20).Value = usuario;
        comando.Parameters.Add("@cAreaTI", SqlDbType.Char, 3).Value = area;
        comando.Parameters.Add("@cUsuarioSolicitante", SqlDbType.VarChar, 20).Value = s.UsuarioSolicitante;
        comando.Parameters.Add("@cLinea", SqlDbType.Char, 3).Value = s.Linea;
        comando.Parameters.Add("@cTipo", SqlDbType.Char, 3).Value = s.Tipo;
        comando.Parameters.Add("@cTitulo", SqlDbType.NVarChar, 250).Value = s.Titulo;
        comando.Parameters.Add("@cDetalle", SqlDbType.NVarChar, -1).Value = s.Detalle;
        comando.Parameters.Add("@cMensajeError", SqlDbType.NVarChar, 1000).Value = string.IsNullOrWhiteSpace(s.MensajeError) ? DBNull.Value : s.MensajeError;
        comando.Parameters.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = idCorrelacion;
        await conexion.OpenAsync(ct);

        try
        {
            await using var lector = await comando.ExecuteReaderAsync(ct);
            if (!await lector.ReadAsync(ct)) throw new InvalidOperationException("No se obtuvo el ticket creado por mesa de ayuda.");
            return new TicketMesaAyudaCreado { IncidenciaNumero = LeerCadena(lector, "IncidenciaNumero"), FechaRegistro = Convert.ToDateTime(lector["FechaRegistro"]) };
        }
        catch (SqlException ex) when (EsErrorFuncional(ex)) { throw new InvalidOperationException(ex.Message, ex); }
    }

    private async Task EjecutarAccionAsync(string procedimiento, string usuario, string area, string incidenciaNumero, Action<SqlCommand> configurar, CancellationToken ct)
    {
        await using var conexion = conexionSqlServer.CrearConexion();
        await using var comando = new SqlCommand(procedimiento, conexion) { CommandType = CommandType.StoredProcedure };
        AgregarIdentidad(comando, usuario, area);
        comando.Parameters.Add("@cIncidenciaNumero", SqlDbType.VarChar, 12).Value = incidenciaNumero;
        configurar(comando);
        await conexion.OpenAsync(ct);
        try { await comando.ExecuteNonQueryAsync(ct); }
        catch (SqlException ex) when (EsErrorFuncional(ex)) { throw new InvalidOperationException(ex.Message, ex); }
    }

    private static void AgregarIdentidad(SqlCommand comando, string usuario, string area)
    {
        comando.Parameters.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;
        comando.Parameters.Add("@cArea", SqlDbType.Char, 3).Value = area;
    }

    private static bool EsErrorFuncional(SqlException ex) => ex.Number is >= 50200 and <= 50499;
    private static string LeerCadena(SqlDataReader lector, string columna) => lector[columna] is DBNull ? string.Empty : lector[columna].ToString()?.Trim() ?? string.Empty;
}
