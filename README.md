# Sistema de Tickets Inteligente

Modernización del sistema corporativo de incidencias con backend ASP.NET Core, frontend React y SQL Server.

## Estructura

```text
SistemaTicketsInteligente/
├── SistemaTicketsInteligente.slnx
├── .github/workflows/ci.yml   Integración continua
├── backend/SistemaTicketsInteligente.Pruebas/   Pruebas del backend (xUnit)
├── backend/SistemaTicketsInteligente.Api/
│   ├── Controllers/     Entrada HTTP (un controlador por módulo)
│   ├── BLL/             Reglas de cada módulo; llaman a sus Stored Procedures (Agente/ e IA/ aparte)
│   ├── Comun/           BaseDatos, lectura de resultados, validaciones, archivos y controlador base
│   └── DTO/             Contratos de cada módulo
├── frontend/src/        features/ (pantallas por módulo), components/ (compartidos) y services/ (API)
├── database/            Scripts SQL Server (00 a 40) y pruebas funcionales de la base
└── docs/                Documentación técnica y funcional, guía de reconstrucción, runbook del agente y decisiones
```

El flujo es Controller -> BLL -> Stored Procedures, en un solo proyecto y sin una capa DAO intermedia: cada BLL valida y llama a sus procedimientos mediante `Comun/BaseDatos`. El detalle está en [docs/03_EstructuraSolucion.md](docs/03_EstructuraSolucion.md) y las reglas del proyecto en [CLAUDE.md](CLAUDE.md).

## Bases de datos

La aplicación distingue la base propia del sistema nuevo de las fuentes corporativas confirmadas en el legado:

| Conexión | Catálogo | Uso |
|---|---|---|
| `CnnSistemaTickets` | `GestionSistemas` | Operación del proyecto nuevo y sus objetos `TI_*` / `Usp_TI_*` |
| `CnnGestionTi` | `GestionSistemas` | Fuente histórica del sistema de incidencias legado |
| `CnnSeguridad` | `IntranetCalimod` | Perfiles y menús del sistema legado |
| `CnnSpring` | `Spring` | Identidad y cargos corporativos |
| `CnnAgenteLectura` | `GestionSistemas` | Herramientas de diagnóstico del agente (login en `Rol_TI_AgenteLectura`) |
| `CnnAgenteEscritura` | `GestionSistemas` | Ejecutores catalogados del agente (login en `Rol_TI_AgenteEscritura`) |

En desarrollo las conexiones utilizan la instancia SQL Server local (las dos del agente son opcionales: sin ellas se usa `CnnSistemaTickets`). En un ambiente corporativo cada una usa su propio login sin permisos administrativos: la API en `Rol_TI_Api` y el agente en sus roles de lectura y escritura (`38_PermisosMinimos.sql`). En un ambiente corporativo, las credenciales no deben guardarse en el repositorio y deben suministrarse por configuración segura. Por ejemplo, en PowerShell:

```powershell
$env:ConnectionStrings__CnnGestionTi = "Server=SERVIDOR;Database=GestionSistemas;User ID=USUARIO;Password=CLAVE;Encrypt=True;TrustServerCertificate=True;"
$env:ConnectionStrings__CnnSeguridad = "Server=SERVIDOR;Database=IntranetCalimod;User ID=USUARIO;Password=CLAVE;Encrypt=True;TrustServerCertificate=True;"
$env:ConnectionStrings__CnnSpring = "Server=SERVIDOR;Database=Spring;User ID=USUARIO;Password=CLAVE;Encrypt=True;TrustServerCertificate=True;"
$env:IdentidadCorporativa__Habilitada = "true"
```

Los bloques `INSERT` de los informes empresariales son muestras parciales y redactadas. No sustituyen una migración desde las bases completas y no deben cargarse como si fueran el conjunto real.

Los scripts están organizados en `database/legado` para las tres bases reconstruidas desde los informes y `database/sistema-inteligente` para los objetos `TI_*` que extienden `GestionSistemas` y permiten ejecutar esta aplicación.

