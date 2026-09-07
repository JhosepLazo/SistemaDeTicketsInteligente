# Diccionario de Datos — Sistema de Tickets Inteligente

## 1. Objetivo

Este documento describe el significado funcional y técnico de las tablas y columnas del módulo `TI_`. Su finalidad es que cualquier desarrollador pueda entender qué representa cada dato, por qué existe, cómo se relaciona y qué reglas deben respetarse al utilizarlo.

El DDL contenido en `database/` continúa siendo la fuente ejecutable de la estructura. Este documento explica la semántica del modelo; no sustituye las restricciones físicas definidas en SQL Server.

## 2. Convenciones generales

- `TI_` identifica objetos pertenecientes al módulo de Tecnologías de Información.
- Las claves naturales se conservan cuando representan códigos empresariales estables.
- `Secuencia` identifica registros hijos dentro de una entidad padre y normalmente forma parte de una Primary Key compuesta.
- `UltimoUsuario` y `UltimaFechaModif` representan la última modificación administrativa realizada sobre un maestro. No sustituyen a `TI_Auditoria`.
- Los campos `Estado` de maestros representan vigencia del registro. Los códigos exactos deben respetar el catálogo corporativo; los datos sintéticos de desarrollo utilizan `A` como activo.
- Los campos `nvarchar` se utilizan para contenido humano o generado por IA que puede contener caracteres Unicode.
- Los campos `varchar` y `char` se utilizan principalmente para códigos y textos controlados.
- Los valores `Null` significan que el dato no aplica, aún no existe o depende de una etapa posterior del flujo. No debe utilizarse una cadena vacía para representar ausencia.
- Las contraseñas nunca deben almacenarse en texto legible. Mientras exista la columna `TI_Usuario.Clave`, debe contener únicamente un hash seguro cuando se implemente autenticación local.

## 3. Relaciones principales

```text
TI_Area
 ├─ TI_Linea
 └─ TI_Usuario

TI_Linea
 └─ TI_Item

TI_Tipo + TI_Categoria
 └─ TI_SubTipo

TI_Item + TI_Categoria
 └─ TI_ItemCategoria

TI_Incidencia
 ├─ TI_IncidenciaAvance
 ├─ TI_IncidenciaEstado
 ├─ TI_IncidenciaMensaje
 │   └─ TI_IncidenciaAdjunto
 ├─ TI_IncidenciaDocumento
 ├─ TI_IncidenciaDiagnostico
 │   └─ TI_IncidenciaDiagnosticoEvidencia
 ├─ TI_SolicitudAprobacion
 ├─ TI_EjecucionAccion
 └─ TI_Auditoria
```

---

# 4. Maestros

## TI_Area

**Finalidad:** representa las áreas organizacionales que participan en la creación, atención, asignación o análisis de tickets.

**Primary Key:** `Area`.

| Columna | Tipo | Null | Representa / finalidad |
|---|---|---:|---|
| `Area` | `char(3)` | No | Código corto y estable del área. Se usa como clave natural y como referencia desde usuarios, líneas e incidencias. |
| `Descripcion` | `varchar(60)` | No | Nombre funcional del área mostrado a usuarios y técnicos. |
| `Estado` | `varchar(2)` | No | Vigencia administrativa del área. Permite desactivar el registro sin eliminarlo físicamente. |
| `Telefono` | `varchar(20)` | Sí | Número o anexo general de contacto del área cuando exista. |
| `UltimoUsuario` | `varchar(20)` | Sí | Usuario que realizó la última modificación administrativa del maestro. |
| `UltimaFechaModif` | `datetime2(0)` | Sí | Fecha y hora de la última modificación administrativa. |

## TI_Linea

**Finalidad:** agrupa funcionalmente los ítems o procesos atendidos por el sistema y los vincula con un área responsable.

**Primary Key:** `Linea`.

**Foreign Key:** `Area -> TI_Area.Area`.

| Columna | Tipo | Null | Representa / finalidad |
|---|---|---:|---|
| `Linea` | `char(3)` | No | Código de la línea funcional. |
| `Area` | `char(3)` | No | Área propietaria o responsable de la línea. |
| `Descripcion` | `varchar(60)` | No | Nombre funcional de la línea. |
| `Estado` | `varchar(2)` | No | Vigencia del registro. |
| `UltimoUsuario` | `varchar(20)` | Sí | Último usuario que modificó el maestro. |
| `UltimaFechaModif` | `datetime2(0)` | Sí | Última fecha de modificación. |

## TI_Cargo

**Finalidad:** representa el cargo organizacional del colaborador. El cargo describe una función laboral; no determina permisos de la aplicación.

**Primary Key:** `Cargo`.

