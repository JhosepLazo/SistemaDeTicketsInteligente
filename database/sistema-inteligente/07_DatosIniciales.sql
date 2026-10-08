/*
	Archivo: 07_DatosIniciales.sql
	Objetivo: Insertar los valores mínimos de configuración requeridos para iniciar la operación del nuevo sistema.
	Responsabilidad: Registrar únicamente catálogos iniciales propios del sistema que hayan sido previamente definidos y aprobados.
	Dependencias: Requiere los maestros correspondientes creados en 01_Maestros.sql.
	Orden: Ejecutar después de crear la estructura e índices principales.
	Consideraciones: No migra información histórica ni inventa códigos corporativos. Es el único responsable de cargar TI_Perfil
		(09_InsertDeDatos.sql ya no los inserta) y puede ejecutarse otra vez: solo agrega los perfiles que falten.
*/

Use [GestionSistemas]
Go

Set Xact_Abort on

-- A (activo) es el código que usan todos los procedimientos y la API para los registros vigentes; I marca los inactivos.
Declare @cEstadoActivo varchar(2) = 'A'

Begin Try
	Begin Transaction

	Insert Into dbo.TI_Perfil
	(
		Perfil,
		Descripcion,
		Estado
	)
	Select perfil.Perfil, perfil.Descripcion, @cEstadoActivo
	From (Values
		('USR', N'Usuario'),
		('TEC', N'Técnico TI'),
		('SUP', N'Supervisor TI'),
		('ADM', N'Administrador')
	) as perfil (Perfil, Descripcion)
	Where Not Exists (Select 1 From dbo.TI_Perfil as existente Where existente.Perfil = perfil.Perfil)

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
