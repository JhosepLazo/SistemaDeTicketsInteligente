/*
	Archivo: 40_SeguridadSesion.sql
	Objetivo: Entregar a la autenticación lo que necesita para rechazar cuentas técnicas y revalidar sesiones abiertas.
	Responsabilidad: Agregar el tipo de usuario a la búsqueda de autenticación; la API la usa al iniciar sesión (una cuenta SISTEMA nunca
		inicia sesión) y cada pocos minutos para comprobar que el usuario, su perfil y su área siguen vigentes.
	Dependencias: Requiere 10_Autenticacion.sql y 34_DominiosYMaquinaEstados.sql (dominio de TipoUsuario).
	Orden: Ejecutar después de 39_CorreccionQuotedIdentifier.sql.
	Consideraciones: Solo lectura. El hash sigue comparándose en el backend, nunca en SQL.
*/

Use [GestionSistemas]
Go

Set Ansi_Nulls On
Set Quoted_Identifier On
Go

-- Usp_TI_Buscar_UsuarioAutenticacion 'USR001'
Create Or Alter Procedure Usp_TI_Buscar_UsuarioAutenticacion
/*================================================================================
Objetivo            : Obtener los datos mínimos necesarios para validar la autenticación de un usuario.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : Sep. 2026
SP Anterior         : Usp_TI_Buscar_UsuarioAutenticacion (10_Autenticacion.sql)
Comentario Cambios  : 07/10/2026 Devuelve TipoUsuario para rechazar el inicio de sesión de cuentas SISTEMA.
================================================================================*/

@cUsuario	varchar(20)

As
Begin
Set NoCount On

	Select u.Usuario, u.NombreCompleto, u.Clave as ClaveHash, u.Area, u.Perfil,
		u.Estado as EstadoUsuario, p.Estado as EstadoPerfil, TipoUsuario = IsNull(u.TipoUsuario, '')
	From TI_Usuario as u
	Inner Join TI_Perfil as p on p.Perfil = u.Perfil
	Where u.Usuario = @cUsuario

End
Go

/* Ejecuta Procedure */
