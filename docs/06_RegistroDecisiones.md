# Registro de decisiones y aplicación del plan de mejoras

> **Versión:** 1.0 · **Fecha:** 08/10/2026 · **Fuente:** `Mejoras_Sistema_Tickets_Inteligente.md` (plan de mejoras redactado sobre `origin/main`, commit `ee11ad0`), aplicado el 07–08/10/2026 sobre la rama `feature/asistente-ti-agente-ingenieria` (base `ca3569e`). Los cambios no tienen commit todavía.
>
> El plan se escribió cuando el repositorio solo tenía la autenticación. La rama actual ya tenía el núcleo de tickets, las pantallas por perfil y el agente; por eso muchos puntos figuran como **ya existía**. Cada punto indica su estado y dónde quedó.

**Estados usados:** `APLICADO` (se implementó ahora) · `YA EXISTÍA` (estaba en la rama antes del plan) · `APLICADO CON VARIANTE` (se cumple el objetivo de otra forma, con el motivo) · `PENDIENTE DE DECISIÓN` (requiere a TI) · `NO APLICADO` (con el motivo).

---

## 1. Decisiones tomadas al aplicar el plan (07/10/2026)

| Tema | Decisión | Quién |
|---|---|---|
| Base de datos | Crear los scripts 34 a 40 y ejecutarlos en la base local `GestionSistemas`. | Responsable del proyecto |
| Permisos de Configuración TI | Mantener TEC, SUP y ADM en los maestros (como el código); solo ADM cambia el control del agente, la política de autonomía, el catálogo de acciones y las fichas. Se corrigieron la cabecera del controlador, `docs/04` y la documentación. | Responsable del proyecto |
| Ficha del requerimiento | Activa desde la instalación (29 campos, 18 obligatorios). | Responsable del proyecto |
| Reinicio de la API | Autorizado para tomar el código nuevo. | Responsable del proyecto |

## 2. Decisiones abiertas del plan (D1 a D14)

