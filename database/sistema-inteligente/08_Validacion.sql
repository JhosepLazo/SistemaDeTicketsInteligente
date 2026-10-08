/*
	Archivo: 08_Validacion.sql
	Objetivo: Verificar que la estructura física y relacional de GestionSistemas coincida con el modelo aprobado antes de utilizarla desde la aplicación.
	Responsabilidad: Validar tablas, Primary Keys, Foreign Keys, Unique, Check, índices, tipos compatibles, collation, restricciones confiables y uso autorizado de Identity.
	Dependencias: Requiere la instalación completa (00 a 40). Las listas esperadas cubren los objetos de todos los scripts.
	Orden: Ejecutar al finalizar la instalación (último paso de InstalarLocal.ps1) y después de cualquier modificación estructural;
		cada script que agregue tablas, claves, restricciones, índices o triggers debe agregarlos también aquí.
	Consideraciones: Ejecuta inserciones controladas dentro de una transacción y finaliza con Rollback, permitiendo validar los principales Join y relaciones sin dejar registros de prueba.
*/

Use [GestionSistemas]
Go

Set NoCount on
Set Xact_Abort on

Declare @TablasEsperadas Table
(
	Tabla sysname Primary Key
)

Insert Into @TablasEsperadas (Tabla)
Values
	(N'TI_Accion'),
	(N'TI_AgenteAccionEjecutor'),
	(N'TI_AgenteEvento'),
	(N'TI_AgenteHerramienta'),
	(N'TI_AgenteSesion'),
	(N'TI_Area'),
	(N'TI_Auditoria'),
	(N'TI_BaseConocimiento'),
	(N'TI_Cargo'),
	(N'TI_Categoria'),
	(N'TI_ConocimientoVector'),
	(N'TI_EjecucionAccion'),
	(N'TI_Estado'),
	(N'TI_EstadoTransicion'),
	(N'TI_FormatoSoporte'),
	(N'TI_Incidencia'),
	(N'TI_IncidenciaAdjunto'),
	(N'TI_IncidenciaAvance'),
	(N'TI_IncidenciaClasificacion'),
	(N'TI_IncidenciaDato'),
	(N'TI_IncidenciaDiagnostico'),
	(N'TI_IncidenciaDiagnosticoEvidencia'),
	(N'TI_IncidenciaDocumento'),
	(N'TI_IncidenciaEstado'),
	(N'TI_IncidenciaMensaje'),
	(N'TI_Item'),
	(N'TI_ItemCategoria'),
	(N'TI_Linea'),
	(N'TI_Notificacion'),
	(N'TI_Parametro'),
	(N'TI_ParametroSLA'),
	(N'TI_Perfil'),
	(N'TI_PlantillaCampo'),
	(N'TI_PoliticaAutonomia'),
	(N'TI_SolicitudAprobacion'),
	(N'TI_SubTipo'),
	(N'TI_Tipo'),
	(N'TI_Usuario')

Declare @ForeignKeysEsperadas Table
(
	Nombre sysname Primary Key
)

