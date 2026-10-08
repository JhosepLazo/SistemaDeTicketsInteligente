# Scripts de base de datos

La carpeta se divide en dos grupos:

- `legado/`: reconstrucción local de `GestionSistemas`, `IntranetCalimod` y `Spring` a partir de los documentos de análisis entregados.
- `sistema-inteligente/`: extensión `TI_*` y `Usp_TI_*` requerida por el backend y aplicada dentro de `GestionSistemas`.

Los scripts de `legado/` deben ejecutarse en orden numérico. Contienen las 31 tablas cuyo DDL fue confirmado, las muestras de datos incluidas en los documentos, tres objetos de soporte requeridos por los SP y los 90 procedimientos almacenados documentados.

Los datos son muestras parciales y las credenciales aparecen redactadas como `[REDACTADO]`; por ello reproducen el material entregado, no un respaldo integral de producción.

## Orden de instalación local

1. Ejecutar `legado/00_CrearBases.sql` hasta `legado/07_CrearProcedimientosGestionSistemas.sql`.
2. Ejecutar `sistema-inteligente/00_PrepararGestionSistemas.sql` hasta `07_DatosIniciales.sql` (este último es la única fuente de los perfiles `TI_Perfil`).
3. Ejecutar `09_InsertDeDatos.sql` hasta `40_SeguridadSesion.sql` en el orden definido por `InstalarLocal.ps1`.
4. Ejecutar `08_Validacion.sql` al final: sus listas esperadas cubren los objetos de todos los scripts (38 tablas, 83 claves foráneas, 3 claves únicas, 53 restricciones CHECK, 17 índices y 5 triggers habilitados) y sus registros de prueba se revierten mediante `Rollback`.

`InstalarLocal.ps1` acepta `-Servidor` (por defecto `.`) y `-Usuario`; con usuario, la contraseña se lee de la variable de entorno `SQLCMDPASSWORD` y nunca se escribe en el script. Ejecuta cada archivo con `sqlcmd -C -I -l 20 -b -f 65001`.

`pruebas/PruebasFuncionales.sql` comprueba las reglas de la base con 22 casos, cada uno dentro de una transacción que siempre se revierte (no deja datos): dominios cerrados, máquina de estados, aprobación ligada al diagnóstico y con vencimiento, segregación de funciones, ejecución que exige aprobación, idempotencia, ejecución autónoma y sus rechazos, reconciliación de ejecuciones, investigación automática pendiente, autocierre, ficha del requerimiento y exclusión de los mensajes internos. Se ejecuta después de la instalación con `sqlcmd -S . -E -C -I -b -f 65001 -i database/pruebas/PruebasFuncionales.sql`; si un caso falla, termina con `Throw 50699`.

Los scripts de `legado/` se generaron una sola vez a partir de los documentos de análisis originales, que no forman parte del repositorio; no se regeneran.

Cada procedimiento tiene una sola definición vigente en `sistema-inteligente/`: cuando un script posterior lo reemplaza (`Create Or Alter`), el anterior solo conserva una línea que indica dónde está la versión vigente. Por eso los scripts se ejecutan completos y en orden, como lo hace `InstalarLocal.ps1`, siempre con `sqlcmd -I -f 65001` para leerlos como UTF-8 y con `QUOTED_IDENTIFIER ON` (sin `-I`, los procedimientos que escriben en tablas con índices filtrados fallan al ejecutarse).

`GenerarHash.cs` genera el hash de una contraseña de desarrollo compatible con ASP.NET Core Identity (`dotnet run database/GenerarHash.cs`); pide la contraseña dos veces sin mostrarla en pantalla. Solo hace falta para crear o cambiar una contraseña local distinta de las que ya prepara `13_CredencialesDesarrollo.sql`.

`sistema-inteligente/23_SincronizarDatosLegado.sql` conserva los accesos locales de desarrollo y reemplaza la transacción demostrativa visible por los tickets y avances reconstruidos desde `Inc20Incidencia` e `Inc21Avance`.
Para comprobar la vista de usuario normal, la copia local habilita `YPENALOZA` con la contraseña temporal de desarrollo `123456`; no modifica la clave redactada de `Inc03Usuario`.

