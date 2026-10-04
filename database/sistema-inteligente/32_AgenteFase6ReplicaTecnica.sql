/*
	Archivo: 32_AgenteFase6ReplicaTecnica.sql
	Objetivo: Que el agente replique técnicamente lo que el usuario mostró: analiza la grabación, busca el error en el código fuente
		y en los procedimientos, revisa los datos con consultas de solo lectura, investiga automáticamente y avisa a TI.
	Responsabilidad: Catalogar las herramientas internas de réplica técnica, registrar las grabaciones de pantalla (como adjunto del
		ticket y evidencia de la investigación), reabrir la observación conservando el diagnóstico anterior, importar la evidencia
		que el colaborador mostró al registrar su ticket, elegir el operador responsable de la investigación automática y notificarle.
	Dependencias: Requiere 31_AgenteFase5ConocimientoSemantico.sql.
	Orden: Ejecutar después de 31_AgenteFase5ConocimientoSemantico.sql.
	Consideraciones: La grabación solo se guarda con consentimiento y queda en uploads/incidencias (nunca en la base).
		Las consultas a la base del sistema investigado las ejecuta el backend: solo SELECT validados, con login de lectura,
		en una transacción que siempre se revierte. Ninguna herramienta de este script modifica datos del negocio.
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

/* ============================================================================
   Herramientas internas de réplica técnica (las ejecuta el backend)
   ============================================================================ */

