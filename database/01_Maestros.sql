/*
    Archivo: 01_Maestros.sql
    Objetivo: Crear las entidades maestras que soportan la clasificación, organización, usuarios y relaciones base del Sistema de Tickets Inteligente.
    Responsabilidad: Definir maestros, claves primarias, claves únicas y relaciones referenciales necesarias para las entidades operativas posteriores.
    Dependencias: Requiere la base SistemaTicketsInteligente creada previamente.
    Orden: Ejecutar después de 00_CrearBaseDatos.sql y antes de cualquier tabla transaccional.
    Consideraciones: Las Foreign Keys se definen dentro del Create Table cuando la entidad padre ya existe, manteniendo cada tabla autocontenida y evitando Alter Table innecesarios.
*/

Use [SistemaTicketsInteligente]
Go

Set Xact_Abort on

Begin Try
	Begin Transaction

	Create Table dbo.TI_Area (
		Area					char(3)			Not Null,
		Descripcion				varchar(60)		Not Null,
		Estado					varchar(2)		Not Null,
		Telefono				varchar(20)		Null,
		UltimoUsuario			varchar(20)		Null,
		UltimaFechaModif		datetime2(0)	Null,

		Constraint PK_TI_Area
			Primary Key (Area)
	)

	Create Table dbo.TI_Linea (
		Linea					char(3)			Not Null,
		Area					char(3)			Not Null,
		Descripcion				varchar(60)		Not Null,
		Estado					varchar(2)		Not Null,
		UltimoUsuario			varchar(20)		Null,
		UltimaFechaModif		datetime2(0)	Null,

		Constraint PK_TI_Linea
			Primary Key (Linea),
		Constraint FK_TI_Linea_Area
			Foreign Key (Area)
			References dbo.TI_Area (Area)
	)

	Create Table dbo.TI_Cargo (
		Cargo					char(3)			Not Null,
		Descripcion				varchar(60)		Not Null,
		Estado					varchar(2)		Not Null,
		UltimoUsuario			varchar(20)		Null,
		UltimaFechaModif		datetime2(0)	Null,

		Constraint PK_TI_Cargo
			Primary Key (Cargo)
	)

	Create Table dbo.TI_Perfil (
		Perfil					char(3)			Not Null,
		Descripcion				varchar(60)		Not Null,
		Estado					varchar(2)		Not Null,
		UltimoUsuario			varchar(20)		Null,
		UltimaFechaModif		datetime2(0)	Null,

		Constraint PK_TI_Perfil
			Primary Key (Perfil)
	)

	Create Table dbo.TI_Usuario (
		Usuario					varchar(20)		Not Null,
		NombreCompleto			varchar(255)	Not Null,
		-- Debe almacenar únicamente un hash de contraseña cuando se utilice autenticación local.
		Clave					varchar(100)	Not Null,
		Area					char(3)			Not Null,
		Cargo					char(3)			Null,
		Perfil					char(3)			Not Null,
		Correo					varchar(100)	Null,
		Anexo					varchar(10)		Null,
		Telefono				varchar(100)	Null,
		Estado					varchar(2)		Not Null,
		UltimoUsuario			varchar(20)		Null,
		UltimaFechaModif		datetime2(0)	Null,
		TipoUsuario				varchar(20)		Null,
		Jefe					varchar(20)		Null,

		Constraint PK_TI_Usuario
			Primary Key (Usuario),
		Constraint FK_TI_Usuario_Area
			Foreign Key (Area)
			References dbo.TI_Area (Area),
		Constraint FK_TI_Usuario_Cargo
			Foreign Key (Cargo)
			References dbo.TI_Cargo (Cargo),
		Constraint FK_TI_Usuario_Perfil
			Foreign Key (Perfil)
			References dbo.TI_Perfil (Perfil),
		-- El jefe, cuando se informa, debe corresponder a otro usuario registrado.
		Constraint FK_TI_Usuario_Jefe
			Foreign Key (Jefe)
			References dbo.TI_Usuario (Usuario)
	)

	Create Table dbo.TI_Item (
		Item					varchar(20)		Not Null,
		Linea					char(3)			Not Null,
		Descripcion				varchar(255)	Not Null,
		Estado					varchar(2)		Not Null,
		UltimoUsuario			varchar(20)		Null,
		UltimaFechaModif		datetime2(0)	Null,

		Constraint PK_TI_Item
			Primary Key (Item),
		-- Esta clave permite validar Linea + Item desde TI_Incidencia.
		Constraint UQ_TI_Item_LineaItem
			Unique (Linea, Item),
		Constraint FK_TI_Item_Linea
			Foreign Key (Linea)
			References dbo.TI_Linea (Linea)
	)

	Create Table dbo.TI_Tipo (
		Tipo					char(3)			Not Null,
		Descripcion				varchar(60)		Not Null,
		Abreviatura				varchar(60)		Null,
		Item					varchar(20)		Null,
		Estado					varchar(2)		Not Null,
		UltimoUsuario			varchar(20)		Null,
		UltimaFechaModif		datetime2(0)	Null,

		Constraint PK_TI_Tipo
			Primary Key (Tipo)
	)

	Create Table dbo.TI_Categoria (
		Categoria				varchar(20)		Not Null,
		Descripcion				varchar(60)		Not Null,
		Abreviatura				varchar(3)		Null,
		Estado					varchar(2)		Not Null,
		UltimoUsuario			varchar(20)		Null,
		UltimaFechaModif		datetime2(0)	Null,

		Constraint PK_TI_Categoria
			Primary Key (Categoria)
	)

	Create Table dbo.TI_SubTipo (
		Tipo					char(3)			Not Null,
		SubTipo					char(3)			Not Null,
		Categoria				varchar(20)		Not Null,
		Descripcion				varchar(60)		Not Null,
		Abreviatura				varchar(60)		Null,
		Estado					varchar(2)		Not Null,
		UltimoUsuario			varchar(20)		Null,
		UltimaFechaModif		datetime2(0)	Null,

		Constraint PK_TI_SubTipo
			Primary Key (Tipo, SubTipo, Categoria),
		Constraint FK_TI_SubTipo_Tipo
			Foreign Key (Tipo)
			References dbo.TI_Tipo (Tipo),
		Constraint FK_TI_SubTipo_Categoria
			Foreign Key (Categoria)
			References dbo.TI_Categoria (Categoria)
	)

	Create Table dbo.TI_Estado (
		Estado					char(2)			Not Null,
		Descripcion				varchar(60)		Not Null,
		Orden					int				Not Null,
		UltimoUsuario			varchar(20)		Null,
		UltimaFechaModif		datetime2(0)	Null,

		Constraint PK_TI_Estado
			Primary Key (Estado)
	)

	Create Table dbo.TI_ItemCategoria (
		Item					varchar(20)		Not Null,
		Categoria				varchar(20)		Not Null,
		Prioridad				numeric(18,2)	Null,
		Impacto					numeric(18,2)	Null,
		Complejidad				numeric(18,2)	Null,
		Estado					varchar(2)		Not Null,
		UltimoUsuario			varchar(20)		Null,
		UltimaFechaModif		datetime2(0)	Null,

		Constraint PK_TI_ItemCategoria
			Primary Key (Item, Categoria),
		Constraint FK_TI_ItemCategoria_Item
			Foreign Key (Item)
			References dbo.TI_Item (Item),
		Constraint FK_TI_ItemCategoria_Categoria
			Foreign Key (Categoria)
			References dbo.TI_Categoria (Categoria)
	)

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