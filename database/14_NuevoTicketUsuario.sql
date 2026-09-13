/*
	Archivo: 14_NuevoTicketUsuario.sql
	Objetivo: Crear los Stored Procedures requeridos por el módulo Nuevo Ticket para usuarios autenticados.
	Responsabilidad: Consultar los datos mínimos del formulario, registrar una incidencia nueva y asociar sus adjuntos sin exponer consultas SQL desde la aplicación.
	Dependencias: TI_Usuario, TI_Area, TI_Linea, TI_Tipo, TI_Estado, TI_Incidencia, TI_IncidenciaEstado, TI_IncidenciaAdjunto y TI_Auditoria.
	Orden: Ejecutar después de 13_CredencialesDesarrollo.sql.
	Consideraciones: El usuario y área se obtienen desde la identidad autenticada; la clasificación técnica permanece fuera del formulario del usuario y el ticket inicia en estado NV.
*/

Use [SistemaTicketsInteligente]
Go

/* Ejemplo: Exec dbo.Usp_TI_Obtener_DatosNuevoTicket 'USR001' */
Create Or Alter Procedure dbo.Usp_TI_Obtener_DatosNuevoTicket
/*================================================================================
Objetivo            : Obtener identidad, área y catálogos mínimos para registrar un ticket.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Primera versión para el módulo Nuevo Ticket del usuario.
================================================================================*/
	@cUsuario varchar(20)
As
Begin
	Set NoCount On

	Select u.Usuario, u.NombreCompleto, u.Area, AreaDescripcion = a.Descripcion
	From dbo.TI_Usuario as u
	Inner Join dbo.TI_Area as a on a.Area = u.Area
	Where u.Usuario = @cUsuario and u.Estado = 'A'

	Select Codigo = LTrim(RTrim(l.Linea)), Descripcion = l.Descripcion
	From dbo.TI_Linea as l
	Where l.Estado = 'A'
	Order By l.Descripcion

	Select Codigo = LTrim(RTrim(t.Tipo)), Descripcion = t.Descripcion
	From dbo.TI_Tipo as t
	Where t.Estado = 'A'
	Order By Case t.Tipo When 'INC' Then 1 When 'REQ' Then 2 When 'SOL' Then 3 Else 4 End, t.Descripcion
End
Go

/* Ejecuta Procedure */

