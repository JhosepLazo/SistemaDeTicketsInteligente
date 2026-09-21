/*
	Archivo: 07_DatosIniciales.sql
	Objetivo: Insertar los valores mínimos de configuración requeridos para iniciar la operación del nuevo sistema.
	Responsabilidad: Registrar únicamente catálogos iniciales propios del sistema que hayan sido previamente definidos y aprobados.
	Dependencias: Requiere los maestros correspondientes creados en 01_Maestros.sql.
	Orden: Ejecutar después de crear la estructura e índices principales.
	Consideraciones: No migra información histórica ni inventa códigos corporativos desconocidos; cualquier valor pendiente debe permanecer sin ejecutar hasta ser confirmado.
*/

Use [GestionSistemas]
Go

Set Xact_Abort on

-- Debe definirse con el código corporativo que represente un registro activo.
Declare @cEstadoActivo varchar(2) = Null

If NullIf(LTrim(RTrim(@cEstadoActivo)), '') Is Null
Begin
	;Throw 50015, 'Debe definir el valor de @cEstadoActivo antes de insertar los perfiles.', 1
End

If Exists
(
	Select 1
	From dbo.TI_Perfil
	Where Perfil In ('USR', 'TEC', 'SUP', 'ADM')
)
Begin
	;Throw 50016, 'Uno o más perfiles iniciales ya existen. No se realizaron cambios.', 1
End

Begin Try
	Begin Transaction

	Insert Into dbo.TI_Perfil
	(
		Perfil,
		Descripcion,
		Estado
	)
	Values
		('USR', N'Usuario', @cEstadoActivo),
		('TEC', N'Técnico TI', @cEstadoActivo),
		('SUP', N'Supervisor TI', @cEstadoActivo),
		('ADM', N'Administrador', @cEstadoActivo)

	Commit Transaction
End Try
Begin Catch
	If Xact_State() <> 0
	Begin
		Rollback Transaction
	End

	;Throw
End Catch
Go
