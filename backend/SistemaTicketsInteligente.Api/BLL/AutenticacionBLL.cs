/**
 * Archivo: AutenticacionBLL.cs
 * Objetivo: Aplicar las reglas necesarias para validar el inicio de sesión de un usuario.
 * Responsabilidad: Validar entrada, consultar al usuario, verificar la contraseña, revisar estados y devolver únicamente identidad segura.
 * Dependencias: AutenticacionDAO, PasswordHasher y DTO de autenticación.
 * Flujo: AutenticacionController -> AutenticacionBLL -> AutenticacionDAO -> SQL Server.
 * Consideraciones: No conoce HTTP ni React; la contraseña nunca se registra, persiste ni se envía a SQL Server para comparación.
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
    PerfilInactivo
}

public sealed class AutenticacionBLL
{
    private readonly AutenticacionDAO autenticacionDAO;
    private readonly PasswordHasher<UsuarioAutenticacion> passwordHasher = new();

    public AutenticacionBLL(AutenticacionDAO autenticacionDAO)
    {
        this.autenticacionDAO = autenticacionDAO;
    }

    public async Task<(ResultadoInicioSesion Resultado, RespuestaInicioSesion? Respuesta)> IniciarSesionAsync(
        SolicitudInicioSesion solicitud, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(solicitud.Usuario) || string.IsNullOrEmpty(solicitud.Contrasena)) return (ResultadoInicioSesion.DatosInvalidos, null);

        var usuarioIngresado = solicitud.Usuario.Trim();
        if (usuarioIngresado.Length > 20) return (ResultadoInicioSesion.DatosInvalidos, null);

        var idCorrelacion = Guid.NewGuid();
        var usuario = await autenticacionDAO.BuscarUsuarioAsync(usuarioIngresado, cancellationToken);

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
}
