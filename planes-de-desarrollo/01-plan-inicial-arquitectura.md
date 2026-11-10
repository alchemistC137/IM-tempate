# Plan 01 — Arquitectura Inicial de MemByte-IM

> **Estado:** Borrador · **Versión:** 1.0 · **Fecha:** 2026-08-01
>
> **Stack base:** .NET 10 LTS · C# 13 · Blazor · Entity Framework Core 10

---

## Índice

1. [Estrategia de Alojamiento de Blazor](#1-estrategia-de-alojamiento-de-blazor)
2. [Arquitectura de la Solución y Principios SOLID](#2-arquitectura-de-la-solución-y-principios-solid)
3. [Stack Tecnológico Detallado](#3-stack-tecnológico-detallado)
4. [Consideraciones de Seguridad en la Arquitectura](#4-consideraciones-de-seguridad-en-la-arquitectura)
5. [Consideraciones para los Módulos y Rentabilidad](#5-consideraciones-para-los-módulos-y-rentabilidad)
6. [Plan de Despliegue Inicial y Costes](#6-plan-de-despliegue-inicial-y-costes)
7. [Guía de 5 Pasos: Del Repositorio al Primer Despliegue](#7-guía-de-5-pasos-del-repositorio-al-primer-despliegue)

---

## 1. Estrategia de Alojamiento de Blazor

### Opciones Evaluadas

| Modelo | Ejecución | WebSocket | Sin JS | Funciona Offline | Recomendado para Intranet |
|--------|-----------|-----------|--------|-----------------|--------------------------|
| **Blazor Server** | Servidor | Sí (SignalR) | Sí | No | ✅ Parcialmente |
| **Blazor WebAssembly** | Cliente | No | No | Sí | ✅ Parcialmente |
| **Blazor SSR + Interactividad Selectiva** (.NET 8/10) | Híbrido | Opcional | Sí | Parcial | ✅✅ **Recomendado** |

### Decisión: Blazor con SSR y Modo de Renderizado Interactivo por Componente

En .NET 10 el modelo recomendado para aplicaciones empresariales es **Blazor Web App** con renderizado en servidor (SSR) por defecto y activación de interactividad (`InteractiveServer` o `InteractiveAuto`) sólo en los componentes que la requieren.

#### Justificación para una Intranet

**Conectividad garantizada.** Una intranet corporativa opera en una red local o VPN con latencia predecible. No existe la restricción de ancho de banda impredecible que penalizaría a Blazor Server en internet abierto. La conexión SignalR permanente es viable y aporta capacidades de tiempo real sin coste adicional.

**Seguridad mejorada.** Con SSR, el código de negocio y el acceso a la base de datos permanecen en el servidor. Ningún ensamblado de lógica sensible se envía al navegador, a diferencia de Blazor WebAssembly donde el IL es descargable y potencialmente inspeccionable por el cliente.

**Mantenimiento simplificado para equipos C#.** El equipo trabaja exclusivamente en C#; no es necesario mantener una API REST separada para alimentar el frontend. Todos los servicios se inyectan directamente en los componentes del servidor, reduciendo la complejidad del proyecto y el tiempo de desarrollo.

**SEO y First Contentful Paint (FCP).** SSR entrega HTML completo en la primera respuesta, lo cual es relevante para portales de empresa que puedan ser indexados internamente y mejora la percepción de velocidad en el primer acceso.

**Modelo de renderizado recomendado por componente:**

```csharp
// Páginas estáticas (directorio de empleados, reportes de lectura): SSR puro
@rendermode RenderMode.Static

// Formularios y dashboards con actualizaciones en tiempo real: Servidor interactivo
@rendermode InteractiveServer

// Componentes que pueden funcionar offline (carrito de compras): Auto (SSR + WASM fallback)
@rendermode InteractiveAuto
```

---

## 2. Arquitectura de la Solución y Principios SOLID

### Estructura de Proyectos (Clean Architecture)

```
MemByte-IM.sln
│
├── src/
│   ├── MemByte.Domain/              # Entidades, value objects, interfaces de repositorio, eventos de dominio
│   ├── MemByte.Application/         # Casos de uso, DTOs, interfaces de servicio, validaciones (FluentValidation)
│   ├── MemByte.Infrastructure/      # EF Core, repositorios concretos, servicios externos (correo, SignalR hub)
│   ├── MemByte.Web/                 # Proyecto Blazor Web App: componentes UI, páginas, layout, DI root
│   └── MemByte.Shared/              # Contratos compartidos entre capas (enums, constantes, extensiones)
│
├── tests/
│   ├── MemByte.Domain.Tests/        # Pruebas unitarias de entidades y reglas de negocio
│   ├── MemByte.Application.Tests/   # Pruebas unitarias de casos de uso con mocks
│   └── MemByte.Web.Tests/           # Pruebas de integración con bUnit (componentes Blazor)
│
├── .github/
│   └── workflows/
│       ├── ci.yml                   # Build, test, auditoría de paquetes, escaneo de secretos
│       └── cd.yml                   # Despliegue a Azure (staging → producción)
│
├── planes-de-desarrollo/            # Documentación de arquitectura y decisiones técnicas
└── agents.md                        # Definición de roles y permisos del sistema
```

### Dependencias entre Capas

```
Web  ──depends on──►  Application  ──depends on──►  Domain
Infrastructure  ──implements──►  Application (interfaces)
Web  ──depends on──►  Infrastructure (sólo en DI root, Program.cs)
```

Esta estructura garantiza que el dominio y la aplicación sean independientes de cualquier framework o tecnología específica, facilitando las pruebas unitarias y la sustitución de implementaciones.

---

### Aplicación de Principios SOLID

#### S — Single Responsibility Principle

Cada clase tiene una única razón para cambiar. La lógica de negocio no reside en los componentes de UI.

```csharp
// ❌ MAL: componente Blazor con lógica de negocio embebida
@code {
    private async Task CreateEmployee(EmployeeDto dto) {
        if (dto.Name.Length < 2) throw new Exception("Nombre inválido");
        await _db.Employees.AddAsync(new Employee { Name = dto.Name });
        await _db.SaveChangesAsync();
    }
}

// ✅ BIEN: el componente delega en el servicio de aplicación
@code {
    [Inject] IEmployeeService EmployeeService { get; set; } = default!;

    private async Task CreateEmployee(EmployeeDto dto) {
        var result = await EmployeeService.CreateAsync(dto);
        if (!result.IsSuccess) _errorMessage = result.Error;
    }
}
```

**Separación en `MemByte.Application`:**
- `IEmployeeService` — interfaz del caso de uso.
- `EmployeeService` — implementación con validación y orquestación.
- `IEmployeeRepository` — contrato de acceso a datos (implementado en Infrastructure).

### Módulo de Negocio: Gestión de Proyectos y Clientes

Este es el módulo central del propósito de MemByte-IM: permitir que MemByte gestione sus propios proyectos y soluciones digitales vendidas a clientes. Sigue exactamente el mismo patrón de capas que el resto de módulos:

```
src/
├── MemByte.Domain/
│   ├── Clients/            # Entidad Client, value objects (TaxId, ContactInfo)
│   ├── Projects/           # Entidades Project, Deliverable, ProjectStatus (enum)
│   └── Billing/            # Entidades TimeEntry, InvoiceDraft, RateCard
│
├── MemByte.Application/
│   ├── Clients/            # IClientService, ClientDto, validadores
│   ├── Projects/           # IProjectService, IDeliverableService
│   └── Billing/            # ITimeEntryService, IInvoiceGenerator, IReportExporter (reutilizado)
│
├── MemByte.Infrastructure/
│   ├── Clients/            # ClientRepository (EF Core)
│   ├── Projects/           # ProjectRepository, DeliverableRepository
│   └── Billing/            # TimeEntryRepository, InvoiceDraftGenerator
│
└── MemByte.Web/
    └── Modules/
        ├── Clients/        # Páginas y componentes Razor de clientes
        ├── Projects/       # Páginas y componentes Razor de proyectos
        └── Billing/        # Timesheet, borradores de factura
```

**Entidades de dominio clave:**
- `Client` — cliente contratante (razón social, datos fiscales, contactos).
- `Project` — proyecto o solución digital contratada, vinculado a un `Client`.
- `Deliverable` — entregable/hito de un `Project`, con fecha de cierre.
- `TimeEntry` — registro de horas imputadas por un empleado a un `Project`.
- `InvoiceDraft` — borrador de facturación generado a partir de `TimeEntry` y `Deliverable` cerrados.

#### O — Open/Closed Principle

El sistema está abierto a extensión y cerrado a modificación. Se añaden nuevos módulos creando nuevas implementaciones de interfaces existentes, sin modificar el código base.

```csharp
// Interfaz base para cualquier módulo de notificación
public interface INotificationChannel
{
    Task SendAsync(Notification notification, CancellationToken ct = default);
}

// Implementaciones independientes; añadir una nueva no modifica las existentes
public class EmailNotificationChannel : INotificationChannel { ... }
public class SignalRNotificationChannel : INotificationChannel { ... }
// En el futuro: SlackNotificationChannel, TeamsNotificationChannel, etc.
```

**Registro en DI:**
```csharp
builder.Services.AddScoped<INotificationChannel, EmailNotificationChannel>();
builder.Services.AddScoped<INotificationChannel, SignalRNotificationChannel>();
```

#### L — Liskov Substitution Principle

Las clases derivadas pueden sustituir a sus bases sin alterar el comportamiento esperado del sistema. Las interfaces se diseñan con contratos claros.

```csharp
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

// EmployeeRepository puede sustituir a cualquier IRepository<Employee>
// sin sorpresas en tiempo de ejecución
public class EmployeeRepository : IRepository<Employee> { ... }
```

#### I — Interface Segregation Principle

Interfaces pequeñas y específicas. Ningún cliente depende de métodos que no utiliza.

```csharp
// ❌ MAL: interfaz demasiado amplia
public interface IEmployeeService
{
    Task<Employee> GetByIdAsync(Guid id);
    Task CreateAsync(CreateEmployeeDto dto);
    Task UpdateAsync(UpdateEmployeeDto dto);
    Task DeleteAsync(Guid id);
    Task<OrgChartNode> GetOrgChartAsync();
    Task<byte[]> ExportToCsvAsync();
    Task SendWelcomeEmailAsync(Guid employeeId);
}

// ✅ BIEN: interfaces segregadas por responsabilidad
public interface IEmployeeReader
{
    Task<EmployeeDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<EmployeeDto>> GetPagedAsync(EmployeeFilter filter, CancellationToken ct = default);
}

public interface IEmployeeWriter
{
    Task<Result<Guid>> CreateAsync(CreateEmployeeCommand command, CancellationToken ct = default);
    Task<Result> UpdateAsync(UpdateEmployeeCommand command, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IOrgChartService
{
    Task<OrgChartNode> GetOrgChartAsync(CancellationToken ct = default);
}

public interface IEmployeeExportService
{
    Task<byte[]> ExportToCsvAsync(EmployeeFilter filter, CancellationToken ct = default);
}
```

#### D — Dependency Inversion Principle

Los módulos de alto nivel (Application) no dependen de los de bajo nivel (Infrastructure), sino de abstracciones definidas en el contrato.

```csharp
// Application define el contrato (MemByte.Application)
public interface IEmployeeRepository
{
    Task<Employee?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<Employee>> GetPagedAsync(EmployeeFilter filter, CancellationToken ct = default);
    Task AddAsync(Employee employee, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

// Infrastructure implementa el contrato (MemByte.Infrastructure)
// Application no tiene referencia a Infrastructure
internal sealed class EmployeeRepository(AppDbContext db) : IEmployeeRepository
{
    public async Task<Employee?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await db.Employees.FindAsync([id], ct);

    public async Task<PagedResult<Employee>> GetPagedAsync(EmployeeFilter filter, CancellationToken ct) =>
        await db.Employees
            .Where(e => !e.IsDeleted)
            .AsNoTracking()
            .ToPagedResultAsync(filter, ct);

    public async Task AddAsync(Employee employee, CancellationToken ct) =>
        await db.Employees.AddAsync(employee, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        db.SaveChangesAsync(ct);
}

// DI Root en Web (Program.cs) es el único lugar que conoce la implementación concreta
builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
```

---

## 3. Stack Tecnológico Detallado

### Núcleo

| Componente | Tecnología | Versión | Justificación |
|---|---|---|---|
| Runtime | .NET LTS | 10 | Soporte hasta noviembre 2028; estabilidad máxima para producto comercial. |
| Lenguaje | C# | 13 | Características modernas: primary constructors, collection expressions, pattern matching avanzado. |
| IDE | Visual Studio | 2026 / VS Code | Tooling oficial; hot reload, debugger, integración con Azure. |
| UI Framework | Blazor Web App | .NET 10 | SSR + interactividad selectiva; sin duplicar el modelo mental del equipo C#. |
| ORM | Entity Framework Core | 10 | Migraciones, LINQ nativo, integración con ASP.NET Core Identity; soporte de primera clase en .NET 10. |

### Base de Datos

| Opción | Coste (arranque) | Coste (escala) | Pros | Contras | Recomendación |
|---|---|---|---|---|---|
| **Azure SQL Serverless** | ~$0 (pausa automática) | ~$15–$50/mes según vCores | Gestión cero, escalado automático, integración con Entra ID, Managed Identity | Latencia en cold start (~5 s) | ✅ Producción SaaS |
| **Supabase (PostgreSQL)** | $0 (plan free) | $25/mes (Pro) | Cero configuración, API REST + Realtime out-of-the-box, coste reducido en arranque | Menor integración con Azure ecosystem, no soporta Managed Identity de Azure directamente | ✅ MVP / Prototipo |
| **SQL Server on Azure VM** | ~$50/mes (B2s) | Alto | Control total | Responsabilidad de parches, backups, HA | ❌ Descartado para arranque |

**Decisión por fase:**
- **Fase 0 (MVP):** Supabase con PostgreSQL para minimizar costes; EF Core con proveedor Npgsql.
- **Fase 1 (Producción SaaS):** Migrar a Azure SQL Serverless con Managed Identity.

### Autenticación y Autorización

```
ASP.NET Core Identity  ──  gestión local de usuarios, hashing Argon2/PBKDF2
        │
        └──►  OpenID Connect / OAuth 2.0
                    │
                    └──►  Microsoft Entra ID (Azure AD)
                               • Credenciales corporativas SSO
                               • MFA obligatorio para Admin y AreaManager
                               • Conditional Access Policies
```

**Implementación RBAC:**
```csharp
// Políticas basadas en claims para granularidad fina
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanApproveTeamPurchases", policy =>
        policy.RequireClaim("permission", "purchase:approve:team"));

    options.AddPolicy("CanManageCatalog", policy =>
        policy.RequireClaim("permission", "catalog:manage"));
});

// Uso en componentes Blazor
<AuthorizeView Policy="CanApproveTeamPurchases">
    <Authorized>
        <ApprovalPanel />
    </Authorized>
</AuthorizeView>
```

### Librería de Componentes UI

**Recomendación: MudBlazor**

| Criterio | MudBlazor | Radzen | Syncfusion |
|---|---|---|---|
| Licencia | MIT (gratuito) | MIT (gratuito) | Comercial (community edition limitada) |
| Comunidad | ⭐⭐⭐⭐⭐ Muy activa | ⭐⭐⭐ Activa | ⭐⭐⭐ Activa |
| Componentes Data Grid | Avanzado, virtualización nativa | Avanzado | Avanzado (mejor en clase) |
| Tema Material Design | ✅ Nativo | ✅ Parcial | ✅ Personalizable |
| Integración Blazor SSR | ✅ Completa (.NET 8/10) | ✅ Completa | ✅ Completa |
| Coste para multi-cliente | $0 | $0 | Alto si se supera el limite de community |

MudBlazor es la elección óptima para un producto multi-cliente: licencia MIT, documentación exhaustiva, virtualización de listas (crítico para Blazor Server), y el mayor ecosistema de la comunidad Blazor.

### Manejo de Estado y Tiempo Real

**SignalR** para notificaciones en tiempo real (mensajería interna, alertas de aprobación de compras):

```csharp
// Hub en Infrastructure
public class NotificationHub : Hub
{
    public async Task JoinUserGroup(string userId) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
}

// Servicio en Application
public interface IRealtimeNotificationService
{
    Task NotifyUserAsync(string userId, NotificationDto notification, CancellationToken ct = default);
}
```

**Fluxor** para manejo de estado complejo en componentes con interactividad (`InteractiveServer`/`InteractiveAuto`):
- Se utiliza en el módulo de Carrito/Compras donde el estado persiste a lo largo de múltiples pasos de un flujo de trabajo.
- Patrón: `Feature → State → Action → Reducer → Effect`.
- Evita prop drilling en jerarquías profundas de componentes Blazor.

---

## 4. Consideraciones de Seguridad en la Arquitectura

### 4.1 Gestión de Secretos y Configuración

**Regla:** Ninguna contraseña, connection string ni API key se almacenará en `appsettings.json` o en el código fuente.

```csharp
// ❌ PROHIBIDO en cualquier entorno
{
  "ConnectionStrings": {
    "Default": "Server=myserver;Database=mydb;******"
  }
}

// ✅ Desarrollo: dotnet user-secrets
// dotnet user-secrets init
// dotnet user-secrets set "ConnectionStrings:Default" "Server=...;******"
// Almacenado en %APPDATA%\Microsoft\UserSecrets\<id>\secrets.json (fuera del repo)

// ✅ Producción: Azure Key Vault + Managed Identity (sin contraseñas en config)
builder.Configuration.AddAzureKeyVault(
    new Uri($"https://{builder.Configuration["KeyVaultName"]}.vault.azure.net/"),
    new DefaultAzureCredential()); // Managed Identity en Azure; az login en local
```

**`appsettings.json` sólo contendrá referencias no sensibles:**
```json
{
  "KeyVaultName": "membyte-kv-prod",
  "AzureAd": {
    "TenantId": "00000000-0000-0000-0000-000000000000",
    "ClientId": "00000000-0000-0000-0000-000000000000"
  },
  "Logging": { "LogLevel": { "Default": "Information" } }
}
```

### 4.2 Auditoría de Paquetes NuGet en CI/CD

```xml
<!-- Directory.Build.props — aplicado a todos los proyectos de la solución -->
<Project>
  <PropertyGroup>
    <!-- Tratar advertencias de vulnerabilidades como errores en CI -->
    <NuGetAudit>true</NuGetAudit>
    <NuGetAuditLevel>moderate</NuGetAuditLevel>
    <NuGetAuditMode>all</NuGetAuditMode>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

```yaml
# En el workflow de CI (.github/workflows/ci.yml)
- name: Audit NuGet packages for vulnerabilities
  run: dotnet list package --vulnerable --include-transitive
  working-directory: src/
```

### 4.3 Prevención de Vulnerabilidades Comunes

**Serialización segura:**
```csharp
// ✅ System.Text.Json por defecto en toda la aplicación
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    // TypeNameHandling desactivado por defecto en System.Text.Json
});

// ❌ PROHIBIDO: Newtonsoft.Json con TypeNameHandling
// JsonConvert.DeserializeObject(json, new JsonSerializerSettings {
//     TypeNameHandling = TypeNameHandling.All // VULNERABILIDAD
// });

// ❌ PROHIBIDO: BinaryFormatter (eliminado en .NET 9+)
```

**Anti-patrones de Blazor Server mitigados:**
```csharp
// Paginación obligatoria: nunca cargar colecciones ilimitadas
public async Task<PagedResult<EmployeeDto>> GetEmployeesAsync(
    int page = 1,
    int pageSize = 25, // máximo 100
    CancellationToken ct = default)
{
    pageSize = Math.Clamp(pageSize, 1, 100);
    // ...
}

// Virtualización de listas largas con MudBlazor
<MudDataGrid T="EmployeeDto"
             ServerData="LoadEmployees"
             Virtualize="true"
             RowsPerPage="25" />
```

### 4.4 Hardening de ASP.NET Core

```csharp
// Program.cs — configuración de seguridad en orden recomendado

var app = builder.Build();

// 1. Manejo seguro de errores (nunca exponer stack traces en producción)
if (app.Environment.IsProduction())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts(); // HTTP Strict Transport Security
}

// 2. HTTPS obligatorio
app.UseHttpsRedirection();

// 3. Content Security Policy y cabeceras de seguridad
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline'; " + // requerido por Blazor
        "style-src 'self' 'unsafe-inline'; " +
        "connect-src 'self' wss:;"); // WebSocket para SignalR
    await next();
});

// 4. Limitación de tasa
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("api", limiter =>
    {
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.PermitLimit = 100;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiter.QueueLimit = 10;
    });
});
app.UseRateLimiter();

