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

## Backend

```powershell
dotnet restore SistemaTicketsInteligente.sln
dotnet build SistemaTicketsInteligente.sln
dotnet run --project backend/SistemaTicketsInteligente.Api
```

Comprobaciones disponibles:

- `GET /api/salud`: confirma que la API está ejecutándose.
- `GET /api/salud/bd`: confirma que SQL Server está disponible para la API.

La autenticación utiliza cookie HttpOnly, expiración deslizante, rate limiting y protección antiforgery para operaciones autenticadas que modifican estado.

## Frontend

```powershell
Set-Location frontend
npm install
npm run dev
```

La aplicación consulta la sesión al abrirse, muestra el Login cuando no existe una cookie válida y protege temporalmente la ruta `/inicio`.

En desarrollo, Vite redirige `/api` hacia `http://localhost:5000`, por lo que no es necesario configurar una URL absoluta en los servicios React.

## Configuración por ambiente

La configuración local actual utiliza `appsettings.json`. En producción, los secretos y datos sensibles deben suministrarse mediante configuración externa y no versionarse en Git.

Ejemplo de variable de entorno para la conexión:

```text
ConnectionStrings__CnnGestionTi=<cadena de conexión de producción>
```

Los orígenes CORS también pueden sobreescribirse mediante configuración del ambiente. En producción la API fuerza cookie segura, HSTS y redirección HTTPS.