Merge dbo.TI_AgenteHerramienta As t
Using (Values
	('DIAG_ANALIZAR_GRABACION', N'Análisis de la grabación de pantalla',
		N'Analiza con IA la grabación de pantalla de la reproducción: reconstruye los pasos con su segundo, las pantallas, los datos usados (documentos, códigos, fechas) y el mensaje de error exacto. Se ejecuta automáticamente cuando hay grabación.',
		N'{"type":"object","properties":{},"required":[],"additionalProperties":false}', 1, 0, 40),
	('DIAG_CODIGO_BUSCAR', N'Buscar en el código fuente',
		N'Busca un texto literal (mensaje de error, nombre de pantalla, botón o método) en el código fuente del sistema indicado y devuelve archivo y línea. Úsala primero con el mensaje de error exacto para encontrar dónde se genera.',
		N'{"type":"object","properties":{"sistema":{"type":"string","description":"Código del sistema investigable."},"texto":{"type":"string","description":"Texto literal a buscar (3 a 200 caracteres).","minLength":3,"maxLength":200}},"required":["sistema","texto"],"additionalProperties":false}', 1, 0, 20),
	('DIAG_CODIGO_LEER', N'Leer código fuente',
		N'Devuelve hasta 80 líneas de un archivo del código fuente a partir de una línea. Úsala tras DIAG_CODIGO_BUSCAR para entender la condición que produce el error.',
		N'{"type":"object","properties":{"sistema":{"type":"string","description":"Código del sistema investigable."},"archivo":{"type":"string","description":"Ruta relativa devuelta por DIAG_CODIGO_BUSCAR.","minLength":3,"maxLength":300},"desde":{"type":"integer","description":"Línea inicial.","minimum":1,"maximum":200000}},"required":["sistema","archivo","desde"],"additionalProperties":false}', 0, 0, 80),
	('DIAG_BD_BUSCAR', N'Buscar en la base de datos',
		N'Busca un texto literal dentro de procedimientos, funciones, vistas y triggers y, si el texto parece un nombre, en tablas y columnas de la base del sistema. Úsala con el mensaje de error exacto para encontrar qué procedimiento lo lanza; si falla por tiempo, no la repitas con variantes del mismo texto.',
		N'{"type":"object","properties":{"sistema":{"type":"string","description":"Código del sistema investigable."},"texto":{"type":"string","description":"Texto literal a buscar (3 a 200 caracteres).","minLength":3,"maxLength":200}},"required":["sistema","texto"],"additionalProperties":false}', 1, 0, 25),
	('DIAG_BD_DEFINICION', N'Leer procedimiento o vista',
		N'Devuelve hasta 120 líneas de la definición de un procedimiento, función, vista o trigger desde una línea. Úsala para leer la lógica que produce el error.',
		N'{"type":"object","properties":{"sistema":{"type":"string","description":"Código del sistema investigable."},"objeto":{"type":"string","description":"Nombre del objeto, por ejemplo dbo.Usp_Ejemplo.","minLength":3,"maxLength":256},"desde":{"type":"integer","description":"Línea inicial.","minimum":1,"maximum":100000}},"required":["sistema","objeto","desde"],"additionalProperties":false}', 0, 0, 120),
	('DIAG_BD_ESTRUCTURA', N'Estructura de una tabla',
		N'Devuelve columnas, tipos, clave primaria, referencias y filas aproximadas de una tabla. Úsala antes de consultar datos para no inventar columnas.',
		N'{"type":"object","properties":{"sistema":{"type":"string","description":"Código del sistema investigable."},"tabla":{"type":"string","description":"Nombre de la tabla, por ejemplo dbo.TI_Incidencia.","minLength":3,"maxLength":256}},"required":["sistema","tabla"],"additionalProperties":false}', 0, 0, 200),
	('DIAG_BD_CONSULTAR', N'Consultar datos (solo lectura)',
		N'Ejecuta un único SELECT de solo lectura (máximo 50 filas) para comprobar con datos reales la condición que produce el error, usando los valores que usó el usuario. No admite INSERT, UPDATE, DELETE, EXEC, SELECT INTO, funciones de usuario ni columnas sensibles; se ejecuta en una transacción que siempre se revierte.',
		N'{"type":"object","properties":{"sistema":{"type":"string","description":"Código del sistema investigable."},"sql":{"type":"string","description":"Un único SELECT de T-SQL.","minLength":10,"maxLength":4000}},"required":["sistema","sql"],"additionalProperties":false}', 0, 0, 50)
) As s (HerramientaCodigo, Nombre, Descripcion, ParametrosEsquemaJson, Automatica, RequiereTicket, MaximoFilas)
On t.HerramientaCodigo = s.HerramientaCodigo
When Matched Then Update Set Nombre = s.Nombre, Descripcion = s.Descripcion, ParametrosEsquemaJson = s.ParametrosEsquemaJson,
	Automatica = s.Automatica, RequiereTicket = s.RequiereTicket, MaximoFilas = s.MaximoFilas, Tipo = 'INTERNA', Procedimiento = Null, UltimaFechaModif = SysDateTime()
When Not Matched Then Insert (HerramientaCodigo, Nombre, Descripcion, Procedimiento, ParametrosEsquemaJson, Automatica, RequiereTicket, MaximoFilas, AccionCodigo, Estado, UltimoUsuario, UltimaFechaModif, Tipo)
	Values (s.HerramientaCodigo, s.Nombre, s.Descripcion, Null, s.ParametrosEsquemaJson, s.Automatica, s.RequiereTicket, s.MaximoFilas, Null, 'A', Null, SysDateTime(), 'INTERNA');
Go

/* ============================================================================
   Grabaciones de pantalla
   ============================================================================ */

