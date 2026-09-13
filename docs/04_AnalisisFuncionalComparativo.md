# Analisis funcional comparativo: sistema legado vs proyecto actual

Fecha: 2026-09-13

## Alcance

Este analisis compara, solo a nivel funcional, dos fuentes:

- Sistema legado de la empresa: GestionSistemas.zip, descrito en el primer texto pegado.
- Proyecto actual: Sistema de Tickets Inteligente en desarrollo, descrito en el segundo texto pegado.

No se evalua diseno visual ni se proponen cambios de codigo. La IA, RAG y diagnostico inteligente se consideran pendientes y no se toman como brecha negativa en esta etapa, porque el usuario indico que se implementaran al final.

## Conclusiones principales

El proyecto actual ya cubre la columna vertebral funcional del sistema legado: autenticacion, perfiles, registro de tickets, seguimiento, interaccion usuario-TI, gestion operativa, resolucion, validacion, reapertura, calificacion, base de conocimiento y reportes.

No conviene copiar el sistema legado pantalla por pantalla. El legado tiene muchas pantallas porque su arquitectura y su epoca lo llevaron a separar funciones que hoy pueden estar mejor integradas en menos modulos.

Las brechas indispensables no son muchas, pero si son importantes: sincronizacion corporativa de usuarios, gestion completa de catalogos/reglas de clasificacion, autorizaciones con bloqueo real del flujo, registro estructurado de esfuerzo/area causante, correos/notificaciones operativas y soporte para crear tickets a nombre de otro usuario desde TI.

La recomendacion es no aumentar muchos modulos. Para Usuario no hace falta agregar modulos nuevos antes de la IA. Para TI si conviene agregar un modulo pequeno de Administracion/Configuracion TI, o integrar esa administracion dentro de una seccion restringida, porque el sistema necesita mantener catalogos, matriz de prioridad y usuarios. Sin eso, la operacion dependeria demasiado de datos cargados manualmente por base de datos.

## Funciones para Usuarios

### Lo que tiene el sistema legado

El usuario puede iniciar sesion, recuperar contrasena, ver menu segun perfil, registrar ticket propio, adjuntar evidencia, editar el ticket mientras no este en proceso, consultar sus tickets, ver historial de avances, descargar documentos, descargar formatos, ver tutoriales, confirmar atencion y calificar el servicio.

Tambien existe una cola inicial que obliga a calificar atenciones pendientes antes de continuar. El sistema permite confirmar el servicio mediante un avance final trazable y luego responder una encuesta de satisfaccion con estrellas, comentario y confirmacion de si el problema fue resuelto.

El registro del ticket exige tipo, linea, item, titulo y detalle. Si es requerimiento, exige archivo. La clasificacion inicial se basa en catalogos, pero el area TI se determina desde la linea seleccionada.

### Lo que tiene el proyecto actual

El usuario ya cuenta con Inicio, Nuevo Ticket y Mis Tickets.

Inicio resume tickets activos, casos en atencion, casos que requieren accion, resueltos recientes y pendientes de calificacion.

Nuevo Ticket permite registrar solicitudes con linea, tipo, titulo, detalle, mensaje de error y adjuntos. El usuario autenticado y su area salen de la sesion, no del formulario. El sistema exige adjunto para requerimientos y valida archivos en frontend y backend.

Mis Tickets permite consultar bandeja personal, filtros, detalle completo, historial, avances visibles, mensajes, adjuntos, responder observaciones, validar solucion, reabrir si no fue solucionado y calificar la atencion.

### Funciones indispensables que faltan o deben asegurarse

1. Sincronizacion corporativa de identidad.
   El legado sincroniza usuarios/cargos desde Spring. El proyecto actual autentica correctamente con su propia tabla/procedimiento, pero todavia no replica la integracion corporativa completa. Si la empresa exige usar las mismas credenciales y datos de Spring, esto es indispensable.

2. Descarga/repositorio de formatos frecuentes.
   El legado tiene formatos descargables para solicitudes comunes. En el proyecto actual no aparece como modulo claro para Usuario. No necesita ser un modulo grande; puede integrarse en Nuevo Ticket o Base de Conocimiento como documentos de soporte.

