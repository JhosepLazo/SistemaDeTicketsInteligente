# Scripts de base de datos

La carpeta se divide en dos grupos:

- `legado/`: reconstrucción local de `GestionSistemas`, `IntranetCalimod` y `Spring` a partir de los documentos de análisis entregados.
- `sistema-inteligente/`: extensión `TI_*` y `Usp_TI_*` requerida por el backend y aplicada dentro de `GestionSistemas`.

Los scripts de `legado/` deben ejecutarse en orden numérico. Contienen las 31 tablas cuyo DDL fue confirmado, las muestras de datos incluidas en los documentos, tres objetos de soporte requeridos por los SP y los 90 procedimientos almacenados documentados.

Los datos son muestras parciales y las credenciales aparecen redactadas como `[REDACTADO]`; por ello reproducen el material entregado, no un respaldo integral de producción.

## Orden de instalación local

1. Ejecutar `legado/00_CrearBases.sql` hasta `legado/07_CrearProcedimientosGestionSistemas.sql`.
2. Ejecutar `sistema-inteligente/00_PrepararGestionSistemas.sql` hasta `06_Indices.sql`.
3. Omitir `07_DatosIniciales.sql`: exige una decisión corporativa y los perfiles de desarrollo ya están incluidos en `09_InsertDeDatos.sql`.
4. Ejecutar `09_InsertDeDatos.sql` hasta `30_AgenteFase4HerramientasDiagnostico.sql` en el orden definido por `InstalarLocal.ps1`.
5. `08_Validacion.sql` conserva su posición de validación histórica dentro del instalador; sus registros de prueba se revierten mediante `Rollback`.

`herramientas/GenerarLegadoDesdeMarkdown.ps1` regenera los scripts de `legado/` desde los tres documentos originales y aplica únicamente las normalizaciones necesarias para que el SQL exportado sea ejecutable.

`sistema-inteligente/23_SincronizarDatosLegado.sql` conserva los accesos locales de desarrollo y reemplaza la transacción demostrativa visible por los tickets y avances reconstruidos desde `Inc20Incidencia` e `Inc21Avance`.
Para comprobar la vista de usuario normal, la copia local habilita `YPENALOZA` con la contraseña temporal de desarrollo `123456`; no modifica la clave redactada de `Inc03Usuario`.

`sistema-inteligente/25_AsistenteIngenieriaAutonomo.sql` agrega el workspace del Agente de Ingeniería, correlación de eventos Live, expediente Markdown y el contrato de ejecutores controlados. No registra correcciones ERP por defecto: cada `dbo.Usp_TI_AgenteAccion_*` debe ser validado y habilitado explícitamente por TI en `TI_AgenteAccionEjecutor`.

`sistema-inteligente/27_AgenteFase1Integracion.sql` integra el agente con el ciclo del ticket: la aprobación de una acción del agente bloquea el ticket en `PA` y notifica a los aprobadores, el solicitante no puede responder su propia aprobación, cancelar o grabar la investigación desbloquea el ticket, y cada decisión deja una nota interna. Registra dos ejecutores que solo operan sobre datos propios de `GestionSistemas` (no ERP): `ACC-007` reactiva la cuenta `USR` del solicitante del ticket y `ACC-004` libera un ticket en `PA` sin aprobación pendiente. Ambos se registran únicamente si la acción existe en `TI_Accion`.

`sistema-inteligente/28_AgenteFase2Integracion.sql` conecta las investigaciones con Gestión de Tickets y la supervisión: SUP/ADM consultan todas las investigaciones (solo lectura) y pueden reasignarlas, cualquier operador TI ve los expedientes del agente desde el detalle del ticket, y la consola obtiene áreas y operadores para cerrar el caso. Refuerza la segregación: tampoco aprueba quien es responsable actual de la investigación que originó la solicitud, y un ticket en `PA` ya no puede resolverse.

`sistema-inteligente/29_AgenteFase3ReproduccionUsuario.sql` incorpora al usuario final en la observación: TI invita al solicitante del ticket (vigencia de 24 horas), el usuario acepta con consentimiento versionado (`REPRODUCCION_V1`) o rechaza, y mientras la invitación está aceptada puede aportar transcripción y el error observado (fuente `LIVE_USUARIO`) y sus solicitudes al portal se correlacionan como telemetría. El usuario nunca accede al diagnóstico ni al expediente.

`sistema-inteligente/30_AgenteFase4HerramientasDiagnostico.sql` convierte el diagnóstico en una investigación de varios pasos: cataloga herramientas de solo lectura (`TI_AgenteHerramienta`, procedimientos `dbo.Usp_TI_AgenteDiag_*`) que el backend ejecuta en una transacción siempre revertida y registra como evidencia del servidor, permite simular la acción propuesta con sus parámetros reales sin persistir cambios (el dry-run exige que la última simulación sea exitosa y con los mismos parámetros) y admite los pasos que Live registra mediante la función `registrar_paso` (`PASO_OBSERVADO`). Para agregar una herramienta basta crear el procedimiento con el contrato común y registrarlo en el catálogo; el modelo nunca elige el procedimiento ni genera SQL.

Para una reconstrucción local completa y destructiva puede usarse `InstalarLocal.ps1 -ConfirmarRecreacion`. El parámetro es obligatorio para evitar eliminaciones accidentales.
