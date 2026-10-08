# Reglas del proyecto — Sistema de Tickets Inteligente

Portal de incidencias de TI de Calimod: backend ASP.NET Core (.NET 10), frontend React + TypeScript (Vite) y SQL Server con
Stored Procedures. La estructura y el flujo están en [docs/03_EstructuraSolucion.md](docs/03_EstructuraSolucion.md).

## Principios de trabajo

- Primero legible; después corto. La menor complejidad que resuelva el problema sin sacrificar integridad.
- No crear capas, carpetas, tablas, dependencias o validaciones sin una necesidad concreta.
- Conservar las convenciones existentes y usar nombres nuevos en español (sin eñes ni tildes en identificadores).
- No renombrar objetos por preferencia personal.
- Explicar antes cualquier cambio de tablas, columnas, claves, tipos, nulabilidad o reglas de negocio.
- No hacer commits ni ejecutar scripts automáticamente sin autorización.
- Mantener separadas la configuración y las credenciales de desarrollo y producción.

## Backend

- Flujo: `Controller` (hereda `ControladorBase`) -> `BLL` -> `BaseDatos` -> Stored Procedure. No hay capa DAO.
- El controlador solo atiende HTTP: toma usuario y área de la cookie y usa `Responder`/`Ejecutar` para traducir errores
  (400 validación, 404 no encontrado, 409 regla de negocio). Nunca acepta identidad, perfil ni aprobaciones del navegador.
- La BLL valida primero (`Validacion`), declara cada parámetro con su tipo y largo exactos y lee cada columna por su nombre
  (`LecturaSql`). Los errores de negocio de un procedimiento se lanzan con `Throw 50000-50999`.
- Cada archivo empieza con su encabezado: Archivo, Objetivo, Responsabilidad, Dependencias, Flujo y Consideraciones.
- Una clase pública por archivo; si crece demasiado, se divide por tema con archivos parciales.
- Constructores primarios y `using` globales (definidos en el `.csproj`).
- `dotnet build SistemaTicketsInteligente.slnx` debe terminar con 0 errores y 0 advertencias, y `dotnet test SistemaTicketsInteligente.slnx`
  sin fallas. Un endpoint nuevo necesita su fila en la matriz de `MatrizAutorizacionPruebas` (la prueba falla si falta).
- Toda operación que modifica datos exige el token CSRF; el agente lee con `CrearConexionAgenteLectura` y ejecuta con
  `CrearConexionAgenteEscritura`, nunca con la conexión de la API.

## Frontend

- `features/<módulo>/` para pantallas y componentes de un módulo; `components/` solo para lo que usan varias pantallas
  (`MarcoPortal`, `Icono`, `NotificacionesCampana`, `FichaRegistrada`).
- Toda petición pasa por `services/api.ts` mediante un `<módulo>Api.ts`; los servicios sin red se llaman `<asunto>Service.ts`.
- Formato con Prettier (`frontend/.prettierrc.json`); `npm run typecheck`, `npm test` y `npm run build` sin errores. Las pruebas van junto
  al código que prueban (`*.test.ts(x)`).

## Base de datos

- Scripts numerados e idempotentes en `database/sistema-inteligente`, ejecutados completos y en orden (`InstalarLocal.ps1`).
- Cada procedimiento tiene una sola definición vigente: si un script posterior lo reemplaza, el anterior deja solo una línea
  que indica dónde está la nueva.
- Archivos UTF-8 con BOM; aplicar siempre con `sqlcmd ... -C -I -f 65001`.
- `08_Validacion.sql` se ejecuta al final y sus listas se actualizan con cada objeto nuevo. Un procedimiento nuevo exige volver a ejecutar
  `38_PermisosMinimos.sql` (otorga los permisos por rol según los procedimientos existentes). `database/pruebas/PruebasFuncionales.sql`
  debe seguir terminando en "todas correctas".
- Objetos propios con prefijo `TI_` y procedimientos `Usp_TI_<Asunto>_<Acción>`.
- Estándar T-SQL: palabras clave con inicial mayúscula (`Select`, `From`, `Where`, `Create`, `Begin`); tipos en minúscula
  (`varchar`, `nvarchar`, `int`, `decimal`, `datetime2`, `bit`, `uniqueidentifier`); conectores en minúscula (`as`, `on`,
  `and`, `or`, `is`, `not`, `in`, `like`, `between`); funciones como `IsNull()`, `Count()`, `Convert()`, `Object_Id()`;
  tabulaciones de cuatro posiciones; alias en minúscula solo cuando ayudan a leer; comentarios solo para intención,
  reglas, riesgos y decisiones no evidentes.

## Seguridad

- Nunca conectar a producción durante el desarrollo ni usar `sa` o permisos administrativos en la API.
- No guardar contraseñas, claves de IA ni cadenas de conexión con credenciales en archivos versionados: van en variables
  de entorno o en la configuración local. Las claves de IA solo existen en el servidor.
- El agente solo lee y diagnostica; un cambio productivo exige un ejecutor catalogado (`Usp_TI_AgenteAccion_*`) y la
  decisión de TI.

## Git

- Nunca se sube un secreto: lo que entra en un commit queda en el historial.
- Nunca `--force` sin permiso explícito.
- Autoría siempre la misma, sin pies de atribución de herramientas.
- Se sube solo cuando se pide; ramas `<tipo>/<asunto-en-minusculas>` y commits con la primera línea corta en imperativo.