Insert Into @ForeignKeysEsperadas (Nombre)
Values
	(N'FK_TI_AgenteAccionEjecutor_Accion'),
	(N'FK_TI_AgenteEvento_Sesion'),
	(N'FK_TI_AgenteHerramienta_Accion'),
	(N'FK_TI_AgenteSesion_Accion'),
	(N'FK_TI_AgenteSesion_AreaTI'),
	(N'FK_TI_AgenteSesion_Incidencia'),
	(N'FK_TI_AgenteSesion_UsuarioDecision'),
	(N'FK_TI_AgenteSesion_UsuarioInvitado'),
	(N'FK_TI_AgenteSesion_UsuarioTI'),
	(N'FK_TI_Auditoria_Incidencia'),
	(N'FK_TI_Auditoria_Usuario'),
	(N'FK_TI_BaseConocimiento_Categoria'),
	(N'FK_TI_BaseConocimiento_IncidenciaOrigen'),
	(N'FK_TI_BaseConocimiento_Item'),
	(N'FK_TI_BaseConocimiento_ItemCategoria'),
	(N'FK_TI_BaseConocimiento_Linea'),
	(N'FK_TI_BaseConocimiento_LineaItem'),
	(N'FK_TI_BaseConocimiento_Tipo'),
	(N'FK_TI_BaseConocimiento_TipoSubTipoCategoria'),
	(N'FK_TI_BaseConocimiento_UsuarioValida'),
	(N'FK_TI_EjecucionAccion_Accion'),
	(N'FK_TI_EjecucionAccion_Incidencia'),
	(N'FK_TI_EjecucionAccion_Solicitud'),
	(N'FK_TI_EjecucionAccion_UsuarioEjecutor'),
	(N'FK_TI_EstadoTransicion_EstadoDestino'),
	(N'FK_TI_EstadoTransicion_EstadoOrigen'),
	(N'FK_TI_FormatoSoporte_Tipo'),
	(N'FK_TI_Incidencia_AreaCausante'),
	(N'FK_TI_Incidencia_AreaSolicitante'),
	(N'FK_TI_Incidencia_AreaTI'),
	(N'FK_TI_Incidencia_Categoria'),
	(N'FK_TI_Incidencia_Estado'),
	(N'FK_TI_Incidencia_ItemCategoria'),
	(N'FK_TI_Incidencia_Linea'),
	(N'FK_TI_Incidencia_LineaItem'),
	(N'FK_TI_Incidencia_Tipo'),
	(N'FK_TI_Incidencia_TipoSubTipoCategoria'),
	(N'FK_TI_Incidencia_UsuarioAsigno'),
	(N'FK_TI_Incidencia_UsuarioRegistro'),
	(N'FK_TI_Incidencia_UsuarioSolicitante'),
	(N'FK_TI_Incidencia_UsuarioTI'),
	(N'FK_TI_IncidenciaAdjunto_Incidencia'),
	(N'FK_TI_IncidenciaAdjunto_Mensaje'),
	(N'FK_TI_IncidenciaAdjunto_UsuarioRegistro'),
	(N'FK_TI_IncidenciaAvance_AreaCausante'),
	(N'FK_TI_IncidenciaAvance_Incidencia'),
	(N'FK_TI_IncidenciaAvance_UsuarioTI'),
	(N'FK_TI_IncidenciaClasificacion_Incidencia'),
	(N'FK_TI_IncidenciaClasificacion_LineaItem'),
	(N'FK_TI_IncidenciaClasificacion_TipoSubTipoCategoria'),
	(N'FK_TI_IncidenciaClasificacion_Usuario'),
	(N'FK_TI_IncidenciaDato_Campo'),
	(N'FK_TI_IncidenciaDato_Incidencia'),
	(N'FK_TI_IncidenciaDato_Usuario'),
	(N'FK_TI_IncidenciaDiagnostico_Incidencia'),
	(N'FK_TI_IncidenciaDiagnostico_UsuarioValida'),
	(N'FK_TI_IncidenciaDiagnosticoEvidencia_Diagnostico'),
	(N'FK_TI_IncidenciaDocumento_Incidencia'),
	(N'FK_TI_IncidenciaEstado_Estado'),
	(N'FK_TI_IncidenciaEstado_Incidencia'),
	(N'FK_TI_IncidenciaEstado_UsuarioCambio'),
	(N'FK_TI_IncidenciaMensaje_Incidencia'),
	(N'FK_TI_IncidenciaMensaje_UsuarioAutor'),
	(N'FK_TI_Item_Linea'),
	(N'FK_TI_ItemCategoria_Categoria'),
	(N'FK_TI_ItemCategoria_Item'),
	(N'FK_TI_Linea_Area'),
	(N'FK_TI_Notificacion_Incidencia'),
	(N'FK_TI_Notificacion_Usuario'),
	(N'FK_TI_PlantillaCampo_Tipo'),
	(N'FK_TI_PoliticaAutonomia_Accion'),
	(N'FK_TI_PoliticaAutonomia_Tipo'),
	(N'FK_TI_SolicitudAprobacion_Accion'),
	(N'FK_TI_SolicitudAprobacion_Diagnostico'),
	(N'FK_TI_SolicitudAprobacion_Incidencia'),
	(N'FK_TI_SolicitudAprobacion_UsuarioAprobador'),
	(N'FK_TI_SolicitudAprobacion_UsuarioSolicitante'),
	(N'FK_TI_SubTipo_Categoria'),
	(N'FK_TI_SubTipo_Tipo'),
	(N'FK_TI_Usuario_Area'),
	(N'FK_TI_Usuario_Cargo'),
	(N'FK_TI_Usuario_Jefe'),
	(N'FK_TI_Usuario_Perfil')