// 5. Autenticación → Autorización (orden obligatorio)
app.UseAuthentication();
app.UseAuthorization();

// 6. Protección antifalsificación (CSRF) — activo por defecto en .NET 8+
app.UseAntiforgery();
```

### 4.5 Análisis de Secretos en CI

```yaml
# .github/workflows/ci.yml
- name: Scan for secrets
  uses: trufflesecurity/trufflehog@main
  with:
    path: ./
    base: ${{ github.event.repository.default_branch }}
    head: HEAD
    extra_args: --only-verified
```

---

## 5. Consideraciones para los Módulos y Rentabilidad

### Navegación y Diseño de Layout

```
AppLayout (MainLayout.razor)
├── NavMenu.razor          — menú lateral contextual según rol
│   ├── /employees         — Gestión de Empleados (Admin, AreaManager)
│   ├── /messages          — Mensajería Interna (todos)
│   ├── /purchases         — Gestión de Compras (todos, vistas distintas por rol)
│   ├── /clients           — Gestión de Clientes (Admin)
│   ├── /projects          — Gestión de Proyectos (todos, vistas distintas por rol)
│   ├── /timesheet         — Registro de Horas (todos, aprobación por AreaManager/Admin)
│   ├── /billing           — Facturación y borradores de factura (Admin)
│   └── /admin             — Panel Admin (sólo Admin)
└── NotificationPanel.razor — notificaciones SignalR en tiempo real
```

**Menú dinámico según rol (sin hardcodear rutas):**
```csharp
// NavMenu.razor.cs
public partial class NavMenu
{
    [CascadingParameter] Task<AuthenticationState> AuthStateTask { get; set; } = default!;

