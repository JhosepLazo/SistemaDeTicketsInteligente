Set NoCount On
Set Xact_Abort On
Go

Use [Spring]
Go
Create Or Alter Procedure [dbo].[spUsuarioComprobarSpring](      
@Usuario varchar(20),      
@Clave varchar(20)      
)      
AS      
 
    
select dbo.Verificar_Usuario(@Usuario,@Clave)
Go
Create Or Alter Procedure [dbo].[Usp_Inc_SelectAllCargos]
AS
BEGIN
	select CodigoPuesto, Descripcion=RTRIM(Descripcion)
	from HR_PuestoEmpresa
END
Go
Create Or Alter Procedure [dbo].[Usp_Inc_SelectAllUsuario]
AS
BEGIN
	select us.usuario,pm.nombrecompleto as nombre,us.clave,
	Estado=CASE WHEN (us.Estado='A' AND pm.Estado = 'A' AND em.Estado = 'A') THEN 'A' ELSE 'I' END,
	EstadoEmpleado=CASE WHEN (em.Estado = 'A' AND em.EstadoEmpleado = 0) THEN 'A' ELSE 'I' END,
	em.codigocargo,
	pm.Documento
	from usuario us
	inner join empleadomast em
		on us.usuario = em.codigousuario
	inner join personamast pm
		on pm.persona = em.empleado
END
Go
Create Or Alter Procedure [dbo].[Usp_Inc_SelectUsuarioByUsuario]
	@Usuario varchar(20)
AS
BEGIN
	select us.usuario,pm.nombrecompleto as nombre,us.clave,
	us.Estado,
	EstadoEmpleado=CASE WHEN em.EstadoEmpleado = 0 THEN 'A' ELSE 'I' END,
	em.codigocargo,
	pm.Documento 
		--,em.centrocostos
	from usuario us
	inner join empleadomast em
		on us.usuario = em.codigousuario
	inner join personamast pm
		on pm.persona = em.empleado
	where us.usuario = @Usuario
END
Go
Create Or Alter Procedure [dbo].[Usp_Inc_SelectUsuarioRecuerdaPw](
@Usuario char(20)
)
AS
SELECT dbo.Fun_Inc_SelectUsuarioRecuerdaPw(@Usuario)
Go
Create Or Alter Procedure [dbo].[Usp_Inc_UpdateUsuarioEstadoActivo]
(
	@Usuario char(20)
)
AS
BEGIN
	IF NOT EXISTS(select * from Usuario WHERE Usuario = @Usuario AND Estado='A')  
	BEGIN
		UPDATE Usuario
		SET Estado = 'A',
			UltimoUsuario = @Usuario,
			UltimaFechaModif = GETDATE()
		WHERE Usuario = @Usuario 
		--AND Usuario IN (SELECT codigousuario FROM EmpleadoMast WHERE Estado='A' and codigousuario=@Usuario)
	END
END
Go