Declare @ObjetosEsperados Table
(
	Tipo	char(2)	Not Null,
	Nombre	sysname	Not Null,
	Primary Key (Tipo, Nombre)
)

Insert Into @ObjetosEsperados (Tipo, Nombre)
Values
	(N'UQ', N'UQ_TI_AgenteSesion_IdCorrelacion'),
	(N'UQ', N'UQ_TI_EjecucionAccion_ClaveIdempotencia'),
	(N'UQ', N'UQ_TI_Item_LineaItem'),
	(N'CK', N'CK_TI_Accion_NivelRiesgo'),
	(N'CK', N'CK_TI_Accion_Tipo'),
	(N'CK', N'CK_TI_AgenteAccionEjecutor_Esquema'),
	(N'CK', N'CK_TI_AgenteAccionEjecutor_MaximoFilas'),
	(N'CK', N'CK_TI_AgenteAccionEjecutor_Procedimiento'),
	(N'CK', N'CK_TI_AgenteHerramienta_Esquema'),
	(N'CK', N'CK_TI_AgenteHerramienta_Estado'),
	(N'CK', N'CK_TI_AgenteHerramienta_MaximoFilas'),
	(N'CK', N'CK_TI_AgenteHerramienta_TipoProcedimiento'),
	(N'CK', N'CK_TI_AgenteSesion_Confianza'),
	(N'CK', N'CK_TI_AgenteSesion_Decision'),
	(N'CK', N'CK_TI_AgenteSesion_Estado'),
	(N'CK', N'CK_TI_AgenteSesion_EstadoInvitacion'),
	(N'CK', N'CK_TI_Auditoria_TipoActor'),
	(N'CK', N'CK_TI_BaseConocimiento_Estado'),
	(N'CK', N'CK_TI_BaseConocimiento_Guia'),
	(N'CK', N'CK_TI_ConocimientoVector_Dimensiones'),
	(N'CK', N'CK_TI_ConocimientoVector_Origen'),
	(N'CK', N'CK_TI_EjecucionAccion_Estado'),
	(N'CK', N'CK_TI_EjecucionAccion_FilasAfectadas'),
	(N'CK', N'CK_TI_EjecucionAccion_TipoEjecutor'),
	(N'CK', N'CK_TI_EstadoTransicion_Distintos'),
	(N'CK', N'CK_TI_EstadoTransicion_Estado'),
	(N'CK', N'CK_TI_Incidencia_Calificacion'),
	(N'CK', N'CK_TI_Incidencia_CanalRegistro'),
	(N'CK', N'CK_TI_Incidencia_Complejidad'),
	(N'CK', N'CK_TI_Incidencia_Impacto'),
	(N'CK', N'CK_TI_Incidencia_Prioridad'),
	(N'CK', N'CK_TI_Incidencia_TipoResolucion'),
	(N'CK', N'CK_TI_IncidenciaAdjunto_TamanoBytes'),
	(N'CK', N'CK_TI_IncidenciaAvance_Porcentaje'),
	(N'CK', N'CK_TI_IncidenciaClasificacion_Confianza'),
	(N'CK', N'CK_TI_IncidenciaClasificacion_Json'),
	(N'CK', N'CK_TI_IncidenciaClasificacion_Niveles'),
	(N'CK', N'CK_TI_IncidenciaClasificacion_Origen'),
	(N'CK', N'CK_TI_IncidenciaDato_Fuente'),
	(N'CK', N'CK_TI_IncidenciaDiagnostico_Confianza'),
	(N'CK', N'CK_TI_IncidenciaDiagnostico_Estado'),
	(N'CK', N'CK_TI_IncidenciaDiagnostico_Origen'),
	(N'CK', N'CK_TI_IncidenciaDiagnosticoEvidencia_Similitud'),
	(N'CK', N'CK_TI_IncidenciaMensaje_TipoAutor'),
	(N'CK', N'CK_TI_ParametroSLA_Minutos'),
	(N'CK', N'CK_TI_ParametroSLA_Prioridad'),
	(N'CK', N'CK_TI_PlantillaCampo_Estado'),
	(N'CK', N'CK_TI_PlantillaCampo_Longitud'),
	(N'CK', N'CK_TI_PlantillaCampo_TipoDato'),
	(N'CK', N'CK_TI_PoliticaAutonomia_Autonoma'),
	(N'CK', N'CK_TI_PoliticaAutonomia_Estado'),
	(N'CK', N'CK_TI_PoliticaAutonomia_Modo'),
	(N'CK', N'CK_TI_SolicitudAprobacion_Estado'),
	(N'CK', N'CK_TI_SolicitudAprobacion_Expiracion'),
	(N'CK', N'CK_TI_SolicitudAprobacion_Respuesta'),
	(N'CK', N'CK_TI_Usuario_TipoUsuario'),
	(N'IX', N'IX_TI_AgenteSesion_IncidenciaFecha'),
	(N'IX', N'IX_TI_AgenteSesion_UsuarioEstadoFecha'),
	(N'IX', N'IX_TI_AgenteSesion_UsuarioInvitado'),
	(N'IX', N'IX_TI_Auditoria_IdCorrelacion'),
	(N'IX', N'IX_TI_Auditoria_Incidencia_Fecha'),
	(N'IX', N'IX_TI_Incidencia_ColaTI'),
	(N'IX', N'IX_TI_Incidencia_Estado_AreaTI_FechaRegistro'),
	(N'IX', N'IX_TI_Incidencia_FechaRegistro_Reportes'),
	(N'IX', N'IX_TI_Incidencia_Linea_Item_Categoria'),
	(N'IX', N'IX_TI_Incidencia_UsuarioSolicitante_FechaRegistro'),
	(N'IX', N'IX_TI_Incidencia_UsuarioSolicitante_UltimaFecha'),
	(N'IX', N'IX_TI_Incidencia_UsuarioTI_Estado_FechaRegistro'),
	(N'IX', N'IX_TI_IncidenciaAvance_FechaUsuario'),
	(N'IX', N'IX_TI_IncidenciaDocumento_Tipo_Numero_Compania'),
	(N'IX', N'IX_TI_Notificacion_UsuarioLeidaFecha'),
	(N'IX', N'IX_TI_SolicitudAprobacion_Estado_Aprobador_Fecha'),
	(N'IX', N'IX_TI_SolicitudAprobacion_Pendiente_Incidencia'),
	(N'TR', N'Tr_TI_Incidencia_BloqueoAprobacion'),
	(N'TR', N'Tr_TI_Incidencia_NotificacionesOperativas'),
	(N'TR', N'Tr_TI_Incidencia_NotificacionRespuestaUsuario'),
	(N'TR', N'Tr_TI_Incidencia_TransicionEstado'),
	(N'TR', N'Tr_TI_SolicitudAprobacion_SeparacionFunciones')