Create Or Alter Procedure dbo.Usp_TI_Agente_RegistrarGrabacion
/*================================================================================
Objetivo            : Registrar la grabación de pantalla de una reproducción como evidencia y como adjunto del ticket.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : @lUsuarioFinal = 0 la sube el responsable TI; 1 la sube el colaborador invitado con invitación aceptada y vigente.
================================================================================*/
	@cUsuario varchar(20),
	@nSesionNumero bigint,
	@lUsuarioFinal bit,
	@cNombreOriginal nvarchar(260),
	@cNombreArchivo nvarchar(260),
	@cRutaArchivo nvarchar(1000),
	@cTipoMime varchar(100),
	@nTamanoBytes bigint,
	@nDuracionSegundos int = Null
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Declare @cIncidencia varchar(12), @nAdjunto int, @nSecuencia int, @dFecha datetime2(0) = SysDateTime(), @cDatos nvarchar(max)
	If @cTipoMime Not In ('video/webm','video/mp4') Throw 50579, 'La grabación debe ser un video WebM o MP4.', 1
	If @nTamanoBytes Not Between 1 and 47185920 Throw 50580, 'La grabación no puede superar los 45 MB.', 1
	If @cRutaArchivo Not Like 'uploads/incidencias/agente/%' Throw 50581, 'La ruta de la grabación no es válida.', 1

	Begin Try
		Begin Transaction
		Select @cIncidencia = IncidenciaNumero From dbo.TI_AgenteSesion With (UpdLock, HoldLock)
		Where SesionNumero = @nSesionNumero and Estado In ('RECOPILANDO','OBSERVANDO','LISTO_INVESTIGAR')
			and ((@lUsuarioFinal = 0 and UsuarioTI = @cUsuario)
				or (@lUsuarioFinal = 1 and UsuarioInvitado = @cUsuario and EstadoInvitacion = 'ACEPTADA' and InvitacionExpira > @dFecha))
		If @@RowCount = 0 Throw 50582, 'La investigación no admite grabaciones de este usuario en su estado actual.', 1

		If @cIncidencia Is Not Null
		Begin
			Select @nAdjunto = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_IncidenciaAdjunto With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidencia
			Insert dbo.TI_IncidenciaAdjunto (IncidenciaNumero, Secuencia, MensajeSecuencia, UsuarioRegistro, NombreOriginal, NombreArchivo, RutaArchivo, TipoMime, TamanoBytes, FechaRegistro)
			Values (@cIncidencia, @nAdjunto, Null, @cUsuario, @cNombreOriginal, @cNombreArchivo, @cRutaArchivo, @cTipoMime, @nTamanoBytes, @dFecha)
		End

		Set @cDatos = (Select ruta = @cRutaArchivo, nombreOriginal = @cNombreOriginal, tipoMime = @cTipoMime, tamanoBytes = @nTamanoBytes,
			duracionSegundos = @nDuracionSegundos, adjuntoSecuencia = @nAdjunto, usuario = @cUsuario For Json Path, Without_Array_Wrapper)
		Select @nSecuencia = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_AgenteEvento With (UpdLock, HoldLock) Where SesionNumero = @nSesionNumero
		Insert dbo.TI_AgenteEvento (SesionNumero, Secuencia, Tipo, Fuente, Contenido, DatosJson, Fecha, OrigenServidor)
		Values (@nSesionNumero, @nSecuencia, 'GRABACION_PANTALLA', Case When @lUsuarioFinal = 1 Then 'LIVE_USUARIO' Else 'LIVE' End,
			Concat(N'Grabación de pantalla de ', Case When @lUsuarioFinal = 1 Then N'la reproducción del usuario' Else N'la observación TI' End,
				Case When @nDuracionSegundos Is Not Null Then Concat(N' (', @nDuracionSegundos / 60, N' min ', @nDuracionSegundos % 60, N' s)') End, N'.'),
			@cDatos, @dFecha, 1)

		Update dbo.TI_AgenteSesion Set Estado = 'OBSERVANDO' Where SesionNumero = @nSesionNumero and Estado = 'RECOPILANDO'
		Commit Transaction
		Select @nSecuencia EventoSecuencia
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go

/* ============================================================================
   Reabrir la observación (conserva el diagnóstico anterior)
   ============================================================================ */

