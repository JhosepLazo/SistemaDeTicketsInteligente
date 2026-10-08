/*
	Archivo: PruebasFuncionales.sql
	Objetivo: Comprobar contra una base instalada las reglas que nunca deben romperse: transiciones de estado, segregación y vigencia
		de aprobaciones, ejecución que exige aprobación, interruptor y política de autonomía del agente, ficha de requerimientos, dominios,
		idempotencia y los procesos de mantenimiento (reconciliación, investigaciones pendientes y autocierre).
	Responsabilidad: Ejecutar cada caso en su propia transacción, revertirla siempre y registrar si el resultado fue el esperado.
	Dependencias: Requiere la instalación completa (InstalarLocal.ps1) con los usuarios de desarrollo USR001, TEC001, SUP001 y ADM001.
	Orden: Ejecutar después de instalar; no forma parte de la instalación.
	Consideraciones: No deja datos: todo se revierte. Termina con error 50699 si algún caso falla, para que el CI lo detecte.
		Se ejecuta con: sqlcmd -S . -E -C -I -b -f 65001 -i database/pruebas/PruebasFuncionales.sql
*/

Use [GestionSistemas]
Go

Set NoCount On
Set Xact_Abort Off

Declare @tResultado table (Caso nvarchar(200), Correcto bit, Detalle nvarchar(2000))
Declare @cLinea char(3), @cTicket varchar(12), @cTicket2 varchar(12), @nSesion bigint, @nError int, @cEstado varchar(80), @nCantidad int,
	@cFicha nvarchar(max), @gCorrelacion uniqueidentifier = NewId()

Select Top (1) @cLinea = Linea From dbo.TI_Linea Where Estado = 'A' Order By Linea

-- Ficha completa y válida del tipo REQ: cada campo obligatorio con su largo mínimo, SI_NO con SI y fechas ISO.
Select @cFicha = N'{' + String_Agg(Convert(nvarchar(max), Concat(N'"', Campo, N'":"',
	Case TipoDato When 'SI_NO' Then N'SI' When 'FECHA' Then N'2026-12-31' Else Left(Replicate(N'Dato de prueba suficiente. ', 10), IsNull(NullIf(LongitudMinima, 0), 5) + 5) End, N'"')), N',') + N'}'
From dbo.TI_PlantillaCampo Where Tipo = 'REQ' and Estado = 'A' and Obligatorio = 1

/* ---------- 1. Una transición no catalogada se rechaza ---------- */
Begin Try
	Begin Transaction
	Exec dbo.Usp_TI_Registrar_Incidencia @cUsuario = 'USR001', @cLinea = @cLinea, @cTipo = 'INC', @cTitulo = N'Prueba de transición', @cDetalle = N'Detalle de la prueba de transición inválida.', @cIdCorrelacion = @gCorrelacion
	Select @cTicket = Max(IncidenciaNumero) From dbo.TI_Incidencia Where IncidenciaNumero Like 'INC-%'
	Update dbo.TI_Incidencia Set Estado = 'RS' Where IncidenciaNumero = @cTicket
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Transición NV -> RS rechazada', 0, N'El cambio directo de NV a RS no fue rechazado.')
End Try
Begin Catch
	Set @nError = Error_Number()
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Transición NV -> RS rechazada', Case When @nError = 50600 Then 1 Else 0 End, Error_Message())
End Catch

/* ---------- 2. Asignar un ticket nuevo lo pasa a diagnóstico (transición catalogada) ---------- */
Begin Try
	Begin Transaction
	Exec dbo.Usp_TI_Registrar_Incidencia @cUsuario = 'USR001', @cLinea = @cLinea, @cTipo = 'INC', @cTitulo = N'Prueba de asignación', @cDetalle = N'Detalle de la prueba de asignación válida.', @cIdCorrelacion = @gCorrelacion
	Select @cTicket = Max(IncidenciaNumero) From dbo.TI_Incidencia Where IncidenciaNumero Like 'INC-%'
	Exec dbo.Usp_TI_Asignar_Ticket @cUsuario = 'TEC001', @cArea = 'TIC', @cIncidenciaNumero = @cTicket, @cUsuarioTI = 'TEC001', @cIdCorrelacion = @gCorrelacion
	Select @cEstado = Estado From dbo.TI_Incidencia Where IncidenciaNumero = @cTicket
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Transición NV -> DG permitida', Case When @cEstado = 'DG' Then 1 Else 0 End, Concat(N'Estado final: ', @cEstado))
End Try
Begin Catch
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Transición NV -> DG permitida', 0, Error_Message())
End Catch

