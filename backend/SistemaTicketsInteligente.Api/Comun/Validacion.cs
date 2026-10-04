/**
 * Archivo: Validacion.cs
 * Objetivo: Validar la entrada de los módulos con las mismas reglas y mensajes en todo el sistema.
 * Responsabilidad: Recortar espacios, exigir largos y formatos y devolver el valor normalizado, o lanzar ArgumentException
 *   (el controlador la devuelve como 400 con su mensaje).
 * Dependencias: Ninguna.
 * Flujo: Controller -> BLL -> Validacion -> BLL continúa con valores limpios -> Stored Procedure.
 * Consideraciones: Los textos reciben el campo con su artículo ("El título", "La descripción") para que el mensaje quede bien escrito.
 *   El Stored Procedure vuelve a validar las reglas de negocio; aquí solo se filtra lo que nunca debe llegar a la base.
 */

namespace SistemaTicketsInteligente.Api.Comun;

public static class Validacion
{
    /// <summary>Usuario autenticado (viene de la cookie): obligatorio y de hasta 20 caracteres.</summary>
    public static string Usuario(string valor) => Requerido(valor, 20, "El usuario autenticado no es válido.");

    /// <summary>Área del usuario autenticado: código de 3 caracteres.</summary>
    public static string Area(string valor) => CodigoExacto(valor, 3, "El área del operador autenticado no es válida.");

    /// <summary>Número de ticket en mayúsculas (INC-000001, TKT-00042671).</summary>
    public static string Incidencia(string valor, int minimo = 1)
    {
        var numero = valor.Trim().ToUpperInvariant();
        if (numero.Length < minimo || numero.Length > 12) throw new ArgumentException("El número de ticket no es válido.");
        return numero;
    }

    /// <summary>Valor obligatorio de hasta <paramref name="maximo"/> caracteres; solo se recortan los espacios.</summary>
    public static string Requerido(string valor, int maximo, string mensaje)
    {
        var texto = valor.Trim();
        if (texto.Length == 0 || texto.Length > maximo) throw new ArgumentException(mensaje);
        return texto;
    }

    /// <summary>Código obligatorio en mayúsculas de hasta <paramref name="maximo"/> caracteres.</summary>
    public static string Codigo(string valor, int maximo, string mensaje)
    {
        var codigo = valor.Trim().ToUpperInvariant();
        if (codigo.Length == 0 || codigo.Length > maximo) throw new ArgumentException(mensaje);
        return codigo;
    }

    /// <summary>Código en mayúsculas de largo exacto (líneas, tipos y áreas son char(3)).</summary>
    public static string CodigoExacto(string valor, int largo, string mensaje)
    {
        var codigo = valor.Trim().ToUpperInvariant();
        if (codigo.Length != largo) throw new ArgumentException(mensaje);
        return codigo;
    }

    /// <summary>Texto obligatorio entre <paramref name="minimo"/> y <paramref name="maximo"/> caracteres.</summary>
    public static string Texto(string valor, int minimo, int maximo, string campo)
    {
        var texto = valor.Trim();
        if (texto.Length < minimo || texto.Length > maximo) throw new ArgumentException($"{campo} debe contener entre {minimo} y {maximo} caracteres.");
        return texto;
    }

    /// <summary>Texto opcional: null si viene vacío.</summary>
    public static string? Opcional(string? valor, int maximo, string campo)
    {
        var texto = valor?.Trim();
        if (string.IsNullOrEmpty(texto)) return null;
        if (texto.Length > maximo) throw new ArgumentException($"{campo} no puede superar los {maximo} caracteres.");
        return texto;
    }

    /// <summary>Prioridad, impacto o complejidad: nivel de 1 a 5 (null si es opcional y no se indicó).</summary>
    public static void Nivel(int? valor, string campo)
    {
        if (valor is < 1 or > 5) throw new ArgumentException($"{campo} debe estar entre 1 y 5.");
    }

    /// <summary>Estado de un registro maestro: A (activo) o I (inactivo).</summary>
    public static string Estado(string valor)
    {
        var estado = valor.Trim().ToUpperInvariant();
        if (estado is not ("A" or "I")) throw new ArgumentException("El estado debe ser A o I.");
        return estado;
    }
}
