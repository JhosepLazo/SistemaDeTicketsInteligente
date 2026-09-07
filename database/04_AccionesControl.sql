/*
	Archivo: 04_AccionesControl.sql
	Objetivo: Crear las estructuras necesarias para registrar acciones autorizadas, solicitudes de aprobación y ejecuciones controladas asociadas a una incidencia.
	Responsabilidad: Separar el catálogo de acciones permitidas, la autorización humana y el resultado real de cada ejecución.
	Dependencias: Requiere TI_Incidencia y TI_Usuario.
	Orden: Ejecutar después de 02_Incidencias.sql; no depende funcionalmente de la implementación del agente de IA.
	Consideraciones: No almacena SQL arbitrario; las acciones se identifican mediante códigos controlados y posteriormente serán ejecutadas por servicios autorizados del backend.
*/

Use [SistemaTicketsInteligente]
Go

Set Xact_Abort on

Begin Try
	Begin Transaction

	Create Table dbo.TI_Accion (
		AccionCodigo				varchar(50)		Not Null,
		Nombre						nvarchar(100)	Not Null,
		Descripcion					nvarchar(500)	Not Null,
		Tipo						char(1)			Not Null,
		NivelRiesgo					varchar(20)		Not Null,
		RequiereAprobacion			bit				Not Null,
		Estado						varchar(2)		Not Null,
		UltimoUsuario				varchar(20)		Null,
		UltimaFechaModif			datetime2(0)	Null,

		Constraint PK_TI_Accion
			Primary Key (AccionCodigo),
		-- L = Lectura, E = Ejecución.
		Constraint CK_TI_Accion_Tipo
			Check (Tipo In ('L', 'E'))
	)

	Create Table dbo.TI_SolicitudAprobacion (
		IncidenciaNumero			varchar(12)		Not Null,
		Secuencia					int				Not Null,
		AccionCodigo				varchar(50)		Not Null,
		UsuarioSolicitante			varchar(20)		Not Null,
		UsuarioAprobador			varchar(20)		Null,
		ParametrosJson				nvarchar(max)	Null,
		Estado						char(1)			Not Null,
		Justificacion				nvarchar(1000)	Not Null,
		ComentarioRespuesta			nvarchar(1000)	Null,
		FechaSolicitud				datetime2(0)	Not Null,
		FechaRespuesta				datetime2(0)	Null,

		Constraint PK_TI_SolicitudAprobacion
			Primary Key (IncidenciaNumero, Secuencia),
		Constraint FK_TI_SolicitudAprobacion_Incidencia
			Foreign Key (IncidenciaNumero)
			References dbo.TI_Incidencia (IncidenciaNumero),
		Constraint FK_TI_SolicitudAprobacion_Accion
			Foreign Key (AccionCodigo)
			References dbo.TI_Accion (AccionCodigo),
		Constraint FK_TI_SolicitudAprobacion_UsuarioSolicitante
			Foreign Key (UsuarioSolicitante)
			References dbo.TI_Usuario (Usuario),
		Constraint FK_TI_SolicitudAprobacion_UsuarioAprobador
			Foreign Key (UsuarioAprobador)
			References dbo.TI_Usuario (Usuario),
		-- P = Pendiente, A = Aprobada, R = Rechazada, C = Cancelada.
		Constraint CK_TI_SolicitudAprobacion_Estado
			Check (Estado In ('P', 'A', 'R', 'C'))
	)

	Create Table dbo.TI_EjecucionAccion (
		IncidenciaNumero			varchar(12)		Not Null,
		Secuencia					int				Not Null,
		AccionCodigo				varchar(50)		Not Null,
		SolicitudSecuencia			int				Null,
		UsuarioEjecutor				varchar(20)		Null,
		ClaveIdempotencia			uniqueidentifier Not Null,
		ParametrosJson				nvarchar(max)	Null,
		ResultadoJson				nvarchar(max)	Null,
		Estado						varchar(2)		Not Null,
		FilasAfectadas				int				Null,
		FechaInicio					datetime2(0)	Not Null,
		FechaFin					datetime2(0)	Null,
		Error						nvarchar(2000)	Null,

		Constraint PK_TI_EjecucionAccion
			Primary Key (IncidenciaNumero, Secuencia),
		Constraint UQ_TI_EjecucionAccion_ClaveIdempotencia
			Unique (ClaveIdempotencia),
		Constraint FK_TI_EjecucionAccion_Incidencia
			Foreign Key (IncidenciaNumero)
			References dbo.TI_Incidencia (IncidenciaNumero),
		Constraint FK_TI_EjecucionAccion_Accion
			Foreign Key (AccionCodigo)
			References dbo.TI_Accion (AccionCodigo),
		Constraint FK_TI_EjecucionAccion_UsuarioEjecutor
			Foreign Key (UsuarioEjecutor)
			References dbo.TI_Usuario (Usuario),
		Constraint FK_TI_EjecucionAccion_Solicitud
			Foreign Key (IncidenciaNumero, SolicitudSecuencia)
			References dbo.TI_SolicitudAprobacion (IncidenciaNumero, Secuencia),
		Constraint CK_TI_EjecucionAccion_FilasAfectadas
			Check (FilasAfectadas Is Null or FilasAfectadas >= 0)
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