/* ---------- 3. Quien solicita una aprobación no puede responderla ---------- */
Begin Try
	Begin Transaction
	Exec dbo.Usp_TI_Registrar_Incidencia @cUsuario = 'USR001', @cLinea = @cLinea, @cTipo = 'INC', @cTitulo = N'Prueba de segregación', @cDetalle = N'Detalle de la prueba de segregación de funciones.', @cIdCorrelacion = @gCorrelacion
	Select @cTicket = Max(IncidenciaNumero) From dbo.TI_Incidencia Where IncidenciaNumero Like 'INC-%'
	Exec dbo.Usp_TI_Solicitar_AprobacionTicket @cUsuario = 'TEC001', @cArea = 'TIC', @cIncidenciaNumero = @cTicket, @cAccionCodigo = 'ACC-002', @cJustificacion = N'Prueba', @cIdCorrelacion = @gCorrelacion
	Exec dbo.Usp_TI_Responder_AprobacionTicket @cUsuario = 'TEC001', @cArea = 'TIC', @cIncidenciaNumero = @cTicket, @nSecuencia = 1, @lAprobar = 1, @cComentario = Null, @cIdCorrelacion = @gCorrelacion
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Segregación de la aprobación', 0, N'El solicitante pudo aprobar su propia solicitud.')
End Try
Begin Catch
	Set @nError = Error_Number()
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Segregación de la aprobación', Case When @nError = 50242 Then 1 Else 0 End, Error_Message())
End Catch

/* ---------- 4. Con vigencia configurada, la aprobación concedida vence en ese plazo ---------- */
Begin Try
	Begin Transaction
	Exec dbo.Usp_TI_Registrar_Incidencia @cUsuario = 'USR001', @cLinea = @cLinea, @cTipo = 'INC', @cTitulo = N'Prueba de vigencia', @cDetalle = N'Detalle de la prueba de vigencia de la aprobación.', @cIdCorrelacion = @gCorrelacion
	Select @cTicket = Max(IncidenciaNumero) From dbo.TI_Incidencia Where IncidenciaNumero Like 'INC-%'
	Exec dbo.Usp_TI_Guardar_ParametroTI @cUsuario = 'ADM001', @cArea = 'TIC', @cParametro = 'APROBACION_VIGENCIA_HORAS', @cValor = N'2', @cIdCorrelacion = @gCorrelacion
	Exec dbo.Usp_TI_Solicitar_AprobacionTicket @cUsuario = 'TEC001', @cArea = 'TIC', @cIncidenciaNumero = @cTicket, @cAccionCodigo = 'ACC-002', @cJustificacion = N'Prueba', @cIdCorrelacion = @gCorrelacion
	Exec dbo.Usp_TI_Responder_AprobacionTicket @cUsuario = 'SUP001', @cArea = 'TIC', @cIncidenciaNumero = @cTicket, @nSecuencia = 1, @lAprobar = 1, @cComentario = Null, @cIdCorrelacion = @gCorrelacion
	Select @nCantidad = DateDiff(minute, FechaRespuesta, FechaExpiracion) From dbo.TI_SolicitudAprobacion Where IncidenciaNumero = @cTicket and Secuencia = 1
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Vigencia de la aprobación', Case When @nCantidad = 120 Then 1 Else 0 End, Concat(N'Minutos de vigencia: ', @nCantidad))
End Try
Begin Catch
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Vigencia de la aprobación', 0, Error_Message())
End Catch

