/*
    Archivo: 19_MejorasFuncionalesSinIA.sql
    Objetivo: Incorporar las brechas operativas identificadas en el análisis funcional comparativo sin adelantar capacidades de IA.
    Responsabilidad: Añadir configuración mantenible, identidad corporativa sincronizable, esfuerzo/área causante, bloqueo real por aprobación, recursos de soporte, notificaciones, edición temprana y registro de tickets por mesa de ayuda.
    Dependencias: Requiere la ejecución previa de 00_PrepararGestionSistemas.sql hasta 18_ReportesTI.sql.
    Orden: Ejecutar después de 18_ReportesTI.sql.
    Consideraciones: No implementa LLM, RAG, diagnóstico, embeddings ni ejecución automática de acciones. Las operaciones históricas permanecen trazables y los catálogos se inactivan en lugar de eliminarse.
*/

Use [GestionSistemas]
Go

Set Xact_Abort On
Go

/* ============================== ESTRUCTURA ============================== */

If Col_Length('dbo.TI_Usuario', 'Documento') Is Null
    Alter Table dbo.TI_Usuario Add Documento varchar(20) Null
Go

If Col_Length('dbo.TI_Usuario', 'FuenteIdentidad') Is Null
    Alter Table dbo.TI_Usuario Add FuenteIdentidad varchar(20) Not Null Constraint DF_TI_Usuario_FuenteIdentidad Default ('LOCAL')
Go

If Col_Length('dbo.TI_Usuario', 'EstadoCorporativo') Is Null
    Alter Table dbo.TI_Usuario Add EstadoCorporativo varchar(20) Null
Go

If Col_Length('dbo.TI_Usuario', 'UltimaSincronizacion') Is Null
    Alter Table dbo.TI_Usuario Add UltimaSincronizacion datetime2(0) Null
Go

If Col_Length('dbo.TI_Incidencia', 'UsuarioRegistro') Is Null
    Alter Table dbo.TI_Incidencia Add UsuarioRegistro varchar(20) Null
Go

If Exists (Select 1 From sys.columns Where object_id = Object_Id('dbo.TI_Incidencia') and name = 'UsuarioRegistro' and is_nullable = 1)
Begin
    Update dbo.TI_Incidencia Set UsuarioRegistro = UsuarioSolicitante Where UsuarioRegistro Is Null
    Alter Table dbo.TI_Incidencia Alter Column UsuarioRegistro varchar(20) Not Null
End
Go

If Not Exists (Select 1 From sys.foreign_keys Where name = 'FK_TI_Incidencia_UsuarioRegistro')
    Alter Table dbo.TI_Incidencia Add Constraint FK_TI_Incidencia_UsuarioRegistro Foreign Key (UsuarioRegistro) References dbo.TI_Usuario (Usuario)
Go

If Col_Length('dbo.TI_IncidenciaAvance', 'AreaCausante') Is Null
    Alter Table dbo.TI_IncidenciaAvance Add AreaCausante char(3) Null
Go

If Not Exists (Select 1 From sys.foreign_keys Where name = 'FK_TI_IncidenciaAvance_AreaCausante')
    Alter Table dbo.TI_IncidenciaAvance Add Constraint FK_TI_IncidenciaAvance_AreaCausante Foreign Key (AreaCausante) References dbo.TI_Area (Area)
Go

If Col_Length('dbo.TI_BaseConocimiento', 'VisibleUsuario') Is Null
    Alter Table dbo.TI_BaseConocimiento Add VisibleUsuario bit Not Null Constraint DF_TI_BaseConocimiento_VisibleUsuario Default (0)
Go

If Object_Id('dbo.TI_ParametroSLA', 'U') Is Null
Begin
    Create Table dbo.TI_ParametroSLA (
        Prioridad               tinyint         Not Null,
        SlaObjetivoMinutos      int             Not Null,
        Estado                  varchar(2)      Not Null,
        UltimoUsuario           varchar(20)     Null,
        UltimaFechaModif        datetime2(0)    Null,

        Constraint PK_TI_ParametroSLA Primary Key (Prioridad),
        Constraint CK_TI_ParametroSLA_Prioridad Check (Prioridad Between 1 and 5),
        Constraint CK_TI_ParametroSLA_Minutos Check (SlaObjetivoMinutos > 0)
    )
End
Go

If Object_Id('dbo.TI_FormatoSoporte', 'U') Is Null
Begin
    Create Table dbo.TI_FormatoSoporte (
        FormatoCodigo           varchar(20)     Not Null,
        Titulo                  nvarchar(120)   Not Null,
        Descripcion             nvarchar(500)   Null,
        NombreOriginal          nvarchar(260)   Not Null,
        RutaArchivo             nvarchar(1000)  Not Null,
        TipoMime                varchar(100)    Not Null,
        TipoTicket              char(3)         Null,
        Estado                  varchar(2)      Not Null,
        UltimoUsuario           varchar(20)     Null,
        UltimaFechaModif        datetime2(0)    Null,

        Constraint PK_TI_FormatoSoporte Primary Key (FormatoCodigo),
        Constraint FK_TI_FormatoSoporte_Tipo Foreign Key (TipoTicket) References dbo.TI_Tipo (Tipo)
    )
End
Go

If Object_Id('dbo.TI_Notificacion', 'U') Is Null
Begin
    Create Table dbo.TI_Notificacion (
        NotificacionNumero      bigint Identity(1,1) Not Null,
        Usuario                 varchar(20)     Not Null,
        IncidenciaNumero        varchar(12)     Null,
        Tipo                    varchar(30)     Not Null,
        Titulo                  nvarchar(120)   Not Null,
        Mensaje                 nvarchar(500)   Not Null,
        Ruta                    varchar(250)    Null,
        Leida                   bit             Not Null Constraint DF_TI_Notificacion_Leida Default (0),
        Fecha                   datetime2(0)    Not Null,
        FechaLectura            datetime2(0)    Null,

        Constraint PK_TI_Notificacion Primary Key (NotificacionNumero),
        Constraint FK_TI_Notificacion_Usuario Foreign Key (Usuario) References dbo.TI_Usuario (Usuario),
        Constraint FK_TI_Notificacion_Incidencia Foreign Key (IncidenciaNumero) References dbo.TI_Incidencia (IncidenciaNumero)
    )

    Create Index IX_TI_Notificacion_UsuarioLeidaFecha On dbo.TI_Notificacion (Usuario, Leida, Fecha Desc)
End
Go

If Not Exists (Select 1 From dbo.TI_ParametroSLA Where Prioridad = 1)
    Insert dbo.TI_ParametroSLA (Prioridad, SlaObjetivoMinutos, Estado) Values (1, 4320, 'A')
If Not Exists (Select 1 From dbo.TI_ParametroSLA Where Prioridad = 2)
    Insert dbo.TI_ParametroSLA (Prioridad, SlaObjetivoMinutos, Estado) Values (2, 2880, 'A')
If Not Exists (Select 1 From dbo.TI_ParametroSLA Where Prioridad = 3)
    Insert dbo.TI_ParametroSLA (Prioridad, SlaObjetivoMinutos, Estado) Values (3, 1440, 'A')
