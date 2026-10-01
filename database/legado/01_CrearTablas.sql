Set NoCount On
Set Xact_Abort On
Go

USE [GestionSistemas];
GO
CREATE TABLE [dbo].[Calificacion] (
    [Inc20Incidencia] [varchar](12) NOT NULL,
    [TipoPregunta] [varchar](20) NULL,
    [Puntos] [int] NULL,
    [Comentarios] [varchar](500) NULL,
    [SeSoluciono] [varchar](10) NULL,
    [UsuarioRegistro] [varchar](20) NULL,
    [FechaRegistro] [datetime] NULL
);
GO

USE [GestionSistemas];
GO
CREATE TABLE [dbo].[Inc00Correlativo] (
    [Codigo] [varchar](50) NULL,
    [Serie] [varchar](50) NULL,
    [Correlativo] [int] NULL,
    [UsuarioModifica] [varchar](20) NULL,
    [FechaModifica] [datetime] NULL
);
GO

USE [GestionSistemas];
GO
CREATE TABLE [dbo].[Inc01Linea] (
    [Inc01Linea] [char](3) NOT NULL,
    [Inc02Area] [char](3) NULL,
    [Inc01Descripcion] [varchar](60) NULL,
    [Inc01UsuarioCrea] [varchar](20) NULL,
    [Inc01FechaCrea] [datetime] NULL,
    [Inc01UsuarioModifica] [varchar](20) NULL,
    [Inc01FechaModifica] [datetime] NULL,
    [Estado] [varchar](2) NULL CONSTRAINT [DF_Inc01Linea_Estado] DEFAULT ('A'),
    [Inc01Linea_Antigua] [char](3) NULL,
    [Inc01Linea_Antigua2] [char](3) NULL,
    [Inc01IDSistemaInHouse] [int] NULL CONSTRAINT [DF__Inc01Line__Inc01__55DFB4D9] DEFAULT (NULL),
    CONSTRAINT [PK_Linea] PRIMARY KEY ([Inc01Linea] ASC)
);
GO

USE [GestionSistemas];
GO
CREATE TABLE [dbo].[Inc02Area] (
    [Inc02Area] [char](3) NOT NULL,
    [Inc02Descripcion] [varchar](60) NULL,
    [Inc02Telefono] [varchar](20) NULL,
    [Inc02UsuarioCrea] [varchar](20) NULL,
    [Inc02FechaCrea] [datetime] NULL,
    [Inc02UsuarioModifica] [varchar](20) NULL,
    [Inc02FechaModifica] [datetime] NULL,
    [Inc20Jefe2] [varchar](20) NULL,
    [Inc20Jefe] [varchar](20) NULL,
    [Estado] [varchar](2) NULL CONSTRAINT [DF_Inc02Area_Estado] DEFAULT ('A'),
    CONSTRAINT [PK_Area] PRIMARY KEY ([Inc02Area] ASC)
);
GO

USE [GestionSistemas];
GO
CREATE TABLE [dbo].[Inc03Usuario] (
    [Inc03Usuario] [varchar](20) NOT NULL,
    [Inc03Descripcion] [varchar](255) NULL,
    [Inc03Clave] [char](100) NULL,
    [Inc02Area] [char](3) NULL,
    [Inc07Cargo] [char](4) NULL,
    [Inc03Estado] [char](1) NULL,
    [Inc03EstadoEmpleado] [char](1) NULL,
    [Inc03Correo] [varchar](100) NULL,
    [Inc03Anexo] [varchar](10) NULL,
    [Inc03UsuarioCrea] [varchar](20) NULL,
    [Inc03FechaCrea] [datetime] NULL,
    [Inc03UsuarioModifica] [varchar](20) NULL,
    [Inc03FechaModifica] [datetime] NULL,
    [Inc03FechaIngreso] [datetime] NULL,
    [Inc03Jefe] [varchar](20) NULL,
    [Inc03Jefe2] [varchar](20) NULL,
    [Inc03Tipo] [char](1) NULL CONSTRAINT [DF_Inc03Usuario_Tipo] DEFAULT ('I'),
    [Telefono] [varchar](100) NULL,
    CONSTRAINT [PK_Inc03Usuario] PRIMARY KEY ([Inc03Usuario] ASC)
);
GO

USE [GestionSistemas];
GO
CREATE TABLE [dbo].[Inc04Item] (
    [Inc04Item] [varchar](20) NOT NULL,
    [Inc01Linea] [char](3) NULL,
    [Inc04Descripcion] [varchar](255) NULL,
    [Inc04UsuarioCrea] [varchar](20) NULL,
    [Inc04FechaCrea] [datetime] NULL,
    [Inc04UsuarioModifica] [varchar](20) NULL,
    [Inc04FechaModifica] [datetime] NULL,
    [Estado] [varchar](2) NULL CONSTRAINT [DF_Inc04Item_Estado] DEFAULT ('A'),
    [Inc04Item_LineaAnt_1] [char](3) NULL,
    [Inc04Item_LineaAnt_2] [char](3) NULL,
    [Inc04Item_ItemAnt_1] [varchar](40) NULL,
    [Inc04Item_ItemAnt_2] [varchar](40) NULL,
    CONSTRAINT [PK_Item] PRIMARY KEY ([Inc04Item] ASC)
);
GO