| ID | Decisión | Estado | Situación actual |
|---|---|---|---|
| D1 | Lectura para diagnosticar: política permanente o autorización por ticket | `PENDIENTE DE DECISIÓN` (confirmar) | Hoy rige una política permanente de solo lectura: las herramientas solo leen, dentro de transacciones revertidas y con la identidad `CnnAgenteLectura`; la investigación la inicia TI o llega con la evidencia del colaborador. TI debe confirmarlo formalmente. |
| D2 | Acciones de escritura liberadas como autónomas y techo de riesgo | `PENDIENTE DE DECISIÓN` | El mecanismo está completo (política, techo, regla compuesta). Lista vacía: todo en "Con aprobación"; techo `MUY_BAJO`. Las acciones de ejecución están en riesgo MEDIO, con aprobación y no reversibles. |
| D3 | `ACC-007` sin TI con aprobación de la jefatura | `PENDIENTE DE DECISIÓN` | Hoy siempre requiere aprobación de TI. La elegibilidad del solicitante se limita a "colaborador USR"; no se consulta la jefatura. |
| D4 | Estado `PT` o motivo estructurado | `APLICADO CON VARIANTE` | No se creó `PT`: el requerimiento llega con su ficha completa desde el registro y, si TI necesita más datos, usa "Solicitar información" (RC). Reportes mide los devueltos por datos faltantes. Confirmar con TI. |
| D5 | Dominios de diagnóstico, ejecución, riesgo, actor, canal y resolución | `APLICADO` | CHECK con los valores que usa el sistema (script 34): diagnóstico P/D/V (V exige quien valida), ejecución PR/OK/ER con su fecha de fin, riesgo MUY_BAJO…MUY_ALTO, actor U/T/I/S, canal PORTAL/ASISTENTE/MESA_AYUDA/LEGADO, resolución CORRECCION/CONFIGURACION/GUIA/REPROCESO. Los datos locales se verificaron antes. |
| D6 | Escalas, cálculo, calificación, unidad de tiempo y SLA | `APLICADO` (parcial) | Prioridad, impacto, complejidad y calificación 1 a 5 (CHECK); el tiempo se registra en minutos (1 a 1440); la prioridad sale de la matriz y el SLA de `TI_ParametroSLA`. **Pendiente:** pausar el SLA mientras el ticket espera al usuario. |
| D7 | Numeración de tickets y `Secuencia` | `PENDIENTE DE DECISIÓN` (parcial) | `Secuencia` ya se calcula con `UpdLock, HoldLock` en cada procedimiento. Todos los tipos se numeran `INC-` (y `TKT-` los del legado): falta decidir serie única o por tipo. |
| D8 | Proveedor de IA y salida de datos | `PENDIENTE DE DECISIÓN` | Proveedor configurable (OpenAI, Gemini o Groq). Los secretos se ocultan siempre y los datos personales antes de salir hacia la IA. La organización debe decidir si la información del ERP puede salir de la empresa. |
| D9 | Versión de SQL Server, identidades y permisos | `APLICADO` (parcial) | SQL Server 2022; tres roles con permisos mínimos (script 38). **Pendiente:** los logins de cada ambiente, que crea TI. |
| D10 | Autenticación local o SSO | `YA EXISTÍA` | Identidad corporativa contra Spring (configurable) y local con hash en desarrollo. Active Directory o SSO no están en el alcance. |
| D11 | Umbral de confianza, vencimiento de aprobaciones y autocierre | `APLICADO` (valores pendientes) | Parámetros editables por ADM: confianza mínima 60, vigencia y autocierre desactivados. TI debe fijar los valores definitivos. |
| D12 | Canal de notificación | `YA EXISTÍA` / `PENDIENTE DE DECISIÓN` | Campana persistida con avisos accionables. Correo o Teams no existen (decisión previa de `docs/04`). |
| D13 | Cuenta técnica del agente | `APLICADO CON VARIANTE` | No se creó `AGENTE_IA`: las acciones se atribuyen al operador responsable de la investigación y se distinguen con `TI_EjecucionAccion.TipoEjecutor = 'I'` (autónoma) y `TI_Auditoria.TipoActor = 'I'`. Las cuentas `TipoUsuario = 'SISTEMA'` no pueden iniciar sesión (lista para cuando TI cree una). Confirmar con TI. |
| D14 | Cuándo nace el ticket | `APLICADO CON VARIANTE` | Se mantiene: el ticket nace al registrarlo con tipo y línea (el asistente prepara un borrador antes). No se hicieron nulos `Tipo` ni `Linea`. |

## 3. Decisiones nuevas que surgieron al aplicar el plan

| ID | Decisión | Por qué |
|---|---|---|
| D15 | Unificar los tipos heredados `001` Incidencia, `002` Requerimiento y `003` Solicitud con `INC`, `REQ` y `SOL` | `23_SincronizarDatosLegado.sql` los deja activos: el colaborador ve seis tipos, la matriz completa solo existe para los heredados, un requerimiento `002` no exige la ficha y una solicitud `003` nunca es autónoma. Mientras tanto, un ADM puede configurar la ficha de `002` o inactivar los heredados para el registro. |
| D16 | Publicar OpenAPI (Swagger) | El plan lo pide como transversal. No se agregó porque suma una dependencia y expone el contrato de la API; requiere decidir si se publica en Empresa. El inventario completo de endpoints está en `01_DOCUMENTACION_TECNICA.md` §16 y lo verifica `MatrizAutorizacionPruebas`. |
| D17 | Lecturas consistentes (`READ_COMMITTED_SNAPSHOT`) y `NOLOCK` en las consultas del agente | No se cambió: RCSI altera el comportamiento de bloqueo de todos los procedimientos. El validador del agente todavía admite `NOLOCK`, que puede leer datos no confirmados (riesgo que el plan advierte). |
| D18 | Ejecutores de reversión por acción (y estado `RV`) | Hoy solo existe el indicador `Reversible` y el estado anterior guardado en `ResultadoJson`; la reversión se hace por el flujo del portal (`05_RunbookAgente.md` §8). |
| D19 | `Idempotency-Key` para registrar tickets y mensajes | Evita duplicados si el navegador reintenta. No aplicado. |
| D20 | Asignar los estados AU, EJ y ES | Están en la máquina de estados como destinos posibles, pero ningún proceso los usa. |
| D21 | Elegibilidad del solicitante por acción (perfil, área, jefatura) | Hoy la regla exige solo que sea colaborador (USR). Ligada a D3. |
| D22 | Semántica de `TI_Tipo.Item` | Pendiente del diccionario de datos; el clasificador no depende de ella. |
| D23 | Datos de prueba con requerimientos y casos autónomos | El seed no tiene tickets `REQ` y la solicitud de ejemplo queda en `PA` (Anexo A, punto 9). Depende de D2 y D3. |

