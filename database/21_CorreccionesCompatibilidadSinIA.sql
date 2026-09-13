/*
    Archivo: 21_CorreccionesCompatibilidadSinIA.sql
    Objetivo: Aplicar correcciones de compatibilidad detectadas durante la revisión técnica de las mejoras funcionales sin IA.
    Responsabilidad: Evitar que una sincronización de identidad falle cuando Spring devuelve un cargo que aún no existe en el maestro local.
    Dependencias: Requiere 19_MejorasFuncionalesSinIA.sql.
    Orden: Ejecutar después de 20_AjustesOperativosSinIA.sql.
    Consideraciones: No modifica credenciales ni reglas de autorización; conserva el cargo local hasta que el catálogo corporativo sea sincronizado.
*/

Use [SistemaTicketsInteligente]
Go

Create Or Alter Procedure dbo.Usp_TI_Sincronizar_UsuarioCorporativo
/*================================================================================
Objetivo            : Sincronizar metadata segura de un usuario corporativo sin romper referencias locales.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : dbo.Usp_TI_Sincronizar_UsuarioCorporativo
Comentario Cambios  : Solo actualiza Cargo cuando el código ya existe en TI_Cargo.
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
            Cargo = Case When @cCargo Is Not Null and Exists (Select 1 From dbo.TI_Cargo Where Cargo = @cCargo) Then @cCargo Else Cargo End,
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

/* Ejecuta Procedure */