Create Or Alter Procedure dbo.Usp_TI_Agente_ReabrirObservacion
/*================================================================================
Objetivo            : Volver a la etapa de observación para reunir más evidencia y generar un nuevo diagnóstico.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : Solo sin cambios en curso; el diagnóstico y el expediente anteriores quedan como evento DIAGNOSTICO_ANTERIOR.
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3),
	@nSesionNumero bigint
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Declare @cIncidencia varchar(12), @cCorrelacion uniqueidentifier, @nSolicitud int, @nSecuencia int, @dFecha datetime2(0) = SysDateTime(), @cDatos nvarchar(max)

	Begin Try
		Begin Transaction
		Select @cIncidencia = IncidenciaNumero, @cCorrelacion = IdCorrelacion, @nSolicitud = SolicitudAprobacionSecuencia
		From dbo.TI_AgenteSesion With (UpdLock, HoldLock)
		Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario and AreaTI = @cArea and Estado In ('PENDIENTE_TI','SIN_EJECUTOR')
		If @@RowCount = 0 Throw 50583, 'Solo puede reabrirse una investigación propia con diagnóstico pendiente de decisión y sin cambios en curso.', 1
		If @nSolicitud Is Not Null and Exists (Select 1 From dbo.TI_SolicitudAprobacion Where IncidenciaNumero = @cIncidencia and Secuencia = @nSolicitud and Estado = 'P')
			Throw 50584, 'La investigación tiene una aprobación pendiente; cancélala o espera su respuesta antes de reabrir.', 1

		Set @cDatos = (Select diagnostico = Diagnostico, causaProbable = CausaProbable, solucionPropuesta = SolucionPropuesta, confianza = Confianza,
				accionCodigo = AccionCodigo, parametrosJson = ParametrosJson, fechaDiagnostico = FechaDiagnostico, informeMarkdown = InformeMarkdown
			From dbo.TI_AgenteSesion Where SesionNumero = @nSesionNumero For Json Path, Without_Array_Wrapper)
		Select @nSecuencia = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_AgenteEvento With (UpdLock, HoldLock) Where SesionNumero = @nSesionNumero
		Insert dbo.TI_AgenteEvento (SesionNumero, Secuencia, Tipo, Fuente, Contenido, DatosJson, Fecha, OrigenServidor)
		Values (@nSesionNumero, @nSecuencia, 'DIAGNOSTICO_ANTERIOR', 'BACKEND', N'TI reabrió la observación para reunir más evidencia; el diagnóstico anterior se conserva en este evento.', @cDatos, @dFecha, 1)

		Update dbo.TI_AgenteSesion
		Set Estado = 'OBSERVANDO', Diagnostico = Null, CausaProbable = Null, SolucionPropuesta = Null, Confianza = Null, AccionCodigo = Null,
			NivelRiesgo = Null, ParametrosJson = Null, EvidenciasJson = Null, InformeMarkdown = Null, FechaDiagnostico = Null,
			SolicitudAprobacionSecuencia = Null, FechaObservacionFin = Null
		Where SesionNumero = @nSesionNumero

		Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, IdCorrelacion, Fecha)
		Values (@cIncidencia, @cUsuario, 'T', 'TI_AgenteSesion', Convert(varchar(30), @nSesionNumero), 'REABRIR_OBSERVACION', 'OBSERVANDO', @cCorrelacion, @dFecha)
		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go

/* ============================================================================
   Investigación automática
   ============================================================================ */

Create Or Alter Procedure dbo.Usp_TI_Agente_ResolverOperadorAutomatico
/*================================================================================
Objetivo            : Elegir quién será responsable de una investigación que el agente inicia por su cuenta.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : Prioridad: responsable del ticket, operador configurado, supervisor del área TI de la línea, cualquier supervisor.
================================================================================*/
	@cIncidenciaNumero varchar(12),
	@cUsuarioPreferido varchar(20) = Null
As
Begin
	Set NoCount On
	Select Top (1) u.Usuario, u.Area
	From dbo.TI_Usuario u
	Left Join dbo.TI_Incidencia i on i.IncidenciaNumero = @cIncidenciaNumero
	Left Join dbo.TI_Linea l on l.Linea = i.Linea
	Where u.Estado = 'A' and u.Perfil In ('TEC','SUP','ADM')
	Order By
		Case When u.Usuario = i.UsuarioTI Then 0
			When u.Usuario = @cUsuarioPreferido Then 1
			When u.Perfil In ('SUP','ADM') and u.Area = l.Area Then 2
			When u.Perfil In ('SUP','ADM') Then 3
			Else 4 End,
		u.Usuario
