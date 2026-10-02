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
4. Ejecutar `09_InsertDeDatos.sql` hasta `25_AsistenteIngenieriaAutonomo.sql` en el orden definido por `InstalarLocal.ps1`.
5. `08_Validacion.sql` conserva su posición de validación histórica dentro del instalador; sus registros de prueba se revierten mediante `Rollback`.

`herramientas/GenerarLegadoDesdeMarkdown.ps1` regenera los scripts de `legado/` desde los tres documentos originales y aplica únicamente las normalizaciones necesarias para que el SQL exportado sea ejecutable.

`sistema-inteligente/23_SincronizarDatosLegado.sql` conserva los accesos locales de desarrollo y reemplaza la transacción demostrativa visible por los tickets y avances reconstruidos desde `Inc20Incidencia` e `Inc21Avance`.
Para comprobar la vista de usuario normal, la copia local habilita `YPENALOZA` con la contraseña temporal de desarrollo `123456`; no modifica la clave redactada de `Inc03Usuario`.

`sistema-inteligente/25_AsistenteIngenieriaAutonomo.sql` agrega el workspace del Agente de Ingeniería, correlación de eventos Live, expediente Markdown y el contrato de ejecutores controlados. No registra correcciones ERP por defecto: cada `dbo.Usp_TI_AgenteAccion_*` debe ser validado y habilitado explícitamente por TI en `TI_AgenteAccionEjecutor`.

Para una reconstrucción local completa y destructiva puede usarse `InstalarLocal.ps1 -ConfirmarRecreacion`. El parámetro es obligatorio para evitar eliminaciones accidentales.
