# MemByte-IM

Gestión interna de proyectos y soluciones digitales.

**MemByte-IM** es la intranet empresarial con la que MemByte gestiona su propia operativa y la de sus clientes: empleados, control de acceso, mensajería interna, compras, y el núcleo del negocio, la **gestión de proyectos y clientes** (incluyendo registro de horas y borradores de facturación).

## Stack

- .NET 10 LTS · C# 13
- Blazor Web App (SSR + interactividad selectiva por componente)
- Entity Framework Core 10
- MudBlazor

## Documentación

- [`agents.md`](./agents.md) — roles, permisos y criterios de éxito del sistema.
- [`planes-de-desarrollo/01-plan-inicial-arquitectura.md`](./planes-de-desarrollo/01-plan-inicial-arquitectura.md) — arquitectura, stack tecnológico y guía de despliegue inicial.
- [`planes-de-desarrollo/02-runbook-despliegue-azure.md`](./planes-de-desarrollo/02-runbook-despliegue-azure.md) — runbook de aprovisionamiento/despliegue en Azure, telemetría/alertas de seguridad y rollout controlado.

## Módulos

- Gestión de Empleados
- Gestión de Usuarios y Roles
- Mensajería Interna
- Gestión de Compras
- **Gestión de Proyectos y Clientes** (proyectos, entregables, horas y borradores de facturación)

## Requisitos

- Visual Studio Community 2026 (o superior) con carga de trabajo ASP.NET y desarrollo web.
- .NET 10 SDK.

## Puesta en marcha

```powershell
dotnet build
dotnet test
```

Abre `MemByte-IM.sln` en Visual Studio y establece `MemByte.Web` como proyecto de inicio para ejecutar la aplicación.

## Despliegue en Azure

La infraestructura se declara en [`infra/main.bicep`](./infra/main.bicep): App Service Linux
(B1 por defecto), Azure Container Registry privado, Key Vault con RBAC e identidad
administrada, Log Analytics y Application Insights. PostgreSQL/Supabase se mantiene externo.
Los parámetros de producción apuntan a recursos nuevos en `rg-membyte-main`,
en Spain Central (`spaincentral`), sin modificar el entorno de desarrollo antiguo.

El `Dockerfile` multietapa publica .NET 10 y ejecuta la aplicación como usuario no root
en el puerto 8080. El despliegue se inicia manualmente desde GitHub Actions con
**Run workflow** en `master`; exige que el último CI de ese commit haya terminado
correctamente. Publica una imagen etiquetada con el SHA y aplica Bicep mediante OIDC,
sin perfiles de publicación ni contraseñas de Azure.

Consulta el [runbook de Azure](./planes-de-desarrollo/02-runbook-despliegue-azure.md)
para configurar OIDC, secretos, migrar el despliegue ZIP existente y hacer rollback.
