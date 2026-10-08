/*
	Archivo: 38_PermisosMinimos.sql
	Objetivo: Separar los permisos de la API, de la lectura del agente y de la escritura del agente.
	Responsabilidad: Crear los roles Rol_TI_Api, Rol_TI_AgenteLectura y Rol_TI_AgenteEscritura y concederles Execute solo sobre los
		procedimientos que cada identidad necesita.
	Dependencias: Requiere que existan todos los procedimientos (scripts 00 a 37).
	Orden: Ejecutar al final de la instalación y otra vez cada vez que un script agregue procedimientos.
	Consideraciones: No crea logins ni usuarios: TI los crea en el servidor, sin permisos administrativos, y los agrega a su rol
		(ejemplo al final). Ningún rol recibe permisos sobre tablas: los procedimientos acceden a ellas por encadenamiento de propiedad
		(todos son de dbo). La API no ejecuta herramientas de diagnóstico ni ejecutores; la lectura del agente solo ejecuta herramientas
		Usp_TI_AgenteDiag_*; la escritura del agente solo ejecutores Usp_TI_AgenteAccion_*. Con seguridad integrada en desarrollo
		(sysadmin) los roles no limitan nada; la separación se comprueba en el servidor de la empresa.
*/

Use [GestionSistemas]
Go

Set Xact_Abort On
Set Ansi_Nulls On
Set Quoted_Identifier On
Go

If Database_Principal_Id('Rol_TI_Api') Is Null Create Role Rol_TI_Api
Go
If Database_Principal_Id('Rol_TI_AgenteLectura') Is Null Create Role Rol_TI_AgenteLectura
Go
If Database_Principal_Id('Rol_TI_AgenteEscritura') Is Null Create Role Rol_TI_AgenteEscritura
Go

Declare @cSentencias nvarchar(max)

Select @cSentencias = String_Agg(Convert(nvarchar(max), Concat(N'Grant Execute On dbo.', QuoteName(procedimiento.name), N' To ',
	Case When procedimiento.name Like 'Usp[_]TI[_]AgenteDiag[_]%' Then N'Rol_TI_AgenteLectura'
		When procedimiento.name Like 'Usp[_]TI[_]AgenteAccion[_]%' Then N'Rol_TI_AgenteEscritura'
		Else N'Rol_TI_Api' End, N';')), Char(10))
From sys.procedures as procedimiento
Where procedimiento.schema_id = Schema_Id('dbo') and procedimiento.name Like 'Usp[_]TI[_]%'

Exec sys.sp_executesql @cSentencias
Go

/* Ejecuta Procedure
-- Con logins creados por TI (sin permisos administrativos):
Create User [usuario_api] For Login [usuario_api];
Alter Role Rol_TI_Api Add Member [usuario_api];
Create User [usuario_agente_lectura] For Login [usuario_agente_lectura];
Alter Role Rol_TI_AgenteLectura Add Member [usuario_agente_lectura];
Create User [usuario_agente_escritura] For Login [usuario_agente_escritura];
Alter Role Rol_TI_AgenteEscritura Add Member [usuario_agente_escritura];

-- Comprobación de permisos por rol:
Select rol = principal.name, procedimiento = objeto.name
From sys.database_permissions as permiso
Inner Join sys.database_principals as principal on principal.principal_id = permiso.grantee_principal_id
Inner Join sys.objects as objeto on objeto.object_id = permiso.major_id
Where principal.name Like 'Rol[_]TI[_]%' Order By rol, procedimiento;
*/