End
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_DatosSesion
/*================================================================================
Objetivo            : Datos mínimos de una sesión para el procesamiento en segundo plano del backend.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : Uso exclusivo del servidor; no se expone en la API.
================================================================================*/
	@nSesionNumero bigint
As
Begin
	Set NoCount On
	Select SesionNumero, UsuarioTI, AreaTI, Estado, IsNull(IncidenciaNumero, '') IncidenciaNumero
	From dbo.TI_AgenteSesion Where SesionNumero = @nSesionNumero
End
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_ImportarEvidenciaTicket
/*================================================================================
Objetivo            : Cargar en una investigación la evidencia que el colaborador mostró en pantalla al registrar su ticket.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : Importa conversación, pasos, error y grabaciones adjuntas al ticket, y deja la sesión lista para investigar.
================================================================================*/
	@cUsuario varchar(20),
	@nSesionNumero bigint,
	@cEvidenciaJson nvarchar(max)
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Declare @cIncidencia varchar(12), @nBase int, @cError nvarchar(1000), @cProceso nvarchar(max), @dFecha datetime2(0) = SysDateTime()
	If IsNull(IsJson(@cEvidenciaJson), 0) <> 1 Throw 50585, 'La evidencia del colaborador no tiene formato JSON válido.', 1

	Begin Try
		Begin Transaction
		Select @cIncidencia = IncidenciaNumero From dbo.TI_AgenteSesion With (UpdLock, HoldLock)
		Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario and Estado In ('RECOPILANDO','OBSERVANDO')
		If @@RowCount = 0 or @cIncidencia Is Null Throw 50586, 'La investigación automática no está disponible para importar evidencia.', 1

		Select @nBase = IsNull(Max(Secuencia), 0) From dbo.TI_AgenteEvento With (UpdLock, HoldLock) Where SesionNumero = @nSesionNumero
		Set @cError = Left(NullIf(LTrim(RTrim(Json_Value(@cEvidenciaJson, '$.mensajeError'))), N''), 1000)

		;With Items as (
			Select Orden = 1000 + Convert(int, c.[key]), Tipo = Case When Json_Value(c.value, '$.rol') = 'usuario' Then 'TRANSCRIPCION_USUARIO' Else 'TRANSCRIPCION_AGENTE' End,
				Contenido = Left(Json_Value(c.value, '$.contenido'), 4000)
			From OpenJson(@cEvidenciaJson, '$.conversacion') c
			Union All
			Select 2000 + Convert(int, p.[key]), 'PASO_OBSERVADO', Left(p.value, 1000)
			From OpenJson(@cEvidenciaJson, '$.pasos') p
			Union All
			Select 3000, 'ERROR_OBSERVADO', @cError Where @cError Is Not Null
		)
		Insert dbo.TI_AgenteEvento (SesionNumero, Secuencia, Tipo, Fuente, Contenido, DatosJson, Fecha, OrigenServidor)
		Select @nSesionNumero, @nBase + Row_Number() Over (Order By Orden), Tipo, 'LIVE_USUARIO', Contenido, N'{"origen":"evidencia_ticket"}', @dFecha, 0
		From Items Where NullIf(LTrim(RTrim(Contenido)), N'') Is Not Null

		-- Las grabaciones que el colaborador adjuntó al ticket pasan a la investigación.
		Select @nBase = IsNull(Max(Secuencia), 0) From dbo.TI_AgenteEvento Where SesionNumero = @nSesionNumero
		Insert dbo.TI_AgenteEvento (SesionNumero, Secuencia, Tipo, Fuente, Contenido, DatosJson, Fecha, OrigenServidor)
		Select @nSesionNumero, @nBase + Row_Number() Over (Order By a.Secuencia), 'GRABACION_PANTALLA', 'LIVE_USUARIO',
			N'Grabación de pantalla adjuntada por el colaborador al registrar el ticket.',
			(Select ruta = a.RutaArchivo, nombreOriginal = a.NombreOriginal, tipoMime = a.TipoMime, tamanoBytes = a.TamanoBytes, adjuntoSecuencia = a.Secuencia, usuario = a.UsuarioRegistro
				For Json Path, Without_Array_Wrapper), @dFecha, 1
		From dbo.TI_IncidenciaAdjunto a
		Where a.IncidenciaNumero = @cIncidencia and a.TipoMime In ('video/webm','video/mp4')

		Select @cProceso = String_Agg(Concat(Orden, N'. ', Case When Tipo = 'PASO_OBSERVADO' Then N'Paso observado (usuario final)' When Tipo = 'TRANSCRIPCION_USUARIO' Then N'Usuario final' Else N'Asistente del usuario' End, N': ', Contenido), Char(10))
			Within Group (Order By Secuencia)
		From (Select Secuencia, Tipo, Contenido, Orden = Row_Number() Over (Order By Secuencia)
			From dbo.TI_AgenteEvento Where SesionNumero = @nSesionNumero and Tipo In ('PASO_OBSERVADO','TRANSCRIPCION_USUARIO','TRANSCRIPCION_AGENTE')) e

		Update dbo.TI_AgenteSesion
		Set Estado = 'LISTO_INVESTIGAR', ProcesoObservado = Left(@cProceso, 24000), ErrorObservado = @cError, FechaObservacionFin = @dFecha,
			ResumenObservacion = N'Evidencia mostrada en pantalla por el colaborador al Asistente TI antes de registrar el ticket.'
		Where SesionNumero = @nSesionNumero
		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_NotificarDiagnostico
