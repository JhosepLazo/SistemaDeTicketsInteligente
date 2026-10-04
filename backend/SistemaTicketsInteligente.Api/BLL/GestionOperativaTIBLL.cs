/**
 * Archivo: GestionOperativaTIBLL.cs
 * Objetivo: Registrar el trabajo operativo del TI: avances con esfuerzo, solicitudes de aprobación y tickets por mesa de ayuda.
 * Responsabilidad: Validar cada operación con la identidad y el área del operador y ejecutar el procedimiento correspondiente.
 * Dependencias: BaseDatos (Usp_TI_Obtener_DatosGestionOperativaTI, Usp_TI_Registrar_AvanceTicket, Usp_TI_Solicitar_AprobacionTicket,
 *   Usp_TI_Crear_TicketPorUsuario).
 * Flujo: GestionOperativaTIController -> GestionOperativaTIBLL -> Stored Procedures.
 * Consideraciones: Cada avance exige minutos reales (1 a 1440) y el área causante; el ticket por mesa de ayuda conserva por
 *   separado al solicitante y al operador que lo registró.
 */

namespace SistemaTicketsInteligente.Api.BLL;

public sealed class GestionOperativaTIBLL(BaseDatos baseDatos)
{
    public Task<GestionOperativaTIDatos> ObtenerDatosAsync(string usuario, string area, CancellationToken ct)
    {
        var (usuarioValido, areaValida) = (Validacion.Usuario(usuario), Validacion.Area(area));
        return baseDatos.LeerAsync("dbo.Usp_TI_Obtener_DatosGestionOperativaTI", p =>
        {
            p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuarioValido;
            p.Add("@cArea", SqlDbType.Char, 3).Value = areaValida;
        }, async lector =>
        {
            var datos = new GestionOperativaTIDatos();
            datos.Usuarios = await lector.ListaAsync(f => new GestionOperativaUsuario { Codigo = f.Texto("Codigo"), Descripcion = f.Texto("Descripcion"), Area = f.Texto("Area") }, ct);
            datos.AccionesAprobacion = await lector.ListaAsync(f => new GestionOperativaAccion { AccionCodigo = f.Texto("AccionCodigo"), Nombre = f.Texto("Nombre"), NivelRiesgo = f.Texto("NivelRiesgo") }, ct);
            datos.Lineas = await lector.ListaAsync(Catalogo, ct);
            datos.Tipos = await lector.ListaAsync(Catalogo, ct);
            return datos;
        }, ct);
    }

    public Task RegistrarAvanceAsync(string usuario, string area, string incidenciaNumero, RegistrarAvanceDetalladoSolicitud s, CancellationToken ct)
    {
        var detalle = Validacion.Texto(s.Detalle, 5, 4000, "El detalle del avance");
        var areaCausante = Validacion.CodigoExacto(s.AreaCausante, 3, "El código de área debe tener 3 caracteres.");
        if (s.TiempoUtilizadoMinutos is <= 0 or > 1440) throw new ArgumentException("El tiempo efectivo debe estar entre 1 y 1440 minutos.");
        return EjecutarAccionAsync("dbo.Usp_TI_Registrar_AvanceTicket", usuario, area, incidenciaNumero, p =>
        {
            p.Add("@cDetalle", SqlDbType.NVarChar, -1).Value = detalle;
            p.Add("@lVisibleUsuario", SqlDbType.Bit).Value = s.VisibleUsuario;
            var minutos = p.Add("@nTiempoUtilizado", SqlDbType.Decimal);
            minutos.Precision = 8;
            minutos.Scale = 2;
            minutos.Value = s.TiempoUtilizadoMinutos;
            p.Add("@cAreaCausante", SqlDbType.Char, 3).Value = areaCausante;
        }, ct);
    }

    public Task SolicitarAprobacionAsync(string usuario, string area, string incidenciaNumero, SolicitarAprobacionOperativaSolicitud s, CancellationToken ct)
    {
        var accion = Validacion.Codigo(s.AccionCodigo, 50, "La acción no es válida.");
        var justificacion = Validacion.Texto(s.Justificacion, 10, 1000, "La justificación");
        return EjecutarAccionAsync("dbo.Usp_TI_Solicitar_AprobacionTicket", usuario, area, incidenciaNumero, p =>
        {
            p.Add("@cAccionCodigo", SqlDbType.VarChar, 50).Value = accion;
            p.Add("@cJustificacion", SqlDbType.NVarChar, 1000).Value = justificacion;
        }, ct);
    }

    /// <summary>TI registra un ticket a nombre de un colaborador (mesa de ayuda).</summary>
    public async Task<TicketMesaAyudaCreado> CrearTicketPorUsuarioAsync(string usuario, string area, CrearTicketMesaAyudaSolicitud s, CancellationToken ct)
    {
        var solicitante = Validacion.Codigo(s.UsuarioSolicitante, 20, "El usuario solicitante no es válido.");
        var linea = Validacion.CodigoExacto(s.Linea, 3, "El código de línea debe tener 3 caracteres.");
        var tipo = Validacion.CodigoExacto(s.Tipo, 3, "El código de tipo debe tener 3 caracteres.");
        var titulo = Validacion.Texto(s.Titulo, 5, 250, "El título");
        var detalle = Validacion.Texto(s.Detalle, 20, 4000, "El detalle");
        var mensajeError = Validacion.Opcional(s.MensajeError, 1000, "El mensaje de error");
        var (usuarioValido, areaValida) = (Validacion.Usuario(usuario), Validacion.Area(area));
        return await baseDatos.LeerAsync("dbo.Usp_TI_Crear_TicketPorUsuario", p =>
        {
            p.Add("@cUsuarioTI", SqlDbType.VarChar, 20).Value = usuarioValido;
            p.Add("@cAreaTI", SqlDbType.Char, 3).Value = areaValida;
            p.Add("@cUsuarioSolicitante", SqlDbType.VarChar, 20).Value = solicitante;
            p.Add("@cLinea", SqlDbType.Char, 3).Value = linea;
            p.Add("@cTipo", SqlDbType.Char, 3).Value = tipo;
            p.Add("@cTitulo", SqlDbType.NVarChar, 250).Value = titulo;
            p.Add("@cDetalle", SqlDbType.NVarChar, -1).Value = detalle;
            p.Add("@cMensajeError", SqlDbType.NVarChar, 1000).Value = BaseDatos.Opcional(mensajeError);
            p.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
        }, lector => lector.FilaAsync(f => new TicketMesaAyudaCreado { IncidenciaNumero = f.Texto("IncidenciaNumero"), FechaRegistro = f.Fecha("FechaRegistro") }, ct), ct)
            ?? throw new InvalidOperationException("No se obtuvo el ticket creado por mesa de ayuda.");
    }

    private Task EjecutarAccionAsync(string procedimiento, string usuario, string area, string incidenciaNumero, Action<SqlParameterCollection> parametros, CancellationToken ct)
    {
        var (usuarioValido, areaValida, incidencia) = (Validacion.Usuario(usuario), Validacion.Area(area), Validacion.Incidencia(incidenciaNumero, minimo: 8));
        return baseDatos.EjecutarAsync(procedimiento, p =>
        {
            p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuarioValido;
            p.Add("@cArea", SqlDbType.Char, 3).Value = areaValida;
            p.Add("@cIncidenciaNumero", SqlDbType.VarChar, 12).Value = incidencia;
            parametros(p);
            p.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
        }, ct);
    }

    private static GestionTicketsTICatalogoItem Catalogo(SqlDataReader f) => new() { Codigo = f.Texto("Codigo"), Descripcion = f.Texto("Descripcion") };
}
