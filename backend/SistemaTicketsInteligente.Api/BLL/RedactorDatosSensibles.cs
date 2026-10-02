/**
 * Archivo: RedactorDatosSensibles.cs
 * Objetivo: Ocultar secretos y datos personales antes de persistir evidencias o enviarlas a proveedores de IA.
 * Responsabilidad: Reemplazar contraseñas, tokens, tarjetas, correos, teléfonos y documentos de identidad por marcadores legibles.
 * Dependencias: System.Text.RegularExpressions.
 * Flujo: Evidencia Live / contexto de investigación / resultado de herramienta -> redacción -> almacenamiento o proveedor IA.
 * Consideraciones: Los secretos se ocultan siempre, incluso en la base de datos; los datos personales solo al salir hacia un proveedor externo,
 *   porque TI los necesita para atender el ticket. Los números de ticket (INC-/TKT-) no se tocan.
 */

using System.Text.RegularExpressions;

namespace SistemaTicketsInteligente.Api.BLL;

public static class RedactorDatosSensibles
{
    private const RegexOptions Opciones = RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;

    private static readonly Regex Credencial = new(@"\b(contrase(?:ñ|n)a|password|passwd|pwd|clave|pin|token|api[\s_-]?key|secret[oa]?)(\s*[:=]\s*)(""[^""]{1,100}""|\S{1,100})", Opciones);
    // "mi contraseña es X": solo con palabras inequívocas, para no ocultar frases como "la clave es revisar...".
    private static readonly Regex CredencialVerbal = new(@"\b(contrase(?:ñ|n)a|password)(\s+(?:es|era|ser[ií]a)\s+)(""[^""]{1,100}""|\S{1,100})", Opciones);
    private static readonly Regex Bearer = new(@"\bbearer\s+[A-Za-z0-9\-._~+/]{12,}=*", Opciones);
    private static readonly Regex ClaveProveedor = new(@"\b(?:sk-[A-Za-z0-9_\-]{16,}|AIza[0-9A-Za-z_\-]{30,}|gh[pousr]_[A-Za-z0-9]{20,}|xox[abp]-[A-Za-z0-9\-]{10,})", Opciones);
    private static readonly Regex Tarjeta = new(@"(?<![\w-])(?:\d[ -]?){12,18}\d(?!\w)", Opciones);
    private static readonly Regex Correo = new(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", Opciones);
    private static readonly Regex Telefono = new(@"(?<![\w-])(?:\+?51[\s-]?)?9\d{2}[\s-]?\d{3}[\s-]?\d{3}(?!\w)", Opciones);
    private static readonly Regex Documento = new(@"\b(dni|documento(?:\s+de\s+identidad)?|ruc|carn[eé]\s+de\s+extranjer[ií]a|pasaporte)(\s*(?:n[°ºo.]*\s*)?:?\s*)([A-Z]?\d{8,11})\b", Opciones);

    /// <summary>Oculta secretos (credenciales, tokens, tarjetas). Se aplica antes de guardar evidencia.</summary>
    public static string RedactarSecretos(string? texto) => RedactarSecretos(texto, out _);

    public static string RedactarSecretos(string? texto, out int ocultados)
    {
        ocultados = 0;
        if (string.IsNullOrEmpty(texto)) return texto ?? string.Empty;
        var contador = 0;
        var resultado = Credencial.Replace(texto, m => { contador++; return $"{m.Groups[1].Value}{m.Groups[2].Value}[SECRETO OCULTO]"; });
        resultado = CredencialVerbal.Replace(resultado, m => { contador++; return $"{m.Groups[1].Value}{m.Groups[2].Value}[SECRETO OCULTO]"; });
        resultado = Bearer.Replace(resultado, _ => { contador++; return "Bearer [SECRETO OCULTO]"; });
        resultado = ClaveProveedor.Replace(resultado, _ => { contador++; return "[SECRETO OCULTO]"; });
        resultado = Tarjeta.Replace(resultado, m =>
        {
            var digitos = new string(m.Value.Where(char.IsDigit).ToArray());
            if (digitos.Length is < 13 or > 19 || !CumpleLuhn(digitos)) return m.Value;
            contador++;
            return $"[TARJETA ****{digitos[^4..]}]";
        });
        ocultados = contador;
        return resultado;
    }

    /// <summary>Oculta secretos y datos personales. Se aplica a todo lo que sale hacia un proveedor de IA.</summary>
    public static string RedactarParaIA(string? texto) => RedactarParaIA(texto, out _);

    public static string RedactarParaIA(string? texto, out int ocultados)
    {
        var resultado = RedactarSecretos(texto, out var contador);
        if (resultado.Length == 0)
        {
            ocultados = 0;
            return resultado;
        }
        resultado = Correo.Replace(resultado, _ => { contador++; return "[CORREO]"; });
        resultado = Documento.Replace(resultado, m => { contador++; return $"{m.Groups[1].Value}{m.Groups[2].Value}[DOCUMENTO]"; });
        resultado = Telefono.Replace(resultado, _ => { contador++; return "[TELEFONO]"; });
        ocultados = contador;
        return resultado;
    }

    private static bool CumpleLuhn(string digitos)
    {
        var suma = 0;
        var duplicar = false;
        for (var i = digitos.Length - 1; i >= 0; i--)
        {
            var valor = digitos[i] - '0';
            if (duplicar && (valor *= 2) > 9) valor -= 9;
            suma += valor;
            duplicar = !duplicar;
        }
        return suma % 10 == 0;
    }
}