3. Autoservicio no inteligente mientras llega la IA.
   El legado tenia tutoriales estaticos. El proyecto actual tendra Asistente TI al final, pero antes de eso conviene que la Base de Conocimiento publicada pueda alimentar ayudas o articulos visibles para usuarios. No hace falta crear un modulo pesado de tutoriales.

4. Edicion del ticket antes de procesamiento.
   El legado permite editar PE/AS/OB y bloquea cuando ya esta en proceso. El proyecto actual permite responder observaciones y manejar el ciclo, pero debe quedar confirmado si el usuario puede corregir datos basicos del ticket cuando aun no fue tomado por TI. Si no existe, es una brecha funcional util.

5. Notificacion clara de acciones pendientes.
   El proyecto actual ya tiene indicadores y campana. Debe mantenerse como parte esencial, porque reemplaza de forma moderna la obligacion del legado de atender confirmaciones/calificaciones pendientes.

## Funciones para Operador TI

### Lo que tiene el sistema legado

TI puede ver bandejas por estado, detectar tickets no leidos, revisar detalle tecnico, reclasificar area TI-linea-item-categoria-tipo, aplicar matriz item/categoria para prioridad-impacto-complejidad, asignar analista, reasignar, observar, marcar No Procede, solicitar autorizacion, registrar avances, registrar tiempo usado, indicar area causante, adjuntar documentos durante la atencion, enviar correos automaticos/manuales, consultar historico transversal, administrar catalogos, sincronizar usuarios/cargos y generar reportes.

El legado tiene dos funciones especialmente importantes:

- La autorizacion no es decorativa: si el ticket requiere autorizacion, bloquea el avance hasta que se autorice.
- El avance no solo documenta texto; tambien registra tiempo efectivo, area causante, clasificacion vigente y respuesta.

### Lo que tiene el proyecto actual

TI ya cuenta con Inicio TI, Gestion de Tickets, Base de Conocimiento y Reportes.

Inicio TI muestra carga operativa, pendientes, tickets sin asignar, prioridad alta, tickets por vencer, reaperturas, aprobaciones pendientes y accesos de accion.

Gestion de Tickets concentra bandeja, filtros, detalle tecnico, clasificacion, asignacion, avances, solicitud de informacion, resolucion, No Procede, reapertura, escalamiento y aprobaciones.

Base de Conocimiento permite crear, editar, validar, revalidar, inactivar y generar articulos desde tickets resueltos. Esto supera al legado, porque convierte soluciones en conocimiento reutilizable.

Reportes incluye filtros, KPIs, evolucion, distribuciones, tiempos, tickets prioritarios y exportacion CSV.

### Funciones indispensables que faltan o deben asegurarse

1. Administracion funcional de catalogos.
   El legado depende de areas, lineas, items, tipos, categorias/subtipos y matriz item-categoria-prioridad-impacto-complejidad. El proyecto actual usa clasificacion, pero debe existir una forma mantenible de administrar esos datos. Esta es la brecha mas importante para TI.

2. Matriz de reglas de prioridad, impacto y complejidad.
   No basta con que TI seleccione prioridad manualmente. El legado tiene una regla configurable por item + categoria. El proyecto actual debe conservar este principio para evitar arbitrariedad y mantener criterios consistentes.

3. Registro de tiempo efectivo por avance.
   El proyecto actual registra avances, pero el texto no confirma que cada avance guarde minutos trabajados. Esto es indispensable si se quieren reportes reales de esfuerzo, carga, productividad y costo operativo.

4. Area causante.
   El legado obliga a registrar area causante en los avances. Es valioso porque separa solicitante, area TI y origen real del problema. Debe conservarse para analisis de reincidencia.

5. Autorizaciones con bloqueo de flujo.
   El proyecto actual tiene aprobaciones, pero debe quedar garantizado que una aprobacion pendiente pueda bloquear la ejecucion cuando corresponda. Si solo se muestra como informacion, no iguala la funcion esencial del legado.

6. Gestion de Calidad.
   El legado permite vincular un usuario de Gestion de Calidad. Si la empresa sigue usando esa responsabilidad, debe existir en el proyecto actual. Si ya no existe como proceso real, puede omitirse.