/* ---------- 5. Solo un ADM cambia la política, y una incidencia nunca se libera ---------- */
Begin Try
	Begin Transaction
	Exec dbo.Usp_TI_Guardar_PoliticaAutonomia @cUsuario = 'TEC001', @cArea = 'TIC', @cTipo = 'SOL', @cAccionCodigo = 'ACC-004', @cModo = 'APROBACION', @nConfianzaMinima = Null, @cEstado = 'A', @cIdCorrelacion = @gCorrelacion
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Política solo para ADM', 0, N'Un TEC pudo cambiar la política.')
End Try
Begin Catch
	Set @nError = Error_Number()
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Política solo para ADM', Case When @nError = 50617 Then 1 Else 0 End, Error_Message())
End Catch

Begin Try
	Begin Transaction
	Exec dbo.Usp_TI_Guardar_PoliticaAutonomia @cUsuario = 'ADM001', @cArea = 'TIC', @cTipo = 'INC', @cAccionCodigo = 'ACC-004', @cModo = 'AUTONOMA', @nConfianzaMinima = 90, @cEstado = 'A', @cIdCorrelacion = @gCorrelacion
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Incidencia nunca autónoma', 0, N'Se liberó una acción para incidencias.')
End Try
Begin Catch
	Set @nError = Error_Number()
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Incidencia nunca autónoma', Case When @nError = 50621 Then 1 Else 0 End, Error_Message())
End Catch

Begin Try
	Begin Transaction
	Exec dbo.Usp_TI_Guardar_PoliticaAutonomia @cUsuario = 'ADM001', @cArea = 'TIC', @cTipo = 'SOL', @cAccionCodigo = 'ACC-007', @cModo = 'AUTONOMA', @nConfianzaMinima = 90, @cEstado = 'A', @cIdCorrelacion = @gCorrelacion
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Acción con aprobación no se libera', 0, N'Se liberó una acción que exige aprobación.')
End Try
Begin Catch
	Set @nError = Error_Number()
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Acción con aprobación no se libera', Case When @nError = 50622 Then 1 Else 0 End, Error_Message())
End Catch

/* ---------- 6. En modo SOMBRA (o APAGADO) no se prepara ninguna ejecución ---------- */
Begin Try
	Begin Transaction
	Update dbo.TI_Parametro Set Valor = N'SOMBRA' Where Parametro = 'AGENTE_MODO'
	Exec dbo.Usp_TI_Agente_PrepararCambio @cUsuario = 'TEC001', @cArea = 'TIC', @nSesionNumero = 0, @cClaveIdempotencia = @gCorrelacion, @cIdCorrelacion = @gCorrelacion
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Modo SOMBRA sin ejecución', 0, N'Se preparó una ejecución en modo SOMBRA.')
End Try
Begin Catch
	Set @nError = Error_Number()
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Modo SOMBRA sin ejecución', Case When @nError = 50601 Then 1 Else 0 End, Error_Message())
End Catch

/* ---------- 7. La ejecución autónoma exige modo AUTONOMO ---------- */
Begin Try
	Begin Transaction
	Exec dbo.Usp_TI_Agente_PrepararCambio @cUsuario = 'TEC001', @cArea = 'TIC', @nSesionNumero = 0, @cClaveIdempotencia = @gCorrelacion, @cIdCorrelacion = @gCorrelacion, @lAutonoma = 1
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Autonomía exige modo AUTONOMO', 0, N'Se preparó una ejecución autónoma en modo ASISTIDO.')
End Try
Begin Catch
	Set @nError = Error_Number()
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Autonomía exige modo AUTONOMO', Case When @nError = 50602 Then 1 Else 0 End, Error_Message())
End Catch