## 4. Las 10 prioridades del plan

| # | Mejora | Estado | Dónde |
|---|---|---|---|
| 1 | Política de autonomía persistida y regla compuesta | `APLICADO` | `TI_PoliticaAutonomia`, `PoliticaAutonomia.cs` (lógica pura con pruebas), `Usp_TI_Agente_PrepararCambio` (35) |
| 2 | Máquina de estados aplicada en la base | `APLICADO CON VARIANTE` | `TI_EstadoTransicion` + trigger `Tr_TI_Incidencia_TransicionEstado` (34) en lugar de un único procedimiento de cambio de estado: protege también a los procedimientos existentes. Sin columnas de tipo ni actor: el actor lo controla cada procedimiento. |
| 3 | Núcleo de tickets sin IA | `YA EXISTÍA` | Módulos de colaborador y TI completos |
| 4 | Catálogo de acciones ejecutable | `APLICADO CON VARIANTE` | Ejecutor catalogado con esquema de parámetros, reversibilidad, tope de filas, postcondición y simulación con transacción revertida. Sin procedimientos separados de precondición, validación y reversión (D18). |
| 5 | Aprobación ligada al diagnóstico y a los parámetros, con expiración | `APLICADO CON VARIANTE` | `DiagnosticoSecuencia` y `FechaExpiracion` (35). En lugar de `ParametrosHash`, se exige igualdad exacta de `ParametrosJson` (más estricta). |
| 6 | Plantilla de recopilación del requerimiento | `APLICADO` | `TI_PlantillaCampo`, `TI_IncidenciaDato`, ficha en Nuevo Ticket, revisión con IA y completitud en Reportes (36, 37) |
| 7 | Traza, defensa contra inyección y enmascaramiento | `APLICADO` | Eventos `LLAMADA_MODELO` (con versión de las instrucciones), `ACCION_RECHAZADA`, `EVALUACION_AUTONOMIA`; redactor de datos sensibles y validador SQL con pruebas |
| 8 | Tres identidades SQL | `APLICADO` | `CnnAgenteLectura`, `CnnAgenteEscritura` y roles del script 38 |
| 9 | Modo sombra y evaluación | `APLICADO` (parcial) | Interruptor SOMBRA y comparativo agente frente a TI en Reportes. **Pendiente:** conjunto de evaluación con tickets históricos ejecutado en cada cambio de modelo o instrucciones. |
| 10 | Pruebas, CI e inconsistencias | `APLICADO` | Proyecto `SistemaTicketsInteligente.Pruebas`, `PruebasFuncionales.sql`, Vitest, `.github/workflows/ci.yml` (sin ejecutar todavía en GitHub) |

## 5. Punto por punto

### 5.1 Política, clasificación y flujos (§3.2 a §3.5)

