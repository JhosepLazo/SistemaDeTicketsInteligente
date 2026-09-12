# Sistema de Tickets Inteligente

Modernización del sistema corporativo de incidencias con backend ASP.NET Core, frontend React y SQL Server.

## Estructura

- `backend/SistemaTicketsInteligente.Api`: API HTTP y composición de dependencias.
- `backend/ComponenteBLL`: reglas y coordinación del negocio.
- `backend/ComponenteDAO`: acceso a datos e integraciones.
- `backend/ComponenteDTO`: contratos y modelos compartidos.
- `frontend`: aplicación React organizada por funcionalidad.
- `database`: scripts SQL Server.
- `docs`: documentación funcional y técnica.

La descripción completa de dependencias y criterios está en [docs/03_EstructuraSolucion.md](docs/03_EstructuraSolucion.md).

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

El inicio de sesión todavía no envía credenciales. Deben definirse primero la fuente de identidad corporativa, el tratamiento seguro de contraseñas y el Stored Procedure de consulta.
