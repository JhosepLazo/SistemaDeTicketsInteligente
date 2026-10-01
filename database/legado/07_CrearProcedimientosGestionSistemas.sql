Set NoCount On
Set Xact_Abort On
Go

Use [GestionSistemas]
Go
Create Or Alter Procedure [dbo].[Usp_Inc_CalificacionByUserSelect] (@Usuario VARCHAR(12))
AS
BEGIN
    SELECT      TOP 1 i.Inc20Incidencia,
                      i.Inc20Detalle Titulo,
                      A.Inc21Detalle,
                      i.Inc20Estado,
                      Puntos,
                      c.Comentarios
      FROM      dbo.Inc20Incidencia i
      LEFT JOIN dbo.Calificacion c
        ON c.Inc20Incidencia = i.Inc20Incidencia
      LEFT JOIN dbo.Inc21Avance A
        ON A.Inc20Incidencia = i.Inc20Incidencia
     WHERE      i.Inc20UsuarioCrea = @Usuario
       AND      i.Inc20Estado           = 'CF'
       AND      c.Puntos IS NULL
	   AND		CONVERT(DATE, a.Inc21Fecha) > CONVERT(DATE,'2022-08-01')
     ORDER BY i.Inc20FechaCierre DESC;
END;
Go
Create Or Alter Procedure [dbo].[Usp_Inc_CalificacionInsert](
	@Inc20Incidencia VARCHAR(12),
	@TipoPregunta VARCHAR(20),
	@Puntos INT,
	@Comentarios varchar(500),
	@SeSoluciono varchar(10),
	@UsuarioRegistro varchar(20)
)
AS
BEGIN
		INSERT INTO dbo.Calificacion (Inc20Incidencia,
		                               TipoPregunta,
		                               Puntos,
		                               Comentarios,
		                               FechaRegistro, 
									   SeSoluciono,
									   UsuarioRegistro)
		VALUES (@Inc20Incidencia, -- Inc20Incidencia - varchar(12)
		        @TipoPregunta, -- TipoPregunta - varchar(20)
		        @Puntos, -- Puntos - int
		        @Comentarios, -- Comentarios - varchar(500)
		        GETDATE(), -- FechaRegistro - datetime
				@SeSoluciono,
				@UsuarioRegistro 
		    )
END
Go
Create Or Alter Procedure [dbo].[Usp_Inc_EnvioCorreo_Automatico]  
  
@vrecipients varchar(max),  
@vsubject varchar(max),  
@vbody varchar(max),  
@vcopy_recipients varchar(max)  
  
as   
begin   
  
--declare  @vcopy_recipients varchar(max)   
declare  @vprofile_name varchar(max)   
  
set @vcopy_recipients  = (select isnull(Ltrim(Rtrim(Inc03Correo)),'') from [dbo].[Inc03Usuario] where Inc03Usuario = @vcopy_recipients)  
set @vprofile_name = 'Area_TI'  
  
  
  
EXEC msdb.dbo.sp_send_dbmail  
@recipients = @vrecipients,  
@subject = @vsubject,  
@body = @vbody,  
@copy_recipients = @vcopy_recipients,  
@profile_name = @vprofile_name  
  
end
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc00CorrelativoGenera](
@Codigo varchar(50),
@Serie varchar(50),
@UsuarioModifica varchar(20)
)
AS
BEGIN
IF NOT EXISTS(SELECT * FROM Inc00Correlativo WHERE Codigo = @Codigo AND Serie = @Serie)
BEGIN
/* INGRESAMOS UN REGISTRO CON NUMERACION CERO */
INSERT INTO Inc00Correlativo
           (Codigo,
            Serie,
            Correlativo,
            UsuarioModifica,
            FechaModifica)
     VALUES
           (@Codigo,
            @Serie,
            0,
            @UsuarioModifica,
            GETDATE())
END
/* BUSCAMOS EL CODIGO Y ADICIONAMOS EN EL CORRELATIVO */
DECLARE @Correlativo INT
SELECT @Correlativo=Correlativo+1 FROM Inc00Correlativo WHERE Codigo = @Codigo AND Serie = @Serie
/* ACTUALIZAMOS EL NUEVO CORRELATIVO */
UPDATE Inc00Correlativo
   SET Correlativo = @Correlativo,
       UsuarioModifica = @UsuarioModifica,
       FechaModifica = GETDATE()
WHERE  Codigo = @Codigo
AND    Serie = @Serie
/* DEVOLVEMOS EL NUMERO GENERADO */
SELECT Correlativo=@Correlativo
END
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc01AreaSelecyByLinea]  
@Inc01Linea varchar(10)
AS    
    
SET NOCOUNT ON    
    
SELECT [Inc02Area]   
FROM [Inc01Linea]    
WHERE Inc01Linea=@Inc01Linea and Estado = 'A'
Go
Create Or Alter Procedure Usp_Inc_Inc01LineaByLinea(    
@Inc01Linea char(3)    
)    
AS    
BEGIN    
SET NOCOUNT ON    
SELECT Inc01Linea,     
Inc02Area,     
Inc01Descripcion,     
Inc01UsuarioCrea,     
Inc01FechaCrea,     
Inc01UsuarioModifica,     
Inc01FechaModifica,  
Estado    
FROM Inc01Linea    
WHERE Inc01Linea = @Inc01Linea    
END
Go
Create Or Alter Procedure Usp_Inc_Inc01LineaInsert(    
@Inc01Linea char(3),    
@Inc02Area char(3),    
@Inc01Descripcion varchar(60),    
@Inc01UsuarioCrea varchar(20),  
@Estado varchar (2)    
)    
AS    
BEGIN    
INSERT INTO Inc01Linea    
           ([Inc01Linea]    
           ,[Inc02Area]    
           ,[Inc01Descripcion]    
           ,[Inc01UsuarioCrea]    
           ,[Inc01FechaCrea]    
     ,[Estado]  
           )    
     VALUES    
           (@Inc01Linea,    
            @Inc02Area,    
            @Inc01Descripcion,    
            @Inc01UsuarioCrea,    
            getdate(),  
   @Estado    
   )    
END
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc01LineaSelectAll](
@Ordenar varchar(250)=''
)
AS
BEGIN
SET NOCOUNT ON
DECLARE @SQL1 VARCHAR(MAX)
SELECT @SQL1 = 'SELECT  
Inc01Linea.Inc02Area, 
Inc02AreaDes=Inc02Area.Inc02Descripcion,
Inc01Linea,
Inc01Descripcion, 
Inc01UsuarioCrea, 
Inc01FechaCrea, 
Inc01UsuarioModifica, 
Inc01FechaModifica
FROM Inc01Linea INNER JOIN Inc02Area 
ON   Inc01Linea.Inc02Area = Inc02Area.Inc02Area '
IF @Ordenar=''
SET @SQL1 = @SQL1 + ' ORDER BY Inc02AreaDes, Inc01Descripcion '
ELSE 
SET @SQL1 = @SQL1 + ' ORDER BY ' + @Ordenar + ' '
PRINT (@SQL1)
EXEC (@SQL1)
END
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc01LineaSelecyByArea]    
 @Inc02Area char(3)    
AS    
    
SET NOCOUNT ON    
    
SELECT [Inc01Linea],    
 [Inc01Descripcion]    
FROM [Inc01Linea]    
WHERE [Inc02Area] = @Inc02Area   
AND  Estado = 'A'  
ORDER BY [Inc01Descripcion] ASC
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc01LineaSelecyByAreaModificado]  
AS    
    
SET NOCOUNT ON    
    
SELECT [Inc01Linea],    
 [Inc01Descripcion]    
FROM [Inc01Linea]    
WHERE Estado = 'A'  
ORDER BY [Inc01Descripcion] ASC
Go
Create Or Alter Procedure Usp_Inc_Inc01LineaUpdate(    
@Inc01Linea char(3),    
@Inc02Area char(3),    
@Inc01Descripcion varchar(60),    
@Inc01UsuarioModifica varchar(20) ,  
@Estado  varchar (2)   
)    
AS    
BEGIN    
UPDATE Inc01Linea    
   SET [Inc02Area] = @Inc02Area,    
       [Inc01Descripcion] = @Inc01Descripcion,    
       [Inc01UsuarioModifica] = @Inc01UsuarioModifica,    
       [Inc01FechaModifica] = GETDATE(),  
    [Estado] = @Estado    
 WHERE Inc01Linea = @Inc01Linea    
END
Go
Create Or Alter Procedure Usp_Inc_Inc02AreaByArea(    
@Inc02Area char(3)    
)    
AS    
BEGIN    
SET NOCOUNT ON    
SELECT Inc02Area,    
Inc02Descripcion,    
Inc02Telefono,    
Inc02UsuarioCrea,    
Inc02FechaCrea,    
Inc02UsuarioModifica,    
Inc02FechaModifica,  
Estado    
FROM Inc02Area    
WHERE Inc02Area = @Inc02Area    
END
Go
Create Or Alter Procedure Usp_Inc_Inc02AreaInsert(    
@Inc02Area char(3),    
@Inc02Descripcion varchar(60),    
@Inc02Telefono varchar(20),    
@Inc02UsuarioCrea varchar(20),  
@Estado varchar (2)    
)    
AS    
BEGIN    
INSERT INTO Inc02Area (Inc02Area, Inc02Descripcion, Inc02Telefono, Inc02UsuarioCrea, Inc02FechaCrea,Estado)    
VALUES (@Inc02Area, @Inc02Descripcion, @Inc02Telefono, @Inc02UsuarioCrea, GETDATE(),@Estado)    
END
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc02AreaSelectAll](
@Ordenar varchar(250)=''
)
AS
BEGIN
SET NOCOUNT ON
DECLARE @SQL1 VARCHAR(MAX)
SELECT @SQL1 = 'SELECT Inc02Area,
Inc02Descripcion,
Inc02Telefono,
Inc02UsuarioCrea,
Inc02FechaCrea,
Inc02UsuarioModifica,
Inc02FechaModifica
FROM [Inc02Area] WHERE Estado=''A'' '
IF @Ordenar=''
SET @SQL1 = @SQL1 + ' ORDER BY [Inc02Descripcion] '
ELSE 
SET @SQL1 = @SQL1 + ' ORDER BY ' + @Ordenar + ' '
PRINT (@SQL1)
EXEC (@SQL1)
END
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc02AreaSelectTI]    
    
AS    
    
SET NOCOUNT ON    
    
SELECT [Inc02Area],    
 [Inc02Descripcion]    
FROM [Inc02Area]    
 WHERE Inc02Area in ('002','003')    
 AND ESTADO = 'A'