`sistema-inteligente/25_AsistenteIngenieriaAutonomo.sql` agrega el workspace del Agente de Ingeniería, correlación de eventos Live, expediente Markdown y el contrato de ejecutores controlados. No registra correcciones ERP por defecto: cada `dbo.Usp_TI_AgenteAccion_*` debe ser validado y habilitado explícitamente por TI en `TI_AgenteAccionEjecutor`.

`sistema-inteligente/27_AgenteFase1Integracion.sql` integra el agente con el ciclo del ticket: la aprobación de una acción del agente bloquea el ticket en `PA` y notifica a los aprobadores, el solicitante no puede responder su propia aprobación, cancelar o grabar la investigación desbloquea el ticket, y cada decisión deja una nota interna. Registra dos ejecutores que solo operan sobre datos propios de `GestionSistemas` (no ERP): `ACC-007` reactiva la cuenta `USR` del solicitante del ticket y `ACC-004` libera un ticket en `PA` sin aprobación pendiente. Ambos se registran únicamente si la acción existe en `TI_Accion`.

`sistema-inteligente/28_AgenteFase2Integracion.sql` conecta las investigaciones con Gestión de Tickets y la supervisión: SUP/ADM consultan todas las investigaciones (solo lectura) y pueden reasignarlas, cualquier operador TI ve los expedientes del agente desde el detalle del ticket, y la consola obtiene áreas y operadores para cerrar el caso. Refuerza la segregación: tampoco aprueba quien es responsable actual de la investigación que originó la solicitud, y un ticket en `PA` ya no puede resolverse.

`sistema-inteligente/29_AgenteFase3ReproduccionUsuario.sql` incorpora al usuario final en la observación: TI invita al solicitante del ticket (vigencia de 24 horas), el usuario acepta con consentimiento versionado (el script se creó con `REPRODUCCION_V1`; el backend vigente envía `REPRODUCCION_V2`, que incluye la grabación de la pantalla) o rechaza, y mientras la invitación está aceptada puede aportar transcripción y el error observado (fuente `LIVE_USUARIO`) y sus solicitudes al portal se correlacionan como telemetría. El usuario nunca accede al diagnóstico ni al expediente.

`sistema-inteligente/30_AgenteFase4HerramientasDiagnostico.sql` convierte el diagnóstico en una investigación de varios pasos: cataloga herramientas de solo lectura (`TI_AgenteHerramienta`, procedimientos `dbo.Usp_TI_AgenteDiag_*`) que el backend ejecuta en una transacción siempre revertida y registra como evidencia del servidor, permite simular la acción propuesta con sus parámetros reales sin persistir cambios (el dry-run exige que la última simulación sea exitosa y con los mismos parámetros) y admite los pasos que Live registra mediante la función `registrar_paso` (`PASO_OBSERVADO`). Para agregar una herramienta basta crear el procedimiento con el contrato común y registrarlo en el catálogo; el modelo nunca elige el procedimiento ni genera SQL.

`sistema-inteligente/31_AgenteFase5ConocimientoSemantico.sql` agrega la búsqueda por significado: guarda los vectores (embeddings) de los artículos activos y de los tickets resueltos en `TI_ConocimientoVector` y registra la herramienta interna `DIAG_CONOCIMIENTO_SEMANTICO`. El colaborador solo busca en artículos visibles para usuarios; los tickets resueltos solo forman parte del corpus de TI.

`sistema-inteligente/32_AgenteFase6ReplicaTecnica.sql` permite que el agente replique técnicamente lo que el usuario mostró: registra las grabaciones de pantalla consentidas (adjunto del ticket y evidencia `GRABACION_PANTALLA`), cataloga las herramientas internas de réplica técnica (análisis de la grabación, búsqueda y lectura de código fuente, búsqueda de objetos, definición y estructura de la base, y `SELECT` de solo lectura), permite reabrir la observación conservando el diagnóstico anterior, importa la evidencia que el colaborador mostró al registrar su ticket, elige el operador responsable de la investigación automática y le notifica el diagnóstico. Desde este script los archivos SQL del agente se guardan con BOM UTF-8 y el instalador usa `-f 65001`.