If Not Exists (Select 1 From dbo.TI_ParametroSLA Where Prioridad = 4)
    Insert dbo.TI_ParametroSLA (Prioridad, SlaObjetivoMinutos, Estado) Values (4, 480, 'A')
If Not Exists (Select 1 From dbo.TI_ParametroSLA Where Prioridad = 5)
    Insert dbo.TI_ParametroSLA (Prioridad, SlaObjetivoMinutos, Estado) Values (5, 240, 'A')
Go

/* ============================== BLOQUEO POR APROBACIÓN ============================== */

Create Or Alter Trigger dbo.Tr_TI_Incidencia_BloqueoAprobacion
On dbo.TI_Incidencia
After Update
As
Begin
    Set NoCount On

    If Exists (
        Select 1
        From inserted as i
        Inner Join dbo.TI_SolicitudAprobacion as s on s.IncidenciaNumero = i.IncidenciaNumero and s.Estado = 'P'
        Inner Join deleted as d on d.IncidenciaNumero = i.IncidenciaNumero
        Where d.Estado = 'PA'
    )
    Begin
        Throw 50490, 'El ticket tiene una aprobación pendiente. Responde la aprobación antes de continuar con la atención.', 1
    End
End
Go

/* ============================== NOTIFICACIONES ============================== */

Create Or Alter Procedure dbo.Usp_TI_Registrar_Notificacion
/*================================================================================
Objetivo            : Registrar una notificación interna accionable para un usuario.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Centraliza el registro de avisos sin depender de correo ni servicios externos.
================================================================================*/
    @cUsuario varchar(20),
    @cIncidenciaNumero varchar(12) = Null,
    @cTipo varchar(30),
    @cTitulo nvarchar(120),
    @cMensaje nvarchar(500),
    @cRuta varchar(250) = Null
As
Begin
    Set NoCount On

    If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Estado = 'A') Return

    Insert dbo.TI_Notificacion (Usuario, IncidenciaNumero, Tipo, Titulo, Mensaje, Ruta, Fecha)
    Values (@cUsuario, @cIncidenciaNumero, @cTipo, @cTitulo, @cMensaje, @cRuta, SysDateTime())
End
Go

Create Or Alter Procedure dbo.Usp_TI_Obtener_Notificaciones
/*================================================================================
Objetivo            : Obtener las notificaciones recientes del usuario autenticado y su total pendiente.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : La campana del portal consume información persistida y accionable.
================================================================================*/
    @cUsuario varchar(20)
As
Begin
    Set NoCount On

    Select NoLeidas = Count(*) From dbo.TI_Notificacion Where Usuario = @cUsuario and Leida = 0

    Select Top (20) NotificacionNumero, IncidenciaNumero, Tipo, Titulo, Mensaje, Ruta, Leida, Fecha
    From dbo.TI_Notificacion
    Where Usuario = @cUsuario
    Order By Fecha Desc, NotificacionNumero Desc
End
Go

Create Or Alter Procedure dbo.Usp_TI_Marcar_NotificacionLeida
/*================================================================================
Objetivo            : Marcar como leída una notificación perteneciente al usuario autenticado.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Impide modificar notificaciones de otros usuarios.
================================================================================*/
    @cUsuario varchar(20),
    @nNotificacionNumero bigint
As
Begin
    Set NoCount On

    Update dbo.TI_Notificacion
    Set Leida = 1, FechaLectura = IsNull(FechaLectura, SysDateTime())
    Where NotificacionNumero = @nNotificacionNumero and Usuario = @cUsuario
End
Go

/* ============================== IDENTIDAD CORPORATIVA ============================== */

Create Or Alter Procedure dbo.Usp_TI_Sincronizar_UsuarioCorporativo
/*================================================================================
Objetivo            : Sincronizar únicamente metadata segura de un usuario corporativo en la tabla local.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Nunca recibe ni almacena la contraseña corporativa; conserva área y perfil como autorización local.
================================================================================*/
    @cUsuario varchar(20),
    @cNombreCompleto varchar(255),
    @cCargo char(3) = Null,
    @cDocumento varchar(20) = Null,
    @cEstadoCorporativo varchar(20) = Null,
    @cUsuarioModifica varchar(20) = Null
As
Begin
    Set NoCount On

    If Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario)
    Begin
        Update dbo.TI_Usuario
        Set NombreCompleto = @cNombreCompleto,
            Cargo = Coalesce(@cCargo, Cargo),
            Documento = NullIf(LTrim(RTrim(@cDocumento)), ''),
            FuenteIdentidad = 'SPRING',
            EstadoCorporativo = NullIf(LTrim(RTrim(@cEstadoCorporativo)), ''),
            UltimaSincronizacion = SysDateTime(),
            UltimoUsuario = Coalesce(@cUsuarioModifica, @cUsuario),
            UltimaFechaModif = SysDateTime()
        Where Usuario = @cUsuario
    End
End
Go

Create Or Alter Procedure dbo.Usp_TI_Registrar_UsuarioCorporativo
/*================================================================================
Objetivo            : Registrar o completar el mapeo local de un usuario proveniente del directorio corporativo.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : El origen corporativo provee identidad; área y perfil se asignan localmente por el equipo TI.
================================================================================*/
    @cUsuarioAdmin varchar(20),
    @cUsuario varchar(20),
    @cNombreCompleto varchar(255),
    @cCargo char(3) = Null,
    @cCargoDescripcion varchar(60) = Null,
    @cDocumento varchar(20) = Null,
    @cEstadoCorporativo varchar(20) = Null,
    @cArea char(3),
    @cPerfil char(3),
    @cCorreo varchar(100) = Null,
    @cEstado varchar(2) = 'A'
As
Begin
    Set NoCount On

    If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuarioAdmin and Perfil In ('TEC', 'SUP', 'ADM') and Estado = 'A') Throw 50400, 'No cuenta con permisos para administrar usuarios.', 1
    If Not Exists (Select 1 From dbo.TI_Area Where Area = @cArea and Estado = 'A') Throw 50401, 'El área seleccionada no es válida.', 1
    If Not Exists (Select 1 From dbo.TI_Perfil Where Perfil = @cPerfil and Estado = 'A') Throw 50402, 'El perfil seleccionado no es válido.', 1

    If @cCargo Is Not Null and Not Exists (Select 1 From dbo.TI_Cargo Where Cargo = @cCargo)
        Insert dbo.TI_Cargo (Cargo, Descripcion, Estado, UltimoUsuario, UltimaFechaModif)
        Values (@cCargo, Coalesce(NullIf(@cCargoDescripcion, ''), @cCargo), 'A', @cUsuarioAdmin, SysDateTime())

    If Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario)
    Begin
        Update dbo.TI_Usuario
        Set NombreCompleto = @cNombreCompleto, Area = @cArea, Cargo = @cCargo, Perfil = @cPerfil,
            Correo = NullIf(LTrim(RTrim(@cCorreo)), ''), Estado = @cEstado, Documento = NullIf(LTrim(RTrim(@cDocumento)), ''),
            FuenteIdentidad = 'SPRING', EstadoCorporativo = @cEstadoCorporativo, UltimaSincronizacion = SysDateTime(),
            UltimoUsuario = @cUsuarioAdmin, UltimaFechaModif = SysDateTime()
        Where Usuario = @cUsuario
    End
    Else
    Begin
        Insert dbo.TI_Usuario (Usuario, NombreCompleto, Clave, Area, Cargo, Perfil, Correo, Estado, UltimoUsuario, UltimaFechaModif, TipoUsuario, Documento, FuenteIdentidad, EstadoCorporativo, UltimaSincronizacion)
        Values (@cUsuario, @cNombreCompleto, 'IDENTIDAD_CORPORATIVA', @cArea, @cCargo, @cPerfil, NullIf(LTrim(RTrim(@cCorreo)), ''), @cEstado, @cUsuarioAdmin, SysDateTime(), 'INTERNO', NullIf(LTrim(RTrim(@cDocumento)), ''), 'SPRING', @cEstadoCorporativo, SysDateTime())
    End
