/*
	Archivo: 14_NuevoTicketUsuario.sql
	Objetivo: Crear los Stored Procedures requeridos por el módulo Nuevo Ticket para usuarios autenticados.
	Responsabilidad: Consultar los datos mínimos del formulario, registrar una incidencia nueva y asociar sus adjuntos sin exponer consultas SQL desde la aplicación.
	Dependencias: TI_Usuario, TI_Area, TI_Linea, TI_Tipo, TI_Estado, TI_Incidencia, TI_IncidenciaEstado, TI_IncidenciaAdjunto y TI_Auditoria.
	Orden: Ejecutar después de 13_CredencialesDesarrollo.sql.
	Consideraciones: El usuario y área se obtienen desde la identidad autenticada; la clasificación técnica permanece fuera del formulario del usuario y el ticket inicia en estado NV.
*/

Use [GestionSistemas]
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

-- dbo.Usp_TI_Registrar_Incidencia: la versión vigente está en 19_MejorasFuncionalesSinIA.sql (aquí había una versión anterior que ese script reemplaza).
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