If DatabasePropertyEx(Db_Name(), 'Collation') <> N'Modern_Spanish_CI_AS'
Begin
	;Throw 50017, 'El collation de la base no coincide con Modern_Spanish_CI_AS.', 1
End

If Exists
(
	Select 1
	From @TablasEsperadas as esperada
	Where Not Exists
	(
		Select 1
		From sys.tables as tabla
		Inner Join sys.schemas as esquema on esquema.schema_id = tabla.schema_id
		Where esquema.[name] = N'dbo'
			and tabla.[name] = esperada.Tabla
	)
)
Begin
	;Throw 50018, 'Faltan tablas del modelo esperado.', 1
End

If Exists
(
	Select 1
	From @TablasEsperadas as esperada
	Where Not Exists
	(
		Select 1
		From sys.key_constraints as restriccion
		Inner Join sys.tables as tabla on tabla.object_id = restriccion.parent_object_id
		Where restriccion.type = 'PK'
			and restriccion.[name] = N'PK_' + esperada.Tabla
			and tabla.[name] = esperada.Tabla
	)
)
Begin
	;Throw 50019, 'Falta una clave primaria esperada o su nombre no coincide con la tabla.', 1
End

If Exists
(
	Select 1
	From @ForeignKeysEsperadas as esperada
	Where Not Exists
	(
		Select 1
		From sys.foreign_keys
		Where [name] = esperada.Nombre
	)
)
Begin
	;Throw 50020, 'Falta una foreign key esperada.', 1