### Perfiles Local y Empresa

Las dos configuraciones se conservan por separado. `appsettings.Local.json` usa las bases reconstruidas en SQL Server local y `appsettings.Empresa.json` contiene los marcadores para el servidor corporativo. JSON no admite comentarios; el perfil activo se elige sin borrar conexiones mediante `ASPNETCORE_ENVIRONMENT`.

Ejecucion local:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Local"
dotnet run --project backend/SistemaTicketsInteligente.Api
```

Ejecucion en la empresa:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Empresa"
$env:ConnectionStrings__CnnSistemaTickets = "Server=SERVIDOR;Database=GestionSistemas;User ID=USUARIO_API;Password=CLAVE;Encrypt=True;TrustServerCertificate=False;"
$env:ConnectionStrings__CnnGestionTi = "Server=SERVIDOR;Database=GestionSistemas;User ID=USUARIO_API;Password=CLAVE;Encrypt=True;TrustServerCertificate=False;"
$env:ConnectionStrings__CnnSeguridad = "Server=SERVIDOR;Database=IntranetCalimod;User ID=USUARIO;Password=CLAVE;Encrypt=True;TrustServerCertificate=False;"
$env:ConnectionStrings__CnnSpring = "Server=SERVIDOR;Database=Spring;User ID=USUARIO;Password=CLAVE;Encrypt=True;TrustServerCertificate=False;"
$env:ConnectionStrings__CnnAgenteLectura = "Server=SERVIDOR;Database=GestionSistemas;User ID=USUARIO_AGENTE_LECTURA;Password=CLAVE;Encrypt=True;TrustServerCertificate=False;"
$env:ConnectionStrings__CnnAgenteEscritura = "Server=SERVIDOR;Database=GestionSistemas;User ID=USUARIO_AGENTE_ESCRITURA;Password=CLAVE;Encrypt=True;TrustServerCertificate=False;"
$env:AllowedHosts = "nombre-del-servidor"
$env:IdentidadCorporativa__Habilitada = "true"
dotnet run --project backend/SistemaTicketsInteligente.Api
```

Las variables de entorno tienen prioridad sobre los archivos. Las credenciales reales no deben reemplazar los marcadores dentro de un archivo versionado.

`TrustServerCertificate=False` exige que el certificado de SQL Server sea de una entidad en la que confíe el servidor de la API. En Empresa la cookie de sesión viaja solo por HTTPS y `AllowedHosts` debe listar el nombre real del servidor.

El perfil `Diagnostico` (`appsettings.Diagnostico.json`) sirve para probar el agente contra copias de prueba: solo arranca si todas las bases configuradas terminan en `_TEST` y deshabilita la ejecución de cambios (`AgenteTI:SoloDiagnostico`).

## Control del agente

Cuánto puede hacer el Agente de Ingeniería lo decide TI desde **Maestros TI → Agente y autonomía** (solo un ADM puede cambiarlo; todo TI lo consulta):

| Interruptor | Efecto |
|---|---|
| `APAGADO` | No investiga, no propone ni ejecuta. |
| `SOMBRA` | Investiga y propone, nunca ejecuta: sirve para medir su acierto en Reportes → Agente. |
| `ASISTIDO` (inicial) | Ejecuta solo lo que TI decide y, si la acción lo exige, lo que otro operador aprobó. |
| `AUTONOMO` | Además ejecuta sin humano las acciones que la política libera: solo solicitudes (`SOL`), acciones reversibles, de riesgo no mayor al techo y con la confianza mínima de la política. |

Al instalar, todas las políticas están en "Con aprobación de TI": nada es autónomo hasta que un ADM lo libere. Cómo apagarlo, reconciliar una ejecución o revertir un cambio está en [docs/05_RunbookAgente.md](docs/05_RunbookAgente.md); las decisiones del plan de mejoras, en [docs/06_RegistroDecisiones.md](docs/06_RegistroDecisiones.md).