USE [GestionSistemas];
GO
CREATE TABLE [dbo].[Inc05Tipo] (
    [Inc05Tipo] [char](3) NOT NULL,
    [Inc05Descripcion] [varchar](60) NULL,
    [Inc05Abreviatura] [varchar](60) NULL,
    [Inc05UsuarioCrea] [varchar](20) NULL,
    [Inc05FechaCrea] [datetime] NULL,
    [Inc05UsuarioModifica] [varchar](20) NULL,
    [Inc05FechaModifica] [datetime] NULL,
    [Estado] [varchar](2) NULL,
    [Inc04Item] [char](3) NULL,
    CONSTRAINT [PK_Inc05Tipo] PRIMARY KEY ([Inc05Tipo] ASC)
);
GO

USE [GestionSistemas];
GO
CREATE TABLE [dbo].[Inc06Estado] (
    [Inc06Estado] [char](2) NOT NULL,
    [Inc06Descripcion] [varchar](60) NULL,
    [Inc06UsuarioCrea] [varchar](20) NULL,
    [Inc06FechaCrea] [datetime] NULL,
    [Inc06UsuarioModifica] [varchar](20) NULL,
    [Inc06FechaModifica] [datetime] NULL,
    CONSTRAINT [PK_Inc06Estado] PRIMARY KEY ([Inc06Estado] ASC)
);
GO

USE [GestionSistemas];
GO
CREATE TABLE [dbo].[Inc07Cargo] (
    [Inc07Cargo] [int] NOT NULL,
    [Inc07Descripcion] [varchar](60) NULL,
    [Inc07UsuarioCrea] [varchar](20) NULL,
    [Inc07FechaCrea] [datetime] NULL,
    [Inc07UsuarioModifica] [varchar](20) NULL,
    [Inc07FechaModifica] [datetime] NULL,
    CONSTRAINT [PK_Inc07Cargo] PRIMARY KEY ([Inc07Cargo] ASC)
);
GO

USE [GestionSistemas];
GO
CREATE TABLE [dbo].[Inc08SubTipo] (
    [Inc05Tipo] [varchar](3) NULL,
    [Inc08SubTipo] [varchar](3) NULL,
    [Inc08Descripcion] [varchar](60) NULL,
    [Inc08Abreviatura] [varchar](60) NULL,
    [Inc08UsuarioCrea] [varchar](20) NULL,
    [Inc08FechaCrea] [datetime] NULL,
    [Inc08UsuarioModifica] [varchar](20) NULL,
    [Inc08FechaModifica] [datetime] NULL,
    [Tipo] [char](1) NULL,
    [Estado] [varchar](2) NULL CONSTRAINT [DF_Inc08SubTipo_Estado] DEFAULT ('A'),
    [CodigoAnterior] [char](6) NULL,
    [IdCategoria] [int] IDENTITY(1,1) NOT NULL,
    [CodigoAnterior_Cambio] [char](6) NULL,
    [CodigoNuevo] [char](6) NULL,
    [TipoNuevo] [char](3) NULL,
    [TipoAntiguo] [char](3) NULL,
    [Inc05Tipo_Antiguo] [char](3) NULL,
    [IdCategoria_Antiguo] [int] NULL,
    [Inc05Tipo_Ok] [char](3) NULL,
    [IdCategoria_Ok] [int] NULL,
    [Inc08SubTipo_Ok] [varchar](3) NULL,
    [TipoError] [varchar](2) NULL,
    CONSTRAINT [PK_Inc08SubTipoe_IdCategoria] PRIMARY KEY ([IdCategoria] ASC)
);
GO

USE [GestionSistemas];
GO
CREATE TABLE [dbo].[Inc10ItemCategoria] (
    [Inc04Item] [varchar](20) NOT NULL,
    [IdCategoria] [int] NOT NULL,
    [incPrioridad] [numeric](18,2) NULL CONSTRAINT [DF__Inc10Item__incPr__7ABC33CD] DEFAULT ((1)),
    [IncImpacto] [numeric](18,2) NULL CONSTRAINT [DF__Inc10Item__IncIm__7BB05806] DEFAULT ((1)),
    [IncComplejidad] [numeric](18,2) NULL CONSTRAINT [DF__Inc10Item__IncCo__7CA47C3F] DEFAULT ((1)),
    [Estado] [varchar](1) NULL CONSTRAINT [DF__Inc10Item__Estad__7D98A078] DEFAULT ('A'),
    CONSTRAINT [PK_Inc10ItemCategoria_IdItemCategoria] PRIMARY KEY ([Inc04Item] ASC, [IdCategoria] ASC)
);
GO