| Punto | Estado | Detalle |
|---|---|---|
| Definición operativa de INC, SOL y REQ | `APLICADO` | En las instrucciones de la propuesta de clasificación |
| Ante la duda, el tipo con menos autonomía | `APLICADO` | Instrucción de la propuesta; además la regla compuesta solo admite `SOL` |
| Clasificación propuesta y confirmada; reclasificación auditada | `APLICADO CON VARIANTE` | La IA propone y TI aplica (historial `TI_IncidenciaClasificacion`: origen I para cada propuesta y T cada vez que TI clasifica). El colaborador no confirma la clasificación. |
| Matriz de autonomía por tipo y modo | `APLICADO` | `TI_PoliticaAutonomia` |
| Regla compuesta (8 condiciones) | `APLICADO` | Condición 5 (precondiciones justo antes) se cumple con la simulación revertida y la validación del propio ejecutor; condición 8 con la variante de D21 |
| Acciones nunca autónomas (permisos, maestros, borrar, no reversibles, sobre el tope) | `APLICADO` (parcial) | No reversible y sobre el techo quedan excluidas por regla; las demás dependen de cómo TI clasifique el riesgo de cada acción (D2) |
| Flujos SOL, INC y REQ | `APLICADO` (parcial) | SOL: autonomía condicionada; INC: aprobación por ticket; REQ: ficha y entrega a TI. Escalar a `ES` tras una falla no está implementado (D20). |
| Tabla de transiciones | `APLICADO CON VARIANTE` | 65 transiciones que reflejan el comportamiento real, incluidas las de estados del legado (TI puede inactivarlas) |

### 5.2 Requerimientos y diagnóstico (§3.6 a §3.9)

| Punto | Estado | Detalle |
|---|---|---|
| Bloques A a F, una pregunta por campo, ayuda y concreción | `APLICADO` | 29 campos; la concreción la revisa la IA a pedido |
| Detección de duplicados | `APLICADO` | La revisión de la ficha compara con tickets abiertos y guías |
| Ficha confirmada por el usuario y devuelta por TI | `APLICADO CON VARIANTE` | El colaborador confirma al enviar; TI devuelve con "Solicitar información" (D4) |
| Plantilla propia para `SOL/ACC` | `PENDIENTE DE DECISIÓN` | El mecanismo existe (Fichas en Maestros TI); falta definir sus campos |
| Playbook por ítem (`ArbolDiagnosticoJson`) | `APLICADO CON VARIANTE` | `TI_BaseConocimiento.GuiaDiagnosticoJson` con editor y uso como evidencia |
| Confianza calculada desde la evidencia | `APLICADO` | Topes por evidencia (ya existían) más el nivel ALTA, MEDIA o BAJA; con BAJA no se propone |
| Paquete para TI (evidencia, causa, parámetros, vista previa, riesgo, alternativas) | `APLICADO` | Se agregaron alternativas descartadas, nivel de evidencia, reversibilidad y versión de las instrucciones al informe |
| Lecturas consistentes | `NO APLICADO` | D17 |
| Ejecución segura: autorización, precondiciones, intención previa, ejecución, validación, registro, reconciliación | `APLICADO` (parcial) | Todo salvo el procedimiento de reversión (D18) y el estado `EC` (se usa `PR`) |
| Borrador de conocimiento al resolver, validación y utilidad | `YA EXISTÍA` / `APLICADO` | La utilidad se mide en Reportes (veces citado y resueltos sin reapertura) |

### 5.3 Base de datos (§4)