End

If Exists
(
	Select 1
	From @ObjetosEsperados as esperado
	Where
	(
		esperado.Tipo = 'UQ'
		and Not Exists
		(
			Select 1
			From sys.key_constraints
			Where type = 'UQ'
				and [name] = esperado.Nombre
		)
	)
	or
	(
		esperado.Tipo = 'CK'
		and Not Exists
		(
			Select 1
			From sys.check_constraints
			Where [name] = esperado.Nombre
		)
	)
	or
	(
		esperado.Tipo = 'IX'
		and Not Exists
		(
			Select 1
			From sys.indexes
			Where [name] = esperado.Nombre
		)
	)
	or
	(
		esperado.Tipo = 'TR'
		and Not Exists
		(
			Select 1
			From sys.triggers
			Where [name] = esperado.Nombre
				and is_disabled = 0
		)
	)
)
Begin
	;Throw 50021, 'Falta una restricción UNIQUE, CHECK, un índice crítico o un trigger habilitado.', 1
End

If Exists
(
	Select 1
	From sys.foreign_keys
	Where [name] Like N'FK_TI[_]%'
		and
		(
			is_disabled = 1
			or is_not_trusted = 1
			or delete_referential_action <> 0
			or update_referential_action <> 0
		)
)
Begin
	;Throw 50022, 'Existen foreign keys deshabilitadas, no confiables o con acciones en cascada.', 1
End

If Exists
(
	Select 1
	From sys.check_constraints
	Where [name] Like N'CK_TI[_]%'
		and (is_disabled = 1 or is_not_trusted = 1)
)
Begin
	;Throw 50023, 'Existen restricciones Check deshabilitadas o no confiables.', 1
End

If Exists
(
	Select 1
	From sys.foreign_keys as fk
	Inner Join sys.foreign_key_columns as fkc on fkc.constraint_object_id = fk.object_id
	Inner Join sys.columns as hija on hija.object_id = fkc.parent_object_id and hija.column_id = fkc.parent_column_id
	Inner Join sys.columns as padre on padre.object_id = fkc.referenced_object_id and padre.column_id = fkc.referenced_column_id
	Where fk.[name] Like N'FK_TI[_]%'
		and
		(
			hija.system_type_id <> padre.system_type_id
			or hija.max_length <> padre.max_length
			or hija.[precision] <> padre.[precision]
			or hija.scale <> padre.scale
			or IsNull(hija.collation_name, N'') <> IsNull(padre.collation_name, N'')
		)
)
Begin
	;Throw 50024, 'Existen columnas relacionadas con tipos, longitudes o collation incompatibles.', 1
End

If
(
	Select Count(*)
	From sys.columns as columna
	Inner Join sys.tables as tabla
		on tabla.object_id = columna.object_id
	Where columna.is_identity = 1
		and tabla.[name] Like N'TI[_]%'
) <> 3
or Not Exists
(
	Select 1
	From sys.columns as columna
	Inner Join sys.tables as tabla
		on tabla.object_id = columna.object_id
	Where tabla.[name] = N'TI_Auditoria'
		and columna.[name] = N'AuditoriaNumero'
		and columna.is_identity = 1
)
or Not Exists
(
	Select 1
	From sys.columns as columna
	Inner Join sys.tables as tabla
		on tabla.object_id = columna.object_id
	Where tabla.[name] = N'TI_Notificacion'
		and columna.[name] = N'NotificacionNumero'
		and columna.is_identity = 1
)
or Not Exists
(
	Select 1
	From sys.columns as columna
	Inner Join sys.tables as tabla
		on tabla.object_id = columna.object_id
	Where tabla.[name] = N'TI_AgenteSesion'
		and columna.[name] = N'SesionNumero'
		and columna.is_identity = 1
)
Begin
	;Throw 50025, 'El uso de Identity no coincide con el modelo aprobado.', 1
