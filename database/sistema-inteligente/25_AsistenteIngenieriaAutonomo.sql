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

Create Or Alter Procedure dbo.Usp_TI_Agente_RegistrarEvento
/*================================================================================
Objetivo            : Registrar evidencia cronológica proveniente de Live, usuario, sistema o telemetría técnica.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 01/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : La pantalla no se persiste; se guardan eventos y transcripciones necesarios para la investigación.
================================================================================*/
	@cUsuario varchar(20),
	@nSesionNumero bigint,
	@cTipo varchar(40),
	@cFuente varchar(30),
	@cContenido nvarchar(max),
	@cDatosJson nvarchar(max) = Null
As
Begin
	Set NoCount On
	Set Xact_Abort On

	If Not Exists (Select 1 From dbo.TI_AgenteSesion Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario) Throw 50503, 'La sesión de investigación no existe o no pertenece al operador.', 1
	If Exists (Select 1 From dbo.TI_AgenteSesion Where SesionNumero = @nSesionNumero and Estado In ('INFORME_GRABADO','CAMBIO_VALIDADO','CANCELADO')) Throw 50504, 'La investigación ya se encuentra finalizada.', 1
	If NullIf(LTrim(RTrim(@cTipo)), '') Is Null or NullIf(LTrim(RTrim(@cFuente)), '') Is Null or NullIf(LTrim(RTrim(@cContenido)), '') Is Null Throw 50505, 'El evento de investigación no contiene información suficiente.', 1
	If @cDatosJson Is Not Null and IsJson(@cDatosJson) = 0 Throw 50506, 'Los datos técnicos del evento no tienen formato JSON válido.', 1
	If Upper(@cFuente) Not In ('LIVE','USUARIO') or Upper(@cTipo) Not In ('INICIO_LIVE','TRANSCRIPCION_USUARIO','TRANSCRIPCION_AGENTE','ERROR_OBSERVADO','FIN_LIVE','NOTA_USUARIO')
		Throw 50526, 'La evidencia del navegador no puede declararse telemetría o código del servidor.', 1

	Declare @nSecuencia int, @dFecha datetime2(0) = SysDateTime()

	Begin Transaction
	If Not Exists (Select 1 From dbo.TI_AgenteSesion With (UpdLock, HoldLock) Where SesionNumero = @nSesionNumero and Estado In ('RECOPILANDO','OBSERVANDO','LISTO_INVESTIGAR'))
		Throw 50527, 'La investigación ya no admite nuevas observaciones.', 1
	Select @nSecuencia = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_AgenteEvento With (UpdLock, HoldLock) Where SesionNumero = @nSesionNumero
	Insert dbo.TI_AgenteEvento (SesionNumero, Secuencia, Tipo, Fuente, Contenido, DatosJson, Fecha)
	Values (@nSesionNumero, @nSecuencia, Upper(LTrim(RTrim(@cTipo))), Upper(LTrim(RTrim(@cFuente))), @cContenido, @cDatosJson, @dFecha)

	Update dbo.TI_AgenteSesion
	Set Estado = Case When Estado = 'RECOPILANDO' Then 'OBSERVANDO' Else Estado End
	Where SesionNumero = @nSesionNumero
	Commit Transaction
End
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

