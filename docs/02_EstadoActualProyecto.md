# Estado actual del proyecto — Sistema de Tickets Inteligente

> **Documento histórico:** este archivo conserva el estado de la etapa de diseño de base de datos al 7 de septiembre de 2026. La estructura vigente de backend y frontend se documenta en [03_EstructuraSolucion.md](03_EstructuraSolucion.md).

**Fecha de actualización:** 7 de septiembre de 2026  
**Rama:** `main`  
**Último commit al preparar este documento:** `7fd421c Documentación sobre la BD`

## 1. Finalidad

Este documento conserva el contexto necesario para continuar el proyecto sin reconstruir decisiones ya tomadas. Resume el trabajo realizado, el estado real del repositorio, las reglas aprobadas, las validaciones disponibles, los prerrequisitos pendientes y el siguiente paso técnico.

La versión actual del repositorio es la fuente de verdad. Los scripts SQL no deben sustituirse por borradores anteriores ni modificarse por preferencias personales de formato o arquitectura.

El detalle funcional de tablas y columnas se encuentra en [01_DiccionarioDatos.md](01_DiccionarioDatos.md). El DDL de `database/` continúa siendo la definición física ejecutable.

## 2. Principios de trabajo aprobados

- Desarrollo conjunto: diseñar, entender, implementar, revisar, probar y aprender.
- Primero legible; después corto.
- Usar la menor complejidad necesaria sin sacrificar integridad.
- Conservar las convenciones empresariales existentes.
- Usar nombres nuevos en español.
- No renombrar objetos por preferencia personal.
- No crear capas, tablas, dependencias o validaciones sin una necesidad concreta.
- Explicar antes cualquier cambio de tablas, columnas, PK, FK, tipos, nulabilidad o reglas de negocio.
- No hacer commits ni ejecutar scripts automáticamente sin autorización.
- Mantener separadas las configuraciones y credenciales de desarrollo y producción.

## 3. Estructura actual del repositorio

```text
SistemaDeTicketsInteligente/
├── .editorconfig
├── .gitignore
├── database/
│   ├── 00_CrearBaseDatos.sql
│   ├── 01_Maestros.sql
│   ├── 02_Incidencias.sql
│   ├── 03_Inteligencia.sql
│   ├── 04_AccionesControl.sql
│   ├── 05_Auditoria.sql
│   ├── 06_Indices.sql
│   ├── 07_DatosIniciales.sql
│   ├── 08_Validacion.sql
│   └── 09_InsertDeDatos.sql
└── docs/
    ├── 01_DiccionarioDatos.md
    └── 02_EstadoActualProyecto.md
```

Todavía no existen `backend/`, `frontend/`, proyectos `.csproj`, solución `.sln` ni `global.json`.

## 4. Historial versionado relevante

Al preparar este documento, el repositorio tenía tres commits:

| Commit | Descripción |
|---|---|
| `8002e94` | Commit inicial |
| `6827092` | Creación de la Base de Datos |
| `7fd421c` | Documentación sobre la BD |

`HEAD`, `origin/main` y `origin/HEAD` apuntaban a `7fd421c`. El árbol de trabajo estaba limpio antes de crear este documento.

Después de iniciar esta documentación se detectó una modificación local ajena a este documento en `database/09_InsertDeDatos.sql`: se agregó una inserción de los perfiles `USR`, `TEC`, `SUP` y `ADM`. El cambio se conservó intacto y todavía no está versionado.

Debe revisarse antes de ejecutar porque el mismo archivo conserva un comentario que indica que esos perfiles pertenecen a `07_DatosIniciales.sql`. Si se ejecuta primero `07_DatosIniciales.sql` y ambos archivos insertan las mismas PK, `09_InsertDeDatos.sql` fallará por duplicidad. Se debe elegir una sola responsabilidad para esa carga y actualizar el comentario correspondiente; este documento no toma esa decisión automáticamente.

## 5. Estado consolidado de la base de datos

| Objeto | Cantidad actual |
|---|---:|
| Tablas | 24 |
| Primary Keys | 24 |
| Foreign Keys | 56 |
| Restricciones Unique | 2 |
| Restricciones Check | 9 |
| Índices secundarios | 8 |
| Columnas Identity | 1 |

La única identidad automática pertenece a `TI_Auditoria.AuditoriaNumero`. Las entidades empresariales conservan claves naturales o compuestas.

