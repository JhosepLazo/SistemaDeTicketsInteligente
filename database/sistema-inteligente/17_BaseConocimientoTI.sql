/*
	Archivo: 17_BaseConocimientoTI.sql
	Objetivo: Crear los Stored Procedures requeridos por el módulo Base de Conocimiento para operadores TI.
	Responsabilidad: Consultar, crear, actualizar, enviar a validación, publicar, revalidar e inactivar artículos de conocimiento manteniendo clasificación, trazabilidad y vínculo opcional con tickets resueltos.
	Dependencias: TI_BaseConocimiento, TI_Incidencia, TI_Linea, TI_Item, TI_Tipo, TI_SubTipo, TI_Categoria, TI_ItemCategoria, TI_Usuario y TI_Auditoria.
	Orden: Ejecutar después de 16_GestionTicketsTI.sql.
	Consideraciones: Los artículos publicados usan Estado A; B representa borrador, P pendiente de validación e I inactivo. Editar un artículo activo lo devuelve a P para evitar publicar contenido modificado sin validación.
*/

Use [GestionSistemas]
Go

/* Ejemplo: Exec dbo.Usp_TI_Obtener_BaseConocimientoTI */
Create Or Alter Procedure dbo.Usp_TI_Obtener_BaseConocimientoTI
/*================================================================================
Objetivo            : Obtener resumen, artículos, catálogos y tickets resueltos candidatos para la Base de Conocimiento TI.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Primera versión del módulo Base de Conocimiento para operadores TI.
================================================================================*/
As
Begin
	Set NoCount On

	Select
		Total = Count(1),
		Activos = Sum(Case When bc.Estado = 'A' Then 1 Else 0 End),
		Borradores = Sum(Case When bc.Estado = 'B' Then 1 Else 0 End),
		PendientesValidacion = Sum(Case When bc.Estado = 'P' Then 1 Else 0 End),
		PorRevisar = Sum(Case When bc.Estado = 'A' and IsNull(bc.FechaRevision, IsNull(bc.FechaValidacion, bc.FechaCreacion)) < DateAdd(day, -180, SysDateTime()) Then 1 Else 0 End),
		CandidatosDesdeTickets = (
			Select Count(1)
			From dbo.TI_Incidencia as i
			Where i.Estado = 'RS' and NullIf(LTrim(RTrim(i.SolucionTecnica)), '') Is Not Null
				and Not Exists (
					Select 1 From dbo.TI_BaseConocimiento as x
					Where x.IncidenciaOrigen = i.IncidenciaNumero and x.Estado <> 'I'
				)
		)
	From dbo.TI_BaseConocimiento as bc

	Select
		bc.ConocimientoCodigo,
		bc.Titulo,
		bc.Problema,
		bc.Solucion,
		bc.Estado,
		EstadoDescripcion = Case bc.Estado When 'A' Then 'Publicado' When 'B' Then 'Borrador' When 'P' Then 'Pendiente de validación' When 'I' Then 'Inactivo' Else bc.Estado End,
		bc.Linea,
		LineaDescripcion = IsNull(l.Descripcion, ''),
		bc.Item,
		ItemDescripcion = IsNull(it.Descripcion, ''),
		bc.Tipo,
		TipoDescripcion = IsNull(t.Descripcion, ''),
		bc.SubTipo,
		SubTipoDescripcion = IsNull(st.Descripcion, ''),
		bc.Categoria,
		CategoriaDescripcion = IsNull(c.Descripcion, ''),
		bc.IncidenciaOrigen,
		Validador = IsNull(u.NombreCompleto, ''),
		bc.FechaCreacion,
		bc.FechaValidacion,
		bc.FechaRevision,
		RequiereRevision = Convert(bit, Case When bc.Estado = 'A' and IsNull(bc.FechaRevision, IsNull(bc.FechaValidacion, bc.FechaCreacion)) < DateAdd(day, -180, SysDateTime()) Then 1 Else 0 End)
	From dbo.TI_BaseConocimiento as bc
	Left Join dbo.TI_Linea as l on l.Linea = bc.Linea
	Left Join dbo.TI_Item as it on it.Item = bc.Item
	Left Join dbo.TI_Tipo as t on t.Tipo = bc.Tipo
	Left Join dbo.TI_SubTipo as st on st.Tipo = bc.Tipo and st.SubTipo = bc.SubTipo and st.Categoria = bc.Categoria
	Left Join dbo.TI_Categoria as c on c.Categoria = bc.Categoria
	Left Join dbo.TI_Usuario as u on u.Usuario = bc.UsuarioValida
	Order By Case bc.Estado When 'P' Then 0 When 'B' Then 1 When 'A' Then 2 Else 3 End,
		IsNull(bc.FechaRevision, IsNull(bc.FechaValidacion, bc.FechaCreacion)) Desc

	Select Codigo = l.Linea, l.Descripcion
	From dbo.TI_Linea as l
	Where l.Estado = 'A'
	Order By l.Descripcion

	Select Codigo = i.Item, i.Linea, i.Descripcion
	From dbo.TI_Item as i
	Where i.Estado = 'A'
	Order By i.Linea, i.Descripcion

	Select Codigo = t.Tipo, t.Descripcion
	From dbo.TI_Tipo as t
	Where t.Estado = 'A'
	Order By t.Descripcion

	Select Codigo = c.Categoria, c.Descripcion
	From dbo.TI_Categoria as c
	Where c.Estado = 'A'
	Order By c.Descripcion

	Select Codigo = st.SubTipo, st.Tipo, st.Categoria, st.Descripcion
	From dbo.TI_SubTipo as st
	Where st.Estado = 'A'
	Order By st.Tipo, st.Categoria, st.Descripcion

	Select Top (50)
		i.IncidenciaNumero,
		i.Titulo,
		i.Detalle,
		i.MensajeError,
		i.Linea,
		i.Item,
		i.Tipo,
		i.SubTipo,
		i.Categoria,
		i.CausaRaiz,
		i.SolucionTecnica,
		i.FechaCierre
	From dbo.TI_Incidencia as i
	Where i.Estado = 'RS' and NullIf(LTrim(RTrim(i.SolucionTecnica)), '') Is Not Null
		and Not Exists (
			Select 1 From dbo.TI_BaseConocimiento as bc
			Where bc.IncidenciaOrigen = i.IncidenciaNumero and bc.Estado <> 'I'
		)
	Order By i.FechaCierre Desc, i.UltimaFechaModif Desc
