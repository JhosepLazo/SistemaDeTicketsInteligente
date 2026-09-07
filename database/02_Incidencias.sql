/*
	Archivo: 02_Incidencias.sql
	Objetivo: Crear el modelo transaccional principal para el registro, seguimiento y gestión del ciclo de vida de las incidencias.
	Responsabilidad: Definir la cabecera TI_Incidencia y sus entidades relacionadas para avances, historial de estados, mensajes, adjuntos y documentos empresariales asociados.
	Dependencias: Requiere los maestros definidos en 01_Maestros.sql.
	Orden: Ejecutar después de 01_Maestros.sql.
	Consideraciones: Las entidades hijas se relacionan directamente mediante IncidenciaNumero; las claves compuestas protegen combinaciones de clasificación y evitan relaciones inconsistentes.
*/

Use [SistemaTicketsInteligente]
Go

Set Xact_Abort on

Begin Try
	Begin Transaction

	Create Table dbo.TI_Incidencia (
		IncidenciaNumero			varchar(12)		Not Null,
		FechaRegistro				datetime2(0)	Not Null,
		UsuarioSolicitante			varchar(20)		Not Null,
		-- Conserva el área del usuario al momento de registrar el ticket.
		AreaSolicitante				char(3)			Not Null,
		AreaTI						char(3)			Null,
		UsuarioTI					varchar(20)		Null,
		UsuarioAsigno				varchar(20)		Null,
		Linea						char(3)			Not Null,
		Item						varchar(20)		Null,
		Tipo						char(3)			Not Null,
		SubTipo						char(3)			Null,
		Categoria					varchar(20)		Null,
		Estado						char(2)			Not Null,
		AreaCausante				char(3)			Null,
		Titulo						nvarchar(250)	Not Null,
		Detalle						nvarchar(max)	Not Null,
		MensajeError				nvarchar(1000)	Null,
		FechaAsignacion				datetime2(0)	Null,
		FechaAtencion				datetime2(0)	Null,
		FechaCierre					datetime2(0)	Null,
		SlaObjetivoMinutos			int				Null,
		-- Se conservan como fotografía histórica aunque la configuración cambie.
		Prioridad					int				Null,
		Impacto						int				Null,
		Complejidad					int				Null,
		CanalRegistro				varchar(20)		Not Null,
		CausaRaiz					nvarchar(max)	Null,
		SolucionTecnica				nvarchar(max)	Null,
		RespuestaUsuario			nvarchar(max)	Null,
		TipoResolucion				varchar(20)		Null,
		Calificacion					tinyint			Null,
		ComentarioCalificacion		nvarchar(500)	Null,
		UltimoUsuario				varchar(20)		Not Null,
		UltimaFechaModif			datetime2(0)	Not Null,

		Constraint PK_TI_Incidencia
			Primary Key (IncidenciaNumero),
		Constraint FK_TI_Incidencia_UsuarioSolicitante
			Foreign Key (UsuarioSolicitante)
			References dbo.TI_Usuario (Usuario),
		Constraint FK_TI_Incidencia_AreaSolicitante
			Foreign Key (AreaSolicitante)
			References dbo.TI_Area (Area),
		Constraint FK_TI_Incidencia_AreaTI
			Foreign Key (AreaTI)
			References dbo.TI_Area (Area),
		Constraint FK_TI_Incidencia_UsuarioTI
			Foreign Key (UsuarioTI)
			References dbo.TI_Usuario (Usuario),
		Constraint FK_TI_Incidencia_UsuarioAsigno
			Foreign Key (UsuarioAsigno)
			References dbo.TI_Usuario (Usuario),
		Constraint FK_TI_Incidencia_Linea
			Foreign Key (Linea)
			References dbo.TI_Linea (Linea),
		-- Valida que el Item realmente pertenezca a la Linea indicada.
		Constraint FK_TI_Incidencia_LineaItem
			Foreign Key (Linea, Item)
			References dbo.TI_Item (Linea, Item),
		Constraint FK_TI_Incidencia_Tipo
			Foreign Key (Tipo)
			References dbo.TI_Tipo (Tipo),
		-- Valida la combinación completa de clasificación del ticket.
		Constraint FK_TI_Incidencia_TipoSubTipoCategoria
			Foreign Key (Tipo, SubTipo, Categoria)
			References dbo.TI_SubTipo (Tipo, SubTipo, Categoria),
		Constraint FK_TI_Incidencia_Categoria
			Foreign Key (Categoria)
			References dbo.TI_Categoria (Categoria),
		Constraint FK_TI_Incidencia_ItemCategoria
			Foreign Key (Item, Categoria)
			References dbo.TI_ItemCategoria (Item, Categoria),
		Constraint FK_TI_Incidencia_Estado
			Foreign Key (Estado)
			References dbo.TI_Estado (Estado),
		Constraint FK_TI_Incidencia_AreaCausante
			Foreign Key (AreaCausante)
			References dbo.TI_Area (Area)
	)

	Create Table dbo.TI_IncidenciaAvance (
		IncidenciaNumero			varchar(12)		Not Null,
		Secuencia					int				Not Null,
		UsuarioTI					varchar(20)		Not Null,
		FechaAvance					datetime2(0)	Not Null,
		Detalle						nvarchar(max)	Not Null,
		TiempoUtilizado				decimal(8,2)	Null,
		PorcentajeAvance			decimal(5,2)	Null,

		Constraint PK_TI_IncidenciaAvance
			Primary Key (IncidenciaNumero, Secuencia),
		Constraint FK_TI_IncidenciaAvance_Incidencia
			Foreign Key (IncidenciaNumero)
			References dbo.TI_Incidencia (IncidenciaNumero),
		Constraint FK_TI_IncidenciaAvance_UsuarioTI
			Foreign Key (UsuarioTI)
			References dbo.TI_Usuario (Usuario),
		Constraint CK_TI_IncidenciaAvance_Porcentaje
			Check (PorcentajeAvance Is Null or PorcentajeAvance Between 0 and 100)
	)

	Create Table dbo.TI_IncidenciaEstado (
		IncidenciaNumero			varchar(12)		Not Null,
		Secuencia					int				Not Null,
		Estado						char(2)			Not Null,
		UsuarioCambio				varchar(20)		Null,
		FechaCambio					datetime2(0)	Not Null,
		Observacion					nvarchar(1000)	Null,

		Constraint PK_TI_IncidenciaEstado
			Primary Key (IncidenciaNumero, Secuencia),
		Constraint FK_TI_IncidenciaEstado_Incidencia
			Foreign Key (IncidenciaNumero)
			References dbo.TI_Incidencia (IncidenciaNumero),
		Constraint FK_TI_IncidenciaEstado_Estado
			Foreign Key (Estado)
			References dbo.TI_Estado (Estado),
		Constraint FK_TI_IncidenciaEstado_UsuarioCambio
			Foreign Key (UsuarioCambio)
			References dbo.TI_Usuario (Usuario)
	)

	Create Table dbo.TI_IncidenciaMensaje (
		IncidenciaNumero			varchar(12)		Not Null,
		Secuencia					int				Not Null,
		UsuarioAutor				varchar(20)		Null,
		TipoAutor					char(1)			Not Null,
		Contenido					nvarchar(max)	Not Null,
		FechaMensaje					datetime2(0)	Not Null,
		EsInterno					bit				Not Null,

		Constraint PK_TI_IncidenciaMensaje
			Primary Key (IncidenciaNumero, Secuencia),
		Constraint FK_TI_IncidenciaMensaje_Incidencia
			Foreign Key (IncidenciaNumero)
			References dbo.TI_Incidencia (IncidenciaNumero),
		Constraint FK_TI_IncidenciaMensaje_UsuarioAutor
			Foreign Key (UsuarioAutor)
			References dbo.TI_Usuario (Usuario),
		-- U = Usuario, T = Técnico, I = Inteligencia Artificial, S = Sistema.
		Constraint CK_TI_IncidenciaMensaje_TipoAutor
			Check (TipoAutor In ('U', 'T', 'I', 'S'))
	)

	Create Table dbo.TI_IncidenciaAdjunto (
		IncidenciaNumero			varchar(12)		Not Null,
		Secuencia					int				Not Null,
		MensajeSecuencia			int				Null,
		UsuarioRegistro				varchar(20)		Not Null,
		NombreOriginal				nvarchar(260)	Not Null,
		NombreArchivo				nvarchar(260)	Not Null,
		RutaArchivo					nvarchar(1000)	Not Null,
		TipoMime					varchar(100)	Not Null,
		TamanoBytes					bigint			Not Null,
		FechaRegistro					datetime2(0)	Not Null,

		Constraint PK_TI_IncidenciaAdjunto
			Primary Key (IncidenciaNumero, Secuencia),
		Constraint FK_TI_IncidenciaAdjunto_Incidencia
			Foreign Key (IncidenciaNumero)
			References dbo.TI_Incidencia (IncidenciaNumero),
		-- La FK compuesta impide enlazar el adjunto a un mensaje de otra incidencia.
		Constraint FK_TI_IncidenciaAdjunto_Mensaje
			Foreign Key (IncidenciaNumero, MensajeSecuencia)
			References dbo.TI_IncidenciaMensaje (IncidenciaNumero, Secuencia),
		Constraint FK_TI_IncidenciaAdjunto_UsuarioRegistro
			Foreign Key (UsuarioRegistro)
			References dbo.TI_Usuario (Usuario),
		Constraint CK_TI_IncidenciaAdjunto_TamanoBytes
			Check (TamanoBytes >= 0)
	)

	Create Table dbo.TI_IncidenciaDocumento (
		IncidenciaNumero			varchar(12)		Not Null,
		Secuencia					int				Not Null,
		CompaniaSocio				varchar(8)		Null,
		TipoDocumento				varchar(10)		Not Null,
		NumeroDocumento				varchar(20)		Not Null,
		Descripcion					nvarchar(250)	Null,

		Constraint PK_TI_IncidenciaDocumento
			Primary Key (IncidenciaNumero, Secuencia),
		Constraint FK_TI_IncidenciaDocumento_Incidencia
			Foreign Key (IncidenciaNumero)
			References dbo.TI_Incidencia (IncidenciaNumero)
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