/* Ejemplo: Exec dbo.Usp_TI_Registrar_Incidencia 'USR001', 'CMP', 'INC', 'Error al generar OC', 'Detalle del problema', 'Mensaje mostrado', '00000000-0000-0000-0000-000000000001' */
Create Or Alter Procedure dbo.Usp_TI_Registrar_Incidencia
/*================================================================================
Objetivo            : Registrar una incidencia nueva utilizando únicamente datos permitidos al usuario.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Registra cabecera, estado inicial y auditoría en una sola operación transaccional.
================================================================================*/
	@cUsuario varchar(20),
	@cLinea char(3),
	@cTipo char(3),
	@cTitulo nvarchar(250),
	@cDetalle nvarchar(max),
	@cMensajeError nvarchar(1000) = Null,
	@cIdCorrelacion uniqueidentifier
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Declare @cArea char(3), @cIncidenciaNumero varchar(12), @nCorrelativo int, @dFecha datetime2(0) = SysDateTime()

	Select @cArea = u.Area
	From dbo.TI_Usuario as u
	Where u.Usuario = @cUsuario and u.Estado = 'A'

	If @cArea Is Null Throw 50001, 'El usuario autenticado no se encuentra habilitado.', 1
	If Not Exists (Select 1 From dbo.TI_Linea Where Linea = @cLinea and Estado = 'A') Throw 50002, 'El sistema o módulo seleccionado no se encuentra disponible.', 1
	If Not Exists (Select 1 From dbo.TI_Tipo Where Tipo = @cTipo and Estado = 'A') Throw 50003, 'El tipo de ticket seleccionado no se encuentra disponible.', 1
	If NullIf(LTrim(RTrim(@cTitulo)), '') Is Null Throw 50004, 'El título del problema es obligatorio.', 1
	If NullIf(LTrim(RTrim(@cDetalle)), '') Is Null Throw 50005, 'La descripción detallada es obligatoria.', 1

	Begin Transaction
	Begin Try
		Select @nCorrelativo = IsNull(Max(Try_Convert(int, Right(IncidenciaNumero, 6))), 0) + 1
		From dbo.TI_Incidencia With (UpdLock, HoldLock)
		Where IncidenciaNumero Like 'INC-[0-9][0-9][0-9][0-9][0-9][0-9]'

		If @nCorrelativo > 999999 Throw 50006, 'Se alcanzó el límite del correlativo de incidencias.', 1
		Set @cIncidenciaNumero = 'INC-' + Right('000000' + Convert(varchar(6), @nCorrelativo), 6)

		Insert dbo.TI_Incidencia (
			IncidenciaNumero, FechaRegistro, UsuarioSolicitante, AreaSolicitante, Linea, Tipo, Estado, Titulo, Detalle, MensajeError,
			CanalRegistro, UltimoUsuario, UltimaFechaModif
		)
		Values (
			@cIncidenciaNumero, @dFecha, @cUsuario, @cArea, @cLinea, @cTipo, 'NV', LTrim(RTrim(@cTitulo)), LTrim(RTrim(@cDetalle)), NullIf(LTrim(RTrim(@cMensajeError)), ''),
			'PORTAL', @cUsuario, @dFecha
		)

		Insert dbo.TI_IncidenciaEstado (IncidenciaNumero, Secuencia, Estado, UsuarioCambio, FechaCambio, Observacion)
		Values (@cIncidenciaNumero, 1, 'NV', @cUsuario, @dFecha, N'Ticket registrado por el usuario desde el portal.')

		Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
		Values (@cIncidenciaNumero, @cUsuario, 'U', 'TI_Incidencia', @cIncidenciaNumero, 'CREAR_TICKET', 'EXITOSO', Null, @cIdCorrelacion, @dFecha)

		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch

	Select IncidenciaNumero = @cIncidenciaNumero, FechaRegistro = @dFecha
End
Go

/* Ejecuta Procedure */

/* Ejemplo: Exec dbo.Usp_TI_Registrar_IncidenciaAdjunto 'INC-000001', 'USR001', 'captura.png', 'archivo_001.png', 'uploads/incidencias/archivo_001.png', 'image/png', 120000 */
Create Or Alter Procedure dbo.Usp_TI_Registrar_IncidenciaAdjunto
/*================================================================================
Objetivo            : Registrar la metadata de un archivo adjunto perteneciente a una incidencia.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Primera versión para evidencias cargadas desde Nuevo Ticket.
================================================================================*/
	@cIncidenciaNumero varchar(12),
	@cUsuario varchar(20),
	@cNombreOriginal nvarchar(260),
	@cNombreArchivo nvarchar(260),
	@cRutaArchivo nvarchar(1000),
	@cTipoMime varchar(100),
	@nTamanoBytes bigint
As
Begin
	Set NoCount On

	Declare @nSecuencia int, @dFecha datetime2(0) = SysDateTime()

	If Not Exists (
		Select 1 From dbo.TI_Incidencia
		Where IncidenciaNumero = @cIncidenciaNumero and UsuarioSolicitante = @cUsuario
	) Throw 50007, 'La incidencia indicada no pertenece al usuario autenticado.', 1

	Select @nSecuencia = IsNull(Max(Secuencia), 0) + 1
	From dbo.TI_IncidenciaAdjunto With (UpdLock, HoldLock)
	Where IncidenciaNumero = @cIncidenciaNumero

	Insert dbo.TI_IncidenciaAdjunto (
		IncidenciaNumero, Secuencia, MensajeSecuencia, UsuarioRegistro, NombreOriginal, NombreArchivo, RutaArchivo, TipoMime, TamanoBytes, FechaRegistro
	)
	Values (
		@cIncidenciaNumero, @nSecuencia, Null, @cUsuario, @cNombreOriginal, @cNombreArchivo, @cRutaArchivo, @cTipoMime, @nTamanoBytes, @dFecha
	)
End
Go

/* Ejecuta Procedure */