### 5.1 Maestros

`01_Maestros.sql` crea:

1. `TI_Area`
2. `TI_Linea`
3. `TI_Cargo`
4. `TI_Perfil`
5. `TI_Usuario`
6. `TI_Item`
7. `TI_Tipo`
8. `TI_Categoria`
9. `TI_SubTipo`
10. `TI_Estado`
11. `TI_ItemCategoria`

Las claves naturales se conservan cuando representan códigos empresariales. Las FK usan columnas de tipo y longitud compatibles con sus padres.

### 5.2 Operación de incidencias

`02_Incidencias.sql` crea:

1. `TI_Incidencia`
2. `TI_IncidenciaAvance`
3. `TI_IncidenciaEstado`
4. `TI_IncidenciaMensaje`
5. `TI_IncidenciaAdjunto`
6. `TI_IncidenciaDocumento`

`TI_Incidencia` es la cabecera del ticket. Sus hijos utilizan `IncidenciaNumero` y, cuando corresponde, `Secuencia` como PK compuesta.

`CompaniaSocio` no forma parte de la identidad de una incidencia. Solo aparece en `TI_IncidenciaDocumento`, donde ayuda a identificar un documento ERP.

La incidencia funciona como unidad de conversación; no se creó una tabla `TI_Conversacion`.

### 5.3 Conocimiento y diagnóstico

`03_Inteligencia.sql` crea:

1. `TI_BaseConocimiento`
2. `TI_IncidenciaDiagnostico`
3. `TI_IncidenciaDiagnosticoEvidencia`

Estas tablas preparan información funcional para conocimiento y diagnóstico, pero no implementan todavía modelos de IA, prompts, embeddings, vectores, agentes ni RAG.

### 5.4 Acciones controladas

`04_AccionesControl.sql` crea:

1. `TI_Accion`
2. `TI_SolicitudAprobacion`
3. `TI_EjecucionAccion`

`TI_Accion.Tipo` utiliza:

- `L`: lectura sin cambios.
- `E`: ejecución controlada.

`TI_EjecucionAccion.ClaveIdempotencia` es `uniqueidentifier` y posee una restricción Unique. Su finalidad es impedir que un mismo intento lógico se procese dos veces por reintentos.

Todavía no existe ejecución automática sobre ERP.

### 5.5 Auditoría

`05_Auditoria.sql` crea `TI_Auditoria`.

La tabla conserva trazabilidad transversal. `AuditoriaNumero bigint Identity(1,1)` es una excepción justificada porque un evento de auditoría no posee una clave natural útil y puede alcanzar un volumen elevado.

`IdCorrelacion uniqueidentifier` permite asociar varios eventos con una misma operación técnica o funcional.

### 5.6 Integridad referencial

- Las FK están declaradas con `With Check`.
- No se usa `With Nocheck` para esconder datos inválidos.
- No se usa `On Delete Cascade` en históricos.
- Las FK compuestas reutilizan la clave completa del padre.
- `TI_Item` conserva una clave candidata `(Linea, Item)` porque incidencias y conocimiento validan la pertenencia del ítem a su línea.
- `TI_SubTipo` conserva la PK `(Tipo, SubTipo, Categoria)`.
- `TI_ItemCategoria` conserva la PK `(Item, Categoria)`.

### 5.7 Checks actuales

Las nueve restricciones Check protegen reglas conocidas, entre ellas:

- porcentaje de avance entre 0 y 100;
- tipo de autor `U`, `T`, `I` o `S`;
- tamaño de adjunto no negativo;
- origen de diagnóstico;
- confianza y similitud entre 0 y 100;
- tipo de acción `L` o `E`;
- estado de solicitud de aprobación;
- filas afectadas no negativas.

No se inventaron rangos para prioridad, impacto o complejidad.

### 5.8 Índices actuales

`06_Indices.sql` contiene ocho índices secundarios dirigidos a:

- incidencias del solicitante;
- cola del área TI;
- incidencias del técnico asignado;
- clasificación de incidencias;
- documentos ERP relacionados;
- solicitudes de aprobación;
- auditoría por correlación;
- auditoría por incidencia y fecha.

No se creó automáticamente un índice por cada FK. Los índices deben revisarse posteriormente con consultas y planes de ejecución reales.

## 6. Finalidad y orden de los scripts

