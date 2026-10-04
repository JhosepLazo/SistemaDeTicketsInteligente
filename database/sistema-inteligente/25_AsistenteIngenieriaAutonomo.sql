/*
	Archivo: 25_AsistenteIngenieriaAutonomo.sql
	Objetivo: Incorporar el workspace persistente del Agente de Ingeniería Autónomo para observar, investigar, diagnosticar, documentar y solicitar ejecuciones controladas.
	Responsabilidad: Registrar sesiones, eventos Live, correlación técnica, diagnóstico, informe Markdown y decisión de TI sin permitir SQL libre generado por IA.
	Dependencias: Requiere TI_Incidencia, TI_Usuario, TI_Accion, TI_SolicitudAprobacion, TI_EjecucionAccion, TI_Auditoria y TI_BaseConocimiento.
	Orden: Ejecutar después de 24_OptimizacionRendimiento.sql.
	Consideraciones: La investigación es de solo lectura. Toda modificación requiere una acción catalogada, aprobación cuando aplique y un Stored Procedure ejecutor explícitamente habilitado.
*/

Use [GestionSistemas]
Go

Set Xact_Abort On
Set Ansi_Nulls On
Set Ansi_Padding On
Set Ansi_Warnings On
Set ArithAbort On
Set Concat_Null_Yields_Null On
Set Quoted_Identifier On
Set Numeric_RoundAbort Off
Go

If Object_Id('dbo.TI_AgenteSesion', 'U') Is Null
Begin
	Create Table dbo.TI_AgenteSesion (
		SesionNumero				bigint Identity(1,1)	Not Null,
		IncidenciaNumero			varchar(12)			Null,
		UsuarioTI					varchar(20)			Not Null,
		AreaTI						char(3)				Not Null,
		IdCorrelacion				uniqueidentifier	Not Null,
		DescripcionInicial			nvarchar(1200)		Not Null,
		Estado						varchar(30)			Not Null,
		ResumenObservacion			nvarchar(max)		Null,
		ProcesoObservado			nvarchar(max)		Null,
		ErrorObservado				nvarchar(1000)		Null,
		Diagnostico					nvarchar(max)		Null,
		CausaProbable				nvarchar(max)		Null,
		SolucionPropuesta			nvarchar(max)		Null,
		Confianza					decimal(5,2)		Null,
		DiagnosticoSecuencia		int					Null,
		AccionCodigo				varchar(50)			Null,
		NivelRiesgo				varchar(20)			Null,
		ParametrosJson				nvarchar(max)		Null,
		InformeMarkdown				nvarchar(max)		Null,
		Decision					varchar(30)			Null,
		UsuarioDecision			varchar(20)			Null,
		SolicitudAprobacionSecuencia	int				Null,
		EjecucionSecuencia int Null,
		FechaInicio					datetime2(0)		Not Null,
		FechaObservacionFin			datetime2(0)		Null,
		FechaDiagnostico			datetime2(0)		Null,
		FechaDecision				datetime2(0)		Null,
		FechaCierre					datetime2(0)		Null,

		Constraint PK_TI_AgenteSesion Primary Key (SesionNumero),
		Constraint UQ_TI_AgenteSesion_IdCorrelacion Unique (IdCorrelacion),
		Constraint FK_TI_AgenteSesion_Incidencia Foreign Key (IncidenciaNumero) References dbo.TI_Incidencia (IncidenciaNumero),
		Constraint FK_TI_AgenteSesion_UsuarioTI Foreign Key (UsuarioTI) References dbo.TI_Usuario (Usuario),
		Constraint FK_TI_AgenteSesion_AreaTI Foreign Key (AreaTI) References dbo.TI_Area (Area),
		Constraint FK_TI_AgenteSesion_Accion Foreign Key (AccionCodigo) References dbo.TI_Accion (AccionCodigo),
		Constraint FK_TI_AgenteSesion_UsuarioDecision Foreign Key (UsuarioDecision) References dbo.TI_Usuario (Usuario),
		Constraint CK_TI_AgenteSesion_Confianza Check (Confianza Is Null or Confianza Between 0 and 100)
	)

End
Go

If Not Exists (Select 1 From sys.indexes Where object_id = Object_Id(N'dbo.TI_AgenteSesion') and name = N'IX_TI_AgenteSesion_UsuarioEstadoFecha')
Begin
	Create Nonclustered Index IX_TI_AgenteSesion_UsuarioEstadoFecha
		on dbo.TI_AgenteSesion (UsuarioTI, Estado, FechaInicio Desc)