| Columna | Tipo | Null | Representa / finalidad |
|---|---|---:|---|
| `Cargo` | `char(3)` | No | Código del cargo, por ejemplo `ANA`, `JEF` o `PRA`. Se maneja como código textual y no como número secuencial. |
| `Descripcion` | `varchar(60)` | No | Nombre del cargo. |
| `Estado` | `varchar(2)` | No | Vigencia del cargo. |
| `UltimoUsuario` | `varchar(20)` | Sí | Último usuario que modificó el maestro. |
| `UltimaFechaModif` | `datetime2(0)` | Sí | Última fecha de modificación. |

## TI_Perfil

**Finalidad:** determina el perfil principal del usuario dentro del Sistema de Tickets Inteligente. Es independiente de `TI_Cargo`.

**Primary Key:** `Perfil`.

| Columna | Tipo | Null | Representa / finalidad |
|---|---|---:|---|
| `Perfil` | `char(3)` | No | Código del perfil de aplicación, por ejemplo `USR`, `TEC`, `SUP` o `ADM`. |
| `Descripcion` | `varchar(60)` | No | Nombre legible del perfil. |
| `Estado` | `varchar(2)` | No | Vigencia del perfil. |
| `UltimoUsuario` | `varchar(20)` | Sí | Último usuario que modificó el maestro. |
| `UltimaFechaModif` | `datetime2(0)` | Sí | Última fecha de modificación. |

## TI_Usuario

**Finalidad:** representa a las personas que registran, atienden, supervisan o administran tickets.

**Primary Key:** `Usuario`.

**Foreign Keys:** `Area -> TI_Area`, `Cargo -> TI_Cargo`, `Perfil -> TI_Perfil`, `Jefe -> TI_Usuario`.

| Columna | Tipo | Null | Representa / finalidad |
|---|---|---:|---|
| `Usuario` | `varchar(20)` | No | Identificador corporativo del usuario dentro del sistema. |
| `NombreCompleto` | `varchar(255)` | No | Nombre descriptivo utilizado en pantallas, asignaciones y reportes. |
| `Clave` | `varchar(100)` | No | Credencial local del usuario. **Debe contener únicamente un hash seguro; nunca una contraseña en texto plano.** Si se adopta SSO, esta columna debe revisarse. |
| `Area` | `char(3)` | No | Área actual del usuario. No sustituye `TI_Incidencia.AreaSolicitante`, que conserva el área histórica del ticket. |
| `Cargo` | `char(3)` | Sí | Cargo laboral actual del usuario. Puede ser `Null` si el dato todavía no está disponible. |
| `Perfil` | `char(3)` | No | Perfil principal de autorización dentro de la aplicación. |
| `Correo` | `varchar(100)` | Sí | Correo corporativo para contacto y futuras notificaciones. |
| `Anexo` | `varchar(10)` | Sí | Anexo telefónico cuando corresponda. |
| `Telefono` | `varchar(100)` | Sí | Teléfono de contacto cuando corresponda. |
| `Estado` | `varchar(2)` | No | Vigencia de la cuenta en el sistema. |
| `UltimoUsuario` | `varchar(20)` | Sí | Último usuario que modificó el maestro. |
| `UltimaFechaModif` | `datetime2(0)` | Sí | Última fecha de modificación. |
| `TipoUsuario` | `varchar(20)` | Sí | Clasificación general del origen del usuario, por ejemplo interno o externo. El dominio definitivo debe mantenerse controlado por la aplicación cuando sea formalizado. |
| `Jefe` | `varchar(20)` | Sí | Usuario que representa la jefatura directa. La FK autorreferencial impide registrar un jefe inexistente. |

## TI_Item

**Finalidad:** representa el objeto funcional concreto sobre el cual se reporta una incidencia o solicitud: requisición, orden de compra, stock/lote, aplicación, acceso, etc.

**Primary Key:** `Item`.

**Unique:** `(Linea, Item)`.

**Foreign Key:** `Linea -> TI_Linea`.

| Columna | Tipo | Null | Representa / finalidad |
|---|---|---:|---|
| `Item` | `varchar(20)` | No | Código funcional del ítem atendido. |
| `Linea` | `char(3)` | No | Línea a la que pertenece el ítem. |
| `Descripcion` | `varchar(255)` | No | Nombre legible del ítem. |
| `Estado` | `varchar(2)` | No | Vigencia del ítem. |
| `UltimoUsuario` | `varchar(20)` | Sí | Último usuario que modificó el maestro. |
| `UltimaFechaModif` | `datetime2(0)` | Sí | Última fecha de modificación. |

La restricción `Unique (Linea, Item)` existe para que `TI_Incidencia` pueda validar la combinación completa y evitar asociar un ítem con una línea a la que no pertenece.

