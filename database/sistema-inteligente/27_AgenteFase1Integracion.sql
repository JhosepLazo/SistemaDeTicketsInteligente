/*
	Archivo: 27_AgenteFase1Integracion.sql
	Objetivo: Integrar el Agente de Ingeniería con el ciclo real del ticket y cerrar las brechas de control detectadas en la Fase 1.
	Responsabilidad: Persistir evidencias del diagnóstico, bloquear el ticket mientras una acción del agente espera aprobación, impedir la autoaprobación,
		dejar notas internas en el ticket ante cada decisión del agente y registrar los primeros ejecutores autorizados sobre datos propios de GestionSistemas.
	Dependencias: Requiere 25_AsistenteIngenieriaAutonomo.sql y 26_AgenteDiagnosticoSeguro.sql.
	Orden: Ejecutar después de 26_AgenteDiagnosticoSeguro.sql.
	Consideraciones: Migración aditiva. No elimina datos. Los ejecutores registrados solo operan sobre TI_Usuario y TI_Incidencia; ninguna corrección ERP se registra aquí.
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

If Col_Length('dbo.TI_AgenteSesion', 'EvidenciasJson') Is Null
	Alter Table dbo.TI_AgenteSesion Add EvidenciasJson nvarchar(max) Null
Go

If Col_Length('dbo.TI_AgenteAccionEjecutor', 'ParametrosDescripcion') Is Null
	Alter Table dbo.TI_AgenteAccionEjecutor Add ParametrosDescripcion nvarchar(500) Null
Go

/* ============================== AUXILIARES DEL AGENTE ============================== */

Create Or Alter Procedure dbo.Usp_TI_Agente_DesbloquearTicket
/*================================================================================
Objetivo            : Devolver a diagnóstico un ticket bloqueado en PA cuando ya no tiene aprobaciones pendientes.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : Evita que cancelar o cerrar una investigación deje el ticket bloqueado indefinidamente.
================================================================================*/
	@cUsuario varchar(20),
	@cIncidenciaNumero varchar(12),
	@cObservacion nvarchar(1000)
As
Begin
	Set NoCount On

	If @cIncidenciaNumero Is Null Return
	If Exists (Select 1 From dbo.TI_SolicitudAprobacion Where IncidenciaNumero = @cIncidenciaNumero and Estado = 'P') Return

	Declare @nEstado int, @dFecha datetime2(0) = SysDateTime()

	Update dbo.TI_Incidencia
	Set Estado = 'DG', UltimoUsuario = @cUsuario, UltimaFechaModif = @dFecha
	Where IncidenciaNumero = @cIncidenciaNumero and Estado = 'PA'
	If @@RowCount = 0 Return

	Select @nEstado = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_IncidenciaEstado With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero
	Insert dbo.TI_IncidenciaEstado (IncidenciaNumero, Secuencia, Estado, UsuarioCambio, FechaCambio, Observacion)
	Values (@cIncidenciaNumero, @nEstado, 'DG', @cUsuario, @dFecha, @cObservacion)
End
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_RegistrarNotaTicket
/*================================================================================
Objetivo            : Registrar en el ticket una nota interna con la decisión tomada sobre una investigación del agente.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : La nota es interna (no visible para el usuario) y conserva al operador que tomó la decisión.
================================================================================*/
	@cUsuario varchar(20),
	@cIncidenciaNumero varchar(12),
	@cContenido nvarchar(max)
As
Begin
	Set NoCount On

	If @cIncidenciaNumero Is Null or NullIf(LTrim(RTrim(@cContenido)), '') Is Null Return

	Declare @nSecuencia int
	Select @nSecuencia = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_IncidenciaMensaje With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero
	Insert dbo.TI_IncidenciaMensaje (IncidenciaNumero, Secuencia, UsuarioAutor, TipoAutor, Contenido, FechaMensaje, EsInterno)
	Values (@cIncidenciaNumero, @nSecuencia, @cUsuario, 'I', @cContenido, SysDateTime(), 1)
End
Go

/* ============================== CONTEXTO Y DIAGNÓSTICO ============================== */

