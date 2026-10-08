/*
	Archivo: 34_DominiosYMaquinaEstados.sql
	Objetivo: Cerrar con restricciones los dominios que el código ya usa y hacer cumplir las transiciones de estado del ticket.
	Responsabilidad: Agregar restricciones Check sobre valores que hoy solo controlan los procedimientos, crear el catálogo de
		transiciones permitidas (TI_EstadoTransicion) con el trigger que rechaza cualquier otra, y registrar el resultado del diagnóstico
		del agente cuando TI valida la solución (V) o cancela la investigación (D).
	Dependencias: Requiere 33_AgenteFase6Mejoras.sql.
	Orden: Ejecutar después de 33_AgenteFase6Mejoras.sql.
	Consideraciones: No se inventan valores: cada dominio es el que escriben hoy los procedimientos y las pantallas. Las únicas novedades
		son CanalRegistro = ASISTENTE (ticket registrado desde el Asistente TI con la evidencia mostrada) y la escala NivelRiesgo
		(MUY_BAJO, BAJO, MEDIO, ALTO, MUY_ALTO), inferida de los dos valores existentes. Las transiciones cargadas son exactamente las
		que realizan hoy los procedimientos, incluidas las de los estados importados del legado; TI puede desactivar una (Estado = I)
		sin desplegar. Las restricciones se crean validando los datos existentes: si alguno no cumple, el script se detiene.
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
   Dominios cerrados
   ============================================================================ */

If Not Exists (Select 1 From sys.check_constraints Where name = 'CK_TI_Auditoria_TipoActor')
	Alter Table dbo.TI_Auditoria Add Constraint CK_TI_Auditoria_TipoActor Check (TipoActor In ('U','T','I','S'))
Go

-- PR en proceso, OK ejecutada con validación posterior, ER con error. Una ejecución terminada siempre tiene fecha de fin.
If Not Exists (Select 1 From sys.check_constraints Where name = 'CK_TI_EjecucionAccion_Estado')
	Alter Table dbo.TI_EjecucionAccion Add Constraint CK_TI_EjecucionAccion_Estado
		Check ((Estado = 'PR' and FechaFin Is Null) or (Estado In ('OK','ER') and FechaFin Is Not Null))
Go

-- Pendiente sin respuesta; aprobada o rechazada con aprobador y fecha; cancelada (por TI o al cerrar la investigación) con fecha.
If Not Exists (Select 1 From sys.check_constraints Where name = 'CK_TI_SolicitudAprobacion_Respuesta')
	Alter Table dbo.TI_SolicitudAprobacion Add Constraint CK_TI_SolicitudAprobacion_Respuesta
		Check ((Estado = 'P' and UsuarioAprobador Is Null and FechaRespuesta Is Null)
			or (Estado In ('A','R') and UsuarioAprobador Is Not Null and FechaRespuesta Is Not Null)
			or (Estado = 'C' and FechaRespuesta Is Not Null))
Go

If Not Exists (Select 1 From sys.check_constraints Where name = 'CK_TI_BaseConocimiento_Estado')
	Alter Table dbo.TI_BaseConocimiento Add Constraint CK_TI_BaseConocimiento_Estado Check (Estado In ('B','P','A','I'))
Go

-- P propuesto (pendiente de revisión TI), V validado por TI, D descartado. Validar exige registrar quién lo hizo.
If Not Exists (Select 1 From sys.check_constraints Where name = 'CK_TI_IncidenciaDiagnostico_Estado')
	Alter Table dbo.TI_IncidenciaDiagnostico Add Constraint CK_TI_IncidenciaDiagnostico_Estado
		Check (Estado In ('P','D') or (Estado = 'V' and UsuarioValida Is Not Null))
Go

If Not Exists (Select 1 From sys.check_constraints Where name = 'CK_TI_Incidencia_Prioridad')
	Alter Table dbo.TI_Incidencia Add Constraint CK_TI_Incidencia_Prioridad Check (Prioridad Is Null or Prioridad Between 1 and 5)
Go
If Not Exists (Select 1 From sys.check_constraints Where name = 'CK_TI_Incidencia_Impacto')
	Alter Table dbo.TI_Incidencia Add Constraint CK_TI_Incidencia_Impacto Check (Impacto Is Null or Impacto Between 1 and 5)