| Orden | Archivo | Efecto esperado |
|---:|---|---|
| 00 | `00_CrearBaseDatos.sql` | Crear `SistemaTicketsInteligente` si no existe |
| 01 | `01_Maestros.sql` | Crear maestros y relaciones principales |
| 02 | `02_Incidencias.sql` | Crear la operación de tickets |
| 03 | `03_Inteligencia.sql` | Crear conocimiento y diagnóstico |
| 04 | `04_AccionesControl.sql` | Crear acciones, solicitudes y ejecuciones |
| 05 | `05_Auditoria.sql` | Crear auditoría transversal |
| 06 | `06_Indices.sql` | Crear índices secundarios |
| 07 | `07_DatosIniciales.sql` | Insertar perfiles iniciales después de definir el estado activo |
| 08 | `08_Validacion.sql` | Validar estructura, restricciones y relaciones mediante datos revertidos |
| 09 | `09_InsertDeDatos.sql` | Cargar datos sintéticos representativos exclusivamente en desarrollo |

Ninguno debe ejecutarse en producción por inferencia. Antes de ejecutar debe confirmarse servidor, base, autenticación, red y permisos.

`07_DatosIniciales.sql` permanece protegido por `@cEstadoActivo`. El valor debe definirse de acuerdo con la convención corporativa antes de ejecutar la carga.

`09_InsertDeDatos.sql` contiene datos sintéticos y no debe utilizarse en producción.

No existe todavía un script de migración histórica. Se creará únicamente después de inspeccionar datos reales del sistema anterior.

## 7. Estándar T-SQL vigente

- Keywords con inicial mayúscula: `Select`, `From`, `Where`, `Create`, `Alter`, `Begin`, `End`.
- Tipos en minúscula: `varchar`, `char`, `nvarchar`, `int`, `bigint`, `decimal`, `datetime2`, `bit`, `uniqueidentifier`.
- Conectores en minúscula: `as`, `on`, `and`, `or`, `is`, `not`, `in`, `like`, `between`.
- Funciones con el formato usado en el repositorio: `IsNull()`, `Count()`, `Convert()`, `Db_Id()`, `Object_Id()`.
- Indentación con tabs reales y ancho visual de cuatro posiciones.
- Columnas y restricciones alineadas según los scripts actuales.
- Alias en minúscula y solo cuando ayudan a leer la consulta.
- Comentarios reservados para intención, reglas, riesgos y decisiones no evidentes.
- Prefijo definitivo `TI_` para objetos del módulo.

`.editorconfig` fija UTF-8, tabs, ancho visual de cuatro, eliminación de espacios finales y salto final de línea.

`.gitignore` excluye artefactos de .NET, Node, IDE, variables de entorno, secretos locales y archivos físicos o respaldos de SQL Server.

## 8. Diagnóstico del entorno de desarrollo

Inspección realizada sin instalar software y sin intentar conectarse a ningún servidor.

| Elemento | Estado encontrado |
|---|---|
| Sistema operativo | Windows 11 Pro, 64 bits, build 26200 |
| PowerShell | 5.1.26100.9278 |
| Git | 2.54.0.windows.1 |
| Visual Studio Code | 1.136.1, x64 |
| `dotnet` | No disponible en `PATH` |
| SDK .NET | No detectado |
| Runtime .NET | No detectado |
| Proyecto o solución .NET | No existe |
| `global.json` | No existe |
| `sqlcmd` | No encontrado |
| SSMS | No encontrado |
| `Invoke-Sqlcmd` | No encontrado |
| `sqlpackage` | No encontrado |
| SQL LocalDB | No encontrado |
| Servicio SQL Server local | No encontrado |
| Driver ODBC | Driver heredado “SQL Server” disponible en 32 y 64 bits |
| Servidor SQL DEV | No identificado |
| Connection strings en el repositorio | No encontradas |
| Variables de entorno de conexión | No encontradas |

El `PATH` contiene carpetas de herramientas globales de .NET, pero no existe el ejecutable principal `dotnet`. Esto no demuestra la presencia de un SDK.

VS Code contiene algunas extensiones SQL genéricas, pero no se confirmó la extensión oficial MSSQL ni un driver MSSQL específico para SQLTools. Esto no garantiza acceso a SQL Server DEV.

## 9. Información corporativa pendiente

### 9.1 .NET

Antes de crear el backend se debe confirmar:

