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

Comprobación básica: `GET /api/salud`.

## Frontend

```powershell
Set-Location frontend
npm install
npm run dev
```

La aplicación consulta la sesión al abrirse, muestra el Login cuando no existe una cookie válida y protege temporalmente la ruta `/inicio`.