/*================================================================================
Objetivo            : Avisar al responsable de la investigación (y al responsable del ticket) que el agente terminó su diagnóstico.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : @lExito = 0 avisa que la investigación automática no pudo completarse.
================================================================================*/
	@nSesionNumero bigint,
	@lExito bit,
	@cDetalle nvarchar(400) = Null
As
Begin
	Set NoCount On
	Declare @cIncidencia varchar(12), @cTitulo nvarchar(120), @cMensaje nvarchar(500)

	Select @cIncidencia = s.IncidenciaNumero,
		@cTitulo = Left(Concat(Case When @lExito = 1 Then N'Diagnóstico del agente listo' Else N'Investigación automática sin completar' End,
			N' · ', IsNull(s.IncidenciaNumero, Concat('AGT-', Right('000000' + Convert(varchar(12), s.SesionNumero), 6)))), 120),
		@cMensaje = Left(Case When @lExito = 1
			Then Concat(N'Confianza ', Convert(int, IsNull(s.Confianza, 0)), N'%. ', IsNull(s.Diagnostico, N''))
			Else Concat(N'El agente no pudo completar la investigación automática. ', @cDetalle) End, 500)
	From dbo.TI_AgenteSesion s Where s.SesionNumero = @nSesionNumero
	If @@RowCount = 0 Return

	Insert dbo.TI_Notificacion (Usuario, IncidenciaNumero, Tipo, Titulo, Mensaje, Ruta, Fecha)
	Select Distinct u.Usuario, @cIncidencia, 'AGENTE', @cTitulo, @cMensaje, Concat('/asistente-ti?sesion=', @nSesionNumero), SysDateTime()
	From dbo.TI_Usuario u
	Where u.Estado = 'A' and u.Perfil In ('TEC','SUP','ADM')
		and (u.Usuario = (Select UsuarioTI From dbo.TI_AgenteSesion Where SesionNumero = @nSesionNumero)
			or u.Usuario = (Select UsuarioTI From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidencia))
End
Go

/* ============================================================================
   Comprobación sin cambios: lo que no aplica no queda como pendiente
   ============================================================================ */