Go
Create Or Alter Procedure Usp_Inc_Inc02AreaUpdate(    
@Inc02Area char(3),    
@Inc02Descripcion varchar(60),    
@Inc02Telefono varchar(20),    
@Inc02UsuarioModifica varchar(20),  
@Estado varchar (2)    
)    
AS    
BEGIN    
UPDATE Inc02Area SET     
Inc02Descripcion = @Inc02Descripcion,     
Inc02Telefono = @Inc02Telefono,    
Inc02UsuarioModifica = @Inc02UsuarioModifica,     
Inc02FechaModifica = GETDATE(),  
Estado = @Estado     
WHERE Inc02Area = @Inc02Area    
END
Go
Create Or Alter Procedure Usp_Inc_Inc03UsuarioByUsuario(
@Inc03Usuario varchar(20))
AS  
BEGIN  
SET NOCOUNT ON  
SELECT 
Inc03Usuario.Inc03Usuario,
Inc03Usuario.Inc03Descripcion,
Inc03Usuario.Inc03Clave,
Inc03Usuario.Inc02Area,
Inc03Usuario.Inc07Cargo,
Inc02Descripcion, 
Inc07Descripcion,
Inc03Usuario.Inc03Estado,
Inc03Usuario.Inc03Correo,
Inc03Usuario.Inc03Anexo
FROM Inc03Usuario  
LEFT OUTER JOIN Inc02Area ON Inc02Area.Inc02Area = Inc03Usuario.Inc02Area
LEFT OUTER JOIN Inc07Cargo ON Inc07Cargo.Inc07Cargo = Inc03Usuario.Inc07Cargo
WHERE Inc03Usuario = @Inc03Usuario
END
Go
--Usp_Inc_Inc03UsuarioSelect 'RCARRASCO'
Create Or Alter Procedure [dbo].[Usp_Inc_Inc03UsuarioCount]
(
	@Inc03Usuario varchar(20)
)
AS
SET NOCOUNT ON
SELECT count([Inc03Usuario])
FROM [Inc03Usuario]
WHERE [Inc03Usuario] = @Inc03Usuario
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc03UsuarioGestionCalidad]
	 
AS
SET NOCOUNT ON
   SELECT Inc03Usuario,
          Inc03Descripcion
        
					 FROM dbo.Inc03Usuario
					WHERE Inc03Usuario IN ( 'JMALLMA', 'KVEGA', 'LCASTILLO' )
					  AND Inc03Estado = 'A'
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc03UsuarioInsert]
(
	@Inc03Usuario varchar(20),
	@Inc03Descripcion varchar(255),
	@Inc03Clave varchar(100),
	@Inc07Cargo varchar(4),
	@Inc03Estado char(1),
	@Inc03EstadoEmpleado char(1)
	--@Inc02Area	varchar(3)='',
	--@Inc03Correo	varchar(100)='',
	--@Inc03Anexo	varchar(10)='',
	--@Inc03UsuarioCrea	varchar(20)=''
)
AS
BEGIN
	INSERT INTO Inc03Usuario
	(
		Inc03Usuario,
		Inc03Descripcion,
		Inc03Clave,
		Inc07Cargo,
		Inc03Estado,
		Inc03EstadoEmpleado,
		Inc03FechaCrea
		--Inc02Area,
		--Inc03Correo,
		--Inc03Anexo,
		--Inc03UsuarioCrea,
	)
	VALUES
	(
		@Inc03Usuario,
		@Inc03Descripcion,
		@Inc03Clave,
		@Inc07Cargo,
		@Inc03Estado,
		@Inc03EstadoEmpleado,
		GETDATE()
		--@Inc02Area,
		--@Inc03Correo,
		--@Inc03Anexo,
		--@Inc03UsuarioCrea,
	)
END
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc03UsuarioSelectArea]
	@Inc03Usuario varchar(20)
AS
SET NOCOUNT ON
SELECT convert(varchar,isnull(Inc02Area,'')) as Inc02Area
FROM Inc03Usuario
WHERE Inc03Usuario = @Inc03Usuario
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc03UsuarioSelectByNombre]
	@Inc03NombreUsuario varchar(255)
AS
SET NOCOUNT ON
SELECT Inc03Usuario,Inc03Descripcion
FROM Inc03Usuario
WHERE Inc03Descripcion like '%' + @Inc03NombreUsuario + '%' 
	and inc03estado <>'I'
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc03UsuarioSelectCorreo]
	@Inc03Usuario varchar(20)