## TI_Tipo

**Finalidad:** representa la naturaleza principal del ticket, por ejemplo incidencia, requerimiento o solicitud.

**Primary Key:** `Tipo`.

| Columna | Tipo | Null | Representa / finalidad |
|---|---|---:|---|
| `Tipo` | `char(3)` | No | Código principal del tipo de ticket. |
| `Descripcion` | `varchar(60)` | No | Descripción completa del tipo. |
| `Abreviatura` | `varchar(60)` | Sí | Texto abreviado utilizado en UI o reportes. |
| `Item` | `varchar(20)` | Sí | Campo opcional presente en el modelo actual para una posible asociación específica entre tipo e ítem. La regla funcional exacta todavía no está formalizada y actualmente no posee FK; no debe utilizarse para inferir una relación automática hasta definir esa regla. |
| `Estado` | `varchar(2)` | No | Vigencia del tipo. |
| `UltimoUsuario` | `varchar(20)` | Sí | Último usuario que modificó el maestro. |
| `UltimaFechaModif` | `datetime2(0)` | Sí | Última fecha de modificación. |

## TI_Categoria

**Finalidad:** representa una dimensión semántica de clasificación transversal, como datos, permisos, integración, configuración, disponibilidad o proceso.

**Primary Key:** `Categoria`.

| Columna | Tipo | Null | Representa / finalidad |
|---|---|---:|---|
| `Categoria` | `varchar(20)` | No | Código semántico de la categoría. Se utiliza texto porque la categoría representa un concepto de negocio y no una secuencia numérica. |
| `Descripcion` | `varchar(60)` | No | Nombre legible de la categoría. |
| `Abreviatura` | `varchar(3)` | Sí | Código abreviado de tres caracteres para UI o reportes. |
| `Estado` | `varchar(2)` | No | Vigencia de la categoría. |
| `UltimoUsuario` | `varchar(20)` | Sí | Último usuario que modificó el maestro. |
| `UltimaFechaModif` | `datetime2(0)` | Sí | Última fecha de modificación. |

## TI_SubTipo

**Finalidad:** especializa un tipo de ticket dentro de una categoría concreta.

**Primary Key:** `(Tipo, SubTipo, Categoria)`.

**Foreign Keys:** `Tipo -> TI_Tipo`, `Categoria -> TI_Categoria`.

| Columna | Tipo | Null | Representa / finalidad |
|---|---|---:|---|
| `Tipo` | `char(3)` | No | Tipo principal al que pertenece el subtipo. |
| `SubTipo` | `char(3)` | No | Código del subtipo. Puede repetirse en otros tipos o categorías, por eso no es PK por sí solo. |
| `Categoria` | `varchar(20)` | No | Categoría dentro de la cual es válida la combinación. |
| `Descripcion` | `varchar(60)` | No | Nombre funcional del subtipo. |
| `Abreviatura` | `varchar(60)` | Sí | Texto abreviado para presentación. |
| `Estado` | `varchar(2)` | No | Vigencia del registro. |
| `UltimoUsuario` | `varchar(20)` | Sí | Último usuario que modificó el registro. |
| `UltimaFechaModif` | `datetime2(0)` | Sí | Última fecha de modificación. |

## TI_Estado

**Finalidad:** catálogo de estados por los que puede transitar un ticket.

**Primary Key:** `Estado`.

| Columna | Tipo | Null | Representa / finalidad |
|---|---|---:|---|
| `Estado` | `char(2)` | No | Código del estado del ticket. |
| `Descripcion` | `varchar(60)` | No | Nombre mostrado en UI y reportes. |
| `Orden` | `int` | No | Posición lógica usada para ordenar estados del flujo. Es numérico porque representa una secuencia, no una identidad de negocio. |
| `UltimoUsuario` | `varchar(20)` | Sí | Último usuario que modificó el catálogo. |
| `UltimaFechaModif` | `datetime2(0)` | Sí | Última fecha de modificación. |

## TI_ItemCategoria

**Finalidad:** define qué categorías pueden aplicarse a cada ítem y permite almacenar valores de configuración de prioridad, impacto y complejidad.

**Primary Key:** `(Item, Categoria)`.

**Foreign Keys:** `Item -> TI_Item`, `Categoria -> TI_Categoria`.