    private bool CanSeeEmployeeManagement;
    private bool CanSeePurchaseApprovals;

    protected override async Task OnInitializedAsync()
    {
        var auth = await AuthStateTask;
        CanSeeEmployeeManagement = auth.User.HasClaim("permission", "employee:read:team") ||
                                   auth.User.IsInRole("Admin");
        CanSeePurchaseApprovals  = auth.User.HasClaim("permission", "purchase:approve:team");
    }
}
```

### Gestión de Sesiones y Seguridad

```csharp
builder.Services.AddAuthentication(options => { ... })
    .AddCookie(options =>
    {
        options.ExpireTimeSpan = TimeSpan.FromHours(8);      // jornada laboral
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;                       // inaccesible a JS
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
    });

// Invalidación de sesión ante cambio de contraseña o revocación de rol
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.FromMinutes(30);
});
```

### Manejo Eficiente de Datos

Estrategia anti-agotamiento de recursos en Blazor Server:

| Técnica | Dónde aplicar | Implementación |
|---|---|---|
| **Paginación servidor** | Todas las listas | `Skip/Take` en EF Core; `PagedResult<T>` genérico |
| **Virtualización** | Listas > 50 items | `MudDataGrid Virtualize="true"` |
| **Cancelación** | Todas las llamadas async | `CancellationToken` en cada método de repositorio |
| **AsNoTracking** | Consultas de sólo lectura | `dbSet.AsNoTracking().ToListAsync(ct)` |
| **Proyección a DTO** | Siempre en queries de lectura | `.Select(e => new EmployeeDto { ... })` evita cargar campos innecesarios |
| **Circuit Breaker** | Llamadas externas (correo, SSO) | `Polly` con `AddResiliencePipeline` |

**Consideración específica para el registro de horas (timesheets):** dado que `TimeEntry` puede crecer a un ritmo alto (varias filas por empleado y día), las consultas de listado deben filtrarse siempre por proyecto, empleado y rango de fechas antes de paginar, y las sumas de horas (por proyecto, por empleado, por periodo) deben calcularse con `SUM()` a nivel de base de datos (`GroupBy` + `SumAsync` en EF Core) en lugar de materializar todas las filas en memoria.

### Generación de Reportes y Exportación

```csharp
public interface IReportExporter
{
    Task<byte[]> ExportAsync<T>(IEnumerable<T> data, ExportFormat format, CancellationToken ct = default);
}

