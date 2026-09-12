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

El frontend React se desarrollará después de cerrar y validar completamente el backend del inicio de sesión.