| Columna | Tipo | Null | Representa / finalidad |
|---|---|---:|---|
| `Item` | `varchar(20)` | No | Ítem funcional. |
| `Categoria` | `varchar(20)` | No | Categoría permitida para el ítem. |
| `Prioridad` | `numeric(18,2)` | Sí | Valor de configuración o referencia de prioridad para la combinación. El rango definitivo depende de la regla corporativa. |
| `Impacto` | `numeric(18,2)` | Sí | Valor de referencia de impacto. |
| `Complejidad` | `numeric(18,2)` | Sí | Valor de referencia de complejidad. |
| `Estado` | `varchar(2)` | No | Vigencia de la combinación. |
| `UltimoUsuario` | `varchar(20)` | Sí | Último usuario que modificó la configuración. |
| `UltimaFechaModif` | `datetime2(0)` | Sí | Última fecha de modificación. |

---

# 5. Incidencias

## TI_Incidencia

**Finalidad:** cabecera principal del ticket. Conserva la información vigente y los datos históricos relevantes del caso.

**Primary Key:** `IncidenciaNumero`.

**Relaciones clave:** usuario solicitante, área solicitante, área TI, técnicos, línea/ítem, tipo/subtipo/categoría, estado y área causante.

| Columna | Tipo | Null | Representa / finalidad |
|---|---|---:|---|
| `IncidenciaNumero` | `varchar(12)` | No | Número natural y visible del ticket. |
| `FechaRegistro` | `datetime2(0)` | No | Fecha y hora en que se registró el ticket. |
| `UsuarioSolicitante` | `varchar(20)` | No | Usuario que origina el ticket. |
| `AreaSolicitante` | `char(3)` | No | Área del usuario al momento del registro. Se conserva como fotografía histórica aunque el usuario cambie de área posteriormente. |
| `AreaTI` | `char(3)` | Sí | Área TI responsable de la atención cuando ya existe asignación. |
| `UsuarioTI` | `varchar(20)` | Sí | Técnico o analista responsable actual. |
| `UsuarioAsigno` | `varchar(20)` | Sí | Usuario que realizó la asignación al técnico. |
| `Linea` | `char(3)` | No | Línea funcional asociada al ticket. |
| `Item` | `varchar(20)` | Sí | Ítem concreto afectado. Puede ser `Null` mientras la clasificación aún no esté completa. |
| `Tipo` | `char(3)` | No | Tipo principal del ticket. |
| `SubTipo` | `char(3)` | Sí | Subtipo específico del ticket. |
| `Categoria` | `varchar(20)` | Sí | Categoría semántica del caso. Usa el mismo tipo que `TI_Categoria.Categoria` para garantizar integridad referencial. |
| `Estado` | `char(2)` | No | Estado actual del ticket. El histórico se conserva en `TI_IncidenciaEstado`. |
| `AreaCausante` | `char(3)` | Sí | Área identificada como causante cuando el análisis permite determinarla. |
| `Titulo` | `nvarchar(250)` | No | Resumen legible del problema o solicitud. |
| `Detalle` | `nvarchar(max)` | No | Descripción completa proporcionada por el usuario o consolidada durante el registro. |
| `MensajeError` | `nvarchar(1000)` | Sí | Mensaje de error concreto reportado por el sistema, cuando existe. Se separa del detalle para facilitar búsquedas y diagnóstico. |
| `FechaAsignacion` | `datetime2(0)` | Sí | Momento en que el ticket fue asignado para atención. |
| `FechaAtencion` | `datetime2(0)` | Sí | Momento en que comenzó la atención efectiva. |
| `FechaCierre` | `datetime2(0)` | Sí | Momento en que el ticket fue cerrado o resuelto. |
| `SlaObjetivoMinutos` | `int` | Sí | Objetivo de SLA expresado explícitamente en minutos. Es una cantidad, por eso utiliza entero. |
| `Prioridad` | `int` | Sí | Valor final de prioridad aplicado al ticket y conservado como fotografía histórica. El rango funcional aún debe quedar formalizado. |
| `Impacto` | `int` | Sí | Valor final de impacto aplicado al ticket. |
| `Complejidad` | `int` | Sí | Valor final de complejidad aplicado al ticket. |
| `CanalRegistro` | `varchar(20)` | No | Canal por el que se originó el caso, por ejemplo portal o chatbot. |
| `CausaRaiz` | `nvarchar(max)` | Sí | Causa confirmada después del diagnóstico. No debe completarse con una simple hipótesis. |
| `SolucionTecnica` | `nvarchar(max)` | Sí | Solución efectivamente aplicada por TI o por un mecanismo controlado. |
| `RespuestaUsuario` | `nvarchar(max)` | Sí | Confirmación o respuesta final del usuario respecto a la solución. |
| `TipoResolucion` | `varchar(20)` | Sí | Clasificación final de la forma en que se resolvió el caso. |
| `Calificacion` | `tinyint` | Sí | Calificación numérica entregada por el usuario. El rango definitivo debe validarse con la regla de negocio antes de agregar un Check. |
| `ComentarioCalificacion` | `nvarchar(500)` | Sí | Comentario asociado a la calificación. |
| `UltimoUsuario` | `varchar(20)` | No | Usuario responsable de la última modificación de la cabecera. |
| `UltimaFechaModif` | `datetime2(0)` | No | Fecha de la última modificación de la cabecera. |