-- dbo.Usp_TI_Agente_ObtenerContexto: la versión vigente está en 33_AgenteFase6Mejoras.sql (aquí había una versión anterior que ese script reemplaza).
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_GuardarDiagnostico
/*================================================================================
Objetivo            : Persistir el diagnóstico sustentado y el expediente Markdown generado por el agente.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 01/10/2026
SP Anterior         : dbo.Usp_TI_Agente_GuardarDiagnostico (25_AsistenteIngenieriaAutonomo.sql)
Comentario Cambios  : 02/10/2026 Conserva las evidencias en la sesión para mostrarlas aunque la investigación no tenga ticket o se recargue la consola.
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
			ParametrosJson = @cParametrosJson, EvidenciasJson = @cEvidenciasJson, InformeMarkdown = @cInformeMarkdown, FechaDiagnostico = @dFecha
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

/* ============================== DECISIONES DE TI ============================== */

Create Or Alter Procedure dbo.Usp_TI_Agente_GrabarInformacion
/*================================================================================
Objetivo            : Finalizar la investigación conservando el expediente sin ejecutar ningún cambio.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 01/10/2026
SP Anterior         : dbo.Usp_TI_Agente_GrabarInformacion (25_AsistenteIngenieriaAutonomo.sql)
Comentario Cambios  : 02/10/2026 Desbloquea el ticket si quedó en PA por la acción cancelada y registra una nota interna con el diagnóstico.
================================================================================*/
	@cUsuario varchar(20),
	@nSesionNumero bigint,
	@cIdCorrelacion uniqueidentifier
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Declare @cIncidenciaNumero varchar(12), @cNota nvarchar(max), @dFecha datetime2(0) = SysDateTime()
	Select @cIncidenciaNumero = IncidenciaNumero From dbo.TI_AgenteSesion Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario
	If @@RowCount = 0 Throw 50515, 'La sesión de investigación no existe o no pertenece al operador.', 1
	If Not Exists (Select 1 From dbo.TI_AgenteSesion Where SesionNumero = @nSesionNumero and InformeMarkdown Is Not Null) Throw 50516, 'La investigación todavía no tiene un informe técnico generado.', 1

	Begin Try
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

		Exec dbo.Usp_TI_Agente_DesbloquearTicket @cUsuario, @cIncidenciaNumero, N'La acción propuesta por el agente se cerró sin ejecutar cambios; el ticket vuelve a diagnóstico.'

		Update dbo.TI_AgenteSesion
		Set Estado = 'INFORME_GRABADO', Decision = 'GRABAR_INFORMACION', UsuarioDecision = @cUsuario, FechaDecision = @dFecha, FechaCierre = @dFecha
		Where SesionNumero = @nSesionNumero

		Select @cNota = Concat(N'Agente de Ingeniería · AGT-', Right(Concat('000000', @nSesionNumero), 6), N' · Expediente grabado sin ejecutar cambios.', Char(10),
			N'Diagnóstico: ', Left(IsNull(Diagnostico, N''), 1500), Char(10), N'Solución propuesta: ', Left(IsNull(SolucionPropuesta, N''), 1500))
		From dbo.TI_AgenteSesion Where SesionNumero = @nSesionNumero
		Exec dbo.Usp_TI_Agente_RegistrarNotaTicket @cUsuario, @cIncidenciaNumero, @cNota

		Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
		Values (@cIncidenciaNumero, @cUsuario, 'T', 'TI_AgenteSesion', Convert(varchar(30), @nSesionNumero), 'GRABAR_INFORMACION_AGENTE', 'FINALIZADO', Null, @cIdCorrelacion, @dFecha)
		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch

	Select InformeMarkdown From dbo.TI_AgenteSesion Where SesionNumero = @nSesionNumero
End
Go

