#:sdk Microsoft.NET.Sdk.Web

/**
 * Archivo: GenerarHash.cs
 * Objetivo: Generar manualmente un hash compatible con ASP.NET Core Identity para una contraseña de desarrollo.
 * Responsabilidad: Servir como utilidad puntual cuando se necesite crear o cambiar una contraseña local fuera de los usuarios sintéticos ya preparados.
 * Dependencias: Microsoft.AspNetCore.Identity y .NET SDK con soporte para aplicaciones C# basadas en archivo.
 * Flujo: dotnet run database/GenerarHash.cs -> ingreso de contraseña -> PasswordHasher -> hash mostrado en consola.
 * Consideraciones: No modifica la base de datos ni guarda contraseñas. Los usuarios DEV estándar ya se preparan mediante 13_CredencialesDesarrollo.sql, por lo que este archivo no debe ejecutarse en cada inicio del proyecto.
 */

using Microsoft.AspNetCore.Identity;

Console.Write("Contraseña temporal DEV: ");
var contrasena = Console.ReadLine();

if (string.IsNullOrWhiteSpace(contrasena))
{
    Console.WriteLine("La contraseña es obligatoria.");
    return;
}

var passwordHasher = new PasswordHasher<object>();
var hash = passwordHasher.HashPassword(new object(), contrasena);

Console.WriteLine();
Console.WriteLine(hash);