USE [GestionSistemas];
GO
CREATE TABLE [dbo].[Inc20Incidencia] (
    [Inc20Incidencia] [varchar](12) NOT NULL,
    [Inc20Fecha] [datetime] NULL,
    [Inc02AreaTI] [char](3) NULL,
    [Inc03Usuario] [varchar](20) NULL,
    [Inc02Area] [char](3) NULL,
    [Inc01Linea] [char](3) NULL,
    [Inc04Item] [varchar](20) NULL,
    [Inc20Detalle] [nvarchar](MAX) NULL,
    [Inc20Estado] [char](2) NULL,
    [Inc03UsuarioTI] [varchar](20) NULL,
    [Inc05Tipo] [char](3) NULL,
    [Inc08SubTipo] [char](3) NULL,
    [Inc02AreaCausante] [char](3) NULL,
    [Inc20FechaAsignado] [datetime] NULL,
    [Inc03UsuarioAsigno] [varchar](20) NULL,
    [Inc20FechaCierre] [datetime] NULL,
    [Inc20FlagAutorizacion] [char](1) NULL CONSTRAINT [DF_Inc20Incidencia_Inc20FlagAutorizacion] DEFAULT ('N'),
    [Inc20UsuarioCrea] [varchar](20) NULL,
    [Inc20UsuarioModifica] [varchar](20) NULL,
    [Inc20FechaModifica] [datetime] NULL CONSTRAINT [DF_Inc20Incidencia_Inc20FechaModif] DEFAULT (getdate()),
    [Inc20FlagLectura] [bit] NULL CONSTRAINT [DF_Inc20Incidencia_Inc20FlagLectura] DEFAULT ((1)),
    [IncSLA] [int] NULL,
    [Inc20NroAcciones] [int] NULL,
    [IncPrioridad] [int] NULL CONSTRAINT [DF_Inc20Incidencia_IncPrioridad] DEFAULT ((1)),
    [IncImpacto] [int] NULL CONSTRAINT [DF_Inc20Incidencia_IncImpacto] DEFAULT ((1)),
    [IncComplejidad] [int] NULL CONSTRAINT [DF_Inc20Incidencia_IncComplejidad] DEFAULT ((1)),
    [Inc01Linea_Ant] [char](3) NULL,
    [Inc04Item_Ant] [varchar](20) NULL,
    [Inc01Linea_AntBackup] [char](3) NULL,
    [Inc04Item_AntBackup] [varchar](20) NULL,
    [Inc01Linea_Nuevo] [char](3) NULL,
    [Inc04Item_Nuevo] [varchar](20) NULL,
    [IdCategoria] [int] NULL,
    [Categoria_AntBackup] [char](6) NULL,
    [Categoria_Antigua] [char](6) NULL,
    [Categoria_Nuevo] [char](6) NULL,
    [Inc02Area_Antiguo] [char](3) NULL,
    [Inc04Item_Antiguo] [varchar](20) NULL,
    [Inc05Tipo_Antiguo] [char](3) NULL,
    [Inc08SubTipo_Antiguo] [char](3) NULL,
    [IdCategoria_Antiguo] [int] NULL,
    [Inc02Area_Ok] [char](3) NULL,
    [Inc04Item_Ok] [varchar](20) NULL,
    [Inc05Tipo_Ok] [char](3) NULL,
    [Inc08SubTipo_Ok] [char](3) NULL,
    [IdCategoria_Ok] [int] NULL,
    [Inc01Linea_Antiguo] [char](3) NULL,
    [Inc01Linea_Ok] [char](3) NULL,
    [Inc20Titulo] [varchar](MAX) NULL,
    [Inc20FechaAtencion] [datetime] NULL,
    [Respuesta] [varchar](MAX) NULL CONSTRAINT [DF__Inc20Inci__Respu__00FF1D08] DEFAULT (NULL),
    [Inc20UsuarioGC] [varchar](20) NULL CONSTRAINT [DF__Inc20Inci__Inc20__17E28260] DEFAULT (NULL),
    CONSTRAINT [PK_Inc20Incidencia] PRIMARY KEY ([Inc20Incidencia] ASC)
);
GO

USE [GestionSistemas];
GO
CREATE TABLE [dbo].[Inc20IncidenciaArchivos] (
    [Inc20Incidencia] [varchar](12) NOT NULL,
    [NombreEncryptado] [varchar](500) NULL,
    [NombreReal] [varchar](500) NULL,
    [FlagSolicitud] [int] NULL,
    [Estado] [int] NULL,
    [FechaRegistro] [datetime] NULL,
    [UsuarioRegistro] [varchar](12) NULL
);
GO

USE [GestionSistemas];
GO
CREATE TABLE [dbo].[Inc21Avance] (
    [Inc21Avance] [varchar](12) NOT NULL,
    [Inc20Incidencia] [varchar](12) NOT NULL,
    [Inc21Detalle] [nvarchar](MAX) NULL,
    [Inc21Fecha] [datetime] NULL,
    [Inc21TiempoUtilizado] [decimal](8,2) NULL,
    [Inc03UsuarioTI] [varchar](20) NULL,
    [Inc21Porcentaje] [numeric](18,2) NULL CONSTRAINT [DF_Inc21Proceso_Inc21Porcentaje] DEFAULT ((0)),
    [Inc21Autoriza] [char](1) NULL CONSTRAINT [DF_Inc21Avance_Inc21Autoriza] DEFAULT ('N'),
    [Inc21Confirma] [char](1) NULL CONSTRAINT [DF_Inc21Avance_Inc21Confirma] DEFAULT ('N'),
    CONSTRAINT [PK_In21Proceso] PRIMARY KEY ([Inc21Avance] ASC, [Inc20Incidencia] ASC)
);
GO

USE [GestionSistemas];
GO
CREATE TABLE [dbo].[IncComplejidad] (
    [IncComplejidad] [int] NOT NULL,
    [Descripcion] [varchar](100) NULL,
    [DescripcionLarga] [varchar](8000) NULL,
    [Estado] [varchar](2) NULL,
    CONSTRAINT [PK_IncComplejidad_IncComplejidad] PRIMARY KEY ([IncComplejidad] ASC)
);
GO