Go
If Not Exists (Select 1 From sys.check_constraints Where name = 'CK_TI_Incidencia_Complejidad')
	Alter Table dbo.TI_Incidencia Add Constraint CK_TI_Incidencia_Complejidad Check (Complejidad Is Null or Complejidad Between 1 and 5)
Go
If Not Exists (Select 1 From sys.check_constraints Where name = 'CK_TI_Incidencia_Calificacion')
	Alter Table dbo.TI_Incidencia Add Constraint CK_TI_Incidencia_Calificacion Check (Calificacion Is Null or Calificacion Between 1 and 5)
Go
-- Las cuatro opciones de Gestión de Tickets y de la consola del agente.
If Not Exists (Select 1 From sys.check_constraints Where name = 'CK_TI_Incidencia_TipoResolucion')
	Alter Table dbo.TI_Incidencia Add Constraint CK_TI_Incidencia_TipoResolucion
		Check (TipoResolucion Is Null or TipoResolucion In ('CORRECCION','CONFIGURACION','GUIA','REPROCESO'))
Go
-- PORTAL: formulario; ASISTENTE: formulario con la evidencia mostrada al Asistente TI; MESA_AYUDA: TI a nombre del usuario; LEGADO: importado.
If Not Exists (Select 1 From sys.check_constraints Where name = 'CK_TI_Incidencia_CanalRegistro')
	Alter Table dbo.TI_Incidencia Add Constraint CK_TI_Incidencia_CanalRegistro
		Check (CanalRegistro In ('PORTAL','ASISTENTE','MESA_AYUDA','LEGADO'))
Go

-- INTERNO es el valor que registran hoy los procedimientos; EXTERNO figura en el diccionario de datos; SISTEMA queda reservado
-- para cuentas técnicas, que no pueden iniciar sesión.
If Not Exists (Select 1 From sys.check_constraints Where name = 'CK_TI_Usuario_TipoUsuario')
	Alter Table dbo.TI_Usuario Add Constraint CK_TI_Usuario_TipoUsuario Check (TipoUsuario Is Null or TipoUsuario In ('INTERNO','EXTERNO','SISTEMA'))
Go

-- Escala ordenada: permite comparar el riesgo de una acción con el techo que TI fija para la autonomía del agente.
If Not Exists (Select 1 From sys.check_constraints Where name = 'CK_TI_Accion_NivelRiesgo')
	Alter Table dbo.TI_Accion Add Constraint CK_TI_Accion_NivelRiesgo Check (NivelRiesgo In ('MUY_BAJO','BAJO','MEDIO','ALTO','MUY_ALTO'))
Go

If Not Exists (Select 1 From sys.check_constraints Where name = 'CK_TI_AgenteSesion_Estado')
	Alter Table dbo.TI_AgenteSesion Add Constraint CK_TI_AgenteSesion_Estado
		Check (Estado In ('RECOPILANDO','OBSERVANDO','LISTO_INVESTIGAR','PENDIENTE_TI','PENDIENTE_APROBACION','SIN_EJECUTOR',
			'EJECUTANDO','CAMBIO_VALIDADO','ERROR_EJECUCION','INFORME_GRABADO','CANCELADO'))
Go

/* ============================================================================
   Máquina de estados del ticket
   ============================================================================ */

If Object_Id('dbo.TI_EstadoTransicion', 'U') Is Null
Begin
	Create Table dbo.TI_EstadoTransicion (
		EstadoOrigen				char(2)			Not Null,
		EstadoDestino				char(2)			Not Null,
		-- Proceso que realiza el cambio; documenta el flujo y orienta el mensaje cuando una transición se rechaza.
		Proceso						nvarchar(250)	Not Null,
		Estado						varchar(2)		Not Null,
		UltimoUsuario				varchar(20)		Null,
		UltimaFechaModif			datetime2(0)	Null,

		Constraint PK_TI_EstadoTransicion Primary Key (EstadoOrigen, EstadoDestino),
		Constraint FK_TI_EstadoTransicion_EstadoOrigen Foreign Key (EstadoOrigen) References dbo.TI_Estado (Estado),
		Constraint FK_TI_EstadoTransicion_EstadoDestino Foreign Key (EstadoDestino) References dbo.TI_Estado (Estado),
		Constraint CK_TI_EstadoTransicion_Distintos Check (EstadoOrigen <> EstadoDestino),
		Constraint CK_TI_EstadoTransicion_Estado Check (Estado In ('A','I'))
	)
