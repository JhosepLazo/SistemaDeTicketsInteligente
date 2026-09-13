/*
    Archivo: 20_AjustesOperativosSinIA.sql
    Objetivo: Completar los contratos operativos necesarios para consumir desde frontend las mejoras introducidas en 19_MejorasFuncionalesSinIA.sql.
    Responsabilidad: Mantener compatibilidad controlada del avance, exponer datos de mesa de ayuda/aprobaciones y generar notificaciones por cambios relevantes del ticket.
    Dependencias: Requiere 19_MejorasFuncionalesSinIA.sql.
    Orden: Ejecutar después de 19_MejorasFuncionalesSinIA.sql.
    Consideraciones: No implementa IA ni automatización de acciones; todas las notificaciones son internas al portal.
*/

Use [SistemaTicketsInteligente]
Go

Create Or Alter Procedure dbo.Usp_TI_Registrar_AvanceTicket
/*================================================================================
Objetivo            : Registrar un avance técnico con esfuerzo efectivo y área causante obligatorios.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : dbo.Usp_TI_Registrar_AvanceTicket
Comentario Cambios  : Mantiene parámetros nuevos con valores por defecto para devolver un error funcional claro a clientes desactualizados.
================================================================================*/
    @cUsuario varchar(20), @cArea char(3), @cIncidenciaNumero varchar(12), @cDetalle nvarchar(max), @lVisibleUsuario bit = 0,
    @nTiempoUtilizado decimal(8,2) = Null, @cAreaCausante char(3) = Null, @cIdCorrelacion uniqueidentifier
As
Begin
    Set NoCount On
    Set Xact_Abort On

    Begin Try
        Begin Transaction

        Declare @nAvance int, @nMensaje int, @nEstado int, @cEstado char(2), @dFecha datetime2(0) = SysDateTime()

        If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM')) Throw 50216, 'El operador TI no es válido.', 1
        If NullIf(LTrim(RTrim(@cDetalle)), '') Is Null Throw 50217, 'El detalle del avance no puede estar vacío.', 1
        If @nTiempoUtilizado Is Null or @nTiempoUtilizado <= 0 or @nTiempoUtilizado > 1440 Throw 50234, 'Registra el tiempo efectivo utilizado en minutos (1 a 1440).', 1
        If @cAreaCausante Is Null or Not Exists (Select 1 From dbo.TI_Area Where Area = @cAreaCausante and Estado = 'A') Throw 50235, 'Selecciona el área causante del avance.', 1

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

Create Or Alter Procedure dbo.Usp_TI_Obtener_DatosGestionOperativaTI
/*================================================================================
Objetivo            : Obtener catálogos pequeños requeridos por aprobación manual y registro de tickets por mesa de ayuda.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Evita reutilizar Configuración TI y mantiene acceso disponible para TEC/SUP/ADM.
================================================================================*/
    @cUsuario varchar(20), @cArea char(3)
As
Begin
    Set NoCount On
    If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM')) Throw 50241, 'El operador TI no es válido.', 1

    Select Codigo = Usuario, Descripcion = NombreCompleto, Area
    From dbo.TI_Usuario
    Where Estado = 'A' and Perfil = 'USR'
    Order By NombreCompleto

    Select AccionCodigo, Nombre, NivelRiesgo
    From dbo.TI_Accion
    Where Estado = 'A' and RequiereAprobacion = 1
    Order By NivelRiesgo, Nombre

    Select Codigo = Linea, Descripcion From dbo.TI_Linea Where Estado = 'A' Order By Descripcion
    Select Codigo = Tipo, Descripcion From dbo.TI_Tipo Where Estado = 'A' Order By Descripcion
End
Go

