/**
 * Archivo: AutenticacionBLL.cs
 * Objetivo: Aplicar las reglas necesarias para iniciar y cerrar la sesión de un usuario.
 * Responsabilidad: Usar la identidad corporativa cuando está habilitada, conservar la autenticación local para desarrollo,
 *   validar los estados del usuario y su perfil, auditar cada intento y devolver solo la identidad segura.
 * Dependencias: BaseDatos (Usp_TI_Buscar_UsuarioAutenticacion, Usp_TI_Sincronizar_UsuarioCorporativo,
 *   Usp_TI_Registrar_AuditoriaAutenticacion), IdentidadCorporativa y PasswordHasher de ASP.NET Core.
 * Flujo: AutenticacionController -> AutenticacionBLL -> identidad corporativa o local -> SQL Server.
 * Consideraciones: La contraseña corporativa solo se entrega al procedimiento de Spring; nunca se registra ni se guarda.
 *   El área y el perfil siguen siendo la autorización propia del sistema.
 */

using Microsoft.AspNetCore.Identity;

namespace SistemaTicketsInteligente.Api.BLL;

public enum ResultadoInicioSesion
{
    Correcto,
    DatosInvalidos,
    CredencialesIncorrectas,
    UsuarioInactivo,
    PerfilInactivo,
    UsuarioSinConfiguracionLocal,
    IdentidadCorporativaNoDisponible
}

public sealed class AutenticacionBLL(BaseDatos baseDatos, IdentidadCorporativa identidadCorporativa)
{
    private readonly PasswordHasher<UsuarioAutenticacion> passwordHasher = new();

    public async Task<(ResultadoInicioSesion Resultado, RespuestaInicioSesion? Respuesta)> IniciarSesionAsync(SolicitudInicioSesion solicitud, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(solicitud.Usuario) || string.IsNullOrEmpty(solicitud.Contrasena)) return (ResultadoInicioSesion.DatosInvalidos, null);
        var usuarioIngresado = solicitud.Usuario.Trim();
        if (usuarioIngresado.Length > 20) return (ResultadoInicioSesion.DatosInvalidos, null);
        var idCorrelacion = Guid.NewGuid();
        Task Auditar(string? usuario, string evento, string resultado) => RegistrarAuditoriaAsync(usuario, usuarioIngresado, evento, resultado, idCorrelacion, ct);

        UsuarioAutenticacion? usuario;
        if (identidadCorporativa.Disponible)
        {
            bool credencialesValidas;
            try { credencialesValidas = await identidadCorporativa.AutenticarAsync(usuarioIngresado, solicitud.Contrasena, ct); }
            catch (SqlException) { return (ResultadoInicioSesion.IdentidadCorporativaNoDisponible, null); }

            if (!credencialesValidas)
            {
                await Auditar(null, "LOGIN_CORPORATIVO_FALLIDO", "DENEGADO");
                return (ResultadoInicioSesion.CredencialesIncorrectas, null);
            }
            var usuarioCorporativo = await identidadCorporativa.ObtenerUsuarioAsync(usuarioIngresado, ct);
            if (usuarioCorporativo is null)
            {
                await Auditar(null, "LOGIN_CORPORATIVO_SIN_DATOS", "DENEGADO");
                return (ResultadoInicioSesion.UsuarioSinConfiguracionLocal, null);
            }
            if (await BuscarUsuarioAsync(usuarioIngresado, ct) is null)
            {
                await Auditar(null, "LOGIN_SIN_CONFIGURACION_LOCAL", "DENEGADO");
                return (ResultadoInicioSesion.UsuarioSinConfiguracionLocal, null);
            }
            await SincronizarUsuarioCorporativoAsync(usuarioCorporativo, usuarioIngresado, ct);
            usuario = await BuscarUsuarioAsync(usuarioIngresado, ct);
        }
        else
        {
            usuario = await BuscarUsuarioAsync(usuarioIngresado, ct);
            if (usuario is null)
            {
                await Auditar(null, "LOGIN_FALLIDO", "DENEGADO");
                return (ResultadoInicioSesion.CredencialesIncorrectas, null);
            }
            PasswordVerificationResult verificacion;
            try { verificacion = passwordHasher.VerifyHashedPassword(usuario, usuario.ClaveHash, solicitud.Contrasena); }
            catch (FormatException ex) { throw new InvalidOperationException("El usuario posee un hash de contraseña con formato inválido.", ex); }
            if (verificacion == PasswordVerificationResult.Failed)
            {
                await Auditar(usuario.Usuario, "LOGIN_FALLIDO", "DENEGADO");
                return (ResultadoInicioSesion.CredencialesIncorrectas, null);
            }
        }