End
Go

/*
	Transiciones que realizan hoy los procedimientos (las de los estados del legado AS, AT, CF, NP, OB, PC y PE existen porque
	Solicitar información, Resolver, No Procede y Solicitar aprobación no los excluyen). Solo se cargan las que tienen ambos estados
	en TI_Estado y no existían; una transición desactivada por TI no se reactiva al volver a ejecutar el script.
*/
Insert dbo.TI_EstadoTransicion (EstadoOrigen, EstadoDestino, Proceso, Estado, UltimoUsuario, UltimaFechaModif)
Select v.EstadoOrigen, v.EstadoDestino, v.Proceso, 'A', Null, SysDateTime()
From (Values
	-- Asignación o primer avance técnico: el ticket entra a diagnóstico.
	('NV','DG', N'Asignar ticket o registrar el primer avance técnico'),
	('RA','DG', N'Asignar ticket reabierto o registrar avance técnico'),
	('ES','DG', N'Asignar ticket escalado o registrar avance técnico'),
	-- El usuario responde la información solicitada.
	('RC','DG', N'El usuario responde la solicitud de información'),
	-- Aprobación respondida, investigación del agente cancelada o bloqueo liberado (ACC-004).
	('PA','DG', N'Responder aprobación, cancelar la investigación del agente o liberar un bloqueo sin aprobación pendiente'),
	-- TI solicita información al usuario (todo estado salvo RS, CA y PV).
	('NV','RC', N'TI solicita información al usuario'),
	('DG','RC', N'TI solicita información al usuario'),
	('AU','RC', N'TI solicita información al usuario'),
	('PA','RC', N'TI solicita información al usuario'),
	('EJ','RC', N'TI solicita información al usuario'),
	('ES','RC', N'TI solicita información al usuario'),
	('RA','RC', N'TI solicita información al usuario'),
	('AS','RC', N'TI solicita información al usuario (estado del legado)'),
	('AT','RC', N'TI solicita información al usuario (estado del legado)'),
	('CF','RC', N'TI solicita información al usuario (estado del legado)'),
	('NP','RC', N'TI solicita información al usuario (estado del legado)'),
	('OB','RC', N'TI solicita información al usuario (estado del legado)'),
	('PC','RC', N'TI solicita información al usuario (estado del legado)'),
	('PE','RC', N'TI solicita información al usuario (estado del legado)'),
	-- TI envía la solución a validación del usuario (todo estado salvo RS, CA, PV y PA).
	('NV','PV', N'TI registra la solución y la envía a validación'),
	('RC','PV', N'TI registra la solución y la envía a validación'),
	('DG','PV', N'TI registra la solución y la envía a validación'),
	('AU','PV', N'TI registra la solución y la envía a validación'),
	('EJ','PV', N'TI registra la solución y la envía a validación'),
	('ES','PV', N'TI registra la solución y la envía a validación'),
	('RA','PV', N'TI registra la solución y la envía a validación'),
	('AS','PV', N'TI registra la solución y la envía a validación (estado del legado)'),
	('AT','PV', N'TI registra la solución y la envía a validación (estado del legado)'),
	('CF','PV', N'TI registra la solución y la envía a validación (estado del legado)'),
	('NP','PV', N'TI registra la solución y la envía a validación (estado del legado)'),
	('OB','PV', N'TI registra la solución y la envía a validación (estado del legado)'),
	('PC','PV', N'TI registra la solución y la envía a validación (estado del legado)'),
	('PE','PV', N'TI registra la solución y la envía a validación (estado del legado)'),
	-- El usuario confirma la solución o indica que el problema continúa.
	('PV','RS', N'El usuario confirma la solución'),
	('PV','RA', N'El usuario indica que el problema continúa'),
	-- No Procede (todo estado salvo RS y CA).
	('NV','CA', N'TI marca el ticket como No Procede'),
	('RC','CA', N'TI marca el ticket como No Procede'),
	('DG','CA', N'TI marca el ticket como No Procede'),
	('AU','CA', N'TI marca el ticket como No Procede'),
	('PA','CA', N'TI marca el ticket como No Procede'),
	('EJ','CA', N'TI marca el ticket como No Procede'),
	('PV','CA', N'TI marca el ticket como No Procede'),
	('ES','CA', N'TI marca el ticket como No Procede'),
	('RA','CA', N'TI marca el ticket como No Procede'),
	('AS','CA', N'TI marca el ticket como No Procede (estado del legado)'),
	('AT','CA', N'TI marca el ticket como No Procede (estado del legado)'),
	('CF','CA', N'TI marca el ticket como No Procede (estado del legado)'),
	('NP','CA', N'TI marca el ticket como No Procede (estado del legado)'),
	('OB','CA', N'TI marca el ticket como No Procede (estado del legado)'),
	('PC','CA', N'TI marca el ticket como No Procede (estado del legado)'),
	('PE','CA', N'TI marca el ticket como No Procede (estado del legado)'),
	-- Solicitud de aprobación manual o del agente (todo estado salvo RS, CA, PV y PA).
	('NV','PA', N'Solicitar aprobación (manual o del agente)'),
	('RC','PA', N'Solicitar aprobación (manual o del agente)'),
	('DG','PA', N'Solicitar aprobación (manual o del agente)'),
	('AU','PA', N'Solicitar aprobación (manual o del agente)'),
	('EJ','PA', N'Solicitar aprobación (manual o del agente)'),
	('ES','PA', N'Solicitar aprobación (manual o del agente)'),
	('RA','PA', N'Solicitar aprobación (manual o del agente)'),
	('AS','PA', N'Solicitar aprobación (estado del legado)'),
	('AT','PA', N'Solicitar aprobación (estado del legado)'),
	('CF','PA', N'Solicitar aprobación manual (estado del legado)'),
	('NP','PA', N'Solicitar aprobación manual (estado del legado)'),
	('OB','PA', N'Solicitar aprobación (estado del legado)'),
	('PC','PA', N'Solicitar aprobación (estado del legado)'),
	('PE','PA', N'Solicitar aprobación (estado del legado)')
) as v (EstadoOrigen, EstadoDestino, Proceso)
Where Exists (Select 1 From dbo.TI_Estado Where Estado = v.EstadoOrigen)
	and Exists (Select 1 From dbo.TI_Estado Where Estado = v.EstadoDestino)
	and Not Exists (Select 1 From dbo.TI_EstadoTransicion as t Where t.EstadoOrigen = v.EstadoOrigen and t.EstadoDestino = v.EstadoDestino)
