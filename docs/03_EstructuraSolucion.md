# Estructura de la solución

## Objetivo

La solución conserva la separación de responsabilidades observada en el sistema corporativo, adaptada a ASP.NET Core, C# y React, pero evitando dividir cada responsabilidad en proyectos independientes cuando no aporta valor de mantenimiento.

```text
SistemaTicketsInteligente/
├── SistemaTicketsInteligente.sln
├── backend/
│   └── SistemaTicketsInteligente.Api/
│       ├── Controllers/                 Entrada HTTP y sesión web
│       ├── BLL/                         Reglas y coordinación del negocio
│       ├── DAO/                         Acceso a SQL Server y Stored Procedures
│       ├── DTO/                         Contratos y modelos
│       ├── Program.cs                   Composición y middleware
│       └── appsettings.json             Configuración no sensible
├── frontend/                            Aplicación React
├── database/                            DDL, datos iniciales y Stored Procedures
└── docs/                                Documentación funcional y técnica
```

## Dependencias permitidas

```text
React -> API
Controller -> BLL + DTO
BLL -> DAO + DTO
DAO -> DTO + SQL Server
DTO -> ninguna responsabilidad funcional
```

La API no debe contener SQL. La BLL no debe conocer detalles de SQL ni componentes React. La DAO no debe decidir permisos ni reglas de negocio. Los DTO solo transportan información.

## Criterio de mantenimiento

La separación se realiza mediante carpetas y responsabilidades dentro de un único proyecto ASP.NET Core. Solo se crearán nuevos proyectos, interfaces, servicios base o abstracciones cuando exista una necesidad técnica concreta que justifique su mantenimiento.

El criterio general es mantener el menor número de archivos y capas posible sin mezclar responsabilidades.

## Autenticación

El backend de autenticación utiliza actualmente:

- Stored Procedures para consultar usuario y registrar auditoría.
- `PasswordHasher` de ASP.NET Core para verificar la contraseña.
- Cookie Authentication para mantener la sesión web.
- Claims mínimos: Usuario, NombreCompleto, Area y Perfil.
- Rate limiting sobre el endpoint de inicio de sesión.
- Respuestas genéricas para credenciales incorrectas y manejo general de errores.

La contraseña y su hash no deben enviarse al frontend, registrarse en auditoría ni escribirse en logs.

## Pendiente

- Generar un hash válido para los usuarios de desarrollo y ejecutar pruebas integrales del Login.
- Validar la futura integración corporativa con Spring, Active Directory o SSO sin copiar contraseñas.
- Implementar el frontend React después de cerrar las pruebas del backend.
