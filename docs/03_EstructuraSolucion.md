# Estructura de la solución

## Objetivo

La solución conserva la separación de responsabilidades observada en el sistema corporativo `GestionIncidencias`, adaptada a ASP.NET Core, C# y React.

```text
SistemaTicketsInteligente/
├── SistemaTicketsInteligente.sln
├── backend/
│   ├── SistemaTicketsInteligente.Api/   Entrada HTTP y composición
│   ├── ComponenteBLL/                   Reglas y coordinación del negocio
│   ├── ComponenteDAO/                   Acceso a SQL Server y sistemas externos
│   └── ComponenteDTO/                   Contratos y modelos compartidos
├── frontend/                            Aplicación React independiente
│   └── src/
│       ├── app/                         Composición global
│       ├── features/                    Módulos por capacidad de negocio
│       └── styles/                      Estilos globales
├── database/                            DDL, datos iniciales y validación SQL
└── docs/                                Documentación funcional y técnica
```

## Dependencias permitidas

```text
React → API
API → BLL + DTO + DAO (solo para composición de dependencias)
BLL → DAO + DTO
DAO → DTO + SQL Server
DTO → ninguna capa del sistema
```

La API no debe contener SQL. La BLL no debe conocer HTTP ni componentes de React. La DAO no debe decidir permisos o reglas del workflow. DTO no debe contener acceso a datos.

## Organización por funcionalidad

En cada componente, las clases se agrupan por capacidad (`Autenticacion`, `Incidencias`, `Maestros`, `Aprobaciones`, etc.). React sigue el mismo criterio dentro de `src/features` para evitar reproducir una página independiente por cada Web Form legado.

## Configuración

La conexión mantiene el nombre corporativo `CnnGestionTi`, pero su valor se obtiene mediante configuración de ASP.NET Core. Los secretos de producción no deben almacenarse en el repositorio.

## Decisiones pendientes

- Origen de identidad: Spring, Active Directory, SSO o autenticación local.
- Algoritmo y transición de contraseñas heredadas.
- Contrato definitivo del inicio de sesión.
- Stored Procedure `Usp_TI_BuscarUsuarioAutenticacion`.
- Política corporativa de versiones de .NET, Node.js y paquetes npm.
