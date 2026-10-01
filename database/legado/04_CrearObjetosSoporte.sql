Set NoCount On
Set Xact_Abort On
Go

Use [IntranetCalimod]
Go
Create Or Alter View dbo.vwUsuarioPerfilMenu
As
Select
    pu.UsuarioId,
    pu.SistemaId,
    pu.PerfilId,
    p.Descripcion as PerfilDescripcion,
    p.Habilitado as PerfilHabilitado,
    m.MenuId,
    m.Descripcion as MenuDescripcion,
    m.PadreId,
    m.Posicion,
    m.Icono,
    m.Habilitado as MenuHabilitado,
    m.FormUrl
From dbo.PerfilUsuario as pu
Inner Join dbo.Perfil as p
    on p.PerfilId = pu.PerfilId and p.SistemaId = pu.SistemaId
Inner Join dbo.PerfilMenu as pm
    on pm.PerfilId = pu.PerfilId and pm.SistemaId = pu.SistemaId
Inner Join dbo.Menu as m
    on m.MenuId = pm.MenuId and m.SistemaId = pm.SistemaId
Go

Use [GestionSistemas]
Go
Create Or Alter Function dbo.Fun_Inc_Inc03UsuarioVerifica
(
    @Usuario varchar(20),
    @Clave varchar(20)
)
Returns bit
As
Begin
    Declare @Resultado bit = 0
    If Exists
    (
        Select 1
        From dbo.Inc03Usuario
        Where Inc03Usuario = @Usuario
            and RTrim(Inc03Clave) = @Clave
            and IsNull(Inc03Estado, 'A') = 'A'
    )
        Set @Resultado = 1
    Return @Resultado
End
Go

Use [Spring]
Go
Create Or Alter Function dbo.Verificar_Usuario
(
    @Usuario varchar(20),
    @Clave varchar(20)
)
Returns bit
As
Begin
    Declare @Resultado bit = 0
    If Exists
    (
        Select 1
        From dbo.Usuario
        Where Usuario = @Usuario
            and RTrim(Clave) = @Clave
            and IsNull(Estado, 'A') = 'A'
    )
        Set @Resultado = 1
    Return @Resultado
End
Go

Create Or Alter Function dbo.Fun_Inc_SelectUsuarioRecuerdaPw
(
    @Usuario varchar(20)
)
Returns varchar(20)
As
Begin
    Declare @Clave varchar(20)
    Select @Clave = RTrim(Clave)
    From dbo.Usuario
    Where Usuario = @Usuario
    Return @Clave
End
Go
