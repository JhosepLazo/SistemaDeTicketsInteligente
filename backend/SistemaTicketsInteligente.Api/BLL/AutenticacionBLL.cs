/**
 * Archivo: AutenticacionBLL.cs
 * Objetivo: Aplicar las reglas necesarias para iniciar y cerrar la sesión de un usuario y mantenerla vigente.
 * Responsabilidad: Usar la identidad corporativa cuando está habilitada, conservar la autenticación local para desarrollo,
 *   validar los estados del usuario y su perfil, auditar cada intento, limitar los intentos fallidos por usuario, devolver solo la
 *   identidad segura y revalidar cada pocos minutos que la sesión abierta siga correspondiendo a un usuario y perfil vigentes.
 * Dependencias: BaseDatos (Usp_TI_Buscar_UsuarioAutenticacion, Usp_TI_Sincronizar_UsuarioCorporativo,
 *   Usp_TI_Registrar_AuditoriaAutenticacion), IdentidadCorporativa, PasswordHasher de ASP.NET Core e IMemoryCache.
 * Flujo: AutenticacionController -> AutenticacionBLL -> identidad corporativa o local -> SQL Server.
 *   Program.cs (OnValidatePrincipal) -> SesionVigenteAsync en cada petición, con caché corta.
 * Consideraciones: La contraseña corporativa solo se entrega al procedimiento de Spring; nunca se registra ni se guarda.
 *   El área y el perfil siguen siendo la autorización propia del sistema. Un usuario inexistente se compara contra un hash
 *   ficticio para que el tiempo de respuesta no revele si existe; una cuenta SISTEMA nunca inicia sesión.
 */

using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;

namespace SistemaTicketsInteligente.Api.BLL;

public enum ResultadoInicioSesion
{
    Correcto,
    DatosInvalidos,
    CredencialesIncorrectas,
    UsuarioInactivo,
    PerfilInactivo,
    UsuarioSinConfiguracionLocal,
    IdentidadCorporativaNoDisponible,
    DemasiadosIntentos
}

public sealed class AutenticacionBLL(BaseDatos baseDatos, IdentidadCorporativa identidadCorporativa, IMemoryCache cache, ILogger<AutenticacionBLL> logger)
{
    // Código que usan todos los procedimientos y maestros para un registro vigente (I marca los inactivos).
    private const string EstadoActivo = "A";
    private const string CuentaSistema = "SISTEMA";
    // Mismo límite que la política "Login" por IP (Program.cs), ahora también por usuario.
    private const int MaximoIntentosFallidos = 5;
    private static readonly TimeSpan VentanaIntentos = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan VigenciaRevalidacion = TimeSpan.FromMinutes(2);
    private static readonly PasswordHasher<UsuarioAutenticacion> PasswordHasher = new();
    // Hash de una contraseña aleatoria: verificar contra él cuesta lo mismo que verificar un usuario real.
    private static readonly string HashFicticio = PasswordHasher.HashPassword(new UsuarioAutenticacion(), Guid.NewGuid().ToString("N"));

    public async Task<(ResultadoInicioSesion Resultado, RespuestaInicioSesion? Respuesta)> IniciarSesionAsync(SolicitudInicioSesion solicitud, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(solicitud.Usuario) || string.IsNullOrEmpty(solicitud.Contrasena)) return (ResultadoInicioSesion.DatosInvalidos, null);
        var usuarioIngresado = solicitud.Usuario.Trim();
        if (usuarioIngresado.Length > 20) return (ResultadoInicioSesion.DatosInvalidos, null);
        var idCorrelacion = TrazaAgente.CorrelacionActual;
        Task Auditar(string? usuario, string evento, string resultado) => RegistrarAuditoriaAsync(usuario, usuarioIngresado, evento, resultado, idCorrelacion, ct);

        var claveIntentos = $"login-fallos:{usuarioIngresado.ToUpperInvariant()}";
        if (cache.TryGetValue(claveIntentos, out ContadorIntentos? previos) && previos!.Cantidad >= MaximoIntentosFallidos)
        {
            await Auditar(null, "LOGIN_BLOQUEO_TEMPORAL", "DENEGADO");
            return (ResultadoInicioSesion.DemasiadosIntentos, null);
        }
        async Task<(ResultadoInicioSesion, RespuestaInicioSesion?)> Fallido(string? usuario, string evento, string resultado)
        {
            await Auditar(usuario, evento, resultado);
            RegistrarIntentoFallido(claveIntentos);
            return (ResultadoInicioSesion.CredencialesIncorrectas, null);
        }