AS
SET NOCOUNT ON
SELECT convert(varchar,isnull(Inc03Correo,'')) as Inc03Correo
FROM Inc03Usuario
WHERE Inc03Usuario = @Inc03Usuario
Go
Create Or Alter Procedure Usp_Inc_Inc03UsuarioSelectGrid(
@Inc03Descripcion varchar(20),
@Inc02Area varchar(3),
@Inc07Descripcion varchar(20),
@Inc03Estado varchar(1),
@Ordenar varchar(50)=''
)
AS  
BEGIN  
DECLARE @SQL1 VARCHAR(MAX)
DECLARE @OrderSql VARCHAR(MAX)
SELECT @SQL1 = 'SELECT 
Inc03Usuario.Inc03Usuario,
Inc03Usuario.Inc03Descripcion,
Inc03Usuario.Inc03Clave,
Inc03Usuario.Inc02Area,
Inc03Usuario.Inc07Cargo,
Inc02Descripcion, 
Inc07Descripcion,
Inc03Usuario.Inc03Correo,
Inc03Usuario.Inc03Anexo,
Inc03Estado=CASE WHEN Inc03Usuario.Inc03Estado=''A'' THEN ''Activo'' ELSE ''Inactivo'' END,
Inc03EstadoEmpleado=CASE WHEN Inc03Usuario.Inc03EstadoEmpleado=''A'' THEN ''Activo'' ELSE ''Inactivo'' END
FROM Inc03Usuario  
LEFT OUTER JOIN Inc02Area ON Inc02Area.Inc02Area = Inc03Usuario.Inc02Area
LEFT OUTER JOIN Inc07Cargo ON Inc07Cargo.Inc07Cargo = Inc03Usuario.Inc07Cargo 
WHERE NOT Inc03Usuario.Inc03Usuario IS NULL
'
IF LEN(RTRIM(@Inc03Descripcion))>0
SET @SQL1 = @SQL1 + ' AND Inc03Usuario.Inc03Descripcion LIKE ''%' + @Inc03Descripcion + '%'' '
IF LEN(RTRIM(@Inc02Area))>0
SET @SQL1 = @SQL1 + ' AND Inc03Usuario.Inc02Area = ''' + @Inc02Area + ''' '
IF LEN(RTRIM(@Inc07Descripcion))>0
SET @SQL1 = @SQL1 + ' AND Inc07Descripcion LIKE ''%' + @Inc07Descripcion + '%'' '
IF LEN(RTRIM(@Inc03Estado))>0
SET @SQL1 = @SQL1 + ' AND Inc03Usuario.Inc03Estado = ''' + @Inc03Estado + ''' '
IF RTRIM(@Ordenar)=''
	BEGIN
		SET @OrderSql = ' ORDER BY Inc03Usuario.Inc03Usuario ASC'
	END
	ELSE
	BEGIN
		SET @OrderSql = ' ORDER BY ' + @Ordenar
	END
EXECUTE( @SQL1 + @OrderSql)
END
Go
Create Or Alter Procedure dbo.Usp_Inc_Inc03UsuariosTI
AS
SET NOCOUNT ON
BEGIN
SELECT rtrim(Inc03Usuario) as Inc03Usuario,Inc03Descripcion,Inc03Clave,Inc02Area,Inc07Cargo,Inc03Estado
FROM Inc03Usuario
WHERE Inc02Area in ('002','003')
AND   Inc03Estado = 'A'
ORDER BY Inc03Descripcion
END
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc03UsuarioUpdate]
(
	@Inc03Usuario varchar(20),
	@Inc03Descripcion varchar(255),
	@Inc03Clave varchar(100),
	@Inc07Cargo varchar(4),
	@Inc03Estado char(1),
	@Inc03EstadoEmpleado char(1)
)
AS
BEGIN
	UPDATE Inc03Usuario
	SET Inc03Descripcion = @Inc03Descripcion,
		Inc03Clave = @Inc03Clave,
		Inc07Cargo = @Inc07Cargo,
		Inc03Estado = @Inc03Estado,
		Inc03EstadoEmpleado = @Inc03EstadoEmpleado,
		Inc03FechaModifica = GETDATE()
	WHERE Inc03Usuario = @Inc03Usuario
END
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc03UsuarioUpdate_Completo]
(
	@Inc03Usuario varchar(20),
	@Inc03Descripcion varchar(255),
	@Inc03Clave varchar(100),
	@Inc07Cargo varchar(4),
	@Inc03Estado char(1),
	@Inc02Area	varchar(3),
	@Inc03Correo	varchar(100),
	@Inc03Anexo	varchar(10),
	@Inc03UsuarioModifica	varchar(20)
)
AS
BEGIN
	UPDATE Inc03Usuario
	SET Inc03Descripcion = @Inc03Descripcion,
		--Inc03Clave = @Inc03Clave,
		Inc07Cargo = @Inc07Cargo,
		Inc03Estado = @Inc03Estado,
		Inc02Area  = @Inc02Area,
		Inc03Correo  = @Inc03Correo,
		Inc03Anexo  = @Inc03Anexo,
		Inc03UsuarioModifica  = @Inc03UsuarioModifica,
		Inc03FechaModifica = GETDATE()
	WHERE Inc03Usuario = @Inc03Usuario
END
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc03UsuarioVerifica](
@Usuario varchar(20),
@Clave varchar(20)
)
AS
select dbo.Fun_Inc_Inc03UsuarioVerifica(@Usuario,@Clave)
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc04ItemByItem](
@Inc04Item	varchar(20)
)
AS
BEGIN
SET NOCOUNT ON
SELECT Inc04Item,
Inc01Linea.Inc02Area,
Inc04Item.Inc01Linea,
Inc01LineaDes=Inc01Linea.Inc01Descripcion,
Inc04Descripcion,
Inc04UsuarioCrea,
Inc04FechaCrea,
Inc04UsuarioModifica,
Inc04FechaModifica
FROM   Inc04Item INNER JOIN Inc01Linea
ON     Inc04Item.Inc01Linea = Inc01Linea.Inc01Linea
WHERE  Inc04Item = @Inc04Item
END
Go
Create Or Alter Procedure Usp_Inc_Inc04ItemInsert    
(    
@Inc04Item varchar(20),    
@Inc01Linea char(3),    
@Inc04Descripcion varchar(255),    
@Inc04UsuarioCrea varchar(20),  
@Estado varchar (2)    
)    
AS    
BEGIN    
SET NOCOUNT ON    
INSERT INTO [Inc04Item]    
(    
Inc04Item,    
Inc01Linea,    
Inc04Descripcion,    
Inc04UsuarioCrea,    
Inc04FechaCrea,  
Estado    
)    
VALUES    
(    
@Inc04Item,    
@Inc01Linea,    
@Inc04Descripcion,    
@Inc04UsuarioCrea,    
GETDATE(),  
@Estado    
  
)    
END
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc04ItemSelectAll](
@Ordenar varchar(250)=''
)
AS
BEGIN
SET NOCOUNT ON
DECLARE @SQL1 VARCHAR(MAX)
SELECT @SQL1 = 'SELECT 
Inc01Linea.Inc02Area,
Inc02AreaDes=Inc02Area.Inc02Descripcion,
Inc04Item.Inc01Linea,
Inc01LineaDes=Inc01Linea.Inc01Descripcion,
Inc04Item,
Inc04Descripcion,
Inc04UsuarioCrea,
Inc04FechaCrea,
Inc04UsuarioModifica,
Inc04FechaModifica
FROM   Inc04Item INNER JOIN Inc01Linea
ON     Inc04Item.Inc01Linea = Inc01Linea.Inc01Linea
INNER JOIN Inc02Area
ON     Inc01Linea.Inc02Area = Inc02Area.Inc02Area '
IF @Ordenar=''
SET @SQL1 = @SQL1 + ' ORDER BY Inc02AreaDes, Inc01LineaDes, Inc04Descripcion '
ELSE 
SET @SQL1 = @SQL1 + ' ORDER BY ' + @Ordenar + ' '
PRINT (@SQL1)
EXEC (@SQL1)
END
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc04ItemSelectByLinea]
	@Inc01Linea char(3)
AS
SET NOCOUNT ON
SELECT [Inc04Item],
	[Inc01Linea],
	[Inc04Descripcion]
FROM [Inc04Item]
WHERE [Inc01Linea] = @Inc01Linea
order by [Inc04Descripcion]
Go
Create Or Alter Procedure Usp_Inc_Inc04ItemUpdate    
(    
@Inc04Item varchar(20),    
@Inc01Linea char(3),    
@Inc04Descripcion varchar(255),    
@Inc04UsuarioModifica varchar(20),  
@Estado varchar (2)    
)    
AS    
BEGIN    
SET NOCOUNT ON    
    
UPDATE Inc04Item    
SET    Inc01Linea = @Inc01Linea,    
    Inc04Descripcion = @Inc04Descripcion,    
    Inc04UsuarioModifica = @Inc04UsuarioModifica,    
    Inc04FechaModifica = GETDATE(),  
 Estado = @Estado    
WHERE  Inc04Item = @Inc04Item    
END
Go
Create Or Alter Procedure Usp_Inc_Inc05TipoByTipo(
@Inc05Tipo CHAR(3)
)
AS
BEGIN
	SELECT Inc05Tipo,
	Inc05Descripcion,
	Inc05Abreviatura,
	Inc05UsuarioCrea,
	Inc05FechaCrea,
	Inc05UsuarioModifica,
	Inc05FechaModifica
	FROM Inc05Tipo
	WHERE Inc05Tipo = @Inc05Tipo
END
Go
Create Or Alter Procedure Usp_Inc_Inc05TipoInsert(    
@Inc05Tipo char(3),    
@Inc05Descripcion varchar(60),    
@Inc05Abreviatura varchar(60),    
@Inc05UsuarioCrea varchar(20),  
@Estado Varchar (2)    
)    
AS    
BEGIN    
INSERT INTO Inc05Tipo (Inc05Tipo, Inc05Descripcion, Inc05Abreviatura, Inc05UsuarioCrea, Inc05FechaCrea,Estado)    
VALUES (@Inc05Tipo, @Inc05Descripcion, @Inc05Abreviatura, @Inc05UsuarioCrea, GETDATE(),@Estado)    
END
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc05TipoSelectAll]    
@Ordenar varchar(250) = ''    
    
As    
    
Begin    
  Set Nocount On    
 Select Inc05Tipo, Inc05Descripcion, Inc05Abreviatura, Inc05UsuarioCrea, Inc05FechaCrea, Inc05UsuarioModifica, Inc05FechaModifica  From Inc05Tipo  Where Estado = 'A'  Order By Inc05Descripcion Asc     
End
Go
Create Or Alter Procedure Usp_Inc_Inc05TipoUpdate(    
@Inc05Tipo char(3),    
@Inc05Descripcion varchar(60),    
@Inc05Abreviatura varchar(60),    
@Inc05UsuarioModifica varchar(20),  
@Estado varchar(2)    
)    
AS    
BEGIN    
UPDATE Inc05Tipo SET     
Inc05Descripcion = @Inc05Descripcion,     
Inc05Abreviatura = @Inc05Abreviatura,    
Inc05UsuarioModifica = @Inc05UsuarioModifica,     
Inc05FechaModifica = GETDATE(),  
Estado = @Estado     
WHERE Inc05Tipo = @Inc05Tipo    
END
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc06EstadoInsert](
	@Inc06Estado char(2),
	@Inc06Descripcion varchar(60),
	@Inc06UsuarioCrea varchar(20)
)
AS
	BEGIN
		INSERT INTO Inc06Estado
				   ([Inc06Estado]
				   ,[Inc06Descripcion]
				   ,[Inc06UsuarioCrea]
				   ,[Inc06FechaCrea]
				   )
			 VALUES
				   (@Inc06Estado,
					@Inc06Descripcion,
					@Inc06UsuarioCrea,
					getdate()
					)
	END
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc06EstadoSelectAll]
AS
BEGIN
SET NOCOUNT ON
SELECT  
	Inc06Estado, 
	Inc06Descripcion, 
	Inc06UsuarioCrea, 
	Inc06FechaCrea, 
	Inc06UsuarioModifica, 
	Inc06FechaModifica
FROM Inc06Estado
END
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc06EstadoUpdate](
	@Inc06Estado char(2),
	@Inc06Descripcion varchar(60),
	@Inc06UsuarioModifica varchar(20)
)
AS
	BEGIN
		UPDATE Inc06Estado
		   SET [Inc06Descripcion] = @Inc06Descripcion,
			   [Inc06UsuarioModifica] = @Inc06UsuarioModifica,
			   [Inc06FechaModifica] = GETDATE()
		 WHERE Inc06Estado = @Inc06Estado
	END
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc07CargoCount]
(
	@Inc07Cargo int
)
AS
BEGIN
SET NOCOUNT ON
SELECT count([Inc07Cargo])
FROM [Inc07Cargo]
WHERE [Inc07Cargo] = @Inc07Cargo
END
Go
Create Or Alter Procedure Usp_Inc_Inc07CargoInsert(
@Inc07Cargo int,
@Inc07Descripcion varchar(60),
@Inc07UsuarioCrea varchar(20)
)
AS
BEGIN
INSERT INTO [dbo].[Inc07Cargo]
           ([Inc07Cargo]
           ,[Inc07Descripcion]
           ,[Inc07UsuarioCrea]
           ,[Inc07FechaCrea]
           )
     VALUES
           (@Inc07Cargo,
           @Inc07Descripcion,
           @Inc07UsuarioCrea,
           GETDATE()
		   )
END
Go
Create Or Alter Procedure Usp_Inc_Inc07CargoUpdate(
@Inc07Cargo int,
@Inc07Descripcion varchar(60),
@Inc07UsuarioModifica varchar(20)
)
AS
BEGIN
UPDATE [dbo].[Inc07Cargo]
   SET [Inc07Descripcion] = @Inc07Descripcion,
       [Inc07UsuarioModifica] = @Inc07UsuarioModifica,
       [Inc07FechaModifica] = GETDATE()
 WHERE [Inc07Cargo] = @Inc07Cargo
END
Go
Create Or Alter Procedure Usp_Inc_Inc08SubTipoByTipo    
@Inc05Tipo char(3) = ''    
--@Inc05Tipo char(3)   
As    
    
Set Nocount On    
    
Set @Inc05Tipo = '000'   
    
Select Inc08SubTipo, Inc08Descripcion    
From inc08subtipo    
Where Inc05Tipo =@Inc05Tipo --and Estado='A'      
Order By Inc08Descripcion Asc
Go
/****************************/  
/* Inicio : Store Procedure */  
/****************************/  
  
-- Usp_Inc_Inc08SubTipoByTipoSubTipo 1  
Create Or Alter Procedure Usp_Inc_Inc08SubTipoByTipoSubTipo  
@IdCategoria int   
As  
Begin  
 Set Nocount On  
  
 Select IdCategoria, Inc08Descripcion, Inc08Abreviatura,  Inc08UsuarioCrea, Inc08FechaCrea, Inc08UsuarioModifica, Inc08FechaModifica,   
   Inc08SubTipo, Estado  
 From Inc08SubTipo   
 Where IdCategoria = @IdCategoria  
End
Go
Create Or Alter Procedure Usp_Inc_Inc08SubTipoInsert  
@IdCategoria  int,  
@Inc08Descripcion varchar(60),   
@Inc08Abreviatura varchar(60),   
@Inc08UsuarioCrea varchar(20),   
@Estado    char(1)  
As  
Begin  
 Insert Into Inc08SubTipo (Inc08Descripcion, Inc08Abreviatura, Inc08UsuarioCrea, Inc08FechaCrea, Estado)  
 Values (@Inc08Descripcion, @Inc08Abreviatura, @Inc08UsuarioCrea, Getdate(), @Estado)  
End
Go
Create Or Alter Procedure Usp_Inc_Inc08SubTipoSelectAll    
@Ordenar varchar(250) = ''    
As    
Begin    
 Set Nocount On    
 Declare @SQL1 varchar(max)    
 Set @SQL1 = ' Select IdCategoria, Inc08Descripcion, Inc08Abreviatura,  Inc08UsuarioCrea, Inc08FechaCrea, Inc08UsuarioModifica, Inc08FechaModifica, '    
 Set @SQL1 = @SQL1 + ' Inc08SubTipo, Estado '    
 Set @SQL1 = @SQL1 + ' From Inc08SubTipo '    
 Set @SQL1 = @SQL1 + ' Where Estado = ''A'' '    
     
 If @Ordenar = ''    
  Set @SQL1 = @SQL1 + ' Order By IdCategoria Asc '    
 Else    
  Set @SQL1 = @SQL1 + ' Order By '''+ @Ordenar +''' '    
      
 Print (@SQL1)      
 Execute (@SQL1)     
End
Go
Create Or Alter Procedure Usp_Inc_Inc08SubTipoUpdate  
@IdCategoria   int,  
@Inc08Descripcion  varchar(60),   
@Inc08Abreviatura  varchar(60),   
@Inc08UsuarioModifica varchar(20),   
@Estado     char(1)  
As  
Begin  
 Update Inc08SubTipo Set   
  Inc08Descripcion = @Inc08Descripcion,   
  Inc08Abreviatura = @Inc08Abreviatura,   
  Inc08UsuarioModifica = @Inc08UsuarioModifica,   
  Inc08FechaModifica = Getdate(),  
  Estado = @Estado  
 Where IdCategoria = @IdCategoria   
End
Go
-- Usp_Inc_Inc10ItemCategoria_Insert 1, '111', 8, 4, 2, 5, 'A'  
-- select *from Inc04Item where estado = 'A'  
-- select idcategoria, * from Inc08SubTipo where estado = 'A'  
Create Or Alter Procedure Usp_Inc_Inc10ItemCategoria_Insert  
@Inc04Item   varchar(20),   
@IdCategoria  int,   
@incPrioridad  numeric(18,2),   
@IncImpacto   numeric(18,2),   
@IncComplejidad  numeric(18,2),   
@Estado    varchar(1)  
As  
Begin  
 Insert Into Inc10ItemCategoria (Inc04Item, IdCategoria, incPrioridad, IncImpacto, IncComplejidad, Estado)  
 Values (@Inc04Item, @IdCategoria, @incPrioridad, @IncImpacto, @IncComplejidad, @Estado)  
End
Go
Create Or Alter Procedure Usp_Inc_Inc10ItemCategoria_Update  
@Inc04Item   varchar(20),   
@IdCategoria  int,   
@incPrioridad  numeric(18,2),   
@IncImpacto   numeric(18,2),   
@IncComplejidad  numeric(18,2),   
@Estado    varchar(1)  
As  
Begin  
 Update Inc10ItemCategoria Set   
  incPrioridad = @incPrioridad,   
  IncImpacto = @IncImpacto,   
  IncComplejidad = @IncComplejidad,   
  Estado = @Estado  
 Where Inc04Item = @Inc04Item   
 and IdCategoria = @IdCategoria  
End
Go
Create Or Alter Procedure Usp_Inc_Inc10ItemCategoriaSelect_Por_Item    
@Inc04Item  varchar(20)    
As    
Begin    
 Select a.IdCategoria, e.Inc08Descripcion    
 From Inc10ItemCategoria as a with(nolock)     
 Inner Join Inc04Item as b with(nolock) On a.Inc04Item = b.Inc04Item     
 Inner Join Inc01Linea as c with(nolock) On b.Inc01Linea = c.Inc01Linea     
 Inner Join Inc02Area as d with(nolock) On c.Inc02Area = d.Inc02Area     
 Inner Join Inc08SubTipo as e with(nolock) On  a.IdCategoria = e.IdCategoria     
 Where a.Inc04Item = @Inc04Item 
 order by  e.Inc08Descripcion
End
Go
Create Or Alter Procedure Usp_Inc_Inc10ItemCategoriaSelect_PorCategoriaItem  
@Inc04Item  varchar(20),  
@IdCategoria Int  
As  
Begin  
 Select c.Inc02Area, d.Inc02Descripcion, b.Inc01Linea, c.Inc01Descripcion, a.Inc04Item, b.Inc04Descripcion,   
   a.IdCategoria, e.Inc08Descripcion, IncPrioridad=cast(isnull(a.IncPrioridad,0) as int), IncImpacto=cast(isnull(a.IncImpacto,0) as int), IncComplejidad=cast(isnull(a.IncComplejidad,0) as int), a.Estado   
 From Inc10ItemCategoria as a with(nolock)   
 Inner Join Inc04Item as b with(nolock) On a.Inc04Item = b.Inc04Item   
 Inner Join Inc01Linea as c with(nolock) On b.Inc01Linea = c.Inc01Linea   
 Inner Join Inc02Area as d with(nolock) On c.Inc02Area = d.Inc02Area   
 Inner Join Inc08SubTipo as e with(nolock) On  a.IdCategoria = e.IdCategoria   
 Where a.Inc04Item = @Inc04Item  
 and a.IdCategoria = @IdCategoria  
End
Go
Create Or Alter Procedure Usp_Inc_Inc10ItemCategoriaSelect_PorCategoriaItem_V1  
@ItemCategoria  varchar(30)  
As  
Begin  
 Select c.Inc02Area, d.Inc02Descripcion, b.Inc01Linea, c.Inc01Descripcion, a.Inc04Item, b.Inc04Descripcion,   
   a.IdCategoria, e.Inc08Descripcion, IncPrioridad=cast(isnull(a.IncPrioridad,0) as int), IncImpacto=cast(isnull(a.IncImpacto,0) as int), IncComplejidad=cast(isnull(a.IncComplejidad,0) as int), a.Estado   
 From Inc10ItemCategoria as a with(nolock)   
 Inner Join Inc04Item as b with(nolock) On a.Inc04Item = b.Inc04Item   
 Inner Join Inc01Linea as c with(nolock) On b.Inc01Linea = c.Inc01Linea   
 Inner Join Inc02Area as d with(nolock) On c.Inc02Area = d.Inc02Area   
 Inner Join Inc08SubTipo as e with(nolock) On  a.IdCategoria = e.IdCategoria   
 Where ltrim(rtrim(a.Inc04Item)) + ltrim(rtrim(cast(a.IdCategoria as varchar))) = @ItemCategoria  
End
Go
Create Or Alter Procedure Usp_Inc_Inc10ItemCategoriaSelectAll  
@Ordenar varchar(250) = ''  
As  
Begin  
 Set Nocount On  
 Declare @SQL1 varchar(max)  
 Set @SQL1 = ' Select c.Inc02Area, d.Inc02Descripcion, '  
 Set @SQL1 = @SQL1 + ' b.Inc01Linea, c.Inc01Descripcion, '  
 Set @SQL1 = @SQL1 + ' a.Inc04Item, b.Inc04Descripcion, '  
 Set @SQL1 = @SQL1 + ' a.IdCategoria, e.Inc08Descripcion, '  
 Set @SQL1 = @SQL1 + ' IncPrioridad=cast(isnull(a.IncPrioridad,0) as int), IncImpacto=cast(isnull(a.IncImpacto,0) as int), IncComplejidad=cast(isnull(a.IncComplejidad,0) as int), a.Estado, '  
 Set @SQL1 = @SQL1 + ' IdCategoriaItem = ( ltrim(rtrim(a.Inc04Item)) + ltrim(rtrim(cast(a.IdCategoria as varchar))) ) '  
 Set @SQL1 = @SQL1 + ' From Inc10ItemCategoria as a with(nolock) '  
 Set @SQL1 = @SQL1 + ' Inner Join Inc04Item as b with(nolock) On a.Inc04Item = b.Inc04Item '  
 Set @SQL1 = @SQL1 + ' Inner Join Inc01Linea as c with(nolock) On b.Inc01Linea = c.Inc01Linea '  
 Set @SQL1 = @SQL1 + ' Inner Join Inc02Area as d with(nolock) On c.Inc02Area = d.Inc02Area '  
 Set @SQL1 = @SQL1 + ' Inner Join Inc08SubTipo as e with(nolock) On  a.IdCategoria = e.IdCategoria '  
  
 If @Ordenar = ''  
  Set @SQL1 = @SQL1 + ' Order By a.Inc04Item, a.IdCategoria '  
 Else  
  Set @SQL1 = @SQL1 + ' Order By '''+ @Ordenar +''' '  
    
 Print (@SQL1)    
 Execute (@SQL1)   
End
Go
Create Or Alter Procedure Usp_Inc_Inc20IncidenciaArchivoListar
 (
	 @Inc20Incidencia VARCHAR(12),
	 @FlagSolicitud int
 )
 AS
 BEGIN
 SELECT Inc20Incidencia,
        NombreEncryptado,
        NombreReal,
        FlagSolicitud,
        Estado,
        FechaRegistro,
        UsuarioRegistro FROM dbo.Inc20IncidenciaArchivos
		WHERE Inc20Incidencia = @Inc20Incidencia AND FlagSolicitud = @FlagSolicitud
 END;
Go
-- Usp_Inc_Inc20IncidenciaAvanceByIncidencia 'ANALISTA','MRAMON','','AT'
Create Or Alter Procedure [dbo].[Usp_Inc_Inc20IncidenciaAvanceByIncidencia]
	@Opcion varchar(20),
	@ValorBusqueda varchar(25)='',
	@Ordenar varchar(50)='',
	@Estado char(2)=''
AS
BEGIN
	SET NOCOUNT ON
	
	declare @CamposSql nvarchar(4000) 
	declare @TablasSql nvarchar(4000) 
	declare @WhereSql nvarchar(4000)
	declare @OrderSql nvarchar(255)
	declare @SelectSql nvarchar(max) 
	
	set @CamposSql = 'SELECT inc.Inc20Incidencia,inc.Inc20Fecha,inc.Inc02AreaTI,
		ati.Inc02Descripcion as Inc02DescripAreaTI,inc.Inc03Usuario,inc.Inc02Area,
		are.Inc02Descripcion as Inc02DescripArea,inc.Inc01Linea,inc.Inc04Item,
		inc.Inc20Detalle,inc.Inc20Estado,est.Inc06Descripcion,inc.Inc20FechaAsignado,
		Inc21FechaAvance=(select MAX(Inc21Fecha) from Inc21avance where Inc21avance.Inc20Incidencia = inc.Inc20Incidencia),
		isnull(inc.Inc03UsuarioTI,'''') as Inc03UsuarioTI, isnull(inc.Inc05Tipo,'''') as Inc05Tipo,
		inc05Descripcion,
		inc20FlagAutorizacion, 
		inc21Porcentaje = isnull((select sum(Inc21Porcentaje) from inc21avance
						where inc20Incidencia = inc.Inc20Incidencia),0),
		inc08SubTipo,
		inc02AreaCausante '
	set @TablasSql = 'FROM Inc20Incidencia as inc
		INNER JOIN Inc02Area as ati
			on ati.inc02Area = inc.Inc02AreaTI
		INNER JOIN Inc02Area as are
			on are.inc02Area = inc.Inc02Area
		INNER JOIN Inc06Estado est
			on est.inc06estado = inc.Inc20Estado
		left join Inc05Tipo tp
			on tp.Inc05Tipo = Inc.Inc05Tipo '
	if @Opcion  = 'USUARIO'
		set @WhereSql = 'WHERE inc.inc03Usuario = ''' +  @ValorBusqueda + ''' '	;
	
	if @Opcion  = 'AREA'
		set @WhereSql = 'WHERE inc.inc02Area = ''' +  @ValorBusqueda + ''' '	;
	
	print @Opcion
	if @Opcion = 'ANALISTA'
		begin
			set @WhereSql = 'WHERE inc.inc03UsuarioTI = ''' +  @ValorBusqueda + ''' '	;
		end
	if @Opcion  = 'INCIDENCIA'
		set @WhereSql = 'WHERE inc.inc20Incidencia = ''' +  @ValorBusqueda + ''' '	;
	if @Estado <> ''
		begin
			set @WhereSql = @WhereSql + ' and inc.inc20estado = ''' +  @Estado + ''' '	;
		end
	IF RTRIM(@Ordenar)=''
	BEGIN
		SET @OrderSql = ' ORDER BY inc.Inc20Incidencia DESC'
	END
	ELSE
	BEGIN
		SET @OrderSql = ' ORDER BY ' + @Ordenar
	END
	set @SelectSql = @CamposSql + @TablasSql + @WhereSql + @OrderSql--+ ' and inc.inc20estado not in(''AT'',''CF'',''PE'')'
	print @SelectSql 
	exec sp_executesql @SelectSql 
END
Go
-- Usp_Inc_Inc20IncidenciaAvanceUserByIncidencia 'ANALISTA','MRAMON','','AT'    
Create Or Alter Procedure [dbo].[Usp_Inc_Inc20IncidenciaAvanceUserByIncidencia]    
 @Opcion varchar(20),    
 @ValorBusqueda varchar(25)='',    
 @Ordenar varchar(50)='',    
 @Estado char(2)='' ,    
 @User varchar (25)= ''  
AS    
BEGIN    
 SET NOCOUNT ON    
     
 declare @CamposSql nvarchar(4000)     
 declare @TablasSql nvarchar(4000)     
 declare @WhereSql nvarchar(4000)    
 declare @OrderSql nvarchar(255)    
 declare @SelectSql nvarchar(max)     
     
 set @CamposSql = 'SELECT inc.Inc20Incidencia,inc.Inc20Fecha,inc.Inc02AreaTI,    
  ati.Inc02Descripcion as Inc02DescripAreaTI,inc.Inc03Usuario,inc.Inc02Area,    
  are.Inc02Descripcion as Inc02DescripArea,inc.Inc01Linea,inc.Inc04Item,    
  inc.Inc20Detalle,inc.Inc20Estado,est.Inc06Descripcion,inc.Inc20FechaAsignado,    
  Inc21FechaAvance=(select MAX(Inc21Fecha) from Inc21avance where Inc21avance.Inc20Incidencia = inc.Inc20Incidencia),    
  isnull(inc.Inc03UsuarioTI,'''') as Inc03UsuarioTI, isnull(inc.Inc05Tipo,'''') as Inc05Tipo,    
  inc05Descripcion,    
  inc20FlagAutorizacion,     
  inc21Porcentaje = isnull((select sum(Inc21Porcentaje) from inc21avance    
      where inc20Incidencia = inc.Inc20Incidencia),0),    
  inc08SubTipo,    
  inc02AreaCausante '    
    
 set @TablasSql = 'FROM Inc20Incidencia as inc    
  INNER JOIN Inc02Area as ati    
   on ati.inc02Area = inc.Inc02AreaTI    
  INNER JOIN Inc02Area as are    
   on are.inc02Area = inc.Inc02Area    
  INNER JOIN Inc06Estado est    
   on est.inc06estado = inc.Inc20Estado    
  left join Inc05Tipo tp    
   on tp.Inc05Tipo = Inc.Inc05Tipo '    
    
 if @Opcion  = 'USUARIO'    
  set @WhereSql = 'WHERE inc.inc03Usuario = ''' +  @ValorBusqueda + ''' ' ;    
     
 if @Opcion  = 'AREA'    
  set @WhereSql = 'WHERE inc.inc02Area = ''' +  @ValorBusqueda + ''' and inc.inc03Usuario = ''' +  @User + ''' ' ;    
     
 print @Opcion    
 if @Opcion = 'ANALISTA'    
  begin    
   set @WhereSql = 'WHERE inc.inc03UsuarioTI = ''' +  @ValorBusqueda + ''' and inc.inc03Usuario = ''' +  @User + ''' ' ;    
  end    
    
 if @Opcion  = 'INCIDENCIA'    
  set @WhereSql = 'WHERE inc.inc20Incidencia = ''' +  @ValorBusqueda + ''' and inc.inc03Usuario = ''' +  @User + ''' ' ;    
    
 if @Estado <> ''    
  begin    
   set @WhereSql = @WhereSql + ' and inc.inc20estado = ''' +  @Estado + ''' and inc.inc03Usuario = ''' +  @User + ''' ' ;    
  end    
    
 IF RTRIM(@Ordenar)=''    
 BEGIN    
  SET @OrderSql = ' ORDER BY inc.Inc20Incidencia DESC'    
 END    
 ELSE    
 BEGIN    
  SET @OrderSql = ' ORDER BY ' + @Ordenar    
 END    
    
 set @SelectSql = @CamposSql + @TablasSql + @WhereSql + @OrderSql--+ ' and inc.inc20estado not in(''AT'',''CF'',''PE'')'    
 print @SelectSql     
 exec sp_executesql @SelectSql     
END
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc20IncidenciaColaAtencion] 
@Usuario VARCHAR(20), 
@AreaTI VARCHAR(10) = ''
AS
BEGIN
	IF EXISTS(	SELECT *
		  FROM (   SELECT *
					 FROM dbo.Inc03Usuario
					WHERE Inc02Area IN ( '002', '003' )
					  AND Inc03Estado = 'A'
				   UNION ALL
				   SELECT *
					 FROM dbo.Inc03Usuario
					WHERE Inc03Usuario IN ( 'JMALLMA', 'KVEGA', 'LCASTILLO' )
					  AND Inc03Estado = 'A') R WHERE r.Inc03Usuario = @Usuario)
			BEGIN
			SET @Usuario = ''
			END	
    SELECT      inc.Inc20Incidencia,
                CONVERT(VARCHAR(10), inc.Inc20Fecha, 103) AS Inc20Fecha,
                inc.Inc02AreaTI,
                ati.Inc02Descripcion AS Inc02DescripAreaTI,
                inc.Inc03Usuario,
                inc.Inc02Area,
                are.Inc02Descripcion AS Inc02DescripArea,
                inc.Inc01Linea,
                inc.Inc04Item,
                inc.Inc20Detalle,
                inc.Inc20Estado,
                est.Inc06Descripcion,
                CONVERT(VARCHAR(10), inc.Inc20FechaAsignado, 103) AS Inc20FechaAsignado,
                CONVERT(VARCHAR(10), inc.Inc20FechaAtencion, 103) AS Inc20FechaAtencion,
                Inc21FechaAvance = (   SELECT MAX(CONVERT(VARCHAR(10), Inc21Fecha, 103))
                                         FROM Inc21Avance
                                        WHERE Inc21Avance.Inc20Incidencia = inc.Inc20Incidencia),
                ISNULL(inc.Inc03UsuarioTI, '''') AS Inc03UsuarioTI,
                ISNULL(inc.Inc05Tipo, '''') AS Inc05Tipo,
                Inc05Descripcion,
                Inc20FlagAutorizacion,
                inc21Porcentaje = ISNULL((   SELECT SUM(Inc21Porcentaje)
                                               FROM Inc21Avance
                                              WHERE Inc20Incidencia = inc.Inc20Incidencia),
                                         0),
                Inc08SubTipo,
                Inc02AreaCausante,
                CASE
                     WHEN Inc20Estado = 'PC' THEN 3
                     WHEN Inc20Estado = 'AS' THEN 3
                     WHEN Inc20Estado = 'PE' THEN 4
                     WHEN Inc20Estado = 'OB' THEN 5
                     ELSE 0 END AS ORDEN,
                Prd.descripcion AS Prioridad,
                Cmp.Descripcion AS Complejidad,
                Imp.Descripcion AS Impacto,
                --Sla.Dias + ' Día(s)' As TiempoAprox,  
                (   SELECT Dias
                      FROM [IncSLA]
                     WHERE RangoInicial <= (inc.IncComplejidad * (   SELECT Valor = (Porcentaje / 100)
                                                                       FROM IncSLAPorcentaje
                                                                      WHERE Descripcion = 'Complejidad'))
                                           + (inc.IncImpacto * (   SELECT Valor = (Porcentaje / 100)
                                                                     FROM IncSLAPorcentaje
                                                                    WHERE Descripcion = 'Impacto'))
                                           + (inc.IncPrioridad * (   SELECT Valor = (Porcentaje / 100)
                                                                       FROM IncSLAPorcentaje
                                                                      WHERE Descripcion = 'Prioridad'))
                       AND RangoFinal   >= (inc.IncComplejidad * (   SELECT Valor = (Porcentaje / 100)
                                                                       FROM IncSLAPorcentaje
                                                                      WHERE Descripcion = 'Complejidad'))
                                           + (inc.IncImpacto * (   SELECT Valor = (Porcentaje / 100)
                                                                     FROM IncSLAPorcentaje
       WHERE Descripcion = 'Impacto'))
                                           + (inc.IncPrioridad * (   SELECT Valor = (Porcentaje / 100)
                                                                       FROM IncSLAPorcentaje
                                                                      WHERE Descripcion = 'Prioridad'))) + ' Día(s)' AS TiempoAprox
      FROM      Inc20Incidencia AS inc
     INNER JOIN Inc02Area AS ati
        ON ati.Inc02Area      = inc.Inc02AreaTI
     INNER JOIN Inc02Area AS are
        ON are.Inc02Area      = inc.Inc02Area
     INNER JOIN Inc06Estado est
        ON est.Inc06Estado    = inc.Inc20Estado
      LEFT JOIN Inc05Tipo tp
        ON tp.Inc05Tipo       = inc.Inc05Tipo
      LEFT JOIN IncPrioridad Prd
        ON Prd.IncPrioridad   = inc.IncPrioridad
      LEFT JOIN IncComplejidad Cmp
        ON Cmp.IncComplejidad = inc.IncComplejidad
      LEFT JOIN IncImpacto Imp
        ON Imp.IncImpacto     = inc.IncImpacto
     --left join IncSLA Sla    
     --on Sla.IncSLA = Inc.IncSLA    
     WHERE      inc.Inc20Estado IN ( '', 'PC', 'AS', 'PE', 'OB' )
       AND      inc.Inc02AreaTI = @AreaTI
	   AND		(@Usuario = '' OR	inc.Inc03Usuario = @Usuario)
     ORDER BY 
			  inc.Inc05Tipo ASC,
              inc.Inc20Fecha DESC,
              inc.IncPrioridad DESC
--,case WHEN Inc20Estado = 'AS' THEN inc.Inc20Fecha DESC      
--   WHEN Inc20Estado = 'PE' THEN inc.Inc20Fecha ASC     
--   WHEN Inc20Estado = 'OB' THEN inc.Inc20Fecha ASC    
--ELSE 0 END  
END;
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc20IncidenciaGetLast]
AS
SET NOCOUNT ON
SELECT isnull(max(Right([Inc20Incidencia],8)),0) + 1
FROM [Inc20Incidencia]
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc20IncidenciaInsert_V2]      
(      
 @Inc20Incidencia varchar(20),      
 @Inc20Fecha datetime,      
 @Inc02AreaTI char(3),      
 @Inc03Usuario varchar(20),      
 @Inc02Area char(3),      
 @Inc01Linea char(3),      
 @Inc04Item varchar(20),      
 @Inc20Detalle nvarchar(max),      
 @Inc20Titulo nvarchar(max),      
 @Inc20Estado char(2),      
 @Inc20UsuarioCrea varchar(20),      
 @Inc20UsuarioModifica varchar(20),      
 @Inc20FechaModifica datetime,    
 @Inc20FlagLectura Bit      ,
 @Inc20Tipo CHAR(3)
 
)      
AS      
BEGIN      
SET NOCOUNT ON      
INSERT INTO [Inc20Incidencia]      
(      
 [Inc20Incidencia],      
 [Inc20Fecha],      
 [Inc02AreaTI],      
 [Inc03Usuario],      
 [Inc02Area],      
 [Inc01Linea],      
 [Inc04Item],      
 [Inc20Detalle],      
 [Inc20Titulo],   
 [Inc20Estado],      
 [Inc20UsuarioCrea],      
 [Inc20UsuarioModifica],      
 [Inc20FechaModifica],    
 [Inc20FlagLectura]    ,
 Inc05Tipo  
)      
VALUES      
(      
 @Inc20Incidencia,      
 GETDATE(),      
 @Inc02AreaTI,      
 @Inc03Usuario,      
 @Inc02Area,      
 @Inc01Linea,      
 @Inc04Item,      
 @Inc20Detalle,      
 @Inc20Titulo,      
 @Inc20Estado,      
 @Inc20UsuarioCrea,      
 @Inc20UsuarioModifica,      
 GETDATE(),    
 @Inc20FlagLectura     ,
 @Inc20Tipo  
)      
END
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc20IncidenciaSelect] @Inc20Incidencia VARCHAR(12)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT      Inc20Incidencia,
                Inc20Fecha,
                Inc02AreaTI,
                inc.Inc03Usuario,
                usr.Inc03Descripcion,
                inc.Inc02Area,
                Inc02AreaDes = area.Inc02Descripcion,
                Inc01Linea,
                Inc04Item,
                Inc20Detalle,
                ISNULL(Inc20Titulo, '') AS Inc20Titulo,
                Inc20Estado,
                Inc05Tipo = RTRIM(ISNULL(Inc05Tipo, '')),
                Inc08SubTipo = RTRIM(ISNULL(Inc08SubTipo, '')),
                Inc03UsuarioTI = RTRIM(ISNULL([Inc03UsuarioTI], '')),
                Inc02AreaCausante = ISNULL(Inc02AreaCausante, ''),
                Inc20FlagAutorizacion = ISNULL(Inc20FlagAutorizacion, 'N'),
                Inc21Porcentaje = ISNULL((   SELECT SUM(av.Inc21Porcentaje)
                                               FROM Inc21Avance av
                                              WHERE av.Inc20Incidencia = @Inc20Incidencia),
                                         0.0),
                Inc03Correo,
                Inc03Anexo,
                Inc02Telefono,
                Inc20FlagLectura,
                IdCategoria = ISNULL(IdCategoria, 0),
                IncPrioridad = CAST(ISNULL(IncPrioridad, 0) AS INT),
                IncImpacto = CAST(ISNULL(IncImpacto, 0) AS INT),
				inc.Inc20UsuarioGC,
                IncComplejidad = CAST(ISNULL(IncComplejidad, 0) AS INT)
      FROM      Inc20Incidencia AS inc WITH (NOLOCK)
     INNER JOIN Inc03Usuario AS usr WITH (NOLOCK)
        ON usr.Inc03Usuario = inc.Inc03Usuario
     INNER JOIN Inc02Area AS area WITH (NOLOCK)
        ON area.Inc02Area   = inc.Inc02Area
     WHERE      Inc20Incidencia = @Inc20Incidencia;
END;
Go
Create Or Alter Procedure Usp_Inc_Inc20IncidenciaSelectByEstado_V2  
@Inc20Estado varchar(2),   
@Inc03Analista varchar(10) = "",   
@Ordenar  varchar(50) = "",   
@AreaTI   varchar (10) = ""   
  
As  
  
Begin  
 Set Nocount On  
  
 declare @CamposSql nvarchar(4000)  
 declare @TablasSql nvarchar(4000)  
 declare @WhereSql nvarchar(4000)  
 declare @andSql  nvarchar(4000)  
 declare @OrderSql nvarchar(255)  
 declare @SelectSql nvarchar(max)  
  
 Set @CamposSql = ""  
 Set @CamposSql = @CamposSql + " Select inc.Inc20Incidencia, inc.Inc20Fecha, inc.Inc02AreaTI, ati.Inc02Descripcion as Inc02DescripAreaTI, "  
 Set @CamposSql = @CamposSql + " inc.Inc03Usuario, inc.Inc02Area, are.Inc02Descripcion as Inc02DescripArea, inc.Inc01Linea, inc.Inc04Item, "  
 Set @CamposSql = @CamposSql + " inc.Inc20Detalle, inc.Inc20Estado, est.Inc06Descripcion, inc.Inc20FechaAsignado, "  
 Set @CamposSql = @CamposSql + " Inc21FechaAvance = (Select Max(Inc21Fecha) From Inc21avance Where Inc21avance.Inc20Incidencia = inc.Inc20Incidencia), "  
 Set @CamposSql = @CamposSql + " isnull(inc.Inc03UsuarioTI,'') as Inc03UsuarioTI, isnull(inc.Inc05Tipo,'') as Inc05Tipo, inc05Descripcion, "  
 Set @CamposSql = @CamposSql + " inc08SubTipo, inc20FlagAutorizacion, "  
 Set @CamposSql = @CamposSql + " inc21Porcentaje = isnull((Select Sum(Inc21Porcentaje) From inc21avance Where inc20Incidencia = inc.Inc20Incidencia),0), "  
 Set @CamposSql = @CamposSql + " itm.inc04descripcion, inc.Inc20FlagLectura, "  
 Set @CamposSql = @CamposSql + " inc.IdCategoria, IncPrioridad=cast(isnull(inc.IncPrioridad,0) as int), IncImpacto=cast(isnull(inc.IncImpacto,0) as int), IncComplejidad=cast(isnull(inc.IncComplejidad,0) as int), inc.IncSLA "  
  
 Set @TablasSql = ""  
 Set @TablasSql = @TablasSql + " From Inc20Incidencia as inc  "  
    Set @TablasSql = @TablasSql + " Inner Join Inc02Area as ati On ati.inc02Area = inc.Inc02AreaTI  "    
 Set @TablasSql = @TablasSql + " Inner Join Inc02Area as are On are.inc02Area = inc.Inc02Area "  
 Set @TablasSql = @TablasSql + " Inner Join Inc06Estado as est On est.inc06estado = inc.Inc20Estado "  
 Set @TablasSql = @TablasSql + " Left Join Inc05Tipo as tp On tp.Inc05Tipo = Inc.Inc05Tipo "  
 Set @TablasSql = @TablasSql + " Left Join inc01Linea as lin On lin.inc01linea =inc.inc01linea "  
 Set @TablasSql = @TablasSql + " Left Join inc04item as itm On itm.inc04item = inc.inc04item "  
      
 Set @WhereSql = ""    
 Set @WhereSql = @WhereSql + "Where inc.inc20Estado = '"+ @Inc20Estado +"' "  
  
 If isnull(rtrim(@Inc03Analista),"") <> "" Set @WhereSql = @WhereSql + " and inc.Inc03UsuarioTI = '"+ @Inc03Analista +"' "  
 If isnull(rtrim(@AreaTI),"") <> "" Set @andSql = " and inc.Inc02AreaTI = '"+ @AreaTI +"' "  
  
 If rtrim(@Ordenar) = ""  
 begin  
  Set @OrderSql = " Order By inc.Inc20Incidencia Desc "  
 end  
 else  
 begin  
  Set @OrderSql = " Order By '"+ @Ordenar +"' "  
 end  
  
 If isnull(rtrim(@AreaTI),"") <> ""  
 begin  
  Set @SelectSql = @CamposSql + @TablasSql + @WhereSql + @andSql + @OrderSql  
 end  
 else  
 begin  
  Set @SelectSql = @CamposSql + @TablasSql + @WhereSql + @OrderSql  
 end  
         
 Print @SelectSql  
 Execute sp_executesql @SelectSql  
End
Go
-- Usp_Inc_Inc20IncidenciaSelectByOpcion 'ANALISTA','ccaqui'    
--Sp_helptext	Usp_Inc_Inc20IncidenciaSelectByOpcion
-- Usp_Inc_Inc20IncidenciaSelectByOpcion 'ANALISTA','ccaqui'    
Create Or Alter Procedure [dbo].[Usp_Inc_Inc20IncidenciaSelectByOpcion]    
@Opcion varchar(20),    
@ValorBusqueda varchar(25),    
@Ordenar varchar(50)=''    
AS    
BEGIN    
 SET NOCOUNT ON    
     
 declare @CamposSql nvarchar(4000)     
 declare @TablasSql nvarchar(4000)     
 declare @WhereSql nvarchar(4000)    
 declare @OrderSql nvarchar(255)    
 declare @SelectSql nvarchar(max)     
     
 set @CamposSql = 'SELECT inc.Inc20Incidencia,inc.Inc20Fecha,inc.Inc02AreaTI,    
  ati.Inc02Descripcion as Inc02DescripAreaTI,inc.Inc03Usuario,inc.Inc02Area,    
  are.Inc02Descripcion as Inc02DescripArea,inc.Inc01Linea,inc.Inc04Item,    
  inc.Inc20Detalle,inc.Inc20Estado,est.Inc06Descripcion,inc.Inc20FechaAsignado,    
  Inc21FechaAvance=(select MAX(Inc21Fecha) from Inc21avance where Inc21avance.Inc20Incidencia = inc.Inc20Incidencia),    
  ISNULL(inc.Inc03UsuarioTI,'''') as Inc03UsuarioTI, isnull(inc.Inc05Tipo,'''') as Inc05Tipo,    
  inc05Descripcion,    
  inc20FlagAutorizacion,     
  inc21Porcentaje = ISNULL((select sum(Inc21Porcentaje) from inc21avance    
      where inc20Incidencia = inc.Inc20Incidencia),0),    
  inc08SubTipo,    
  inc02AreaCausante,    
  itm.inc04descripcion,  
  
  prd.Descripcion as Prioridad,  
  cmp.Descripcion as Complejidad,   
  Imp.Descripcion as Impacto,  
     
  (select Dias from IncSLA where RangoInicial <= (inc.IncComplejidad * (select Valor = (Porcentaje/100) from IncSLAPorcentaje where Descripcion = ''Complejidad'')) + (Inc.IncImpacto * (select Valor = (Porcentaje/100) from IncSLAPorcentaje where Descripcion = ''Impacto'')) + (inc.IncPrioridad * (select Valor = (Porcentaje/100) from IncSLAPorcentaje where Descripcion = ''Prioridad''))  and RangoFinal >= (inc.IncComplejidad * (select Valor = (Porcentaje/100) from IncSLAPorcentaje where Descripcion = ''Complejidad'')) + (Inc.IncImpacto * (select Valor = (Porcentaje/100) from IncSLAPorcentaje where Descripcion = ''Impacto'')) + (inc.IncPrioridad * (select Valor = (Porcentaje/100) from IncSLAPorcentaje where Descripcion = ''Prioridad''))) + '' Día(s)'' As TiempoAprox  '    
    
 set @TablasSql = 'FROM Inc20Incidencia as inc    
  INNER JOIN Inc02Area as ati    
   on ati.inc02Area = inc.Inc02AreaTI    
  INNER JOIN Inc02Area as are    
   on are.inc02Area = inc.Inc02Area    
  INNER JOIN Inc06Estado est    
   on est.inc06estado = inc.Inc20Estado    
  left join Inc05Tipo tp    
   on tp.Inc05Tipo = Inc.Inc05Tipo     
  inner join inc01Linea lin    
   on lin.inc01linea =inc.inc01linea    
  inner join inc04item itm    
   on itm.inc04item = inc.inc04item   
  
  left join IncPrioridad Prd    
  on prd.IncPrioridad = Inc.IncPrioridad    
  left join IncComplejidad Cmp    
  on Cmp.IncComplejidad = inc.IncComplejidad    
  left join IncImpacto Imp    
  on Imp.IncImpacto = Inc.IncImpacto  '    
    
 if @Opcion  = 'USUARIO'    
  set @WhereSql = 'WHERE inc.inc03Usuario = ''' +  @ValorBusqueda + ''' ' ;    
     
 if @Opcion  = 'AREA'    
  set @WhereSql = 'WHERE inc.inc02Area = ''' +  @ValorBusqueda + ''' ' ;    
     
 if @Opcion  = 'ANALISTA'    
  set @WhereSql = 'WHERE inc.inc03UsuarioTI = ''' +  @ValorBusqueda + ''' ' ;    
    
 if @Opcion  = 'INCIDENCIA'    
  set @WhereSql = 'WHERE inc.inc20Incidencia = ''' +  @ValorBusqueda + ''' ' ;    
    
 IF RTRIM(@Ordenar)=''    
 BEGIN    
  SET @OrderSql = ' ORDER BY inc.IncPrioridad desc,  Inc20Incidencia '    
 END    
 ELSE    
 BEGIN    
  SET @OrderSql = ' ORDER BY ' + @Ordenar    
 END    
    
 set @SelectSql = @CamposSql + @TablasSql + @WhereSql + ' and inc.inc20estado not in(''AT'',''CF'',''PE'',''NP'')' + @OrderSql    
 print @SelectSql     
    
 exec sp_executesql @SelectSql     
END
Go
-- Usp_Inc_Inc20IncidenciaSelectByOpcionAutoriza 'AREA','022'
Create Or Alter Procedure [dbo].[Usp_Inc_Inc20IncidenciaSelectByOpcionAutoriza]
	@Opcion varchar(20),
	@ValorBusqueda varchar(25)
AS
BEGIN
	SET NOCOUNT ON
	
	declare @CamposSql nvarchar(4000) 
	declare @TablasSql nvarchar(4000) 
	declare @WhereSql nvarchar(4000)
	declare @OrderSql nvarchar(255)
	declare @SelectSql nvarchar(max) 
	
	set @CamposSql = 'SELECT inc.Inc20Incidencia,inc.Inc20Fecha,inc.Inc02AreaTI,
		ati.Inc02Descripcion as Inc02DescripAreaTI,inc.Inc03Usuario,inc.Inc02Area,
		are.Inc02Descripcion as Inc02DescripArea,inc.Inc01Linea,inc.Inc04Item,
		inc.Inc20Detalle,inc.Inc20Estado,est.Inc06Descripcion,inc.Inc20FechaAsignado,
		Inc21FechaAvance=(select MAX(Inc21Fecha) from Inc21avance where Inc21avance.Inc20Incidencia = inc.Inc20Incidencia),
		isnull(inc.Inc03UsuarioTI,'''') as Inc03UsuarioTI, isnull(inc.Inc05Tipo,'''') as Inc05Tipo,
		inc05Descripcion,
		inc21Porcentaje = isnull((select sum(Inc21Porcentaje) from inc21avance
						where inc20Incidencia = inc.Inc20Incidencia),0) '
	set @TablasSql = 'FROM Inc20Incidencia as inc
		INNER JOIN Inc02Area as ati
			on ati.inc02Area = inc.Inc02AreaTI
		INNER JOIN Inc02Area as are
			on are.inc02Area = inc.Inc02Area
		INNER JOIN Inc06Estado est
			on est.inc06estado = inc.Inc20Estado
		left join Inc05Tipo tp
			on tp.Inc05Tipo = Inc.Inc05Tipo '
	if @Opcion  = 'USUARIO'
		set @WhereSql = 'WHERE inc.inc03Usuario = ''' +  @ValorBusqueda + ''' '	;
	
	if @Opcion  = 'AREA'
		set @WhereSql = 'WHERE inc.inc02Area = ''' +  @ValorBusqueda + ''' '	;
	
	if @Opcion  = 'ANALISTA'
		set @WhereSql = 'WHERE inc.inc03UsuarioTI = ''' +  @ValorBusqueda + ''' '	;
	if @Opcion  = 'INCIDENCIA'
		set @WhereSql = 'WHERE inc.inc20Incidencia = ''' +  @ValorBusqueda + ''' '	;
	set @SelectSql = @CamposSql + @TablasSql + @WhereSql + 
		' and inc20FlagAutorizacion = ''S''
		and (select count(Inc21Autoriza) from inc21avance
						where inc20Incidencia = inc.Inc20Incidencia)=0 
		and inc.inc20estado not in(''AT'',''CF'')'
	print @SelectSql
	exec sp_executesql @SelectSql 
END
Go
-- Usp_Inc_Inc20IncidenciaSelectByOpcionConfirma 'AREA','015','015'
Create Or Alter Procedure [dbo].[Usp_Inc_Inc20IncidenciaSelectByOpcionConfirma]
	@Opcion varchar(20),
	@ValorBusqueda varchar(25),
	@Area varchar(3) = ''
AS
BEGIN
	SET NOCOUNT ON
	
	declare @CamposSql nvarchar(4000) 
	declare @TablasSql nvarchar(4000) 
	declare @WhereSql nvarchar(4000)
	declare @OrderSql nvarchar(255)
	declare @SelectSql nvarchar(max) 
	
	set @CamposSql = 'SELECT inc.Inc20Incidencia,inc.Inc20Fecha,inc.Inc02AreaTI,
		ati.Inc02Descripcion as Inc02DescripAreaTI,inc.Inc03Usuario,inc.Inc02Area,
		are.Inc02Descripcion as Inc02DescripArea,inc.Inc01Linea,inc.Inc04Item,
		inc.Inc20Detalle,inc.Inc20Estado,est.Inc06Descripcion,inc.Inc20FechaAsignado,
		Inc21FechaAvance=(select MAX(Inc21Fecha) from Inc21avance where Inc21avance.Inc20Incidencia = inc.Inc20Incidencia),
		isnull(inc.Inc03UsuarioTI,'''') as Inc03UsuarioTI, isnull(inc.Inc05Tipo,'''') as Inc05Tipo,
		inc05Descripcion,
		inc21Porcentaje = isnull((select sum(Inc21Porcentaje) from inc21avance
						where inc20Incidencia = inc.Inc20Incidencia),0) '
	set @TablasSql = 'FROM Inc20Incidencia as inc
		INNER JOIN Inc02Area as ati
			on ati.inc02Area = inc.Inc02AreaTI
		INNER JOIN Inc02Area as are
			on are.inc02Area = inc.Inc02Area
		INNER JOIN Inc06Estado est
			on est.inc06estado = inc.Inc20Estado
		left join Inc05Tipo tp
			on tp.Inc05Tipo = Inc.Inc05Tipo '
	if @Opcion  = 'USUARIO'
		set @WhereSql = 'WHERE inc.inc03Usuario = ''' +  @ValorBusqueda + ''' '	;
	
	if @Opcion  = 'AREA'
		set @WhereSql = 'WHERE inc.inc02Area = ''' +  @ValorBusqueda + ''' '	;
	
	if @Opcion  = 'ANALISTA'
		set @WhereSql = 'WHERE inc.inc03UsuarioTI = ''' +  @ValorBusqueda + ''' '	;
	if @Opcion  = 'INCIDENCIA'
		set @WhereSql = 'WHERE inc.inc20Incidencia = ''' +  @ValorBusqueda + ''' '	;
	set @SelectSql = @CamposSql + @TablasSql + @WhereSql + 
		' and inc.inc20estado = ''AT''' 
	+ 
		 ' and inc.inc02Area = ''' +  @Area + ''' '
	print @SelectSql 
	exec sp_executesql @SelectSql 
END
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc20IncidenciaUpdate_V2]      
 @Inc20Incidencia varchar(20),      
 @Inc02AreaTI char(3),      
 @Inc03Usuario varchar(20),      
 @Inc02Area char(3),      
 @Inc01Linea char(3),      
 @Inc04Item varchar(20),      
 @Inc20Detalle nvarchar(max),      
 @Inc20Titulo nvarchar(max),      
 @Inc20UsuarioModifica varchar(20),      
 @Inc20FechaModifica datetime,    
 @Inc20FlagLectura Bit        
AS      
 SET NOCOUNT ON      
       
 UPDATE  [Inc20Incidencia]      
 SET [Inc02AreaTI] = @Inc02AreaTI,      
  [Inc03Usuario] = @Inc03Usuario,      
  [Inc02Area] =@Inc02Area,      
  [Inc01Linea] = @Inc01Linea,      
  [Inc04Item] = @Inc04Item,      
  [Inc20Detalle] = @Inc20Detalle,      
  [Inc20Titulo] = @Inc20Titulo,      
  [Inc20UsuarioModifica] =@Inc20UsuarioModifica,      
  [Inc20FechaModifica]=@Inc20FechaModifica,    
  [Inc20FlagLectura] = @Inc20FlagLectura     
 WHERE [Inc20Incidencia] = @Inc20Incidencia
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc20IncidenciaUpdateAsignacion]  
@Inc20Incidencia  varchar(20),    
@Inc20Detalle   varchar(max),    
@Inc20Estado   varchar(2),    
@Inc03UsuarioTI   varchar(20),    
@Inc05Tipo    varchar(3),    
@Inc08Subtipo   varchar(3),    
@Inc20FechaAsignado  datetime,    
@Inc03UsuarioAsigno  varchar(20),    
@Inc20FlagAutorizacion char(1),    
@Inc20UsuarioModifica varchar(20),    
@Inc20FechaModifica  datetime,    
@Inc01Linea    varchar(3),    
@Inc04Item    varchar(20),    
@Inc02AreaTI   varchar(3)=null,    
@IncPrioridad   numeric(18,2),  
@IncImpacto    numeric(18,2),  
@IncComplejidad   numeric(18,2),  
@IdCategoria   int  ,
@Inc20UsuarioGC VARCHAR(20) = NULL
  
As  
    
Set Nocount On  
    
Update Inc20Incidencia Set  
  Inc20Detalle = @Inc20Detalle,    
  Inc20Estado = @Inc20Estado,    
  Inc03UsuarioTI = @Inc03UsuarioTI,    
  Inc05Tipo = @Inc05Tipo,    
  Inc08SubTipo = @Inc08SubTipo,    
  Inc20FechaAsignado = @Inc20FechaAsignado,    
  Inc03UsuarioAsigno = @Inc03UsuarioAsigno,    
  Inc20FlagAutorizacion = @Inc20FlagAutorizacion,    
  Inc20UsuarioModifica = @Inc20UsuarioModifica,    
  Inc20FechaModifica = @Inc20FechaModifica,    
  Inc01Linea = @Inc01Linea,    
  Inc04Item = @Inc04Item,  
  IncPrioridad = @IncPrioridad,  
  IncImpacto = @IncImpacto,  
  IncComplejidad = @IncComplejidad,  
  IdCategoria = @IdCategoria  ,
  Inc20UsuarioGC = @Inc20UsuarioGC
Where Inc20Incidencia = @Inc20Incidencia
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc20IncidenciaUpdateAvance] (
@Inc20Incidencia VARCHAR(20),
@Inc20Detalle VARCHAR(MAX),
@Inc05Tipo CHAR(3),
@Inc08Subtipo CHAR(3),
@Inc02AreaCausante CHAR(3),
@Inc20UsuarioModifica VARCHAR(20),
@Inc20FechaModifica DATETIME,
@Inc02areati CHAR(3) = NULL,
@Inc01linea CHAR(3) = NULL,
@Inc04item VARCHAR(20) = NULL,
@Inc20Respuesta VARCHAR(MAX) = NULL
--,@Inc20UsuarioGC VARCHAR(20) = NULL
)
AS
SET NOCOUNT ON;
UPDATE [Inc20Incidencia]
   SET [Inc20Detalle] = @Inc20Detalle,
       [Inc05Tipo] = @Inc05Tipo,
       [Inc08SubTipo] = @Inc08Subtipo,
       [Inc02AreaCausante] = @Inc02AreaCausante,
       [Inc20UsuarioModifica] = @Inc20UsuarioModifica,
       [Inc20FechaModifica] = @Inc20FechaModifica,
       [Inc02AreaTI] = @Inc02areati,
       [Inc01Linea] = @Inc01linea,
       [Inc04Item] = @Inc04item,
       Respuesta = @Inc20Respuesta
	   --,       @Inc20UsuarioGC = @Inc20UsuarioGC
 WHERE [Inc20Incidencia] = @Inc20Incidencia;
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc20IncidenciaUpdateFlagLecturaTue]      
(      
 @Inc20Incidencia varchar(20)      
)      
AS      
BEGIN      
 SET NOCOUNT ON      
 UPDATE [Inc20Incidencia]  
 SET  [Inc20FlagLectura] = 1   
 WHERE [Inc20Incidencia] = @Inc20Incidencia     
END
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc20IncidenciaUpdateReasignacion] (
@Inc20Incidencia VARCHAR(20),
@Inc20Detalle VARCHAR(MAX),
@Inc20Estado CHAR(2),
@Inc03UsuarioTI VARCHAR(20),
@Inc05Tipo CHAR(3),
@Inc08Subtipo CHAR(3),
@Inc20FechaAsignado DATETIME,
@Inc03UsuarioAsigno VARCHAR(20),
@Inc20FlagAutorizacion CHAR(1),
@Inc20UsuarioModifica VARCHAR(20),
@Inc20FechaModifica DATETIME,
@Inc01Linea CHAR(3),
@Inc04Item VARCHAR(20),
@Inc02AreaTI VARCHAR(3) = NULL,
@IncPrioridad VARCHAR(3),
@IncImpacto VARCHAR(3),
@IncComplejidad VARCHAR(3),
@IdCategoria VARCHAR(3),
@Inc20UsuarioGC VARCHAR(20) = NULL)
AS
SET NOCOUNT ON;
UPDATE [Inc20Incidencia]
   SET [Inc20Detalle] = @Inc20Detalle,
       [Inc20Estado] = @Inc20Estado,
       [Inc03UsuarioTI] = @Inc03UsuarioTI,
       [Inc05Tipo] = @Inc05Tipo,
       [Inc08SubTipo] = @Inc08Subtipo,
       --[Inc20FechaAsignado] = @Inc20FechaAsignado,      
       [Inc03UsuarioAsigno] = @Inc03UsuarioAsigno,
       [Inc20FlagAutorizacion] = @Inc20FlagAutorizacion,
       [Inc20UsuarioModifica] = @Inc20UsuarioModifica,
       [Inc20FechaModifica] = @Inc20FechaModifica,
       [Inc01Linea] = @Inc01Linea,
       [Inc04Item] = @Inc04Item,
       [Inc02AreaTI] = @Inc02AreaTI,
       [IncPrioridad] = @IncPrioridad,
       [IncImpacto] = @IncImpacto,
       [IncComplejidad] = @IncComplejidad,
       [IdCategoria] = @IdCategoria,
	   Inc20UsuarioGC = @Inc20UsuarioGC
 WHERE [Inc20Incidencia] = @Inc20Incidencia;
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc21AvanceGetLast]
AS
SET NOCOUNT ON
SELECT isnull(max(Right([Inc21Avance],8)),0) + 1
FROM [Inc21Avance]
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc21AvanceInsert]
(
	@Inc21Avance varchar(20),
	@Inc20Incidencia varchar(20),
	@Inc21Detalle nvarchar(max),
	@Inc21Fecha datetime,
	@Inc03UsuarioTI varchar(20),
	@Inc21Porcentaje numeric(18, 2),
	@Inc21TiempoUtilizado decimal(8,2)
)
AS
BEGIN
SET NOCOUNT ON
INSERT INTO [Inc21Avance]
(
	[Inc21Avance],
	[Inc20Incidencia],
	[Inc21Detalle],
	[Inc21Fecha],
	[Inc03UsuarioTI],
	[Inc21Porcentaje],
	[Inc21TiempoUtilizado]
)
VALUES
(
	@Inc21Avance,
	@Inc20Incidencia,
	@Inc21Detalle,
	GETDATE(),
	@Inc03UsuarioTI,
	@Inc21Porcentaje,
	@Inc21TiempoUtilizado
)
END
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc21AvanceInsertAutoriza]
(
	@Inc21Avance varchar(20),
	@Inc20Incidencia varchar(20),
	@Inc21Detalle nvarchar(max),
	@Inc21Fecha datetime,
	@Inc03UsuarioTI varchar(20),
	@Inc21Porcentaje numeric(18,2),
	@Inc21Autoriza char(1)
)
AS
BEGIN
SET NOCOUNT ON
INSERT INTO [Inc21Avance]
(
	[Inc21Avance],
	[Inc20Incidencia],
	[Inc21Detalle],
	[Inc21Fecha],
	[Inc03UsuarioTI],
	[Inc21Porcentaje],
	[Inc21Autoriza]
)
VALUES
(
	@Inc21Avance,
	@Inc20Incidencia,
	@Inc21Detalle,
	GETDATE(),
	@Inc03UsuarioTI,
	@Inc21Porcentaje,
	@Inc21Autoriza
)
END
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc21AvanceInsertConfirma]
(
	@Inc21Avance varchar(20),
	@Inc20Incidencia varchar(20),
	@Inc21Detalle nvarchar(max),
	@Inc21Fecha datetime,
	@Inc03UsuarioTI varchar(20),
	@Inc21Porcentaje numeric(18,2),
	@Inc21Confirma char(1)
)
AS
BEGIN
SET NOCOUNT ON
INSERT INTO [Inc21Avance]
(
	[Inc21Avance],
	[Inc20Incidencia],
	[Inc21Detalle],
	[Inc21Fecha],
	[Inc03UsuarioTI],
	[Inc21Porcentaje],
	[Inc21Confirma]
)
VALUES
(
	@Inc21Avance,
	@Inc20Incidencia,
	@Inc21Detalle,
	GETDATE(),
	@Inc03UsuarioTI,
	@Inc21Porcentaje,
	@Inc21Confirma
)
END
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Inc21AvanceSelectByIncidencia]
(
	@Inc20Incidencia varchar(20)
)
AS
SET NOCOUNT ON
SELECT [Inc21Avance],
	[Inc20Incidencia],
	[Inc21Detalle],
	[Inc21Fecha],
	[Inc03UsuarioTI],
	[Inc21Porcentaje],
	[Inc21Autoriza],
	[Inc21Confirma]
FROM [Inc21Avance]
WHERE [Inc20Incidencia] = @Inc20Incidencia
Go
-- Usp_Inc_Inc21AvanceUpdate_EstadoIncidencia 'INC-00000012'
Create Or Alter Procedure [dbo].[Usp_Inc_Inc21AvanceUpdate_EstadoIncidencia]
(
	@Inc20Incidencia varchar(20)
)
AS
	Declare @PorcentajeAvance numeric(18,2)
	set @PorcentajeAvance = isnull((Select sum(Inc21Porcentaje) 
							from Inc21Avance
							where Inc20Incidencia = @Inc20Incidencia),0)
	print @PorcentajeAvance
	
	update inc20Incidencia
	set Inc20Estado = case when (@PorcentajeAvance>97) then
							'CF'
						when (@PorcentajeAvance=97) then
							'AT'
					  when (@PorcentajeAvance>0) and (@PorcentajeAvance<97) then
							'PC'
					  else
							'AS'
						end
	where inc20incidencia = @Inc20Incidencia
Go
Create Or Alter Procedure [dbo].[Usp_Inc_IncFormato]  
AS  
SELECT * from IncFormato   
WHERE Estado='A'
Go
Create Or Alter Procedure [dbo].[Usp_Inc_IncVideoTutorial]  
(  
@IncVideoTutorial int,  
@Descripcion varchar(150)  
)  
AS  
BEGIN  
 IF @IncVideoTutorial = 0  
  BEGIN  
   IF @Descripcion = ''  
    BEGIN  
     SELECT * from IncVideoTutorial  
     WHERE Estado='A'       
    END  
   ELSE  
    BEGIN  
     SELECT * from IncVideoTutorial  
     WHERE Estado='A'  
     AND Descripcion like '%' + @Descripcion + '%'   
    END     
  END  
 ELSE  
  BEGIN  
   SELECT * from IncVideoTutorial  
   WHERE Estado='A'  
   AND IncVideoTutorial=@IncVideoTutorial  
  END  
END
Go
Create Or Alter Procedure Usp_Inc_Lista_Complejidad  
  
As  
  
Begin  
 Set Nocount On  
  
 Select IncComplejidad, Descripcion  
 From IncComplejidad  
 Where Estado = 'A'  
 Order By IncComplejidad  
End
Go
Create Or Alter Procedure Usp_Inc_Lista_Impacto  
  
As  
  
Begin  
 Set Nocount On  
  
 Select IncImpacto, Descripcion  
 From IncImpacto  
 Where Estado ='A'  
 Order By IncImpacto  
End
Go
Create Or Alter Procedure Usp_Inc_Lista_Prioridad  
  
As  
  
Begin  
 Set Nocount On  
  
 Select IncPrioridad, Descripcion  
 From IncPrioridad  
 Where Estado = 'A'  
 Order By IncPrioridad  
End
Go
Create Or Alter Procedure [dbo].[Usp_Inc_Sis01MenuSelect]
(
	@Sis01Sistema varchar(12)
)
AS
SET NOCOUNT ON
SELECT [Sis01Sistema],
	[Sis01MenuId],
	[Sis01Descripcion],
	[Sis01PadreId],
	[Sis01Posicion],
	[Sis01Icono],
	[Sis01Habilitado],
	[Sis01Url],
	[Sis01FechaCrea],
	[Sis01UsuarioCrea],
	[Sis01FechaModif],
	[Sis01UsuarioModif]
FROM [Sis01Menu]
WHERE [Sis01Sistema] = @Sis01Sistema
	and [Sis01Habilitado] = 'S'
Order by [Sis01Posicion]
Go