## TI_IncidenciaAvance

**Finalidad:** registra avances técnicos realizados durante la atención.

**Primary Key:** `(IncidenciaNumero, Secuencia)`.

| Columna | Tipo | Null | Representa / finalidad |
|---|---|---:|---|
| `IncidenciaNumero` | `varchar(12)` | No | Ticket al que pertenece el avance. |
| `Secuencia` | `int` | No | Número correlativo del avance dentro del ticket. |
| `UsuarioTI` | `varchar(20)` | No | Técnico que registra el avance. |
| `FechaAvance` | `datetime2(0)` | No | Momento del avance. |
| `Detalle` | `nvarchar(max)` | No | Descripción técnica de lo realizado o encontrado. |
| `TiempoUtilizado` | `decimal(8,2)` | Sí | Tiempo invertido en la actividad. La unidad operativa debe mantenerse documentada por el proceso que lo utilice. |
| `PorcentajeAvance` | `decimal(5,2)` | Sí | Porcentaje acumulado de avance, restringido entre 0 y 100. |

## TI_IncidenciaEstado

**Finalidad:** conserva cada cambio de estado del ticket y permite reconstruir su flujo histórico.

**Primary Key:** `(IncidenciaNumero, Secuencia)`.

| Columna | Tipo | Null | Representa / finalidad |
|---|---|---:|---|
| `IncidenciaNumero` | `varchar(12)` | No | Ticket afectado. |
| `Secuencia` | `int` | No | Orden del cambio de estado dentro del ticket. |
| `Estado` | `char(2)` | No | Estado al que cambió el ticket. |
| `UsuarioCambio` | `varchar(20)` | Sí | Usuario que ejecutó el cambio. Puede ser `Null` cuando la operación provenga de un actor de sistema. |
| `FechaCambio` | `datetime2(0)` | No | Momento exacto del cambio. |
| `Observacion` | `nvarchar(1000)` | Sí | Contexto o motivo del cambio cuando sea necesario. |

## TI_IncidenciaMensaje

**Finalidad:** almacena la conversación asociada directamente al ticket.

**Primary Key:** `(IncidenciaNumero, Secuencia)`.

| Columna | Tipo | Null | Representa / finalidad |
|---|---|---:|---|
| `IncidenciaNumero` | `varchar(12)` | No | Ticket al que pertenece el mensaje. |
| `Secuencia` | `int` | No | Orden del mensaje dentro de la conversación. |
| `UsuarioAutor` | `varchar(20)` | Sí | Usuario humano autor del mensaje. Es `Null` cuando el mensaje es generado por IA o sistema. |
| `TipoAutor` | `char(1)` | No | Tipo de actor: `U` usuario, `T` técnico, `I` inteligencia artificial, `S` sistema. |
| `Contenido` | `nvarchar(max)` | No | Texto completo del mensaje. |
| `FechaMensaje` | `datetime2(0)` | No | Momento de emisión del mensaje. |
| `EsInterno` | `bit` | No | `1` cuando el mensaje es visible solo para personal interno; `0` cuando forma parte de la conversación visible al usuario. |

## TI_IncidenciaAdjunto

**Finalidad:** registra metadatos de archivos asociados al ticket o a un mensaje, sin almacenar el binario dentro de SQL Server.

**Primary Key:** `(IncidenciaNumero, Secuencia)`.

| Columna | Tipo | Null | Representa / finalidad |
|---|---|---:|---|
| `IncidenciaNumero` | `varchar(12)` | No | Ticket propietario del adjunto. |
| `Secuencia` | `int` | No | Secuencia del adjunto dentro del ticket. |
| `MensajeSecuencia` | `int` | Sí | Mensaje concreto al que pertenece el archivo. La FK compuesta impide apuntar a un mensaje de otro ticket. |
| `UsuarioRegistro` | `varchar(20)` | No | Usuario que cargó o registró el archivo. |
| `NombreOriginal` | `nvarchar(260)` | No | Nombre original recibido desde el usuario. |
| `NombreArchivo` | `nvarchar(260)` | No | Nombre controlado utilizado para guardar físicamente el archivo. |
| `RutaArchivo` | `nvarchar(1000)` | No | Ubicación lógica o física del archivo. |
| `TipoMime` | `varchar(100)` | No | Tipo MIME, por ejemplo `image/png` o `application/pdf`. |
| `TamanoBytes` | `bigint` | No | Tamaño del archivo en bytes; usa `bigint` porque representa una cantidad potencialmente superior al rango práctico de un entero pequeño. |
| `FechaRegistro` | `datetime2(0)` | No | Momento de carga del archivo. |

