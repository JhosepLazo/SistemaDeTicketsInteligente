# Estructura de la solución

## Objetivo

Un solo proyecto backend (más su proyecto de pruebas) y un solo frontend, organizados por responsabilidad y por funcionalidad, con el menor número de carpetas y capas que permita mantenerlos sin mezclar responsabilidades.

```text
SistemaTicketsInteligente/
├── SistemaTicketsInteligente.slnx       Solución (formato .slnx): API y pruebas
├── .github/workflows/ci.yml             Integración continua: compilación, pruebas, instalación de la base y escaneo de secretos
├── global.json                          SDK de .NET 10
├── .editorconfig · .gitattributes       Formato y fin de línea (LF) iguales en todo editor y equipo
├── CLAUDE.md                            Reglas permanentes del proyecto
├── IniciarProyecto.ps1                  Arranque local: SQL Server, claves de IA, frontend y API
├── backend/SistemaTicketsInteligente.Api/
│   ├── Controllers/                     Un controlador por módulo; solo HTTP e identidad de la cookie
│   ├── BLL/                             Reglas de cada módulo; llaman a sus Stored Procedures
│   │   ├── Agente/                      Agente de Ingeniería: investigación, réplica técnica, grabaciones y cola automática
│   │   └── IA/                          Proveedores de IA (texto, Live, video), conocimiento semántico y redacción de datos sensibles
│   ├── Comun/                           Infraestructura compartida: BaseDatos, LecturaSql, Validacion, Archivos,
│   │                                    ControladorBase, identidad corporativa, correlación y traza del agente
│   ├── DTO/                             Contratos de cada módulo (un archivo por módulo)
│   └── Program.cs                       Composición, seguridad (cookie, revalidación, CSRF) y manejo general de errores
├── backend/SistemaTicketsInteligente.Pruebas/
│   ├── Unitarias/                       Reglas puras: política de autonomía, validador SQL, redactor, ficha, rutas
│   └── Integracion/                     API en memoria: matriz de autorización, CSRF, correlación y flujos contra la base
├── frontend/src/
│   ├── features/<módulo>/               Pantallas de cada módulo con su CSS y sus componentes propios
│   ├── components/                      Lo compartido por varias pantallas: marco del portal, íconos, notificaciones y ficha registrada
│   ├── services/                        api.ts (cliente único), <módulo>Api.ts (red) y <asunto>Service.ts (sin red)
│   ├── **/*.test.ts(x)                  Pruebas de Vitest junto al código que prueban
│   └── App.tsx · main.tsx · index.css   Rutas, arranque y estilos base con el tema común
├── database/
│   ├── legado/                          Reconstrucción local de las bases corporativas
│   ├── sistema-inteligente/             Objetos TI_* y Usp_TI_*, numerados y en orden (00 a 40; 08 valida al final)
│   ├── pruebas/                         PruebasFuncionales.sql: casos aislados con Rollback
│   ├── InstalarLocal.ps1 · GenerarHash.cs
│   └── README.md
└── docs/                                Documentación técnica y funcional, guía de reconstrucción, diccionario, esta estructura,
                                         análisis funcional, runbook del agente y registro de decisiones
```

## Flujo y dependencias

```text
React (features) -> services/<módulo>Api.ts -> services/api.ts -> API
Controller (ControladorBase) -> BLL -> BaseDatos -> Stored Procedure
```

- **Controller:** recibe la petición, toma usuario y área de la cookie y traduce los errores a HTTP (`ControladorBase.Responder`). No contiene reglas ni SQL.
- **BLL:** valida, llama al Stored Procedure con parámetros tipados (tipo y largo exactos) y arma la respuesta leyendo cada columna por su nombre (`LecturaSql`). No hay capa DAO: cada BLL es dueña de sus procedimientos.
- **BaseDatos:** abre la conexión, ejecuta y convierte los errores de negocio del procedimiento (`Throw 50000-50999`) en `InvalidOperationException`, que el controlador devuelve como 409 con su mensaje. El agente usa conexiones propias: lectura para sus herramientas y escritura para sus ejecutores.
- **DTO:** solo transportan datos.
- **Frontend:** cada pantalla vive en su funcionalidad; `services/api.ts` concentra la cookie, el token CSRF, la sesión vencida (401), la traza del agente y los mensajes de error.

## Criterio de mantenimiento

- Un archivo por clase pública o por asunto; las clases grandes se dividen por tema con archivos parciales (`AsistenteTIBLL.Sesion.cs`, `.Investigacion.cs`, `.Decision.cs`).
- Se crea una carpeta, capa, interfaz o proyecto solo cuando resuelve un problema concreto.
- Un solo proyecto backend y su proyecto de pruebas: por eso no hay `Directory.Build.props` ni `Directory.Packages.props` (las propiedades y versiones viven en cada `.csproj`).
- Antes de integrar un cambio: `dotnet build` sin advertencias, `dotnet test`, `npm run typecheck`, `npm test`, `npm run build` y Prettier; el CI lo repite.
- El código del frontend se formatea con Prettier (`npx prettier@3 --write "src/**/*.{ts,tsx,css}"`, configuración en `frontend/.prettierrc.json`).

## Autenticación

- Identidad corporativa (Spring) cuando está habilitada; usuarios locales con `PasswordHasher` en desarrollo.
- Cookie HttpOnly con los claims mínimos: Usuario, NombreCompleto, Area y Perfil.
- Rate limiting en el inicio de sesión, límite de 5 fallos por minuto por usuario y auditoría de cada intento en `TI_Auditoria`.
- La sesión se revalida cada 2 minutos (usuario activo, mismo perfil y área) y toda operación que modifica datos exige el token CSRF de la sesión (`X-CSRF-TOKEN`).
- El frontend recupera la sesión al abrirse y protege las rutas según el perfil; la autorización definitiva la hace cada endpoint.
- La contraseña y su hash nunca se envían al frontend ni se escriben en auditoría o logs.