End
Go

If Not Exists (Select 1 From sys.indexes Where object_id = Object_Id(N'dbo.TI_AgenteSesion') and name = N'IX_TI_AgenteSesion_IncidenciaFecha')
Begin
	Create Nonclustered Index IX_TI_AgenteSesion_IncidenciaFecha
		on dbo.TI_AgenteSesion (IncidenciaNumero, FechaInicio Desc)
		Where IncidenciaNumero Is Not Null
End
Go

If Col_Length('dbo.TI_AgenteSesion', 'EjecucionSecuencia') Is Null
    Alter Table dbo.TI_AgenteSesion Add EjecucionSecuencia int Null
Go
If Col_Length('dbo.TI_AgenteSesion', 'SolucionValidada') Is Null
    Alter Table dbo.TI_AgenteSesion Add SolucionValidada bit Not Null Constraint DF_TI_AgenteSesion_Validada Default 0, ConocimientoCodigo varchar(20) Null
Go

If Object_Id('dbo.TI_AgenteEvento', 'U') Is Null
Begin
	Create Table dbo.TI_AgenteEvento (
		SesionNumero				bigint				Not Null,
		Secuencia					int					Not Null,
		Tipo						varchar(40)			Not Null,
		Fuente					varchar(30)			Not Null,
		Contenido					nvarchar(max)		Not Null,
		DatosJson					nvarchar(max)		Null,
		Fecha						datetime2(0)		Not Null,

		Constraint PK_TI_AgenteEvento Primary Key (SesionNumero, Secuencia),
		Constraint FK_TI_AgenteEvento_Sesion Foreign Key (SesionNumero) References dbo.TI_AgenteSesion (SesionNumero)
	)
End
Go

If Object_Id('dbo.TI_AgenteAccionEjecutor', 'U') Is Null
Begin
	Create Table dbo.TI_AgenteAccionEjecutor (
		AccionCodigo				varchar(50)		Not Null,
		Procedimiento				varchar(200)	Not Null,
		MaximoFilas				int				Not Null,
		Estado						varchar(2)		Not Null,
		UltimoUsuario				varchar(20)		Null,
		UltimaFechaModif			datetime2(0)	Null,

		Constraint PK_TI_AgenteAccionEjecutor Primary Key (AccionCodigo),
		Constraint FK_TI_AgenteAccionEjecutor_Accion Foreign Key (AccionCodigo) References dbo.TI_Accion (AccionCodigo),
		Constraint CK_TI_AgenteAccionEjecutor_MaximoFilas Check (MaximoFilas Between 1 and 1000),
		Constraint CK_TI_AgenteAccionEjecutor_Procedimiento Check (Procedimiento Like 'dbo.Usp_TI_AgenteAccion[_]%')
	)
End
Go
If Col_Length('dbo.TI_AgenteEvento', 'OrigenServidor') Is Null
    Alter Table dbo.TI_AgenteEvento Add OrigenServidor bit Not Null Constraint DF_TI_AgenteEvento_Origen Default 0
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_CrearSesion
/*================================================================================
Objetivo            : Crear una sesión de investigación autónoma para un operador TI autenticado.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 01/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : Inicia correlación y auditoría sin ejecutar acciones de negocio.
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3),
	@cIncidenciaNumero varchar(12) = Null,
	@cDescripcion nvarchar(1200),
	@cIdCorrelacion uniqueidentifier
As
Begin
	Set NoCount On
	Set Xact_Abort On

	If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM')) Throw 50500, 'El operador TI no se encuentra habilitado.', 1
	If NullIf(LTrim(RTrim(@cDescripcion)), '') Is Null Throw 50501, 'Describe el problema que debe investigar el agente.', 1
	If @cIncidenciaNumero Is Not Null and Not Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero) Throw 50502, 'La incidencia indicada no existe.', 1

	Declare @nSesionNumero bigint, @dFecha datetime2(0) = SysDateTime()

	Begin Try
		Begin Transaction

		Insert dbo.TI_AgenteSesion (IncidenciaNumero, UsuarioTI, AreaTI, IdCorrelacion, DescripcionInicial, Estado, FechaInicio)
		Values (@cIncidenciaNumero, @cUsuario, @cArea, @cIdCorrelacion, LTrim(RTrim(@cDescripcion)), 'RECOPILANDO', @dFecha)

		Set @nSesionNumero = Scope_Identity()

		Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
		Values (@cIncidenciaNumero, @cUsuario, 'T', 'TI_AgenteSesion', Convert(varchar(30), @nSesionNumero), 'INICIAR_INVESTIGACION_AGENTE', 'EXITOSO', Null, @cIdCorrelacion, @dFecha)

		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch

	Select SesionNumero, IncidenciaNumero, IdCorrelacion, DescripcionInicial, Estado, FechaInicio
	From dbo.TI_AgenteSesion
	Where SesionNumero = @nSesionNumero
End
Go

-- dbo.Usp_TI_Agente_RegistrarEvento: la versión vigente está en 30_AgenteFase4HerramientasDiagnostico.sql (aquí había una versión anterior que ese script reemplaza).
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_FinalizarObservacion
/*================================================================================
Objetivo            : Cerrar la etapa de observación y dejar la sesión lista para investigación técnica.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 01/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : Separa explícitamente la reproducción humana de la investigación posterior.
================================================================================*/
	@cUsuario varchar(20),
	@nSesionNumero bigint,
	@cResumenObservacion nvarchar(max),
	@cProcesoObservado nvarchar(max),
	@cErrorObservado nvarchar(1000) = Null