/* ---------- 8. Con todas las condiciones, una solicitud se prepara sin humano; una incidencia no ---------- */
Begin Try
	Begin Transaction
	Insert dbo.TI_Categoria (Categoria, Descripcion, Abreviatura, Estado) Values ('PRUEBA_SQL', N'Categoría de prueba', 'PSQ', 'A')
	Insert dbo.TI_SubTipo (Tipo, SubTipo, Categoria, Descripcion, Abreviatura, Estado) Values ('SOL', 'PSQ', 'PRUEBA_SQL', N'Subtipo de prueba', 'PSQ', 'A')
	Exec dbo.Usp_TI_Registrar_Incidencia @cUsuario = 'USR001', @cLinea = @cLinea, @cTipo = 'SOL', @cTitulo = N'Prueba de autonomía', @cDetalle = N'Detalle de la prueba de ejecución autónoma.', @cIdCorrelacion = @gCorrelacion
	Select @cTicket = Max(IncidenciaNumero) From dbo.TI_Incidencia Where IncidenciaNumero Like 'INC-%'
	Update dbo.TI_Incidencia Set SubTipo = 'PSQ', Categoria = 'PRUEBA_SQL' Where IncidenciaNumero = @cTicket
	Exec dbo.Usp_TI_Guardar_ParametroTI @cUsuario = 'ADM001', @cArea = 'TIC', @cParametro = 'AGENTE_MODO', @cValor = N'AUTONOMO', @cIdCorrelacion = @gCorrelacion
	Exec dbo.Usp_TI_Guardar_AccionCatalogo @cUsuario = 'ADM001', @cArea = 'TIC', @cAccionCodigo = 'ACC-004', @cNivelRiesgo = 'MUY_BAJO', @lRequiereAprobacion = 0, @lReversible = 1, @cEstado = 'A', @cIdCorrelacion = @gCorrelacion
	Exec dbo.Usp_TI_Guardar_PoliticaAutonomia @cUsuario = 'ADM001', @cArea = 'TIC', @cTipo = 'SOL', @cAccionCodigo = 'ACC-004', @cModo = 'AUTONOMA', @nConfianzaMinima = 80, @cEstado = 'A', @cIdCorrelacion = @gCorrelacion
	Insert dbo.TI_AgenteSesion (IncidenciaNumero, UsuarioTI, AreaTI, IdCorrelacion, DescripcionInicial, Estado, Confianza, AccionCodigo, NivelRiesgo, ParametrosJson, InformeMarkdown, FechaInicio, FechaDiagnostico)
	Values (@cTicket, 'TEC001', 'TIC', @gCorrelacion, N'Prueba de autonomía', 'PENDIENTE_TI', 90, 'ACC-004', 'MUY_BAJO', N'{}', N'# Informe', SysDateTime(), SysDateTime())
	Set @nSesion = Scope_Identity()
	Exec dbo.Usp_TI_Agente_PrepararCambio @cUsuario = 'TEC001', @cArea = 'TIC', @nSesionNumero = @nSesion, @cClaveIdempotencia = @gCorrelacion, @cIdCorrelacion = @gCorrelacion, @lAutonoma = 1
	Select @cEstado = Concat(sesion.Estado, '/', sesion.Decision, '/', ejecucion.TipoEjecutor)
	From dbo.TI_AgenteSesion as sesion
	Inner Join dbo.TI_EjecucionAccion as ejecucion on ejecucion.IncidenciaNumero = sesion.IncidenciaNumero and ejecucion.Secuencia = sesion.EjecucionSecuencia
	Where sesion.SesionNumero = @nSesion
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Ejecución autónoma de una solicitud', Case When @cEstado = 'EJECUTANDO/EJECUCION_AUTONOMA/I' Then 1 Else 0 End, Concat(N'Resultado: ', @cEstado))
End Try
Begin Catch
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Ejecución autónoma de una solicitud', 0, Error_Message())
End Catch