End
Go

/* Ejecuta Procedure */

/* Ejemplo: Exec dbo.Usp_TI_Obtener_DetalleBaseConocimientoTI 'KB-000001' */
Create Or Alter Procedure dbo.Usp_TI_Obtener_DetalleBaseConocimientoTI
/*================================================================================
Objetivo            : Obtener el contenido completo y la clasificación de un artículo de conocimiento.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Devuelve la información necesaria para lectura, edición y validación del artículo.
================================================================================*/
	@cConocimientoCodigo varchar(20)
As
Begin
	Set NoCount On

	Select
		bc.ConocimientoCodigo,
		bc.Titulo,
		bc.Problema,
		bc.Sintomas,
		bc.MensajeError,
		bc.Causa,
		bc.Solucion,
		bc.Procedimiento,
		bc.Linea,
		LineaDescripcion = IsNull(l.Descripcion, ''),
		bc.Item,
		ItemDescripcion = IsNull(it.Descripcion, ''),
		bc.Tipo,
		TipoDescripcion = IsNull(t.Descripcion, ''),
		bc.SubTipo,
		SubTipoDescripcion = IsNull(st.Descripcion, ''),
		bc.Categoria,
		CategoriaDescripcion = IsNull(c.Descripcion, ''),
		bc.IncidenciaOrigen,
		bc.Estado,
		EstadoDescripcion = Case bc.Estado When 'A' Then 'Publicado' When 'B' Then 'Borrador' When 'P' Then 'Pendiente de validación' When 'I' Then 'Inactivo' Else bc.Estado End,
		bc.UsuarioValida,
		Validador = IsNull(u.NombreCompleto, ''),
		bc.FechaCreacion,
		bc.FechaValidacion,
		bc.FechaRevision,
		RequiereRevision = Convert(bit, Case When bc.Estado = 'A' and IsNull(bc.FechaRevision, IsNull(bc.FechaValidacion, bc.FechaCreacion)) < DateAdd(day, -180, SysDateTime()) Then 1 Else 0 End)
	From dbo.TI_BaseConocimiento as bc
	Left Join dbo.TI_Linea as l on l.Linea = bc.Linea
	Left Join dbo.TI_Item as it on it.Item = bc.Item
	Left Join dbo.TI_Tipo as t on t.Tipo = bc.Tipo
	Left Join dbo.TI_SubTipo as st on st.Tipo = bc.Tipo and st.SubTipo = bc.SubTipo and st.Categoria = bc.Categoria
	Left Join dbo.TI_Categoria as c on c.Categoria = bc.Categoria
	Left Join dbo.TI_Usuario as u on u.Usuario = bc.UsuarioValida
	Where bc.ConocimientoCodigo = @cConocimientoCodigo