End
Go

Create Or Alter Procedure dbo.Usp_TI_Sincronizar_CargoCorporativo
/*================================================================================
Objetivo            : Insertar o actualizar un cargo proveniente del catálogo corporativo.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Mantiene el maestro local de cargos alineado sin eliminar referencias históricas.
================================================================================*/
    @cUsuarioAdmin varchar(20),
    @cCargo char(3),
    @cDescripcion varchar(60)
As
Begin
    Set NoCount On

    If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuarioAdmin and Perfil In ('TEC', 'SUP', 'ADM') and Estado = 'A') Throw 50403, 'No cuenta con permisos para sincronizar cargos.', 1

    If Exists (Select 1 From dbo.TI_Cargo Where Cargo = @cCargo)
        Update dbo.TI_Cargo Set Descripcion = @cDescripcion, Estado = 'A', UltimoUsuario = @cUsuarioAdmin, UltimaFechaModif = SysDateTime() Where Cargo = @cCargo
    Else
        Insert dbo.TI_Cargo (Cargo, Descripcion, Estado, UltimoUsuario, UltimaFechaModif) Values (@cCargo, @cDescripcion, 'A', @cUsuarioAdmin, SysDateTime())
End
Go

/* ============================== CONFIGURACIÓN TI ============================== */

Create Or Alter Procedure dbo.Usp_TI_Obtener_ConfiguracionTI
/*================================================================================
Objetivo            : Obtener en una sola llamada los catálogos y reglas administrables por el equipo TI.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Evita múltiples llamadas y concentra únicamente configuración funcional indispensable.
================================================================================*/
    @cUsuario varchar(20)
As
Begin
    Set NoCount On

    If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Perfil In ('TEC', 'SUP', 'ADM') and Estado = 'A') Throw 50404, 'No cuenta con permisos para acceder a Maestros TI.', 1

    Select Area, Descripcion, Estado, Telefono From dbo.TI_Area Order By Descripcion
    Select Linea, Area, Descripcion, Estado From dbo.TI_Linea Order By Area, Descripcion
    Select Item, Linea, Descripcion, Estado From dbo.TI_Item Order By Linea, Descripcion
    Select Tipo, Descripcion, Abreviatura, Estado From dbo.TI_Tipo Order By Descripcion
    Select Categoria, Descripcion, Abreviatura, Estado From dbo.TI_Categoria Order By Descripcion
    Select Tipo, SubTipo, Categoria, Descripcion, Abreviatura, Estado From dbo.TI_SubTipo Order By Tipo, Categoria, Descripcion
    Select Item, Categoria, Prioridad = Convert(int, Prioridad), Impacto = Convert(int, Impacto), Complejidad = Convert(int, Complejidad), Estado From dbo.TI_ItemCategoria Order By Item, Categoria
    Select Prioridad, SlaObjetivoMinutos, Estado From dbo.TI_ParametroSLA Order By Prioridad Desc
    Select Usuario, NombreCompleto, Area, Cargo, Perfil, Correo, Estado, FuenteIdentidad, Documento, EstadoCorporativo, UltimaSincronizacion From dbo.TI_Usuario Order By NombreCompleto
    Select FormatoCodigo, Titulo, Descripcion, NombreOriginal, TipoMime, TipoTicket, Estado From dbo.TI_FormatoSoporte Order By Titulo
    Select ConocimientoCodigo, Titulo, Estado, VisibleUsuario From dbo.TI_BaseConocimiento Order By Titulo
End
Go

Create Or Alter Procedure dbo.Usp_TI_Guardar_Area
    @cUsuario varchar(20), @cArea char(3), @cDescripcion varchar(60), @cTelefono varchar(20) = Null, @cEstado varchar(2)
As
Begin
    Set NoCount On
    If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Perfil In ('TEC','SUP','ADM') and Estado = 'A') Throw 50405, 'No cuenta con permisos para administrar áreas.', 1
    If Exists (Select 1 From dbo.TI_Area Where Area = @cArea)
        Update dbo.TI_Area Set Descripcion = @cDescripcion, Telefono = NullIf(@cTelefono, ''), Estado = @cEstado, UltimoUsuario = @cUsuario, UltimaFechaModif = SysDateTime() Where Area = @cArea
    Else
        Insert dbo.TI_Area (Area, Descripcion, Estado, Telefono, UltimoUsuario, UltimaFechaModif) Values (@cArea, @cDescripcion, @cEstado, NullIf(@cTelefono, ''), @cUsuario, SysDateTime())
End
Go

Create Or Alter Procedure dbo.Usp_TI_Guardar_Linea
    @cUsuario varchar(20), @cLinea char(3), @cArea char(3), @cDescripcion varchar(60), @cEstado varchar(2)
As
Begin
    Set NoCount On
    If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Perfil In ('TEC','SUP','ADM') and Estado = 'A') Throw 50406, 'No cuenta con permisos para administrar líneas.', 1
    If Not Exists (Select 1 From dbo.TI_Area Where Area = @cArea) Throw 50407, 'El área indicada no existe.', 1
    If Exists (Select 1 From dbo.TI_Linea Where Linea = @cLinea)
        Update dbo.TI_Linea Set Area = @cArea, Descripcion = @cDescripcion, Estado = @cEstado, UltimoUsuario = @cUsuario, UltimaFechaModif = SysDateTime() Where Linea = @cLinea
    Else
        Insert dbo.TI_Linea (Linea, Area, Descripcion, Estado, UltimoUsuario, UltimaFechaModif) Values (@cLinea, @cArea, @cDescripcion, @cEstado, @cUsuario, SysDateTime())
End
Go

Create Or Alter Procedure dbo.Usp_TI_Guardar_Item
    @cUsuario varchar(20), @cItem varchar(20), @cLinea char(3), @cDescripcion varchar(255), @cEstado varchar(2)
As
Begin
    Set NoCount On
    If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Perfil In ('TEC','SUP','ADM') and Estado = 'A') Throw 50408, 'No cuenta con permisos para administrar ítems.', 1
    If Not Exists (Select 1 From dbo.TI_Linea Where Linea = @cLinea) Throw 50409, 'La línea indicada no existe.', 1
    If Exists (Select 1 From dbo.TI_Item Where Item = @cItem)
        Update dbo.TI_Item Set Linea = @cLinea, Descripcion = @cDescripcion, Estado = @cEstado, UltimoUsuario = @cUsuario, UltimaFechaModif = SysDateTime() Where Item = @cItem
    Else
        Insert dbo.TI_Item (Item, Linea, Descripcion, Estado, UltimoUsuario, UltimaFechaModif) Values (@cItem, @cLinea, @cDescripcion, @cEstado, @cUsuario, SysDateTime())
