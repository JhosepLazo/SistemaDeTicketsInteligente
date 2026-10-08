/*
	Archivo: 36_ClasificacionFichaYGuias.sql
	Objetivo: Registrar la clasificación que propone la IA, recopilar los requerimientos con una ficha estructurada y guardar guías
		de diagnóstico que el agente sigue.
	Responsabilidad: Crear el historial de clasificaciones (TI_IncidenciaClasificacion), la plantilla de campos por tipo de ticket
		(TI_PlantillaCampo) con los datos del ticket (TI_IncidenciaDato), la guía de diagnóstico del artículo de conocimiento y los
		procedimientos que los usan; validar la ficha al registrar el ticket y entregar al agente las guías y los atributos de las acciones.
	Dependencias: Requiere 35_ControlAgenteYAutonomia.sql.
	Orden: Ejecutar después de 35_ControlAgenteYAutonomia.sql.
	Consideraciones: La IA solo propone la clasificación; TI la aplica o la corrige con la matriz y ambas quedan en el historial. La ficha de
		requerimientos se carga activa (decisión del 07/10/2026) con los bloques del plan de mejoras; TI ajusta preguntas y obligatoriedad
		desde Configuración TI. La guía de un artículo publicado vuelve a validación al cambiar, igual que su contenido.
*/

Use [GestionSistemas]
Go

Set Xact_Abort On
Set Ansi_Nulls On
Set Ansi_Padding On
Set Ansi_Warnings On
Set ArithAbort On
Set Concat_Null_Yields_Null On
Set Quoted_Identifier On
Set Numeric_RoundAbort Off
Go

/* ============================================================================
   Historial de clasificación
   ============================================================================ */

If Object_Id('dbo.TI_IncidenciaClasificacion', 'U') Is Null
Begin
	Create Table dbo.TI_IncidenciaClasificacion (
		IncidenciaNumero			varchar(12)		Not Null,
		Secuencia					int				Not Null,
		-- I: propuesta de la IA (no cambia el ticket). T: clasificación aplicada por TI con la matriz.
		Origen						char(1)			Not Null,
		Linea						char(3)			Null,
		Item						varchar(20)		Null,
		Tipo						char(3)			Null,
		SubTipo						char(3)			Null,
		Categoria					varchar(20)		Null,
		Prioridad					tinyint			Null,
		Impacto						tinyint			Null,
		Complejidad					tinyint			Null,
		Confianza					decimal(5,2)	Null,
		-- Señales del texto que sustentan la propuesta y preguntas que TI podría hacer al usuario (arreglos JSON de textos).
		Senales						nvarchar(max)	Null,
		Justificacion				nvarchar(1000)	Null,
		PreguntasPendientes			nvarchar(max)	Null,
		Modelo						varchar(60)		Null,
		Usuario						varchar(20)		Not Null,
		FechaClasificacion			datetime2(0)	Not Null,

		Constraint PK_TI_IncidenciaClasificacion Primary Key (IncidenciaNumero, Secuencia),
		Constraint FK_TI_IncidenciaClasificacion_Incidencia Foreign Key (IncidenciaNumero) References dbo.TI_Incidencia (IncidenciaNumero),
		Constraint FK_TI_IncidenciaClasificacion_LineaItem Foreign Key (Linea, Item) References dbo.TI_Item (Linea, Item),
		Constraint FK_TI_IncidenciaClasificacion_TipoSubTipoCategoria Foreign Key (Tipo, SubTipo, Categoria) References dbo.TI_SubTipo (Tipo, SubTipo, Categoria),
		Constraint FK_TI_IncidenciaClasificacion_Usuario Foreign Key (Usuario) References dbo.TI_Usuario (Usuario),
		Constraint CK_TI_IncidenciaClasificacion_Origen Check (Origen In ('I','T')),
		Constraint CK_TI_IncidenciaClasificacion_Niveles Check ((Prioridad Is Null or Prioridad Between 1 and 5) and (Impacto Is Null or Impacto Between 1 and 5)
			and (Complejidad Is Null or Complejidad Between 1 and 5)),
		Constraint CK_TI_IncidenciaClasificacion_Confianza Check (Confianza Is Null or Confianza Between 0 and 100),
		Constraint CK_TI_IncidenciaClasificacion_Json Check ((Senales Is Null or IsJson(Senales) = 1) and (PreguntasPendientes Is Null or IsJson(PreguntasPendientes) = 1))
	)
End
Go

/* ============================================================================
   Ficha estructurada por tipo de ticket
   ============================================================================ */

If Object_Id('dbo.TI_PlantillaCampo', 'U') Is Null
Begin
	Create Table dbo.TI_PlantillaCampo (
		Tipo						char(3)			Not Null,
		Campo						varchar(40)		Not Null,
		Bloque						nvarchar(60)	Not Null,
		Orden						int				Not Null,
		Pregunta					nvarchar(300)	Not Null,
		Ayuda						nvarchar(500)	Null,
		-- TEXTO y TEXTO_LARGO: texto libre; FECHA: aaaa-mm-dd; SI_NO: SI o NO.
		TipoDato					varchar(15)		Not Null,
		Obligatorio					bit				Not Null,
		LongitudMinima				int				Null,
		LongitudMaxima				int				Not Null,
		Estado						varchar(2)		Not Null,
		UltimoUsuario				varchar(20)		Null,
		UltimaFechaModif			datetime2(0)	Null,

		Constraint PK_TI_PlantillaCampo Primary Key (Tipo, Campo),
		Constraint FK_TI_PlantillaCampo_Tipo Foreign Key (Tipo) References dbo.TI_Tipo (Tipo),
		Constraint CK_TI_PlantillaCampo_TipoDato Check (TipoDato In ('TEXTO','TEXTO_LARGO','FECHA','SI_NO')),
		Constraint CK_TI_PlantillaCampo_Longitud Check (LongitudMaxima Between 1 and 4000 and (LongitudMinima Is Null or LongitudMinima Between 0 and LongitudMaxima)),
		Constraint CK_TI_PlantillaCampo_Estado Check (Estado In ('A','I'))
	)
End
Go

If Object_Id('dbo.TI_IncidenciaDato', 'U') Is Null
Begin
	Create Table dbo.TI_IncidenciaDato (
		IncidenciaNumero			varchar(12)		Not Null,
		-- Tipo de la plantilla con que se registró el dato: si el ticket se reclasifica, la ficha conserva su origen.
		Tipo						char(3)			Not Null,
		Campo						varchar(40)		Not Null,
		Valor						nvarchar(4000)	Not Null,
		-- U: lo registró el usuario. T: lo registró TI.
		Fuente						char(1)			Not Null,
		UsuarioRegistro				varchar(20)		Not Null,
		FechaRegistro				datetime2(0)	Not Null,

		Constraint PK_TI_IncidenciaDato Primary Key (IncidenciaNumero, Campo),
		Constraint FK_TI_IncidenciaDato_Incidencia Foreign Key (IncidenciaNumero) References dbo.TI_Incidencia (IncidenciaNumero),
		Constraint FK_TI_IncidenciaDato_Campo Foreign Key (Tipo, Campo) References dbo.TI_PlantillaCampo (Tipo, Campo),
		Constraint FK_TI_IncidenciaDato_Usuario Foreign Key (UsuarioRegistro) References dbo.TI_Usuario (Usuario),
		Constraint CK_TI_IncidenciaDato_Fuente Check (Fuente In ('U','T'))
	)
End
Go