## Asistentes IA

El asistente de colaboradores funciona en dos modos sin cambiar la interfaz:

- `CONOCIMIENTO`: modo local sin costo. Busca únicamente en artículos publicados por TI y permite consultar los tickets propios del usuario.
- `IA`: usa la Responses API de OpenAI para redactar una orientación más natural sobre el mismo contexto autorizado.

Cuando el conocimiento publicado no resuelve el caso, el asistente puede preparar un borrador de ticket con el título y la descripción de la conversación. El colaborador siempre revisa la clasificación, completa la evidencia y realiza el envío final desde `Nuevo Ticket`.

La conversación no se persiste. El backend no envía adjuntos, contraseñas, tickets de otros usuarios ni la base completa al proveedor. La clave se configura únicamente en el servidor:

```powershell
$env:OPENAI_API_KEY = "TU_CLAVE"
$env:AsistenteIA__Modelo = "gpt-5-mini"
```

Para pruebas sin costo puede usarse **Gemini en su nivel gratuito** (sin tarjeta): una sola clave de [Google AI Studio](https://aistudio.google.com/apikey) activa el asistente del colaborador, el diagnóstico del Agente de Ingeniería y la observación Live. Como alternativa existe Groq (`GROQ_API_KEY`, clave gratuita en console.groq.com), aunque su límite gratuito de tokens por minuto es bajo para investigaciones extensas.

```powershell
$env:GEMINI_API_KEY = "TU_CLAVE_DE_AI_STUDIO"
# Opcional: fijar proveedor y modelo (por defecto se usa el primero con clave: OpenAI, Gemini, Groq)
$env:AsistenteIA__Proveedor = "Gemini"
$env:AsistenteIA__ModeloGemini = "gemini-3.5-flash"
```

Los modelos gratuitos de Gemini se saturan por momentos (error 503). El sistema recorre una cadena de modelos (`AsistenteIA:ModelosGeminiRespaldo`) y recuerda unos minutos cuáles están saturados para no esperarlos otra vez; el análisis de grabaciones usa `AsistenteIA:ModeloGeminiVideo`. Si todos fallan, el agente continúa sin IA con la evidencia reunida.

Cada modelo tiene un tiempo máximo propio: `AsistenteIA:SegundosPorModeloChat` (25 s, chat) y `AsistenteIA:SegundosPorModelo` (75 s, borradores e investigación). Si un modelo no responde a tiempo, se marca como saturado y se prueba el siguiente. `AsistenteIA:MaxTokens` debe ser al menos 1800: los modelos con razonamiento gastan parte de ese límite pensando y, con menos, la respuesta queda cortada. El borrador del ticket y el diagnóstico se piden con un esquema JSON y reservan su propio espacio.

La investigación con herramientas dura como máximo `AgenteTI:TiempoMaximoInvestigacionSegundos` (240 s). Al vencer, el agente deja de pedir herramientas y entrega el diagnóstico con lo reunido (tiene 90 s más para esa respuesta). Una herramienta que agota su tiempo dos veces, o que falla cuatro veces seguidas, se desactiva en esa investigación.

### Réplica técnica: código fuente y base de datos de cada sistema

El Agente de Ingeniería puede buscar el mensaje de error en el código fuente y en los procedimientos de un sistema, leer esa lógica, revisar la estructura de las tablas y comprobar la condición con un `SELECT` de solo lectura. Cada sistema se registra en `AgenteTI:Sistemas` (en `appsettings.Local.json` están de ejemplo este portal y la réplica local de Spring):

```json
"AgenteTI": {
  "OperadorAutomatico": "SUP001",
  "Sistemas": [
    { "Codigo": "ERP_SPRING", "Nombre": "ERP Spring", "Lineas": ["115"], "ConexionLectura": "CnnSpringLectura",
      "RutaCodigo": "D:\\Fuentes\\Spring", "ConsultasLibres": true, "ColumnasSensibles": ["Sueldo", "Cuenta", "Clave"] }
  ]
}
```

- `Lineas`: líneas de ticket que pertenecen al sistema; con ellas el agente busca el error automáticamente.
- `ConexionLectura`: nombre de una cadena en `ConnectionStrings`. En producción debe usar un login **solo lectura** (`db_datareader`, sin permisos de ejecución ni escritura). Además, cada consulta se valida con el analizador oficial de T-SQL (solo un `SELECT`, sin `INTO`, servidores vinculados, otras bases, funciones de usuario ni columnas sensibles) y se ejecuta en una transacción que siempre se revierte, con un máximo de 50 filas y 30 segundos por consulta.
- `RutaCodigo`: carpeta del código fuente (`@repositorio` usa este portal). Se excluyen configuraciones, credenciales y compilados.
- `ConsultasLibres`: `false` deja al agente solo con metadatos y definiciones, sin consultar datos.
- `ColumnasSensibles`: fragmentos de nombre de columna que nunca se consultan ni se muestran (por ejemplo, `Sueldo` protege `SueldoActualLocal`).
- `OperadorAutomatico`: responsable de las investigaciones que el agente inicia solo cuando el ticket todavía no tiene responsable TI.

Para buscar un texto en la base, la API lee una vez las definiciones de procedimientos, vistas, funciones y triggers de cada sistema (hasta 12 millones de caracteres) y las busca en memoria, sin distinguir mayúsculas ni tildes. Solo vuelve a leerlas si cambió algún objeto. Si el catálogo es más grande, busca directamente en el servidor. El mensaje se busca completo y también sin sus datos variables: "…punto de emisión T003." encuentra el literal `'…punto de emisión ' + @punto` del procedimiento.

**SQL Server local con poca memoria.** En un equipo de 6 GB con otras aplicaciones abiertas, Windows puede dejar a SQL Server casi sin memoria. Los síntomas son el error 802 ("No hay suficiente memoria disponible en el grupo de búferes"), esperas `RESOURCE_SEMAPHORE` y lecturas de minutos. En este equipo se fijó un mínimo garantizado:

```sql
Exec sp_configure 'show advanced options', 1; Reconfigure;
Exec sp_configure 'min server memory (MB)', 512; Reconfigure;   -- para revertir: 0
```

Cuando el colaborador muestra su error en pantalla (por invitación de TI o desde su asistente), la pantalla se graba con su consentimiento, la grabación queda en el ticket, el agente la analiza, replica técnicamente el proceso, diagnostica y notifica al responsable TI. Ninguna de estas tareas modifica datos.

Después de definir la variable, se debe reiniciar la API. Si la clave no existe o el proveedor no está disponible, el sistema vuelve automáticamente al modo de conocimiento local. Nunca se debe colocar la clave en React ni en un archivo versionado.

El asistente operativo para TI está disponible para los perfiles `TEC`, `SUP` y `ADM`. Consulta los catálogos vigentes de áreas, líneas, ítems, tipos, categorías, SLA, formatos, conocimiento y usuarios. También puede preparar la creación o actualización de un usuario corporativo a partir de una instrucción como:

```text
Crea el usuario JPEREZ en el area 021 con perfil USR y correo jperez@empresa.com
```

La operación valida los catálogos, muestra una propuesta y exige confirmación manual mediante un token firmado que vence en 10 minutos. No crea identidades ni contraseñas: el usuario debe existir previamente en Spring y la API reutiliza el flujo corporativo de sincronización. En `Local` la ejecución permanece bloqueada porque `IdentidadCorporativa:Habilitada` es `false`; en `Empresa` se habilita mediante la conexión corporativa ya configurada.

## Arranque rápido (local)

```powershell
.\IniciarProyecto.ps1
```

Verifica que SQL Server esté encendido y que no haya otra API en el puerto 5000, carga las claves de IA guardadas como variables de usuario, abre el frontend si no está corriendo y compila e inicia la API con el perfil `Local`. Si cambió el código del backend, la API debe reiniciarse para tomarlo.

## Backend

```powershell
dotnet restore SistemaTicketsInteligente.slnx
dotnet build SistemaTicketsInteligente.slnx
dotnet test SistemaTicketsInteligente.slnx
dotnet run --project backend/SistemaTicketsInteligente.Api
```

Comprobación básica: `GET /api/salud`.

El endpoint `GET /api/salud` abre una conexion real a SQL Server: responde `200` con `baseDatos: disponible` o `503` cuando la base no responde.

El perfil `Empresa` valida las seis conexiones y `AllowedHosts` al arrancar y se detiene con un mensaje claro mientras encuentre los marcadores `SERVIDOR_EMPRESA`, `USUARIO_EMPRESA`, `CLAVE_EMPRESA` o `HOST_EMPRESA`. Las conexiones SQL aplican reintentos breves ante fallos transitorios.

Toda operación que modifica datos exige el token antifalsificación de la sesión (`X-CSRF-TOKEN`, que entrega `GET /api/autenticacion/token-csrf`); el frontend lo maneja solo. Cada respuesta incluye `X-Correlation-ID`, el mismo identificador que aparece en los registros, en la auditoría y como `idSeguimiento` de un error.

## Frontend

```powershell
Set-Location frontend
npm install
npm run dev
```

La aplicación consulta la sesión al abrirse, muestra el Login cuando no existe una cookie válida y protege cada ruta según el perfil (la autorización definitiva la hace cada endpoint). Antes de cerrar un cambio: `npm run typecheck`, `npm test`, `npm run build` y `npx prettier@3 --check "src/**/*.{ts,tsx,css}"`.

## Pruebas

| Parte | Comando | Qué cubre |
|---|---|---|
| Backend | `dotnet test SistemaTicketsInteligente.slnx` | Política de autonomía, validador de SQL del agente, redacción de datos sensibles, ficha, parámetros del agente, rutas de archivos, matriz de autorización (cada endpoint frente a cada perfil), CSRF y correlación. Sin base de datos. |
| Backend contra la base | `$env:PRUEBAS_BASE_DATOS = "1"; dotnet test SistemaTicketsInteligente.slnx` | Cada consulta con un usuario de cada perfil permitido y la ficha obligatoria del requerimiento (solo lee). `PRUEBAS_FLUJOS=1` agrega el ciclo completo de un ticket, que **crea datos**: úsalo solo en una base desechable (el CI lo hace en un contenedor). |
| Base de datos | `sqlcmd -S . -E -C -I -b -f 65001 -i database/pruebas/PruebasFuncionales.sql` | 22 casos con `Rollback` (no deja datos). |
| Frontend | `npm test` (en `frontend/`) | Cliente de API y CSRF, ficha, guardas de ruta, contexto de sesión y componente de la ficha. |

El CI (`.github/workflows/ci.yml`) repite todo en cada push a `main` y en cada pull request, instala la base desde cero en un SQL Server 2022 en contenedor con tres logins de permisos mínimos, y revisa secretos (gitleaks) y dependencias vulnerables (NuGet y npm).

## Pendientes

- **Funciones con API pero sin pantalla:** edición temprana del ticket por el colaborador (`POST /api/mis-tickets/{n}/editar`), carga de formatos de soporte (`POST /api/configuracion-ti/formatos`) y visibilidad de artículos para el usuario (`POST /api/configuracion-ti/conocimiento/{codigo}/visibilidad`). Mientras no exista esa pantalla, un artículo solo se publica para colaboradores por API o en la base.
- **Live:** si el modelo de Gemini Live se satura, no hay un modelo de respaldo para la sesión de voz y pantalla.
- **Decisiones de TI abiertas:** unificar los tipos heredados del legado (`001` a `003`) con `INC`, `REQ` y `SOL`, qué acciones de escritura se liberan como autónomas, estados AU, EJ y ES, y las demás listadas en [docs/06_RegistroDecisiones.md](docs/06_RegistroDecisiones.md).