End

Create Table #ViolacionesRestricciones
(
	Tabla			nvarchar(776)	Null,
	Restriccion		nvarchar(776)	Null,
	Condicion		nvarchar(max)	Null
)

Insert Into #ViolacionesRestricciones (Tabla, Restriccion, Condicion)
Exec(N'Dbcc CheckConstraints With All_Constraints, No_Infomsgs')

If Exists (Select 1 From #ViolacionesRestricciones)
Begin
	Select Tabla, Restriccion, Condicion
	From #ViolacionesRestricciones

	;Throw 50026, 'Dbcc CheckConstraints detectó datos que incumplen restricciones.', 1
End
Go

-- Prueba mínima de relaciones. Todo se revierte al finalizar.
Begin Try
	Begin Transaction

	Insert Into dbo.TI_Area (Area, Descripcion, Estado)
	Values ('ZZZ', 'Area de validacion', 'T')

	Insert Into dbo.TI_Linea (Linea, Area, Descripcion, Estado)
	Values ('ZZZ', 'ZZZ', 'Linea de validacion', 'T')

	Insert Into dbo.TI_Cargo (Cargo, Descripcion, Estado)
	Values ('TST', 'Cargo de validacion', 'T')

	Insert Into dbo.TI_Perfil (Perfil, Descripcion, Estado)
	Values ('TST', 'Perfil de validacion', 'T')

	Insert Into dbo.TI_Usuario (Usuario, NombreCompleto, Clave, Area, Cargo, Perfil, Estado)
	Values ('VALIDACION_SQL', 'Usuario de validacion', 'HASH_DEMO_NO_VALIDO', 'ZZZ', 'TST', 'TST', 'T')

	Insert Into dbo.TI_Item (Item, Linea, Descripcion, Estado)
	Values ('ITEM_VALIDACION', 'ZZZ', 'Item de validacion', 'T')

	Insert Into dbo.TI_Tipo (Tipo, Descripcion, Abreviatura, Estado)
	Values ('TST', 'Tipo de validacion', 'TST', 'T')

	Insert Into dbo.TI_Categoria (Categoria, Descripcion, Abreviatura, Estado)
	Values ('PRUEBA', 'Categoria de validacion', 'PRB', 'T')

	Insert Into dbo.TI_SubTipo (Tipo, SubTipo, Categoria, Descripcion, Abreviatura, Estado)
	Values ('TST', 'TST', 'PRUEBA', 'Subtipo de validacion', 'TST', 'T')

	Insert Into dbo.TI_Estado (Estado, Descripcion, Orden)
	Values ('TS', 'Estado de validacion', 1)

	Insert Into dbo.TI_ItemCategoria (Item, Categoria, Prioridad, Impacto, Complejidad, Estado)
	Values ('ITEM_VALIDACION', 'PRUEBA', 1, 1, 1, 'T')

	Insert Into dbo.TI_Incidencia (
		IncidenciaNumero, FechaRegistro, UsuarioSolicitante, UsuarioRegistro, AreaSolicitante, AreaTI, UsuarioTI, UsuarioAsigno, Linea,
		Item, Tipo, SubTipo, Categoria, Estado, AreaCausante, Titulo, Detalle, SlaObjetivoMinutos, Prioridad, Impacto,
		Complejidad, CanalRegistro, UltimoUsuario, UltimaFechaModif
	)
	Values
	(
		'TST-00000001', '2099-01-01T00:00:00', 'VALIDACION_SQL', 'VALIDACION_SQL', 'ZZZ', 'ZZZ', 'VALIDACION_SQL', 'VALIDACION_SQL', 'ZZZ',
		'ITEM_VALIDACION', 'TST', 'TST', 'PRUEBA', 'TS', 'ZZZ', N'Incidencia de validación', N'Detalle de validación',
		60, 1, 1, 1, 'PORTAL', 'VALIDACION_SQL', '2099-01-01T00:00:00'
	)

	Insert Into dbo.TI_IncidenciaAvance (IncidenciaNumero, Secuencia, UsuarioTI, FechaAvance, Detalle, TiempoUtilizado, PorcentajeAvance)
	Values ('TST-00000001', 1, 'VALIDACION_SQL', '2099-01-01T00:01:00', N'Avance de validación', 1, 50)

	Insert Into dbo.TI_IncidenciaEstado (IncidenciaNumero, Secuencia, Estado, UsuarioCambio, FechaCambio, Observacion)
	Values ('TST-00000001', 1, 'TS', 'VALIDACION_SQL', '2099-01-01T00:02:00', N'Estado de validación')

	Insert Into dbo.TI_IncidenciaMensaje (IncidenciaNumero, Secuencia, UsuarioAutor, TipoAutor, Contenido, FechaMensaje, EsInterno)
	Values ('TST-00000001', 1, 'VALIDACION_SQL', 'U', N'Mensaje de validación', '2099-01-01T00:03:00', 0)

	Insert Into dbo.TI_IncidenciaAdjunto (
		IncidenciaNumero, Secuencia, MensajeSecuencia, UsuarioRegistro, NombreOriginal, NombreArchivo, RutaArchivo, TipoMime, TamanoBytes, FechaRegistro
	)
	Values
	(
		'TST-00000001', 1, 1, 'VALIDACION_SQL', N'validacion.txt', N'validacion.txt',
		N'/validacion/validacion.txt', 'text/plain', 0, '2099-01-01T00:04:00'
	)

	Insert Into dbo.TI_IncidenciaDocumento (IncidenciaNumero, Secuencia, CompaniaSocio, TipoDocumento, NumeroDocumento, Descripcion)
	Values ('TST-00000001', 1, Null, 'PRUEBA', 'DOC-VALIDACION', N'Documento de validación')

	Insert Into dbo.TI_IncidenciaDiagnostico (
		IncidenciaNumero, Secuencia, Origen, Diagnostico, CausaProbable, SolucionSugerida, Confianza, Estado, UsuarioValida, FechaDiagnostico
	)
	Values
	(
		'TST-00000001', 1, 'T', N'Diagnóstico de validación', N'Causa probable de validación',
		N'Solución sugerida de validación', 100, 'V', 'VALIDACION_SQL', '2099-01-01T00:05:00'
	)

	Insert Into dbo.TI_IncidenciaDiagnosticoEvidencia (IncidenciaNumero, DiagnosticoSecuencia, Secuencia, TipoFuente, Referencia, Descripcion, Similitud)
	Values ('TST-00000001', 1, 1, 'PRUEBA', N'REF-VALIDACION', N'Evidencia de validación', 100)

	Select
			i.IncidenciaNumero, u.NombreCompleto as UsuarioSolicitante, a.Descripcion as AreaSolicitante,
			l.Descripcion as Linea, it.Descripcion as Item, t.Descripcion as Tipo, st.Descripcion as SubTipo,
			c.Descripcion as Categoria, e.Descripcion as Estado
	From dbo.TI_Incidencia as i
	Inner Join dbo.TI_Usuario as u on u.Usuario = i.UsuarioSolicitante
	Inner Join dbo.TI_Area as a on a.Area = i.AreaSolicitante
	Inner Join dbo.TI_Linea as l on l.Linea = i.Linea
	Inner Join dbo.TI_Item as it on it.Linea = i.Linea and it.Item = i.Item
	Inner Join dbo.TI_Tipo as t on t.Tipo = i.Tipo
	Inner Join dbo.TI_SubTipo as st on st.Tipo = i.Tipo and st.SubTipo = i.SubTipo and st.Categoria = i.Categoria
	Inner Join dbo.TI_Categoria as c on c.Categoria = i.Categoria
	Inner Join dbo.TI_Estado as e on e.Estado = i.Estado
	Where i.IncidenciaNumero = 'TST-00000001'

	Rollback Transaction

	Print N'Prueba controlada finalizada. Todos los datos artificiales fueron revertidos.'
End Try
Begin Catch
	If Xact_State() <> 0
	Begin
		Rollback Transaction
	End

	;Throw
End Catch
Go


