/**
 * Archivo: AutenticacionBLL.cs
 * Objetivo: Aplicar las reglas necesarias para iniciar y cerrar la sesión de un usuario.
 * Responsabilidad: Usar identidad corporativa cuando esté habilitada, conservar autenticación local para desarrollo, validar estados y devolver identidad segura.
 * Dependencias: AutenticacionDAO, IdentidadCorporativaDAO, PasswordHasher y DTO de autenticación.
 * Flujo: AutenticacionController -> AutenticacionBLL -> identidad corporativa/local -> SQL Server.
 * Consideraciones: La contraseña corporativa solo se entrega al procedimiento de autenticación de Spring; nunca se registra ni persiste localmente. Área y perfil continúan siendo autorización propia del sistema.
 */

using Microsoft.AspNetCore.Identity;
using SistemaTicketsInteligente.Api.DAO;
using SistemaTicketsInteligente.Api.DTO.Autenticacion;

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

public sealed class AutenticacionBLL
{
    private readonly AutenticacionDAO autenticacionDAO;
    private readonly IdentidadCorporativaDAO identidadCorporativaDAO;
    private readonly PasswordHasher<UsuarioAutenticacion> passwordHasher = new();

    public AutenticacionBLL(AutenticacionDAO autenticacionDAO, IdentidadCorporativaDAO identidadCorporativaDAO)
    {
        this.autenticacionDAO = autenticacionDAO;
        this.identidadCorporativaDAO = identidadCorporativaDAO;
    }

    public async Task<(ResultadoInicioSesion Resultado, RespuestaInicioSesion? Respuesta)> IniciarSesionAsync(
        SolicitudInicioSesion solicitud, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(solicitud.Usuario) || string.IsNullOrEmpty(solicitud.Contrasena)) return (ResultadoInicioSesion.DatosInvalidos, null);

        var usuarioIngresado = solicitud.Usuario.Trim();
        if (usuarioIngresado.Length > 20) return (ResultadoInicioSesion.DatosInvalidos, null);

        var idCorrelacion = Guid.NewGuid();
        UsuarioAutenticacion? usuario;

        if (identidadCorporativaDAO.Disponible)
        {
            bool credencialesValidas;
            try
            {
                credencialesValidas = await identidadCorporativaDAO.AutenticarAsync(usuarioIngresado, solicitud.Contrasena, cancellationToken);
            }
            catch (SqlException)
            {
                return (ResultadoInicioSesion.IdentidadCorporativaNoDisponible, null);
            }

            if (!credencialesValidas)
            {
                await autenticacionDAO.RegistrarAuditoriaAsync(null, usuarioIngresado, "LOGIN_CORPORATIVO_FALLIDO", "DENEGADO", idCorrelacion, cancellationToken);
                return (ResultadoInicioSesion.CredencialesIncorrectas, null);
            }

            var usuarioCorporativo = await identidadCorporativaDAO.ObtenerUsuarioAsync(usuarioIngresado, cancellationToken);
            if (usuarioCorporativo is null)
            {
                await autenticacionDAO.RegistrarAuditoriaAsync(null, usuarioIngresado, "LOGIN_CORPORATIVO_SIN_DATOS", "DENEGADO", idCorrelacion, cancellationToken);
                return (ResultadoInicioSesion.UsuarioSinConfiguracionLocal, null);
            }

            usuario = await autenticacionDAO.BuscarUsuarioAsync(usuarioIngresado, cancellationToken);
            if (usuario is null)
            {
                await autenticacionDAO.RegistrarAuditoriaAsync(null, usuarioIngresado, "LOGIN_SIN_CONFIGURACION_LOCAL", "DENEGADO", idCorrelacion, cancellationToken);
                return (ResultadoInicioSesion.UsuarioSinConfiguracionLocal, null);
            }

            await autenticacionDAO.SincronizarUsuarioCorporativoAsync(usuarioCorporativo, usuarioIngresado, cancellationToken);
            usuario = await autenticacionDAO.BuscarUsuarioAsync(usuarioIngresado, cancellationToken);
        }
        else
        {
            usuario = await autenticacionDAO.BuscarUsuarioAsync(usuarioIngresado, cancellationToken);
            if (usuario is null)
            {
                await autenticacionDAO.RegistrarAuditoriaAsync(null, usuarioIngresado, "LOGIN_FALLIDO", "DENEGADO", idCorrelacion, cancellationToken);
                return (ResultadoInicioSesion.CredencialesIncorrectas, null);
            }

            PasswordVerificationResult verificacionClave;
            try
            {
                verificacionClave = passwordHasher.VerifyHashedPassword(usuario, usuario.ClaveHash, solicitud.Contrasena);
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException("El usuario posee un hash de contraseña con formato inválido.", ex);
            }

            if (verificacionClave == PasswordVerificationResult.Failed)
            {
                await autenticacionDAO.RegistrarAuditoriaAsync(usuario.Usuario, usuarioIngresado, "LOGIN_FALLIDO", "DENEGADO", idCorrelacion, cancellationToken);
                return (ResultadoInicioSesion.CredencialesIncorrectas, null);
            }
        }

        if (usuario is null) return (ResultadoInicioSesion.UsuarioSinConfiguracionLocal, null);

        if (!string.Equals(usuario.EstadoUsuario, "A", StringComparison.OrdinalIgnoreCase))
        {
            await autenticacionDAO.RegistrarAuditoriaAsync(usuario.Usuario, usuarioIngresado, "LOGIN_DENEGADO_USUARIO_INACTIVO", "DENEGADO", idCorrelacion, cancellationToken);
            return (ResultadoInicioSesion.UsuarioInactivo, null);
        }

        if (!string.Equals(usuario.EstadoPerfil, "A", StringComparison.OrdinalIgnoreCase))
        {
            await autenticacionDAO.RegistrarAuditoriaAsync(usuario.Usuario, usuarioIngresado, "LOGIN_DENEGADO_PERFIL_INACTIVO", "DENEGADO", idCorrelacion, cancellationToken);
            return (ResultadoInicioSesion.PerfilInactivo, null);
        }

        await autenticacionDAO.RegistrarAuditoriaAsync(usuario.Usuario, usuarioIngresado, "LOGIN_EXITOSO", "EXITOSO", idCorrelacion, cancellationToken);

        return (ResultadoInicioSesion.Correcto, new RespuestaInicioSesion
        {
            Usuario = usuario.Usuario,
            NombreCompleto = usuario.NombreCompleto,
            Area = usuario.Area,
            Perfil = usuario.Perfil
        });
    }

    public Task CerrarSesionAsync(string usuario, CancellationToken cancellationToken = default)
    {
        var usuarioNormalizado = usuario.Trim();
        if (usuarioNormalizado.Length == 0) throw new ArgumentException("El usuario autenticado es obligatorio.", nameof(usuario));

        return autenticacionDAO.RegistrarAuditoriaAsync(
            usuarioNormalizado,
            usuarioNormalizado,
            "LOGOUT_EXITOSO",
            "EXITOSO",
            Guid.NewGuid(),
            cancellationToken);
    }
}