USE [GestionSistemas];
GO
CREATE TABLE [dbo].[IncFormato] (
    [IncFormato] [int] NOT NULL,
    [Descripcion] [varchar](150) NOT NULL,
    [Archivo] [varchar](255) NULL,
    [Estado] [varchar](2) NULL
);
GO

USE [GestionSistemas];
GO
CREATE TABLE [dbo].[IncImpacto] (
    [IncImpacto] [int] NOT NULL,
    [Descripcion] [varchar](100) NULL,
    [DescripcionLarga] [varchar](8000) NULL,
    [Estado] [varchar](2) NULL,
    CONSTRAINT [PK_IncImpacto_IncImpacto] PRIMARY KEY ([IncImpacto] ASC)
);
GO

USE [GestionSistemas];
GO
CREATE TABLE [dbo].[IncPrioridad] (
    [IncPrioridad] [int] NOT NULL,
    [descripcion] [varchar](100) NULL,
    [DescripcionLarga] [varchar](8000) NULL,
    [estado] [varchar](2) NULL,
    CONSTRAINT [PK_IncPrioridad_IncPrioridad] PRIMARY KEY ([IncPrioridad] ASC)
);
GO

USE [GestionSistemas];
GO
CREATE TABLE [dbo].[IncSLA] (
    [IncSLA] [int] NOT NULL,
    [RangoInicial] [numeric](18,2) NULL,
    [RangoFinal] [numeric](18,2) NULL,
    [Dias] [varchar](5) NULL,
    [Estado] [varchar](2) NULL,
    CONSTRAINT [PK_IncSLA_IncSLA] PRIMARY KEY ([IncSLA] ASC)
);
GO

USE [GestionSistemas];
GO
CREATE TABLE [dbo].[IncSLAPorcentaje] (
    [IncTipo] [int] NOT NULL,
    [Descripcion] [varchar](100) NULL,
    [Porcentaje] [numeric](18,2) NULL,
    [Estado] [varchar](2) NULL,
    CONSTRAINT [PK_IncSLAPorcentaje_IncTipo] PRIMARY KEY ([IncTipo] ASC)
);
GO

USE [GestionSistemas];
GO
CREATE TABLE [dbo].[IncVideoTutorial] (
    [IncVideoTutorial] [int] NOT NULL,
    [Descripcion] [varchar](150) NOT NULL,
    [URLVideo] [varchar](255) NULL,
    [Estado] [varchar](2) NULL
);
GO

USE [GestionSistemas];
GO
CREATE TABLE [dbo].[Sis01Menu] (
    [Sis01Sistema] [varchar](12) NOT NULL,
    [Sis01MenuId] [int] NOT NULL,
    [Sis01Descripcion] [varchar](50) NULL,
    [Sis01PadreId] [int] NULL,
    [Sis01Posicion] [int] NULL,
    [Sis01Icono] [varchar](100) NULL,
    [Sis01Habilitado] [char](1) NULL,
    [Sis01Url] [varchar](50) NULL,
    [Sis01FechaCrea] [varchar](15) NULL,
    [Sis01UsuarioCrea] [varchar](15) NULL,
    [Sis01FechaModif] [varchar](15) NULL,
    [Sis01UsuarioModif] [varchar](15) NULL,
    CONSTRAINT [PK_Sis01Menu] PRIMARY KEY ([Sis01Sistema] ASC, [Sis01MenuId] ASC)
);
GO

USE [IntranetCalimod];
GO
CREATE TABLE [dbo].[Usuarios] (
    [UsuarioId] [int] IDENTITY(1,1) NOT NULL,
    [Nombre] [nvarchar](50) NOT NULL,
    [NombreCompleto] [varchar](150) NULL,
    [Clave] [nvarchar](250) NOT NULL,
    [Habilitado] [bit] NOT NULL CONSTRAINT [DF_Usuarios_Habilitado] DEFAULT ((1)),
    [FechaCreacion] [datetime] NULL CONSTRAINT [DF_Usuarios_FechaCreacion] DEFAULT (getdate()),
    [UsuarioCreacion] [varchar](50) NULL CONSTRAINT [DF_Usuarios_UsuarioCreacion] DEFAULT (suser_sname()),
    [FechaModificacion] [datetime] NULL,
    [UsuarioModificacion] [varchar](50) NULL,
    [TokenNotificacionIncidenciaApp] [varchar](MAX) NULL CONSTRAINT [DF__Usuarios__TokenN__52AE4273] DEFAULT (NULL),
    [CorreoRecuperacion] [varchar](50) NULL CONSTRAINT [DF__Usuarios__Correo__54968AE5] DEFAULT (NULL),
    [TelefonoRecuperacion] [varchar](12) NULL CONSTRAINT [DF__Usuarios__Telefo__558AAF1E] DEFAULT (NULL),
    [ImagenPerfil] [varchar](100) NULL CONSTRAINT [DF__Usuarios__Imagen__567ED357] DEFAULT (NULL),
    CONSTRAINT [PK_Usuarios] PRIMARY KEY ([UsuarioId] ASC)
);
GO