End
Go

/* Ejecuta Procedure */

/* Ejemplo: Exec dbo.Usp_TI_Crear_BaseConocimientoTI 'TEC001', N'Título', N'Problema', N'Síntomas', Null, N'Causa', N'Solución', N'Procedimiento', 'CMP', 'ORDEN_COMPRA', 'INC', 'DAT', 'DATOS', Null, '00000000-0000-0000-0000-000000000001' */
Create Or Alter Procedure dbo.Usp_TI_Crear_BaseConocimientoTI
/*================================================================================
Objetivo            : Crear un artículo de conocimiento en estado borrador con clasificación válida y trazabilidad.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Genera correlativo KB, valida clasificación y permite relacionar un ticket resuelto como origen.
================================================================================*/
	@cUsuario varchar(20),
	@cTitulo nvarchar(250),
	@cProblema nvarchar(max),
	@cSintomas nvarchar(max),
	@cMensajeError nvarchar(1000) = Null,
	@cCausa nvarchar(max),
	@cSolucion nvarchar(max),
	@cProcedimiento nvarchar(max) = Null,
	@cLinea char(3),
	@cItem varchar(20),
	@cTipo char(3),
	@cSubTipo char(3),
	@cCategoria varchar(20),
	@cIncidenciaOrigen varchar(12) = Null,
	@cIdCorrelacion uniqueidentifier
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Declare @nNumero int, @cCodigo varchar(20), @dFecha datetime2(0) = SysDateTime()

	If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Estado = 'A') Throw 50200, 'El operador TI no es válido o se encuentra inactivo.', 1
	If Not Exists (Select 1 From dbo.TI_Item Where Item = @cItem and Linea = @cLinea and Estado = 'A') Throw 50201, 'La línea y el item seleccionados no forman una clasificación válida.', 1
	If Not Exists (Select 1 From dbo.TI_SubTipo Where Tipo = @cTipo and SubTipo = @cSubTipo and Categoria = @cCategoria and Estado = 'A') Throw 50202, 'El tipo, subtipo y categoría seleccionados no forman una clasificación válida.', 1
	If Not Exists (Select 1 From dbo.TI_ItemCategoria Where Item = @cItem and Categoria = @cCategoria and Estado = 'A') Throw 50203, 'El item no se encuentra habilitado para la categoría seleccionada.', 1
	If @cIncidenciaOrigen Is Not Null and Not Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaOrigen and Estado = 'RS') Throw 50204, 'El ticket de origen debe existir y encontrarse resuelto.', 1

	Begin Try
		Begin Transaction

		Select @nNumero = IsNull(Max(Try_Convert(int, Replace(ConocimientoCodigo, 'KB-', ''))), 0) + 1
		From dbo.TI_BaseConocimiento With (UpdLock, HoldLock)
		Where ConocimientoCodigo Like 'KB-%'

		Set @cCodigo = 'KB-' + Right('000000' + Convert(varchar(6), @nNumero), 6)

		Insert dbo.TI_BaseConocimiento (
			ConocimientoCodigo, Titulo, Problema, Sintomas, MensajeError, Causa, Solucion, Procedimiento, Linea, Item, Tipo, SubTipo, Categoria,
			IncidenciaOrigen, Estado, UsuarioValida, FechaCreacion, FechaValidacion, FechaRevision
		)
		Values (
			@cCodigo, LTrim(RTrim(@cTitulo)), LTrim(RTrim(@cProblema)), LTrim(RTrim(@cSintomas)), NullIf(LTrim(RTrim(@cMensajeError)), ''),
			LTrim(RTrim(@cCausa)), LTrim(RTrim(@cSolucion)), NullIf(LTrim(RTrim(@cProcedimiento)), ''), @cLinea, @cItem, @cTipo, @cSubTipo, @cCategoria,
			@cIncidenciaOrigen, 'B', Null, @dFecha, Null, Null
		)

		Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
		Values (@cIncidenciaOrigen, @cUsuario, 'T', 'TI_BaseConocimiento', @cCodigo, 'CREAR_CONOCIMIENTO', 'EXITOSO', Null, @cIdCorrelacion, @dFecha)

		Commit Transaction
		Select @cCodigo as ConocimientoCodigo
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go

/* Ejecuta Procedure */

