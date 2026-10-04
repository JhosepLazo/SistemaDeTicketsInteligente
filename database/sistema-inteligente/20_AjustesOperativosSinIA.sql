/*
    Archivo: 20_AjustesOperativosSinIA.sql
    Objetivo: Completar los contratos operativos necesarios para consumir desde frontend las mejoras introducidas en 19_MejorasFuncionalesSinIA.sql.
    Responsabilidad: Mantener compatibilidad controlada del avance, exponer datos de mesa de ayuda/aprobaciones y generar notificaciones por cambios relevantes del ticket.
    Dependencias: Requiere 19_MejorasFuncionalesSinIA.sql.
    Orden: Ejecutar después de 19_MejorasFuncionalesSinIA.sql.
    Consideraciones: No implementa IA ni automatización de acciones; todas las notificaciones son internas al portal.
*/

Use [GestionSistemas]
Go

-- dbo.Usp_TI_Registrar_AvanceTicket: la versión vigente está en 22_CierreMejorasFuncionalesSinIA.sql (aquí había una versión anterior que ese script reemplaza).
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

-- dbo.Usp_TI_Obtener_EsfuerzoOperativoTI: la versión vigente está en 22_CierreMejorasFuncionalesSinIA.sql (aquí había una versión anterior que ese script reemplaza).
Go

