/*
	Archivo: 09_InsertDeDatos.sql
	Objetivo: Cargar un conjunto controlado de datos representativos para validar los principales flujos funcionales del Sistema de Tickets Inteligente.
	Responsabilidad: Registrar datos de desarrollo para maestros, incidencias, conocimiento, diagnósticos, acciones, aprobaciones, ejecuciones y auditoría sin utilizar información productiva real.
	Dependencias: Requiere la ejecución previa de 00_CrearBaseDatos.sql hasta 07_DatosIniciales.sql. TI_Perfil se obtiene de 07_DatosIniciales.sql y no se duplica en este archivo.
	Orden: Ejecutar después de crear y validar la estructura de la base de datos.
	Consideraciones: Contiene únicamente datos sintéticos de desarrollo; no debe ejecutarse en producción. Las credenciales, documentos y referencias incluidas no representan información operativa real.
*/

Use [SistemaTicketsInteligente]
Go

Set Xact_Abort on

Begin Try
	Begin Transaction

	Insert dbo.TI_Area (Area, Descripcion, Estado, Telefono, UltimoUsuario, UltimaFechaModif)
	Values
		('TIC', 'Tecnologías de la Información', 'A', Null, Null, Null),
		('COM', 'Comercial', 'A', Null, Null, Null),
		('LOG', 'Logística', 'A', Null, Null, Null),
		('ALM', 'Almacén', 'A', Null, Null, Null),
		('CMP', 'Compras', 'A', Null, Null, Null),
		('PRO', 'Producción', 'A', Null, Null, Null),
		('FIN', 'Finanzas', 'A', Null, Null, Null),
		('CON', 'Contabilidad', 'A', Null, Null, Null),
		('RRH', 'Recursos Humanos', 'A', Null, Null, Null),
		('SCP', 'Supply Chain', 'A', Null, Null, Null)

	Insert dbo.TI_Linea (Linea, Area, Descripcion, Estado, UltimoUsuario, UltimaFechaModif)
	Values
		('SIS', 'TIC', 'Sistemas y Aplicaciones', 'A', Null, Null),
		('INF', 'TIC', 'Infraestructura Tecnológica', 'A', Null, Null),
		('SEG', 'TIC', 'Seguridad y Accesos', 'A', Null, Null),
		('COM', 'COM', 'Gestión Comercial', 'A', Null, Null),
		('CMP', 'CMP', 'Compras y Abastecimiento', 'A', Null, Null),
		('ALM', 'ALM', 'Almacenes e Inventarios', 'A', Null, Null),
		('PRO', 'PRO', 'Producción', 'A', Null, Null),
		('LOG', 'LOG', 'Logística', 'A', Null, Null),
		('SCP', 'SCP', 'Supply Chain', 'A', Null, Null),
		('FIN', 'FIN', 'Finanzas', 'A', Null, Null),
		('CON', 'CON', 'Contabilidad', 'A', Null, Null),
		('RRH', 'RRH', 'Recursos Humanos', 'A', Null, Null)

	Insert dbo.TI_Cargo (Cargo, Descripcion, Estado, UltimoUsuario, UltimaFechaModif)
	Values
		('PRA', 'Practicante', 'A', Null, Null),
		('AST', 'Asistente', 'A', Null, Null),
		('ANA', 'Analista', 'A', Null, Null),
		('COO', 'Coordinador', 'A', Null, Null),
		('JEF', 'Jefe', 'A', Null, Null),
		('GER', 'Gerente', 'A', Null, Null)

	Insert dbo.TI_Perfil (Perfil, Descripcion, Estado, UltimoUsuario, UltimaFechaModif)
	Values
		('USR', 'Usuario', 'A', Null, Null),
		('TEC', 'Técnico TI', 'A', Null, Null),
		('SUP', 'Supervisor TI', 'A', Null, Null),
		('ADM', 'Administrador', 'A', Null, Null)

	-- Los perfiles USR, TEC, SUP y ADM pertenecen a 07_DatosIniciales.sql.
	-- La clave de estos usuarios es un marcador no válido para autenticación.
	Insert dbo.TI_Usuario (Usuario, NombreCompleto, Clave, Area, Cargo, Perfil, Correo, Anexo, Telefono, Estado, UltimoUsuario, UltimaFechaModif, TipoUsuario, Jefe)
	Values
		('SUP001', 'Supervisor TI de Prueba', 'HASH_DEMO_NO_VALIDO', 'TIC', 'JEF', 'SUP', 'supervisor.prueba@example.com', Null, Null, 'A', Null, Null, 'INTERNO', Null),
		('USR001', 'Usuario de Prueba', 'HASH_DEMO_NO_VALIDO', 'CMP', 'ANA', 'USR', 'usuario.prueba@example.com', Null, Null, 'A', Null, Null, 'INTERNO', Null),
		('TEC001', 'Técnico TI de Prueba', 'HASH_DEMO_NO_VALIDO', 'TIC', 'ANA', 'TEC', 'tecnico.prueba@example.com', Null, Null, 'A', Null, Null, 'INTERNO', 'SUP001'),
		('ADM001', 'Administrador de Prueba', 'HASH_DEMO_NO_VALIDO', 'TIC', 'ANA', 'ADM', 'administrador.prueba@example.com', Null, Null, 'A', Null, Null, 'INTERNO', 'SUP001')

	Insert dbo.TI_Item (Item, Linea, Descripcion, Estado, UltimoUsuario, UltimaFechaModif)
	Values
		('REQUISICION', 'CMP', 'Requisiciones', 'A', Null, Null),
		('ORDEN_COMPRA', 'CMP', 'Órdenes de Compra', 'A', Null, Null),
		('PROVEEDOR', 'CMP', 'Proveedores', 'A', Null, Null),
		('PEDIDO_COMERCIAL', 'COM', 'Pedidos Comerciales', 'A', Null, Null),
		('PEDIDO_PT', 'PRO', 'Pedidos de Producto Terminado', 'A', Null, Null),
		('OUTSOURCING', 'PRO', 'Pedidos de Outsourcing', 'A', Null, Null),
		('TRANSACCION', 'ALM', 'Transacciones de Almacén', 'A', Null, Null),
		('STOCK_LOTE', 'ALM', 'Stock y Lotes', 'A', Null, Null),
		('COMPROMISO_STOCK', 'SCP', 'Compromiso de Stock', 'A', Null, Null),
		('APLICACION_ERP', 'SIS', 'Aplicación ERP', 'A', Null, Null),
		('USUARIO_ACCESO', 'SEG', 'Usuarios, Accesos y Permisos', 'A', Null, Null),
		('CONECTIVIDAD', 'INF', 'Conectividad e Infraestructura', 'A', Null, Null)

	Insert dbo.TI_Tipo (Tipo, Descripcion, Abreviatura, Item, Estado, UltimoUsuario, UltimaFechaModif)
	Values
		('INC', 'Incidencia', 'INC', Null, 'A', Null, Null),
		('REQ', 'Requerimiento', 'REQ', Null, 'A', Null, Null),
		('SOL', 'Solicitud', 'SOL', Null, 'A', Null, Null)

	Insert dbo.TI_Categoria (Categoria, Descripcion, Abreviatura, Estado, UltimoUsuario, UltimaFechaModif)
	Values
		('DATOS', 'Datos', 'DAT', 'A', Null, Null),
		('PERMISOS', 'Permisos', 'PER', 'A', Null, Null),
		('INTEGRACION', 'Integración', 'INT', 'A', Null, Null),
		('CONFIGURACION', 'Configuración', 'CFG', 'A', Null, Null),
		('DISPONIBILIDAD', 'Disponibilidad', 'DSP', 'A', Null, Null),
		('PROCESO', 'Proceso', 'PRC', 'A', Null, Null)

	Insert dbo.TI_SubTipo (Tipo, SubTipo, Categoria, Descripcion, Abreviatura, Estado, UltimoUsuario, UltimaFechaModif)
	Values
		('INC', 'DAT', 'DATOS', 'Error de datos', 'ERROR DATOS', 'A', Null, Null),
		('INC', 'PER', 'PERMISOS', 'Problema de permisos', 'ERROR PERMISOS', 'A', Null, Null),
		('INC', 'INT', 'INTEGRACION', 'Error de integración', 'ERROR INTEGRACION', 'A', Null, Null),
		('INC', 'CFG', 'CONFIGURACION', 'Error de configuración', 'ERROR CONFIG', 'A', Null, Null),
		('INC', 'DSP', 'DISPONIBILIDAD', 'Indisponibilidad del servicio', 'NO DISPONIBLE', 'A', Null, Null),
		('INC', 'PRC', 'PROCESO', 'Error de proceso', 'ERROR PROCESO', 'A', Null, Null),
		('REQ', 'NUE', 'PROCESO', 'Nuevo requerimiento funcional', 'NUEVO REQ', 'A', Null, Null),
		('REQ', 'CAM', 'CONFIGURACION', 'Cambio de configuración', 'CAMBIO CONFIG', 'A', Null, Null),
		('REQ', 'INT', 'INTEGRACION', 'Nueva integración', 'NUEVA INTEGRACION', 'A', Null, Null),
		('SOL', 'ACC', 'PERMISOS', 'Acceso o permiso', 'ACCESO', 'A', Null, Null),
		('SOL', 'INF', 'DATOS', 'Solicitud de información', 'INFORMACION', 'A', Null, Null),
		('SOL', 'SER', 'PROCESO', 'Solicitud de servicio', 'SERVICIO', 'A', Null, Null)

	Insert dbo.TI_Estado (Estado, Descripcion, Orden, UltimoUsuario, UltimaFechaModif)
	Values
		('NV', 'Nueva', 1, Null, Null),
		('RC', 'En recopilación', 2, Null, Null),
		('DG', 'En diagnóstico', 3, Null, Null),
		('AU', 'Autoservicio', 4, Null, Null),
		('PA', 'Pendiente de aprobación', 5, Null, Null),
		('EJ', 'En ejecución', 6, Null, Null),
		('PV', 'Pendiente de validación', 7, Null, Null),
		('RS', 'Resuelta', 8, Null, Null),
		('ES', 'Escalada', 9, Null, Null),
		('CA', 'Rechazada / Cancelada', 10, Null, Null),
		('RA', 'Reabierta', 11, Null, Null)

	Insert dbo.TI_ItemCategoria (Item, Categoria, Prioridad, Impacto, Complejidad, Estado, UltimoUsuario, UltimaFechaModif)
	Values
		('REQUISICION', 'DATOS', Null, Null, Null, 'A', Null, Null),
		('REQUISICION', 'PROCESO', Null, Null, Null, 'A', Null, Null),
		('ORDEN_COMPRA', 'DATOS', Null, Null, Null, 'A', Null, Null),
		('ORDEN_COMPRA', 'PROCESO', Null, Null, Null, 'A', Null, Null),
		('PROVEEDOR', 'DATOS', Null, Null, Null, 'A', Null, Null),
		('PROVEEDOR', 'PROCESO', Null, Null, Null, 'A', Null, Null),
		('PEDIDO_COMERCIAL', 'DATOS', Null, Null, Null, 'A', Null, Null),
		('PEDIDO_COMERCIAL', 'PROCESO', Null, Null, Null, 'A', Null, Null),
		('PEDIDO_PT', 'DATOS', Null, Null, Null, 'A', Null, Null),
		('PEDIDO_PT', 'PROCESO', Null, Null, Null, 'A', Null, Null),
		('OUTSOURCING', 'DATOS', Null, Null, Null, 'A', Null, Null),
		('OUTSOURCING', 'INTEGRACION', Null, Null, Null, 'A', Null, Null),
		('OUTSOURCING', 'PROCESO', Null, Null, Null, 'A', Null, Null),
		('TRANSACCION', 'DATOS', Null, Null, Null, 'A', Null, Null),
		('TRANSACCION', 'PROCESO', Null, Null, Null, 'A', Null, Null),
		('STOCK_LOTE', 'DATOS', Null, Null, Null, 'A', Null, Null),
		('STOCK_LOTE', 'PROCESO', Null, Null, Null, 'A', Null, Null),
		('COMPROMISO_STOCK', 'DATOS', Null, Null, Null, 'A', Null, Null),
		('COMPROMISO_STOCK', 'PROCESO', Null, Null, Null, 'A', Null, Null),
		('APLICACION_ERP', 'CONFIGURACION', Null, Null, Null, 'A', Null, Null),
		('APLICACION_ERP', 'DISPONIBILIDAD', Null, Null, Null, 'A', Null, Null),
		('USUARIO_ACCESO', 'PERMISOS', Null, Null, Null, 'A', Null, Null),
		('CONECTIVIDAD', 'DISPONIBILIDAD', Null, Null, Null, 'A', Null, Null)

	Insert dbo.TI_Incidencia (
		IncidenciaNumero, FechaRegistro, UsuarioSolicitante, AreaSolicitante, AreaTI, UsuarioTI, UsuarioAsigno, Linea, Item, Tipo, SubTipo, Categoria, Estado,
		AreaCausante, Titulo, Detalle, MensajeError, FechaAsignacion, FechaAtencion, FechaCierre, SlaObjetivoMinutos, Prioridad, Impacto, Complejidad, CanalRegistro,
		CausaRaiz, SolucionTecnica, RespuestaUsuario, TipoResolucion, Calificacion, ComentarioCalificacion, UltimoUsuario, UltimaFechaModif
	)
	Values
		('INC-000001', '2026-09-01T08:30:00', 'USR001', 'CMP', 'TIC', 'TEC001', 'SUP001', 'CMP', 'ORDEN_COMPRA', 'INC', 'DAT', 'DATOS', 'RS', 'CMP',
		N'Error al generar orden de compra', N'El usuario intenta generar una orden de compra desde una requisición procesada, pero el sistema no permite completar la operación.',
		N'No se pudo generar la orden de compra debido a una inconsistencia en los datos.', '2026-09-01T08:40:00', '2026-09-01T08:50:00', '2026-09-01T10:05:00', Null, Null, Null, Null,
		'CHATBOT', N'Inconsistencia encontrada entre los datos requeridos para generar la orden de compra.', N'Se corrigió la inconsistencia identificada y se validó nuevamente la generación de la orden de compra.',
		N'El usuario confirmó que la orden de compra se generó correctamente.', 'CORRECCION', 5, N'Problema solucionado correctamente.', 'TEC001', '2026-09-01T10:05:00'),
		('INC-000002', '2026-09-02T09:10:00', 'USR001', 'CMP', 'TIC', 'TEC001', 'SUP001', 'CMP', 'REQUISICION', 'INC', 'PRC', 'PROCESO', 'DG', Null,
		N'Error durante el procesamiento de requisición', N'El usuario indica que una requisición no continúa con el flujo normal del proceso.', N'La requisición no puede continuar con el proceso solicitado.',
		'2026-09-02T09:20:00', '2026-09-02T09:30:00', Null, Null, Null, Null, Null, 'PORTAL', Null, Null, Null, Null, Null, Null, 'TEC001', '2026-09-02T09:45:00'),
		('INC-000003', '2026-09-03T10:15:00', 'USR001', 'CMP', 'TIC', 'TEC001', 'SUP001', 'SEG', 'USUARIO_ACCESO', 'SOL', 'ACC', 'PERMISOS', 'PA', Null,
		N'Solicitud de acceso a funcionalidad del sistema', N'El usuario solicita acceso a una funcionalidad del sistema que actualmente no se encuentra disponible para su cuenta.', Null,
		'2026-09-03T10:25:00', '2026-09-03T10:35:00', Null, Null, Null, Null, Null, 'CHATBOT', Null, Null, Null, Null, Null, Null, 'TEC001', '2026-09-03T11:00:00'),
		('INC-000004', '2026-09-04T14:20:00', 'USR001', 'CMP', 'TIC', 'TEC001', 'SUP001', 'ALM', 'STOCK_LOTE', 'INC', 'DAT', 'DATOS', 'ES', 'ALM',
		N'Inconsistencia en stock por lote', N'El usuario detecta que la información disponible del lote no coincide con el stock esperado para continuar el proceso.', N'No existe disponibilidad suficiente para el lote seleccionado.',
		'2026-09-04T14:30:00', '2026-09-04T14:40:00', Null, Null, Null, Null, Null, 'PORTAL', Null, Null, Null, Null, Null, Null, 'TEC001', '2026-09-04T15:30:00'),
		('INC-000005', '2026-09-05T08:15:00', 'USR001', 'CMP', Null, Null, Null, 'INF', 'CONECTIVIDAD', 'INC', 'DSP', 'DISPONIBILIDAD', 'NV', Null,
		N'Problema de conectividad con el sistema', N'El usuario indica que no puede conectarse al sistema desde su equipo de trabajo.', N'No se puede establecer conexión con el servidor.',
		Null, Null, Null, Null, Null, Null, Null, 'CHATBOT', Null, Null, Null, Null, Null, Null, 'USR001', '2026-09-05T08:15:00')

	Insert dbo.TI_IncidenciaEstado (IncidenciaNumero, Secuencia, Estado, UsuarioCambio, FechaCambio, Observacion)
	Values
		('INC-000001', 1, 'NV', 'USR001', '2026-09-01T08:30:00', N'Incidencia registrada por el usuario.'),
		('INC-000001', 2, 'DG', 'TEC001', '2026-09-01T08:50:00', N'Se inicia el diagnóstico de la incidencia.'),
		('INC-000001', 3, 'PV', 'TEC001', '2026-09-01T09:50:00', N'Se solicita al usuario validar nuevamente la operación.'),
		('INC-000001', 4, 'RS', 'TEC001', '2026-09-01T10:05:00', N'El usuario confirma que el problema fue solucionado.'),
		('INC-000002', 1, 'NV', 'USR001', '2026-09-02T09:10:00', N'Incidencia registrada por el usuario.'),
		('INC-000002', 2, 'DG', 'TEC001', '2026-09-02T09:30:00', N'Incidencia actualmente en diagnóstico.'),
		('INC-000003', 1, 'NV', 'USR001', '2026-09-03T10:15:00', N'Solicitud registrada por el usuario.'),
		('INC-000003', 2, 'PA', 'TEC001', '2026-09-03T11:00:00', N'Solicitud pendiente de aprobación.'),
		('INC-000004', 1, 'NV', 'USR001', '2026-09-04T14:20:00', N'Incidencia registrada por el usuario.'),
		('INC-000004', 2, 'DG', 'TEC001', '2026-09-04T14:40:00', N'Se inicia revisión de stock y lotes.'),
		('INC-000004', 3, 'ES', 'TEC001', '2026-09-04T15:30:00', N'Se escala el caso para revisión especializada.'),
		('INC-000005', 1, 'NV', 'USR001', '2026-09-05T08:15:00', N'Incidencia registrada y pendiente de atención.')

	Insert dbo.TI_IncidenciaMensaje (IncidenciaNumero, Secuencia, UsuarioAutor, TipoAutor, Contenido, FechaMensaje, EsInterno)
	Values
		('INC-000001', 1, 'USR001', 'U', N'No puedo generar la orden de compra. Cuando intento procesarla aparece un error.', '2026-09-01T08:30:00', 0),
		('INC-000001', 2, Null, 'I', N'Voy a revisar la información del caso. ¿El error ocurre inmediatamente al intentar generar la orden de compra?', '2026-09-01T08:31:00', 0),
		('INC-000001', 3, 'USR001', 'U', N'Sí. Presiono generar y aparece el mensaje de inconsistencia.', '2026-09-01T08:33:00', 0),
		('INC-000001', 4, 'TEC001', 'T', N'Se identificó una inconsistencia en los datos relacionados con la generación de la orden.', '2026-09-01T09:20:00', 1),
		('INC-000001', 5, 'TEC001', 'T', N'Se realizó la corrección. Por favor intenta generar nuevamente la orden de compra.', '2026-09-01T09:50:00', 0),
		('INC-000001', 6, 'USR001', 'U', N'Ya pude generar la orden correctamente.', '2026-09-01T10:03:00', 0),
		('INC-000002', 1, 'USR001', 'U', N'La requisición no continúa con el proceso. Se queda detenida.', '2026-09-02T09:10:00', 0),
		('INC-000002', 2, Null, 'I', N'El caso será revisado. Se identificó que corresponde al proceso de requisiciones.', '2026-09-02T09:11:00', 0),
		('INC-000002', 3, 'TEC001', 'T', N'Se está revisando el estado actual del documento y sus registros asociados.', '2026-09-02T09:45:00', 0),
		('INC-000003', 1, 'USR001', 'U', N'Necesito acceso a una opción del sistema que actualmente no tengo habilitada.', '2026-09-03T10:15:00', 0),
		('INC-000003', 2, Null, 'I', N'La solicitud fue identificada como una solicitud de acceso y será enviada para validación.', '2026-09-03T10:16:00', 0),
		('INC-000003', 3, 'TEC001', 'T', N'Se verificó la solicitud. Se encuentra pendiente de aprobación antes de realizar cualquier cambio.', '2026-09-03T11:00:00', 0),
		('INC-000004', 1, 'USR001', 'U', N'El sistema indica que no existe stock suficiente para el lote, pero debería tener disponibilidad.', '2026-09-04T14:20:00', 0),
		('INC-000004', 2, 'TEC001', 'T', N'Se está verificando la información de stock, lote y compromiso asociado.', '2026-09-04T14:40:00', 0),
		('INC-000004', 3, 'TEC001', 'T', N'El caso requiere una revisión adicional y será escalado.', '2026-09-04T15:30:00', 0),
		('INC-000005', 1, 'USR001', 'U', N'No puedo ingresar al sistema. Me aparece que no puede conectarse al servidor.', '2026-09-05T08:15:00', 0),
		('INC-000005', 2, Null, 'I', N'He registrado el problema como una incidencia de conectividad. El caso se encuentra pendiente de atención.', '2026-09-05T08:16:00', 0)

	Insert dbo.TI_IncidenciaAvance (IncidenciaNumero, Secuencia, UsuarioTI, FechaAvance, Detalle, TiempoUtilizado, PorcentajeAvance)
	Values
		('INC-000001', 1, 'TEC001', '2026-09-01T09:20:00', N'Se revisaron los datos relacionados con la generación de la orden de compra y se identificó la inconsistencia.', Null, 60),
		('INC-000001', 2, 'TEC001', '2026-09-01T09:50:00', N'Se realizó la corrección y se solicitó validación al usuario.', Null, 100),
		('INC-000002', 1, 'TEC001', '2026-09-02T09:45:00', N'Se inició la revisión del estado de la requisición y sus registros relacionados.', Null, 30),
		('INC-000003', 1, 'TEC001', '2026-09-03T11:00:00', N'Se verificó la solicitud de acceso y fue enviada al flujo de aprobación.', Null, 40),
		('INC-000004', 1, 'TEC001', '2026-09-04T15:00:00', N'Se revisaron los datos iniciales de stock y lote. Se requiere análisis adicional.', Null, 40)

	Insert dbo.TI_IncidenciaAdjunto (IncidenciaNumero, Secuencia, MensajeSecuencia, UsuarioRegistro, NombreOriginal, NombreArchivo, RutaArchivo, TipoMime, TamanoBytes, FechaRegistro)
	Values
		('INC-000001', 1, 1, 'USR001', N'ErrorOrdenCompra.png', N'INC-000001_001.png', N'/incidencias/INC-000001/INC-000001_001.png', 'image/png', 245760, '2026-09-01T08:30:30'),
		('INC-000004', 1, 1, 'USR001', N'ErrorStockLote.png', N'INC-000004_001.png', N'/incidencias/INC-000004/INC-000004_001.png', 'image/png', 184320, '2026-09-04T14:20:30')

	Insert dbo.TI_IncidenciaDocumento (IncidenciaNumero, Secuencia, CompaniaSocio, TipoDocumento, NumeroDocumento, Descripcion)
	Values
		('INC-000001', 1, '01000000', 'OC', 'OC-PRUEBA-001', N'Orden de compra relacionada con la incidencia.'),
		('INC-000002', 1, '01000000', 'REQ', 'REQ-PRUEBA-001', N'Requisición que presenta el problema.'),
		('INC-000004', 1, '01000000', 'LOTE', 'LOTE-PRUEBA-001', N'Lote relacionado con la inconsistencia de stock.')

	Insert dbo.TI_BaseConocimiento (
		ConocimientoCodigo, Titulo, Problema, Sintomas, MensajeError, Causa, Solucion, Procedimiento, Linea, Item, Tipo, SubTipo, Categoria, IncidenciaOrigen,
		Estado, UsuarioValida, FechaCreacion, FechaValidacion, FechaRevision
	)
	Values
		('KB-000001', N'Error al generar orden de compra por inconsistencia de datos', N'El usuario no puede completar la generación de una orden de compra debido a una inconsistencia en la información asociada al documento.',
		N'La generación de la orden de compra se detiene y el sistema informa que los datos requeridos no son consistentes.', N'No se pudo generar la orden de compra debido a una inconsistencia en los datos.',
		N'Existe una inconsistencia en los datos requeridos para continuar con la generación de la orden de compra.', N'Validar los datos asociados al documento, corregir la inconsistencia identificada y volver a ejecutar la generación de la orden de compra.',
		N'1. Identificar el documento afectado. 2. Revisar los datos relacionados. 3. Identificar la inconsistencia. 4. Corregir únicamente el dato afectado. 5. Validar nuevamente la generación.',
		'CMP', 'ORDEN_COMPRA', 'INC', 'DAT', 'DATOS', 'INC-000001', 'A', 'TEC001', '2026-09-01T10:10:00', '2026-09-01T10:20:00', Null),
		('KB-000002', N'Requisición detenida durante el procesamiento', N'Una requisición no continúa con el flujo esperado aunque haya sido registrada correctamente.',
		N'El documento permanece detenido y el usuario no puede continuar con el siguiente paso del proceso.', N'La requisición no puede continuar con el proceso solicitado.',
		N'El estado o alguno de los registros relacionados con la requisición no cumple las condiciones requeridas.', N'Revisar el estado actual de la requisición y sus registros relacionados antes de determinar la corrección correspondiente.',
		N'1. Identificar la requisición. 2. Consultar su estado. 3. Revisar registros relacionados. 4. Identificar la condición que impide continuar. 5. Validar nuevamente el flujo.',
		'CMP', 'REQUISICION', 'INC', 'PRC', 'PROCESO', Null, 'A', 'TEC001', '2026-08-15T09:00:00', '2026-08-15T10:00:00', Null),
		('KB-000003', N'Solicitud de acceso a funcionalidad del sistema', N'El usuario requiere utilizar una funcionalidad para la cual actualmente no cuenta con acceso.',
		N'La opción no se encuentra disponible para el usuario o el sistema informa que no cuenta con los permisos necesarios.', Null,
		N'El usuario no posee el permiso requerido para utilizar la funcionalidad solicitada.', N'Validar que el acceso solicitado corresponda a las funciones del usuario y derivar la solicitud al responsable de aprobación.',
		N'1. Identificar al usuario. 2. Identificar la funcionalidad. 3. Validar perfil y área. 4. Solicitar aprobación. 5. Registrar el resultado.',
		'SEG', 'USUARIO_ACCESO', 'SOL', 'ACC', 'PERMISOS', Null, 'A', 'TEC001', '2026-08-20T11:00:00', '2026-08-20T12:00:00', Null),
		('KB-000004', N'Problema de conectividad con aplicación empresarial', N'El usuario no puede establecer conexión con la aplicación o con el servidor requerido para utilizarla.',
		N'La aplicación no inicia correctamente, pierde conexión o informa que el servidor no se encuentra disponible.', N'No se puede establecer conexión con el servidor.',
		N'Existe una interrupción o problema de conectividad entre el equipo del usuario y el servicio requerido.', N'Validar conectividad del equipo, disponibilidad del servicio y acceso a los recursos necesarios antes de escalar el caso.',
		N'1. Confirmar conectividad. 2. Validar acceso a recursos empresariales. 3. Confirmar disponibilidad del servicio. 4. Escalar a infraestructura si la falla persiste.',
		'INF', 'CONECTIVIDAD', 'INC', 'DSP', 'DISPONIBILIDAD', Null, 'A', 'TEC001', '2026-08-25T08:00:00', '2026-08-25T09:00:00', Null)

	Insert dbo.TI_IncidenciaDiagnostico (IncidenciaNumero, Secuencia, Origen, Diagnostico, CausaProbable, SolucionSugerida, Confianza, Estado, UsuarioValida, FechaDiagnostico)
	Values
		('INC-000001', 1, 'I', N'La incidencia presenta características similares a errores de generación de órdenes de compra asociados a inconsistencias de datos.', N'Posible inconsistencia en los datos requeridos para generar la orden de compra.', N'Revisar la información asociada al documento antes de intentar nuevamente la generación.', 88.00, 'I', Null, '2026-09-01T08:35:00'),
		('INC-000001', 2, 'T', N'Se confirmó una inconsistencia en los datos relacionados con la generación de la orden de compra.', N'Inconsistencia en los datos requeridos por el proceso de generación de la orden de compra.', N'Corregir la inconsistencia identificada y validar nuevamente la generación del documento.', 100.00, 'A', 'TEC001', '2026-09-01T09:20:00'),
		('INC-000002', 1, 'I', N'La requisición se encuentra detenida dentro del flujo normal de procesamiento.', N'El estado actual del documento o alguno de sus registros asociados podría no cumplir las condiciones necesarias para continuar.', N'Consultar el estado de la requisición y revisar sus registros asociados antes de realizar una corrección.', 76.50, 'A', Null, '2026-09-02T09:15:00'),
		('INC-000003', 1, 'I', N'La solicitud corresponde a un requerimiento de acceso a una funcionalidad actualmente no disponible para el usuario.', N'El usuario no posee el permiso requerido para acceder a la funcionalidad solicitada.', N'Validar el perfil del usuario y enviar la solicitud al responsable correspondiente para su aprobación.', 96.00, 'A', 'TEC001', '2026-09-03T10:20:00'),
		('INC-000004', 1, 'I', N'Existe una posible inconsistencia entre la disponibilidad esperada del lote y la información de stock registrada.', N'El lote podría no disponer de la cantidad requerida o existir una diferencia entre stock disponible y stock comprometido.', N'Revisar stock, lote y compromisos asociados antes de realizar cualquier modificación.', 72.00, 'A', Null, '2026-09-04T14:30:00'),
		('INC-000005', 1, 'I', N'El mensaje reportado corresponde a un posible problema de conectividad con el servidor requerido por la aplicación.', N'Interrupción o imposibilidad de establecer comunicación entre el equipo del usuario y el servicio.', N'Validar conectividad, disponibilidad del servicio y acceso a los recursos empresariales.', 84.00, 'A', Null, '2026-09-05T08:17:00')

	Insert dbo.TI_IncidenciaDiagnosticoEvidencia (IncidenciaNumero, DiagnosticoSecuencia, Secuencia, TipoFuente, Referencia, Descripcion, Similitud)
	Values
		('INC-000001', 1, 1, 'BASE_CONOCIMIENTO', N'KB-000001', N'Artículo relacionado con errores de generación de órdenes de compra provocados por inconsistencias de datos.', 94.00),
		('INC-000001', 1, 2, 'MENSAJE', N'INC-000001 / Mensaje 1', N'El usuario informa que no puede generar la orden de compra y que el proceso muestra un error.', Null),
		('INC-000001', 1, 3, 'DOCUMENTO', N'OC-PRUEBA-001', N'Orden de compra relacionada con la incidencia reportada.', Null),
		('INC-000001', 2, 1, 'BASE_CONOCIMIENTO', N'KB-000001', N'El artículo describe la misma clase de inconsistencia encontrada durante la revisión técnica.', 96.00),
		('INC-000001', 2, 2, 'DATOS', N'Revisión técnica OC-PRUEBA-001', N'La revisión realizada por TI confirmó una inconsistencia en la información utilizada para generar la orden de compra.', Null),
		('INC-000002', 1, 1, 'BASE_CONOCIMIENTO', N'KB-000002', N'Artículo relacionado con requisiciones que quedan detenidas durante su procesamiento.', 87.50),
		('INC-000002', 1, 2, 'DOCUMENTO', N'REQ-PRUEBA-001', N'Requisición asociada con el problema reportado.', Null),
		('INC-000003', 1, 1, 'BASE_CONOCIMIENTO', N'KB-000003', N'Artículo relacionado con solicitudes de acceso a funcionalidades del sistema.', 97.00),
		('INC-000004', 1, 1, 'MENSAJE', N'INC-000004 / Mensaje 1', N'El usuario indica que el sistema reporta falta de stock aunque espera que exista disponibilidad.', Null),
		('INC-000004', 1, 2, 'DOCUMENTO', N'LOTE-PRUEBA-001', N'Lote identificado como parte del contexto de la incidencia.', Null),
		('INC-000005', 1, 1, 'BASE_CONOCIMIENTO', N'KB-000004', N'Artículo relacionado con problemas de conectividad entre el usuario y la aplicación empresarial.', 91.00)

	Insert dbo.TI_Accion (AccionCodigo, Nombre, Descripcion, Tipo, NivelRiesgo, RequiereAprobacion, Estado, UltimoUsuario, UltimaFechaModif)
	Values
		('ACC-001', N'Consultar estado de documento', N'Consulta el estado y la información general de un documento empresarial sin realizar modificaciones.', 'L', 'MUY_BAJO', 0, 'A', Null, Null),
		('ACC-002', N'Reprocesar documento', N'Vuelve a ejecutar de forma controlada el procesamiento de un documento que quedó detenido.', 'E', 'MEDIO', 1, 'A', Null, Null),
		('ACC-003', N'Corregir secuencia', N'Corrige de forma controlada una inconsistencia de secuencia previamente identificada y validada.', 'E', 'MEDIO', 1, 'A', Null, Null),
		('ACC-004', N'Liberar registro', N'Libera un registro bloqueado o retenido cuando las validaciones del proceso permiten realizar la operación.', 'E', 'MEDIO', 1, 'A', Null, Null),
		('ACC-005', N'Recalcular total', N'Recalcula los importes o totales de un documento mediante un procedimiento autorizado.', 'E', 'MEDIO', 1, 'A', Null, Null),
		('ACC-006', N'Consultar stock', N'Consulta stock, lote y cantidades comprometidas sin modificar información.', 'L', 'MUY_BAJO', 0, 'A', Null, Null),
		('ACC-007', N'Habilitar acceso', N'Habilita un acceso o permiso previamente solicitado y aprobado para un usuario.', 'E', 'MEDIO', 1, 'A', Null, Null)

	Insert dbo.TI_SolicitudAprobacion (IncidenciaNumero, Secuencia, AccionCodigo, UsuarioSolicitante, UsuarioAprobador, ParametrosJson, Estado, Justificacion, ComentarioRespuesta, FechaSolicitud, FechaRespuesta)
	Values
		('INC-000001', 1, 'ACC-003', 'TEC001', 'SUP001', N'{"companiaSocio":"01000000","tipoDocumento":"OC","numeroDocumento":"OC-PRUEBA-001"}', 'A', N'Se identificó una inconsistencia de secuencia relacionada con la generación de la orden de compra. La corrección requiere aprobación antes de ejecutarse.', N'Se aprueba la corrección debido a que la causa fue validada y la acción se encuentra dentro del alcance autorizado.', '2026-09-01T09:25:00', '2026-09-01T09:35:00'),
		('INC-000002', 1, 'ACC-002', 'TEC001', 'SUP001', N'{"companiaSocio":"01000000","tipoDocumento":"REQ","numeroDocumento":"REQ-PRUEBA-001"}', 'R', N'Se solicita reprocesar la requisición debido a que permanece detenida durante el flujo.', N'No se aprueba el reproceso porque todavía no se ha determinado la causa raíz de la incidencia.', '2026-09-02T09:50:00', '2026-09-02T10:00:00'),
		('INC-000003', 1, 'ACC-007', 'TEC001', 'SUP001', N'{"usuario":"USR001","accesoSolicitado":"FUNCIONALIDAD_PRUEBA"}', 'P', N'El usuario requiere acceso a una funcionalidad que actualmente no posee. Se solicita autorización antes de habilitar el permiso.', Null, '2026-09-03T11:00:00', Null)

	Insert dbo.TI_EjecucionAccion (IncidenciaNumero, Secuencia, AccionCodigo, SolicitudSecuencia, UsuarioEjecutor, ClaveIdempotencia, ParametrosJson, ResultadoJson, Estado, FilasAfectadas, FechaInicio, FechaFin, Error)
	Values
		('INC-000001', 1, 'ACC-001', Null, 'TEC001', '10000000-0000-0000-0000-000000000001', N'{"companiaSocio":"01000000","tipoDocumento":"OC","numeroDocumento":"OC-PRUEBA-001"}', N'{"existe":true,"estado":"PENDIENTE","resultado":"INCONSISTENCIA_DETECTADA"}', 'OK', 0, '2026-09-01T08:55:00', '2026-09-01T08:55:02', Null),
		('INC-000001', 2, 'ACC-003', 1, 'TEC001', '10000000-0000-0000-0000-000000000002', N'{"companiaSocio":"01000000","tipoDocumento":"OC","numeroDocumento":"OC-PRUEBA-001"}', N'{"resultado":"CORRECCION_REALIZADA","validacionPosterior":true}', 'OK', 1, '2026-09-01T09:40:00', '2026-09-01T09:40:03', Null),
		('INC-000002', 1, 'ACC-001', Null, 'TEC001', '20000000-0000-0000-0000-000000000001', N'{"companiaSocio":"01000000","tipoDocumento":"REQ","numeroDocumento":"REQ-PRUEBA-001"}', N'{"existe":true,"estado":"PENDIENTE","resultado":"REQUIERE_ANALISIS"}', 'OK', 0, '2026-09-02T09:35:00', '2026-09-02T09:35:01', Null),
		('INC-000004', 1, 'ACC-006', Null, 'TEC001', '40000000-0000-0000-0000-000000000001', N'{"companiaSocio":"01000000","lote":"LOTE-PRUEBA-001"}', N'{"stockDisponible":10,"stockComprometido":10,"saldoDisponible":0}', 'OK', 0, '2026-09-04T14:45:00', '2026-09-04T14:45:02', Null),
		('INC-000004', 2, 'ACC-006', Null, 'TEC001', '40000000-0000-0000-0000-000000000002', N'{"companiaSocio":"01000000","lote":"LOTE-PRUEBA-001","consulta":"DETALLE_COMPROMISOS"}', Null, 'ER', Null, '2026-09-04T15:05:00', '2026-09-04T15:05:30', N'La consulta excedió el tiempo máximo permitido y no fue posible determinar el resultado.')

	Declare @idCorrelacionInc000001 uniqueidentifier = '10000000-1000-1000-1000-000000000001'
	Declare @idCorrelacionInc000002 uniqueidentifier = '20000000-2000-2000-2000-000000000002'
	Declare @idCorrelacionInc000003 uniqueidentifier = '30000000-3000-3000-3000-000000000003'
	Declare @idCorrelacionInc000004 uniqueidentifier = '40000000-4000-4000-4000-000000000004'
	Declare @idCorrelacionInc000005 uniqueidentifier = '50000000-5000-5000-5000-000000000005'

	Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
	Values
		('INC-000001', 'USR001', 'U', 'TI_Incidencia', 'INC-000001', 'CREACION_INCIDENCIA', 'EXITOSO', N'{"canalRegistro":"CHATBOT","tipo":"INC","item":"ORDEN_COMPRA"}', @idCorrelacionInc000001, '2026-09-01T08:30:00'),
		('INC-000001', Null, 'I', 'TI_IncidenciaDiagnostico', 'INC-000001/1', 'GENERACION_DIAGNOSTICO', 'EXITOSO', N'{"confianza":88.00,"origen":"IA"}', @idCorrelacionInc000001, '2026-09-01T08:35:00'),
		('INC-000001', 'SUP001', 'T', 'TI_Incidencia', 'INC-000001', 'ASIGNACION_TECNICO', 'EXITOSO', N'{"usuarioTI":"TEC001","areaTI":"TIC"}', @idCorrelacionInc000001, '2026-09-01T08:40:00'),
		('INC-000001', 'TEC001', 'T', 'TI_SolicitudAprobacion', 'INC-000001/1', 'SOLICITUD_APROBACION', 'PENDIENTE', N'{"accionCodigo":"ACC-003","nivelRiesgo":"MEDIO"}', @idCorrelacionInc000001, '2026-09-01T09:25:00'),
		('INC-000001', 'SUP001', 'T', 'TI_SolicitudAprobacion', 'INC-000001/1', 'APROBACION_ACCION', 'EXITOSO', N'{"accionCodigo":"ACC-003","estado":"A"}', @idCorrelacionInc000001, '2026-09-01T09:35:00'),
		('INC-000001', 'TEC001', 'T', 'TI_EjecucionAccion', 'INC-000001/2', 'EJECUCION_ACCION', 'EXITOSO', N'{"accionCodigo":"ACC-003","filasAfectadas":1,"validacionPosterior":true}', @idCorrelacionInc000001, '2026-09-01T09:40:03'),
		('INC-000001', 'TEC001', 'T', 'TI_Incidencia', 'INC-000001', 'CIERRE_INCIDENCIA', 'EXITOSO', N'{"estadoFinal":"RS","tipoResolucion":"CORRECCION","calificacion":5}', @idCorrelacionInc000001, '2026-09-01T10:05:00'),
		('INC-000002', 'USR001', 'U', 'TI_Incidencia', 'INC-000002', 'CREACION_INCIDENCIA', 'EXITOSO', N'{"canalRegistro":"PORTAL","item":"REQUISICION"}', @idCorrelacionInc000002, '2026-09-02T09:10:00'),
		('INC-000002', 'SUP001', 'T', 'TI_SolicitudAprobacion', 'INC-000002/1', 'RECHAZO_ACCION', 'RECHAZADO', N'{"accionCodigo":"ACC-002","motivo":"Causa raíz todavía no determinada"}', @idCorrelacionInc000002, '2026-09-02T10:00:00'),
		('INC-000003', 'USR001', 'U', 'TI_Incidencia', 'INC-000003', 'CREACION_INCIDENCIA', 'EXITOSO', N'{"canalRegistro":"CHATBOT","tipo":"SOL","item":"USUARIO_ACCESO"}', @idCorrelacionInc000003, '2026-09-03T10:15:00'),
		('INC-000003', 'TEC001', 'T', 'TI_SolicitudAprobacion', 'INC-000003/1', 'SOLICITUD_APROBACION', 'PENDIENTE', N'{"accionCodigo":"ACC-007","usuario":"USR001"}', @idCorrelacionInc000003, '2026-09-03T11:00:00'),
		('INC-000004', 'USR001', 'U', 'TI_Incidencia', 'INC-000004', 'CREACION_INCIDENCIA', 'EXITOSO', N'{"canalRegistro":"PORTAL","item":"STOCK_LOTE"}', @idCorrelacionInc000004, '2026-09-04T14:20:00'),
		('INC-000004', 'TEC001', 'T', 'TI_EjecucionAccion', 'INC-000004/1', 'CONSULTA_STOCK', 'EXITOSO', N'{"accionCodigo":"ACC-006","stockDisponible":10,"stockComprometido":10,"saldoDisponible":0}', @idCorrelacionInc000004, '2026-09-04T14:45:02'),
		('INC-000004', 'TEC001', 'T', 'TI_EjecucionAccion', 'INC-000004/2', 'CONSULTA_STOCK', 'ERROR', N'{"accionCodigo":"ACC-006","consulta":"DETALLE_COMPROMISOS","error":"TIMEOUT"}', @idCorrelacionInc000004, '2026-09-04T15:05:30'),
		('INC-000004', 'TEC001', 'T', 'TI_IncidenciaEstado', 'INC-000004/3', 'ESCALAMIENTO_INCIDENCIA', 'EXITOSO', N'{"estadoAnterior":"DG","estadoNuevo":"ES"}', @idCorrelacionInc000004, '2026-09-04T15:30:00'),
		('INC-000005', 'USR001', 'U', 'TI_Incidencia', 'INC-000005', 'CREACION_INCIDENCIA', 'EXITOSO', N'{"canalRegistro":"CHATBOT","item":"CONECTIVIDAD"}', @idCorrelacionInc000005, '2026-09-05T08:15:00'),
		('INC-000005', Null, 'I', 'TI_IncidenciaDiagnostico', 'INC-000005/1', 'GENERACION_DIAGNOSTICO', 'EXITOSO', N'{"confianza":84.00,"causaProbable":"Problema de conectividad"}', @idCorrelacionInc000005, '2026-09-05T08:17:00')

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