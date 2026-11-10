# Plan 03 — Gestión de Usuarios

> **Estado:** Borrador · **Versión:** 1.0 · **Fecha:** 2026-08-20
>
> **Depende de:** Plan 01 (arquitectura), módulo de Gestión de Empleados (implementado)

---

## Índice

1. [Contexto y objetivo](#1-contexto-y-objetivo)
2. [Análisis del código actual](#2-análisis-del-código-actual)
3. [Modelo de dominio propuesto](#3-modelo-de-dominio-propuesto)
4. [Diseño por capas](#4-diseño-por-capas)
5. [Flujos de negocio](#5-flujos-de-negocio)
6. [Seguridad y auditoría](#6-seguridad-y-auditoría)
7. [Permisos y actualización de agents.md](#7-permisos-y-actualización-de-agentsmd)
8. [UI y rutas propuestas](#8-ui-y-rutas-propuestas)
9. [Migraciones EF necesarias](#9-migraciones-ef-necesarias)
10. [Fases de implementación](#10-fases-de-implementación)
11. [Fuera de alcance](#11-fuera-de-alcance)

---

## 1. Contexto y objetivo

Hasta ahora la plataforma distingue:

- **`Employee`** (Domain): ficha de RRHH de un trabajador. No implica acceso a la intranet.
- **`ApplicationUser`** (Infrastructure/Identity): cuenta de acceso (login, contraseña, MFA, roles). Puede o no estar vinculada a un `Employee` (`EmployeeId` nullable, ya existente).

Ese diseño ya anticipaba el requisito actual: **no todo usuario es un empleado**. Ahora se añade un segundo origen de usuarios: **usuarios de cliente**, personas externas que acceden a una aplicación frontal independiente (fuera de esta intranet), pero cuya cuenta, rol y estado se administran **desde MemByte-IM**.

Según la aclaración de negocio: los usuarios se dan de alta de la misma forma independientemente de su origen; lo que cambia es que existirá una **entidad `Client`** que agrupa usuarios, y esos usuarios pueden tener funcionalidades diferentes según la naturaleza del cliente (a definir en el futuro). El objetivo de este plan es:

1. Diseñar el módulo **Gestión de Usuarios** (`/users`) para administrar cualquier `ApplicationUser` (interno o de cliente) desde el Admin.
2. Sentar la base mínima de la entidad **`Client`** (agrupador de usuarios), sin construir todavía el módulo completo de "Proyectos y Clientes" descrito en `agents.md`.
3. Mantener la separación limpia ya existente entre ficha (Employee/Client) y cuenta (ApplicationUser).

---

## 2. Análisis del código actual

| Pieza | Ubicación | Relevancia para este plan |
|---|---|---|
| `Employee` | `MemByte.Domain.Employees` | Patrón a replicar para `Client`: entidad con constructor validado, métodos `UpdateDetails/Activate/Deactivate`. |
| `IEmployeeService` / `EmployeeService` | `Application`/`Infrastructure.Employees` | Patrón a replicar para `IUserService`/`IClientService`: DTOs de solo lectura, `Request` records, `OperationResult` con errores de negocio. |
| `EmployeeList.razor`, `EmployeeCreate.razor` | `Web.Components.Pages.Employees` | Patrón de UI a replicar: `@attribute [Authorize(Roles = ...)]`, `EditForm` + `DataAnnotationsValidator`, filtros client-side, modal de confirmación de borrado. |
| `ApplicationUser` | `Infrastructure.Identity` | **Ya tiene** `EmployeeId`, `DepartmentId`, `DisplayName`, `IsMfaRequired`, `LastLoginAt`. Falta `ClientId`. |
| `ApplicationRoleNames` / `EmployeeRoles` | `Domain.Employees` / `Infrastructure.Identity` | Roles de autorización ya definidos (Admin, AreaManager, PurchasingAgent, Employee). No se toca su catálogo en este plan. |
| `AuthService`, `UserSession`, `AuthAuditEntry` | `Infrastructure.Auth` | Ya registran login/lockout/sesiones. Este plan **extiende** su uso (revocar sesión, nuevos `EventType`) en vez de crear un sistema paralelo. |
| `IdentitySeeder` | `Infrastructure.Identity.Seeding` | Comentario explícito en el código: *"en producción... las cuentas se crean desde el módulo de Gestión de Usuarios, nunca automáticamente"*. Este plan implementa esa promesa. |
| Envío de email | — | **No existe** ningún `IEmailSender`/SMTP. Es un hueco a resolver para invitaciones y reseteo de contraseña (ver §5 y §10). |
| `Client` (entidad) | — | No existe. `agents.md` solo prevé `ClientPortalUser` como rol de Fase 2, sin entidad de datos. |

**Conclusión:** la arquitectura (Clean Architecture, separación ficha/cuenta) ya está preparada para este módulo; no hace falta refactorizar nada existente, solo añadir piezas nuevas siguiendo los mismos patrones.

---

## 3. Modelo de dominio propuesto

### 3.1 Entidad `Client` (nueva, base mínima)

```csharp
namespace MemByte.Domain.Clients;

/// <summary>
/// Agrupador de usuarios externos (usuarios de cliente) que acceden a la
/// aplicación frontal de MemByte. Es la base mínima de la que partirá el
/// futuro módulo de "Gestión de Proyectos y Clientes" (agents.md): por ahora
/// solo identifica y agrupa cuentas, sin proyectos, tarifas ni facturación.
/// </summary>
public class Client
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }

    // Constructor, UpdateDetails(name), Activate()/Deactivate()
    // siguiendo el mismo patrón que Employee.cs.
}
```

No se modela todavía "naturaleza del negocio" ni catálogo de features por cliente: es la parte más incierta y de negocio; se deja como **extensión futura** (ver §11) para no sobre-diseñar. Cuando se defina, lo más probable es añadir un campo `ClientType`/`Segment` o una tabla de *feature flags* por cliente — no un cambio estructural sobre lo que aquí se crea.

### 3.2 Vínculo Usuario ↔ Cliente

Se añade a `ApplicationUser` (Infrastructure/Identity, no Domain, igual que `EmployeeId`):

```csharp
public Guid? ClientId { get; set; }
```

Reglas:
- Un `ApplicationUser` puede tener `EmployeeId`, `ClientId`, ambos a `null`, o (en teoría) ambos rellenos si algún día un empleado también representa a un cliente — no se restringe por regla de negocio, solo por UI (ver §5).
- `ClientId == null` ⇒ usuario "interno" a efectos de UI (empleado o cuenta de servicio).
- `ClientId != null` ⇒ usuario "de cliente"; la intranet lo agrupa bajo la ficha del `Client` correspondiente.
- El alta de usuario **no cambia de formulario** según el origen (tal y como se ha decidido): el mismo formulario permite opcionalmente elegir un `Employee` existente y/o un `Client` existente.

---

## 4. Diseño por capas

```
src/
├── MemByte.Domain/
│   └── Clients/
│       └── Client.cs
│
├── MemByte.Application/
│   ├── Clients/
│   │   ├── ClientDto.cs
│   │   ├── CreateClientRequest.cs
│   │   ├── UpdateClientRequest.cs
│   │   ├── ClientOperationResult.cs
│   │   └── IClientService.cs
│   └── Users/
│       ├── UserDto.cs                 // Id, Email, DisplayName, EmployeeId?, EmployeeFullName?,
│       │                              // ClientId?, ClientName?, Roles[], IsLockedOut, LastLoginAt, IsMfaRequired
│       ├── CreateUserRequest.cs       // Email, DisplayName, EmployeeId?, ClientId?, Roles[]
│       ├── UpdateUserRequest.cs       // DisplayName, EmployeeId?, ClientId?, Roles[], IsMfaRequired
│       ├── UserOperationResult.cs
│       └── IUserService.cs
│
├── MemByte.Infrastructure/
│   ├── Clients/
│   │   └── ClientService.cs           // EF sobre MemByteDbContext, mismo estilo que EmployeeService
│   └── Users/
│       └── UserService.cs             // Usa UserManager<ApplicationUser> + RoleManager<ApplicationRole>
│                                       // + MemByteDbContext (para Client/Employee/UserSession/AuthAuditEntry)
│
└── MemByte.Web/Components/Pages/
    ├── Clients/
    │   ├── ClientList.razor           // /clients
    │   ├── ClientCreate.razor         // /clients/new
    │   └── ClientDetail.razor         // /clients/{id} — datos + pestaña "Usuarios del cliente"
    └── Users/
        ├── UserList.razor             // /users — filtros: tipo (interno/cliente), rol, estado
        ├── UserCreate.razor           // /users/new
        └── UserDetail.razor           // /users/{id} — roles, vínculo Employee/Client, activar/
                                        // desactivar, resetear contraseña, revocar sesiones
```

`IUserService` (contrato propuesto, análogo a `IEmployeeService`):

```csharp
public interface IUserService
{
    Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken ct = default);
    Task<UserDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<UserOperationResult> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<UserOperationResult> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);

    Task<UserOperationResult> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default);
    Task<UserOperationResult> ResetPasswordAsync(Guid id, CancellationToken ct = default);
    Task<UserOperationResult> RevokeSessionsAsync(Guid id, CancellationToken ct = default);
}
```

---

## 5. Flujos de negocio

1. **Alta de usuario interno vinculado a un empleado.** Desde `/users/new` o desde la ficha de un `Employee` sin cuenta ("Crear acceso"), el Admin elige el `Employee`, asigna roles y guarda. No se fija contraseña manualmente: se genera un token de invitación (`UserManager.GeneratePasswordResetTokenAsync`) y se envía un enlace de "activar cuenta / fijar contraseña".
2. **Alta de usuario de cliente.** Desde `/clients/{id}` → "Añadir usuario", el formulario es el mismo que en (1) pero con `ClientId` preseleccionado y sin `EmployeeId`. También se puede vincular un usuario ya existente sin cliente asignado ("Vincular usuario existente").
3. **Alta de usuario "suelto".** Desde `/users/new` sin elegir `Employee` ni `Client` (p. ej. cuentas de servicio o roles especiales). Permitido, pero la UI lo marca claramente para evitar altas accidentales sin trazabilidad.
4. **Edición.** Cambiar roles, (des)vincular `Employee`/`Client`, marcar MFA obligatorio.
5. **Activar/desactivar.** No se borra el `ApplicationUser` en duro (rompería `AuthAuditEntries`/`UserSessions`/historial de contraseñas). Se implementa como bloqueo permanente: `LockoutEnabled = true` + `LockoutEnd = DateTimeOffset.MaxValue`, análogo a `Employee.Deactivate()`.
6. **Resetear contraseña (por Admin).** Nunca se teclea una contraseña en claro desde el panel: se genera un token de reseteo y se envía por email (o, mientras no haya envío de email real, se muestra al Admin un enlace de un solo uso para que lo transmita de forma segura — ver hueco de email en §10).
7. **Revocar sesiones activas.** Marca todas las `UserSession` del usuario como revocadas **y** llama a `UserManager.UpdateSecurityStampAsync(user)` para invalidar de inmediato la cookie de autenticación (necesario: hoy nada bump-ea el `SecurityStamp`).

---

## 6. Seguridad y auditoría

- Se reutiliza `AuthAuditEntry` (no se crea un sistema de auditoría paralelo). Nuevos `EventType`: `UserCreatedByAdmin`, `UserRoleChanged`, `UserDeactivated`, `UserReactivated`, `PasswordResetRequestedByAdmin`, `SessionsRevokedByAdmin`.
- Principio de mínimo privilegio: solo `Admin` gestiona usuarios y clientes (igual que hoy solo `Admin` puede crear/eliminar empleados).
- Ningún flujo de este módulo expone si un email existe o no fuera del contexto ya autenticado del Admin (se mantiene el criterio anti-enumeración usado en `AuthService`).
- Las contraseñas nunca se fijan por el Admin en texto plano; siempre vía token de un solo uso con expiración (los `TokenProvider` por defecto de Identity ya están registrados: `AddDefaultTokenProviders()`).

---

## 7. Permisos y actualización de agents.md

Este módulo lo gestiona exclusivamente `Admin` (permiso ya cubierto por `admin:*`). No se requiere una matriz nueva de momento porque no se introduce un rol nuevo. Cuando se implemente, `agents.md` deberá actualizarse con:

- Nueva subsección bajo **Admin → Módulo: Gestión de Usuarios**, describiendo alta/edición/activación/reseteo/revocación de sesiones y la gestión de la entidad `Client`.
- Nota en el roadmap (`ClientPortalUser` — Fase 2) indicando que ya existe la entidad `Client` como agrupador de usuarios, aunque el rol/portal específico del cliente sigue fuera de alcance hasta que se defina el catálogo de funcionalidades diferenciadas.

---

## 8. UI y rutas propuestas

| Ruta | Acceso | Descripción |
|---|---|---|
| `/users` | Admin | Listado con filtros (tipo interno/cliente, rol, estado, texto libre), igual patrón que `/employees`. |
| `/users/new` | Admin | Alta: email, nombre a mostrar, `Employee` opcional (buscador), `Client` opcional (buscador), roles. |
| `/users/{id}` | Admin | Detalle/edición: roles, vínculos, MFA, activar/desactivar, resetear contraseña, revocar sesiones, historial de auditoría del usuario. |
| `/clients` | Admin | Listado de clientes (nombre, nº de usuarios, estado). |
| `/clients/new` | Admin | Alta de cliente (nombre). |
| `/clients/{id}` | Admin | Detalle + pestaña "Usuarios" (añadir nuevo / vincular existente / desvincular). |

---

## 9. Migraciones EF necesarias

1. `Clients` (tabla nueva: `Id`, `Name`, `IsActive`, `CreatedAt`).
2. `Users.ClientId` (columna nullable + FK a `Clients.Id`, `OnDelete: Restrict` para forzar desvincular usuarios antes de desactivar/eliminar un cliente, + índice).
3. (Opcional, si se decide en Fase 2 de este módulo) columna `Users.InvitationTokenExpiresAt` o similar si el flujo de invitación necesita metadatos propios más allá de los tokens estándar de Identity.

---

## 10. Fases de implementación

| Fase | Contenido | Notas |
|---|---|---|
| **PR1** | Entidad `Client` + `IClientService`/`ClientService` + migración + UI `/clients` (CRUD básico, sin usuarios todavía). | Sin dependencias externas. |
| **PR2** | `ApplicationUser.ClientId` + migración + `IUserService`/`UserService` (listar, activar/desactivar, roles, revocar sesiones) + UI `/users` (listado y detalle, sin alta todavía). | Reutiliza usuarios ya creados por el seeder/Employees. |
| **PR3** | Alta de usuario (`/users/new`) con generación de token de invitación. | **Bloqueante real:** no existe `IEmailSender`. Se crea la interfaz en `Application` y una implementación *stub* (log/consola) en `Infrastructure` para no bloquear el desarrollo; el envío real (SMTP/SendGrid/Azure Communication Services) queda como tarea de despliegue, coherente con el runbook de Azure. |
| **PR4** | Auditoría ampliada (nuevos `EventType`) + tests unitarios (`Domain.Tests`, `Application.Tests`, `Infrastructure.Tests`) siguiendo la cobertura ya existente para Employees/Auth. | Cierra el KPI de cobertura ≥ 70 % también para este módulo. |
| **PR5** (posterior, fuera de este plan) | Envío real de emails + flujo self-service de "olvidé mi contraseña". | Requiere decidir proveedor de email (no cubierto aún en el runbook de Azure). |

---

## 11. Fuera de alcance

- La **aplicación frontal para usuarios de cliente** es un proyecto/repositorio aparte; este plan solo cubre su gestión (altas, roles, estado) desde la intranet, no su implementación.
- El **rol `ClientPortalUser`** y sus permisos específicos (Fase 2 en `agents.md`) no se crean todavía: la pertenencia a un `Client` es, por ahora, un atributo del usuario, no un rol de autorización nuevo.
- Las **"funcionalidades u opciones extra según la naturaleza del negocio del cliente"** mencionadas por negocio no se modelan aquí: se deja como extensión futura de la entidad `Client` (p. ej. `ClientType`/segmentación o feature flags) una vez se conozcan los casos concretos, para no sobre-diseñar sin requisitos claros.
- El módulo completo de **"Gestión de Proyectos y Clientes"** (tarifas, facturación, proyectos, horas) descrito en `agents.md` no se implementa aquí: `Client` es únicamente la base mínima (nombre + estado) que ese módulo futuro extenderá.