-- dbo.Usp_TI_Agente_PrepararCambio: la versión vigente está en 35_ControlAgenteYAutonomia.sql (aquí había una versión anterior que ese script reemplaza).
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_FinalizarCambio
/*================================================================================
Objetivo            : Registrar el resultado real del ejecutor autorizado y cerrar o escalar la sesión según validación.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 01/10/2026
SP Anterior         : dbo.Usp_TI_Agente_FinalizarCambio (25_AsistenteIngenieriaAutonomo.sql)
Comentario Cambios  : 02/10/2026 Registra en el ticket una nota interna con el resultado de la ejecución controlada.
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

	Declare @cIncidenciaNumero varchar(12), @cAccionCodigo varchar(50), @cNota nvarchar(max), @dFecha datetime2(0) = SysDateTime()
	Select @cIncidenciaNumero = IncidenciaNumero, @cAccionCodigo = AccionCodigo From dbo.TI_AgenteSesion Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario
	If @cIncidenciaNumero Is Null Throw 50524, 'No se encontró la ejecución asociada a la investigación.', 1

	Begin Try
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

		Set @cNota = Case When @lExito = 1
			Then Concat(N'Agente de Ingeniería · AGT-', Right(Concat('000000', @nSesionNumero), 6), N' · La acción ', @cAccionCodigo, N' se ejecutó y el procedimiento confirmó su validación posterior (',
				IsNull(Convert(varchar(10), @nFilasAfectadas), '0'), N' fila(s)). Confirma con el usuario que el problema quedó resuelto.')
			Else Concat(N'Agente de Ingeniería · AGT-', Right(Concat('000000', @nSesionNumero), 6), N' · La ejecución de ', @cAccionCodigo, N' no se confirmó y requiere revisión TI antes de cualquier nuevo intento. ', Left(IsNull(@cError, N''), 500))
		End
		Exec dbo.Usp_TI_Agente_RegistrarNotaTicket @cUsuario, @cIncidenciaNumero, @cNota

		Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
		Values (@cIncidenciaNumero, @cUsuario, 'S', 'TI_EjecucionAccion', Concat(@cIncidenciaNumero, '/', @nEjecucionSecuencia), 'EJECUCION_CAMBIO_AGENTE', Case When @lExito = 1 Then 'EXITOSO' Else 'ERROR' End, @cResultadoJson, @cIdCorrelacion, @dFecha)
		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go

-- dbo.Usp_TI_Agente_Cancelar: la versión vigente está en 34_DominiosYMaquinaEstados.sql (aquí había una versión anterior que ese script reemplaza).
Go

/* ============================== APROBACIONES (GESTIÓN DE TICKETS) ============================== */

-- dbo.Usp_TI_Responder_AprobacionTicket: la versión vigente está en 28_AgenteFase2Integracion.sql (aquí había una versión anterior que ese script reemplaza).
Go

