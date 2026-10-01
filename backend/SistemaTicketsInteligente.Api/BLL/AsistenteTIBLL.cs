/**
 * Asistente operativo para perfiles TI.
 * Las respuestas pueden usar IA, pero las acciones se construyen con reglas locales,
 * catálogos reales y un token firmado que exige confirmación explícita.
 */

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using SistemaTicketsInteligente.Api.DTO;

namespace SistemaTicketsInteligente.Api.BLL;

public sealed class AsistenteTIBLL
{
    private static readonly object ConfirmacionLock = new();
    private static readonly IReadOnlyDictionary<string, string> Perfiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["USR"] = "Usuario",
        ["TEC"] = "Operador TI",
        ["SUP"] = "Supervisor",
        ["ADM"] = "Administrador"
    };

    private readonly ConfiguracionTIBLL configuracionTI;
    private readonly OpenAIAsistenteClient openAI;
    private readonly ITimeLimitedDataProtector protector;
    private readonly IMemoryCache cache;

    public AsistenteTIBLL(
        ConfiguracionTIBLL configuracionTI,
        OpenAIAsistenteClient openAI,
        IDataProtectionProvider dataProtection,
        IMemoryCache cache)
    {
        this.configuracionTI = configuracionTI;
        this.openAI = openAI;
        this.cache = cache;
        protector = dataProtection.CreateProtector("SistemaTickets.AsistenteTI.Acciones.v1").ToTimeLimitedDataProtector();
    }

    public async Task<AsistenteTIRespuesta> ResponderAsync(string operador, AsistenteTISolicitud solicitud, CancellationToken ct)
    {
        var mensaje = solicitud.Mensaje?.Trim() ?? string.Empty;
        if (mensaje.Length < 3) throw new ArgumentException("Describe brevemente la consulta o acción que necesitas.");
        if (mensaje.Length > 1200) throw new ArgumentException("La consulta no puede superar los 1200 caracteres.");
        solicitud.Historial ??= [];
        solicitud.Historial = solicitud.Historial.TakeLast(10).ToList();
        if (solicitud.Historial.Any(x => string.IsNullOrWhiteSpace(x.Contenido) || x.Contenido.Length > 1800 || (x.Rol != "usuario" && x.Rol != "asistente")))
            throw new ArgumentException("El historial de la conversación no es válido.");

        var configuracion = await configuracionTI.ObtenerAsync(operador, ct);
        var textoConversacion = string.Join(" ", solicitud.Historial.Where(x => x.Rol == "usuario").TakeLast(4).Select(x => x.Contenido).Append(mensaje));

        if (EsSolicitudUsuario(textoConversacion))
            return PrepararUsuario(operador, textoConversacion, configuracion);

        var respuestaLocal = ResponderConConfiguracion(mensaje, configuracion);
        if (!openAI.EstaDisponible)
            return respuestaLocal;

        var instrucciones = """
            Eres el Asistente Operativo TI de Calimod. Responde en español profesional, claro y breve.
            Usa el contexto como única fuente para datos internos. No inventes usuarios, áreas, métricas ni acciones ejecutadas.
            Nunca solicites contraseñas ni códigos. No entregues SQL, comandos administrativos ni datos personales sensibles.
            Para crear o sincronizar usuarios, indica que el flujo seguro requiere usuario Spring, área, perfil y confirmación explícita.
            La ejecución de acciones pertenece exclusivamente al backend; jamás afirmes que realizaste un cambio.
            """;
        var entrada = ConstruirContexto(mensaje, solicitud.Historial, configuracion);
        var generada = await openAI.GenerarAsync(instrucciones, entrada, ct);
        if (string.IsNullOrWhiteSpace(generada)) return respuestaLocal;

        respuestaLocal.Respuesta = generada;
        respuestaLocal.Modo = "IA";
        return respuestaLocal;
    }

    public async Task<ConfirmarAccionTIRespuesta> ConfirmarAsync(string operador, ConfirmarAccionTISolicitud solicitud, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(solicitud.TokenConfirmacion)) throw new ArgumentException("La confirmación de la acción es obligatoria.");

        AccionFirmada accion;
        try
        {
            var contenido = protector.Unprotect(solicitud.TokenConfirmacion, out _);
            accion = JsonSerializer.Deserialize<AccionFirmada>(contenido) ?? throw new CryptographicException();
        }
        catch (CryptographicException)
        {
            throw new InvalidOperationException("La propuesta venció o no es válida. Solicita al asistente que la prepare nuevamente.");
        }

        if (!string.Equals(accion.Operador, operador, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("La propuesta pertenece a otra sesión de usuario.");
        if (!string.Equals(accion.Tipo, "SINCRONIZAR_USUARIO", StringComparison.Ordinal))
            throw new InvalidOperationException("La acción solicitada no está habilitada.");

        var huella = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(solicitud.TokenConfirmacion)));
        var claveConfirmacion = $"asistente-ti:{huella}";
        lock (ConfirmacionLock)
        {
            if (cache.TryGetValue(claveConfirmacion, out _))
                throw new InvalidOperationException("Esta acción ya fue procesada.");

            cache.Set(claveConfirmacion, true, TimeSpan.FromMinutes(15));
        }
        try
        {
            await configuracionTI.SincronizarUsuarioCorporativoAsync(operador, new SincronizarUsuarioCorporativoSolicitud
            {
                Usuario = accion.Usuario,
                Area = accion.Area,
                Perfil = accion.Perfil,
                Correo = string.IsNullOrWhiteSpace(accion.Correo) ? null : accion.Correo,
                Estado = accion.Estado
            }, ct);
        }
        catch
        {
            cache.Remove(claveConfirmacion);
            throw;
        }

        return new ConfirmarAccionTIRespuesta
        {
            Tipo = accion.Tipo,
            Registro = accion.Usuario,
            Mensaje = $"El usuario corporativo {accion.Usuario} fue sincronizado correctamente con el perfil {PerfilDescripcion(accion.Perfil)}."
        };
    }

    private AsistenteTIRespuesta PrepararUsuario(string operador, string texto, ConfiguracionTIRespuesta configuracion)
    {
        var usuario = ExtraerUsuario(texto);
        var area = ExtraerArea(texto, configuracion.Areas);
        var perfil = ExtraerPerfil(texto);
        var correo = Regex.Match(texto, @"[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}", RegexOptions.IgnoreCase).Value;
        var faltantes = new List<string>();
        if (string.IsNullOrWhiteSpace(usuario)) faltantes.Add("usuario Spring");
        if (area is null) faltantes.Add("área");
        if (string.IsNullOrWhiteSpace(perfil)) faltantes.Add("perfil");

        if (faltantes.Count > 0)
        {
            return new AsistenteTIRespuesta
            {
                Respuesta = $"Puedo preparar el alta corporativa, pero todavía necesito: {string.Join(", ", faltantes)}. Indícalos en un mensaje; el correo es opcional.",
                Fuentes = ["Maestros TI", "Identidad corporativa Spring"],
                Sugerencias = ["Ver áreas disponibles", "Ver perfiles disponibles"],
                Accion = new AsistenteTIAccion { Tipo = "SINCRONIZAR_USUARIO", Titulo = "Sincronizar usuario corporativo", Faltantes = faltantes }
            };
        }

        var existente = configuracion.Usuarios.FirstOrDefault(x => string.Equals(x.Usuario, usuario, StringComparison.OrdinalIgnoreCase));
        var propuesta = new AsistenteTIUsuarioPropuesto
        {
            Usuario = usuario,
            Area = area!.Area,
            AreaDescripcion = area.Descripcion,
            Perfil = perfil,
            PerfilDescripcion = PerfilDescripcion(perfil),
            Correo = correo,
            Estado = "A",
            YaExiste = existente is not null
        };
        var firmada = new AccionFirmada("SINCRONIZAR_USUARIO", operador, propuesta.Usuario, propuesta.Area, propuesta.Perfil, propuesta.Correo, propuesta.Estado);
        var expira = DateTimeOffset.UtcNow.AddMinutes(10);
        var token = protector.Protect(JsonSerializer.Serialize(firmada), expira);

        return new AsistenteTIRespuesta
        {
            Respuesta = existente is null
                ? "Preparé el alta con los catálogos vigentes. Revisa los datos antes de confirmar; la identidad debe existir en Spring."
                : "Ese usuario ya está configurado. Si confirmas, se sincronizará nuevamente y se actualizarán el área, perfil, correo y estado indicados.",
            Fuentes = ["Maestros TI", "Identidad corporativa Spring"],
            Sugerencias = ["Ver áreas disponibles", "Ver perfiles disponibles"],
            Accion = new AsistenteTIAccion
            {
                Tipo = "SINCRONIZAR_USUARIO",
                Titulo = existente is null ? "Crear usuario corporativo" : "Actualizar usuario corporativo",
                TokenConfirmacion = token,
                ExpiraEn = expira,
                Usuario = propuesta
            }
        };
    }

    private static AsistenteTIRespuesta ResponderConConfiguracion(string mensaje, ConfiguracionTIRespuesta configuracion)
    {
        var texto = Normalizar(mensaje);
        string respuesta;
        if (texto.Contains("sla") || texto.Contains("tiempo objetivo") || texto.Contains("tiempo de atencion"))
        {
            var niveles = configuracion.Sla.Where(x => x.Estado == "A").OrderByDescending(x => x.Prioridad)
                .Select(x => $"Prioridad {x.Prioridad}: {FormatearDuracion(x.SlaObjetivoMinutos)}");
            respuesta = $"SLA activos por prioridad:\n\n{string.Join("\n", niveles)}";
        }
        else if (texto.Contains("categoria"))
        {
            respuesta = ResumirCatalogo("categorías activas", configuracion.Categorias.Where(x => x.Estado == "A").Select(x => $"{x.Categoria} · {x.Descripcion}"));
        }
        else if (texto.Contains("tipo de ticket") || texto.Contains("tipos de ticket") || texto.Contains("tipologia"))
        {
            respuesta = ResumirCatalogo("tipos de ticket activos", configuracion.Tipos.Where(x => x.Estado == "A").Select(x => $"{x.Tipo} · {x.Descripcion}"));
        }
        else if (texto.Contains("linea"))
        {
            respuesta = ResumirCatalogo("líneas activas", configuracion.Lineas.Where(x => x.Estado == "A").Select(x => $"{x.Linea} · {x.Descripcion} (area {x.Area})"));
        }
        else if (texto.Contains("item") || texto.Contains("servicio"))
        {
            respuesta = ResumirCatalogo("ítems o servicios activos", configuracion.Items.Where(x => x.Estado == "A").Select(x => $"{x.Item} · {x.Descripcion} (línea {x.Linea})"));
        }
        else if (texto.Contains("formato") || texto.Contains("plantilla"))
        {
            respuesta = ResumirCatalogo("formatos de soporte activos", configuracion.Formatos.Where(x => x.Estado == "A").Select(x => $"{x.FormatoCodigo} · {x.Titulo}"));
        }
        else if (texto.Contains("conocimiento") || texto.Contains("articulo") || texto.Contains("guia"))
        {
            var publicados = configuracion.Conocimientos.Where(x => x.Estado == "A");
            respuesta = ResumirCatalogo("artículos activos de la base de conocimiento", publicados.Select(x => $"{x.ConocimientoCodigo} · {x.Titulo}{(x.VisibleUsuario ? " · visible para usuarios" : " · solo TI")}"));
        }
        else if (texto.Contains("area"))
        {
            respuesta = ResumirCatalogo("áreas activas disponibles para asignación", configuracion.Areas.Where(x => x.Estado == "A").Select(x => $"{x.Area} · {x.Descripcion}"));
        }
        else if (texto.Contains("perfil") || texto.Contains("rol"))
        {
            respuesta = "Perfiles habilitados:\n\nUSR · Usuario\nTEC · Operador TI\nSUP · Supervisor\nADM · Administrador";
        }
        else if (texto.Contains("usuario"))
        {
            var activos = configuracion.Usuarios.Count(x => x.Estado == "A");
            var porPerfil = configuracion.Usuarios.GroupBy(x => x.Perfil).OrderBy(x => x.Key).Select(x => $"{x.Key}: {x.Count()}");
            respuesta = $"Hay {configuracion.Usuarios.Count} usuarios configurados, {activos} activos. Distribución actual: {string.Join(" · ", porPerfil)}. Para crear uno escribe, por ejemplo: Crea el usuario JPEREZ en el área 021 con perfil USR.";
        }
        else
        {
            respuesta = $"Puedo consultar la configuración vigente y preparar acciones seguras. Actualmente hay {configuracion.Areas.Count(x => x.Estado == "A")} áreas, {configuracion.Lineas.Count(x => x.Estado == "A")} líneas, {configuracion.Items.Count(x => x.Estado == "A")} ítems, {configuracion.Categorias.Count(x => x.Estado == "A")} categorías y {configuracion.Usuarios.Count(x => x.Estado == "A")} usuarios activos. También puedo preparar la creación o actualización de un usuario corporativo.";
        }

        return new AsistenteTIRespuesta
        {
            Respuesta = respuesta,
            Fuentes = ["Configuración vigente de Gestión TI"],
            Sugerencias = ["Crear un usuario", "Ver áreas disponibles", "Ver SLA activos"]
        };
    }

    private static string ResumirCatalogo(string titulo, IEnumerable<string> valores)
    {
        var lista = valores.OrderBy(x => x).ToList();
        if (lista.Count == 0) return $"No hay registros para {titulo} en la configuración vigente.";

        const int maximo = 40;
        var visibles = lista.Take(maximo);
        var complemento = lista.Count > maximo ? $"\n\nSe muestran {maximo} de {lista.Count} registros." : string.Empty;
        return $"Configuración vigente: {titulo}.\n\n{string.Join("\n", visibles)}{complemento}";
    }

    private static string FormatearDuracion(int minutos)
    {
        if (minutos < 60) return $"{minutos} min";
        if (minutos % 1440 == 0) return $"{minutos / 1440} día(s)";
        if (minutos % 60 == 0) return $"{minutos / 60} h";
        return $"{minutos / 60} h {minutos % 60} min";
    }

    private static string ConstruirContexto(string mensaje, IEnumerable<AsistenteUsuarioMensaje> historial, ConfiguracionTIRespuesta configuracion)
    {
        var texto = new StringBuilder();
        texto.AppendLine("CONFIGURACIÓN AUTORIZADA:");
        texto.AppendLine($"Áreas activas: {string.Join("; ", configuracion.Areas.Where(x => x.Estado == "A").Select(x => $"{x.Area}={x.Descripcion}"))}");
        texto.AppendLine($"Líneas activas: {string.Join("; ", configuracion.Lineas.Where(x => x.Estado == "A").Take(30).Select(x => $"{x.Linea}={x.Descripcion}"))}");
        texto.AppendLine($"Tipos activos: {string.Join("; ", configuracion.Tipos.Where(x => x.Estado == "A").Select(x => $"{x.Tipo}={x.Descripcion}"))}");
        texto.AppendLine($"Categorías activas: {string.Join("; ", configuracion.Categorias.Where(x => x.Estado == "A").Select(x => $"{x.Categoria}={x.Descripcion}"))}");
        texto.AppendLine($"SLA activos: {string.Join("; ", configuracion.Sla.Where(x => x.Estado == "A").Select(x => $"P{x.Prioridad}={x.SlaObjetivoMinutos} minutos"))}");
        texto.AppendLine($"Totales activos: {configuracion.Items.Count(x => x.Estado == "A")} items; {configuracion.Formatos.Count(x => x.Estado == "A")} formatos; {configuracion.Conocimientos.Count(x => x.Estado == "A")} artículos.");
        texto.AppendLine($"Usuarios configurados: {configuracion.Usuarios.Count}; activos: {configuracion.Usuarios.Count(x => x.Estado == "A")}.");
        texto.AppendLine("Perfiles: USR=Usuario; TEC=Operador TI; SUP=Supervisor; ADM=Administrador.");
        texto.AppendLine("CONVERSACIÓN:");
        foreach (var item in historial.TakeLast(6)) texto.AppendLine($"{item.Rol}: {item.Contenido}");
        texto.AppendLine($"usuario: {mensaje}");
        return texto.ToString();
    }

    private static bool EsSolicitudUsuario(string texto)
    {
        var normalizado = Normalizar(texto);
        return (normalizado.Contains("crear") || normalizado.Contains("crea ") || normalizado.Contains("registrar") || normalizado.Contains("agregar") || normalizado.Contains("anadir") || normalizado.Contains("sincronizar") || normalizado.Contains("dar de alta"))
            && (normalizado.Contains("usuario") || normalizado.Contains("cuenta"));
    }

    private static string ExtraerUsuario(string texto)
    {
        var match = Regex.Match(texto.ToUpperInvariant(), @"(?:USUARIO|CUENTA)\s+(?:SPRING\s+)?([A-Z0-9._-]{3,20})");
        if (!match.Success) return string.Empty;
        var valor = match.Groups[1].Value;
        return new[] { "CON", "DEL", "PARA", "EN", "AREA", "ÁREA", "PERFIL" }.Contains(valor) ? string.Empty : valor;
    }

    private static ConfiguracionTIArea? ExtraerArea(string texto, IEnumerable<ConfiguracionTIArea> areas)
    {
        var activas = areas.Where(x => x.Estado == "A").ToList();
        var match = Regex.Match(texto.ToUpperInvariant(), @"[ÁA]REA\s+(?:DE\s+)?([A-Z0-9]{1,3})\b");
        if (match.Success)
        {
            var porCodigo = activas.FirstOrDefault(x => string.Equals(x.Area, match.Groups[1].Value, StringComparison.OrdinalIgnoreCase));
            if (porCodigo is not null) return porCodigo;
        }

        var normalizado = Normalizar(texto);
        return activas.OrderByDescending(x => x.Descripcion.Length).FirstOrDefault(x => x.Descripcion.Length >= 4 && normalizado.Contains(Normalizar(x.Descripcion)));
    }

    private static string ExtraerPerfil(string texto)
    {
        var normalizado = Normalizar(texto);
        var codigo = Regex.Match(normalizado.ToUpperInvariant(), @"\b(USR|TEC|SUP|ADM)\b");
        if (codigo.Success) return codigo.Value;
        if (normalizado.Contains("administrador")) return "ADM";
        if (normalizado.Contains("supervisor")) return "SUP";
        if (normalizado.Contains("tecnico") || normalizado.Contains("operador ti")) return "TEC";
        if (normalizado.Contains("colaborador") || normalizado.Contains("usuario final") || Regex.IsMatch(normalizado, @"perfil\s+(?:de\s+)?(?:usuario|normal)")) return "USR";
        return string.Empty;
    }

    private static string PerfilDescripcion(string perfil) => Perfiles.TryGetValue(perfil, out var descripcion) ? descripcion : perfil;

    private static string Normalizar(string texto)
    {
        var descompuesto = (texto ?? string.Empty).ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return new string(descompuesto.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).Normalize(NormalizationForm.FormC);
    }

    private sealed record AccionFirmada(string Tipo, string Operador, string Usuario, string Area, string Perfil, string Correo, string Estado);
}