As
Begin
	Set NoCount On
	Set Xact_Abort On

	If Not Exists (Select 1 From dbo.TI_AgenteSesion Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario) Throw 50507, 'La sesión de investigación no existe o no pertenece al operador.', 1

	Update dbo.TI_AgenteSesion
	Set ResumenObservacion = NullIf(LTrim(RTrim(@cResumenObservacion)), ''),
		ProcesoObservado = NullIf(LTrim(RTrim(@cProcesoObservado)), ''),
		ErrorObservado = NullIf(LTrim(RTrim(@cErrorObservado)), ''),
		Estado = 'LISTO_INVESTIGAR',
		FechaObservacionFin = SysDateTime()
	Where SesionNumero = @nSesionNumero and Estado In ('RECOPILANDO','OBSERVANDO','LISTO_INVESTIGAR')
	If @@RowCount = 0 Throw 50527, 'La investigación ya no admite nuevas observaciones.', 1
End
Go

-- dbo.Usp_TI_Agente_ObtenerContexto: la versión vigente está en 33_AgenteFase6Mejoras.sql (aquí había una versión anterior que ese script reemplaza).
Go

-- dbo.Usp_TI_Agente_GuardarDiagnostico: la versión vigente está en 27_AgenteFase1Integracion.sql (aquí había una versión anterior que ese script reemplaza).
Go

-- dbo.Usp_TI_Agente_GrabarInformacion: la versión vigente está en 27_AgenteFase1Integracion.sql (aquí había una versión anterior que ese script reemplaza).
Go

-- dbo.Usp_TI_Agente_PrepararCambio: la versión vigente está en 27_AgenteFase1Integracion.sql (aquí había una versión anterior que ese script reemplaza).
Go

-- dbo.Usp_TI_Agente_FinalizarCambio: la versión vigente está en 27_AgenteFase1Integracion.sql (aquí había una versión anterior que ese script reemplaza).
Go

/*
Contrato para ejecutores autónomos autorizados:
- El nombre debe iniciar con dbo.Usp_TI_AgenteAccion_.
- Debe recibir: @cUsuario varchar(20), @cArea char(3), @cIncidenciaNumero varchar(12), @cParametrosJson nvarchar(max), @cIdCorrelacion uniqueidentifier.
- Debe devolver una fila con ResultadoJson nvarchar(max) y FilasAfectadas int.
- Debe validar precondiciones, ejecutar dentro de transacción y hacer rollback ante cualquier postcondición inválida.
- No se registra ningún ejecutor por defecto porque cada corrección ERP debe estar validada por su propietario técnico.
*/

/* Ejecuta Procedure
Exec dbo.Usp_TI_Agente_CrearSesion
	@cUsuario = 'TEC001',
	@cArea = '001',
	@cIncidenciaNumero = 'INC-000001',
	@cDescripcion = N'Investigar el error reportado por el usuario.',
	@cIdCorrelacion = '11111111-1111-1111-1111-111111111111';
*/
