#:sdk Microsoft.NET.Sdk.Web

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