/* Ejemplo: Exec dbo.Usp_TI_Actualizar_BaseConocimientoTI 'TEC001', 'KB-000001', N'Título actualizado', N'Problema', N'Síntomas', Null, N'Causa', N'Solución', Null, 'CMP', 'ORDEN_COMPRA', 'INC', 'DAT', 'DATOS', Null, '00000000-0000-0000-0000-000000000001' */
Create Or Alter Procedure dbo.Usp_TI_Actualizar_BaseConocimientoTI
/*================================================================================
Objetivo            : Actualizar el contenido de un artículo y devolver a validación cualquier publicación modificada.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Los artículos activos pasan a P; los inactivos editados vuelven a B y deben validar nuevamente.
================================================================================*/
	@cUsuario varchar(20),
	@cConocimientoCodigo varchar(20),
	@cTitulo nvarchar(250),
	@cProblema nvarchar(max),
	@cSintomas nvarchar(max),
	@cMensajeError nvarchar(1000) = Null,
	@cCausa nvarchar(max),
	@cSolucion nvarchar(max),
	@cProcedimiento nvarchar(max) = Null,
	@cLinea char(3),
	@cItem varchar(20),
	@cTipo char(3),
	@cSubTipo char(3),
	@cCategoria varchar(20),
	@cIncidenciaOrigen varchar(12) = Null,
	@cIdCorrelacion uniqueidentifier
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Declare @cEstado varchar(2), @cNuevoEstado varchar(2), @dFecha datetime2(0) = SysDateTime()

	Select @cEstado = Estado From dbo.TI_BaseConocimiento Where ConocimientoCodigo = @cConocimientoCodigo
	If @cEstado Is Null Throw 50210, 'El artículo de conocimiento no existe.', 1
	If Not Exists (Select 1 From dbo.TI_Item Where Item = @cItem and Linea = @cLinea and Estado = 'A') Throw 50211, 'La línea y el item seleccionados no forman una clasificación válida.', 1
	If Not Exists (Select 1 From dbo.TI_SubTipo Where Tipo = @cTipo and SubTipo = @cSubTipo and Categoria = @cCategoria and Estado = 'A') Throw 50212, 'El tipo, subtipo y categoría seleccionados no forman una clasificación válida.', 1
	If Not Exists (Select 1 From dbo.TI_ItemCategoria Where Item = @cItem and Categoria = @cCategoria and Estado = 'A') Throw 50213, 'El item no se encuentra habilitado para la categoría seleccionada.', 1
	If @cIncidenciaOrigen Is Not Null and Not Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaOrigen and Estado = 'RS') Throw 50214, 'El ticket de origen debe existir y encontrarse resuelto.', 1

	Set @cNuevoEstado = Case When @cEstado = 'A' Then 'P' When @cEstado = 'I' Then 'B' Else @cEstado End

	Begin Try
		Begin Transaction

		Update dbo.TI_BaseConocimiento
		Set Titulo = LTrim(RTrim(@cTitulo)),
			Problema = LTrim(RTrim(@cProblema)),
			Sintomas = LTrim(RTrim(@cSintomas)),
			MensajeError = NullIf(LTrim(RTrim(@cMensajeError)), ''),
			Causa = LTrim(RTrim(@cCausa)),
			Solucion = LTrim(RTrim(@cSolucion)),
			Procedimiento = NullIf(LTrim(RTrim(@cProcedimiento)), ''),
			Linea = @cLinea,
			Item = @cItem,
			Tipo = @cTipo,
			SubTipo = @cSubTipo,
			Categoria = @cCategoria,
			IncidenciaOrigen = @cIncidenciaOrigen,
			Estado = @cNuevoEstado,
			UsuarioValida = Case When @cNuevoEstado In ('B', 'P') Then Null Else UsuarioValida End,
			FechaValidacion = Case When @cNuevoEstado In ('B', 'P') Then Null Else FechaValidacion End,
			FechaRevision = @dFecha
		Where ConocimientoCodigo = @cConocimientoCodigo

		Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
		Values (@cIncidenciaOrigen, @cUsuario, 'T', 'TI_BaseConocimiento', @cConocimientoCodigo, 'ACTUALIZAR_CONOCIMIENTO', 'EXITOSO', Null, @cIdCorrelacion, @dFecha)

		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go

/* Ejecuta Procedure */