## TI_IncidenciaDocumento

**Finalidad:** relaciona una incidencia con uno o varios documentos empresariales del ERP.

**Primary Key:** `(IncidenciaNumero, Secuencia)`.

| Columna | Tipo | Null | Representa / finalidad |
|---|---|---:|---|
| `IncidenciaNumero` | `varchar(12)` | No | Ticket al que se vincula el documento. |
| `Secuencia` | `int` | No | Secuencia del documento dentro del ticket. |
| `CompaniaSocio` | `varchar(8)` | Sí | Compañía ERP del documento. No forma parte de la identidad del ticket. |
| `TipoDocumento` | `varchar(10)` | No | Tipo funcional del documento, por ejemplo OC, REQ o PE. |
| `NumeroDocumento` | `varchar(20)` | No | Número o código empresarial del documento. |
| `Descripcion` | `nvarchar(250)` | Sí | Contexto adicional sobre la relación con la incidencia. |

---

# 6. Inteligencia y conocimiento

## TI_BaseConocimiento

**Finalidad:** almacena conocimiento técnico validado y reutilizable para búsqueda, RAG y autoservicio.

**Primary Key:** `ConocimientoCodigo`.

| Columna | Tipo | Null | Representa / finalidad |
|---|---|---:|---|
| `ConocimientoCodigo` | `varchar(20)` | No | Código natural del artículo de conocimiento. |
| `Titulo` | `nvarchar(250)` | No | Nombre resumido del conocimiento. |
| `Problema` | `nvarchar(max)` | No | Descripción del problema que el artículo ayuda a resolver. |
| `Sintomas` | `nvarchar(max)` | No | Señales observables que permiten reconocer el caso. |
| `MensajeError` | `nvarchar(1000)` | Sí | Mensaje de error característico cuando existe. |
| `Causa` | `nvarchar(max)` | No | Causa validada que explica el problema. |
| `Solucion` | `nvarchar(max)` | No | Solución validada. |
| `Procedimiento` | `nvarchar(max)` | Sí | Pasos recomendados o procedimiento operativo. |
| `Linea` | `char(3)` | Sí | Línea funcional a la que aplica el conocimiento. |
| `Item` | `varchar(20)` | Sí | Ítem específico relacionado. |
| `Tipo` | `char(3)` | Sí | Tipo de ticket relacionado. |
| `SubTipo` | `char(3)` | Sí | Subtipo relacionado. |
| `Categoria` | `varchar(20)` | Sí | Categoría relacionada. |
| `IncidenciaOrigen` | `varchar(12)` | Sí | Incidencia de la cual surgió el conocimiento, cuando aplica. |
| `Estado` | `varchar(2)` | No | Vigencia/estado del artículo. |
| `UsuarioValida` | `varchar(20)` | Sí | Técnico que validó el contenido. |
| `FechaCreacion` | `datetime2(0)` | No | Fecha de creación del artículo. |
| `FechaValidacion` | `datetime2(0)` | Sí | Fecha de validación técnica. |
| `FechaRevision` | `datetime2(0)` | Sí | Última revisión periódica del contenido. |

## TI_IncidenciaDiagnostico

**Finalidad:** registra propuestas o diagnósticos realizados por IA o técnicos para una incidencia.

**Primary Key:** `(IncidenciaNumero, Secuencia)`.

| Columna | Tipo | Null | Representa / finalidad |
|---|---|---:|---|
| `IncidenciaNumero` | `varchar(12)` | No | Ticket diagnosticado. |
| `Secuencia` | `int` | No | Número del diagnóstico dentro de la incidencia. |
| `Origen` | `char(1)` | No | Origen del diagnóstico: `I` IA o `T` técnico. |
| `Diagnostico` | `nvarchar(max)` | No | Conclusión diagnóstica. |
| `CausaProbable` | `nvarchar(max)` | No | Causa que se considera probable en esa etapa. |
| `SolucionSugerida` | `nvarchar(max)` | No | Acción o solución propuesta, que no implica autorización automática para ejecutarla. |
| `Confianza` | `decimal(5,2)` | Sí | Nivel de confianza porcentual entre 0 y 100. No representa riesgo de ejecución. |
| `Estado` | `varchar(2)` | No | Estado operativo del diagnóstico. El dominio definitivo aún no está formalizado mediante Check y debe cerrarse antes de utilizarlo como regla de negocio. |
| `UsuarioValida` | `varchar(20)` | Sí | Técnico que revisó o validó el diagnóstico. |
| `FechaDiagnostico` | `datetime2(0)` | No | Momento en que se generó el diagnóstico. |

