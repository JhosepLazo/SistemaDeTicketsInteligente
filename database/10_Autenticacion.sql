/*
	Archivo: 10_Autenticacion.sql
	Objetivo: Crear los procedimientos almacenados requeridos por el backend de autenticación.
	Responsabilidad: Consultar los datos mínimos del usuario y registrar trazabilidad de los intentos de acceso.
	Dependencias: TI_Usuario, TI_Perfil y TI_Auditoria.
	Orden: Ejecutar después de 05_Auditoria.sql y de los maestros correspondientes.
	Consideraciones: No recibe contraseñas en texto plano; la comparación del hash se realiza exclusivamente en el backend.
*/

Use [SistemaTicketsInteligente]
Go

-- Exec dbo.Usp_TI_Buscar_UsuarioAutenticacion 'USR001'
Create Or Alter Procedure Usp_TI_Buscar_UsuarioAutenticacion
/*================================================================================
Objetivo            : Obtener los datos mínimos necesarios para validar la autenticación de un usuario.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : Sep. 2026
SP Anterior         : 
Comentario Cambios  : 
================================================================================*/
@cUsuario varchar(20)
As
Begin
	Set NoCount On

	Select u.Usuario, u.NombreCompleto, u.Clave as ClaveHash, u.Area, u.Perfil,
		u.Estado as EstadoUsuario, p.Estado as EstadoPerfil
	From dbo.TI_Usuario u
	Inner Join dbo.TI_Perfil p on p.Perfil = u.Perfil
	Where u.Usuario = @cUsuario
End
Go
/* Ejecuta Procedure */

-- Exec dbo.Usp_TI_Registrar_AuditoriaAutenticacion Null, 'USR001', 'LOGIN_FALLIDO', 'DENEGADO', '00000000-0000-0000-0000-000000000000'
Create Or Alter Procedure Usp_TI_Registrar_AuditoriaAutenticacion
/*================================================================================
Objetivo            : Registrar eventos de autenticación en la auditoría transversal del sistema.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : Sep. 2026
SP Anterior         : 
Comentario Cambios  : 
================================================================================*/
@cUsuario 		varchar(20),
@cRegistro 		varchar(200),
@cEvento 		varchar(100),
@cResultado 	varchar(20),
@cIdCorrelacion uniqueidentifier
As
Begin
	Set NoCount On

	Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
	Values (Null, @cUsuario, 'U', 'AUTENTICACION', @cRegistro, @cEvento, @cResultado, Null, @cIdCorrelacion, SysDateTime())
End
Go
/* Ejecuta Procedure */
