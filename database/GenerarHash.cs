#:sdk Microsoft.NET.Sdk.Web

/**
 * Archivo: GenerarHash.cs
 * Objetivo: Generar manualmente un hash compatible con ASP.NET Core Identity para una contraseña de desarrollo.
 * Responsabilidad: Servir como utilidad puntual cuando se necesite crear o cambiar una contraseña local fuera de los usuarios sintéticos ya preparados.
 * Dependencias: Microsoft.AspNetCore.Identity y .NET SDK con soporte para aplicaciones C# basadas en archivo.
 * Flujo: dotnet run database/GenerarHash.cs -> ingreso de contraseña (sin eco) y confirmación -> PasswordHasher -> hash mostrado en consola.
 * Consideraciones: No modifica la base de datos ni guarda contraseñas. La contraseña no se muestra al escribirla ni queda en el historial de
 *   la consola. Los usuarios DEV estándar ya se preparan mediante 13_CredencialesDesarrollo.sql, por lo que este archivo no debe ejecutarse
 *   en cada inicio del proyecto.
 */

using System.Text;
using Microsoft.AspNetCore.Identity;

var contrasena = LeerOculto("Contraseña temporal DEV: ");
if (string.IsNullOrWhiteSpace(contrasena))
{
    Console.WriteLine("La contraseña es obligatoria.");
    return;
}
if (LeerOculto("Repite la contraseña: ") != contrasena)
{
    Console.WriteLine("Las contraseñas no coinciden.");
    return;
}

var passwordHasher = new PasswordHasher<object>();
var hash = passwordHasher.HashPassword(new object(), contrasena);

Console.WriteLine();
Console.WriteLine(hash);

// Lee la contraseña sin mostrarla; si la entrada viene redirigida (por ejemplo, desde un archivo) no hay teclado que ocultar.
static string LeerOculto(string pregunta)
{
    Console.Write(pregunta);
    if (Console.IsInputRedirected) return Console.ReadLine() ?? string.Empty;
    var texto = new StringBuilder();
    while (true)
    {
        var tecla = Console.ReadKey(intercept: true);
        if (tecla.Key == ConsoleKey.Enter) break;
        if (tecla.Key == ConsoleKey.Backspace)
        {
            if (texto.Length > 0) texto.Length--;
            continue;
        }
        if (!char.IsControl(tecla.KeyChar)) texto.Append(tecla.KeyChar);
    }
    Console.WriteLine();
    return texto.ToString();
}