/*
	Ficha de requerimientos (bloques A a F del plan de mejoras). Lo que el sistema ya sabe (usuario, área, cargo y jefatura) no se
	pregunta. Las respuestas cortas o vagas se evitan con un largo mínimo; lo que puede no aplicar es opcional.
*/
If Exists (Select 1 From dbo.TI_Tipo Where Tipo = 'REQ')
	Insert dbo.TI_PlantillaCampo (Tipo, Campo, Bloque, Orden, Pregunta, Ayuda, TipoDato, Obligatorio, LongitudMinima, LongitudMaxima, Estado, UltimoUsuario, UltimaFechaModif)
	Select 'REQ', v.Campo, v.Bloque, v.Orden, v.Pregunta, v.Ayuda, v.TipoDato, v.Obligatorio, v.LongitudMinima, v.LongitudMaxima, 'A', Null, SysDateTime()
	From (Values
		('OBJETIVO_NEGOCIO', N'A. Contexto', 10, N'¿Qué objetivo de negocio busca este requerimiento?', N'Qué se quiere lograr y para qué; por ejemplo, reducir un tiempo o evitar un error.', 'TEXTO_LARGO', 1, 20, 2000),
		('PROBLEMA_OPORTUNIDAD', N'A. Contexto', 20, N'¿Qué problema u oportunidad lo origina?', Null, 'TEXTO_LARGO', 1, 20, 2000),
		('BENEFICIARIOS', N'A. Contexto', 30, N'¿Quiénes se benefician?', N'Áreas, cargos o cantidad aproximada de personas.', 'TEXTO', 1, 3, 500),
		('AREA_PATROCINADORA', N'A. Contexto', 40, N'¿Qué área patrocina el requerimiento?', Null, 'TEXTO', 1, 2, 200),
		('JEFATURA_VALIDO', N'A. Contexto', 50, N'¿Tu jefatura conoce y respalda este requerimiento?', N'Responde SI o NO.', 'SI_NO', 1, Null, 2),
		('PROCESO_ACTUAL', N'B. Situación actual', 60, N'¿Cómo se hace hoy, paso a paso?', Null, 'TEXTO_LARGO', 1, 30, 3000),
		('MODULO_SISTEMA', N'B. Situación actual', 70, N'¿Qué módulo, pantalla o reporte del sistema interviene?', Null, 'TEXTO', 1, 3, 300),
		('DOCUMENTOS_INVOLUCRADOS', N'B. Situación actual', 80, N'¿Qué documentos reales intervienen (tipo y número)?', N'Por ejemplo: OC 0000045334, requisición 123. Si no aplica, déjalo vacío.', 'TEXTO_LARGO', 0, Null, 1000),
		('SOLUCION_PROVISIONAL', N'B. Situación actual', 90, N'¿Existe hoy una solución provisional?', N'Si no existe, déjalo vacío.', 'TEXTO_LARGO', 0, Null, 1000),
		('FRECUENCIA_VOLUMEN', N'B. Situación actual', 100, N'¿Con qué frecuencia ocurre y qué volumen maneja?', N'Por ejemplo: 40 documentos por día.', 'TEXTO', 1, 3, 300),
		('SOLUCION_DESEADA', N'C. Solución deseada', 110, N'Describe la solución que necesitas.', Null, 'TEXTO_LARGO', 1, 30, 3000),
		('REGLAS_NEGOCIO', N'C. Solución deseada', 120, N'¿Qué reglas de negocio debe respetar?', Null, 'TEXTO_LARGO', 1, 10, 2000),
		('ENTRADAS_SALIDAS', N'C. Solución deseada', 130, N'¿Qué datos entran y qué resultado debe salir?', Null, 'TEXTO_LARGO', 1, 10, 2000),
		('PANTALLAS_REPORTES', N'C. Solución deseada', 140, N'¿Qué pantallas, campos o reportes se afectan?', N'Si no lo sabes, déjalo vacío.', 'TEXTO_LARGO', 0, Null, 2000),
		('EXCEPCIONES', N'C. Solución deseada', 150, N'¿Qué excepciones o casos especiales hay?', N'Si no hay, déjalo vacío.', 'TEXTO_LARGO', 0, Null, 2000),
		('EJEMPLO_NORMAL', N'C. Solución deseada', 160, N'Da un ejemplo concreto del caso normal.', Null, 'TEXTO_LARGO', 1, 20, 2000),
		('EJEMPLO_LIMITE', N'C. Solución deseada', 170, N'Da un ejemplo concreto de un caso límite.', N'Por ejemplo: cantidades en cero, documentos anulados o fechas fuera de periodo.', 'TEXTO_LARGO', 1, 20, 2000),
		('USUARIOS_AFECTADOS', N'D. Alcance y restricciones', 180, N'¿Qué usuarios, perfiles o áreas lo usarán?', Null, 'TEXTO', 1, 3, 500),
		('PERMISOS_NECESARIOS', N'D. Alcance y restricciones', 190, N'¿Qué permisos o accesos se necesitan?', N'Si no aplica, déjalo vacío.', 'TEXTO', 0, Null, 500),
		('INTEGRACIONES', N'D. Alcance y restricciones', 200, N'¿Se integra con otro sistema? (sistema, formato y frecuencia)', N'Si no aplica, déjalo vacío.', 'TEXTO_LARGO', 0, Null, 1000),
		('NORMATIVA_AUDITORIA', N'D. Alcance y restricciones', 210, N'¿Hay requisitos normativos o de auditoría?', N'Si no aplica, déjalo vacío.', 'TEXTO_LARGO', 0, Null, 1000),
		('DEPENDENCIAS', N'D. Alcance y restricciones', 220, N'¿Depende de otro proyecto, área o proveedor?', N'Si no aplica, déjalo vacío.', 'TEXTO_LARGO', 0, Null, 1000),
		('CRITERIOS_ACEPTACION', N'E. Aceptación', 230, N'¿Cómo sabremos que quedó bien? (criterios medibles)', N'Por ejemplo: "el reporte cuadra con el kardex en 3 casos de prueba". "Que sea más rápido" no es medible.', 'TEXTO_LARGO', 1, 20, 2000),
		('DATOS_PRUEBA', N'E. Aceptación', 240, N'¿Con qué datos se puede probar?', N'Si no los tienes aún, déjalo vacío.', 'TEXTO_LARGO', 0, Null, 1000),
		('VALIDADOR', N'E. Aceptación', 250, N'¿Quién validará el resultado?', Null, 'TEXTO', 1, 3, 200),
		('IMPACTO', N'F. Priorización', 260, N'¿Qué impacto tiene? (usuarios, dinero o riesgo)', Null, 'TEXTO_LARGO', 1, 10, 1000),
		('FECHA_LIMITE', N'F. Priorización', 270, N'¿Hay una fecha límite?', N'Si no la hay, déjala vacía.', 'FECHA', 0, Null, 10),
		('MOTIVO_FECHA', N'F. Priorización', 280, N'¿Por qué esa fecha?', N'Si no hay fecha límite, déjalo vacío.', 'TEXTO', 0, Null, 500),
		('CONSECUENCIA_NO_HACER', N'F. Priorización', 290, N'¿Qué pasa si no se hace?', Null, 'TEXTO_LARGO', 1, 10, 1000)
	) as v (Campo, Bloque, Orden, Pregunta, Ayuda, TipoDato, Obligatorio, LongitudMinima, LongitudMaxima)
	Where Not Exists (Select 1 From dbo.TI_PlantillaCampo as campo Where campo.Tipo = 'REQ' and campo.Campo = v.Campo)
Go

/* ============================================================================
   Guía de diagnóstico del artículo de conocimiento
   ============================================================================ */

-- Pasos ordenados que el agente sigue: qué revisar, con qué herramienta y qué resultado confirma o descarta cada causa.
If Col_Length('dbo.TI_BaseConocimiento', 'GuiaDiagnosticoJson') Is Null
	Alter Table dbo.TI_BaseConocimiento Add GuiaDiagnosticoJson nvarchar(max) Null