Begin Try
	Begin Transaction
	Exec dbo.Usp_TI_Registrar_Incidencia @cUsuario = 'USR001', @cLinea = @cLinea, @cTipo = 'INC', @cTitulo = N'Prueba de autonomía en incidencia', @cDetalle = N'Una incidencia nunca se ejecuta sin humano.', @cIdCorrelacion = @gCorrelacion
	Select @cTicket = Max(IncidenciaNumero) From dbo.TI_Incidencia Where IncidenciaNumero Like 'INC-%'
	Update dbo.TI_Parametro Set Valor = N'AUTONOMO' Where Parametro = 'AGENTE_MODO'
	Update dbo.TI_Accion Set RequiereAprobacion = 0, Reversible = 1, NivelRiesgo = 'MUY_BAJO' Where AccionCodigo = 'ACC-004'
	Insert dbo.TI_AgenteSesion (IncidenciaNumero, UsuarioTI, AreaTI, IdCorrelacion, DescripcionInicial, Estado, Confianza, AccionCodigo, ParametrosJson, InformeMarkdown, FechaInicio)
	Values (@cTicket, 'TEC001', 'TIC', @gCorrelacion, N'Prueba de autonomía en incidencia', 'PENDIENTE_TI', 99, 'ACC-004', N'{}', N'# Informe', SysDateTime())
	Set @nSesion = Scope_Identity()
	Exec dbo.Usp_TI_Agente_PrepararCambio @cUsuario = 'TEC001', @cArea = 'TIC', @nSesionNumero = @nSesion, @cClaveIdempotencia = @gCorrelacion, @cIdCorrelacion = @gCorrelacion, @lAutonoma = 1
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Incidencia rechazada por la autonomía', 0, N'Una incidencia se preparó para ejecución sin humano.')
End Try
Begin Catch
	Set @nError = Error_Number()
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Incidencia rechazada por la autonomía', Case When @nError = 50605 Then 1 Else 0 End, Error_Message())
End Catch

/* ---------- 8b. Una acción que exige aprobación no se ejecuta sin una solicitud aprobada, aunque se insista ---------- */
Begin Try
	Begin Transaction
	Exec dbo.Usp_TI_Registrar_Incidencia @cUsuario = 'USR001', @cLinea = @cLinea, @cTipo = 'INC', @cTitulo = N'Prueba de aprobación obligatoria', @cDetalle = N'Una acción con aprobación no se ejecuta sin aprobar.', @cIdCorrelacion = @gCorrelacion
	Select @cTicket = Max(IncidenciaNumero) From dbo.TI_Incidencia Where IncidenciaNumero Like 'INC-%'
	Insert dbo.TI_AgenteSesion (IncidenciaNumero, UsuarioTI, AreaTI, IdCorrelacion, DescripcionInicial, Estado, Confianza, AccionCodigo, ParametrosJson, InformeMarkdown, FechaInicio)
	Values (@cTicket, 'TEC001', 'TIC', @gCorrelacion, N'Prueba de aprobación obligatoria', 'PENDIENTE_TI', 95, 'ACC-004', N'{}', N'# Informe', SysDateTime())
	Set @nSesion = Scope_Identity()
	-- La primera llamada crea la solicitud; la segunda encuentra la solicitud pendiente. Ninguna debe registrar una ejecución.
	Exec dbo.Usp_TI_Agente_PrepararCambio @cUsuario = 'TEC001', @cArea = 'TIC', @nSesionNumero = @nSesion, @cClaveIdempotencia = @gCorrelacion, @cIdCorrelacion = @gCorrelacion
	Exec dbo.Usp_TI_Agente_PrepararCambio @cUsuario = 'TEC001', @cArea = 'TIC', @nSesionNumero = @nSesion, @cClaveIdempotencia = @gCorrelacion, @cIdCorrelacion = @gCorrelacion
	Select @nCantidad = Count(*) From dbo.TI_EjecucionAccion Where IncidenciaNumero = @cTicket
	Select @cEstado = Concat(sesion.Estado, '/', ticket.Estado, '/', solicitud.Estado)
	From dbo.TI_AgenteSesion as sesion
	Inner Join dbo.TI_Incidencia as ticket on ticket.IncidenciaNumero = sesion.IncidenciaNumero
	Inner Join dbo.TI_SolicitudAprobacion as solicitud on solicitud.IncidenciaNumero = sesion.IncidenciaNumero and solicitud.Secuencia = sesion.SolicitudAprobacionSecuencia
	Where sesion.SesionNumero = @nSesion
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Acción con aprobación no se ejecuta sin aprobar',
		Case When @nCantidad = 0 and @cEstado = 'PENDIENTE_APROBACION/PA/P' Then 1 Else 0 End,
		Concat(N'Ejecuciones: ', @nCantidad, N' · sesión/ticket/solicitud: ', @cEstado))
End Try
Begin Catch
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Acción con aprobación no se ejecuta sin aprobar', 0, Error_Message())
End Catch