7. Correos o notificaciones operativas.
   El legado envia o permite enviar mensajes ante asignacion, avances y atencion terminada. El proyecto actual tiene mensajes internos, pero debe definir si tambien notificara por correo/campana. Es indispensable al menos para eventos que requieren accion del usuario: observacion, validacion, reapertura, aprobacion o cierre.

8. Crear ticket a nombre de otro usuario desde TI.
   En el legado, TI puede registrar tickets para otros usuarios. Es util para mesa de ayuda cuando un usuario llama o no puede acceder. Conviene implementarlo como funcion restringida de TI, no como capacidad general del usuario.

## Brechas indispensables resumidas

| Prioridad | Brecha | Actor | Motivo |
|---|---|---|---|
| Alta | Sincronizacion corporativa de usuarios/cargos | Sistema/TI | Evita doble administracion de identidad |
| Alta | Administracion de catalogos y matriz de reglas | TI | Sin esto la clasificacion no sera sostenible |
| Alta | Aprobaciones que bloqueen ejecucion cuando aplique | TI | Conserva control empresarial real |
| Alta | Registro de tiempo efectivo por avance | TI | Necesario para productividad y reportes |
| Alta | Area causante | TI | Necesario para causa/reincidencia |
| Media | Crear ticket por otro usuario desde TI | TI | Necesario para mesa de ayuda |
| Media | Formatos/documentos frecuentes | Usuario/TI | Reduce errores en requerimientos |
| Media | Notificaciones accionables | Usuario/TI | Evita que validaciones/observaciones queden olvidadas |
| Baja | Tutoriales estaticos | Usuario | Puede reemplazarse por articulos o Asistente |

## Modulos necesarios

### Usuario

No es necesario aumentar mas modulos para Usuario antes de la IA.

Los modulos actuales son suficientes:

- Inicio.
- Nuevo Ticket.
- Mis Tickets.
- Asistente TI pendiente.

Las funciones faltantes de Usuario no requieren modulos nuevos. Pueden resolverse asi:

- Formatos frecuentes dentro de Nuevo Ticket o articulos visibles de Base de Conocimiento.
- Ayuda/autoservicio desde Base de Conocimiento publicada o luego desde Asistente.
- Edicion previa al procesamiento dentro de Mis Tickets.

Agregar un modulo separado de "Tutoriales" o "Formatos" solo replicaria la estructura antigua y puede fragmentar la experiencia.

### Operador TI

Si es necesario agregar o consolidar una capacidad administrativa para TI.

Puede ser un modulo llamado Administracion TI o Configuracion TI, restringido a SUP/ADM. No debe ser grande ni decorativo; debe cubrir solo lo indispensable:

- Areas.
- Lineas.
- Items.
- Tipos.
- Categorias/Subtipos, normalizando el nombre.
- Matriz item/categoria con prioridad, impacto y complejidad.
- Usuarios/operadores TI o sincronizacion con origen corporativo.
- Parametros de SLA si el sistema los maneja desde catalogos.

No recomiendo crear muchos modulos separados como en el legado. Mejor un solo modulo de configuracion con secciones internas.

## Elementos del legado que no conviene copiar

No se debe replicar el envio de contrasena existente por correo. Debe usarse recuperacion segura o identidad corporativa.

No se debe copiar el porcentaje de avance como mecanismo central de trabajo. El proyecto actual hace bien en reemplazarlo por estados, validacion del usuario, avances y resolucion estructurada. El 97% + 3% es trazable, pero operativo y conceptualmente rigido.

No se deben copiar pantallas vacias, excluidas o de prueba: Encuesta.aspx vacia, fLinea.aspx, fItem.aspx, fViewCrystal.aspx, fViewReporting.aspx, Pruebas.aspx, Default.htm historico, archivos .exclude.

No se deben copiar notificaciones moviles comentadas como si fueran funcionalidad productiva.

No se debe copiar la descarga por ruta fisica enviada desde el cliente. El proyecto actual debe mantener descargas por ID autorizado.

No se debe mantener la confusion entre Subtipo y Categoria. El proyecto actual debe escoger un nombre funcional y usarlo de forma consistente.

No se debe crear un modulo de reportes demasiado amplio. El enfoque actual de KPIs utiles, filtros y exportacion es suficiente para productividad.