USE [IntranetCalimod];
GO
CREATE TABLE [dbo].[Menu] (
    [MenuId] [int] IDENTITY(1,1) NOT NULL,
    [SistemaId] [int] NOT NULL,
    [Descripcion] [varchar](150) NOT NULL,
    [PadreId] [int] NOT NULL,
    [Posicion] [int] NOT NULL,
    [Icono] [varchar](150) NULL,
    [Habilitado] [bit] NOT NULL CONSTRAINT [DF_Menu_Habilitado] DEFAULT ((1)),
    [FormUrl] [varchar](250) NULL,
    [FechaCreacion] [datetime] NULL CONSTRAINT [DF_Menu_FechaCreacion] DEFAULT (getdate()),
    [UsuarioCreacion] [varchar](50) NULL CONSTRAINT [DF_Menu_UsuarioCreacion] DEFAULT (suser_sname()),
    [FechaModificacion] [datetime] NULL,
    [UsuarioModificacion] [varchar](50) NULL,
    [MenuIdAnt] [int] NULL,
    CONSTRAINT [PK_Menu_1] PRIMARY KEY ([MenuId] ASC, [SistemaId] ASC)
);
GO

USE [IntranetCalimod];
GO
CREATE TABLE [dbo].[Perfil] (
    [PerfilId] [int] IDENTITY(1,1) NOT NULL,
    [SistemaId] [int] NOT NULL,
    [Descripcion] [varchar](50) NOT NULL,
    [Compania] [char](8) NOT NULL,
    [UnidadNegocio] [char](4) NOT NULL,
    [AlmacenCodigo] [char](10) NOT NULL,
    [Habilitado] [bit] NOT NULL,
    [FechaCreacion] [datetime] NULL CONSTRAINT [DF_Perfil_FechaCreacion] DEFAULT (getdate()),
    [UsuarioCreacion] [varchar](50) NULL CONSTRAINT [DF_Perfil_UsuarioCreacion] DEFAULT (suser_sname()),
    [FechaModificacion] [datetime] NULL,
    [UsuarioModificacion] [varchar](50) NULL,
    CONSTRAINT [PK_Perfil] PRIMARY KEY ([PerfilId] ASC, [SistemaId] ASC)
);
GO

USE [IntranetCalimod];
GO
CREATE TABLE [dbo].[PerfilMenu] (
    [PerfilId] [int] NOT NULL,
    [MenuId] [int] NOT NULL,
    [SistemaId] [int] NOT NULL,
    [FechaCreacion] [datetime] NULL CONSTRAINT [DF_Perfil_Menu_FechaCreacion] DEFAULT (getdate()),
    [UsuarioCreacion] [varchar](50) NULL CONSTRAINT [DF_Perfil_Menu_UsuarioCreacion] DEFAULT (suser_sname()),
    [FechaModificacion] [datetime] NULL,
    [UsuarioModificacion] [varchar](50) NULL,
    CONSTRAINT [PK_Perfil_Menu] PRIMARY KEY ([PerfilId] ASC, [MenuId] ASC, [SistemaId] ASC)
);
GO

USE [IntranetCalimod];
GO
CREATE TABLE [dbo].[PerfilUsuario] (
    [UsuarioId] [int] NOT NULL,
    [PerfilId] [int] NOT NULL,
    [SistemaId] [int] NOT NULL,
    [FechaCreacion] [datetime] NULL CONSTRAINT [DF_PerfilUsuario_FechaCreacion] DEFAULT (getdate()),
    [UsuarioCreacion] [varchar](50) NULL CONSTRAINT [DF_PerfilUsuario_UsuarioCreacion] DEFAULT (suser_sname()),
    [FechaModificacion] [datetime] NULL,
    [UsuarioModificacion] [varchar](50) NULL,
    CONSTRAINT [PK_PerfilUsuario] PRIMARY KEY ([UsuarioId] ASC, [PerfilId] ASC, [SistemaId] ASC)
);
GO

USE [Spring];
GO
CREATE TABLE [dbo].[Usuario] (
    [Usuario] [char](20) NOT NULL,
    [UsuarioPerfil] [char](2) NULL,
    [Nombre] [char](255) NULL,
    [Clave] [char](100) NULL,
    [ExpirarPasswordFlag] [char](1) NULL,
    [FechaExpiracion] [datetime] NULL,
    [UltimoLogin] [datetime] NULL,
    [NumeroLoginsDisponible] [int] NULL,
    [NumeroLoginsUsados] [int] NULL,
    [SQLLogin] [char](20) NULL,
    [SQLPassword] [char](10) NULL,
    [Estado] [char](1) NULL,
    [UltimoUsuario] [char](10) NULL,
    [UltimaFechaModif] [datetime] NULL,
    [UsuarioRed] [char](20) NULL,
    [PersonaNumero] [int] NULL,
    [LoginPersonaFlag] [char](1) NULL,
    [CorreoTrabajo] [varchar](100) NULL CONSTRAINT [DF__Usuario__CorreoT__452EB834] DEFAULT (NULL),
    CONSTRAINT [PK__Usuario__2DB1C7EE] PRIMARY KEY ([Usuario] ASC)
);
GO