Go
If Not Exists (Select 1 From sys.check_constraints Where name = 'CK_TI_BaseConocimiento_Guia')
	Alter Table dbo.TI_BaseConocimiento Add Constraint CK_TI_BaseConocimiento_Guia Check (GuiaDiagnosticoJson Is Null or IsJson(GuiaDiagnosticoJson) = 1)
Go

/* ============================================================================
   Clasificación propuesta por la IA
   ============================================================================ */

-- Usp_TI_Obtener_DatosClasificacionIA 'TEC001', 'TIC', 'INC-000001'
Create Or Alter Procedure Usp_TI_Obtener_DatosClasificacionIA
/*================================================================================
Objetivo        : Entregar el texto del ticket y los catálogos vigentes para que la IA proponga una clasificación válida.
Creado Por      : Jhosep S. Lazo
Fecha Creación  : Oct 2026
SP Anterior     :
Comentario Cambios  : Solo combinaciones activas: la propuesta se valida contra estos mismos catálogos.
================================================================================*/

@cUsuario			varchar(20),
@cArea				char(3),
@cIncidenciaNumero	varchar(12)

As
Begin
Set NoCount On

	If Not Exists (Select 1 From TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM'))
		Throw 50645, 'El operador TI no es válido.', 1

	Select IncidenciaNumero, Titulo, Detalle, MensajeError = IsNull(MensajeError, N''), Linea, Tipo, Estado, CanalRegistro
	From TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero
	If @@RowCount = 0 Throw 50646, 'El ticket indicado no existe.', 1

	Select Top (10) mensaje.TipoAutor, mensaje.Contenido
	From TI_IncidenciaMensaje as mensaje
	Where mensaje.IncidenciaNumero = @cIncidenciaNumero and mensaje.EsInterno = 0
	Order By mensaje.Secuencia Desc

	Select campo.Pregunta, dato.Valor
	From TI_IncidenciaDato as dato
	Inner Join TI_PlantillaCampo as campo on campo.Tipo = dato.Tipo and campo.Campo = dato.Campo
	Where dato.IncidenciaNumero = @cIncidenciaNumero
	Order By campo.Orden

	Select Linea, Descripcion From TI_Linea Where Estado = 'A' Order By Linea
	Select Item, Linea, Descripcion From TI_Item Where Estado = 'A' Order By Linea, Item

	Select subtipo.Tipo, subtipo.SubTipo, subtipo.Categoria, subtipo.Descripcion, TipoDescripcion = tipo.Descripcion, CategoriaDescripcion = categoria.Descripcion
	From TI_SubTipo as subtipo
	Inner Join TI_Tipo as tipo on tipo.Tipo = subtipo.Tipo and tipo.Estado = 'A'
	Inner Join TI_Categoria as categoria on categoria.Categoria = subtipo.Categoria and categoria.Estado = 'A'
	Where subtipo.Estado = 'A'
	Order By subtipo.Tipo, subtipo.SubTipo

	Select Item, Categoria, Prioridad = Convert(int, Prioridad), Impacto = Convert(int, Impacto), Complejidad = Convert(int, Complejidad)
	From TI_ItemCategoria Where Estado = 'A' and Prioridad Is Not Null and Impacto Is Not Null and Complejidad Is Not Null

End
Go

/* Ejecuta Procedure */

-- Usp_TI_Registrar_ClasificacionPropuesta 'TEC001', 'TIC', 'INC-000001', 'SIS', 'ITEM', 'INC', 'DAT', 'DATOS', 80, N'["error al grabar"]', N'Describe un error al grabar.', N'[]', 'gpt-5-mini', '00000000-0000-0000-0000-000000000001'
Create Or Alter Procedure Usp_TI_Registrar_ClasificacionPropuesta
/*================================================================================
Objetivo        : Guardar la clasificación que propone la IA para que TI la revise; no modifica el ticket.
Creado Por      : Jhosep S. Lazo
Fecha Creación  : Oct 2026
SP Anterior     :
Comentario Cambios  : Cada parte de la propuesta debe ser una combinación activa del catálogo; prioridad, impacto y complejidad
					  salen de la matriz cuando el ítem y la categoría propuestos forman un par configurado.
================================================================================*/

@cUsuario				varchar(20),
@cArea					char(3),
@cIncidenciaNumero		varchar(12),
@cLinea					char(3),
@cItem					varchar(20),
@cTipo					char(3),
@cSubTipo				char(3),
@cCategoria				varchar(20),
@nConfianza				decimal(5,2),
@cSenalesJson			nvarchar(max),
@cJustificacion			nvarchar(1000),
@cPreguntasJson			nvarchar(max),
@cModelo				varchar(60),
@cIdCorrelacion			uniqueidentifier

As
Begin
Set NoCount On
Set Xact_Abort On

	Declare @nSecuencia int, @nPrioridad int, @nImpacto int, @nComplejidad int, @dFecha datetime2(0) = SysDateTime()

	If Not Exists (Select 1 From TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM'))
		Throw 50645, 'El operador TI no es válido.', 1
	If Not Exists (Select 1 From TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero and Estado Not In ('RS','CA','CF','NP'))
		Throw 50647, 'El ticket no existe o ya está cerrado.', 1
	If (@cItem Is Not Null and Not Exists (Select 1 From TI_Item Where Linea = @cLinea and Item = @cItem and Estado = 'A'))
		or (@cLinea Is Not Null and Not Exists (Select 1 From TI_Linea Where Linea = @cLinea and Estado = 'A'))
		Throw 50648, 'La línea o el ítem propuestos no son válidos.', 1
	If @cSubTipo Is Not Null and Not Exists (Select 1 From TI_SubTipo Where Tipo = @cTipo and SubTipo = @cSubTipo and Categoria = @cCategoria and Estado = 'A')
		Throw 50649, 'La combinación propuesta de tipo, subtipo y categoría no es válida.', 1
	If @cTipo Is Not Null and Not Exists (Select 1 From TI_Tipo Where Tipo = @cTipo and Estado = 'A') Throw 50649, 'El tipo propuesto no es válido.', 1
	If (@cSenalesJson Is Not Null and IsJson(@cSenalesJson) = 0) or (@cPreguntasJson Is Not Null and IsJson(@cPreguntasJson) = 0)
		Throw 50650, 'Las señales o preguntas de la propuesta no tienen formato JSON válido.', 1

	Select @nPrioridad = Convert(int, Prioridad), @nImpacto = Convert(int, Impacto), @nComplejidad = Convert(int, Complejidad)
	From TI_ItemCategoria Where Item = @cItem and Categoria = @cCategoria and Estado = 'A'

	Begin Transaction

	Select @nSecuencia = IsNull(Max(Secuencia), 0) + 1 From TI_IncidenciaClasificacion With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero
	Insert TI_IncidenciaClasificacion (IncidenciaNumero, Secuencia, Origen, Linea, Item, Tipo, SubTipo, Categoria, Prioridad, Impacto, Complejidad, Confianza,
		Senales, Justificacion, PreguntasPendientes, Modelo, Usuario, FechaClasificacion)
	Values (@cIncidenciaNumero, @nSecuencia, 'I', @cLinea, @cItem, @cTipo, @cSubTipo, @cCategoria, @nPrioridad, @nImpacto, @nComplejidad, @nConfianza,
		@cSenalesJson, @cJustificacion, @cPreguntasJson, @cModelo, @cUsuario, @dFecha)

	Insert TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
	Values (@cIncidenciaNumero, @cUsuario, 'I', 'TI_IncidenciaClasificacion', Concat(@cIncidenciaNumero, '/', @nSecuencia), 'PROPONER_CLASIFICACION', 'PROPUESTA',
		(Select tipo = @cTipo, subTipo = @cSubTipo, categoria = @cCategoria, item = @cItem, confianza = @nConfianza, modelo = @cModelo For Json Path, Without_Array_Wrapper),
		@cIdCorrelacion, @dFecha)

	Commit Transaction

	Select Secuencia = @nSecuencia

End
Go

/* Ejecuta Procedure */

-- Usp_TI_Obtener_ClasificacionesTicket 'TEC001', 'TIC', 'INC-000001'
Create Or Alter Procedure Usp_TI_Obtener_ClasificacionesTicket
/*================================================================================
Objetivo        : Mostrar el historial de clasificaciones del ticket: propuestas de la IA y clasificaciones aplicadas por TI.
Creado Por      : Jhosep S. Lazo
Fecha Creación  : Oct 2026
SP Anterior     :
Comentario Cambios  :
================================================================================*/

@cUsuario			varchar(20),
@cArea				char(3),
@cIncidenciaNumero	varchar(12)

As
Begin
Set NoCount On

	If Not Exists (Select 1 From TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM'))
		Throw 50645, 'El operador TI no es válido.', 1

	Select clasificacion.Secuencia, clasificacion.Origen, Linea = IsNull(clasificacion.Linea, ''), LineaDescripcion = IsNull(linea.Descripcion, N''),
		Item = IsNull(clasificacion.Item, ''), ItemDescripcion = IsNull(item.Descripcion, N''), Tipo = IsNull(clasificacion.Tipo, ''),
		SubTipo = IsNull(clasificacion.SubTipo, ''), SubTipoDescripcion = IsNull(subtipo.Descripcion, N''), Categoria = IsNull(clasificacion.Categoria, ''),
		clasificacion.Prioridad, clasificacion.Impacto, clasificacion.Complejidad, clasificacion.Confianza,
		Senales = IsNull(clasificacion.Senales, N'[]'), Justificacion = IsNull(clasificacion.Justificacion, N''), PreguntasPendientes = IsNull(clasificacion.PreguntasPendientes, N'[]'),
		Modelo = IsNull(clasificacion.Modelo, ''), clasificacion.Usuario, NombreUsuario = usuario.NombreCompleto, clasificacion.FechaClasificacion
	From TI_IncidenciaClasificacion as clasificacion
	Inner Join TI_Usuario as usuario on usuario.Usuario = clasificacion.Usuario
	Left Join TI_Linea as linea on linea.Linea = clasificacion.Linea
	Left Join TI_Item as item on item.Item = clasificacion.Item
	Left Join TI_SubTipo as subtipo on subtipo.Tipo = clasificacion.Tipo and subtipo.SubTipo = clasificacion.SubTipo and subtipo.Categoria = clasificacion.Categoria
	Where clasificacion.IncidenciaNumero = @cIncidenciaNumero
	Order By clasificacion.Secuencia Desc

End
Go

/* Ejecuta Procedure */

Create Or Alter Procedure dbo.Usp_TI_Clasificar_Ticket
/*================================================================================
Objetivo            : Clasificar una incidencia aplicando la matriz configurada de forma obligatoria.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : dbo.Usp_TI_Clasificar_Ticket (19_MejorasFuncionalesSinIA.sql)
Comentario Cambios  : Prioridad, impacto y complejidad dejan de depender de selección manual y se obtienen de TI_ItemCategoria.
					  07/10/2026 Registra la clasificación aplicada en TI_IncidenciaClasificacion (origen T) en la misma transacción.
================================================================================*/
	@cUsuario varchar(20), @cArea char(3), @cIncidenciaNumero varchar(12), @cLinea char(3), @cItem varchar(20),
	@cTipo char(3), @cSubTipo char(3), @cCategoria varchar(20), @cAreaCausante char(3) = Null,
	@nPrioridad int = Null, @nImpacto int = Null, @nComplejidad int = Null, @cIdCorrelacion uniqueidentifier
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Declare @nPrioridadMatriz int, @nImpactoMatriz int, @nComplejidadMatriz int, @nSla int, @nSecuencia int, @dFecha datetime2(0) = SysDateTime()

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

	Begin Transaction

	Update dbo.TI_Incidencia
	Set AreaTI = @cArea, Linea = @cLinea, Item = @cItem, Tipo = @cTipo, SubTipo = @cSubTipo, Categoria = @cCategoria,
		AreaCausante = @cAreaCausante, Prioridad = @nPrioridadMatriz, Impacto = @nImpactoMatriz, Complejidad = @nComplejidadMatriz,
		SlaObjetivoMinutos = @nSla, UltimoUsuario = @cUsuario, UltimaFechaModif = @dFecha
	Where IncidenciaNumero = @cIncidenciaNumero

	Select @nSecuencia = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_IncidenciaClasificacion With (UpdLock, HoldLock) Where IncidenciaNumero = @cIncidenciaNumero
	Insert dbo.TI_IncidenciaClasificacion (IncidenciaNumero, Secuencia, Origen, Linea, Item, Tipo, SubTipo, Categoria, Prioridad, Impacto, Complejidad, Confianza,
		Senales, Justificacion, PreguntasPendientes, Modelo, Usuario, FechaClasificacion)
	Values (@cIncidenciaNumero, @nSecuencia, 'T', @cLinea, @cItem, @cTipo, @cSubTipo, @cCategoria, @nPrioridadMatriz, @nImpactoMatriz, @nComplejidadMatriz, Null,
		Null, Null, Null, Null, @cUsuario, @dFecha)

	Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
	Values (@cIncidenciaNumero, @cUsuario, 'T', 'TI_Incidencia', @cIncidenciaNumero, 'CLASIFICAR_TICKET', 'EXITOSO', Concat('{"prioridad":', @nPrioridadMatriz, ',"impacto":', @nImpactoMatriz, ',"complejidad":', @nComplejidadMatriz, '}'), @cIdCorrelacion, @dFecha)

	Commit Transaction
End
Go

/* ============================================================================
   Ficha del ticket
   ============================================================================ */

-- Usp_TI_Obtener_PlantillaFicha 'REQ'
Create Or Alter Procedure Usp_TI_Obtener_PlantillaFicha
/*================================================================================
Objetivo        : Entregar los campos activos de la ficha para el formulario de registro (todos los tipos o uno).
Creado Por      : Jhosep S. Lazo
Fecha Creación  : Oct 2026
SP Anterior     :
Comentario Cambios  :
================================================================================*/

@cTipo	char(3) = Null

As
Begin
Set NoCount On

	Select Tipo, Campo, Bloque, Orden, Pregunta, Ayuda = IsNull(Ayuda, N''), TipoDato, Obligatorio, LongitudMinima = IsNull(LongitudMinima, 0), LongitudMaxima
	From TI_PlantillaCampo
	Where Estado = 'A' and (@cTipo Is Null or Tipo = @cTipo)
	Order By Tipo, Orden, Campo

End
Go

/* Ejecuta Procedure */

-- Usp_TI_Obtener_PlantillasTI 'TEC001', 'TIC'
Create Or Alter Procedure Usp_TI_Obtener_PlantillasTI
/*================================================================================
Objetivo        : Mostrar a TI todos los campos de las fichas, activos e inactivos, para administrarlos.
Creado Por      : Jhosep S. Lazo
Fecha Creación  : Oct 2026
SP Anterior     :
Comentario Cambios  :
================================================================================*/

@cUsuario	varchar(20),
@cArea		char(3)

As
Begin
Set NoCount On

	If Not Exists (Select 1 From TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM'))
		Throw 50645, 'El operador TI no es válido.', 1

	Select campo.Tipo, TipoDescripcion = tipo.Descripcion, campo.Campo, campo.Bloque, campo.Orden, campo.Pregunta, Ayuda = IsNull(campo.Ayuda, N''),
		campo.TipoDato, campo.Obligatorio, LongitudMinima = IsNull(campo.LongitudMinima, 0), campo.LongitudMaxima, campo.Estado,
		UltimoUsuario = IsNull(campo.UltimoUsuario, ''), campo.UltimaFechaModif
	From TI_PlantillaCampo as campo
	Inner Join TI_Tipo as tipo on tipo.Tipo = campo.Tipo
	Order By campo.Tipo, campo.Orden, campo.Campo

End
Go

/* Ejecuta Procedure */

-- Usp_TI_Guardar_CampoPlantilla 'ADM001', 'TIC', 'REQ', 'OBJETIVO_NEGOCIO', N'A. Contexto', 10, N'¿Qué objetivo busca?', Null, 'TEXTO_LARGO', 1, 20, 2000, 'A', '00000000-0000-0000-0000-000000000001'
Create Or Alter Procedure Usp_TI_Guardar_CampoPlantilla
/*================================================================================
Objetivo        : Crear o modificar un campo de la ficha de un tipo de ticket.
Creado Por      : Jhosep S. Lazo
Fecha Creación  : Oct 2026
SP Anterior     :
Comentario Cambios  : Solo ADM. Un campo no se elimina: se inactiva, para conservar las fichas ya registradas.
================================================================================*/

@cUsuario			varchar(20),
@cArea				char(3),
@cTipo				char(3),
@cCampo				varchar(40),
@cBloque			nvarchar(60),
@nOrden				int,
@cPregunta			nvarchar(300),
@cAyuda				nvarchar(500),
@cTipoDato			varchar(15),
@lObligatorio		bit,
@nLongitudMinima	int,
@nLongitudMaxima	int,
@cEstado			varchar(2),
@cIdCorrelacion		uniqueidentifier

As
Begin
Set NoCount On
Set Xact_Abort On

	Declare @dFecha datetime2(0) = SysDateTime()

	If Not Exists (Select 1 From TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil = 'ADM')
		Throw 50651, 'Solo un administrador puede modificar las fichas.', 1
	If Not Exists (Select 1 From TI_Tipo Where Tipo = @cTipo and Estado = 'A') Throw 50652, 'El tipo de ticket no es válido.', 1
	If @cCampo Not Like '[A-Z]%' or @cCampo Like '%[^A-Z0-9_]%' Throw 50653, 'El código del campo solo admite mayúsculas, números y guion bajo, y empieza con una letra.', 1
	If @cTipoDato = 'SI_NO' Set @nLongitudMaxima = 2
	If @cTipoDato = 'FECHA' Set @nLongitudMaxima = 10
	If @cTipoDato In ('SI_NO','FECHA') Set @nLongitudMinima = Null

	Begin Transaction

	If Exists (Select 1 From TI_PlantillaCampo With (UpdLock, HoldLock) Where Tipo = @cTipo and Campo = @cCampo)
		Update TI_PlantillaCampo
		Set Bloque = @cBloque, Orden = @nOrden, Pregunta = @cPregunta, Ayuda = NullIf(LTrim(RTrim(@cAyuda)), N''), TipoDato = @cTipoDato,
			Obligatorio = @lObligatorio, LongitudMinima = NullIf(@nLongitudMinima, 0), LongitudMaxima = @nLongitudMaxima, Estado = @cEstado,
			UltimoUsuario = @cUsuario, UltimaFechaModif = @dFecha
		Where Tipo = @cTipo and Campo = @cCampo
	Else
		Insert TI_PlantillaCampo (Tipo, Campo, Bloque, Orden, Pregunta, Ayuda, TipoDato, Obligatorio, LongitudMinima, LongitudMaxima, Estado, UltimoUsuario, UltimaFechaModif)
		Values (@cTipo, @cCampo, @cBloque, @nOrden, @cPregunta, NullIf(LTrim(RTrim(@cAyuda)), N''), @cTipoDato, @lObligatorio, NullIf(@nLongitudMinima, 0), @nLongitudMaxima,
			@cEstado, @cUsuario, @dFecha)

	Insert TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
	Values (Null, @cUsuario, 'T', 'TI_PlantillaCampo', Concat(@cTipo, '/', @cCampo), 'GUARDAR_CAMPO_FICHA', 'EXITOSO',
		(Select obligatorio = @lObligatorio, estado = @cEstado, tipoDato = @cTipoDato For Json Path, Without_Array_Wrapper), @cIdCorrelacion, @dFecha)

	Commit Transaction

End
Go

/* Ejecuta Procedure */

-- Usp_TI_Obtener_FichaIncidencia 'USR001', 'COM', 'INC-000001'
Create Or Alter Procedure Usp_TI_Obtener_FichaIncidencia
/*================================================================================
Objetivo        : Mostrar la ficha registrada con el ticket a su solicitante o a un operador TI.
Creado Por      : Jhosep S. Lazo
Fecha Creación  : Oct 2026
SP Anterior     :
Comentario Cambios  :
================================================================================*/

@cUsuario			varchar(20),
@cArea				char(3),
@cIncidenciaNumero	varchar(12)

As
Begin
Set NoCount On

	If Not Exists (Select 1 From TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero and UsuarioSolicitante = @cUsuario)
		and Not Exists (Select 1 From TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM'))
		Throw 50654, 'El ticket no existe o no tienes acceso a su ficha.', 1

	Select dato.Campo, campo.Bloque, campo.Orden, campo.Pregunta, campo.TipoDato, dato.Valor, dato.Fuente, dato.FechaRegistro
	From TI_IncidenciaDato as dato
	Inner Join TI_PlantillaCampo as campo on campo.Tipo = dato.Tipo and campo.Campo = dato.Campo
	Where dato.IncidenciaNumero = @cIncidenciaNumero
	Order By campo.Orden, dato.Campo

End
Go

/* Ejecuta Procedure */

Create Or Alter Procedure dbo.Usp_TI_Registrar_Incidencia
/*================================================================================
Objetivo            : Registrar una incidencia nueva utilizando únicamente datos permitidos al usuario.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : dbo.Usp_TI_Registrar_Incidencia (19_MejorasFuncionalesSinIA.sql)
Comentario Cambios  : Guarda UsuarioRegistro para distinguir el origen sin alterar el contrato del módulo existente.
					  07/10/2026 Canal ASISTENTE cuando el ticket llega con la evidencia mostrada al Asistente TI, y ficha del tipo de
					  ticket: si el tipo tiene campos activos, los obligatorios deben venir completos y válidos.
================================================================================*/
	@cUsuario varchar(20), @cLinea char(3), @cTipo char(3), @cTitulo nvarchar(250), @cDetalle nvarchar(max), @cMensajeError nvarchar(1000) = Null, @cIdCorrelacion uniqueidentifier,
	@cCanalRegistro varchar(20) = 'PORTAL', @cFichaJson nvarchar(max) = Null
As
Begin
	Set NoCount On
	Set Xact_Abort On

	Declare @cArea char(3), @cIncidenciaNumero varchar(12), @nCorrelativo int, @cCampoInvalido nvarchar(300), @cMensajeFicha nvarchar(400), @dFecha datetime2(0) = SysDateTime()
	Declare @tFicha table (Campo varchar(40) Primary Key, Valor nvarchar(max) Not Null)
	Select @cArea = Area From dbo.TI_Usuario Where Usuario = @cUsuario and Estado = 'A'

	If @cArea Is Null Throw 50001, 'El usuario autenticado no se encuentra habilitado.', 1
	If Not Exists (Select 1 From dbo.TI_Linea Where Linea = @cLinea and Estado = 'A') Throw 50002, 'El sistema o módulo seleccionado no se encuentra disponible.', 1
	If Not Exists (Select 1 From dbo.TI_Tipo Where Tipo = @cTipo and Estado = 'A') Throw 50003, 'El tipo de ticket seleccionado no se encuentra disponible.', 1
	If NullIf(LTrim(RTrim(@cTitulo)), '') Is Null Throw 50004, 'El título del problema es obligatorio.', 1
	If NullIf(LTrim(RTrim(@cDetalle)), '') Is Null Throw 50005, 'La descripción detallada es obligatoria.', 1
	If IsNull(@cCanalRegistro, '') Not In ('PORTAL','ASISTENTE') Throw 50640, 'El canal de registro no es válido para el portal.', 1

	-- Ficha: un objeto JSON con un texto por campo de la plantilla activa del tipo.
	If @cFichaJson Is Not Null and (IsJson(@cFichaJson) = 0 or Left(LTrim(@cFichaJson), 1) <> '{') Throw 50641, 'La ficha del ticket no tiene un formato válido.', 1
	If Exists (Select 1 From OpenJson(IsNull(@cFichaJson, N'{}')) Where [type] Not In (0, 1) or Len([key]) Not Between 1 and 40)
		or Exists (Select Upper([key]) From OpenJson(IsNull(@cFichaJson, N'{}')) Group By Upper([key]) Having Count(*) > 1)
		Throw 50641, 'La ficha del ticket no tiene un formato válido.', 1
	Insert @tFicha (Campo, Valor)
	Select Upper(LTrim(RTrim([key]))), LTrim(RTrim([value]))
	From OpenJson(IsNull(@cFichaJson, N'{}'))
	Where [type] = 1 and NullIf(LTrim(RTrim([value])), N'') Is Not Null

	If Exists (Select 1 From @tFicha as dato Where Not Exists (Select 1 From dbo.TI_PlantillaCampo as campo Where campo.Tipo = @cTipo and campo.Campo = dato.Campo and campo.Estado = 'A'))
		Throw 50642, 'La ficha contiene campos que no corresponden al tipo de ticket.', 1
	Select Top (1) @cCampoInvalido = campo.Pregunta
	From dbo.TI_PlantillaCampo as campo
	Left Join @tFicha as dato on dato.Campo = campo.Campo
	Where campo.Tipo = @cTipo and campo.Estado = 'A' and campo.Obligatorio = 1 and dato.Campo Is Null
	Order By campo.Orden
	If @cCampoInvalido Is Not Null
	Begin
		Set @cMensajeFicha = Left(Concat(N'Completa la ficha: ', @cCampoInvalido), 400)
		;Throw 50643, @cMensajeFicha, 1
	End
	Select Top (1) @cCampoInvalido = campo.Pregunta
	From dbo.TI_PlantillaCampo as campo
	Inner Join @tFicha as dato on dato.Campo = campo.Campo
	Where campo.Tipo = @cTipo
		and (Len(dato.Valor) < IsNull(campo.LongitudMinima, 0) or Len(dato.Valor) > campo.LongitudMaxima
			or (campo.TipoDato = 'SI_NO' and dato.Valor Not In ('SI','NO'))
			or (campo.TipoDato = 'FECHA' and Try_Convert(date, dato.Valor, 23) Is Null))
	Order By campo.Orden
	If @cCampoInvalido Is Not Null
	Begin
		Set @cMensajeFicha = Left(Concat(N'Revisa la respuesta de la ficha: ', @cCampoInvalido), 400)
		;Throw 50644, @cMensajeFicha, 1
	End

	Begin Try
		Begin Transaction
		Select @nCorrelativo = IsNull(Max(Try_Convert(int, Right(IncidenciaNumero, 6))), 0) + 1 From dbo.TI_Incidencia With (UpdLock, HoldLock) Where IncidenciaNumero Like 'INC-[0-9][0-9][0-9][0-9][0-9][0-9]'
		If @nCorrelativo > 999999 Throw 50006, 'Se alcanzó el límite del correlativo de incidencias.', 1
		Set @cIncidenciaNumero = 'INC-' + Right('000000' + Convert(varchar(6), @nCorrelativo), 6)

		Insert dbo.TI_Incidencia (IncidenciaNumero, FechaRegistro, UsuarioSolicitante, UsuarioRegistro, AreaSolicitante, Linea, Tipo, Estado, Titulo, Detalle, MensajeError, CanalRegistro, UltimoUsuario, UltimaFechaModif)
		Values (@cIncidenciaNumero, @dFecha, @cUsuario, @cUsuario, @cArea, @cLinea, @cTipo, 'NV', LTrim(RTrim(@cTitulo)), LTrim(RTrim(@cDetalle)), NullIf(LTrim(RTrim(@cMensajeError)), ''), @cCanalRegistro, @cUsuario, @dFecha)

		Insert dbo.TI_IncidenciaEstado (IncidenciaNumero, Secuencia, Estado, UsuarioCambio, FechaCambio, Observacion)
		Values (@cIncidenciaNumero, 1, 'NV', @cUsuario, @dFecha,
			Case When @cCanalRegistro = 'ASISTENTE' Then N'Ticket registrado por el usuario desde el Asistente TI, con la evidencia mostrada en pantalla.' Else N'Ticket registrado por el usuario desde el portal.' End)

		Insert dbo.TI_IncidenciaDato (IncidenciaNumero, Tipo, Campo, Valor, Fuente, UsuarioRegistro, FechaRegistro)
		Select @cIncidenciaNumero, @cTipo, Campo, Valor, 'U', @cUsuario, @dFecha From @tFicha

		Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
		Values (@cIncidenciaNumero, @cUsuario, 'U', 'TI_Incidencia', @cIncidenciaNumero, 'CREAR_TICKET', 'EXITOSO',
			(Select canal = @cCanalRegistro, camposFicha = (Select Count(*) From @tFicha) For Json Path, Without_Array_Wrapper), @cIdCorrelacion, @dFecha)

		Commit Transaction
	End Try
	Begin Catch
		If Xact_State() <> 0 Rollback Transaction
		;Throw
	End Catch

	Select IncidenciaNumero = @cIncidenciaNumero, FechaRegistro = @dFecha
End
Go

Create Or Alter Procedure dbo.Usp_TI_Editar_TicketUsuario
/*================================================================================
Objetivo            : Permitir corregir datos básicos del ticket propio antes de que la atención técnica avance.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 13/09/2026
SP Anterior         : dbo.Usp_TI_Editar_TicketUsuario (19_MejorasFuncionalesSinIA.sql)
Comentario Cambios  : Solo admite estados NV/RC y nunca expone clasificación técnica, prioridad ni responsable.
					  07/10/2026 No permite cambiar a un tipo con ficha obligatoria si el ticket no la tiene completa.
================================================================================*/
	@cUsuario varchar(20), @cIncidenciaNumero varchar(12), @cLinea char(3), @cTipo char(3), @cTitulo nvarchar(250), @cDetalle nvarchar(max), @cMensajeError nvarchar(1000) = Null, @cIdCorrelacion uniqueidentifier
As
Begin
	Set NoCount On
	Set Xact_Abort On

	If Not Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero and UsuarioSolicitante = @cUsuario and Estado In ('NV','RC')) Throw 50430, 'El ticket ya fue tomado por TI o no pertenece al usuario autenticado.', 1
	If Not Exists (Select 1 From dbo.TI_Linea Where Linea = @cLinea and Estado = 'A') Throw 50431, 'La línea seleccionada no es válida.', 1
	If Not Exists (Select 1 From dbo.TI_Tipo Where Tipo = @cTipo and Estado = 'A') Throw 50432, 'El tipo seleccionado no es válido.', 1
	If @cTipo = 'REQ' and Not Exists (Select 1 From dbo.TI_IncidenciaAdjunto Where IncidenciaNumero = @cIncidenciaNumero) Throw 50433, 'Un requerimiento debe conservar al menos un archivo de sustento.', 1
	If Not Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero and Tipo = @cTipo)
		and Exists (Select 1 From dbo.TI_PlantillaCampo as campo
			Where campo.Tipo = @cTipo and campo.Estado = 'A' and campo.Obligatorio = 1
				and Not Exists (Select 1 From dbo.TI_IncidenciaDato as dato Where dato.IncidenciaNumero = @cIncidenciaNumero and dato.Campo = campo.Campo))
		Throw 50655, 'Ese tipo de ticket exige completar su ficha: registra un ticket nuevo con la ficha completa.', 1

	Begin Transaction

	Update dbo.TI_Incidencia
	Set Linea = @cLinea, Tipo = @cTipo, Titulo = LTrim(RTrim(@cTitulo)), Detalle = LTrim(RTrim(@cDetalle)), MensajeError = NullIf(LTrim(RTrim(@cMensajeError)), ''),
		UltimoUsuario = @cUsuario, UltimaFechaModif = SysDateTime()
	Where IncidenciaNumero = @cIncidenciaNumero and UsuarioSolicitante = @cUsuario

	Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
	Values (@cIncidenciaNumero, @cUsuario, 'U', 'TI_Incidencia', @cIncidenciaNumero, 'EDITAR_TICKET_PREVIO', 'EXITOSO', Null, @cIdCorrelacion, SysDateTime())

	Commit Transaction
End
Go

/* ============================================================================
   Guías de diagnóstico
   ============================================================================ */

-- Usp_TI_Obtener_GuiaDiagnostico 'KB-000001'
Create Or Alter Procedure Usp_TI_Obtener_GuiaDiagnostico
/*================================================================================
Objetivo        : Entregar la guía de diagnóstico de un artículo de conocimiento.
Creado Por      : Jhosep S. Lazo
Fecha Creación  : Oct 2026
SP Anterior     :
Comentario Cambios  :
================================================================================*/

@cConocimientoCodigo	varchar(20)

As
Begin
Set NoCount On

	Select GuiaDiagnosticoJson = IsNull(GuiaDiagnosticoJson, N'') From TI_BaseConocimiento Where ConocimientoCodigo = @cConocimientoCodigo

End
Go

/* Ejecuta Procedure */

-- Usp_TI_Guardar_GuiaDiagnostico 'TEC001', 'KB-000001', N'[{"paso":"Revisar el historial del ticket","herramienta":"DIAG_HISTORIAL_TICKET","confirma":"","descarta":""}]', '00000000-0000-0000-0000-000000000001'
Create Or Alter Procedure Usp_TI_Guardar_GuiaDiagnostico
/*================================================================================
Objetivo        : Guardar los pasos de diagnóstico de un artículo: qué revisar, con qué herramienta y qué confirma o descarta.
Creado Por      : Jhosep S. Lazo
Fecha Creación  : Oct 2026
SP Anterior     :
Comentario Cambios  : Igual que el contenido: un artículo publicado vuelve a validación y uno inactivo a borrador, para que el agente
					  solo siga guías validadas. Una guía vacía elimina la guía.
================================================================================*/

@cUsuario				varchar(20),
@cConocimientoCodigo	varchar(20),
@cGuiaJson				nvarchar(max),
@cIdCorrelacion			uniqueidentifier

As
Begin
Set NoCount On
Set Xact_Abort On

	Declare @cEstado varchar(2), @cNuevoEstado varchar(2), @dFecha datetime2(0) = SysDateTime()

	If Not Exists (Select 1 From TI_Usuario Where Usuario = @cUsuario and Estado = 'A' and Perfil In ('TEC','SUP','ADM')) Throw 50656, 'El operador TI no es válido.', 1
	Select @cEstado = Estado From TI_BaseConocimiento Where ConocimientoCodigo = @cConocimientoCodigo
	If @cEstado Is Null Throw 50657, 'El artículo de conocimiento no existe.', 1
	Set @cGuiaJson = NullIf(LTrim(RTrim(@cGuiaJson)), N'')
	If @cGuiaJson Is Not Null
	Begin
		If IsJson(@cGuiaJson) = 0 or Left(@cGuiaJson, 1) <> '[' Throw 50658, 'La guía debe ser una lista de pasos.', 1
		If (Select Count(*) From OpenJson(@cGuiaJson)) Not Between 1 and 15 or Exists (Select 1 From OpenJson(@cGuiaJson) Where [type] <> 5)
			Throw 50658, 'La guía debe tener entre 1 y 15 pasos.', 1
		If Exists (Select 1 From OpenJson(@cGuiaJson) With (paso nvarchar(max) '$.paso', confirma nvarchar(max) '$.confirma', descarta nvarchar(max) '$.descarta')
			Where Len(LTrim(RTrim(IsNull(paso, N'')))) Not Between 5 and 500 or Len(IsNull(confirma, N'')) > 500 or Len(IsNull(descarta, N'')) > 500)
			Throw 50659, 'Cada paso necesita una descripción de 5 a 500 caracteres; lo que confirma o descarta admite hasta 500.', 1
		If Exists (Select 1 From OpenJson(@cGuiaJson) With (herramienta varchar(40) '$.herramienta') as guia
			Where NullIf(guia.herramienta, '') Is Not Null and Not Exists (Select 1 From TI_AgenteHerramienta Where HerramientaCodigo = guia.herramienta and Estado = 'A'))
			Throw 50660, 'Una herramienta de la guía no existe o no está activa en el catálogo del agente.', 1
	End

	Set @cNuevoEstado = Case When @cEstado = 'A' Then 'P' When @cEstado = 'I' Then 'B' Else @cEstado End

	-- Guardar el artículo sin tocar la guía no la vuelve a validación ni deja auditoría.
	If IsNull(@cGuiaJson, N'') <> IsNull((Select GuiaDiagnosticoJson From TI_BaseConocimiento Where ConocimientoCodigo = @cConocimientoCodigo), N'')
	Begin
		Begin Transaction

		Update TI_BaseConocimiento
		Set GuiaDiagnosticoJson = @cGuiaJson, Estado = @cNuevoEstado,
			UsuarioValida = Case When @cNuevoEstado In ('B','P') Then Null Else UsuarioValida End,
			FechaValidacion = Case When @cNuevoEstado In ('B','P') Then Null Else FechaValidacion End,
			FechaRevision = @dFecha
		Where ConocimientoCodigo = @cConocimientoCodigo

		Insert TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
		Values (Null, @cUsuario, 'T', 'TI_BaseConocimiento', @cConocimientoCodigo, 'GUARDAR_GUIA_DIAGNOSTICO', 'EXITOSO',
			(Select estadoAnterior = @cEstado, estadoNuevo = @cNuevoEstado, pasos = (Select Count(*) From OpenJson(IsNull(@cGuiaJson, N'[]'))) For Json Path, Without_Array_Wrapper),
			@cIdCorrelacion, @dFecha)

		Commit Transaction
	End

End
Go

/* Ejecuta Procedure */

/* ============================================================================
   Contexto del agente con guías y atributos de las acciones
   ============================================================================ */

Create Or Alter Procedure dbo.Usp_TI_Agente_ObtenerContexto
/*================================================================================
Objetivo            : Recuperar únicamente el contexto autorizado necesario para investigar una sesión.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 01/10/2026
SP Anterior         : dbo.Usp_TI_Agente_ObtenerContexto (33_AgenteFase6Mejoras.sql)
Comentario Cambios  : 02/10/2026 SUP/ADM pueden consultar investigaciones de otros operadores en modo lectura (EsPropietario = 0).
					  02/10/2026 Devuelve el estado de la invitación de reproducción al usuario final.
					  02/10/2026 El responsable del ticket también puede consultarla y tomarla (PuedeTomar).
					  07/10/2026 Devuelve la guía de diagnóstico de los artículos y, de cada acción, si es reversible y el esquema
					  de parámetros de su ejecutor.
================================================================================*/
	@cUsuario varchar(20),
	@cArea char(3),
	@nSesionNumero bigint
As
Begin
	Set NoCount On

	Declare @cPerfil char(3)
	Select @cPerfil = Perfil From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM')
	If @cPerfil Is Null Throw 50508, 'El operador TI no se encuentra habilitado.', 1
	-- Responsable de la investigación, supervisión o responsable TI del ticket investigado.
	If Not Exists (Select 1 From dbo.TI_AgenteSesion s Left Join dbo.TI_Incidencia i on i.IncidenciaNumero = s.IncidenciaNumero
		Where s.SesionNumero = @nSesionNumero and (s.UsuarioTI = @cUsuario or @cPerfil In ('SUP','ADM') or i.UsuarioTI = @cUsuario))
		Throw 50509, 'La sesión de investigación no existe o no pertenece al operador.', 1

	Select
		s.SesionNumero, s.IncidenciaNumero, s.IdCorrelacion, s.DescripcionInicial, s.Estado, s.ResumenObservacion, s.ProcesoObservado, s.ErrorObservado, s.SolucionValidada, s.ConocimientoCodigo,
		s.Diagnostico, s.CausaProbable, s.SolucionPropuesta, s.Confianza, s.AccionCodigo, s.NivelRiesgo, s.ParametrosJson, s.Decision,
		s.SolicitudAprobacionSecuencia, s.FechaInicio, s.FechaDiagnostico, s.FechaDecision, s.EvidenciasJson, s.InformeMarkdown,
		InformeDisponible = Convert(bit, Case When NullIf(s.InformeMarkdown, '') Is Null Then 0 Else 1 End),
		s.UsuarioTI, NombreOperador = op.NombreCompleto, EsPropietario = Convert(bit, Case When s.UsuarioTI = @cUsuario Then 1 Else 0 End),
		UsuarioInvitado = IsNull(s.UsuarioInvitado, ''), NombreInvitado = IsNull(inv.NombreCompleto, ''), s.InvitacionExpira,
		EstadoInvitacion = Case When s.EstadoInvitacion In ('PENDIENTE','ACEPTADA') and s.InvitacionExpira <= SysDateTime() Then 'VENCIDA' Else IsNull(s.EstadoInvitacion, '') End,
		i.Titulo, i.Detalle, i.MensajeError, i.Estado as EstadoIncidencia, i.Linea, i.Item, i.Tipo, i.SubTipo, i.Categoria, i.UsuarioSolicitante, i.FechaRegistro,
		UsuarioTITicket = IsNull(i.UsuarioTI, ''),
		PuedeTomar = Convert(bit, Case When s.UsuarioTI <> @cUsuario
			and s.Estado In ('RECOPILANDO','OBSERVANDO','LISTO_INVESTIGAR','PENDIENTE_TI','PENDIENTE_APROBACION','SIN_EJECUTOR','ERROR_EJECUCION')
			and (@cPerfil In ('SUP','ADM') or i.UsuarioTI = @cUsuario) Then 1 Else 0 End)
	From dbo.TI_AgenteSesion s
	Inner Join dbo.TI_Usuario op on op.Usuario = s.UsuarioTI
	Left Join dbo.TI_Usuario inv on inv.Usuario = s.UsuarioInvitado
	Left Join dbo.TI_Incidencia i on i.IncidenciaNumero = s.IncidenciaNumero
	Where s.SesionNumero = @nSesionNumero

	Select d.CompaniaSocio, d.TipoDocumento, d.NumeroDocumento, d.Descripcion
	From dbo.TI_IncidenciaDocumento d
	Inner Join dbo.TI_AgenteSesion s on s.IncidenciaNumero = d.IncidenciaNumero
	Where s.SesionNumero = @nSesionNumero
	Order By d.Secuencia

	Select Top (30) m.TipoAutor, Autor = IsNull(u.NombreCompleto, m.UsuarioAutor), m.Contenido, m.FechaMensaje, m.EsInterno
	From dbo.TI_IncidenciaMensaje m
	Inner Join dbo.TI_AgenteSesion s on s.IncidenciaNumero = m.IncidenciaNumero
	Left Join dbo.TI_Usuario u on u.Usuario = m.UsuarioAutor
	Where s.SesionNumero = @nSesionNumero
	Order By m.Secuencia Desc

	;With Contexto as (
		Select i.Linea, i.Item, i.Tipo, i.Categoria
		From dbo.TI_AgenteSesion s
		Left Join dbo.TI_Incidencia i on i.IncidenciaNumero = s.IncidenciaNumero
		Where s.SesionNumero = @nSesionNumero
	)
	Select Top (10) k.ConocimientoCodigo, k.Titulo, k.Problema, k.Sintomas, k.Causa, k.Solucion, k.Procedimiento,
		GuiaDiagnosticoJson = IsNull(k.GuiaDiagnosticoJson, N''),
		PuntajeContextual =
			Case When k.Item Is Not Null and k.Item = c.Item Then 5 Else 0 End +
			Case When k.Linea Is Not Null and k.Linea = c.Linea Then 3 Else 0 End +
			Case When k.Categoria Is Not Null and k.Categoria = c.Categoria Then 2 Else 0 End +
			Case When k.Tipo Is Not Null and k.Tipo = c.Tipo Then 1 Else 0 End
	From dbo.TI_BaseConocimiento k
	Cross Join Contexto c
	Where k.Estado = 'A'
		and (c.Linea Is Null or k.Linea Is Null or k.Linea = c.Linea)
	Order By PuntajeContextual Desc, k.FechaValidacion Desc, k.FechaCreacion Desc

	Select a.AccionCodigo, a.Nombre, a.Descripcion, a.Tipo, a.NivelRiesgo, a.RequiereAprobacion, a.Reversible,
		TieneEjecutor = Convert(bit, Case When x.AccionCodigo Is Null Then 0 Else 1 End),
		ParametrosDescripcion = IsNull(x.ParametrosDescripcion, N''), ParametrosEsquemaJson = IsNull(x.ParametrosEsquemaJson, N'')
	From dbo.TI_Accion a
	Left Join dbo.TI_AgenteAccionEjecutor x on x.AccionCodigo = a.AccionCodigo and x.Estado = 'A'
	Where a.Estado = 'A'
	Order By a.Tipo, a.NivelRiesgo, a.Nombre

	Select Top (50) a.Entidad, a.Registro, a.Evento, a.Resultado, a.DetalleJson, a.IdCorrelacion, a.Fecha
	From dbo.TI_Auditoria a
	Inner Join dbo.TI_AgenteSesion s on s.IncidenciaNumero = a.IncidenciaNumero
	Where s.SesionNumero = @nSesionNumero
	Order By a.Fecha Desc, a.AuditoriaNumero Desc

	Select Secuencia, Tipo, Fuente, Contenido, DatosJson, Fecha, OrigenServidor
	From dbo.TI_AgenteEvento
	Where SesionNumero = @nSesionNumero
	Order By Secuencia
End
Go

/* Ejecuta Procedure
Exec dbo.Usp_TI_Agente_ObtenerContexto @cUsuario = 'TEC001', @cArea = 'TIC', @nSesionNumero = 1;
Exec Usp_TI_Obtener_PlantillaFicha 'REQ';
*/