| Punto | Estado | Detalle |
|---|---|---|
| `Tipo` y `Linea` nulos para crear el ticket antes | `NO APLICADO` | D14 |
| Transiciones de estado | `APLICADO` | Script 34 |
| Dominios de canal, resolución, calificación y niveles | `APLICADO` | Script 34 |
| Estados de diagnóstico con regla "V exige quien valida" | `APLICADO` | Script 34; seed corregido (I/A → P/V) |
| Enlace del catálogo con lo ejecutable | `APLICADO CON VARIANTE` | Prioridad 4 |
| Escala de riesgo cerrada y comparable | `APLICADO` | CHECK y orden usado por el techo |
| Aprobación con diagnóstico, parámetros y vencimiento; CHECK de respuesta | `APLICADO` | Scripts 34 y 35 |
| Origen de la aprobación (agente o técnico) | `APLICADO CON VARIANTE` | Se deduce de la investigación vinculada; Reportes separa AGENTE y MANUAL |
| `TipoEjecutor`, estado previo, validación posterior, correlación en ejecuciones | `APLICADO CON VARIANTE` | `TipoEjecutor` nuevo; estado previo y validación en `ResultadoJson`; la correlación es la clave de idempotencia |
| Ejecución que exige aprobación sin solicitud aprobada | `APLICADO` | Lo impide `PrepararCambio`; caso negativo en `PruebasFuncionales.sql` ("Acción con aprobación no se ejecuta sin aprobar") |
| `TipoActor` con CHECK | `APLICADO` | Script 34 |
| Catálogo de tipos de documento (`TI_TipoDocumento`) | `NO APLICADO` | Ningún flujo actual lo usa; se crearía con la primera herramienta ERP que lo necesite |
| Estados de conocimiento, guía y utilidad | `APLICADO` | CHECK B/P/A/I, guía estructurada y métricas |
| `TipoUsuario` con `SISTEMA` | `APLICADO` | CHECK y rechazo del login |
| Semántica de `TI_Tipo.Item` | `PENDIENTE DE DECISIÓN` | D22 |
| Tablas nuevas | `APLICADO` | `TI_EstadoTransicion`, `TI_IncidenciaClasificacion`, `TI_PlantillaCampo`, `TI_IncidenciaDato`, `TI_PoliticaAutonomia`, `TI_Parametro` |
| `TI_AgenteTraza` | `APLICADO CON VARIANTE` | La traza vive en `TI_AgenteEvento` (eventos tipados con `DatosJson`), separada de `TI_Auditoria` como pide el plan |
| Contrato de los procedimientos del catálogo (simulación por defecto, sin SQL dinámico, idempotente, acotado) | `APLICADO CON VARIANTE` | La simulación ejecuta el ejecutor real dentro de una transacción que siempre se revierte, en lugar de un parámetro `@bSimular` |
| `Secuencia` con bloqueo | `YA EXISTÍA` | `UpdLock, HoldLock` |
| Prefijo de numeración | `PENDIENTE DE DECISIÓN` | D7 |
| Cuenta técnica del agente | `APLICADO CON VARIANTE` | D13 |

### 5.4 Backend (§5)