Create Or Alter Procedure dbo.Usp_TI_Obtener_DetalleGestionTicketTI
/*================================================================================
Objetivo            : Obtener el detalle técnico y la trazabilidad completa de un ticket para el operador TI.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : dbo.Usp_TI_Obtener_DetalleGestionTicketTI (16_GestionTicketsTI.sql)
Comentario Cambios  : 02/10/2026 Las aprobaciones devuelven los parámetros estructurados que realmente se autorizan.
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3),
	@cIncidenciaNumero varchar(12)
As
Begin
	Set NoCount On

	If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC', 'SUP', 'ADM'))
		Throw 50201, 'El usuario autenticado no tiene acceso al detalle técnico.', 1
	If Not Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero)
		Throw 50202, 'El ticket indicado no existe.', 1

	Select
		i.IncidenciaNumero, i.UsuarioSolicitante, Solicitante = us.NombreCompleto, CorreoSolicitante = IsNull(us.Correo, ''),
		i.AreaSolicitante, AreaSolicitanteDescripcion = ars.Descripcion, i.AreaTI, AreaTIDescripcion = IsNull(arti.Descripcion, ''),
		i.UsuarioTI, Responsable = IsNull(ut.NombreCompleto, 'Sin asignar'), i.UsuarioAsigno,
		i.Linea, LineaDescripcion = l.Descripcion, i.Item, ItemDescripcion = IsNull(it.Descripcion, ''),
		i.Tipo, TipoDescripcion = t.Descripcion, i.SubTipo, SubTipoDescripcion = IsNull(st.Descripcion, ''), i.Categoria, CategoriaDescripcion = IsNull(c.Descripcion, ''),
		i.Estado, EstadoDescripcion = e.Descripcion, i.AreaCausante, AreaCausanteDescripcion = IsNull(ac.Descripcion, ''),
		i.Titulo, i.Detalle, i.MensajeError, i.FechaRegistro, i.FechaAsignacion, i.FechaAtencion, i.FechaCierre,
		i.SlaObjetivoMinutos, i.Prioridad, i.Impacto, i.Complejidad, i.CanalRegistro, i.CausaRaiz, i.SolucionTecnica, i.RespuestaUsuario, i.TipoResolucion,
		i.Calificacion, i.ComentarioCalificacion, i.UltimaFechaModif,
		SlaMinutosRestantes = Case When i.SlaObjetivoMinutos Is Null Then Null Else DateDiff(minute, SysDateTime(), DateAdd(minute, i.SlaObjetivoMinutos, i.FechaRegistro)) End
	From dbo.TI_Incidencia as i
	Inner Join dbo.TI_Usuario as us on us.Usuario = i.UsuarioSolicitante
	Inner Join dbo.TI_Area as ars on ars.Area = i.AreaSolicitante
	Inner Join dbo.TI_Linea as l on l.Linea = i.Linea
	Inner Join dbo.TI_Tipo as t on t.Tipo = i.Tipo
	Inner Join dbo.TI_Estado as e on e.Estado = i.Estado
	Left Join dbo.TI_Area as arti on arti.Area = i.AreaTI
	Left Join dbo.TI_Usuario as ut on ut.Usuario = i.UsuarioTI
	Left Join dbo.TI_Item as it on it.Item = i.Item
	Left Join dbo.TI_SubTipo as st on st.Tipo = i.Tipo and st.SubTipo = i.SubTipo and st.Categoria = i.Categoria
	Left Join dbo.TI_Categoria as c on c.Categoria = i.Categoria
	Left Join dbo.TI_Area as ac on ac.Area = i.AreaCausante
	Where i.IncidenciaNumero = @cIncidenciaNumero

	Select h.Secuencia, h.Estado, EstadoDescripcion = e.Descripcion, Actor = IsNull(u.NombreCompleto, 'Sistema'), h.FechaCambio, h.Observacion
	From dbo.TI_IncidenciaEstado as h
	Inner Join dbo.TI_Estado as e on e.Estado = h.Estado
	Left Join dbo.TI_Usuario as u on u.Usuario = h.UsuarioCambio
	Where h.IncidenciaNumero = @cIncidenciaNumero
	Order By h.Secuencia

	Select av.Secuencia, av.UsuarioTI, Responsable = u.NombreCompleto, av.FechaAvance, av.Detalle, av.TiempoUtilizado, av.PorcentajeAvance
	From dbo.TI_IncidenciaAvance as av
	Inner Join dbo.TI_Usuario as u on u.Usuario = av.UsuarioTI
	Where av.IncidenciaNumero = @cIncidenciaNumero
	Order By av.Secuencia

	Select m.Secuencia, m.TipoAutor, Autor = Case When m.TipoAutor = 'S' Then 'Sistema' When m.TipoAutor = 'I' Then 'Asistente TI' Else IsNull(u.NombreCompleto, 'Usuario') End,
		m.Contenido, m.FechaMensaje, m.EsInterno
	From dbo.TI_IncidenciaMensaje as m
	Left Join dbo.TI_Usuario as u on u.Usuario = m.UsuarioAutor
	Where m.IncidenciaNumero = @cIncidenciaNumero
	Order By m.Secuencia

	Select ad.Secuencia, ad.MensajeSecuencia, ad.NombreOriginal, ad.TipoMime, ad.TamanoBytes, ad.FechaRegistro, ad.UsuarioRegistro
	From dbo.TI_IncidenciaAdjunto as ad
	Where ad.IncidenciaNumero = @cIncidenciaNumero
	Order By ad.Secuencia

	Select d.Secuencia, d.CompaniaSocio, d.TipoDocumento, d.NumeroDocumento, d.Descripcion
	From dbo.TI_IncidenciaDocumento as d
	Where d.IncidenciaNumero = @cIncidenciaNumero
	Order By d.Secuencia

	Select s.Secuencia, s.AccionCodigo, AccionNombre = a.Nombre, a.NivelRiesgo, s.Estado, s.Justificacion, s.ComentarioRespuesta,
		s.UsuarioSolicitante, Solicitante = us.NombreCompleto, s.UsuarioAprobador, Aprobador = IsNull(ua.NombreCompleto, ''), s.FechaSolicitud, s.FechaRespuesta,
		ParametrosJson = IsNull(s.ParametrosJson, N'')
	From dbo.TI_SolicitudAprobacion as s
	Inner Join dbo.TI_Accion as a on a.AccionCodigo = s.AccionCodigo
	Inner Join dbo.TI_Usuario as us on us.Usuario = s.UsuarioSolicitante
	Left Join dbo.TI_Usuario as ua on ua.Usuario = s.UsuarioAprobador
	Where s.IncidenciaNumero = @cIncidenciaNumero
	Order By s.Secuencia Desc
End
Go

/* ============================== EJECUTORES AUTORIZADOS ============================== */
/*
	Contrato (ver 25_AsistenteIngenieriaAutonomo.sql): el ejecutor se invoca dentro de la transacción del DAO,
	no abre ni confirma transacciones propias, lanza Throw ante cualquier precondición inválida y devuelve
	una fila con ResultadoJson (incluye validacionPosterior) y FilasAfectadas.
*/

