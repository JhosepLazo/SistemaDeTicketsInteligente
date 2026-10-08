# Runbook del Agente de Ingeniería

> **Versión:** 1.0 · **Fecha:** 08/10/2026 · **Para:** el equipo de TI que opera el portal (operadores TEC/SUP y administradores ADM).
>
> Qué hacer con el agente en la operación diaria y ante un problema: cómo apagarlo, cómo cambiar su modo, cómo liberar una acción autónoma,
> cómo revisar lo que hizo, cómo cerrar una ejecución interrumpida y cómo revertir un cambio. El diseño está en
> `01_DOCUMENTACION_TECNICA.md` (secciones 8.4, 13 y 25) y las reglas funcionales en `02_DOCUMENTACION_FUNCIONAL.md` (secciones 10 y 14).

---

## 1. Lo esencial

| Pregunta | Respuesta corta |
|---|---|
| ¿Quién controla al agente? | Un ADM, en **Maestros TI → Agente y autonomía**. Todo TI puede consultar. |
| ¿Cuánto tarda en aplicarse un cambio? | Menos de 30 segundos (la API guarda los parámetros en caché 30 s). |
| ¿Cómo lo detengo ya? | Interruptor en **Apagado** (sección 2). |
| ¿Puede el agente cambiar datos por su cuenta? | Solo en modo **Autónomo** y solo con acciones que un ADM liberó para solicitudes (`SOL`), reversibles y de bajo riesgo. Al instalar, ninguna acción está liberada. |
| ¿Dónde veo lo que hizo? | La consola del Asistente TI (investigación `AGT-nnnnnn`), el detalle del ticket y **Reportes → Agente**. |
| ¿Queda registro? | Sí: cada evento, llamada al modelo, rechazo, evaluación de política, aprobación, ejecución y cambio de configuración queda en la base con su correlación. |

**Estado inicial después de instalar:** interruptor **Asistido**, techo de riesgo **MUY_BAJO**, confianza mínima para proponer **60 %**, aprobaciones sin vencimiento, autocierre desactivado y todas las políticas en **Con aprobación de TI**.

## 2. Apagar el agente

**Desde el portal (recomendado):** con un usuario ADM, Maestros TI → Agente y autonomía → *Interruptor del agente* → **Apagado** → Guardar.

| Modo | Investiga | Propone | Ejecuta |
|---|:---:|:---:|:---:|
| Apagado | No | No | No |
| Sombra | Sí | Sí | No |
| Asistido | Sí | Sí | Solo con decisión de TI (y aprobación de otro operador si la acción lo exige) |
| Autónomo | Sí | Sí | Además, sin humano, lo que la política libera |

Con **Apagado**, el botón Investigar responde "TI apagó el Agente de Ingeniería…", las investigaciones automáticas no se inician y Realizar cambio se rechaza. Las investigaciones ya guardadas se conservan.

**Si el portal no está disponible:** un DBA ejecuta en `GestionSistemas`, a nombre de un ADM activo (el procedimiento rechaza otros perfiles y audita el cambio con el valor anterior):

```sql
Declare @cCorrelacion uniqueidentifier = NewId()
Exec dbo.Usp_TI_Guardar_ParametroTI @cUsuario = 'ADM001', @cArea = 'TIC', @cParametro = 'AGENTE_MODO', @cValor = N'APAGADO', @cIdCorrelacion = @cCorrelacion
```

**Si además hay que detener los procesos en segundo plano:** definir en el servidor `AgenteTI__InvestigacionAutomatica=false` y `AgenteTI__Mantenimiento=false` y reiniciar la API. `AgenteTI__SoloDiagnostico=true` deshabilita toda ejecución de cambios aunque el interruptor diga otra cosa.

**Cortar la escritura a nivel de base (último recurso):** deshabilitar el login de la cadena `CnnAgenteEscritura` (`Alter Login <login> Disable`). Las investigaciones siguen (usan el login de lectura) y cualquier ejecución falla y queda registrada como error.

## 3. Cambiar de modo con criterio

1. **Sombra** mientras no haya evidencia de acierto: el agente investiga y propone, TI resuelve como siempre.
2. Revisar **Reportes → Agente** del período: acierto de la clasificación, comparativo *agente frente a TI* (causa del agente junto a la causa raíz de TI), aprobaciones y motivos de rechazo.
3. **Asistido** cuando TI confíe en las propuestas: TI decide cada ejecución.
4. **Autónomo** solo cuando exista al menos una acción liberada (sección 4) y la evidencia lo justifique. La meta del plan de mejoras para subir a Autónomo es **cero** solicitudes tratadas como autónomas por error en el período evaluado.

Cada cambio queda auditado (`TI_Auditoria`, entidad `TI_Parametro`, con el valor anterior y el nuevo).

## 4. Liberar una acción para ejecución autónoma

El procedimiento rechaza cualquier paso fuera de este orden, con un mensaje que indica qué falta.