| Punto | Estado | Detalle |
|---|---|---|
| Tres identidades SQL | `APLICADO` | Script 38, `BaseDatos`, `Program.cs` (obligatorias en Empresa) |
| Estado activo fijo en la BLL | `APLICADO` | Constante única y `07_DatosIniciales.sql` con `A` |
| Correlación única por petición | `APLICADO` | `X-Correlation-ID` = `TraceIdentifier` = `IdCorrelacion` de la auditoría; también en respuestas 400, 401, 403 y 429 |
| Procedimiento de auditoría genérico | `APLICADO CON VARIANTE` | Cada procedimiento audita con su actor real; el dominio de `TipoActor` quedó cerrado |
| Cuentas técnicas y hash inválido | `APLICADO` | Rechazo de `SISTEMA` antes del hash; hash inválido = intento fallido auditado (`HASH_INVALIDO`) |
| Revalidación de la sesión | `APLICADO` | `OnValidatePrincipal`, caché de 2 minutos |
| Enumeración por tiempo y bloqueo por usuario | `APLICADO` | Hash ficticio y 5 fallos por minuto por usuario |
| `AllowedHosts` y certificado | `APLICADO` | Obligatorios en Empresa; `TrustServerCertificate=False` |
| `GenerarHash.cs` sin eco | `APLICADO` | Lee oculto y pide confirmación |
| `Idempotency-Key` | `NO APLICADO` | D19 |
| Endpoints de tickets, aprobaciones y conocimiento | `YA EXISTÍA` | Rutas propias del proyecto |
| Endpoints de catálogo, política, plantillas y control del agente | `APLICADO` | `api/configuracion-ti/agente*` y `fichas` (solo ADM cambia) |
| Auditoría y traza consultables | `APLICADO CON VARIANTE` | Consola del agente, expediente y Reportes; no hay endpoint genérico de auditoría |
| OpenAPI | `NO APLICADO` | D16 |
| Mensajes internos y traza nunca hacia USR | `YA EXISTÍA` + `APLICADO` | El procedimiento filtra `EsInterno = 0`; ahora lo comprueban `FlujoTicketPruebas` y `PruebasFuncionales.sql` |
| Agente en segundo plano tomando trabajo de la base | `APLICADO` | Marca `INVESTIGACION_AUTOMATICA`, `MantenimientoAgenteTI` y reconciliación |
| Lectura del agente acotada al solicitante | `YA EXISTÍA` (parcial) | El asistente del colaborador solo ve sus tickets; el agente trabaja para TI con su identidad de lectura |

### 5.5 Frontend (§6)

| Punto | Estado | Detalle |
|---|---|---|
| Pantallas por perfil | `YA EXISTÍA` + `APLICADO` | Se agregaron ficha, clasificación con IA, guía de diagnóstico, control del agente, fichas e indicadores del agente |
| Cliente de API único con CSRF | `APLICADO` | `services/api.ts` |
| Rutas por perfil | `YA EXISTÍA` | Movidas a `GuardiasRuta.tsx` y probadas |
| Transparencia del asistente y estados de carga | `YA EXISTÍA` | — |
| Íconos de `LoginPage` en un módulo | `APLICADO` | `IconoLogin.tsx` y `MarcaCalimod.tsx` (523 → 386 líneas) |
| Vitest y Testing Library | `APLICADO` | 30 pruebas |
| Tipos generados desde OpenAPI | `NO APLICADO` | D16 |

### 5.6 Capa de IA (§7)

| Punto | Estado | Detalle |
|---|---|---|
| Herramientas solo del catálogo, con esquema y límites | `YA EXISTÍA` | Más la validación de los parámetros de la acción contra el esquema del ejecutor |
| Decisión de ejecutar en el backend, no en el prompt | `APLICADO` | `PoliticaAutonomia` + `PrepararCambio` |
| Registro de pedidos no permitidos | `APLICADO` | `ACCION_RECHAZADA` |
| Pruebas adversarias | `APLICADO` (parcial) | Validador de SQL, redactor y política con casos adversos; falta la evaluación con el modelo real |
| Privacidad y enmascaramiento | `YA EXISTÍA` | D8 pendiente |
| Salidas estructuradas y prompts versionados | `APLICADO` | Esquemas JSON; versión `agente-ti-2026-10-07` guardada en cada llamada |
| Conjunto de evaluación con históricos | `PENDIENTE DE DECISIÓN` | Requiere seleccionar con TI los casos y sus resultados esperados |
| Modo sombra con comparación | `APLICADO` | Interruptor y comparativo en Reportes |
| Interruptor global con cuatro modos | `APLICADO` | `TI_Parametro.AGENTE_MODO` |
| Degradación sin IA, límites de costo y tasa, adjuntos | `YA EXISTÍA` | Además se registran tokens y duración por llamada |

### 5.7 Calidad y documentación (§8)

