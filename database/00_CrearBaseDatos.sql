/*
    Archivo: 00_CrearBaseDatos.sql
    Objetivo: Crear la base de datos SistemaTicketsInteligente con el collation corporativo requerido.
    Responsabilidad: Inicializar únicamente el contenedor físico de la base de datos, sin crear tablas ni datos funcionales.
    Dependencias: SQL Server y permisos para crear bases de datos desde master.
    Orden: Primer script del proceso de instalación.
    Consideraciones: No reemplaza ni elimina una base existente; valida previamente su existencia mediante Db_Id().
*/

Use [master]
Go

If Db_Id(N'SistemaTicketsInteligente') Is Null
Begin
	-- Create Database debe ejecutarse como lote independiente; por eso se encapsula en Exec.
	Exec(N'
		Create Database [SistemaTicketsInteligente]
		Collate Modern_Spanish_CI_AS
	')

	Print N'Base de datos SistemaTicketsInteligente creada correctamente.'
End
Else
Begin
	Print N'La base de datos SistemaTicketsInteligente ya existe. No se realizaron cambios.'

	Select
			[name] as BaseDatos,
			[collation_name] as CollationActual,
			[state_desc] as EstadoActual
	From sys.databases
	Where [name] = N'SistemaTicketsInteligente'
End
Go