USE [Spring];
GO
CREATE TABLE [dbo].[EmpleadoMast] (
    [Empleado] [int] NOT NULL,
    [TipoPago] [char](2) NULL,
    [TipoTrabajador] [char](2) NULL,
    [Raza] [char](3) NULL,
    [Religion] [char](3) NULL,
    [TipoVisa] [char](3) NULL,
    [VisaFechaInicio] [datetime] NULL,
    [VisaFechaExpiracion] [datetime] NULL,
    [ServicioMilitarDesde] [datetime] NULL,
    [ServicioMilitarHasta] [datetime] NULL,
    [NumeroArchivo] [char](10) NULL,
    [UnidadNegocioAsignada] [char](4) NULL,
    [LocacionAsignada] [char](4) NULL,
    [LocaciondePago] [char](4) NULL,
    [ResponsableEmpleado] [char](4) NULL,
    [ResponsableJefe] [char](4) NULL,
    [ResponsableSueldo] [char](4) NULL,
    [CodigoUsuario] [char](20) NULL,
    [TipoAsistenciaSocial] [char](3) NULL,
    [CentroAsistenciaSocial] [char](6) NULL,
    [TipoCarnetAsistenciaSocial] [char](2) NULL,
    [CarnetAsistenciaSocial] [char](20) NULL,
    [TipoPension] [char](3) NULL,
    [FechaInicioPension] [datetime] NULL,
    [FechaTerminoPension] [datetime] NULL,
    [CodigoAFP] [char](3) NULL,
    [NumeroAFP] [char](20) NULL,
    [BancoCTS] [char](3) NULL,
    [TipoCuentaCTS] [char](3) NULL,
    [TipoMonedaCTS] [char](2) NULL,
    [NumeroCuentaCTS] [char](20) NULL,
    [EstadoEmpleado] [char](1) NULL,
    [TipoPlanilla] [char](2) NULL,
    [FechaIngreso] [datetime] NULL,
    [FechaUltimaPlanilla] [datetime] NULL,
    [TipoContrato] [char](2) NULL,
    [FechaInicioContrato] [datetime] NULL,
    [FechaFinContrato] [datetime] NULL,
    [FechaCese] [datetime] NULL,
    [RazonCese] [char](150) NULL,
    [Contratista] [int] NULL,
    [CompaniaSocio] [char](8) NOT NULL,
    [CentroCostos] [char](10) NULL,
    [AFE] [char](15) NULL,
    [DeptoOrganizacion] [char](3) NULL,
    [TipoHorario] [int] NULL,
    [GradoSalario] [char](3) NULL,
    [Cargo] [char](3) NULL,
    [Posicion] [char](6) NULL,
    [NivelAcceso] [char](3) NULL,
    [FlagIPSSVIDA] [char](1) NULL,
    [FlagAccTrabajo] [char](1) NULL,
    [FlagSindicato] [char](1) NULL,
    [FlagCooperativa] [char](1) NULL,
    [FlagRetencionJudicial] [char](1) NULL,
    [FlagReingresos] [char](1) NULL,
    [FlagComision] [char](1) NULL,
    [FlagImpuestoRenta] [char](1) NULL,
    [CorreoInterno] [varchar](255) NULL,
    [SueldoActualLocal] [money] NULL,
    [SueldoActualDolar] [money] NULL,
    [SueldoAnteriorLocal] [money] NULL,
    [SueldoAnteriorDolar] [money] NULL,
    [MonedaPago] [char](2) NULL,
    [EmpleadoRelacionado] [int] NULL,
    [Foto] [char](30) NULL,
    [TarjetadeCredito] [char](20) NULL,
    [MotivoCese] [int] NULL,
    [DepartamentoOrganizacional] [char](100) NULL,
    [DepartamentoOperacional] [char](10) NULL,
    [Perfil] [int] NULL,
    [NivelSalario] [char](3) NULL,
    [FechaLiquidacion] [datetime] NULL,
    [FechaReingreso] [datetime] NULL,
    [UnidadReplicacion] [char](10) NULL,
    [CodigoCargo] [int] NULL,
    [UltimaFechaPagoVacacion] [datetime] NULL,
    [Gerencia] [int] NULL,
    [SubGerencia] [int] NULL,
    [RedondeoACuenta] [float] NULL,
    [RUCCentroAsistenciaSocial] [char](20) NULL,
    [PlantillaConcepto] [int] NULL,
    [Sucursal] [char](4) NULL,
    [Actividad] [char](8) NULL,
    [Estado] [char](1) NULL,
    [UltimoUsuario] [char](20) NULL,
    [UltimaFechaModif] [datetime] NULL,
    [TipoPlanillaOP] [char](2) NULL,
    [AsignacionFamiliar] [char](1) NULL,
    [FlagAccTrabajoP] [char](1) NULL,
    [SucursalBancoNacion] [char](4) NULL,
    [GrupoOcupacional] [numeric](10,0) NULL,
    [Cliente] [int] NULL,
    [ClienteUnidad] [int] NULL,
    [TareoNivel] [int] NULL,
    [EmpleadoNivel] [int] NULL,
    [TipoPuesto] [numeric](10,0) NULL,
    [FlagTrabajadorPensionista] [char](1) NULL CONSTRAINT [DF__EmpleadoM__FlagT__61D54E7C] DEFAULT ((0)),
    [TipoPensionJubilacion] [char](1) NULL,
    [FechaBajaEPS] [datetime] NULL,
    [FlagSCTRSalud] [char](1) NULL CONSTRAINT [DF__EmpleadoM__FlagS__62C972B5] DEFAULT ((0)),
    [FlagSCTRPension] [char](1) NULL CONSTRAINT [DF__EmpleadoM__FlagS__63BD96EE] DEFAULT ((0)),
    [FlagDiscapacidad] [char](1) NULL CONSTRAINT [DF__EmpleadoM__FlagD__64B1BB27] DEFAULT ((0)),
    [TipoRemuneracionRTPS] [char](1) NULL,
    [FlagSujetoControl] [char](1) NULL CONSTRAINT [DF__EmpleadoM__FlagS__65A5DF60] DEFAULT ((0)),
    [FlagSindicalizado] [char](1) NULL CONSTRAINT [DF__EmpleadoM__FlagS__669A0399] DEFAULT ((0)),
    [MunicipalidadNacimiento] [char](6) NULL,
    [FlagDomiciliado] [char](1) NULL CONSTRAINT [DF__EmpleadoM__FlagD__678E27D2] DEFAULT ((1)),
    [EstablecimientoRTPS] [char](4) NULL,
    [ProveedorIntermediacion] [int] NULL,
    [NivelEducativoRTPS] [char](2) NULL,
    [FlagSMF] [char](1) NULL,
    [EstadoNivelacion] [varchar](2) NULL,
    [AprobadorNivelacion] [int] NULL,
    [SolicitadorNivelacion] [int] NULL,
    [FechaVacaciones] [datetime] NULL,
    [PosicionOrganigrama] [int] NULL,
    [Formato_2] [varchar](3) NULL,
    [FlagRegimenAlternativo] [char](1) NULL CONSTRAINT [DF__EmpleadoM__flagr__146BBB41] DEFAULT ((0)),
    [FlagJornadaMaxima] [char](1) NULL CONSTRAINT [DF__EmpleadoM__flagj__155FDF7A] DEFAULT ((0)),
    [FlagHorarioNocturno] [char](1) NULL CONSTRAINT [DF__EmpleadoM__flagh__165403B3] DEFAULT ((0)),
    [FlagOtrosQuinta] [char](1) NULL CONSTRAINT [DF__EmpleadoM__flago__174827EC] DEFAULT ((0)),
    [FlagQuintaExonerada] [char](1) NULL CONSTRAINT [DF__EmpleadoM__flagq__183C4C25] DEFAULT ((0)),
    [SituacionEspecial] [char](1) NULL CONSTRAINT [DF__EmpleadoM__situa__1930705E] DEFAULT ((0)),
    [FlagMadreResponsabilidad] [char](1) NULL CONSTRAINT [DF__EmpleadoM__flagm__1A249497] DEFAULT ((0)),
    [FlagCentroFormacion] [char](1) NULL CONSTRAINT [DF__EmpleadoM__flagc__1B18B8D0] DEFAULT ((4)),
    [TipoModalidadFormativa] [char](2) NULL,
    [PrestadorServicio] [char](1) NULL,
    [FlagAseguraPension] [char](1) NULL CONSTRAINT [DF__EmpleadoM__FlagA__2C39EE88] DEFAULT ('0'),
    [CategoriaOcupacional] [char](2) NULL CONSTRAINT [DF__EmpleadoM__Categ__2D2E12C1] DEFAULT ('3'),
    [FlagConvenioDobleTrib] [char](1) NULL CONSTRAINT [DF__EmpleadoM__FlagC__2E2236FA] DEFAULT ('0'),
    [Flg_Sup] [char](1) NULL,
    [TipoComisionAFP] [char](1) NULL,
    [FlagEducacionCompletaIEP] [char](1) NOT NULL CONSTRAINT [DF_FlagEducacionCompletaIEP] DEFAULT ('S'),
    [CodigoDiscapacidad] [varchar](30) NULL,
    [CodigoUnidad] [int] NULL,
    [division] [char](4) NULL,
    [FirmaDigitalizada] [char](100) NULL,
    [FlagdeConfianza] [char](1) NULL,
    [FlagEduacionIEP] [char](1) NULL,
    [FlagTransferido] [char](1) NULL,
    [JefeResponsable] [int] NULL,
    [JefeResponsableCompania] [char](8) NULL,
    [Oficina] [char](10) NULL,
    [orden_organigrama] [varchar](200) NULL,
    [Tiempo_Contrato_Total] [varchar](12) NULL,
    [Tiempo_Servicio] [varchar](12) NULL,
    [UnidadOperativa] [char](4) NULL,
    [UnidadTrabajo] [char](4) NULL,
    CONSTRAINT [PK__EmpleadoMast__66E023A9] PRIMARY KEY ([Empleado] ASC, [CompaniaSocio] ASC)
);
GO