## TI_IncidenciaDiagnosticoEvidencia

**Finalidad:** registra la evidencia utilizada para explicar o sustentar un diagnóstico.

**Primary Key:** `(IncidenciaNumero, DiagnosticoSecuencia, Secuencia)`.

| Columna | Tipo | Null | Representa / finalidad |
|---|---|---:|---|
| `IncidenciaNumero` | `varchar(12)` | No | Incidencia propietaria del diagnóstico. |
| `DiagnosticoSecuencia` | `int` | No | Diagnóstico al que pertenece la evidencia. |
| `Secuencia` | `int` | No | Orden de la evidencia dentro del diagnóstico. |
| `TipoFuente` | `varchar(30)` | No | Tipo de fuente: conocimiento, mensaje, documento, datos, etc. |
| `Referencia` | `nvarchar(250)` | No | Identificador legible de la fuente consultada. |
| `Descripcion` | `nvarchar(1000)` | No | Explicación de por qué la evidencia es relevante. |
| `Similitud` | `decimal(5,2)` | Sí | Similitud porcentual cuando la fuente proviene de una búsqueda comparable; 0 a 100. |

---

# 7. Acciones y control

## TI_Accion

**Finalidad:** catálogo de acciones que el backend puede reconocer y controlar. No almacena SQL libre.

**Primary Key:** `AccionCodigo`.

| Columna | Tipo | Null | Representa / finalidad |
|---|---|---:|---|
| `AccionCodigo` | `varchar(50)` | No | Código estable de la acción autorizada. |
| `Nombre` | `nvarchar(100)` | No | Nombre funcional de la acción. |
| `Descripcion` | `nvarchar(500)` | No | Explicación de lo que realiza la acción. |
| `Tipo` | `char(1)` | No | `L` para lectura y `E` para ejecución/modificación controlada. |
| `NivelRiesgo` | `varchar(20)` | No | Nivel de riesgo operativo de la acción. Es independiente de la confianza del diagnóstico. |
| `RequiereAprobacion` | `bit` | No | Indica si la acción necesita aprobación humana previa. |
| `Estado` | `varchar(2)` | No | Vigencia del catálogo de acciones. |
| `UltimoUsuario` | `varchar(20)` | Sí | Último usuario que modificó la acción. |
| `UltimaFechaModif` | `datetime2(0)` | Sí | Última fecha de modificación. |

## TI_SolicitudAprobacion

**Finalidad:** representa una petición formal de autorización para ejecutar una acción vinculada con una incidencia.

**Primary Key:** `(IncidenciaNumero, Secuencia)`.

| Columna | Tipo | Null | Representa / finalidad |
|---|---|---:|---|
| `IncidenciaNumero` | `varchar(12)` | No | Incidencia que origina la solicitud. |
| `Secuencia` | `int` | No | Secuencia de la aprobación dentro del ticket. |
| `AccionCodigo` | `varchar(50)` | No | Acción que se solicita ejecutar. |
| `UsuarioSolicitante` | `varchar(20)` | No | Técnico o usuario interno que solicita la aprobación. |
| `UsuarioAprobador` | `varchar(20)` | Sí | Usuario que aprueba o rechaza. Es `Null` mientras esté pendiente. |
| `ParametrosJson` | `nvarchar(max)` | Sí | Parámetros estructurados necesarios para evaluar o ejecutar la acción. No debe contener secretos. |
| `Estado` | `char(1)` | No | `P` pendiente, `A` aprobada, `R` rechazada o `C` cancelada. |
| `Justificacion` | `nvarchar(1000)` | No | Motivo técnico por el que se solicita la acción. |
| `ComentarioRespuesta` | `nvarchar(1000)` | Sí | Comentario del aprobador. |
| `FechaSolicitud` | `datetime2(0)` | No | Momento de solicitud. |
| `FechaRespuesta` | `datetime2(0)` | Sí | Momento de aprobación, rechazo o cancelación. |

## TI_EjecucionAccion

**Finalidad:** registra cada ejecución real de una acción controlada y su resultado.

**Primary Key:** `(IncidenciaNumero, Secuencia)`.

**Unique:** `ClaveIdempotencia`.