End
Go

Create Or Alter Procedure dbo.Usp_TI_Guardar_Tipo
    @cUsuario varchar(20), @cTipo char(3), @cDescripcion varchar(60), @cAbreviatura varchar(60) = Null, @cEstado varchar(2)
As
Begin
    Set NoCount On
    If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Perfil In ('TEC','SUP','ADM') and Estado = 'A') Throw 50410, 'No cuenta con permisos para administrar tipos.', 1
    If Exists (Select 1 From dbo.TI_Tipo Where Tipo = @cTipo)
        Update dbo.TI_Tipo Set Descripcion = @cDescripcion, Abreviatura = NullIf(@cAbreviatura, ''), Estado = @cEstado, UltimoUsuario = @cUsuario, UltimaFechaModif = SysDateTime() Where Tipo = @cTipo
    Else
        Insert dbo.TI_Tipo (Tipo, Descripcion, Abreviatura, Estado, UltimoUsuario, UltimaFechaModif) Values (@cTipo, @cDescripcion, NullIf(@cAbreviatura, ''), @cEstado, @cUsuario, SysDateTime())
End
Go

Create Or Alter Procedure dbo.Usp_TI_Guardar_Categoria
    @cUsuario varchar(20), @cCategoria varchar(20), @cDescripcion varchar(60), @cAbreviatura varchar(3) = Null, @cEstado varchar(2)
As
Begin
    Set NoCount On
    If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Perfil In ('TEC','SUP','ADM') and Estado = 'A') Throw 50411, 'No cuenta con permisos para administrar categorías.', 1
    If Exists (Select 1 From dbo.TI_Categoria Where Categoria = @cCategoria)
        Update dbo.TI_Categoria Set Descripcion = @cDescripcion, Abreviatura = NullIf(@cAbreviatura, ''), Estado = @cEstado, UltimoUsuario = @cUsuario, UltimaFechaModif = SysDateTime() Where Categoria = @cCategoria
    Else
        Insert dbo.TI_Categoria (Categoria, Descripcion, Abreviatura, Estado, UltimoUsuario, UltimaFechaModif) Values (@cCategoria, @cDescripcion, NullIf(@cAbreviatura, ''), @cEstado, @cUsuario, SysDateTime())
End
Go

Create Or Alter Procedure dbo.Usp_TI_Guardar_SubTipo
    @cUsuario varchar(20), @cTipo char(3), @cSubTipo char(3), @cCategoria varchar(20), @cDescripcion varchar(60), @cAbreviatura varchar(60) = Null, @cEstado varchar(2)
As
Begin
    Set NoCount On
    If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Perfil In ('TEC','SUP','ADM') and Estado = 'A') Throw 50412, 'No cuenta con permisos para administrar subtipos.', 1
    If Not Exists (Select 1 From dbo.TI_Tipo Where Tipo = @cTipo) or Not Exists (Select 1 From dbo.TI_Categoria Where Categoria = @cCategoria) Throw 50413, 'La combinación tipo/categoría no es válida.', 1
    If Exists (Select 1 From dbo.TI_SubTipo Where Tipo = @cTipo and SubTipo = @cSubTipo and Categoria = @cCategoria)
        Update dbo.TI_SubTipo Set Descripcion = @cDescripcion, Abreviatura = NullIf(@cAbreviatura, ''), Estado = @cEstado, UltimoUsuario = @cUsuario, UltimaFechaModif = SysDateTime() Where Tipo = @cTipo and SubTipo = @cSubTipo and Categoria = @cCategoria
    Else
        Insert dbo.TI_SubTipo (Tipo, SubTipo, Categoria, Descripcion, Abreviatura, Estado, UltimoUsuario, UltimaFechaModif) Values (@cTipo, @cSubTipo, @cCategoria, @cDescripcion, NullIf(@cAbreviatura, ''), @cEstado, @cUsuario, SysDateTime())
End
Go

Create Or Alter Procedure dbo.Usp_TI_Guardar_MatrizClasificacion
    @cUsuario varchar(20), @cItem varchar(20), @cCategoria varchar(20), @nPrioridad int, @nImpacto int, @nComplejidad int, @cEstado varchar(2)
As
Begin
    Set NoCount On
    If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Perfil In ('TEC','SUP','ADM') and Estado = 'A') Throw 50414, 'No cuenta con permisos para administrar la matriz.', 1
    If @nPrioridad Not Between 1 and 5 or @nImpacto Not Between 1 and 5 or @nComplejidad Not Between 1 and 5 Throw 50415, 'Prioridad, impacto y complejidad deben estar entre 1 y 5.', 1
    If Exists (Select 1 From dbo.TI_ItemCategoria Where Item = @cItem and Categoria = @cCategoria)
        Update dbo.TI_ItemCategoria Set Prioridad = @nPrioridad, Impacto = @nImpacto, Complejidad = @nComplejidad, Estado = @cEstado, UltimoUsuario = @cUsuario, UltimaFechaModif = SysDateTime() Where Item = @cItem and Categoria = @cCategoria
    Else
        Insert dbo.TI_ItemCategoria (Item, Categoria, Prioridad, Impacto, Complejidad, Estado, UltimoUsuario, UltimaFechaModif) Values (@cItem, @cCategoria, @nPrioridad, @nImpacto, @nComplejidad, @cEstado, @cUsuario, SysDateTime())
End
Go

Create Or Alter Procedure dbo.Usp_TI_Guardar_ParametroSLA
    @cUsuario varchar(20), @nPrioridad tinyint, @nSlaObjetivoMinutos int, @cEstado varchar(2)
As
Begin
    Set NoCount On
    If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Perfil In ('TEC','SUP','ADM') and Estado = 'A') Throw 50416, 'No cuenta con permisos para administrar SLA.', 1
    If @nPrioridad Not Between 1 and 5 or @nSlaObjetivoMinutos <= 0 Throw 50417, 'La prioridad o el tiempo objetivo no son válidos.', 1
    If Exists (Select 1 From dbo.TI_ParametroSLA Where Prioridad = @nPrioridad)
        Update dbo.TI_ParametroSLA Set SlaObjetivoMinutos = @nSlaObjetivoMinutos, Estado = @cEstado, UltimoUsuario = @cUsuario, UltimaFechaModif = SysDateTime() Where Prioridad = @nPrioridad
    Else
        Insert dbo.TI_ParametroSLA (Prioridad, SlaObjetivoMinutos, Estado, UltimoUsuario, UltimaFechaModif) Values (@nPrioridad, @nSlaObjetivoMinutos, @cEstado, @cUsuario, SysDateTime())
End
Go

Create Or Alter Procedure dbo.Usp_TI_Guardar_FormatoSoporte
    @cUsuario varchar(20), @cFormatoCodigo varchar(20), @cTitulo nvarchar(120), @cDescripcion nvarchar(500) = Null,
    @cNombreOriginal nvarchar(260), @cRutaArchivo nvarchar(1000), @cTipoMime varchar(100), @cTipoTicket char(3) = Null, @cEstado varchar(2)
