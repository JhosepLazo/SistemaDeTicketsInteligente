/*
	Archivo: 10_Autenticacion.sql
	Objetivo: Crear los procedimientos almacenados requeridos por el backend de autenticación.
	Responsabilidad: Consultar los datos mínimos del usuario y registrar trazabilidad de los intentos de acceso.
	Dependencias: TI_Usuario, TI_Perfil y TI_Auditoria.
	Orden: Ejecutar después de 05_Auditoria.sql y de los maestros correspondientes.
	Consideraciones: No recibe contraseñas en texto plano; la comparación del hash se realiza exclusivamente en el backend.
*/

Use [GestionSistemas]
Go

-- dbo.Usp_TI_Buscar_UsuarioAutenticacion: la versión vigente está en 40_SeguridadSesion.sql (aquí había una versión anterior que ese script reemplaza).
Go

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