public enum ExportFormat { Csv, Excel, Pdf }

// Implementaciones intercambiables (OCP):
// CsvReportExporter → CsvHelper (MIT)
// ExcelReportExporter → ClosedXML (MIT)
// PdfReportExporter → QuestPDF (community, MIT para < $1M ingresos)
```

**Generación de borradores de factura:** siguiendo el mismo patrón OCP que `IReportExporter`, se define una interfaz específica para desacoplar la generación del borrador de facturación de cualquier proveedor de facturación electrónica concreto, dejando la puerta abierta a integrar un proveedor fiscal local en cada país donde MemByte opere:

```csharp
public interface IInvoiceGenerator
{
    Task<InvoiceDraft> GenerateDraftAsync(Guid projectId, DateRange period, CancellationToken ct = default);
}

// Implementación inicial: agrega TimeEntry aprobadas + Deliverable cerrados del periodo
// Implementaciones futuras: adaptadores hacia proveedores de facturación electrónica por país
```

### Escalabilidad de Módulos (SOLID en la práctica)

Añadir un nuevo módulo (e.g., **Gestión de Vacaciones**) requiere:

1. **Domain:** nueva entidad `LeaveRequest` en `MemByte.Domain/Leaves/`.
2. **Application:** nuevas interfaces `ILeaveRequestService`, `ILeaveRequestRepository` + comandos/consultas.
3. **Infrastructure:** implementación `LeaveRequestRepository` + migración de EF Core.
4. **Web:** nuevos componentes Razor en `MemByte.Web/Modules/Leaves/`.
5. **Registro en DI:** líneas de `AddScoped` en `Program.cs`.

**El código existente de otros módulos no se modifica.** Esto es OCP en acción.

---

## 6. Plan de Despliegue Inicial y Costes

### Infraestructura Recomendada en Azure

| Recurso | SKU | Coste/mes aprox. | Notas |
|---|---|---|---|
| **App Service (Linux)** | B1 (1 vCore, 1.75 GB RAM) | ~$13 | Linux ~35 % más barato que Windows; ideal para arranque. Escalar a B2/B3 según carga. |
| **Azure SQL Database** | Basic (5 DTU) o Serverless (0.5–2 vCores) | $5–$15 | Basic para MVP; Serverless para producción con autopausa en horas sin uso. |
| **Azure Key Vault** | Standard | ~$0.03/10 000 operaciones | Coste prácticamente nulo en desarrollo. |
| **Azure Application Insights** | Pay-as-you-go | ~$0–5 | Primeros 5 GB/mes gratuitos. |
| **Azure AD (Entra ID)** | Free tier | $0 | Hasta 50 000 MAU gratuitos; P1 a $6/usuario/mes si se requiere Conditional Access. |
| **GitHub Actions** | Free (public) / $0.008/min (private) | ~$0–10 | Repositorios privados: 2 000 min/mes gratuitos. |
| **Total estimado (arranque)** | | **~$28–$43/mes** | Sin contar Azure AD P1 ni Supabase |

**Alternativa con Supabase (base de datos):**

| Recurso | Plan | Coste/mes |
|---|---|---|
| Supabase (PostgreSQL + Auth + Realtime) | Free / Pro | $0 / $25 |
| App Service Linux B1 | — | ~$13 |
| **Total** | | **$13–$38/mes** |

Supabase incluye autenticación propia, API REST, y canal Realtime (equivalente a SignalR simplificado), lo que puede eliminar la necesidad de Azure SignalR Service (~$49/mes en plan Standard) en la fase inicial.

### Pipeline de CI/CD con GitHub Actions

```yaml
# .github/workflows/ci.yml
name: CI — Build, Test & Security Audit

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 10
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore dependencies
        run: dotnet restore

      - name: Audit NuGet packages
        run: dotnet list package --vulnerable --include-transitive
        # Falla el build si hay vulnerabilidades (NuGetAudit=true en Directory.Build.props)

      - name: Build (treat warnings as errors)
        run: dotnet build --no-restore --configuration Release

      - name: Run tests with coverage
        run: dotnet test --no-build --configuration Release --collect:"XPlat Code Coverage"

      - name: Upload coverage to Codecov
        uses: codecov/codecov-action@v4

      - name: Scan for hardcoded secrets
        uses: trufflesecurity/trufflehog@main
        with:
          path: ./
          base: ${{ github.event.repository.default_branch }}
          head: HEAD
          extra_args: --only-verified