Create Or Alter Trigger dbo.Tr_TI_Incidencia_NotificacionesOperativas
On dbo.TI_Incidencia
After Update
As
Begin
    Set NoCount On

    Insert dbo.TI_Notificacion (Usuario, IncidenciaNumero, Tipo, Titulo, Mensaje, Ruta, Fecha)
    Select i.UsuarioSolicitante, i.IncidenciaNumero, 'INFORMACION_REQUERIDA', N'TI necesita información',
           Concat(N'El ticket ', i.IncidenciaNumero, N' requiere información adicional para continuar.'), '/mis-tickets', SysDateTime()
    From inserted i Inner Join deleted d on d.IncidenciaNumero = i.IncidenciaNumero
    Where i.Estado = 'RC' and d.Estado <> 'RC'

    Insert dbo.TI_Notificacion (Usuario, IncidenciaNumero, Tipo, Titulo, Mensaje, Ruta, Fecha)
    Select i.UsuarioSolicitante, i.IncidenciaNumero, 'VALIDACION_PENDIENTE', N'Solución pendiente de validación',
           Concat(N'TI registró una solución para ', i.IncidenciaNumero, N'. Confirma si el problema fue resuelto.'), '/mis-tickets', SysDateTime()
    From inserted i Inner Join deleted d on d.IncidenciaNumero = i.IncidenciaNumero
    Where i.Estado = 'PV' and d.Estado <> 'PV'

    Insert dbo.TI_Notificacion (Usuario, IncidenciaNumero, Tipo, Titulo, Mensaje, Ruta, Fecha)
    Select i.UsuarioSolicitante, i.IncidenciaNumero, 'NO_PROCEDE', N'Ticket marcado como No Procede',
           Concat(N'Revisa el motivo registrado por TI para el ticket ', i.IncidenciaNumero, N'.'), '/mis-tickets', SysDateTime()
    From inserted i Inner Join deleted d on d.IncidenciaNumero = i.IncidenciaNumero
    Where i.Estado = 'CA' and d.Estado <> 'CA'

    Insert dbo.TI_Notificacion (Usuario, IncidenciaNumero, Tipo, Titulo, Mensaje, Ruta, Fecha)
    Select i.UsuarioTI, i.IncidenciaNumero, 'REAPERTURA', N'Ticket reabierto',
           Concat(N'El usuario reabrió el ticket ', i.IncidenciaNumero, N'. Revisa nuevamente el caso.'), '/gestion-tickets', SysDateTime()
    From inserted i Inner Join deleted d on d.IncidenciaNumero = i.IncidenciaNumero
    Where i.Estado = 'RA' and d.Estado <> 'RA' and i.UsuarioTI Is Not Null

    Insert dbo.TI_Notificacion (Usuario, IncidenciaNumero, Tipo, Titulo, Mensaje, Ruta, Fecha)
    Select i.UsuarioTI, i.IncidenciaNumero, 'ASIGNACION', N'Nuevo ticket asignado',
           Concat(N'Se te asignó el ticket ', i.IncidenciaNumero, N'.'), '/gestion-tickets', SysDateTime()
    From inserted i Inner Join deleted d on d.IncidenciaNumero = i.IncidenciaNumero
    Where i.UsuarioTI Is Not Null and IsNull(d.UsuarioTI, '') <> i.UsuarioTI
End
Go

Create Or Alter Procedure dbo.Usp_TI_Obtener_EsfuerzoOperativoTI
/*================================================================================
Objetivo            : Resumir tiempo efectivo registrado por los avances técnicos dentro de un rango.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Separa esfuerzo real de duración calendario del ticket.
================================================================================*/
    @dFechaInicio date, @dFechaFin date
As
Begin
    Set NoCount On
    If @dFechaInicio Is Null or @dFechaFin Is Null or @dFechaInicio > @dFechaFin Throw 50450, 'El rango de fechas no es válido.', 1

    Select
        MinutosEfectivos = Cast(IsNull(Sum(av.TiempoUtilizado), 0) as decimal(18,2)),
        HorasEfectivas = Cast(IsNull(Sum(av.TiempoUtilizado), 0) / 60.0 as decimal(18,2)),
        TicketsConEsfuerzo = Count(Distinct av.IncidenciaNumero)
    From dbo.TI_IncidenciaAvance av
    Where av.FechaAvance >= @dFechaInicio and av.FechaAvance < DateAdd(day, 1, @dFechaFin)

    Select av.IncidenciaNumero, MinutosEfectivos = Cast(Sum(av.TiempoUtilizado) as decimal(18,2))
    From dbo.TI_IncidenciaAvance av
    Where av.FechaAvance >= @dFechaInicio and av.FechaAvance < DateAdd(day, 1, @dFechaFin)
    Group By av.IncidenciaNumero
    Order By MinutosEfectivos Desc
End
Go
