/*
    Archivo: 00_PrepararGestionSistemas.sql
    Objetivo: Crear la base de datos GestionSistemas con el collation corporativo requerido.
    Responsabilidad: Inicializar únicamente el contenedor físico de la base de datos, sin crear tablas ni datos funcionales.
    Dependencias: SQL Server y permisos para crear bases de datos desde master.
    Orden: Primer script del proceso de instalación.
    Consideraciones: No reemplaza ni elimina una base existente; valida previamente su existencia mediante Db_Id().
*/

Use [master]
Go

If Db_Id(N'GestionSistemas') Is Null
Begin
	-- Create Database debe ejecutarse como lote independiente; por eso se encapsula en Exec.
	Exec(N'
		Create Database [GestionSistemas]
		Collate Modern_Spanish_CI_AS
	')

	Print N'Base de datos GestionSistemas creada correctamente.'
End
Else
Begin
	Print N'La base de datos GestionSistemas ya existe. No se realizaron cambios.'

	Select
			[name] as BaseDatos,
			[collation_name] as CollationActual,
			[state_desc] as EstadoActual
	From sys.databases
	Where [name] = N'GestionSistemas'
End
Go

-- Evita presión de memoria innecesaria en la instancia local de desarrollo.
Alter Database [GestionSistemas] Set Query_Store = Off
Go

