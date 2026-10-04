/**
 * Archivo: NotificacionesBLL.cs
 * Objetivo: Entregar al usuario autenticado sus avisos recientes y registrar los que ya leyó.
 * Responsabilidad: Validar la identidad y el aviso, y consultar o actualizar las notificaciones persistidas.
 * Dependencias: BaseDatos (Usp_TI_Obtener_Notificaciones y Usp_TI_Marcar_NotificacionLeida).
 * Flujo: NotificacionesController -> NotificacionesBLL -> Stored Procedures.
 * Consideraciones: El usuario siempre sale de la cookie: nadie puede leer ni marcar avisos de otra persona.
 */

namespace SistemaTicketsInteligente.Api.BLL;

public sealed class NotificacionesBLL(BaseDatos baseDatos)
{
    public Task<NotificacionesRespuesta> ObtenerAsync(string usuario, CancellationToken ct)
    {
        var usuarioValido = Validacion.Usuario(usuario);
        return baseDatos.LeerAsync("dbo.Usp_TI_Obtener_Notificaciones", p => p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuarioValido, async lector =>
        {
            var respuesta = new NotificacionesRespuesta();
            if (await lector.ReadAsync(ct)) respuesta.NoLeidas = lector.Entero("NoLeidas");
            await lector.NextResultAsync(ct);
            respuesta.Notificaciones = await lector.ListaAsync(f => new NotificacionItem
            {
                NotificacionNumero = f.Largo("NotificacionNumero"), IncidenciaNumero = f.Texto("IncidenciaNumero"), Tipo = f.Texto("Tipo"),
                Titulo = f.Texto("Titulo"), Mensaje = f.Texto("Mensaje"), Ruta = f.Texto("Ruta"), Leida = f.Booleano("Leida"), Fecha = f.Fecha("Fecha")
            }, ct);
            return respuesta;
        }, ct);
    }

    public Task MarcarLeidaAsync(string usuario, long notificacionNumero, CancellationToken ct)
    {
        var usuarioValido = Validacion.Usuario(usuario);
        if (notificacionNumero <= 0) throw new ArgumentException("La notificación indicada no es válida.");
        return baseDatos.EjecutarAsync("dbo.Usp_TI_Marcar_NotificacionLeida", p =>
        {
            p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuarioValido;
            p.Add("@nNotificacionNumero", SqlDbType.BigInt).Value = notificacionNumero;
        }, ct);
    }
}