        UsuarioAutenticacion? usuario;
        if (identidadCorporativa.Disponible)
        {
            bool credencialesValidas;
            try { credencialesValidas = await identidadCorporativa.AutenticarAsync(usuarioIngresado, solicitud.Contrasena, ct); }
            catch (SqlException) { return (ResultadoInicioSesion.IdentidadCorporativaNoDisponible, null); }

            if (!credencialesValidas) return await Fallido(null, "LOGIN_CORPORATIVO_FALLIDO", "DENEGADO");
            var usuarioCorporativo = await identidadCorporativa.ObtenerUsuarioAsync(usuarioIngresado, ct);
            if (usuarioCorporativo is null)
            {
                await Auditar(null, "LOGIN_CORPORATIVO_SIN_DATOS", "DENEGADO");
                return (ResultadoInicioSesion.UsuarioSinConfiguracionLocal, null);
            }
            var local = await BuscarUsuarioAsync(usuarioIngresado, ct);
            if (local is null)
            {
                await Auditar(null, "LOGIN_SIN_CONFIGURACION_LOCAL", "DENEGADO");
                return (ResultadoInicioSesion.UsuarioSinConfiguracionLocal, null);
            }
            if (EsCuentaSistema(local)) return await Fallido(local.Usuario, "LOGIN_DENEGADO_CUENTA_SISTEMA", "DENEGADO");
            await SincronizarUsuarioCorporativoAsync(usuarioCorporativo, usuarioIngresado, ct);
            usuario = await BuscarUsuarioAsync(usuarioIngresado, ct);
        }
        else
        {
            usuario = await BuscarUsuarioAsync(usuarioIngresado, ct);
            if (usuario is null)
            {
                PasswordHasher.VerifyHashedPassword(new UsuarioAutenticacion(), HashFicticio, solicitud.Contrasena);
                return await Fallido(null, "LOGIN_FALLIDO", "DENEGADO");
            }
            // Una cuenta técnica nunca inicia sesión: se rechaza antes de comparar el hash y con el mismo mensaje genérico.
            if (EsCuentaSistema(usuario)) return await Fallido(usuario.Usuario, "LOGIN_DENEGADO_CUENTA_SISTEMA", "DENEGADO");
            PasswordVerificationResult verificacion;
            try { verificacion = PasswordHasher.VerifyHashedPassword(usuario, usuario.ClaveHash, solicitud.Contrasena); }
            catch (FormatException)
            {
                // Cuentas importadas del legado: tienen un marcador en lugar de hash y no pueden ingresar con contraseña local.
                logger.LogWarning("El usuario {Usuario} tiene un hash de contraseña con formato inválido; el intento se rechaza.", usuario.Usuario);
                return await Fallido(usuario.Usuario, "LOGIN_FALLIDO", "HASH_INVALIDO");
            }
            if (verificacion == PasswordVerificationResult.Failed) return await Fallido(usuario.Usuario, "LOGIN_FALLIDO", "DENEGADO");
        }