| Punto | Estado | Detalle |
|---|---|---|
| Pruebas de política, SQL, backend y frontend | `APLICADO` | Ver `01_DOCUMENTACION_TECNICA.md` §30 |
| Matriz de autorización por endpoint y perfil | `APLICADO` | `MatrizAutorizacionPruebas` |
| CI con SQL Server en contenedor, escaneo de secretos y dependencias | `APLICADO` | `.github/workflows/ci.yml`; gitleaks local: 0 hallazgos en 184 commits |
| Migraciones numeradas e idempotentes; `08` actualizado | `YA EXISTÍA` + `APLICADO` | `08` cubre los 38 objetos de tabla y va al final |
| Estado vigente, diseño, política, runbook y decisiones | `APLICADO` | `01`, `02`, `03`, `05_RunbookAgente.md` y este registro |
| Hashes de usuarios DEV documentados | `YA EXISTÍA` | `13_CredencialesDesarrollo.sql` |

### 5.8 Anexo A (inconsistencias)

| # | Estado | Detalle |
|---|---|---|
| 1 | `APLICADO` | `07_DatosIniciales.sql` es la única fuente de `TI_Perfil`; `09` ya no la duplica |
| 2 | `APLICADO` | `@cEstadoActivo = 'A'`; el instalador ejecuta `07` |
| 3 | `APLICADO` | Constante única en `AutenticacionBLL` |
| 4 | `APLICADO` | Correlación única |
| 5 | `APLICADO CON VARIANTE` | Sin procedimiento genérico (§5.4) |
| 6 a 8 | `YA EXISTÍA` | `docs/02_EstadoActualProyecto.md` ya no existe; lo reemplazan los documentos vigentes |
| 9 | `PENDIENTE DE DECISIÓN` | D23 |
| 10 | `APLICADO` | Diagnósticos del seed en P/V |
| 11 | `APLICADO CON VARIANTE` | D13 |
| 12 | `NO APLICADO` | D14 |
| 13 | `APLICADO` | Proyecto de pruebas en la solución |

### 5.9 Anexo B (métricas)

Todas salen de `Usp_TI_Obtener_MetricasAgente` y se ven en Reportes → Agente: exactitud de clasificación por tipo, subtipo e ítem; aprobaciones y motivos de rechazo; completitud de requerimientos y devueltos; reaperturas; tiempos a primera respuesta y a resolución por tipo; ejecuciones con error (no hay `RV`, D18); calificación; tokens, duración y fallas por modelo; utilidad del conocimiento. El **acierto del diagnóstico** y las **solicitudes autónomas erróneas** se evalúan con el comparativo caso por caso, porque requieren el juicio de TI.

## 6. Hallazgos durante la aplicación

| Hallazgo | Qué se hizo |
|---|---|
| 54 procedimientos y triggers creados sin `QUOTED_IDENTIFIER ON` (scripts 10 a 22 aplicados sin `-I`): fallaban al escribir en tablas con índices filtrados, por ejemplo al solicitar una aprobación | Script 39 los recompila; el instalador usa `-I` |
| La regla del responsable de una investigación automática quedó duplicada al hacer durable la investigación | `Usp_TI_Agente_PrepararInvestigacionAutomatica` llama a `Usp_TI_Agente_ResolverOperadorAutomatico`; una sola definición |
| Tipos del legado activos junto a INC, REQ y SOL | Documentado como riesgo y decisión D15 |
| `database/README.md` decía consentimiento `REPRODUCCION_V1` y la cabecera de `ReproduccionUsuarioPage.tsx` decía que la pantalla no se graba | Corregidos (`REPRODUCCION_V2`, grabación con consentimiento) |
| `source-map-js` 1.2.1 con una vulnerabilidad alta (dependencia de desarrollo de Vite) | Actualizada a 1.2.2 con `npm audit fix` (sin `--force`) |
| `X-Correlation-ID` faltaba en las respuestas rechazadas antes de llegar al controlador | `CorrelacionMiddleware` se movió antes del CSRF, del límite y de la autorización |