Go

/* Solo valida cambios de estado (Update); el estado inicial lo fija el procedimiento que registra el ticket. */
Create Or Alter Trigger dbo.Tr_TI_Incidencia_TransicionEstado
On dbo.TI_Incidencia
After Update
As
Begin
	Set NoCount On

	If Update(Estado) and Exists (
		Select 1
		From inserted as nuevo
		Inner Join deleted as anterior on anterior.IncidenciaNumero = nuevo.IncidenciaNumero
		Where nuevo.Estado <> anterior.Estado
			and Not Exists (Select 1 From dbo.TI_EstadoTransicion as t
				Where t.EstadoOrigen = anterior.Estado and t.EstadoDestino = nuevo.Estado and t.Estado = 'A')
	)
		Throw 50600, 'El ticket no puede pasar a ese estado desde su estado actual: la transición no está permitida en el flujo vigente.', 1
End
Go

/* ============================================================================
   Resultado del diagnóstico del agente
   ============================================================================ */

Create Or Alter Procedure dbo.Usp_TI_Agente_ValidarSolucion
/*================================================================================
Objetivo            : Registrar que TI comprobó que la solución de la investigación funcionó.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 01/10/2026
SP Anterior         : dbo.Usp_TI_Agente_ValidarSolucion (26_AgenteDiagnosticoSeguro.sql)
Comentario Cambios  : 07/10/2026 El diagnóstico copiado al ticket queda validado (Estado V) con el operador que lo confirma.
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3),
	@nSesionNumero bigint
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Begin Try
		Begin Transaction
		Declare @cIncidencia varchar(12), @nDiagnostico int, @cCorrelacion uniqueidentifier
		Select @cIncidencia = IncidenciaNumero, @nDiagnostico = DiagnosticoSecuencia, @cCorrelacion = IdCorrelacion
		From dbo.TI_AgenteSesion With (UpdLock, HoldLock)
		Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario and AreaTI = @cArea
			and Estado In ('INFORME_GRABADO','CAMBIO_VALIDADO') and Diagnostico Is Not Null and SolucionPropuesta Is Not Null
		If @@RowCount = 0 Throw 50534, 'Finaliza la investigación antes de validar su solución.', 1

		Update dbo.TI_AgenteSesion Set SolucionValidada = 1 Where SesionNumero = @nSesionNumero

		Update dbo.TI_IncidenciaDiagnostico Set Estado = 'V', UsuarioValida = @cUsuario
		Where IncidenciaNumero = @cIncidencia and Secuencia = @nDiagnostico and Estado = 'P'

		Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, IdCorrelacion, Fecha)
		Values (@cIncidencia, @cUsuario, 'T', 'TI_AgenteSesion', Convert(varchar(30), @nSesionNumero), 'VALIDAR_SOLUCION_AGENTE', 'VALIDADO_TI', @cCorrelacion, SysDateTime())
		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_Cancelar
/*================================================================================
Objetivo            : Cancelar una investigación que todavía no ejecutó cambios.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 01/10/2026
SP Anterior         : dbo.Usp_TI_Agente_Cancelar (27_AgenteFase1Integracion.sql)
Comentario Cambios  : 02/10/2026 Desbloquea el ticket si la aprobación cancelada era la que lo mantenía en PA.
					  07/10/2026 El diagnóstico copiado al ticket queda descartado (Estado D) con el operador que canceló.
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3),
	@nSesionNumero bigint
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Begin Try
		Begin Transaction
		Declare @cIncidencia varchar(12), @nSolicitud int, @nDiagnostico int, @cCorrelacion uniqueidentifier
		Select @cIncidencia = IncidenciaNumero, @nSolicitud = SolicitudAprobacionSecuencia, @nDiagnostico = DiagnosticoSecuencia, @cCorrelacion = IdCorrelacion
		From dbo.TI_AgenteSesion With (UpdLock, HoldLock)
		Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario and AreaTI = @cArea
			and Estado In ('RECOPILANDO','OBSERVANDO','LISTO_INVESTIGAR','PENDIENTE_TI','PENDIENTE_APROBACION','SIN_EJECUTOR')
		If @@RowCount = 0 Throw 50532, 'La investigación no puede cancelarse en su estado actual.', 1

		Update dbo.TI_SolicitudAprobacion Set Estado = 'C', FechaRespuesta = SysDateTime(), ComentarioRespuesta = N'Investigación cancelada por TI.'
		Where IncidenciaNumero = @cIncidencia and Secuencia = @nSolicitud and Estado = 'P'

		Exec dbo.Usp_TI_Agente_DesbloquearTicket @cUsuario, @cIncidencia, N'La investigación del agente fue cancelada; el ticket vuelve a diagnóstico.'

		Update dbo.TI_IncidenciaDiagnostico Set Estado = 'D', UsuarioValida = @cUsuario
		Where IncidenciaNumero = @cIncidencia and Secuencia = @nDiagnostico and Estado = 'P'

		Update dbo.TI_AgenteSesion Set Estado = 'CANCELADO', FechaCierre = SysDateTime(), UsuarioDecision = @cUsuario, Decision = 'CANCELAR'
		Where SesionNumero = @nSesionNumero

		Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, IdCorrelacion, Fecha)
		Values (@cIncidencia, @cUsuario, 'T', 'TI_AgenteSesion', Convert(varchar(30), @nSesionNumero), 'CANCELAR_INVESTIGACION', 'CANCELADO', @cCorrelacion, SysDateTime())
		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go

/* Ejecuta Procedure
Select * From dbo.TI_EstadoTransicion Order By EstadoDestino, EstadoOrigen;
Exec dbo.Usp_TI_Agente_ValidarSolucion @cUsuario = 'TEC001', @cArea = 'TIC', @nSesionNumero = 1;
*/
