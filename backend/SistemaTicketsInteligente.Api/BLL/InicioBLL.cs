/**
 * Archivo: InicioBLL.cs
 * Objetivo: Armar la pantalla de inicio del colaborador y la del operador TI.
 * Responsabilidad: Validar la identidad y leer el resumen, los tickets que requieren atención, las acciones pendientes,
 *   los recordatorios y la actividad reciente.
 * Dependencias: BaseDatos (Usp_TI_Obtener_InicioUsuario y Usp_TI_Obtener_InicioTI).
 * Flujo: InicioController -> InicioBLL -> Stored Procedure -> InicioDTO.
 * Consideraciones: La identidad sale de la cookie; cada procedimiento filtra lo que corresponde al usuario o a su área.
 */

namespace SistemaTicketsInteligente.Api.BLL;

public sealed class InicioBLL(BaseDatos baseDatos)
{
    public Task<InicioUsuarioRespuesta> ObtenerUsuarioAsync(string usuario, CancellationToken ct)
    {
        var usuarioValido = Validacion.Usuario(usuario);
        return baseDatos.LeerAsync("dbo.Usp_TI_Obtener_InicioUsuario", p => p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuarioValido, async lector =>
        {
            var respuesta = new InicioUsuarioRespuesta();
            respuesta.Resumen = await lector.FilaAsync(f => new InicioUsuarioResumen
            {
                TicketsActivos = f.Entero("TicketsActivos"), EnAtencion = f.Entero("EnAtencion"), RequierenAtencion = f.Entero("RequierenAtencion"),
                Resueltos30Dias = f.Entero("Resueltos30Dias"), PendientesCalificacion = f.Entero("PendientesCalificacion")
            }, ct) ?? new();
            respuesta.RequierenAtencion = await lector.ListaAsync(f => new InicioUsuarioTicket
            {
                IncidenciaNumero = f.Texto("IncidenciaNumero"), Titulo = f.Texto("Titulo"), Estado = f.Texto("Estado"),
                EstadoDescripcion = f.Texto("EstadoDescripcion"), Accion = f.Texto("Accion"), UltimaFechaModif = f.Fecha("UltimaFechaModif")
            }, ct);
            respuesta.TicketsRecientes = await lector.ListaAsync(f => new InicioUsuarioTicket
            {
                IncidenciaNumero = f.Texto("IncidenciaNumero"), Titulo = f.Texto("Titulo"), Estado = f.Texto("Estado"),
                EstadoDescripcion = f.Texto("EstadoDescripcion"), Responsable = f.Texto("Responsable"), UltimaFechaModif = f.Fecha("UltimaFechaModif")
            }, ct);
            respuesta.AccionesPendientes = await lector.ListaAsync(f => new InicioUsuarioAccionPendiente
            {
                IncidenciaNumero = f.Texto("IncidenciaNumero"), Titulo = f.Texto("Titulo"), TipoAccion = f.Texto("TipoAccion"),
                Descripcion = f.Texto("Descripcion"), UltimaFechaModif = f.Fecha("UltimaFechaModif")
            }, ct);
            respuesta.ActividadReciente = await lector.ListaAsync(f => new InicioUsuarioActividad
            {
                IncidenciaNumero = f.Texto("IncidenciaNumero"), Titulo = f.Texto("Titulo"), EstadoDescripcion = f.Texto("EstadoDescripcion"),
                Actor = f.Texto("Actor"), FechaCambio = f.Fecha("FechaCambio")
            }, ct);
            return respuesta;
        }, ct);
    }

    public Task<InicioTIRespuesta> ObtenerTIAsync(string usuario, string area, CancellationToken ct)
    {
        var usuarioValido = Validacion.Usuario(usuario);
        var areaValida = Validacion.Area(area);
        return baseDatos.LeerAsync("dbo.Usp_TI_Obtener_InicioTI", p =>
        {
            p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuarioValido;
            p.Add("@cArea", SqlDbType.Char, 3).Value = areaValida;
        }, async lector =>
        {
            var respuesta = new InicioTIRespuesta();
            respuesta.Resumen = await lector.FilaAsync(f => new InicioTIResumen
            {
                Pendientes = f.Entero("Pendientes"), PendientesDesdeAyer = f.Entero("PendientesDesdeAyer"), EnAtencion = f.Entero("EnAtencion"),
                EnProgresoHoy = f.Entero("EnProgresoHoy"), RequierenAccion = f.Entero("RequierenAccion"), SinAsignar = f.Entero("SinAsignar"),
                PrioridadAlta = f.Entero("PrioridadAlta"), TicketsActivos = f.Entero("TicketsActivos"), MisAsignados = f.Entero("MisAsignados")
            }, ct) ?? new();
            respuesta.RequierenAtencion = await lector.ListaAsync(f => new InicioTITicketPrioritario
            {
                IncidenciaNumero = f.Texto("IncidenciaNumero"), UsuarioSolicitante = f.Texto("UsuarioSolicitante"), Titulo = f.Texto("Titulo"),
                Tipo = f.Texto("Tipo"), TipoDescripcion = f.Texto("TipoDescripcion"), Estado = f.Texto("Estado"), EstadoDescripcion = f.Texto("EstadoDescripcion"),
                Prioridad = f.EnteroNulo("Prioridad"), UsuarioTI = f.Texto("UsuarioTI"), Responsable = f.Texto("Responsable"),
                UltimaFechaModif = f.Fecha("UltimaFechaModif"), SlaMinutosRestantes = f.EnteroNulo("SlaMinutosRestantes"),
                TipoAtencion = f.Texto("TipoAtencion"), Accion = f.Texto("Accion")
            }, ct);
            respuesta.Recordatorios = await lector.FilaAsync(f => new InicioTIRecordatorios
            {
                AprobacionesPendientes = f.Entero("AprobacionesPendientes"), SlaPorVencer = f.Entero("SlaPorVencer"), TicketsReabiertos = f.Entero("TicketsReabiertos")
            }, ct) ?? new();
            respuesta.TicketsActivos = await lector.ListaAsync(f => new InicioTITicketActivo
            {
                IncidenciaNumero = f.Texto("IncidenciaNumero"), UsuarioSolicitante = f.Texto("UsuarioSolicitante"), Titulo = f.Texto("Titulo"),
                Tipo = f.Texto("Tipo"), TipoDescripcion = f.Texto("TipoDescripcion"), Prioridad = f.EnteroNulo("Prioridad"), AreaTI = f.Texto("AreaTI"),
                GrupoSoporte = f.Texto("GrupoSoporte"), Estado = f.Texto("Estado"), EstadoDescripcion = f.Texto("EstadoDescripcion"),
                UsuarioTI = f.Texto("UsuarioTI"), Responsable = f.Texto("Responsable"), UltimaFechaModif = f.Fecha("UltimaFechaModif")
            }, ct);
            respuesta.ActividadReciente = await lector.ListaAsync(f => new InicioTIActividad
            {
                IncidenciaNumero = f.Texto("IncidenciaNumero"), Titulo = f.Texto("Titulo"), Estado = f.Texto("Estado"),
                EstadoDescripcion = f.Texto("EstadoDescripcion"), Actor = f.Texto("Actor"), FechaCambio = f.Fecha("FechaCambio")
            }, ct);
            return respuesta;
        }, ct);
    }
}