No se debe exponer al usuario final prioridad, impacto, complejidad o responsable TI como campos editables. Esa decision corresponde a TI o a la IA supervisada en el futuro.

## Mejoras recomendadas sin aumentar complejidad

Mantener la division actual: Usuario describe, TI clasifica, IA luego ayuda.

Convertir formatos y tutoriales en conocimiento reutilizable, no en modulos aislados.

Hacer que cada accion importante deje trazabilidad: quien, cuando, que cambio, comentario y adjuntos.

Separar avances internos de avances visibles para usuario, como ya contempla el proyecto actual.

Usar las calificaciones y reaperturas como indicadores de calidad, no solo como campos historicos.

Preparar la Base de Conocimiento para RAG, pero sin publicar automaticamente cualquier solucion tecnica sin validacion.

Conservar "No Procede" como salida distinta de "Resuelto".

Conservar reapertura cuando el usuario indica que el problema continua.

## Decision funcional recomendada

El proyecto actual no necesita mas cantidad de modulos para verse completo. Necesita asegurar pocas funciones profundas que sostienen la operacion real de la empresa.

Para Usuario, la estructura actual es adecuada y no requiere nuevos modulos antes del Asistente TI.

Para TI, si falta una capacidad indispensable: Administracion/Configuracion TI para catalogos, reglas y usuarios/sincronizacion. Sin ella, Gestion de Tickets dependera de configuraciones no mantenibles desde la aplicacion.

El siguiente orden funcional recomendado es:

1. Consolidar ramas actuales en una version unica del producto.
2. Confirmar o implementar sincronizacion corporativa de usuarios.
3. Implementar Administracion/Configuracion TI minima.
4. Asegurar matriz de clasificacion/prioridad, tiempo por avance, area causante y bloqueo por aprobacion.
5. Recien despues avanzar con Asistente TI, RAG y diagnostico inteligente.

Con esa ruta, el sistema no crece por cantidad de pantallas, sino por calidad operacional.

## Estado de implementacion

Actualizado el 13 de septiembre de 2026 despues de contrastar este analisis con la rama funcional vigente.

| Recomendacion | Estado | Implementacion funcional |
|---|---|---|
| Identidad corporativa | Implementada | Autenticacion contra Spring configurable, sincronizacion segura de metadata, cargos y asignacion local de area/perfil. |
| Configuracion TI | Implementada | Un unico modulo restringido a SUP/ADM administra catalogos, usuarios, formatos, visibilidad de conocimiento, matriz y SLA. |
| Matriz de clasificacion | Implementada | Prioridad, impacto y complejidad se obtienen de Item/Categoria; el operador no los decide manualmente. |
| Aprobaciones con bloqueo | Implementada | El estado PA bloquea la operacion hasta aprobar o rechazar. Se impide que el solicitante responda su propia aprobacion. |
| Tiempo efectivo y area causante | Implementada | Cada avance tecnico exige minutos reales y area causante; Reportes TI resume el esfuerzo con sus filtros operativos. |
| Ticket a nombre de otro usuario | Implementada | TEC/SUP/ADM puede registrar por mesa de ayuda, conservando solicitante y operador registrador por separado. |
| Formatos y autoservicio | Implementada | Nuevo Ticket consume formatos descargables y articulos de conocimiento publicados, sin crear modulos separados. |
| Edicion temprana del ticket | Implementada | El usuario puede corregir datos basicos propios antes del procesamiento, sin modificar clasificacion tecnica. |
| Notificaciones accionables | Implementada | Asignacion, informacion requerida, respuesta del usuario, avance visible, validacion, reapertura, aprobacion y No Procede generan avisos persistidos que abren el ticket correspondiente. |
| Conocimiento reutilizable | Implementada | Los articulos requieren gestion y validacion antes de hacerse visibles al usuario; quedan preparados para el RAG futuro. |

No se agrego un modulo de Gestion de Calidad porque el analisis lo condiciona a la existencia actual de ese proceso empresarial y no hay evidencia suficiente para crear un rol nuevo. Tampoco se agrego correo saliente: la campana persistida cumple la necesidad operativa inmediata sin inventar servidores, credenciales o reglas de entrega corporativas. La integracion de IA, RAG y diagnostico permanece fuera de esta etapa por decision del proyecto.