# .github/workflows/cd.yml
name: CD — Deploy to Azure

on:
  push:
    branches: [ main ]

jobs:
  deploy:
    runs-on: ubuntu-latest
    environment: production
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 10
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Publish
        run: dotnet publish src/MemByte.Web/MemByte.Web.csproj -c Release -o ./publish

      - name: Deploy to Azure Web App
        uses: azure/webapps-deploy@v3
        with:
          app-name: ${{ secrets.AZURE_APP_NAME }}
          publish-profile: ${{ secrets.AZURE_PUBLISH_PROFILE }}
          package: ./publish
```

**Controles de seguridad en el pipeline:**

| Control | Herramienta | Fase |
|---|---|---|
| Vulnerabilidades en dependencias | `dotnet list package --vulnerable` + NuGetAudit | Build |
| Secretos en código fuente | TruffleHog | Build |
| Calidad de código | `dotnet build --warnaserror` | Build |
| Cobertura de pruebas | Coverlet + Codecov | Test |
| Análisis estático de seguridad | GitHub CodeQL (opcional, plan gratuito) | PR |
| Revisión de imagen Docker (futuro) | Trivy | Build |

---

## 7. Guía de 5 Pasos: Del Repositorio al Primer Despliegue

### Paso 1 — Crear el Repositorio y la Solución

```bash
# Crear la solución .NET
mkdir MemByte-IM && cd MemByte-IM
dotnet new sln -n MemByte-IM