Create Or Alter Procedure dbo.Usp_TI_Agente_ObtenerContexto
/*================================================================================
Objetivo            : Recuperar únicamente el contexto autorizado necesario para investigar una sesión.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 01/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : Consolida ticket, documentos, conversación, conocimiento, acciones, auditoría y eventos sin exponer SQL libre al modelo.
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3),
	@nSesionNumero bigint
As
Begin
	Set NoCount On

	If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM')) Throw 50508, 'El operador TI no se encuentra habilitado.', 1
	If Not Exists (Select 1 From dbo.TI_AgenteSesion Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario) Throw 50509, 'La sesión de investigación no existe o no pertenece al operador.', 1

	Select
		s.SesionNumero, s.IncidenciaNumero, s.IdCorrelacion, s.DescripcionInicial, s.Estado, s.ResumenObservacion, s.ProcesoObservado, s.ErrorObservado, s.SolucionValidada, s.ConocimientoCodigo,
		s.Diagnostico, s.CausaProbable, s.SolucionPropuesta, s.Confianza, s.AccionCodigo, s.NivelRiesgo, s.ParametrosJson, s.Decision,
		s.SolicitudAprobacionSecuencia, s.FechaInicio, s.FechaDiagnostico, s.FechaDecision,
		InformeDisponible = Convert(bit, Case When NullIf(s.InformeMarkdown, '') Is Null Then 0 Else 1 End),
		i.Titulo, i.Detalle, i.MensajeError, i.Estado as EstadoIncidencia, i.Linea, i.Item, i.Tipo, i.SubTipo, i.Categoria, i.UsuarioSolicitante, i.FechaRegistro
	From dbo.TI_AgenteSesion s
	Left Join dbo.TI_Incidencia i on i.IncidenciaNumero = s.IncidenciaNumero
	Where s.SesionNumero = @nSesionNumero

	Select d.CompaniaSocio, d.TipoDocumento, d.NumeroDocumento, d.Descripcion
	From dbo.TI_IncidenciaDocumento d
	Inner Join dbo.TI_AgenteSesion s on s.IncidenciaNumero = d.IncidenciaNumero
	Where s.SesionNumero = @nSesionNumero
	Order By d.Secuencia

	Select Top (30) m.TipoAutor, Autor = IsNull(u.NombreCompleto, m.UsuarioAutor), m.Contenido, m.FechaMensaje, m.EsInterno
	From dbo.TI_IncidenciaMensaje m
	Inner Join dbo.TI_AgenteSesion s on s.IncidenciaNumero = m.IncidenciaNumero
	Left Join dbo.TI_Usuario u on u.Usuario = m.UsuarioAutor
	Where s.SesionNumero = @nSesionNumero
	Order By m.Secuencia Desc

	;With Contexto as (
		Select i.Linea, i.Item, i.Tipo, i.Categoria
		From dbo.TI_AgenteSesion s
		Left Join dbo.TI_Incidencia i on i.IncidenciaNumero = s.IncidenciaNumero
		Where s.SesionNumero = @nSesionNumero
	)
	Select Top (10) k.ConocimientoCodigo, k.Titulo, k.Problema, k.Sintomas, k.Causa, k.Solucion, k.Procedimiento,
		PuntajeContextual =
			Case When k.Item Is Not Null and k.Item = c.Item Then 5 Else 0 End +
			Case When k.Linea Is Not Null and k.Linea = c.Linea Then 3 Else 0 End +
			Case When k.Categoria Is Not Null and k.Categoria = c.Categoria Then 2 Else 0 End +
			Case When k.Tipo Is Not Null and k.Tipo = c.Tipo Then 1 Else 0 End
	From dbo.TI_BaseConocimiento k
	Cross Join Contexto c
	Where k.Estado = 'A'
		and (c.Linea Is Null or k.Linea Is Null or k.Linea = c.Linea)
	Order By PuntajeContextual Desc, k.FechaValidacion Desc, k.FechaCreacion Desc

	Select AccionCodigo, Nombre, Descripcion, Tipo, NivelRiesgo, RequiereAprobacion
	From dbo.TI_Accion
	Where Estado = 'A'
	Order By Tipo, NivelRiesgo, Nombre

	Select Top (50) a.Entidad, a.Registro, a.Evento, a.Resultado, a.DetalleJson, a.IdCorrelacion, a.Fecha
	From dbo.TI_Auditoria a
	Inner Join dbo.TI_AgenteSesion s on s.IncidenciaNumero = a.IncidenciaNumero
	Where s.SesionNumero = @nSesionNumero
	Order By a.Fecha Desc, a.AuditoriaNumero Desc

	Select Secuencia, Tipo, Fuente, Contenido, DatosJson, Fecha, OrigenServidor
	From dbo.TI_AgenteEvento
	Where SesionNumero = @nSesionNumero
	Order By Secuencia
End
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_GuardarDiagnostico
/*================================================================================
Objetivo            : Persistir el diagnóstico sustentado y el expediente Markdown generado por el agente.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 01/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : Registra evidencia en las entidades existentes cuando la sesión está asociada a una incidencia.
================================================================================*/
	@cUsuario varchar(20),
	@nSesionNumero bigint,
	@cDiagnostico nvarchar(max),
	@cCausaProbable nvarchar(max),
	@cSolucionPropuesta nvarchar(max),
	@nConfianza decimal(5,2),
	@cAccionCodigo varchar(50) = Null,
	@cNivelRiesgo varchar(20) = Null,
	@cParametrosJson nvarchar(max) = Null,
	@cEvidenciasJson nvarchar(max) = Null,
	@cInformeMarkdown nvarchar(max),
	@cIdCorrelacion uniqueidentifier