        if (usuario is null) return (ResultadoInicioSesion.UsuarioSinConfiguracionLocal, null);
        if (!string.Equals(usuario.EstadoUsuario, "A", StringComparison.OrdinalIgnoreCase))
        {
            await Auditar(usuario.Usuario, "LOGIN_DENEGADO_USUARIO_INACTIVO", "DENEGADO");
            return (ResultadoInicioSesion.UsuarioInactivo, null);
        }
        if (!string.Equals(usuario.EstadoPerfil, "A", StringComparison.OrdinalIgnoreCase))
        {
            await Auditar(usuario.Usuario, "LOGIN_DENEGADO_PERFIL_INACTIVO", "DENEGADO");
            return (ResultadoInicioSesion.PerfilInactivo, null);
        }
        await Auditar(usuario.Usuario, "LOGIN_EXITOSO", "EXITOSO");
        return (ResultadoInicioSesion.Correcto, new RespuestaInicioSesion { Usuario = usuario.Usuario, NombreCompleto = usuario.NombreCompleto, Area = usuario.Area, Perfil = usuario.Perfil });
    }

    public Task CerrarSesionAsync(string usuario, CancellationToken ct)
    {
        var usuarioNormalizado = usuario.Trim();
        if (usuarioNormalizado.Length == 0) throw new ArgumentException("El usuario autenticado es obligatorio.");
        return RegistrarAuditoriaAsync(usuarioNormalizado, usuarioNormalizado, "LOGOUT_EXITOSO", "EXITOSO", Guid.NewGuid(), ct);
    }

    private Task<UsuarioAutenticacion?> BuscarUsuarioAsync(string usuario, CancellationToken ct) =>
        baseDatos.LeerAsync("dbo.Usp_TI_Buscar_UsuarioAutenticacion", p => p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario,
            lector => lector.FilaAsync(f => new UsuarioAutenticacion
            {
                Usuario = f.Texto("Usuario"), NombreCompleto = f.Texto("NombreCompleto"), ClaveHash = f.Texto("ClaveHash"), Area = f.Texto("Area"),
                Perfil = f.Texto("Perfil"), EstadoUsuario = f.Texto("EstadoUsuario"), EstadoPerfil = f.Texto("EstadoPerfil")
            }, ct), ct);

    // Actualiza en la base local los datos que provienen de Spring (nombre, cargo, documento y estado corporativo).
    private Task SincronizarUsuarioCorporativoAsync(UsuarioCorporativo corporativo, string usuarioModifica, CancellationToken ct)
    {
        var cargo = corporativo.Cargo.Trim().ToUpperInvariant();
        if (cargo.Length > 3) cargo = string.Empty;
        var estadoCorporativo = string.Join('/', new[] { corporativo.Estado, corporativo.EstadoEmpleado }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return baseDatos.EjecutarAsync("dbo.Usp_TI_Sincronizar_UsuarioCorporativo", p =>
        {
            p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = corporativo.Usuario;
            p.Add("@cNombreCompleto", SqlDbType.VarChar, 255).Value = corporativo.NombreCompleto;
            p.Add("@cCargo", SqlDbType.Char, 3).Value = BaseDatos.Opcional(cargo);
            p.Add("@cDocumento", SqlDbType.VarChar, 20).Value = BaseDatos.Opcional(corporativo.Documento);
            p.Add("@cEstadoCorporativo", SqlDbType.VarChar, 20).Value = BaseDatos.Opcional(estadoCorporativo.Length > 20 ? estadoCorporativo[..20] : estadoCorporativo);
            p.Add("@cUsuarioModifica", SqlDbType.VarChar, 20).Value = usuarioModifica;
        }, ct);
    }

    private Task RegistrarAuditoriaAsync(string? usuario, string registro, string evento, string resultado, Guid idCorrelacion, CancellationToken ct) =>
        baseDatos.EjecutarAsync("dbo.Usp_TI_Registrar_AuditoriaAutenticacion", p =>
        {
            p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = (object?)usuario ?? DBNull.Value;
            p.Add("@cRegistro", SqlDbType.VarChar, 200).Value = registro;
            p.Add("@cEvento", SqlDbType.VarChar, 100).Value = evento;
            p.Add("@cResultado", SqlDbType.VarChar, 20).Value = resultado;
            p.Add("@cIdCorrelacion", SqlDbType.UniqueIdentifier).Value = idCorrelacion;
        }, ct);
}