# Crear los proyectos de la arquitectura Clean
dotnet new classlib -n MemByte.Domain         -o src/MemByte.Domain         --framework net10.0
dotnet new classlib -n MemByte.Application    -o src/MemByte.Application    --framework net10.0
dotnet new classlib -n MemByte.Infrastructure -o src/MemByte.Infrastructure  --framework net10.0
dotnet new blazor   -n MemByte.Web            -o src/MemByte.Web             --framework net10.0 --interactivity Server
dotnet new classlib -n MemByte.Shared         -o src/MemByte.Shared          --framework net10.0

# Crear proyectos de test
dotnet new xunit -n MemByte.Domain.Tests      -o tests/MemByte.Domain.Tests      --framework net10.0
dotnet new xunit -n MemByte.Application.Tests -o tests/MemByte.Application.Tests --framework net10.0

# Agregar proyectos a la solución
dotnet sln add src/MemByte.Domain/MemByte.Domain.csproj
dotnet sln add src/MemByte.Application/MemByte.Application.csproj
dotnet sln add src/MemByte.Infrastructure/MemByte.Infrastructure.csproj
dotnet sln add src/MemByte.Web/MemByte.Web.csproj
dotnet sln add src/MemByte.Shared/MemByte.Shared.csproj
dotnet sln add tests/MemByte.Domain.Tests/MemByte.Domain.Tests.csproj
dotnet sln add tests/MemByte.Application.Tests/MemByte.Application.Tests.csproj