As
Begin
	Set NoCount On
	Set Xact_Abort On

	If Not Exists (Select 1 From dbo.TI_AgenteSesion Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario) Throw 50510, 'La sesión de investigación no existe o no pertenece al operador.', 1
	If @nConfianza Not Between 0 and 100 Throw 50511, 'La confianza del diagnóstico no es válida.', 1
	If @cAccionCodigo Is Not Null and Not Exists (Select 1 From dbo.TI_Accion Where AccionCodigo = @cAccionCodigo and Estado = 'A') Throw 50512, 'La acción propuesta no pertenece al catálogo activo.', 1
	If @cParametrosJson Is Not Null and IsJson(@cParametrosJson) = 0 Throw 50513, 'Los parámetros de la acción no tienen formato JSON válido.', 1
	If @cEvidenciasJson Is Not Null and IsJson(@cEvidenciasJson) = 0 Throw 50514, 'Las evidencias del diagnóstico no tienen formato JSON válido.', 1

	Declare @cIncidenciaNumero varchar(12), @nDiagnosticoSecuencia int, @dFecha datetime2(0) = SysDateTime()
	Select @cIncidenciaNumero = IncidenciaNumero From dbo.TI_AgenteSesion Where SesionNumero = @nSesionNumero

	Begin Try
		Begin Transaction
		If Not Exists (Select 1 From dbo.TI_AgenteSesion With (UpdLock, HoldLock) Where SesionNumero = @nSesionNumero and Estado In ('RECOPILANDO','OBSERVANDO','LISTO_INVESTIGAR') and InformeMarkdown Is Null)
			Throw 50528, 'Ya existe un diagnóstico o una decisión para esta investigación.', 1

		If @cIncidenciaNumero Is Not Null
		Begin
			Select @nDiagnosticoSecuencia = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_IncidenciaDiagnostico With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero

			Insert dbo.TI_IncidenciaDiagnostico (IncidenciaNumero, Secuencia, Origen, Diagnostico, CausaProbable, SolucionSugerida, Confianza, Estado, UsuarioValida, FechaDiagnostico)
			Values (@cIncidenciaNumero, @nDiagnosticoSecuencia, 'I', @cDiagnostico, @cCausaProbable, @cSolucionPropuesta, @nConfianza, 'P', Null, @dFecha)

			If @cEvidenciasJson Is Not Null
			Begin
				Insert dbo.TI_IncidenciaDiagnosticoEvidencia (IncidenciaNumero, DiagnosticoSecuencia, Secuencia, TipoFuente, Referencia, Descripcion, Similitud)
				Select @cIncidenciaNumero, @nDiagnosticoSecuencia, Row_Number() Over (Order By (Select 1)), TipoFuente, Referencia, Descripcion, Similitud
				From OpenJson(@cEvidenciasJson)
				With (
					TipoFuente varchar(30) '$.tipoFuente',
					Referencia nvarchar(250) '$.referencia',
					Descripcion nvarchar(1000) '$.descripcion',
					Similitud decimal(5,2) '$.similitud'
				)
				Where NullIf(TipoFuente, '') Is Not Null and NullIf(Referencia, '') Is Not Null and NullIf(Descripcion, '') Is Not Null
			End
		End

		Update dbo.TI_AgenteSesion
		Set Estado = 'PENDIENTE_TI', Diagnostico = @cDiagnostico, CausaProbable = @cCausaProbable, SolucionPropuesta = @cSolucionPropuesta,
			Confianza = @nConfianza, DiagnosticoSecuencia = @nDiagnosticoSecuencia, AccionCodigo = @cAccionCodigo, NivelRiesgo = @cNivelRiesgo,
			ParametrosJson = @cParametrosJson, InformeMarkdown = @cInformeMarkdown, FechaDiagnostico = @dFecha
		Where SesionNumero = @nSesionNumero

		Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
		Values (@cIncidenciaNumero, @cUsuario, 'I', 'TI_AgenteSesion', Convert(varchar(30), @nSesionNumero), 'DIAGNOSTICO_AGENTE', 'PENDIENTE_TI',
			Concat('{"confianza":', Convert(varchar(20), @nConfianza), ',"accion":', Case When @cAccionCodigo Is Null Then 'null' Else Concat('"', @cAccionCodigo, '"') End, '}'), @cIdCorrelacion, @dFecha)

		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_GrabarInformacion