| Columna | Tipo | Null | Representa / finalidad |
|---|---|---:|---|
| `IncidenciaNumero` | `varchar(12)` | No | Incidencia relacionada con la ejecución. |
| `Secuencia` | `int` | No | Secuencia de ejecución dentro del ticket. |
| `AccionCodigo` | `varchar(50)` | No | Acción ejecutada. |
| `SolicitudSecuencia` | `int` | Sí | Solicitud de aprobación que autorizó la ejecución. Puede ser `Null` para lecturas que no requieren aprobación. |
| `UsuarioEjecutor` | `varchar(20)` | Sí | Usuario asociado a la ejecución. Puede ser `Null` cuando la ejecución sea realizada por un proceso de sistema. |
| `ClaveIdempotencia` | `uniqueidentifier` | No | Clave única usada para impedir que un mismo intento se ejecute dos veces por reintentos o errores de red. |
| `ParametrosJson` | `nvarchar(max)` | Sí | Parámetros entregados a la acción. |
| `ResultadoJson` | `nvarchar(max)` | Sí | Resultado estructurado devuelto por la acción. |
| `Estado` | `varchar(2)` | No | Estado final o actual de la ejecución. Los datos de desarrollo utilizan `OK` y `ER`; el dominio definitivo debe formalizarse antes de producción. |
| `FilasAfectadas` | `int` | Sí | Cantidad de filas afectadas cuando la acción modifica datos. No representa éxito por sí sola. |
| `FechaInicio` | `datetime2(0)` | No | Inicio de la ejecución. |
| `FechaFin` | `datetime2(0)` | Sí | Fin de la ejecución. Puede quedar `Null` mientras está en curso. |
| `Error` | `nvarchar(2000)` | Sí | Mensaje técnico de error cuando la acción no finaliza correctamente. |

---

# 8. Auditoría

## TI_Auditoria

**Finalidad:** conserva trazabilidad transversal de eventos relevantes sin reemplazar los historiales funcionales específicos.

**Primary Key:** `AuditoriaNumero` (`Identity`).

| Columna | Tipo | Null | Representa / finalidad |
|---|---|---:|---|
| `AuditoriaNumero` | `bigint Identity(1,1)` | No | Secuencia técnica única del evento de auditoría. Se utiliza Identity porque no existe una clave natural útil para un evento transversal y el volumen puede crecer significativamente. |
| `IncidenciaNumero` | `varchar(12)` | Sí | Incidencia relacionada cuando el evento pertenece a un ticket. |
| `Usuario` | `varchar(20)` | Sí | Usuario humano relacionado con el evento. Puede ser `Null` para IA o procesos de sistema. |
| `TipoActor` | `char(1)` | No | Tipo de actor que origina el evento. Los datos actuales utilizan `U`, `T` e `I`; el sistema también puede registrar actores de sistema cuando corresponda. |
| `Entidad` | `varchar(100)` | No | Nombre de la entidad lógica afectada, por ejemplo `TI_Incidencia`. |
| `Registro` | `varchar(200)` | No | Identificador legible del registro afectado, incluyendo claves compuestas cuando corresponda. |
| `Evento` | `varchar(100)` | No | Acción auditada, por ejemplo creación, asignación, aprobación, ejecución o cierre. |
| `Resultado` | `varchar(20)` | No | Resultado funcional/técnico del evento. |
| `DetalleJson` | `nvarchar(max)` | Sí | Contexto estructurado adicional. Debe evitar datos sensibles o secretos. |
| `IdCorrelacion` | `uniqueidentifier` | No | Identificador que permite agrupar múltiples eventos pertenecientes al mismo flujo o solicitud. |
| `Fecha` | `datetime2(0)` | No | Momento exacto del evento. |

---

# 9. Campos y reglas pendientes de definición funcional

Los siguientes puntos se documentan explícitamente para evitar que una ausencia de definición sea interpretada como una regla aprobada:

1. `TI_Tipo.Item`: existe en el modelo actual, pero su regla funcional y su relación referencial todavía no están formalizadas. Actualmente los datos de desarrollo lo dejan en `Null`.
2. `TI_IncidenciaDiagnostico.Estado`: aún no posee un dominio cerrado mediante `Check`. Antes de utilizarlo para decisiones automáticas deben definirse sus códigos y transiciones.
3. `TI_EjecucionAccion.Estado`: los datos de desarrollo utilizan `OK` y `ER`; debe definirse el catálogo final antes del despliegue productivo.
4. `TI_Incidencia.Prioridad`, `Impacto` y `Complejidad`: actualmente representan valores enteros históricos del ticket; el rango y la forma exacta de cálculo deben validarse con el proceso real antes de agregar restricciones.
5. `TI_IncidenciaAvance.TiempoUtilizado`: el tipo permite decimales, pero la unidad operativa debe quedar confirmada antes de usarse para métricas oficiales.
6. `TI_Usuario.Clave`: el esquema actual contempla autenticación local. Si se adopta SSO/Active Directory, debe revisarse la necesidad de este campo. Mientras exista, solo debe almacenar hash seguro.

Documentar estos puntos como pendientes evita inventar reglas y permite cerrar cada decisión con evidencia empresarial antes de convertirla en una restricción física.