As
Begin
    Set NoCount On
    If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Perfil In ('TEC','SUP','ADM') and Estado = 'A') Throw 50418, 'No cuenta con permisos para administrar formatos.', 1
    If @cTipoTicket Is Not Null and Not Exists (Select 1 From dbo.TI_Tipo Where Tipo = @cTipoTicket) Throw 50419, 'El tipo de ticket indicado no existe.', 1
    If Exists (Select 1 From dbo.TI_FormatoSoporte Where FormatoCodigo = @cFormatoCodigo)
        Update dbo.TI_FormatoSoporte Set Titulo = @cTitulo, Descripcion = NullIf(@cDescripcion, ''), NombreOriginal = @cNombreOriginal, RutaArchivo = @cRutaArchivo, TipoMime = @cTipoMime, TipoTicket = @cTipoTicket, Estado = @cEstado, UltimoUsuario = @cUsuario, UltimaFechaModif = SysDateTime() Where FormatoCodigo = @cFormatoCodigo
    Else
        Insert dbo.TI_FormatoSoporte (FormatoCodigo, Titulo, Descripcion, NombreOriginal, RutaArchivo, TipoMime, TipoTicket, Estado, UltimoUsuario, UltimaFechaModif) Values (@cFormatoCodigo, @cTitulo, NullIf(@cDescripcion, ''), @cNombreOriginal, @cRutaArchivo, @cTipoMime, @cTipoTicket, @cEstado, @cUsuario, SysDateTime())
End
Go

Create Or Alter Procedure dbo.Usp_TI_Actualizar_VisibilidadConocimiento
    @cUsuario varchar(20), @cConocimientoCodigo varchar(20), @lVisibleUsuario bit
As
Begin
    Set NoCount On
    If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Perfil In ('TEC','SUP','ADM') and Estado = 'A') Throw 50420, 'No cuenta con permisos para publicar conocimiento.', 1
    Update dbo.TI_BaseConocimiento Set VisibleUsuario = @lVisibleUsuario Where ConocimientoCodigo = @cConocimientoCodigo
End
Go

/* ============================== MATRIZ Y GESTIÓN DE TICKETS ============================== */

Create Or Alter Procedure dbo.Usp_TI_Clasificar_Ticket
/*================================================================================
Objetivo            : Clasificar una incidencia aplicando la matriz configurada de forma obligatoria.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : dbo.Usp_TI_Clasificar_Ticket
Comentario Cambios  : Prioridad, impacto y complejidad dejan de depender de selección manual y se obtienen de TI_ItemCategoria.
================================================================================*/
    @cUsuario varchar(20), @cArea char(3), @cIncidenciaNumero varchar(12), @cLinea char(3), @cItem varchar(20),
    @cTipo char(3), @cSubTipo char(3), @cCategoria varchar(20), @cAreaCausante char(3) = Null,
    @nPrioridad int = Null, @nImpacto int = Null, @nComplejidad int = Null, @cIdCorrelacion uniqueidentifier
As
Begin
    Set NoCount On

    Declare @nPrioridadMatriz int, @nImpactoMatriz int, @nComplejidadMatriz int, @nSla int

    If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM')) Throw 50203, 'El operador TI no es válido.', 1
    If Not Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero and Estado Not In ('RS','CA','PA')) Throw 50204, 'El ticket no existe, está cerrado o espera una aprobación.', 1
    If Not Exists (Select 1 From dbo.TI_Item Where Item = @cItem and Linea = @cLinea and Estado = 'A') Throw 50205, 'El item no corresponde a la línea seleccionada.', 1
    If Not Exists (Select 1 From dbo.TI_SubTipo Where Tipo = @cTipo and SubTipo = @cSubTipo and Categoria = @cCategoria and Estado = 'A') Throw 50206, 'La combinación de tipo, subtipo y categoría no es válida.', 1
    If @cAreaCausante Is Not Null and Not Exists (Select 1 From dbo.TI_Area Where Area = @cAreaCausante and Estado = 'A') Throw 50208, 'El área causante no es válida.', 1

    Select @nPrioridadMatriz = Convert(int, Prioridad), @nImpactoMatriz = Convert(int, Impacto), @nComplejidadMatriz = Convert(int, Complejidad)
    From dbo.TI_ItemCategoria
    Where Item = @cItem and Categoria = @cCategoria and Estado = 'A'

    If @nPrioridadMatriz Is Null or @nImpactoMatriz Is Null or @nComplejidadMatriz Is Null
        Throw 50207, 'La matriz Item/Categoría no está configurada completamente. Configúrala antes de clasificar el ticket.', 1

    Select @nSla = SlaObjetivoMinutos From dbo.TI_ParametroSLA Where Prioridad = @nPrioridadMatriz and Estado = 'A'

    Update dbo.TI_Incidencia
    Set AreaTI = @cArea, Linea = @cLinea, Item = @cItem, Tipo = @cTipo, SubTipo = @cSubTipo, Categoria = @cCategoria,
        AreaCausante = @cAreaCausante, Prioridad = @nPrioridadMatriz, Impacto = @nImpactoMatriz, Complejidad = @nComplejidadMatriz,
        SlaObjetivoMinutos = @nSla, UltimoUsuario = @cUsuario, UltimaFechaModif = SysDateTime()
    Where IncidenciaNumero = @cIncidenciaNumero

    Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
    Values (@cIncidenciaNumero, @cUsuario, 'T', 'TI_Incidencia', @cIncidenciaNumero, 'CLASIFICAR_TICKET', 'EXITOSO', Concat('{"prioridad":', @nPrioridadMatriz, ',"impacto":', @nImpactoMatriz, ',"complejidad":', @nComplejidadMatriz, '}'), @cIdCorrelacion, SysDateTime())
End
Go

Create Or Alter Procedure dbo.Usp_TI_Registrar_AvanceTicket
/*================================================================================
Objetivo            : Registrar un avance técnico con esfuerzo efectivo y área causante obligatorios.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : dbo.Usp_TI_Registrar_AvanceTicket
Comentario Cambios  : El avance almacena minutos reales y causa estructurada; no usa porcentaje subjetivo.
================================================================================*/
    @cUsuario varchar(20), @cArea char(3), @cIncidenciaNumero varchar(12), @cDetalle nvarchar(max), @lVisibleUsuario bit = 0,
    @nTiempoUtilizado decimal(8,2), @cAreaCausante char(3), @cIdCorrelacion uniqueidentifier