USE [Spring];
GO
CREATE TABLE [dbo].[PersonaMast] (
    [Persona] [int] NOT NULL,
    [Origen] [char](4) NOT NULL,
    [ApellidoPaterno] [char](30) NULL,
    [ApellidoMaterno] [char](30) NULL,
    [Nombres] [char](40) NULL,
    [NombreCompleto] [char](70) NULL,
    [Busqueda] [char](50) NOT NULL,
    [TipoDocumento] [char](1) NULL,
    [Documento] [char](20) NOT NULL,
    [CodigoBarras] [char](18) NULL,
    [EsCliente] [char](1) NULL,
    [EsProveedor] [char](1) NULL,
    [EsEmpleado] [char](1) NULL,
    [EsOtro] [char](1) NULL,
    [TipoPersona] [char](1) NULL,
    [FechaNacimiento] [datetime] NULL,
    [CiudadNacimiento] [char](40) NULL,
    [Sexo] [char](1) NULL,
    [Nacionalidad] [char](20) NULL,
    [EstadoCivil] [char](1) NULL,
    [NivelInstruccion] [char](3) NULL,
    [Direccion] [varchar](120) NULL,
    [CodigoPostal] [char](3) NULL,
    [Provincia] [char](3) NULL,
    [Departamento] [char](3) NULL,
    [Telefono] [varchar](30) NULL,
    [Fax] [varchar](15) NULL,
    [DocumentoFiscal] [char](20) NULL,
    [DocumentoIdentidad] [char](20) NULL,
    [CarnetExtranjeria] [char](10) NULL,
    [DocumentoMilitarFA] [char](10) NULL,
    [TipoBrevete] [char](1) NULL,
    [Brevete] [char](18) NULL,
    [Pasaporte] [char](18) NULL,
    [NombreEmergencia] [varchar](50) NULL,
    [DireccionEmergencia] [varchar](60) NULL,
    [TelefonoEmergencia] [varchar](15) NULL,
    [BancoMonedaLocal] [char](3) NULL,
    [TipoCuentaLocal] [char](3) NULL,
    [CuentaMonedaLocal] [varchar](30) NULL,
    [BancoMonedaExtranjera] [char](3) NULL,
    [TipoCuentaExtranjera] [char](3) NULL,
    [CuentaMonedaExtranjera] [varchar](30) NULL,
    [PersonaAnt] [char](15) NULL,
    [CorreoElectronico] [varchar](200) NULL,
    [ClasePersonaCodigo] [char](3) NULL,
    [EnfermedadGraveFlag] [char](1) NULL,
    [Estado] [char](1) NULL,
    [UltimoUsuario] [char](20) NULL,
    [UltimaFechaModif] [datetime] NULL,
    [TipoPersonaUsuario] [char](3) NULL,
    [IngresoFechaRegistro] [datetime] NULL,
    [IngresoAplicacionCodigo] [char](2) NULL,
    [IngresoUsuario] [char](20) NULL,
    [PYMEFlag] [char](1) NULL,
    [GrupoEmpresarial] [char](4) NULL,
    [PersonaClasificacion] [char](8) NULL,
    [TarjetadeCredito] [char](20) NULL,
    [FlagActualizacion] [char](1) NULL,
    [Celular] [char](15) NULL,
    [ParentescoEmergencia] [char](10) NULL,
    [CelularEmergencia] [char](15) NULL,
    [LugarNacimiento] [varchar](255) NULL,
    [DireccionReferencia] [varchar](255) NULL,
    [FlagRepetido] [char](1) NULL,
    [CodDiscamec] [varchar](15) NULL,
    [FecIniDiscamec] [datetime] NULL,
    [FecFinDiscamec] [datetime] NULL,
    [CodLicArma] [varchar](15) NULL,
    [MarcaArma] [varchar](10) NULL,
    [SerieArma] [varchar](10) NULL,
    [InicioArma] [datetime] NULL,
    [VencimientoArma] [datetime] NULL,
    [SeguroDiscamec] [char](1) NULL,
    [CorrelativoSCTR] [varchar](3) NULL,
    [SUNATNacionalidad] [char](6) NULL,
    [SUNATVia] [char](2) NULL,
    [SUNATZona] [char](2) NULL,
    [SUNATUbigeo] [char](10) NULL,
    [SUNATDomiciliado] [char](1) NULL,
    [PaisEmisor] [char](3) NULL,
    [CodigoLDN] [char](3) NULL,
    [SUNATConvenio] [char](10) NULL,
    [TipoCliente] [varchar](2) NULL,
    [Brevete_FecVcto] [datetime] NULL,
    [CarnetExtranjeria_FecVcto] [datetime] NULL,
    [CodigoInterbancario] [varchar](30) NULL,
    [FLAGSOLICITAUSUARIO] [char](1) NULL,
    [PAIS] [char](4) NULL,
    [SUNATNDConvenio] [char](2) NULL,
    [SUNATNDTipoRenta] [char](2) NULL,
    [SUNATNDExoneracion] [char](1) NULL,
    [SUNATNDServicio] [char](1) NULL,
    CONSTRAINT [PK__PersonaMast__68C86C1B] PRIMARY KEY ([Persona] ASC)
);
GO