/*================================================================================
Objetivo            : Finalizar la investigación conservando el expediente sin ejecutar ningún cambio.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 01/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : Implementa la decisión GRABAR INFORMACIÓN y detiene el agente.
================================================================================*/
	@cUsuario varchar(20),
	@nSesionNumero bigint,
	@cIdCorrelacion uniqueidentifier
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Declare @cIncidenciaNumero varchar(12), @dFecha datetime2(0) = SysDateTime()
	Select @cIncidenciaNumero = IncidenciaNumero From dbo.TI_AgenteSesion Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario
	If @@RowCount = 0 Throw 50515, 'La sesión de investigación no existe o no pertenece al operador.', 1
	If Not Exists (Select 1 From dbo.TI_AgenteSesion Where SesionNumero = @nSesionNumero and InformeMarkdown Is Not Null) Throw 50516, 'La investigación todavía no tiene un informe técnico generado.', 1

	Begin Transaction
	If Not Exists (Select 1 From dbo.TI_AgenteSesion With (UpdLock, HoldLock) Where SesionNumero = @nSesionNumero and Estado In ('PENDIENTE_TI','PENDIENTE_APROBACION','SIN_EJECUTOR','INFORME_GRABADO'))
		Throw 50529, 'No es posible cerrar una investigación en ejecución o con cambio validado.', 1
	If Exists (Select 1 From dbo.TI_AgenteSesion Where SesionNumero = @nSesionNumero and Estado = 'INFORME_GRABADO')
	Begin
		Commit Transaction
		Select InformeMarkdown From dbo.TI_AgenteSesion Where SesionNumero = @nSesionNumero
		Return
	End
	Update a Set Estado = 'C', ComentarioRespuesta = N'Investigación cerrada sin ejecutar cambios.', FechaRespuesta = @dFecha
	From dbo.TI_SolicitudAprobacion a Join dbo.TI_AgenteSesion s on s.IncidenciaNumero = a.IncidenciaNumero and s.SolicitudAprobacionSecuencia = a.Secuencia
	Where s.SesionNumero = @nSesionNumero and a.Estado = 'P'
	Update dbo.TI_AgenteSesion
	Set Estado = 'INFORME_GRABADO', Decision = 'GRABAR_INFORMACION', UsuarioDecision = @cUsuario, FechaDecision = @dFecha, FechaCierre = @dFecha
	Where SesionNumero = @nSesionNumero

	Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
	Values (@cIncidenciaNumero, @cUsuario, 'T', 'TI_AgenteSesion', Convert(varchar(30), @nSesionNumero), 'GRABAR_INFORMACION_AGENTE', 'FINALIZADO', Null, @cIdCorrelacion, @dFecha)
	Commit Transaction

	Select InformeMarkdown From dbo.TI_AgenteSesion Where SesionNumero = @nSesionNumero