As
Begin
    Set NoCount On
    Set Xact_Abort On

    Begin Try
        Begin Transaction

        Declare @nAvance int, @nMensaje int, @nEstado int, @cEstado char(2), @dFecha datetime2(0) = SysDateTime()

        If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM')) Throw 50216, 'El operador TI no es válido.', 1
        If NullIf(LTrim(RTrim(@cDetalle)), '') Is Null Throw 50217, 'El detalle del avance no puede estar vacío.', 1
        If @nTiempoUtilizado Is Null or @nTiempoUtilizado <= 0 or @nTiempoUtilizado > 1440 Throw 50234, 'El tiempo efectivo debe ser mayor a 0 y no superar 1440 minutos por avance.', 1
        If Not Exists (Select 1 From dbo.TI_Area Where Area = @cAreaCausante and Estado = 'A') Throw 50235, 'Selecciona un área causante válida.', 1

        Select @cEstado = Estado From dbo.TI_Incidencia With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero
        If @cEstado Is Null or @cEstado In ('RS','CA','PA') Throw 50218, 'El ticket no existe, está cerrado o espera una aprobación.', 1

        Select @nAvance = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_IncidenciaAvance With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero
        Insert dbo.TI_IncidenciaAvance (IncidenciaNumero, Secuencia, UsuarioTI, FechaAvance, Detalle, TiempoUtilizado, PorcentajeAvance, AreaCausante)
        Values (@cIncidenciaNumero, @nAvance, @cUsuario, @dFecha, LTrim(RTrim(@cDetalle)), @nTiempoUtilizado, Null, @cAreaCausante)

        If @lVisibleUsuario = 1
        Begin
            Select @nMensaje = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_IncidenciaMensaje With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero
            Insert dbo.TI_IncidenciaMensaje (IncidenciaNumero, Secuencia, UsuarioAutor, TipoAutor, Contenido, FechaMensaje, EsInterno)
            Values (@cIncidenciaNumero, @nMensaje, @cUsuario, 'T', LTrim(RTrim(@cDetalle)), @dFecha, 0)
        End

        Update dbo.TI_Incidencia
        Set AreaCausante = @cAreaCausante, FechaAtencion = IsNull(FechaAtencion, @dFecha),
            Estado = Case When @cEstado In ('NV','RA','ES') Then 'DG' Else Estado End,
            UltimoUsuario = @cUsuario, UltimaFechaModif = @dFecha
        Where IncidenciaNumero = @cIncidenciaNumero

        If @cEstado In ('NV','RA','ES')
        Begin
            Select @nEstado = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_IncidenciaEstado With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero
            Insert dbo.TI_IncidenciaEstado (IncidenciaNumero, Secuencia, Estado, UsuarioCambio, FechaCambio, Observacion)
            Values (@cIncidenciaNumero, @nEstado, 'DG', @cUsuario, @dFecha, N'Se inició o retomó la atención técnica del ticket.')
        End

        Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
        Values (@cIncidenciaNumero, @cUsuario, 'T', 'TI_IncidenciaAvance', Concat(@cIncidenciaNumero, '-', @nAvance), 'REGISTRAR_AVANCE', 'EXITOSO', Concat('{"minutos":', Convert(varchar(30), @nTiempoUtilizado), ',"areaCausante":"', @cAreaCausante, '"}'), @cIdCorrelacion, @dFecha)

        Commit Transaction
    End Try
    Begin Catch
        If Xact_State() <> 0 Rollback Transaction
        ;Throw
    End Catch
End
Go

Create Or Alter Procedure dbo.Usp_TI_Solicitar_AprobacionTicket
/*================================================================================
Objetivo            : Crear una solicitud de aprobación manual y bloquear el flujo del ticket hasta su respuesta.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : La aprobación es funcional y no ejecuta acciones automáticas; solo controla continuidad del caso.
================================================================================*/
    @cUsuario varchar(20), @cArea char(3), @cIncidenciaNumero varchar(12), @cAccionCodigo varchar(50), @cJustificacion nvarchar(1000), @cIdCorrelacion uniqueidentifier
As
Begin
    Set NoCount On
    Set Xact_Abort On

    Begin Try
        Begin Transaction
        Declare @nSecuencia int, @nEstado int, @dFecha datetime2(0) = SysDateTime()

        If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM')) Throw 50236, 'El operador TI no es válido.', 1
        If Not Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero and Estado Not In ('RS','CA','PV','PA')) Throw 50237, 'El ticket no admite una solicitud de aprobación en su estado actual.', 1
        If Not Exists (Select 1 From dbo.TI_Accion Where AccionCodigo = @cAccionCodigo and Estado = 'A' and RequiereAprobacion = 1) Throw 50238, 'Selecciona una acción activa que requiera aprobación.', 1
        If NullIf(LTrim(RTrim(@cJustificacion)), '') Is Null Throw 50239, 'La justificación de la aprobación es obligatoria.', 1
        If Exists (Select 1 From dbo.TI_SolicitudAprobacion Where IncidenciaNumero = @cIncidenciaNumero and Estado = 'P') Throw 50240, 'El ticket ya tiene una aprobación pendiente.', 1

        Update dbo.TI_Incidencia Set Estado = 'PA', UltimoUsuario = @cUsuario, UltimaFechaModif = @dFecha Where IncidenciaNumero = @cIncidenciaNumero

        Select @nSecuencia = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_SolicitudAprobacion With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero
        Insert dbo.TI_SolicitudAprobacion (IncidenciaNumero, Secuencia, AccionCodigo, UsuarioSolicitante, Estado, Justificacion, FechaSolicitud)
        Values (@cIncidenciaNumero, @nSecuencia, @cAccionCodigo, @cUsuario, 'P', LTrim(RTrim(@cJustificacion)), @dFecha)

        Select @nEstado = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_IncidenciaEstado With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero
        Insert dbo.TI_IncidenciaEstado (IncidenciaNumero, Secuencia, Estado, UsuarioCambio, FechaCambio, Observacion)
        Values (@cIncidenciaNumero, @nEstado, 'PA', @cUsuario, @dFecha, N'La atención quedó bloqueada hasta responder la aprobación solicitada.')

        Insert dbo.TI_Notificacion (Usuario, IncidenciaNumero, Tipo, Titulo, Mensaje, Ruta, Fecha)
        Select u.Usuario, @cIncidenciaNumero, 'APROBACION', N'Aprobación pendiente', Concat(N'Se requiere revisar la aprobación del ticket ', @cIncidenciaNumero, N'.'), '/gestion-tickets', @dFecha
        From dbo.TI_Usuario as u
        Where u.Estado = 'A' and u.Perfil In ('TEC','SUP','ADM') and u.Usuario <> @cUsuario

        Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
        Values (@cIncidenciaNumero, @cUsuario, 'T', 'TI_SolicitudAprobacion', Concat(@cIncidenciaNumero, '-', @nSecuencia), 'SOLICITAR_APROBACION', 'PENDIENTE', Concat('{"accion":"', @cAccionCodigo, '"}'), @cIdCorrelacion, @dFecha)

        Commit Transaction
    End Try
    Begin Catch
        If Xact_State() <> 0 Rollback Transaction
        ;Throw
    End Catch
End
Go

Create Or Alter Procedure dbo.Usp_TI_Responder_AprobacionTicket
/*================================================================================
Objetivo            : Aprobar o rechazar una solicitud pendiente y desbloquear el flujo operativo.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : dbo.Usp_TI_Responder_AprobacionTicket
Comentario Cambios  : La aprobación deja de ser informativa; mientras está pendiente el ticket permanece bloqueado.
================================================================================*/
    @cUsuario varchar(20), @cArea char(3), @cIncidenciaNumero varchar(12), @nSecuencia int, @lAprobar bit, @cComentario nvarchar(1000), @cIdCorrelacion uniqueidentifier
