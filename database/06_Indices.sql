/*
	Archivo: 06_Indices.sql
	Objetivo: Crear los índices secundarios requeridos para optimizar las consultas principales del Sistema de Tickets Inteligente.
	Responsabilidad: Mejorar el rendimiento de búsquedas por solicitante, cola TI, técnico asignado, clasificación, documentos relacionados, aprobaciones y auditoría.
	Dependencias: Requiere todas las tablas involucradas en los índices previamente creadas.
	Orden: Ejecutar después de los scripts estructurales de maestros, incidencias, inteligencia, control y auditoría.
	Consideraciones: No crea índices especulativos ni duplica aquellos ya generados por Primary Key o Unique; los índices futuros deben justificarse mediante consultas reales.
*/

Use [SistemaTicketsInteligente]
Go

Set Xact_Abort on

Begin Try
	Begin Transaction

	Create Nonclustered Index IX_TI_Incidencia_UsuarioSolicitante_FechaRegistro
		on dbo.TI_Incidencia (UsuarioSolicitante, FechaRegistro Desc)

	Create Nonclustered Index IX_TI_Incidencia_Estado_AreaTI_FechaRegistro
		on dbo.TI_Incidencia (Estado, AreaTI, FechaRegistro Desc)

	Create Nonclustered Index IX_TI_Incidencia_UsuarioTI_Estado_FechaRegistro
		on dbo.TI_Incidencia (UsuarioTI, Estado, FechaRegistro Desc)

	Create Nonclustered Index IX_TI_Incidencia_Linea_Item_Categoria
		on dbo.TI_Incidencia (Linea, Item, Categoria)

	Create Nonclustered Index IX_TI_IncidenciaDocumento_Tipo_Numero_Compania
		on dbo.TI_IncidenciaDocumento (TipoDocumento, NumeroDocumento, CompaniaSocio)

	Create Nonclustered Index IX_TI_SolicitudAprobacion_Estado_Aprobador_Fecha
		on dbo.TI_SolicitudAprobacion (Estado, UsuarioAprobador, FechaSolicitud Desc)

	Create Nonclustered Index IX_TI_Auditoria_IdCorrelacion
		on dbo.TI_Auditoria (IdCorrelacion)

	Create Nonclustered Index IX_TI_Auditoria_Incidencia_Fecha
		on dbo.TI_Auditoria (IncidenciaNumero, Fecha Desc)

	Commit Transaction
End Try
Begin Catch
	If Xact_State() <> 0
	Begin
		Rollback Transaction
	End

	;Throw
End Catch
Go