# Sistema de Tickets Inteligente

Modernización del sistema corporativo de incidencias con backend ASP.NET Core, frontend React y SQL Server.

## Estructura

```text
SistemaTicketsInteligente/
├── SistemaTicketsInteligente.sln
├── backend/
│   └── SistemaTicketsInteligente.Api/
│       ├── Controllers/     Entrada HTTP
│       ├── BLL/             Reglas y coordinación del negocio
│       ├── DAO/             Acceso a SQL Server
│       └── DTO/             Contratos y modelos
├── frontend/                Aplicación React
├── database/                Scripts SQL Server
└── docs/                    Documentación funcional y técnica
```

La solución mantiene la separación Controller -> BLL -> DAO -> Stored Procedures sin dividir cada responsabilidad en un proyecto independiente.

## Bases de datos

La aplicación distingue la base propia del sistema nuevo de las fuentes corporativas confirmadas en el legado:

| Conexión | Catálogo | Uso |
|---|---|---|
| `CnnSistemaTickets` | `GestionSistemas` | Operación del proyecto nuevo y sus objetos `TI_*` / `Usp_TI_*` |
| `CnnGestionTi` | `GestionSistemas` | Fuente histórica del sistema de incidencias legado |
| `CnnSeguridad` | `IntranetCalimod` | Perfiles y menús del sistema legado |
| `CnnSpring` | `Spring` | Identidad y cargos corporativos |

En desarrollo las cuatro conexiones utilizan la instancia SQL Server local. En un ambiente corporativo, las credenciales no deben guardarse en el repositorio y deben suministrarse por configuración segura. Por ejemplo, en PowerShell:

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
$env:ConnectionStrings__CnnSistemaTickets = "Server=SERVIDOR;Database=GestionSistemas;User ID=USUARIO;Password=CLAVE;Encrypt=True;TrustServerCertificate=True;"
$env:ConnectionStrings__CnnGestionTi = "Server=SERVIDOR;Database=GestionSistemas;User ID=USUARIO;Password=CLAVE;Encrypt=True;TrustServerCertificate=True;"
$env:ConnectionStrings__CnnSeguridad = "Server=SERVIDOR;Database=IntranetCalimod;User ID=USUARIO;Password=CLAVE;Encrypt=True;TrustServerCertificate=True;"
$env:ConnectionStrings__CnnSpring = "Server=SERVIDOR;Database=Spring;User ID=USUARIO;Password=CLAVE;Encrypt=True;TrustServerCertificate=True;"
$env:IdentidadCorporativa__Habilitada = "true"
dotnet run --project backend/SistemaTicketsInteligente.Api
```

Las variables de entorno tienen prioridad sobre los archivos. Las credenciales reales no deben reemplazar los marcadores dentro de un archivo versionado.

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

Después de definir la variable, se debe reiniciar la API. Si la clave no existe o el proveedor no está disponible, el sistema vuelve automáticamente al modo de conocimiento local. Nunca se debe colocar la clave en React ni en un archivo versionado.

El asistente operativo para TI está disponible para los perfiles `TEC`, `SUP` y `ADM`. Consulta los catálogos vigentes de áreas, líneas, ítems, tipos, categorías, SLA, formatos, conocimiento y usuarios. También puede preparar la creación o actualización de un usuario corporativo a partir de una instrucción como:

```text
Crea el usuario JPEREZ en el area 021 con perfil USR y correo jperez@empresa.com
```

La operación valida los catálogos, muestra una propuesta y exige confirmación manual mediante un token firmado que vence en 10 minutos. No crea identidades ni contraseñas: el usuario debe existir previamente en Spring y la API reutiliza el flujo corporativo de sincronización. En `Local` la ejecución permanece bloqueada porque `IdentidadCorporativa:Habilitada` es `false`; en `Empresa` se habilita mediante la conexión corporativa ya configurada.

## Backend

```powershell
dotnet restore SistemaTicketsInteligente.sln
dotnet build SistemaTicketsInteligente.sln
dotnet run --project backend/SistemaTicketsInteligente.Api
```

Comprobación básica: `GET /api/salud`.

El endpoint `GET /api/salud` abre una conexion real a SQL Server: responde `200` con `baseDatos: disponible` o `503` cuando la base no responde.

El perfil `Empresa` valida las cuatro conexiones al arrancar y se detiene con un mensaje claro mientras encuentre los marcadores `SERVIDOR_EMPRESA`, `USUARIO_EMPRESA` o `CLAVE_EMPRESA`. Las conexiones SQL aplican reintentos breves ante fallos transitorios.

## Frontend

```powershell
Set-Location frontend
npm install
npm run dev
```

La aplicación consulta la sesión al abrirse, muestra el Login cuando no existe una cookie válida y protege temporalmente la ruta `/inicio`.