As
Begin
    Set NoCount On
    Set Xact_Abort On

    Begin Try
        Begin Transaction
        Declare @nEstado int, @cSolicitante varchar(20), @cMensajeNotificacion nvarchar(500), @dFecha datetime2(0) = SysDateTime()

        If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Estado = 'A' and Perfil In ('TEC','SUP','ADM')) Throw 50230, 'Solo un operador TI activo puede responder esta aprobación.', 1
        Select @cSolicitante = UsuarioSolicitante From dbo.TI_SolicitudAprobacion Where IncidenciaNumero = @cIncidenciaNumero and Secuencia = @nSecuencia and Estado = 'P'
        If @cSolicitante Is Null Throw 50231, 'La solicitud de aprobación ya no se encuentra pendiente.', 1
        If @lAprobar = 0 and NullIf(LTrim(RTrim(@cComentario)), '') Is Null Throw 50232, 'Indica el motivo del rechazo.', 1

        Update dbo.TI_SolicitudAprobacion
        Set Estado = Case When @lAprobar = 1 Then 'A' Else 'R' End, UsuarioAprobador = @cUsuario,
            ComentarioRespuesta = NullIf(LTrim(RTrim(@cComentario)), ''), FechaRespuesta = @dFecha
        Where IncidenciaNumero = @cIncidenciaNumero and Secuencia = @nSecuencia and Estado = 'P'

        Update dbo.TI_Incidencia Set Estado = 'DG', UltimoUsuario = @cUsuario, UltimaFechaModif = @dFecha Where IncidenciaNumero = @cIncidenciaNumero and Estado = 'PA'
        Select @nEstado = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_IncidenciaEstado With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero
        Insert dbo.TI_IncidenciaEstado (IncidenciaNumero, Secuencia, Estado, UsuarioCambio, FechaCambio, Observacion)
        Values (@cIncidenciaNumero, @nEstado, 'DG', @cUsuario, @dFecha, Case When @lAprobar = 1 Then N'La aprobación fue concedida; el ticket puede continuar.' Else N'La aprobación fue rechazada; el ticket vuelve a diagnóstico.' End)

        Set @cMensajeNotificacion = Case When @lAprobar = 1 Then N'La solicitud fue aprobada y el ticket puede continuar.' Else N'La solicitud fue rechazada. Revisa el comentario registrado.' End
        Exec dbo.Usp_TI_Registrar_Notificacion
            @cUsuario = @cSolicitante,
            @cIncidenciaNumero = @cIncidenciaNumero,
            @cTipo = 'APROBACION_RESPUESTA',
            @cTitulo = N'Respuesta de aprobación',
            @cMensaje = @cMensajeNotificacion,
            @cRuta = '/gestion-tickets'

        Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
        Values (@cIncidenciaNumero, @cUsuario, 'T', 'TI_SolicitudAprobacion', Concat(@cIncidenciaNumero, '-', @nSecuencia), Case When @lAprobar = 1 Then 'APROBAR_ACCION' Else 'RECHAZAR_ACCION' End, 'EXITOSO', Null, @cIdCorrelacion, @dFecha)

        Commit Transaction
    End Try
    Begin Catch
        If Xact_State() <> 0 Rollback Transaction
        ;Throw
    End Catch
End
Go

/* ============================== USUARIO: EDICIÓN Y RECURSOS ============================== */

Create Or Alter Procedure dbo.Usp_TI_Editar_TicketUsuario
/*================================================================================
Objetivo            : Permitir corregir datos básicos del ticket propio antes de que la atención técnica avance.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Solo admite estados NV/RC y nunca expone clasificación técnica, prioridad ni responsable.
================================================================================*/
    @cUsuario varchar(20), @cIncidenciaNumero varchar(12), @cLinea char(3), @cTipo char(3), @cTitulo nvarchar(250), @cDetalle nvarchar(max), @cMensajeError nvarchar(1000) = Null, @cIdCorrelacion uniqueidentifier
As
Begin
    Set NoCount On

    If Not Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero and UsuarioSolicitante = @cUsuario and Estado In ('NV','RC')) Throw 50430, 'El ticket ya fue tomado por TI o no pertenece al usuario autenticado.', 1
    If Not Exists (Select 1 From dbo.TI_Linea Where Linea = @cLinea and Estado = 'A') Throw 50431, 'La línea seleccionada no es válida.', 1
    If Not Exists (Select 1 From dbo.TI_Tipo Where Tipo = @cTipo and Estado = 'A') Throw 50432, 'El tipo seleccionado no es válido.', 1
    If @cTipo = 'REQ' and Not Exists (Select 1 From dbo.TI_IncidenciaAdjunto Where IncidenciaNumero = @cIncidenciaNumero) Throw 50433, 'Un requerimiento debe conservar al menos un archivo de sustento.', 1

    Update dbo.TI_Incidencia
    Set Linea = @cLinea, Tipo = @cTipo, Titulo = LTrim(RTrim(@cTitulo)), Detalle = LTrim(RTrim(@cDetalle)), MensajeError = NullIf(LTrim(RTrim(@cMensajeError)), ''),
        UltimoUsuario = @cUsuario, UltimaFechaModif = SysDateTime()
    Where IncidenciaNumero = @cIncidenciaNumero and UsuarioSolicitante = @cUsuario

    Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
    Values (@cIncidenciaNumero, @cUsuario, 'U', 'TI_Incidencia', @cIncidenciaNumero, 'EDITAR_TICKET_PREVIO', 'EXITOSO', Null, @cIdCorrelacion, SysDateTime())
End
Go

Create Or Alter Procedure dbo.Usp_TI_Obtener_RecursosSoporteUsuario
/*================================================================================
Objetivo            : Entregar formatos y conocimiento publicado como autoservicio estático previo a la IA.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Evita crear módulos separados de tutoriales o formatos.
================================================================================*/
As
Begin
    Set NoCount On

    Select FormatoCodigo, Titulo, Descripcion, NombreOriginal, TipoMime, TipoTicket
    From dbo.TI_FormatoSoporte
    Where Estado = 'A'
    Order By Titulo

    Select ConocimientoCodigo, Titulo, Problema, Sintomas, Solucion, Procedimiento, Tipo
    From dbo.TI_BaseConocimiento
    Where Estado = 'A' and VisibleUsuario = 1
    Order By Titulo
End
Go

Create Or Alter Procedure dbo.Usp_TI_Obtener_FormatoSoporteUsuario
    @cFormatoCodigo varchar(20)
As
Begin
    Set NoCount On
    Select NombreOriginal, RutaArchivo, TipoMime From dbo.TI_FormatoSoporte Where FormatoCodigo = @cFormatoCodigo and Estado = 'A'
End
Go

/* ============================== MESA DE AYUDA ============================== */

Create Or Alter Procedure dbo.Usp_TI_Crear_TicketPorUsuario
/*================================================================================
Objetivo            : Registrar desde TI un ticket a nombre de otro usuario cuando la mesa de ayuda recibe el caso por otro canal.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Distingue UsuarioSolicitante de UsuarioRegistro y mantiene el mismo flujo funcional del portal.
================================================================================*/
    @cUsuarioTI varchar(20), @cAreaTI char(3), @cUsuarioSolicitante varchar(20), @cLinea char(3), @cTipo char(3),
    @cTitulo nvarchar(250), @cDetalle nvarchar(max), @cMensajeError nvarchar(1000) = Null, @cIdCorrelacion uniqueidentifier