/* ---------- 9. Ficha de requerimientos: incompleta se rechaza, completa se guarda ---------- */
Begin Try
	Begin Transaction
	Exec dbo.Usp_TI_Registrar_Incidencia @cUsuario = 'USR001', @cLinea = @cLinea, @cTipo = 'REQ', @cTitulo = N'Prueba de ficha', @cDetalle = N'Requerimiento sin ficha completa.', @cIdCorrelacion = @gCorrelacion
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Ficha REQ incompleta rechazada', Case When Exists (Select 1 From dbo.TI_PlantillaCampo Where Tipo = 'REQ' and Estado = 'A' and Obligatorio = 1) Then 0 Else 1 End,
		N'Se registró un requerimiento sin ficha.')
End Try
Begin Catch
	Set @nError = Error_Number()
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Ficha REQ incompleta rechazada', Case When @nError = 50643 Then 1 Else 0 End, Error_Message())
End Catch

Begin Try
	Begin Transaction
	Exec dbo.Usp_TI_Registrar_Incidencia @cUsuario = 'USR001', @cLinea = @cLinea, @cTipo = 'REQ', @cTitulo = N'Prueba de ficha completa', @cDetalle = N'Requerimiento con la ficha completa.', @cIdCorrelacion = @gCorrelacion, @cFichaJson = @cFicha
	Select @cTicket = Max(IncidenciaNumero) From dbo.TI_Incidencia Where IncidenciaNumero Like 'INC-%'
	Select @nCantidad = Count(*) From dbo.TI_IncidenciaDato Where IncidenciaNumero = @cTicket
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Ficha REQ completa guardada', Case When @nCantidad = (Select Count(*) From dbo.TI_PlantillaCampo Where Tipo = 'REQ' and Estado = 'A' and Obligatorio = 1) Then 1 Else 0 End,
		Concat(N'Campos guardados: ', @nCantidad))
End Try
Begin Catch
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Ficha REQ completa guardada', 0, Error_Message())
End Catch

Begin Try
	Begin Transaction
	Declare @cFichaInvalida nvarchar(max) = Json_Modify(@cFicha, '$.JEFATURA_VALIDO', N'TALVEZ')
	Exec dbo.Usp_TI_Registrar_Incidencia @cUsuario = 'USR001', @cLinea = @cLinea, @cTipo = 'REQ', @cTitulo = N'Prueba de ficha inválida', @cDetalle = N'Requerimiento con un SI_NO inválido.', @cIdCorrelacion = @gCorrelacion, @cFichaJson = @cFichaInvalida
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Ficha REQ con valor inválido', 0, N'Se aceptó un valor inválido en un campo SI_NO.')
End Try
Begin Catch
	Set @nError = Error_Number()
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Ficha REQ con valor inválido', Case When @nError = 50644 Then 1 Else 0 End, Error_Message())
End Catch

/* ---------- 10. Dominios cerrados e idempotencia ---------- */
Begin Try
	Begin Transaction
	Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson, IdCorrelacion, Fecha)
	Values (Null, Null, 'X', 'PRUEBA', 'PRUEBA', 'PRUEBA', 'PRUEBA', Null, NewId(), SysDateTime())
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Dominio de TipoActor', 0, N'Se aceptó un tipo de actor inexistente.')
End Try
Begin Catch
	Set @nError = Error_Number()
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Dominio de TipoActor', Case When @nError = 547 Then 1 Else 0 End, Error_Message())
End Catch