        if (usuario is null) return (ResultadoInicioSesion.UsuarioSinConfiguracionLocal, null);
        if (!string.Equals(usuario.EstadoUsuario, EstadoActivo, StringComparison.OrdinalIgnoreCase))
        {
            await Auditar(usuario.Usuario, "LOGIN_DENEGADO_USUARIO_INACTIVO", "DENEGADO");
            return (ResultadoInicioSesion.UsuarioInactivo, null);
        }
        if (!string.Equals(usuario.EstadoPerfil, EstadoActivo, StringComparison.OrdinalIgnoreCase))
        {
            await Auditar(usuario.Usuario, "LOGIN_DENEGADO_PERFIL_INACTIVO", "DENEGADO");
            return (ResultadoInicioSesion.PerfilInactivo, null);
        }
        cache.Remove(claveIntentos);
        await Auditar(usuario.Usuario, "LOGIN_EXITOSO", "EXITOSO");
        return (ResultadoInicioSesion.Correcto, new RespuestaInicioSesion { Usuario = usuario.Usuario, NombreCompleto = usuario.NombreCompleto, Area = usuario.Area, Perfil = usuario.Perfil });
    }

    public Task CerrarSesionAsync(string usuario, CancellationToken ct)
    {
        var usuarioNormalizado = usuario.Trim();
        if (usuarioNormalizado.Length == 0) throw new ArgumentException("El usuario autenticado es obligatorio.");
        cache.Remove(ClaveSesion(usuarioNormalizado));
        return RegistrarAuditoriaAsync(usuarioNormalizado, usuarioNormalizado, "LOGOUT_EXITOSO", "EXITOSO", TrazaAgente.CorrelacionActual, ct);
    }

    /// <summary>
    /// Una cookie emitida antes de desactivar al usuario, cambiar su perfil o su área deja de servir en minutos, no en horas:
    /// el usuario debe seguir activo, con perfil activo y con el mismo perfil y área que dicen sus claims. Si la base no responde,
    /// la sesión se conserva (la petición fallará igual por la base).
    /// </summary>
    public async Task<bool> SesionVigenteAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        var usuario = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var perfil = principal.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        var area = principal.FindFirstValue("Area") ?? string.Empty;
        if (usuario.Length == 0) return false;
        var clave = ClaveSesion(usuario);
        if (cache.TryGetValue(clave, out string? vigente) && vigente == $"{perfil}|{area}") return true;

        UsuarioAutenticacion? actual;
        try { actual = await BuscarUsuarioAsync(usuario, ct); }
        catch (SqlException ex)
        {
            logger.LogWarning("No se pudo revalidar la sesión de {Usuario}. Tipo {Tipo}.", usuario, ex.GetType().Name);
            return true;
        }
        var valida = actual is not null && !EsCuentaSistema(actual)
            && string.Equals(actual.EstadoUsuario, EstadoActivo, StringComparison.OrdinalIgnoreCase)
            && string.Equals(actual.EstadoPerfil, EstadoActivo, StringComparison.OrdinalIgnoreCase)
            && string.Equals(actual.Perfil, perfil, StringComparison.OrdinalIgnoreCase)
            && string.Equals(actual.Area, area, StringComparison.OrdinalIgnoreCase);
        if (valida) cache.Set(clave, $"{perfil}|{area}", VigenciaRevalidacion);
        return valida;
    }

    private static string ClaveSesion(string usuario) => $"sesion-vigente:{usuario.ToUpperInvariant()}";

    private static bool EsCuentaSistema(UsuarioAutenticacion usuario) => string.Equals(usuario.TipoUsuario, CuentaSistema, StringComparison.OrdinalIgnoreCase);

    private void RegistrarIntentoFallido(string clave)
    {
        // La ventana empieza con el primer fallo y no se extiende con los siguientes.
        var contador = cache.GetOrCreate(clave, entrada =>
        {
            entrada.AbsoluteExpirationRelativeToNow = VentanaIntentos;
            return new ContadorIntentos();
        })!;
        Interlocked.Increment(ref contador.Cantidad);
    }

    private sealed class ContadorIntentos
    {
        public int Cantidad;
    }

    private Task<UsuarioAutenticacion?> BuscarUsuarioAsync(string usuario, CancellationToken ct) =>
        baseDatos.LeerAsync("dbo.Usp_TI_Buscar_UsuarioAutenticacion", p => p.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario,
            lector => lector.FilaAsync(f => new UsuarioAutenticacion
            {
                Usuario = f.Texto("Usuario"), NombreCompleto = f.Texto("NombreCompleto"), ClaveHash = f.Texto("ClaveHash"), Area = f.Texto("Area"),
                Perfil = f.Texto("Perfil"), EstadoUsuario = f.Texto("EstadoUsuario"), EstadoPerfil = f.Texto("EstadoPerfil"),
                // Columna del script 40; sin él, ninguna cuenta se trata como SISTEMA.
                TipoUsuario = f.TieneColumna("TipoUsuario") ? f.Texto("TipoUsuario") : string.Empty
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