1. **Ejecutor con esquema.** La acción debe tener un procedimiento `dbo.Usp_TI_AgenteAccion_*` activo en `TI_AgenteAccionEjecutor` con `ParametrosEsquemaJson`. Hoy solo `ACC-004` (liberar un ticket bloqueado sin aprobación pendiente) y `ACC-007` (reactivar la cuenta de colaborador del solicitante) tienen ejecutor y esquema.
2. **Catálogo** (Maestros TI → Agente y autonomía → *Catálogo de acciones*): marcar **Reversible**, quitar **Requiere aprobación** y fijar un **riesgo** que no supere el techo. Hoy todas las acciones de ejecución están en riesgo MEDIO, con aprobación y no reversibles.
3. **Techo de riesgo** (*Interruptor y límites*): igual o mayor que el riesgo de la acción. Al bajarlo después, las políticas que lo superen vuelven solas a "Con aprobación".
4. **Política** (*Política de autonomía*): fila `SOL` × acción → **Autónoma**, con confianza mínima de 60 a 100. Para `INC` y `REQ` la opción no existe: siempre pasan por TI.
5. **Interruptor** en **Autónomo**.

Además, en cada caso el sistema exige: ticket `SOL` clasificado por TI, solicitante colaborador (`USR`), diagnóstico con la confianza mínima y nivel de evidencia no bajo, parámetros válidos contra el esquema, simulación previa exitosa e investigación en espera de TI sin otra decisión en curso. `Usp_TI_Agente_PrepararCambio` lo vuelve a comprobar en la base (errores 50601 a 50611).

> **Decisión pendiente (D2 y D3 en `06_RegistroDecisiones.md`):** qué acciones de escritura se liberan y si `ACC-007` puede ejecutarse sin TI. Hasta que TI decida, no liberar ninguna.

**Para retirar la autonomía de una acción:** cambiar su política a "Con aprobación" o "Prohibida", o inactivar la acción en el catálogo.

## 5. Revisar lo que hizo el agente

**En el portal:**

- **Consola del Asistente TI:** la investigación `AGT-nnnnnn` muestra evidencia, herramientas, diagnóstico, nivel de evidencia, alternativas descartadas, acción propuesta y decisión; el expediente Markdown se descarga.
- **Gestión de Tickets → detalle:** investigaciones del ticket, nota interna de cada decisión, aprobaciones y la clasificación propuesta por la IA (historial en el modal Clasificar).
- **Reportes → Agente:** totales del período, ejecuciones aprobadas por TI frente a autónomas, modelos usados y comparativo con TI.

**Con un login de consulta sobre `GestionSistemas`:**

```sql
-- Últimas ejecuciones (TipoEjecutor: T = decidida o aprobada por TI, I = autónoma)
Select Top (50) IncidenciaNumero, Secuencia, AccionCodigo, TipoEjecutor, Estado, FilasAfectadas, FechaInicio, FechaFin, ResultadoJson, Error
From dbo.TI_EjecucionAccion
Order By FechaInicio Desc

-- Eventos de una investigación: LLAMADA_MODELO, ACCION_RECHAZADA, EVALUACION_AUTONOMIA, EJECUCION_AUTONOMA,
-- INVESTIGACION_AUTOMATICA y su _FIN, herramientas, telemetría, etc.
Select Secuencia, Tipo, Fuente, Fecha, Left(Contenido, 300) as Contenido, DatosJson
From dbo.TI_AgenteEvento
Where SesionNumero = 50022
Order By Secuencia

-- Todo lo ocurrido en una petición o investigación (X-Correlation-ID de la respuesta, idSeguimiento de un error o IdCorrelacion de la investigación)
Select Fecha, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, DetalleJson
From dbo.TI_Auditoria
Where IdCorrelacion = '00000000-0000-0000-0000-000000000000'
Order By Fecha
```

Los registros de la API (consola del servicio) escriben cada petición como `API <método> <ruta> <resultado> <duración> ms Correlacion <id>`; ese mismo id está en la auditoría.

## 6. Una ejecución quedó "en proceso"

Una ejecución nace en estado `PR` antes de actuar (clave de idempotencia) y pasa a `OK` o `ER`. Si la API se cae en medio, queda `PR`.

- **Automático:** `MantenimientoAgenteTI` la cierra como `ER` ("interrumpida") 15 minutos después, en su ciclo de cada 10 minutos.
- **Manual:** `Exec dbo.Usp_TI_Agente_ReconciliarEjecuciones @nMinutos = 15`.

Después, **verificar el efecto real**: el ejecutor trabaja en una sola transacción, así que el cambio se aplicó completo o no se aplicó. Revisar el objeto afectado (por ejemplo, el estado de la cuenta o del ticket). La misma investigación no puede repetir la acción (idempotencia): si hace falta, se inicia una investigación nueva.

## 7. Una investigación automática no terminó