Begin Try
	Begin Transaction
	Exec dbo.Usp_TI_Registrar_Incidencia @cUsuario = 'USR001', @cLinea = @cLinea, @cTipo = 'INC', @cTitulo = N'Prueba de idempotencia', @cDetalle = N'Dos ejecuciones con la misma clave.', @cIdCorrelacion = @gCorrelacion
	Select @cTicket = Max(IncidenciaNumero) From dbo.TI_Incidencia Where IncidenciaNumero Like 'INC-%'
	Insert dbo.TI_EjecucionAccion (IncidenciaNumero, Secuencia, AccionCodigo, ClaveIdempotencia, Estado, FechaInicio) Values (@cTicket, 1, 'ACC-004', @gCorrelacion, 'PR', SysDateTime())
	Insert dbo.TI_EjecucionAccion (IncidenciaNumero, Secuencia, AccionCodigo, ClaveIdempotencia, Estado, FechaInicio) Values (@cTicket, 2, 'ACC-004', @gCorrelacion, 'PR', SysDateTime())
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Clave de idempotencia única', 0, N'Se aceptaron dos ejecuciones con la misma clave.')
End Try
Begin Catch
	Set @nError = Error_Number()
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Clave de idempotencia única', Case When @nError In (2601, 2627) Then 1 Else 0 End, Error_Message())
End Catch

Begin Try
	Begin Transaction
	Exec dbo.Usp_TI_Registrar_Incidencia @cUsuario = 'USR001', @cLinea = @cLinea, @cTipo = 'INC', @cTitulo = N'Prueba de diagnóstico validado', @cDetalle = N'Validar exige registrar quién lo hizo.', @cIdCorrelacion = @gCorrelacion
	Select @cTicket = Max(IncidenciaNumero) From dbo.TI_Incidencia Where IncidenciaNumero Like 'INC-%'
	Insert dbo.TI_IncidenciaDiagnostico (IncidenciaNumero, Secuencia, Origen, Diagnostico, CausaProbable, SolucionSugerida, Confianza, Estado, UsuarioValida, FechaDiagnostico)
	Values (@cTicket, 1, 'I', N'Prueba', N'Prueba', N'Prueba', 50, 'V', Null, SysDateTime())
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Diagnóstico validado con responsable', 0, N'Se aceptó un diagnóstico validado sin responsable.')
End Try
Begin Catch
	Set @nError = Error_Number()
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Diagnóstico validado con responsable', Case When @nError = 547 Then 1 Else 0 End, Error_Message())
End Catch

/* ---------- 11. Mantenimiento: ejecuciones interrumpidas, investigaciones pendientes y autocierre ---------- */
Begin Try
	Begin Transaction
	Exec dbo.Usp_TI_Registrar_Incidencia @cUsuario = 'USR001', @cLinea = @cLinea, @cTipo = 'INC', @cTitulo = N'Prueba de reconciliación', @cDetalle = N'Ejecución que quedó en proceso.', @cIdCorrelacion = @gCorrelacion
	Select @cTicket = Max(IncidenciaNumero) From dbo.TI_Incidencia Where IncidenciaNumero Like 'INC-%'
	Insert dbo.TI_EjecucionAccion (IncidenciaNumero, Secuencia, AccionCodigo, UsuarioEjecutor, ClaveIdempotencia, Estado, FechaInicio)
	Values (@cTicket, 1, 'ACC-004', 'TEC001', NewId(), 'PR', DateAdd(hour, -1, SysDateTime()))
	Exec dbo.Usp_TI_Agente_ReconciliarEjecuciones @nMinutos = 15
	Select @cEstado = Estado From dbo.TI_EjecucionAccion Where IncidenciaNumero = @cTicket and Secuencia = 1
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Reconciliación de ejecuciones', Case When @cEstado = 'ER' Then 1 Else 0 End, Concat(N'Estado final: ', @cEstado))
End Try
Begin Catch
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Reconciliación de ejecuciones', 0, Error_Message())
End Catch

