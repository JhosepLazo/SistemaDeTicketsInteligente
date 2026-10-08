/*
	Archivo: 28_AgenteFase2Integracion.sql
	Objetivo: Integrar las investigaciones del agente con Gestión de Tickets y habilitar la supervisión de TI.
	Responsabilidad: Permitir que SUP/ADM consulten y reasignen investigaciones, exponer los expedientes del agente desde el ticket,
		entregar catálogos para cerrar el caso desde la consola y reforzar la segregación de funciones en aprobaciones y resoluciones.
	Dependencias: Requiere 27_AgenteFase1Integracion.sql.
	Orden: Ejecutar después de 27_AgenteFase1Integracion.sql.
	Consideraciones: La lectura de una investigación ajena es solo de consulta; todos los SP que modifican una sesión siguen exigiendo ser su operador.
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

/* ============================== CONSULTA Y SUPERVISIÓN ============================== */

-- dbo.Usp_TI_Agente_ObtenerContexto: la versión vigente está en 33_AgenteFase6Mejoras.sql (aquí había una versión anterior que ese script reemplaza).
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_Listar
/*================================================================================
Objetivo            : Listar las investigaciones del operador o, para SUP/ADM, todas las investigaciones.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 01/10/2026
SP Anterior         : dbo.Usp_TI_Agente_Listar (26_AgenteDiagnosticoSeguro.sql)
Comentario Cambios  : 02/10/2026 Agrega el alcance de supervisión y el operador responsable de cada investigación.
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3),
	@lTodas bit = 0
As
Begin
	Set NoCount On

	Declare @cPerfil char(3)
	Select @cPerfil = Perfil From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM')
	If @cPerfil Is Null Throw 50500, 'El operador TI no se encuentra habilitado.', 1
	If @lTodas = 1 and @cPerfil Not In ('SUP','ADM') Throw 50548, 'Solo un supervisor o administrador puede consultar las investigaciones de todo el equipo.', 1

	Select Top (200) s.SesionNumero, IsNull(s.IncidenciaNumero, '') IncidenciaNumero, s.IdCorrelacion, s.DescripcionInicial, s.Estado, s.FechaInicio,
		s.SolucionValidada, IsNull(s.ConocimientoCodigo, '') ConocimientoCodigo, s.Confianza, IsNull(s.AccionCodigo, '') AccionCodigo,
		s.UsuarioTI, NombreOperador = op.NombreCompleto, EsPropietario = Convert(bit, Case When s.UsuarioTI = @cUsuario Then 1 Else 0 End)
	From dbo.TI_AgenteSesion s
	Inner Join dbo.TI_Usuario op on op.Usuario = s.UsuarioTI
	Where (@lTodas = 1 or (s.UsuarioTI = @cUsuario and s.AreaTI = @cArea))
	Order By s.FechaInicio Desc, s.SesionNumero Desc
End
Go

-- dbo.Usp_TI_Agente_Reasignar: la versión vigente está en 33_AgenteFase6Mejoras.sql (aquí había una versión anterior que ese script reemplaza).
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_Catalogos
/*================================================================================
Objetivo            : Entregar los catálogos que la consola del agente necesita para cerrar un caso o reasignarlo.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : Devuelve áreas activas (área causante) y operadores TI activos (reasignación).
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3)
As
Begin
	Set NoCount On

	If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM')) Throw 50500, 'El operador TI no se encuentra habilitado.', 1

	Select Codigo = Area, Descripcion From dbo.TI_Area Where Estado = 'A' Order By Descripcion
	Select Codigo = Usuario, Descripcion = Concat(NombreCompleto, ' · ', Perfil) From dbo.TI_Usuario Where Estado = 'A' and Perfil In ('TEC','SUP','ADM') Order By NombreCompleto
End
Go

/* ============================== EXPEDIENTES DESDE EL TICKET ============================== */

-- dbo.Usp_TI_Agente_ListarPorTicket: la versión vigente está en 33_AgenteFase6Mejoras.sql (aquí había una versión anterior que ese script reemplaza).
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_ObtenerInforme
/*================================================================================
Objetivo            : Obtener el expediente Markdown de una investigación asociada a un ticket.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 02/10/2026
SP Anterior         : Ninguno
Comentario Cambios  : Lectura para cualquier operador TI activo; el expediente forma parte del historial técnico del ticket.
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3),
	@nSesionNumero bigint
As
Begin
	Set NoCount On

	If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM')) Throw 50500, 'El operador TI no se encuentra habilitado.', 1

	Select InformeMarkdown, IncidenciaNumero = IsNull(IncidenciaNumero, '')
	From dbo.TI_AgenteSesion
	Where SesionNumero = @nSesionNumero and NullIf(InformeMarkdown, '') Is Not Null
		and (IncidenciaNumero Is Not Null or UsuarioTI = @cUsuario)
	If @@RowCount = 0 Throw 50553, 'La investigación no tiene un expediente disponible para consulta.', 1
End
Go

/* ============================== SEGREGACIÓN DE FUNCIONES ============================== */