La investigación y la evidencia se guardan antes de encolarla, con la marca `INVESTIGACION_AUTOMATICA`. Si la API se reinicia o el trabajo supera 8 minutos:

- El mantenimiento la vuelve a encolar si lleva más de 10 minutos sin marca de fin.
- Para ver las pendientes: `Exec dbo.Usp_TI_Agente_ListarInvestigacionesPendientes @nMinutos = 10`.
- El responsable puede pulsar **Investigar** en la consola en cualquier momento.

Con el interruptor en Apagado, las pendientes no se procesan hasta volver a encenderlo.

## 8. Revertir un cambio del agente

No existe todavía un ejecutor de reversión catalogado (decisión D18). Cada ejecutor guarda el estado anterior en `TI_EjecucionAccion.ResultadoJson` (`estadoAnterior`) y en `TI_Auditoria`. La reversión se hace por el flujo normal del portal, que deja su propia auditoría; **nunca con un `Update` directo** en la base (regla del proyecto: un cambio productivo exige un ejecutor catalogado y la decisión de TI).

| Acción | Qué cambió | Cómo revertir |
|---|---|---|
| `ACC-007` Habilitar acceso | La cuenta `USR` del solicitante pasó de inactiva a activa (`TI_Usuario.Estado`) | Maestros TI → Usuarios: volver a sincronizar el usuario con estado **Inactivo** (requiere identidad corporativa habilitada, como en Empresa). |
| `ACC-004` Liberar registro | El ticket pasó de `PA` a `DG` (no tenía aprobación pendiente real) | Si el bloqueo era necesario, **Solicitar aprobación** desde Gestión de Tickets: el ticket vuelve a `PA` con una solicitud real. |

Luego, si la acción no debía ejecutarse: retirar su autonomía (sección 4) o inactivarla en el catálogo, y registrar lo ocurrido como avance interno del ticket.

## 9. Respuesta a un incidente con el agente

1. **Apagar** (sección 2). Si se sospecha de la identidad de escritura, deshabilitar su login.
2. **Delimitar:** ejecuciones de las últimas horas (sección 5), sobre todo las `TipoEjecutor = 'I'`.
3. **Revertir** cada cambio indebido (sección 8).
4. **Investigar la causa** con los eventos de la investigación: `ACCION_RECHAZADA` (el modelo pidió algo no permitido: posible instrucción incrustada en los datos), `EVALUACION_AUTONOMIA` (por qué se permitió) y `LLAMADA_MODELO` (modelo y versión de las instrucciones).
5. **Corregir la configuración:** política, catálogo, techo o umbral.
6. **Volver a Sombra** y revisar Reportes → Agente antes de subir de nuevo.

## 10. El agente responde mal o no responde

| Síntoma | Causa probable | Qué hacer |
|---|---|---|
| Diagnósticos "sin modelo" con confianza 20 % | No hay clave de IA o el proveedor no responde | Revisar `GEMINI_API_KEY`/`OPENAI_API_KEY`/`GROQ_API_KEY` en el servidor y los avisos de la API. El sistema sigue funcionando sin IA. |
| Respuestas lentas o "modelo saturado" (503/429) | Límites del nivel gratuito | El agente recorre la cadena de respaldo; ver `AsistenteIA:ModelosGeminiRespaldo` y Reportes → Agente (modelos con fallas). |
| "Proponer con IA" responde que la IA no está configurada | Sin proveedor de IA | Clasificar con la matriz. |
| La propuesta no trae acción | Evidencia baja, confianza bajo el mínimo o parámetros que no cumplen el esquema | Es el comportamiento esperado; el informe indica el motivo. |
| Una herramienta se desactiva en la investigación | Falló 4 veces seguidas o agotó su tiempo 2 veces | Revisar la base o el sistema investigable; en equipos con poca memoria, ver la nota de SQL Server en el `README`. |
| 400 "La protección de la sesión venció" | Token CSRF de otra sesión (por ejemplo, se inició sesión en otra pestaña) | El portal lo renueva solo y reintenta; si persiste, volver a iniciar sesión. |

## 11. Parámetros de operación

| Parámetro | Para qué | Recomendación inicial |
|---|---|---|
| `AGENTE_MODO` | Interruptor | Sombra o Asistido hasta tener evidencia |
| `AGENTE_RIESGO_MAXIMO_AUTONOMO` | Techo para ejecutar sin humano | `MUY_BAJO` |
| `AGENTE_CONFIANZA_MINIMA_PROPUESTA` | Debajo de este valor no se propone acción | 60, y subirlo si TI rechaza muchas propuestas |
| `APROBACION_VIGENCIA_HORAS` | Una aprobación vencida se pide de nuevo | Definir con TI (por ejemplo, 24) |
| `TICKET_AUTOCIERRE_PV_DIAS` | Cierra tickets en PV sin respuesta | Definir con TI; vacío = desactivado |

Los valores definitivos son la decisión D11 del registro de decisiones.