Begin Try
	Begin Transaction
	Exec dbo.Usp_TI_Registrar_Incidencia @cUsuario = 'USR001', @cLinea = @cLinea, @cTipo = 'INC', @cTitulo = N'Prueba de investigación pendiente', @cDetalle = N'Investigación automática encolada.', @cIdCorrelacion = @gCorrelacion
	Select @cTicket = Max(IncidenciaNumero) From dbo.TI_Incidencia Where IncidenciaNumero Like 'INC-%'
	Exec dbo.Usp_TI_Agente_PrepararInvestigacionAutomatica @cIncidenciaNumero = @cTicket, @cEvidenciaJson = N'{"pasos":["Abrí la pantalla"],"mensajeError":"Error de prueba"}', @cUsuarioPreferido = 'TEC001', @cIdCorrelacion = @gCorrelacion
	Select @nSesion = SesionNumero From dbo.TI_AgenteSesion Where IdCorrelacion = @gCorrelacion
	Declare @tPendientes table (SesionNumero bigint)
	Insert @tPendientes Exec dbo.Usp_TI_Agente_ListarInvestigacionesPendientes @nMinutos = -1
	Set @nCantidad = (Select Count(*) From @tPendientes Where SesionNumero = @nSesion)
	Exec dbo.Usp_TI_Agente_RegistrarEventoServidor @nSesionNumero = @nSesion, @cTipo = 'INVESTIGACION_AUTOMATICA_FIN', @cContenido = N'Fin de prueba', @cDatosJson = N'{"exito":false}'
	Delete @tPendientes
	Insert @tPendientes Exec dbo.Usp_TI_Agente_ListarInvestigacionesPendientes @nMinutos = -1
	Set @nCantidad = @nCantidad * 10 + (Select Count(*) From @tPendientes Where SesionNumero = @nSesion)
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Investigación automática pendiente', Case When @nCantidad = 10 Then 1 Else 0 End, Concat(N'Pendiente antes/después del fin: ', @nCantidad))
End Try
Begin Catch
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Investigación automática pendiente', 0, Error_Message())
End Catch

Begin Try
	Begin Transaction
	Exec dbo.Usp_TI_Registrar_Incidencia @cUsuario = 'USR001', @cLinea = @cLinea, @cTipo = 'INC', @cTitulo = N'Prueba de autocierre', @cDetalle = N'Ticket que espera validación.', @cIdCorrelacion = @gCorrelacion
	Select @cTicket = Max(IncidenciaNumero) From dbo.TI_Incidencia Where IncidenciaNumero Like 'INC-%'
	Exec dbo.Usp_TI_Asignar_Ticket @cUsuario = 'TEC001', @cArea = 'TIC', @cIncidenciaNumero = @cTicket, @cUsuarioTI = 'TEC001', @cIdCorrelacion = @gCorrelacion
	Exec dbo.Usp_TI_Resolver_TicketTI @cUsuario = 'TEC001', @cArea = 'TIC', @cIncidenciaNumero = @cTicket, @cCausaRaiz = N'Causa de prueba', @cSolucion = N'Solución de prueba',
		@cRespuestaUsuario = N'Respuesta de prueba', @cTipoResolucion = 'CORRECCION', @cIdCorrelacion = @gCorrelacion
	Update dbo.TI_IncidenciaEstado Set FechaCambio = DateAdd(day, -20, FechaCambio) Where IncidenciaNumero = @cTicket and Estado = 'PV'
	Exec dbo.Usp_TI_Cerrar_TicketsSinValidacion @nDias = 15
	Select @cEstado = Estado From dbo.TI_Incidencia Where IncidenciaNumero = @cTicket
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Autocierre de tickets en validación', Case When @cEstado = 'RS' Then 1 Else 0 End, Concat(N'Estado final: ', @cEstado))
End Try
Begin Catch
	If @@TranCount > 0 Rollback Transaction
	Insert @tResultado Values (N'Autocierre de tickets en validación', 0, Error_Message())
End Catch

/* ---------- 12. La conversación interna de TI nunca llega al usuario ---------- */
Insert @tResultado
Select N'Mis Tickets excluye mensajes internos',
	Case When Object_Definition(Object_Id('dbo.Usp_TI_Obtener_DetalleTicketUsuario')) Like N'%EsInterno = 0%' Then 1 Else 0 End,
	N'Comprobación estática: el procedimiento del detalle del usuario filtra EsInterno = 0.'

/* ---------- Resultado ---------- */
Select Caso, Resultado = Case When Correcto = 1 Then 'OK' Else 'FALLA' End, Detalle From @tResultado

If @@TranCount > 0 Rollback Transaction
If Exists (Select 1 From @tResultado Where Correcto = 0)
	Throw 50699, 'Una o más pruebas funcionales fallaron. Revisa el detalle anterior.', 1

Print N'Pruebas funcionales: todas correctas. No quedaron datos de prueba.'
Go