`sistema-inteligente/33_AgenteFase6Mejoras.sql` permite que el responsable TI del ticket consulte y tome la investigación del agente (antes solo podía un supervisor; quien la tenía recibe un aviso) y vincula a la investigación los videos adjuntados al ticket para que el agente también los analice.

`sistema-inteligente/34_DominiosYMaquinaEstados.sql` cierra con restricciones CHECK los dominios que antes solo validaban los procedimientos (tipo de actor de la auditoría, ejecución con su fecha de fin, respuesta de una aprobación, estados de conocimiento y diagnóstico, niveles 1 a 5, tipo de resolución, canal de registro, tipo de usuario, nivel de riesgo y estados de investigación) y crea la máquina de estados del ticket: `TI_EstadoTransicion` (65 transiciones que reflejan el comportamiento real, incluidos los estados del legado) y el trigger `Tr_TI_Incidencia_TransicionEstado`, que rechaza cualquier otro cambio con el error 50600. Las versiones vigentes de `Usp_TI_Agente_ValidarSolucion` y `Usp_TI_Agente_Cancelar` están aquí.

`sistema-inteligente/35_ControlAgenteYAutonomia.sql` da a TI el control del agente: `TI_Parametro` (interruptor `AGENTE_MODO` en `ASISTIDO`, techo de riesgo autónomo `MUY_BAJO`, confianza mínima para proponer 60, vigencia de aprobaciones y autocierre desactivados), `TI_PoliticaAutonomia` (todas las combinaciones de `INC`, `SOL` y `REQ` con las acciones de ejecución en `APROBACION`: nada es autónomo hasta que un ADM lo libere), la reversibilidad de cada acción, el esquema de parámetros de los ejecutores de `ACC-007` y `ACC-004`, la aprobación ligada al diagnóstico y con vencimiento, el tipo de ejecutor (TI o autónomo), los eventos que registra el servidor, la investigación automática durable, la reconciliación de ejecuciones interrumpidas y el autocierre de tickets en PV. La versión vigente de `Usp_TI_Agente_PrepararCambio` vuelve a comprobar en la base la regla compuesta de autonomía (errores 50601 a 50611).

`sistema-inteligente/36_ClasificacionFichaYGuias.sql` agrega el historial de clasificaciones (`TI_IncidenciaClasificacion`: propuestas de la IA y aplicadas por TI), la ficha estructurada por tipo de ticket (`TI_PlantillaCampo`, con los 29 campos del requerimiento en seis bloques, 18 obligatorios y activos) y sus respuestas (`TI_IncidenciaDato`), y la guía de diagnóstico de los artículos (`TI_BaseConocimiento.GuiaDiagnosticoJson`). `Usp_TI_Registrar_Incidencia` exige la ficha del tipo y recibe el canal de registro; un ticket no puede cambiarse a un tipo con ficha obligatoria.

`sistema-inteligente/37_MetricasAgente.sql` entrega los indicadores del agente para Reportes TI y el comparativo caso por caso entre su diagnóstico y la causa raíz confirmada por TI.

`sistema-inteligente/38_PermisosMinimos.sql` crea los roles `Rol_TI_Api` (procedimientos de la aplicación), `Rol_TI_AgenteLectura` (solo `Usp_TI_AgenteDiag_*`) y `Rol_TI_AgenteEscritura` (solo `Usp_TI_AgenteAccion_*`), sin acceso directo a tablas. No crea logins: cada ambiente asigna los suyos a esos roles. Otorga los permisos según los procedimientos que existen al ejecutarlo, por lo que debe volver a ejecutarse cuando se agrega un procedimiento.

`sistema-inteligente/39_CorreccionQuotedIdentifier.sql` recompila, sin cambiar su definición, los procedimientos y triggers que se habían creado sin `QUOTED_IDENTIFIER ON` (los scripts 10 a 22 se aplicaban sin `-I`); sin esa corrección, por ejemplo, solicitar una aprobación fallaba al escribir en una tabla con índice filtrado.

`sistema-inteligente/40_SeguridadSesion.sql` devuelve el tipo de usuario al autenticar, para que una cuenta de sistema no pueda iniciar sesión.

Para una reconstrucción local completa y destructiva puede usarse `InstalarLocal.ps1 -ConfirmarRecreacion`. El parámetro es obligatorio para evitar eliminaciones accidentales.
