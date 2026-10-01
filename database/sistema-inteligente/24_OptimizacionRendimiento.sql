/*
	Archivo: 24_OptimizacionRendimiento.sql
	Objetivo: Optimizar las lecturas de Inicio, Mis Tickets, Gestion de Tickets y Reportes.
	Responsabilidad: Crear indices dirigidos a las consultas reales sin modificar datos funcionales.
	Dependencias: TI_Incidencia y TI_SolicitudAprobacion.
	Consideraciones: El script es repetible y no recrea indices existentes.
*/

Use [GestionSistemas]
Go

Set NoCount On
Set Xact_Abort On
Set Ansi_Nulls On
Set Quoted_Identifier On
Go

If Not Exists (Select 1 From sys.indexes Where object_id = Object_Id(N'dbo.TI_Incidencia') and name = N'IX_TI_Incidencia_ColaTI')
Begin
	Create Nonclustered Index IX_TI_Incidencia_ColaTI
		on dbo.TI_Incidencia (AreaTI, Estado, UltimaFechaModif Desc)
		Include (UsuarioTI, Prioridad, FechaRegistro, CanalRegistro, SlaObjetivoMinutos, UsuarioSolicitante, Titulo, Linea, Tipo)
End
Go

If Not Exists (Select 1 From sys.indexes Where object_id = Object_Id(N'dbo.TI_Incidencia') and name = N'IX_TI_Incidencia_UsuarioSolicitante_UltimaFecha')
Begin
	Create Nonclustered Index IX_TI_Incidencia_UsuarioSolicitante_UltimaFecha
		on dbo.TI_Incidencia (UsuarioSolicitante, UltimaFechaModif Desc)
		Include (IncidenciaNumero, Titulo, Estado, UsuarioTI, FechaRegistro, FechaCierre, Prioridad, Calificacion)
End
Go

If Not Exists (Select 1 From sys.indexes Where object_id = Object_Id(N'dbo.TI_SolicitudAprobacion') and name = N'IX_TI_SolicitudAprobacion_Pendiente_Incidencia')
Begin
	Create Nonclustered Index IX_TI_SolicitudAprobacion_Pendiente_Incidencia
		on dbo.TI_SolicitudAprobacion (IncidenciaNumero)
		Include (Secuencia, UsuarioAprobador, FechaSolicitud)
		Where Estado = 'P'
End
Go

If Not Exists (Select 1 From sys.indexes Where object_id = Object_Id(N'dbo.TI_Incidencia') and name = N'IX_TI_Incidencia_FechaRegistro_Reportes')
Begin
	Create Nonclustered Index IX_TI_Incidencia_FechaRegistro_Reportes
		on dbo.TI_Incidencia (FechaRegistro Desc)
		Include (Estado, AreaSolicitante, AreaTI, Tipo, Prioridad, UsuarioTI, FechaAtencion, FechaCierre, UltimaFechaModif)
End
Go

Update Statistics dbo.TI_Incidencia With FullScan
Update Statistics dbo.TI_SolicitudAprobacion With FullScan
Go
