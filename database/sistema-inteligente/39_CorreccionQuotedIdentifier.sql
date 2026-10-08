/*
	Archivo: 39_CorreccionQuotedIdentifier.sql
	Objetivo: Recompilar con QUOTED_IDENTIFIER ON los procedimientos y triggers del sistema que se crearon con la opción apagada.
	Responsabilidad: Volver a aplicar, sin cambiar una sola línea, la definición vigente de cada módulo Usp_TI_* y Tr_TI_* afectado.
	Dependencias: Requiere los scripts 00 a 38.
	Orden: Ejecutar después de 38_PermisosMinimos.sql. En una instalación nueva no encuentra nada que corregir.
	Consideraciones: SQL Server guarda en cada módulo la opción con que se creó. Con QUOTED_IDENTIFIER apagado, un procedimiento no
		puede escribir en una tabla con índice filtrado (TI_SolicitudAprobacion, TI_AgenteSesion): por ejemplo,
		Usp_TI_Solicitar_AprobacionTicket fallaba al solicitar una aprobación manual. Los scripts 10 a 22 no fijan la opción y el
		instalador no usaba -I; desde ahora InstalarLocal.ps1 ejecuta con -I y este script corrige las bases ya instaladas.
		Se usa Alter para conservar los permisos concedidos en 38_PermisosMinimos.sql. Los módulos del legado (Usp_Inc_*) no se tocan.
*/

Use [GestionSistemas]
Go

Set Ansi_Nulls On
Set Quoted_Identifier On
Go

Declare @tModulos table (Nombre sysname Primary Key, Definicion nvarchar(max))
Declare @cNombre sysname, @cDefinicion nvarchar(max), @nPosicion int

Insert @tModulos (Nombre, Definicion)
Select objeto.name, modulo.definition
From sys.sql_modules as modulo
Inner Join sys.objects as objeto on objeto.object_id = modulo.object_id
Where modulo.uses_quoted_identifier = 0 and objeto.schema_id = Schema_Id('dbo')
	and ((objeto.type = 'P' and objeto.name Like 'Usp[_]TI[_]%') or (objeto.type = 'TR' and objeto.name Like 'Tr[_]TI[_]%'))

While Exists (Select 1 From @tModulos)
Begin
	Select Top (1) @cNombre = Nombre, @cDefinicion = Definicion From @tModulos Order By Nombre
	-- La definición guardada empieza con "Create" (SQL Server conserva Create aunque el script dijera Create Or Alter).
	Set @nPosicion = PatIndex(N'%Create[ ' + NChar(9) + NChar(10) + NChar(13) + N']%', @cDefinicion)
	If @nPosicion = 0 Throw 50680, 'No se encontró la instrucción Create en la definición de un módulo.', 1
	Set @cDefinicion = Stuff(@cDefinicion, @nPosicion, 6, N'Alter')
	Exec sys.sp_executesql @cDefinicion
	Print Concat(N'Recompilado con QUOTED_IDENTIFIER ON: ', @cNombre)
	Delete @tModulos Where Nombre = @cNombre
End
Go

/* Ejecuta Procedure
Select objeto.name From sys.sql_modules as modulo Inner Join sys.objects as objeto on objeto.object_id = modulo.object_id
Where modulo.uses_quoted_identifier = 0 and (objeto.name Like 'Usp[_]TI[_]%' or objeto.name Like 'Tr[_]TI[_]%');
*/
