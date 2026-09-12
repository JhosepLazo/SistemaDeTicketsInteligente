/**
 * Archivo: UsuarioAutenticacion.cs
 * Objetivo: Representar internamente los datos recuperados de SQL Server para validar el inicio de sesión.
 * Responsabilidad: Transportar identidad, hash, área, perfil y estados desde DAO hacia BLL.
 * Dependencias: Usp_TI_Buscar_UsuarioAutenticacion.
 * Flujo: SQL Server -> AutenticacionDAO -> UsuarioAutenticacion -> AutenticacionBLL.
 * Consideraciones: Es un modelo interno y nunca debe enviarse al frontend porque contiene ClaveHash.
 */

namespace SistemaTicketsInteligente.Entidades.ComponenteDTO.Autenticacion;

public sealed class UsuarioAutenticacion
{
    public string Usuario { get; init; } = string.Empty;
    public string NombreCompleto { get; init; } = string.Empty;
    public string ClaveHash { get; init; } = string.Empty;
    public string Area { get; init; } = string.Empty;
    public string Perfil { get; init; } = string.Empty;
    public string EstadoUsuario { get; init; } = string.Empty;
    public string EstadoPerfil { get; init; } = string.Empty;
}