As
Begin
    Set NoCount On
    Set Xact_Abort On

    Declare @cAreaSolicitante char(3), @cIncidenciaNumero varchar(12), @cMensajeNotificacion nvarchar(500), @nCorrelativo int, @dFecha datetime2(0) = SysDateTime()

    If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuarioTI and Area = @cAreaTI and Estado = 'A' and Perfil In ('TEC','SUP','ADM')) Throw 50440, 'El operador TI no es válido.', 1
    Select @cAreaSolicitante = Area From dbo.TI_Usuario Where Usuario = @cUsuarioSolicitante and Estado = 'A'
    If @cAreaSolicitante Is Null Throw 50441, 'El usuario solicitante no se encuentra activo.', 1
    If Not Exists (Select 1 From dbo.TI_Linea Where Linea = @cLinea and Estado = 'A') Throw 50442, 'La línea seleccionada no es válida.', 1
    If Not Exists (Select 1 From dbo.TI_Tipo Where Tipo = @cTipo and Estado = 'A') Throw 50443, 'El tipo seleccionado no es válido.', 1

    Begin Try
        Begin Transaction
        Select @nCorrelativo = IsNull(Max(Try_Convert(int, Right(IncidenciaNumero, 6))), 0) + 1 From dbo.TI_Incidencia With (UpdLock, HoldLock) Where IncidenciaNumero Like 'INC-[0-9][0-9][0-9][0-9][0-9][0-9]'
        If @nCorrelativo > 999999 Throw 50444, 'Se alcanzó el límite del correlativo de incidencias.', 1
        Set @cIncidenciaNumero = 'INC-' + Right('000000' + Convert(varchar(6), @nCorrelativo), 6)

        Insert dbo.TI_Incidencia (IncidenciaNumero, FechaRegistro, UsuarioSolicitante, UsuarioRegistro, AreaSolicitante, Linea, Tipo, Estado, Titulo, Detalle, MensajeError, CanalRegistro, UltimoUsuario, UltimaFechaModif)
        Values (@cIncidenciaNumero, @dFecha, @cUsuarioSolicitante, @cUsuarioTI, @cAreaSolicitante, @cLinea, @cTipo, 'NV', LTrim(RTrim(@cTitulo)), LTrim(RTrim(@cDetalle)), NullIf(LTrim(RTrim(@cMensajeError)), ''), 'MESA_AYUDA', @cUsuarioTI, @dFecha)

        Insert dbo.TI_IncidenciaEstado (IncidenciaNumero, Secuencia, Estado, UsuarioCambio, FechaCambio, Observacion)
        Values (@cIncidenciaNumero, 1, 'NV', @cUsuarioTI, @dFecha, N'Ticket registrado por mesa de ayuda a nombre del usuario solicitante.')

        Set @cMensajeNotificacion = Concat(N'TI registró el ticket ', @cIncidenciaNumero, N' a tu nombre.')
        Exec dbo.Usp_TI_Registrar_Notificacion
            @cUsuario = @cUsuarioSolicitante,
            @cIncidenciaNumero = @cIncidenciaNumero,
            @cTipo = 'TICKET_CREADO',
            @cTitulo = N'Ticket registrado por TI',
            @cMensaje = @cMensajeNotificacion,
            @cRuta = '/mis-tickets'

        Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
        Values (@cIncidenciaNumero, @cUsuarioTI, 'T', 'TI_Incidencia', @cIncidenciaNumero, 'CREAR_TICKET_POR_USUARIO', 'EXITOSO', Concat('{"solicitante":"', @cUsuarioSolicitante, '"}'), @cIdCorrelacion, @dFecha)

        Commit Transaction
        Select IncidenciaNumero = @cIncidenciaNumero, FechaRegistro = @dFecha
    End Try
    Begin Catch
        If Xact_State() <> 0 Rollback Transaction
        ;Throw
    End Catch
End
Go

/* ============================== COMPATIBILIDAD DEL REGISTRO NORMAL ============================== */

Create Or Alter Procedure dbo.Usp_TI_Registrar_Incidencia
/*================================================================================
Objetivo            : Registrar una incidencia nueva utilizando únicamente datos permitidos al usuario.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : dbo.Usp_TI_Registrar_Incidencia
Comentario Cambios  : Guarda UsuarioRegistro para distinguir el origen sin alterar el contrato del módulo existente.
================================================================================*/
    @cUsuario varchar(20), @cLinea char(3), @cTipo char(3), @cTitulo nvarchar(250), @cDetalle nvarchar(max), @cMensajeError nvarchar(1000) = Null, @cIdCorrelacion uniqueidentifier
As
Begin
    Set NoCount On
    Set Xact_Abort On

    Declare @cArea char(3), @cIncidenciaNumero varchar(12), @nCorrelativo int, @dFecha datetime2(0) = SysDateTime()
    Select @cArea = Area From dbo.TI_Usuario Where Usuario = @cUsuario and Estado = 'A'

    If @cArea Is Null Throw 50001, 'El usuario autenticado no se encuentra habilitado.', 1
    If Not Exists (Select 1 From dbo.TI_Linea Where Linea = @cLinea and Estado = 'A') Throw 50002, 'El sistema o módulo seleccionado no se encuentra disponible.', 1
    If Not Exists (Select 1 From dbo.TI_Tipo Where Tipo = @cTipo and Estado = 'A') Throw 50003, 'El tipo de ticket seleccionado no se encuentra disponible.', 1
    If NullIf(LTrim(RTrim(@cTitulo)), '') Is Null Throw 50004, 'El título del problema es obligatorio.', 1
    If NullIf(LTrim(RTrim(@cDetalle)), '') Is Null Throw 50005, 'La descripción detallada es obligatoria.', 1

    Begin Try
        Begin Transaction
        Select @nCorrelativo = IsNull(Max(Try_Convert(int, Right(IncidenciaNumero, 6))), 0) + 1 From dbo.TI_Incidencia With (UpdLock, HoldLock) Where IncidenciaNumero Like 'INC-[0-9][0-9][0-9][0-9][0-9][0-9]'
        If @nCorrelativo > 999999 Throw 50006, 'Se alcanzó el límite del correlativo de incidencias.', 1
        Set @cIncidenciaNumero = 'INC-' + Right('000000' + Convert(varchar(6), @nCorrelativo), 6)

        Insert dbo.TI_Incidencia (IncidenciaNumero, FechaRegistro, UsuarioSolicitante, UsuarioRegistro, AreaSolicitante, Linea, Tipo, Estado, Titulo, Detalle, MensajeError, CanalRegistro, UltimoUsuario, UltimaFechaModif)
        Values (@cIncidenciaNumero, @dFecha, @cUsuario, @cUsuario, @cArea, @cLinea, @cTipo, 'NV', LTrim(RTrim(@cTitulo)), LTrim(RTrim(@cDetalle)), NullIf(LTrim(RTrim(@cMensajeError)), ''), 'PORTAL', @cUsuario, @dFecha)

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


