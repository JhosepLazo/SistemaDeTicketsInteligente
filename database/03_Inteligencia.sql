/*
	Archivo: 03_Inteligencia.sql
	Objetivo: Crear las estructuras destinadas al conocimiento reutilizable, diagnóstico inteligente y evidencia utilizada para sustentar cada diagnóstico.
	Responsabilidad: Definir la base de conocimiento, los diagnósticos asociados a incidencias y las fuentes o evidencias que respaldan sus resultados.
	Dependencias: Requiere TI_Incidencia y los maestros de clasificación previamente creados.
	Orden: Ejecutar después de 02_Incidencias.sql.
	Consideraciones: Este archivo almacena conocimiento y resultados de IA, pero no implementa embeddings, modelos, prompts ni ejecución directa sobre bases de datos.
*/

Use [SistemaTicketsInteligente]
Go

Set Xact_Abort on

Begin Try
	Begin Transaction

	Create Table dbo.TI_BaseConocimiento (
		ConocimientoCodigo			varchar(20)		Not Null,
		Titulo						nvarchar(250)	Not Null,
		Problema					nvarchar(max)	Not Null,
		Sintomas					nvarchar(max)	Not Null,
		MensajeError				nvarchar(1000)	Null,
		Causa						nvarchar(max)	Not Null,
		Solucion					nvarchar(max)	Not Null,
		Procedimiento				nvarchar(max)	Null,
		Linea						char(3)			Null,
		Item						varchar(20)		Null,
		Tipo						char(3)			Null,
		SubTipo						char(3)			Null,
		Categoria					varchar(20)		Null,
		IncidenciaOrigen			varchar(12)		Null,
		Estado						varchar(2)		Not Null,
		UsuarioValida				varchar(20)		Null,
		FechaCreacion				datetime2(0)	Not Null,
		FechaValidacion				datetime2(0)	Null,
		FechaRevision				datetime2(0)	Null,

		Constraint PK_TI_BaseConocimiento
			Primary Key (ConocimientoCodigo),
		Constraint FK_TI_BaseConocimiento_IncidenciaOrigen
			Foreign Key (IncidenciaOrigen)
			References dbo.TI_Incidencia (IncidenciaNumero),
		Constraint FK_TI_BaseConocimiento_UsuarioValida
			Foreign Key (UsuarioValida)
			References dbo.TI_Usuario (Usuario),
		Constraint FK_TI_BaseConocimiento_Linea
			Foreign Key (Linea)
			References dbo.TI_Linea (Linea),
		Constraint FK_TI_BaseConocimiento_Item
			Foreign Key (Item)
			References dbo.TI_Item (Item),
		Constraint FK_TI_BaseConocimiento_LineaItem
			Foreign Key (Linea, Item)
			References dbo.TI_Item (Linea, Item),
		Constraint FK_TI_BaseConocimiento_Tipo
			Foreign Key (Tipo)
			References dbo.TI_Tipo (Tipo),
		Constraint FK_TI_BaseConocimiento_TipoSubTipoCategoria
			Foreign Key (Tipo, SubTipo, Categoria)
			References dbo.TI_SubTipo (Tipo, SubTipo, Categoria),
		Constraint FK_TI_BaseConocimiento_Categoria
			Foreign Key (Categoria)
			References dbo.TI_Categoria (Categoria),
		Constraint FK_TI_BaseConocimiento_ItemCategoria
			Foreign Key (Item, Categoria)
			References dbo.TI_ItemCategoria (Item, Categoria)
	)

	Create Table dbo.TI_IncidenciaDiagnostico (
		IncidenciaNumero			varchar(12)		Not Null,
		Secuencia					int				Not Null,
		Origen						char(1)			Not Null,
		Diagnostico					nvarchar(max)	Not Null,
		CausaProbable				nvarchar(max)	Not Null,
		SolucionSugerida			nvarchar(max)	Not Null,
		Confianza					decimal(5,2)	Null,
		Estado						varchar(2)		Not Null,
		UsuarioValida				varchar(20)		Null,
		FechaDiagnostico			datetime2(0)	Not Null,

		Constraint PK_TI_IncidenciaDiagnostico
			Primary Key (IncidenciaNumero, Secuencia),
		Constraint FK_TI_IncidenciaDiagnostico_Incidencia
			Foreign Key (IncidenciaNumero)
			References dbo.TI_Incidencia (IncidenciaNumero),
		Constraint FK_TI_IncidenciaDiagnostico_UsuarioValida
			Foreign Key (UsuarioValida)
			References dbo.TI_Usuario (Usuario),
		-- I = Inteligencia Artificial, T = Técnico.
		Constraint CK_TI_IncidenciaDiagnostico_Origen
			Check (Origen In ('I', 'T')),
		Constraint CK_TI_IncidenciaDiagnostico_Confianza
			Check (Confianza Is Null or Confianza Between 0 and 100)
	)

	Create Table dbo.TI_IncidenciaDiagnosticoEvidencia (
		IncidenciaNumero			varchar(12)		Not Null,
		DiagnosticoSecuencia		int				Not Null,
		Secuencia					int				Not Null,
		TipoFuente					varchar(30)		Not Null,
		Referencia					nvarchar(250)	Not Null,
		Descripcion					nvarchar(1000)	Not Null,
		Similitud					decimal(5,2)	Null,

		Constraint PK_TI_IncidenciaDiagnosticoEvidencia
			Primary Key (IncidenciaNumero, DiagnosticoSecuencia, Secuencia),
		Constraint FK_TI_IncidenciaDiagnosticoEvidencia_Diagnostico
			Foreign Key (IncidenciaNumero, DiagnosticoSecuencia)
			References dbo.TI_IncidenciaDiagnostico (IncidenciaNumero, Secuencia),
		Constraint CK_TI_IncidenciaDiagnosticoEvidencia_Similitud
			Check (Similitud Is Null or Similitud Between 0 and 100)
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