-- dbo.Usp_TI_Responder_AprobacionTicket: la versión vigente está en 35_ControlAgenteYAutonomia.sql (aquí había una versión anterior que ese script reemplaza).
Go

Create Or Alter Procedure dbo.Usp_TI_Resolver_TicketTI
/*================================================================================
Objetivo            : Registrar la resolución técnica y enviar el ticket a validación del usuario.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : dbo.Usp_TI_Resolver_TicketTI (16_GestionTicketsTI.sql)
Comentario Cambios  : 02/10/2026 Un ticket bloqueado en PA (aprobación pendiente) no puede resolverse hasta responder la aprobación.
================================================================================*/
	@cUsuario varchar(20), @cArea char(3), @cIncidenciaNumero varchar(12), @cCausaRaiz nvarchar(max), @cSolucion nvarchar(max),
	@cRespuestaUsuario nvarchar(max), @cTipoResolucion varchar(20), @cIdCorrelacion uniqueidentifier
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Begin Try
		Begin Transaction
		Declare @nMensaje int, @nEstado int, @dFecha datetime2(0) = SysDateTime()

		If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC', 'SUP', 'ADM')) Throw 50222, 'El operador TI no es válido.', 1
		If Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero and Estado = 'PA') Throw 50244, 'El ticket tiene una aprobación pendiente; resuélvela antes de enviar la solución.', 1
		If Not Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero and Estado Not In ('RS', 'CA', 'PV')) Throw 50223, 'El ticket no admite resolución en su estado actual.', 1
		If NullIf(LTrim(RTrim(@cCausaRaiz)), '') Is Null Throw 50224, 'Registra la causa raíz validada.', 1
		If NullIf(LTrim(RTrim(@cSolucion)), '') Is Null Throw 50225, 'Registra la solución aplicada.', 1
		If NullIf(LTrim(RTrim(@cRespuestaUsuario)), '') Is Null Throw 50226, 'Registra la respuesta que recibirá el usuario.', 1

		Update dbo.TI_Incidencia
		Set Estado = 'PV', CausaRaiz = LTrim(RTrim(@cCausaRaiz)), SolucionTecnica = LTrim(RTrim(@cSolucion)), RespuestaUsuario = LTrim(RTrim(@cRespuestaUsuario)),
			TipoResolucion = NullIf(LTrim(RTrim(@cTipoResolucion)), ''), FechaAtencion = IsNull(FechaAtencion, @dFecha), UltimoUsuario = @cUsuario, UltimaFechaModif = @dFecha
		Where IncidenciaNumero = @cIncidenciaNumero

		Select @nMensaje = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_IncidenciaMensaje With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero
		Insert dbo.TI_IncidenciaMensaje (IncidenciaNumero, Secuencia, UsuarioAutor, TipoAutor, Contenido, FechaMensaje, EsInterno)
		Values (@cIncidenciaNumero, @nMensaje, @cUsuario, 'T', LTrim(RTrim(@cRespuestaUsuario)), @dFecha, 0)

		Select @nEstado = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_IncidenciaEstado With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero
		Insert dbo.TI_IncidenciaEstado (IncidenciaNumero, Secuencia, Estado, UsuarioCambio, FechaCambio, Observacion)
		Values (@cIncidenciaNumero, @nEstado, 'PV', @cUsuario, @dFecha, N'Solución técnica registrada; pendiente de validación del usuario.')

		Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
		Values (@cIncidenciaNumero, @cUsuario, 'T', 'TI_Incidencia', @cIncidenciaNumero, 'ENVIAR_A_VALIDACION', 'EXITOSO', Null, @cIdCorrelacion, @dFecha)
		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go

/* Ejecuta Procedure
Exec dbo.Usp_TI_Agente_Listar @cUsuario = 'SUP001', @cArea = 'TIC', @lTodas = 1;
Exec dbo.Usp_TI_Agente_ListarPorTicket @cUsuario = 'TEC001', @cArea = 'TIC', @cIncidenciaNumero = 'INC-000001';
*/