# Configurar referencias entre proyectos
dotnet add src/MemByte.Application/MemByte.Application.csproj reference src/MemByte.Domain/MemByte.Domain.csproj
dotnet add src/MemByte.Infrastructure/MemByte.Infrastructure.csproj reference src/MemByte.Application/MemByte.Application.csproj
dotnet add src/MemByte.Web/MemByte.Web.csproj reference src/MemByte.Application/MemByte.Application.csproj
dotnet add src/MemByte.Web/MemByte.Web.csproj reference src/MemByte.Infrastructure/MemByte.Infrastructure.csproj
dotnet add src/MemByte.Web/MemByte.Web.csproj reference src/MemByte.Shared/MemByte.Shared.csproj

# Crear Directory.Build.props (configuración global)
cat > Directory.Build.props << 'EOF'
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <NuGetAudit>true</NuGetAudit>
    <NuGetAuditLevel>moderate</NuGetAuditLevel>
    <NuGetAuditMode>all</NuGetAuditMode>
  </PropertyGroup>
</Project>
EOF

git init && git add . && git commit -m "feat: initial solution structure"
```

### Paso 2 — Configurar Secretos en Desarrollo

```bash
# Inicializar user-secrets para el proyecto Web (una sola vez)
cd src/MemByte.Web
dotnet user-secrets init

# Configurar la cadena de conexión (NUNCA en appsettings.json)
dotnet user-secrets set "ConnectionStrings:Default" \
  "Host=localhost;Database=membyte_dev;Username=postgres;******"

# Verificar que los secretos están configurados
dotnet user-secrets list

# Instalar paquetes principales
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL  # o SqlServer si se usa Azure SQL
dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore
dotnet add package MudBlazor
dotnet add package Azure.Extensions.AspNetCore.Configuration.Secrets  # para Key Vault en producción
dotnet add package Azure.Identity
```

### Paso 3 — Crear el Modelo de Dominio y la Primera Migración

```bash
cd /ruta/a/MemByte-IM

# Crear la primera entidad (Employee) y el DbContext en Infrastructure
# (ver código de ejemplo en la sección de arquitectura)

# Crear y aplicar la primera migración
dotnet ef migrations add InitialCreate \
  --project src/MemByte.Infrastructure \
  --startup-project src/MemByte.Web \
  --output-dir Persistence/Migrations

dotnet ef database update \
  --project src/MemByte.Infrastructure \
  --startup-project src/MemByte.Web

# Verificar que todo compila y los tests pasan
dotnet build
dotnet test
```

### Paso 4 — Configurar el Pipeline de CI/CD

```bash
# Crear el directorio de workflows
mkdir -p .github/workflows

# Crear ci.yml y cd.yml con el contenido del Paso 6 de este documento
# (ver sección "Pipeline de CI/CD con GitHub Actions")

# Configurar los secretos en GitHub:
# Ir a Settings → Secrets and variables → Actions → New repository secret
# - AZURE_APP_NAME: nombre de tu Azure Web App
# - AZURE_PUBLISH_PROFILE: contenido del perfil de publicación de Azure

git add .github/
git commit -m "ci: add GitHub Actions CI/CD pipeline with security controls"
git push origin main
```

### Paso 5 — Crear el Entorno en Azure y Hacer el Primer Despliegue

```bash
# Instalar Azure CLI si no está disponible
# https://docs.microsoft.com/es-es/cli/azure/install-azure-cli

# Autenticarse en Azure
az login

# Variables del proyecto
RESOURCE_GROUP="rg-membyte-dev"
LOCATION="eastus"
APP_SERVICE_PLAN="plan-membyte-dev"
WEB_APP_NAME="membyte-im-dev"
SQL_SERVER="membyte-sql-dev"
SQL_DB="membyte-db-dev"
KEY_VAULT="membyte-kv-dev"

# Crear grupo de recursos
az group create --name $RESOURCE_GROUP --location $LOCATION