End
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_PrepararCambio
/*================================================================================
Objetivo            : Validar la decisión REALIZAR CAMBIO, crear aprobación cuando corresponda y preparar únicamente un ejecutor catalogado.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 01/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : La IA no define procedimientos; el ejecutor proviene de TI_AgenteAccionEjecutor y debe cumplir el contrato Usp_TI_AgenteAccion_*.
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3),
	@nSesionNumero bigint,
	@cClaveIdempotencia uniqueidentifier,
	@cIdCorrelacion uniqueidentifier
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Declare @cIncidenciaNumero varchar(12), @cAccionCodigo varchar(50), @cParametrosJson nvarchar(max), @lRequiereAprobacion bit,
		@nSolicitudSecuencia int, @cEstadoSolicitud char(1), @cProcedimiento varchar(200), @nMaximoFilas int, @nEjecucionSecuencia int, @dFecha datetime2(0) = SysDateTime()

	If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM')) Throw 50517, 'El operador TI no se encuentra habilitado.', 1

	Begin Try
		Begin Transaction

		Select @cIncidenciaNumero = IncidenciaNumero, @cAccionCodigo = AccionCodigo, @cParametrosJson = ParametrosJson, @nSolicitudSecuencia = SolicitudAprobacionSecuencia
		From dbo.TI_AgenteSesion With (UpdLock, HoldLock)
		Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario and AreaTI = @cArea and IdCorrelacion = @cIdCorrelacion
			and Estado In ('PENDIENTE_TI','PENDIENTE_APROBACION','LISTO_EJECUCION','SIN_EJECUTOR')
		If @@RowCount = 0 Throw 50523, 'La investigación ya fue procesada o no admite esta ejecución.', 1
		If @cAccionCodigo Is Null Throw 50518, 'El diagnóstico no contiene una acción correctiva catalogada.', 1
		If @cIncidenciaNumero Is Null Throw 50519, 'Para ejecutar un cambio la investigación debe estar asociada a una incidencia.', 1
		If Not Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero and Estado Not In ('RS','CA','CF','NP')) Throw 50530, 'La incidencia ya está finalizada.', 1
		Select @lRequiereAprobacion = RequiereAprobacion From dbo.TI_Accion With (HoldLock) Where AccionCodigo = @cAccionCodigo and Tipo = 'E' and Estado = 'A'
		If @lRequiereAprobacion Is Null Throw 50520, 'La acción propuesta no está habilitada para ejecución.', 1

		If @lRequiereAprobacion = 1
		Begin
			If @nSolicitudSecuencia Is Null
			Begin
				Select @nSolicitudSecuencia = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_SolicitudAprobacion With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero
				Insert dbo.TI_SolicitudAprobacion (IncidenciaNumero, Secuencia, AccionCodigo, UsuarioSolicitante, UsuarioAprobador, ParametrosJson, Estado, Justificacion, ComentarioRespuesta, FechaSolicitud, FechaRespuesta)
				Select @cIncidenciaNumero, @nSolicitudSecuencia, @cAccionCodigo, @cUsuario, Null, @cParametrosJson, 'P', Left(Coalesce(NullIf(CausaProbable, ''), NullIf(SolucionPropuesta, ''), N'Acción propuesta por investigación autónoma.'), 1000), Null, @dFecha, Null
				From dbo.TI_AgenteSesion Where SesionNumero = @nSesionNumero

				Update dbo.TI_AgenteSesion
				Set Estado = 'PENDIENTE_APROBACION', Decision = 'REALIZAR_CAMBIO', UsuarioDecision = @cUsuario, FechaDecision = @dFecha, SolicitudAprobacionSecuencia = @nSolicitudSecuencia
				Where SesionNumero = @nSesionNumero

				Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
				Values (@cIncidenciaNumero, @cUsuario, 'T', 'TI_SolicitudAprobacion', Concat(@cIncidenciaNumero, '/', @nSolicitudSecuencia), 'SOLICITUD_CAMBIO_AGENTE', 'PENDIENTE', Concat('{"accionCodigo":"', @cAccionCodigo, '"}'), @cIdCorrelacion, @dFecha)

				Commit Transaction
				Select Estado = 'PENDIENTE_APROBACION', Mensaje = 'La acción requiere aprobación. La solicitud fue enviada a la bandeja TI antes de permitir cualquier cambio.', PuedeEjecutar = Convert(bit, 0), ProcedimientoEjecutor = Convert(varchar(200), ''), EjecucionSecuencia = Convert(int, Null), SolicitudAprobacionSecuencia = @nSolicitudSecuencia, ParametrosJson = IsNull(@cParametrosJson, '{}')
				Return
			End

			Select @cEstadoSolicitud = Estado From dbo.TI_SolicitudAprobacion With (UpdLock, HoldLock)
			Where IncidenciaNumero = @cIncidenciaNumero and Secuencia = @nSolicitudSecuencia and AccionCodigo = @cAccionCodigo
				and IsNull(ParametrosJson, '{}') = IsNull(@cParametrosJson, '{}')
			If @cEstadoSolicitud = 'P'
			Begin
				Commit Transaction
				Select Estado = 'PENDIENTE_APROBACION', Mensaje = 'La acción todavía espera aprobación de TI.', PuedeEjecutar = Convert(bit, 0), ProcedimientoEjecutor = Convert(varchar(200), ''), EjecucionSecuencia = Convert(int, Null), SolicitudAprobacionSecuencia = @nSolicitudSecuencia, ParametrosJson = IsNull(@cParametrosJson, '{}')
				Return
			End
			If IsNull(@cEstadoSolicitud, '') <> 'A' Throw 50521, 'No existe una aprobación válida para esta acción y sus parámetros.', 1
		End

		Select @cProcedimiento = Procedimiento, @nMaximoFilas = MaximoFilas From dbo.TI_AgenteAccionEjecutor With (HoldLock) Where AccionCodigo = @cAccionCodigo and Estado = 'A'
		If @cProcedimiento Is Null
		Begin
			Update dbo.TI_AgenteSesion Set Estado = 'SIN_EJECUTOR' Where SesionNumero = @nSesionNumero
			Commit Transaction
			Select Estado = 'SIN_EJECUTOR', Mensaje = 'La acción está aprobada, pero todavía no tiene un procedimiento ejecutor autorizado. El agente no realizará cambios hasta que TI configure ese contrato.', PuedeEjecutar = Convert(bit, 0), ProcedimientoEjecutor = Convert(varchar(200), ''), EjecucionSecuencia = Convert(int, Null), SolicitudAprobacionSecuencia = @nSolicitudSecuencia, ParametrosJson = IsNull(@cParametrosJson, '{}')
			Return
		End

		If @cProcedimiento Not Like 'dbo.Usp_TI_AgenteAccion[_]%' Throw 50522, 'El procedimiento ejecutor no cumple el contrato autorizado.', 1

		If Exists (Select 1 From dbo.TI_EjecucionAccion Where ClaveIdempotencia = @cClaveIdempotencia) Throw 50523, 'La solicitud de ejecución ya fue procesada.', 1

		Select @nEjecucionSecuencia = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_EjecucionAccion With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero
		Insert dbo.TI_EjecucionAccion (IncidenciaNumero, Secuencia, AccionCodigo, SolicitudSecuencia, UsuarioEjecutor, ClaveIdempotencia, ParametrosJson, ResultadoJson, Estado, FilasAfectadas, FechaInicio, FechaFin, Error)
		Values (@cIncidenciaNumero, @nEjecucionSecuencia, @cAccionCodigo, @nSolicitudSecuencia, @cUsuario, @cClaveIdempotencia, @cParametrosJson, Null, 'PR', Null, @dFecha, Null, Null)

		Update dbo.TI_AgenteSesion Set Estado = 'EJECUTANDO', Decision = 'REALIZAR_CAMBIO', UsuarioDecision = @cUsuario, FechaDecision = @dFecha, EjecucionSecuencia = @nEjecucionSecuencia Where SesionNumero = @nSesionNumero

		Commit Transaction
		Select Estado = 'LISTO_EJECUCION', Mensaje = 'La acción superó permisos, aprobación y catálogo de ejecutores.', PuedeEjecutar = Convert(bit, 1), ProcedimientoEjecutor = @cProcedimiento, EjecucionSecuencia = @nEjecucionSecuencia, SolicitudAprobacionSecuencia = @nSolicitudSecuencia, ParametrosJson = IsNull(@cParametrosJson, '{}'), MaximoFilas = @nMaximoFilas
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_FinalizarCambio
/*================================================================================
Objetivo            : Registrar el resultado real del ejecutor autorizado y cerrar o escalar la sesión según validación.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 01/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : Ninguna ejecución se considera resuelta únicamente por haber invocado el procedimiento.
================================================================================*/
	@cUsuario varchar(20),
	@nSesionNumero bigint,
	@nEjecucionSecuencia int,
	@lExito bit,
	@cResultadoJson nvarchar(max) = Null,
	@nFilasAfectadas int = Null,
	@cError nvarchar(2000) = Null,
	@cIdCorrelacion uniqueidentifier
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Declare @cIncidenciaNumero varchar(12), @dFecha datetime2(0) = SysDateTime()
	Select @cIncidenciaNumero = IncidenciaNumero From dbo.TI_AgenteSesion Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario
	If @cIncidenciaNumero Is Null Throw 50524, 'No se encontró la ejecución asociada a la investigación.', 1

	Begin Transaction
	If Not Exists (Select 1 From dbo.TI_AgenteSesion With (UpdLock, HoldLock) Where SesionNumero = @nSesionNumero and Estado = 'EJECUTANDO' and EjecucionSecuencia = @nEjecucionSecuencia and IdCorrelacion = @cIdCorrelacion)
		Throw 50525, 'La ejecución no corresponde a la investigación activa.', 1
	If @lExito = 1
	Begin
		If IsNull(IsJson(@cResultadoJson), 0) <> 1 Throw 50531, 'El resultado del ejecutor no es JSON valido.', 1
		If IsNull(Json_Value(@cResultadoJson, '$.validacionPosterior'), '') <> 'true' Throw 50531, 'El ejecutor no confirmó la validación posterior.', 1
	End
	Update dbo.TI_EjecucionAccion
	Set ResultadoJson = @cResultadoJson, Estado = Case When @lExito = 1 Then 'OK' Else 'ER' End, FilasAfectadas = @nFilasAfectadas, FechaFin = @dFecha, Error = @cError
	Where IncidenciaNumero = @cIncidenciaNumero and Secuencia = @nEjecucionSecuencia and UsuarioEjecutor = @cUsuario and Estado = 'PR'
	If @@RowCount = 0 Throw 50525, 'No se encontró la ejecución controlada que debe finalizarse.', 1

	Update dbo.TI_AgenteSesion
	Set Estado = Case When @lExito = 1 Then 'CAMBIO_VALIDADO' Else 'ERROR_EJECUCION' End,
		FechaCierre = Case When @lExito = 1 Then @dFecha Else FechaCierre End
	Where SesionNumero = @nSesionNumero

	Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
	Values (@cIncidenciaNumero, @cUsuario, 'S', 'TI_EjecucionAccion', Concat(@cIncidenciaNumero, '/', @nEjecucionSecuencia), 'EJECUCION_CAMBIO_AGENTE', Case When @lExito = 1 Then 'EXITOSO' Else 'ERROR' End, @cResultadoJson, @cIdCorrelacion, @dFecha)
	Commit Transaction
End
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