1. Target Framework utilizado por los sistemas web InHouse nuevos.
2. Versión de SDK aprobada.
3. Runtime o Hosting Bundle disponible en los servidores.
4. Uso corporativo de `global.json`.
5. Proyecto empresarial reciente que pueda servir como referencia.
6. Fuentes NuGet, plantillas o políticas obligatorias.

No se debe elegir automáticamente `net8.0`, `net9.0`, `net10.0` u otra versión sin esta evidencia.

### 9.2 SQL Server DEV

Antes de cualquier conexión o ejecución se debe obtener:

```text
Servidor DEV:
Instancia o puerto:
Base DEV:
Autenticación:
Usuario o identidad:
VPN/red necesaria:
Permisos:
Herramienta corporativa:
```

No se deben guardar contraseñas en `appsettings.json`, código C#, README, scripts SQL ni Git.

La identidad que despliegue los scripts puede necesitar permisos DDL o creación de base. La futura identidad de la API debe poseer solo los permisos mínimos de operación y no debe utilizar `sa` ni privilegios administrativos.

## 10. Campos y reglas funcionales pendientes

El diccionario de datos registra actualmente estos puntos abiertos:

1. Definir la semántica y relación de `TI_Tipo.Item`.
2. Definir el dominio y transiciones de `TI_IncidenciaDiagnostico.Estado`.
3. Definir el catálogo final de `TI_EjecucionAccion.Estado`.
4. Validar el significado y rangos de prioridad, impacto y complejidad.
5. Confirmar la unidad de `TI_IncidenciaAvance.TiempoUtilizado`.
6. Decidir autenticación local o SSO/Active Directory y revisar `TI_Usuario.Clave`.
7. Definir cómo genera la empresa el correlativo de `IncidenciaNumero`.
8. Definir la generación segura de secuencias dentro de una operación transaccional.
9. Confirmar la estrategia clustered cuando se conozca el patrón del correlativo.
10. Analizar la base histórica antes de diseñar la migración.

## 11. Primer vertical slice aprobado

El siguiente objetivo es:

```text
SQL Server DEV
→ ASP.NET Core API
→ consulta de TI_Incidencia
→ GET /api/incidencias
→ Swagger/OpenAPI
```

React queda fuera de esta primera prueba.

El backend inicial será un único proyecto API organizado progresivamente por funcionalidad. No se agregarán inicialmente Clean Architecture con múltiples proyectos, CQRS, MediatR, AutoMapper, Repository Pattern, Unit of Work, login, JWT, IA, RAG, chat, dashboard ni CRUD completo.

La respuesta del endpoint utilizará un DTO específico, por ejemplo `IncidenciaResumenDto`, y no devolverá todas las columnas de `TI_Incidencia`.

Antes de elegir Dapper, `Microsoft.Data.SqlClient` o Entity Framework Core se explicarán sus diferencias y se aprobará la alternativa. La empresa utiliza SQL Server, procedimientos almacenados y SQL explícito, por lo que la elección debe respetar ese contexto.

## 12. Seguridad aprobada para el backend

- Nunca conectar a producción durante desarrollo.
- No subir connection strings con contraseña.
- No hardcodear credenciales.
- No utilizar `sa`.
- No conceder permisos administrativos a la API.
- Usar configuración local, variables de entorno o User Secrets según el stack aprobado.
- Mantener el controller pequeño y sin SQL directo.
- Separar HTTP, servicio de incidencias y consulta SQL sin crear capas innecesarias.

## 13. Siguiente paso exacto

El proyecto está detenido correctamente en los prerrequisitos.

Se necesita obtener:

1. Target Framework y SDK .NET aprobados por la empresa.
2. Datos no secretos de SQL Server DEV.
3. Confirmación de la herramienta corporativa para ejecutar scripts.
4. Código corporativo de estado activo requerido por `07_DatosIniciales.sql`.

Una vez disponible esa información:

1. Comprobar o instalar manualmente el SDK aprobado.
2. Identificar inequívocamente servidor y base DEV.
3. Mostrar antes de ejecutar: servidor, base, autenticación, script y efecto.
4. Ejecutar los scripts solo con autorización expresa.
5. Validar la estructura en SQL Server.
6. Crear el proyecto único `backend/SistemaTickets.Api`.
7. Implementar el primer flujo `GET /api/incidencias` y Swagger.

No se avanzará a React hasta completar y comprender el flujo SQL Server → ASP.NET Core → HTTP.
