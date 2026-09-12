/**
    Archivo: AutenticacionRepository.cs
    Objetivo: Reservar el acceso a datos del futuro módulo de autenticación.
    Responsabilidad: Ejecutar los procedimientos almacenados relacionados con autenticación y convertir sus resultados en modelos internos utilizados por el backend.
    Dependencias: ConexionSqlServer y Microsoft.Data.SqlClient, pendientes de integración en este archivo.
    Flujo: AutenticacionService -> AutenticacionRepository -> ConexionSqlServer -> SQL Server.
    Consideraciones: No valida contraseñas ni decide si el usuario puede iniciar sesión. Los valores recibidos deben enviarse a SQL Server mediante parámetros.
*/

using System.Data;
using Microsoft.Data.SqlClient;
using SistemaTicketsInteligente.Api.Data;
using SistemaTicketsInteligente.Api.Models.Autenticacion;

namespace SistemaTicketsInteligente.Api.Repositories;

public class AutenticacionRepository
{
    private readonly ConexionSqlServer conexionSqlServer;

    public AutenticacionRepository(ConexionSqlServer conexionSqlServer)
    {
        this.conexionSqlServer = conexionSqlServer;
    }

    public async Task<UsuarioAutenticacion?> BuscarUsuarioAsync(string usuario)
    {
        await using var conexion = conexionSqlServer.CrearConexion();

        using var comando = new SqlCommand("Usp_TI_BuscarUsuarioAutenticacion", conexion)
        {
            CommandType = CommandType.StoredProcedure
        };

        comando.Parameters.Add("@cUsuario", SqlDbType.VarChar, 20).Value = usuario;

        await conexion.OpenAsync();
        await using var lector = await comando.ExecuteReaderAsync();

        if (!await lector.ReadAsync()) return null;

        return new UsuarioAutenticacion
        {
            Usuario = lector.GetString(0),
            NombreCompleto = lector.GetString(1),
            ClaveHash = lector.GetString(2),
            Area = lector.GetString(3),
            Perfil = lector.GetString(4),
            EstadoUsuario = lector.GetString(5),
            EstadoPerfil = lector.GetString(6)
        };
    }
}