/* Ejemplo: Exec dbo.Usp_TI_EnviarValidacion_BaseConocimientoTI 'TEC001', 'KB-000005', '00000000-0000-0000-0000-000000000001' */
Create Or Alter Procedure dbo.Usp_TI_EnviarValidacion_BaseConocimientoTI
/*================================================================================
Objetivo            : Enviar un borrador a la bandeja de validación antes de su publicación.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Implementa transición B -> P y registra auditoría.
================================================================================*/
	@cUsuario varchar(20),
	@cConocimientoCodigo varchar(20),
	@cIdCorrelacion uniqueidentifier
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Declare @cIncidenciaOrigen varchar(12), @dFecha datetime2(0) = SysDateTime()
	Select @cIncidenciaOrigen = IncidenciaOrigen From dbo.TI_BaseConocimiento Where ConocimientoCodigo = @cConocimientoCodigo and Estado = 'B'
	If @@RowCount = 0 Throw 50220, 'Solo los artículos en borrador pueden enviarse a validación.', 1

	Begin Try
		Begin Transaction
		Update dbo.TI_BaseConocimiento Set Estado = 'P', FechaRevision = @dFecha Where ConocimientoCodigo = @cConocimientoCodigo and Estado = 'B'
		Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
		Values (@cIncidenciaOrigen, @cUsuario, 'T', 'TI_BaseConocimiento', @cConocimientoCodigo, 'ENVIAR_VALIDACION_CONOCIMIENTO', 'EXITOSO', Null, @cIdCorrelacion, @dFecha)
		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go

/* Ejecuta Procedure */

/* Ejemplo: Exec dbo.Usp_TI_Validar_BaseConocimientoTI 'TEC001', 'KB-000005', '00000000-0000-0000-0000-000000000001' */
Create Or Alter Procedure dbo.Usp_TI_Validar_BaseConocimientoTI
/*================================================================================
Objetivo            : Publicar un artículo pendiente o revalidar un artículo activo después de su revisión periódica.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Permite P -> A y revalidación de A actualizando responsable y fechas de control.
================================================================================*/
	@cUsuario varchar(20),
	@cConocimientoCodigo varchar(20),
	@cIdCorrelacion uniqueidentifier
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Declare @cEstado varchar(2), @cIncidenciaOrigen varchar(12), @dFecha datetime2(0) = SysDateTime()
	Select @cEstado = Estado, @cIncidenciaOrigen = IncidenciaOrigen From dbo.TI_BaseConocimiento Where ConocimientoCodigo = @cConocimientoCodigo
	If @cEstado Is Null Throw 50230, 'El artículo de conocimiento no existe.', 1
	If @cEstado Not In ('P', 'A') Throw 50231, 'El artículo debe estar pendiente de validación o publicado para poder validarse.', 1

	Begin Try
		Begin Transaction
		Update dbo.TI_BaseConocimiento
		Set Estado = 'A', UsuarioValida = @cUsuario, FechaValidacion = @dFecha, FechaRevision = @dFecha
		Where ConocimientoCodigo = @cConocimientoCodigo

		Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
		Values (@cIncidenciaOrigen, @cUsuario, 'T', 'TI_BaseConocimiento', @cConocimientoCodigo, Case When @cEstado = 'A' Then 'REVALIDAR_CONOCIMIENTO' Else 'PUBLICAR_CONOCIMIENTO' End, 'EXITOSO', Null, @cIdCorrelacion, @dFecha)
		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go

/* Ejecuta Procedure */

/* Ejemplo: Exec dbo.Usp_TI_Inactivar_BaseConocimientoTI 'TEC001', 'KB-000005', '00000000-0000-0000-0000-000000000001' */
Create Or Alter Procedure dbo.Usp_TI_Inactivar_BaseConocimientoTI
/*================================================================================
Objetivo            : Retirar un artículo de la publicación sin eliminar su historial ni trazabilidad.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Cambia cualquier artículo vigente a I y conserva la información histórica.
================================================================================*/
	@cUsuario varchar(20),
	@cConocimientoCodigo varchar(20),
	@cIdCorrelacion uniqueidentifier
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Declare @cIncidenciaOrigen varchar(12), @cEstado varchar(2), @dFecha datetime2(0) = SysDateTime()
	Select @cEstado = Estado, @cIncidenciaOrigen = IncidenciaOrigen From dbo.TI_BaseConocimiento Where ConocimientoCodigo = @cConocimientoCodigo
	If @cEstado Is Null Throw 50240, 'El artículo de conocimiento no existe.', 1
	If @cEstado = 'I' Throw 50241, 'El artículo ya se encuentra inactivo.', 1

	Begin Try
		Begin Transaction
		Update dbo.TI_BaseConocimiento Set Estado = 'I', FechaRevision = @dFecha Where ConocimientoCodigo = @cConocimientoCodigo
		Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
		Values (@cIncidenciaOrigen, @cUsuario, 'T', 'TI_BaseConocimiento', @cConocimientoCodigo, 'INACTIVAR_CONOCIMIENTO', 'EXITOSO', Null, @cIdCorrelacion, @dFecha)
		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch
End
Go

/* Ejecuta Procedure */