Create Or Alter Procedure dbo.Usp_TI_Agente_DryRun
	@cUsuario varchar(20), @cArea char(3), @nSesionNumero bigint
As
Begin
	Set NoCount On
	-- Solo SELECT: no invoca ejecutores, crea aprobaciones ni cambia estados.
	Declare @cIncidencia varchar(12), @cAccion varchar(50), @cParametros nvarchar(max), @nSolicitud int, @cNoAplica varchar(20)
	Select @cIncidencia = IncidenciaNumero, @cAccion = AccionCodigo, @cParametros = ParametrosJson, @nSolicitud = SolicitudAprobacionSecuencia
	From dbo.TI_AgenteSesion Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario and AreaTI = @cArea
	If @@RowCount = 0 Throw 50503, 'La investigacion no pertenece al operador.', 1
	-- Sin acción propuesta, las comprobaciones de ejecución no aplican.
	Set @cNoAplica = Case When @cAccion Is Null Then 'NO_APLICA' End

	Select 'IDENTIDAD' Codigo, 'Operador TI habilitado' Descripcion,
		Case When Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM')) Then 'OK' Else 'ERROR' End Resultado
	Union All Select 'TICKET', 'Ticket vinculado y existente', Case When Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidencia) Then 'OK' Else 'PENDIENTE' End
	Union All Select 'ESTADO', 'Ticket admite intervencion', Case When Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidencia and Estado Not In ('RS','CA','CF','NP')) Then 'OK' Else 'PENDIENTE' End
	Union All Select 'ACCION', 'Accion correctiva activa', Coalesce(@cNoAplica, Case When Exists (Select 1 From dbo.TI_Accion Where AccionCodigo = @cAccion and Tipo = 'E' and Estado = 'A') Then 'OK' Else 'PENDIENTE' End)
	Union All Select 'PARAMETROS', 'Parametros estructurados validos', Coalesce(@cNoAplica, Case When IsJson(@cParametros) = 1 and Left(LTrim(@cParametros), 1) = '{' Then 'OK' Else 'PENDIENTE' End)
	Union All Select 'APROBACION', 'Aprobacion formal para esta accion y parametros', Coalesce(@cNoAplica, Case When Exists (Select 1 From dbo.TI_Accion Where AccionCodigo = @cAccion and RequiereAprobacion = 0)
		or Exists (Select 1 From dbo.TI_SolicitudAprobacion Where IncidenciaNumero = @cIncidencia and Secuencia = @nSolicitud and AccionCodigo = @cAccion and Estado = 'A' and IsNull(ParametrosJson, '{}') = IsNull(@cParametros, '{}')) Then 'OK' Else 'PENDIENTE' End)
	Union All Select 'EJECUTOR', 'Procedimiento autorizado instalado', Coalesce(@cNoAplica, Case When Exists (Select 1 From dbo.TI_AgenteAccionEjecutor Where AccionCodigo = @cAccion and Estado = 'A' and Object_Id(Procedimiento, 'P') Is Not Null) Then 'OK' Else 'PENDIENTE' End)
	-- La simulación más reciente decide: si falló o usó otros parámetros, deja de valer.
	Union All Select 'SIMULACION', 'Simulacion transaccional exitosa con los mismos parametros', Coalesce(@cNoAplica,
		(Select Top (1) Case When Json_Value(e.DatosJson, '$.exito') = 'true' and Json_Value(e.DatosJson, '$.accionCodigo') = @cAccion
				and Json_Value(e.DatosJson, '$.parametrosJson') = IsNull(@cParametros, N'{}') Then 'OK' Else 'ERROR' End
			From dbo.TI_AgenteEvento e Where e.SesionNumero = @nSesionNumero and e.Tipo = 'SIMULACION_CAMBIO' and e.OrigenServidor = 1
			Order By e.Secuencia Desc), 'PENDIENTE')
	Union All Select 'ERP', 'Validaciones internas ERP: requieren contrato diagnostico del propietario', 'NO_DISPONIBLE'
End
Go
