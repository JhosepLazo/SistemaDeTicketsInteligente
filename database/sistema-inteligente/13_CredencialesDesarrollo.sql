/*
	Archivo: 13_CredencialesDesarrollo.sql
	Objetivo: Reemplazar únicamente los marcadores de contraseña de los usuarios sintéticos por hashes válidos para pruebas locales.
	Responsabilidad: Permitir que USR001, TEC001, SUP001 y ADM001 puedan autenticarse en desarrollo sin generar manualmente un hash después de cada recreación de la base.
	Dependencias: TI_Usuario y los usuarios de prueba registrados por 09_InsertDeDatos.sql.
	Orden: Ejecutar una vez después de 09_InsertDeDatos.sql cuando se prepare o recree una base de desarrollo.
	Consideraciones: Script exclusivo de desarrollo. La contraseña temporal de los usuarios sintéticos es 123456. Solo reemplaza HASH_DEMO_NO_VALIDO y nunca sobrescribe una contraseña que ya haya sido configurada.
*/

Use [GestionSistemas]
Go

Begin Try
	Begin Transaction

	Update dbo.TI_Usuario
	Set Clave = Case Usuario
		When 'USR001' Then 'AQAAAAIAAYagAAAAEKUTRGJbixNEhfNtBo0XAAv9HwlHTKn3JgNWYcBwZCtFhbCVJsC1S3+SHKq5fvvvxg=='
		When 'TEC001' Then 'AQAAAAIAAYagAAAAEGhbeIXpZo983giacEp6bu54O/sCWC+n7gQEF88aCgyWmErxRtYbG1CBgnMj8u2Btw=='
		When 'SUP001' Then 'AQAAAAIAAYagAAAAEG2I1gqWv31Ad2g+JHzn3r5dExGA9jjSf3lSSJeMoA9bx7FvDzOg3M7fVeCbfimBcw=='
		When 'ADM001' Then 'AQAAAAIAAYagAAAAECzEMAvBqs3upuM3A63Kb2AWQsZ08HxQg9g3beIJYRf+GI09gy7GYbjLtdMZtyJAtA=='
		Else Clave
	End,
	UltimoUsuario = 'DEV_SETUP',
	UltimaFechaModif = SysDateTime()
	Where Usuario In ('USR001', 'TEC001', 'SUP001', 'ADM001') and Clave = 'HASH_DEMO_NO_VALIDO'

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

/* Ejecuta Script */
-- Select Usuario, NombreCompleto, Perfil, Estado From dbo.TI_Usuario Where Usuario In ('USR001', 'TEC001', 'SUP001', 'ADM001')