# Crear App Service Plan (Linux B1)
az appservice plan create \
  --name $APP_SERVICE_PLAN \
  --resource-group $RESOURCE_GROUP \
  --sku B1 \
  --is-linux

# Crear Web App con .NET 10
az webapp create \
  --name $WEB_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --plan $APP_SERVICE_PLAN \
  --runtime "DOTNETCORE:10.0"

# Crear Azure SQL Server y base de datos
az sql server create \
  --name $SQL_SERVER \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --admin-user membyte_admin \
  --admin-password "$(openssl rand -base64 32)"

az sql db create \
  --name $SQL_DB \
  --server $SQL_SERVER \
  --resource-group $RESOURCE_GROUP \
  --edition Basic  # Escalar a GeneralPurpose Serverless en producción

# Crear Key Vault
az keyvault create \
  --name $KEY_VAULT \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION

# Activar Managed Identity en Web App
az webapp identity assign \
  --name $WEB_APP_NAME \
  --resource-group $RESOURCE_GROUP

# Dar acceso a Key Vault a la Managed Identity
PRINCIPAL_ID=$(az webapp identity show \
  --name $WEB_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --query principalId -o tsv)

az keyvault set-policy \
  --name $KEY_VAULT \
  --object-id $PRINCIPAL_ID \
  --secret-permissions get list

# Almacenar connection string en Key Vault (nunca en variables de entorno de App Service)
az keyvault secret set \
  --vault-name $KEY_VAULT \
  --name "ConnectionStrings--Default" \
  --value "Server=$SQL_SERVER.database.windows.net;Database=$SQL_DB;..."

# Configurar el nombre del Key Vault como variable de entorno (no sensible)
az webapp config appsettings set \
  --name $WEB_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --settings KeyVaultName=$KEY_VAULT

# El pipeline de CD (cd.yml) hará el despliegue automáticamente
# al hacer push a main. Para un despliegue manual inicial:
dotnet publish src/MemByte.Web/MemByte.Web.csproj -c Release -o ./publish
az webapp deploy \
  --name $WEB_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --src-path ./publish \
  --type zip

echo "✅ MemByte-IM desplegado en: https://$WEB_APP_NAME.azurewebsites.net"
```

---

## Fuera de Alcance / Próximas Fases

> Estas capacidades han sido identificadas durante el diseño del módulo de Gestión de Proyectos y Clientes, pero quedan **explícitamente fuera del alcance de este Plan 01**. Se documentan para preservar la decisión y evitar retrabajo al abordarlas en un Plan 02.

| Capacidad | Motivo de exclusión | Momento previsto |
|---|---|---|
| **Portal de Cliente externo** (`ClientPortalUser`) | Requiere un modelo de autenticación e identidad distinto (cuentas externas, aislamiento de datos por cliente); se prioriza primero consolidar el uso interno del módulo. | Plan 02 — Fase 2 |
| **Integración con proveedor de facturación electrónica** | La generación de `InvoiceDraft` cubre el borrador interno; la emisión fiscal formal varía por país/normativa y requiere elegir proveedor por mercado. | Plan 02, tras validar el flujo interno de facturación |
| **Multi-tenancy real (aislamiento de datos por cliente instalado)** | El arranque prioriza una única instalación gestionada por MemByte; el aislamiento estricto por tenant se evaluará cuando exista más de un cliente con instalación propia. | Evaluación en Plan 03, según demanda comercial |

La interfaz `IInvoiceGenerator` (sección 5) se diseña ya pensando en esta extensión futura, de modo que añadir un proveedor de facturación electrónica no requiera modificar el código existente (OCP).

---

## Resumen de Decisiones Arquitectónicas

| Decisión | Elección | Alternativa Considerada | Razón Principal |
|---|---|---|---|
| Modelo de renderizado Blazor | SSR + InteractiveServer selectivo | Blazor Server puro / WASM | Balance óptimo: seguridad + rendimiento + mantenibilidad |
| Arquitectura | Clean Architecture | Vertical Slice | Mayor separación de responsabilidades; mejor para equipos C# con experiencia en capas |
| Base de datos (arranque) | Supabase / Azure SQL Serverless | SQL Server on VM | Mínimo coste operativo; escalabilidad automática |
| UI Components | MudBlazor | Radzen / Syncfusion | Licencia MIT; mayor comunidad; virtualización nativa |
| Autenticación | ASP.NET Core Identity + Entra ID | Auth0 / Okta | Integración nativa con .NET; sin dependencia de terceros de pago obligatorio |
| Serialización | System.Text.Json | Newtonsoft.Json | Más seguro por defecto; mayor rendimiento; integrado en .NET |
| Gestión de secretos | Azure Key Vault + Managed Identity | Variables de entorno / app settings | Elimina el riesgo de exposición de credenciales |
| Estado en tiempo real | SignalR + Fluxor | Redux pattern / Akka.NET | SignalR nativo en .NET; Fluxor específico para Blazor |
| Gestión de Proyectos y Clientes | Módulo propio siguiendo el mismo patrón Clean Architecture/OCP que el resto | Herramienta externa de CRM/PM | Evita duplicar autenticación e integraciones; un único núcleo operativo para MemByte |
