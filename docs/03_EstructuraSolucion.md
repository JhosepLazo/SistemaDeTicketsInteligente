# Estructura de la solución

## Objetivo

Un solo proyecto backend y un solo frontend, organizados por responsabilidad y por funcionalidad, con el menor número de carpetas y capas que permita mantenerlos sin mezclar responsabilidades.

```text
SistemaTicketsInteligente/
├── SistemaTicketsInteligente.slnx       Solución (formato .slnx)
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
│   └── Program.cs                       Composición, seguridad y manejo general de errores
├── frontend/src/
│   ├── features/<módulo>/               Pantallas de cada módulo con su CSS y sus componentes propios
│   ├── components/                      Lo compartido por varias pantallas: marco del portal, íconos y notificaciones
│   ├── services/                        api.ts (cliente único), <módulo>Api.ts (red) y <asunto>Service.ts (sin red)
│   └── App.tsx · main.tsx · index.css   Rutas, arranque y estilos base con el tema común
├── database/
│   ├── legado/                          Reconstrucción local de las bases corporativas
│   ├── sistema-inteligente/             Objetos TI_* y Usp_TI_*, numerados y en orden
│   ├── InstalarLocal.ps1 · GenerarHash.cs
│   └── README.md
└── docs/                                Diccionario de datos, esta estructura y análisis funcional
```

## Flujo y dependencias

```text
React (features) -> services/<módulo>Api.ts -> services/api.ts -> API
Controller (ControladorBase) -> BLL -> BaseDatos -> Stored Procedure
```

- **Controller:** recibe la petición, toma usuario y área de la cookie y traduce los errores a HTTP (`ControladorBase.Responder`). No contiene reglas ni SQL.
- **BLL:** valida, llama al Stored Procedure con parámetros tipados (tipo y largo exactos) y arma la respuesta leyendo cada columna por su nombre (`LecturaSql`). No hay capa DAO: cada BLL es dueña de sus procedimientos.
- **BaseDatos:** abre la conexión, ejecuta y convierte los errores de negocio del procedimiento (`Throw 50000-50999`) en `InvalidOperationException`, que el controlador devuelve como 409 con su mensaje.
- **DTO:** solo transportan datos.
- **Frontend:** cada pantalla vive en su funcionalidad; `services/api.ts` concentra la cookie, la sesión vencida (401), la traza del agente y los mensajes de error.

## Criterio de mantenimiento

- Un archivo por clase pública o por asunto; las clases grandes se dividen por tema con archivos parciales (`AsistenteTIBLL.Sesion.cs`, `.Investigacion.cs`, `.Decision.cs`).
- Se crea una carpeta, capa, interfaz o proyecto solo cuando resuelve un problema concreto.
- Un solo proyecto backend: por eso no hay `Directory.Build.props` ni `Directory.Packages.props` (las propiedades y versiones viven en el `.csproj`).
- El código del frontend se formatea con Prettier (`npx prettier@3 --write "src/**/*.{ts,tsx,css}"`, configuración en `frontend/.prettierrc.json`).

## Autenticación

- Identidad corporativa (Spring) cuando está habilitada; usuarios locales con `PasswordHasher` en desarrollo.
- Cookie HttpOnly con los claims mínimos: Usuario, NombreCompleto, Area y Perfil.
- Rate limiting en el inicio de sesión y auditoría de cada intento en `TI_Auditoria`.
- El frontend recupera la sesión al abrirse y protege las rutas según el perfil; la autorización definitiva la hace cada endpoint.
- La contraseña y su hash nunca se envían al frontend ni se escriben en auditoría o logs.