Create Or Alter Procedure dbo.Usp_TI_AgenteAccion_HabilitarAcceso
/*================================================================================
Objetivo            : ACC-007 · Reactivar la cuenta de colaborador del solicitante del ticket.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : Solo opera sobre el solicitante del ticket y solo sobre perfil USR, para impedir escalamiento de privilegios.
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3),
	@cIncidenciaNumero varchar(12),
	@cParametrosJson nvarchar(max),
	@cIdCorrelacion uniqueidentifier
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Declare @cObjetivo varchar(20), @cEstadoAnterior varchar(2), @nFilas int, @lValidado bit, @dFecha datetime2(0) = SysDateTime()

	If IsNull(IsJson(@cParametrosJson), 0) <> 1 Throw 50540, 'Los parámetros del ejecutor no tienen formato JSON válido.', 1
	Set @cObjetivo = Upper(LTrim(RTrim(Json_Value(@cParametrosJson, '$.usuario'))))
	If NullIf(@cObjetivo, '') Is Null Throw 50541, 'El parámetro "usuario" es obligatorio.', 1
	If Not Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero and UsuarioSolicitante = @cObjetivo)
		Throw 50542, 'Solo puede habilitarse la cuenta del solicitante del ticket investigado.', 1

	Select @cEstadoAnterior = Estado From dbo.TI_Usuario With (UpdLock, HoldLock) Where Usuario = @cObjetivo and Perfil = 'USR'
	If @cEstadoAnterior Is Null Throw 50543, 'La cuenta indicada no existe o no es una cuenta de colaborador (USR).', 1
	If @cEstadoAnterior = 'A' Throw 50544, 'La cuenta ya se encuentra activa; no existe una corrección que aplicar.', 1

	Update dbo.TI_Usuario
	Set Estado = 'A', UltimoUsuario = @cUsuario, UltimaFechaModif = @dFecha
	Where Usuario = @cObjetivo and Perfil = 'USR' and Estado = @cEstadoAnterior
	Set @nFilas = @@RowCount

	Set @lValidado = Case When @nFilas = 1 and Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cObjetivo and Estado = 'A') Then 1 Else 0 End
	If @lValidado = 0 Throw 50545, 'La postcondición no se cumplió: la cuenta no quedó activa.', 1

	Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
	Values (@cIncidenciaNumero, @cUsuario, 'S', 'TI_Usuario', @cObjetivo, 'HABILITAR_ACCESO_AGENTE', 'EXITOSO',
		Concat('{"estadoAnterior":"', @cEstadoAnterior, '","estadoNuevo":"A"}'), @cIdCorrelacion, @dFecha)

	Select ResultadoJson = (Select resultado = 'ACCESO_HABILITADO', usuario = @cObjetivo, estadoAnterior = @cEstadoAnterior, estadoNuevo = 'A', validacionPosterior = @lValidado For Json Path, Without_Array_Wrapper),
		FilasAfectadas = @nFilas
End
Go

Create Or Alter Procedure dbo.Usp_TI_AgenteAccion_LiberarTicketBloqueado
/*================================================================================
Objetivo            : ACC-004 · Liberar un ticket que quedó en PA sin ninguna aprobación pendiente que lo justifique.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : Corrige la inconsistencia de bloqueo huérfano; nunca libera un ticket con aprobación pendiente real.
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3),
	@cIncidenciaNumero varchar(12),
	@cParametrosJson nvarchar(max),
	@cIdCorrelacion uniqueidentifier
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Declare @cObjetivo varchar(12), @nFilas int, @nEstado int, @lValidado bit, @dFecha datetime2(0) = SysDateTime()

	If IsNull(IsJson(@cParametrosJson), 0) <> 1 Throw 50540, 'Los parámetros del ejecutor no tienen formato JSON válido.', 1
	Set @cObjetivo = Upper(LTrim(RTrim(IsNull(Json_Value(@cParametrosJson, '$.incidenciaNumero'), @cIncidenciaNumero))))
	If Not Exists (Select 1 From dbo.TI_Incidencia With (UpdLock, HoldLock) Where IncidenciaNumero = @cObjetivo and Estado = 'PA')
		Throw 50546, 'El ticket indicado no existe o no está bloqueado en PA.', 1
	If Exists (Select 1 From dbo.TI_SolicitudAprobacion Where IncidenciaNumero = @cObjetivo and Estado = 'P')
		Throw 50547, 'El ticket tiene una aprobación pendiente real; debe resolverse desde Gestión de Tickets, no liberarse.', 1

	Update dbo.TI_Incidencia Set Estado = 'DG', UltimoUsuario = @cUsuario, UltimaFechaModif = @dFecha Where IncidenciaNumero = @cObjetivo and Estado = 'PA'
	Set @nFilas = @@RowCount

	Select @nEstado = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_IncidenciaEstado With (UpdLock, HoldLock) Where IncidenciaNumero = @cObjetivo
	Insert dbo.TI_IncidenciaEstado (IncidenciaNumero, Secuencia, Estado, UsuarioCambio, FechaCambio, Observacion)
	Values (@cObjetivo, @nEstado, 'DG', @cUsuario, @dFecha, N'Bloqueo sin aprobación pendiente liberado mediante acción controlada del agente (ACC-004).')

	Set @lValidado = Case When @nFilas = 1 and Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cObjetivo and Estado = 'DG') Then 1 Else 0 End
	If @lValidado = 0 Throw 50545, 'La postcondición no se cumplió: el ticket continúa bloqueado.', 1

	Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
	Values (@cObjetivo, @cUsuario, 'S', 'TI_Incidencia', @cObjetivo, 'LIBERAR_BLOQUEO_AGENTE', 'EXITOSO', '{"estadoAnterior":"PA","estadoNuevo":"DG"}', @cIdCorrelacion, @dFecha)

	Select ResultadoJson = (Select resultado = 'TICKET_LIBERADO', incidenciaNumero = @cObjetivo, estadoAnterior = 'PA', estadoNuevo = 'DG', validacionPosterior = @lValidado For Json Path, Without_Array_Wrapper),
		FilasAfectadas = @nFilas
End
Go

/* Registro de ejecutores: solo si la acción existe en el catálogo del ambiente. */
If Exists (Select 1 From dbo.TI_Accion Where AccionCodigo = 'ACC-007' and Tipo = 'E')
	and Not Exists (Select 1 From dbo.TI_AgenteAccionEjecutor Where AccionCodigo = 'ACC-007')
	Insert dbo.TI_AgenteAccionEjecutor (AccionCodigo, Procedimiento, MaximoFilas, Estado, UltimoUsuario, UltimaFechaModif, ParametrosDescripcion)
	Values ('ACC-007', 'dbo.Usp_TI_AgenteAccion_HabilitarAcceso', 1, 'A', Null, SysDateTime(),
		N'{"usuario":"<usuario solicitante del ticket>"}. Solo reactiva cuentas USR inactivas del solicitante.')
Go

If Exists (Select 1 From dbo.TI_Accion Where AccionCodigo = 'ACC-004' and Tipo = 'E')
	and Not Exists (Select 1 From dbo.TI_AgenteAccionEjecutor Where AccionCodigo = 'ACC-004')
	Insert dbo.TI_AgenteAccionEjecutor (AccionCodigo, Procedimiento, MaximoFilas, Estado, UltimoUsuario, UltimaFechaModif, ParametrosDescripcion)
	Values ('ACC-004', 'dbo.Usp_TI_AgenteAccion_LiberarTicketBloqueado', 1, 'A', Null, SysDateTime(),
		N'{"incidenciaNumero":"INC-000000"}. Libera un ticket en PA sin aprobación pendiente; si se omite usa el ticket investigado.')
Go

/* Ejecuta Procedure
Exec dbo.Usp_TI_Agente_ObtenerContexto @cUsuario = 'TEC001', @cArea = 'TIC', @nSesionNumero = 1;
*/