USE [Spring];
GO
CREATE TABLE [dbo].[HR_PuestoEmpresa] (
    [CodigoPuesto] [int] NOT NULL,
    [GradoSalario] [char](3) NULL,
    [Descripcion] [char](60) NULL,
    [DescripcionIngles] [char](60) NULL,
    [Comentarios] [char](250) NULL,
    [Estado] [char](1) NULL,
    [UnidadNegocio] [char](4) NULL,
    [UltimoUsuario] [char](20) NULL,
    [UltimaFechaModif] [datetime] NULL,
    [UnidadReplicacion] [char](4) NULL,
    [TipoPuesto] [int] NULL,
    [CategoriaFuncional] [char](4) NULL,
    [PuestoSuperior] [int] NULL,
    [PlantillaEvaluacion] [char](5) NULL,
    [PlantillaDocumento] [int] NULL,
    [CodAnterior] [char](10) NULL,
    [codigoRTPS] [char](6) NULL,
    [CodigoCAP] [numeric](11,0) NULL,
    [FlagProrrogaAF] [char](1) NULL,
    [PeriodoProrrogaAF] [char](6) NULL,
    [DescripcionCap] [varchar](60) NULL,
    [GrupoOcupacional] [numeric](10,0) NULL,
    [PlantillaPotencial] [char](5) NULL,
    CONSTRAINT [PK_HR_PuestoEmpresa] PRIMARY KEY ([CodigoPuesto] ASC)
);
GO

