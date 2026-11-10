using MemByte.Application.Auth;
using MemByte.Application.Catalog;
using MemByte.Application.Orders;
using MemByte.Application.Clients;
using MemByte.Application.Employees;
using MemByte.Application.Messaging;
using MemByte.Application.Notifications;
using MemByte.Application.Users;
using MemByte.Infrastructure.Audit;
using MemByte.Infrastructure.Auth;
using MemByte.Infrastructure.Catalog;
using MemByte.Infrastructure.Orders;
using MemByte.Infrastructure.Clients;
using MemByte.Infrastructure.Employees;
using MemByte.Infrastructure.Identity;
using MemByte.Infrastructure.Identity.Seeding;
using MemByte.Infrastructure.Messaging;
using MemByte.Infrastructure.Notifications;
using MemByte.Infrastructure.Persistence;
using MemByte.Infrastructure.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MemByte.Web.Components;
using MemByte.Web.Messaging;
using Microsoft.AspNetCore.DataProtection;
using MudBlazor.Services;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);


// Data Protection: en producción usar /home, que sí es escribible en App Service Linux.
// En local, seguir usando ContentRootPath para que las claves queden dentro del proyecto.
var dataProtectionKeysPath = builder.Environment.IsProduction()
	? "/home/.dataprotection-keys"
	: Path.Combine(builder.Environment.ContentRootPath, ".dataprotection-keys");

// Persiste las claves de Data Protection (usadas para cifrar cookies,
// tokens antiforgery, etc.) en disco, en vez de mantenerlas solo en memoria.
// Sin esto, cada reinicio del proceso (ej. al depurar) genera un juego de
// claves nuevo, invalidando cualquier token antiforgery embebido en una
// página del navegador que no se haya recargado, lo que provoca el error
// "A valid antiforgery token was not provided with the request." En Azure
// con más de una instancia, además, hace falta compartir estas claves entre
// instancias (ej. Azure Blob Storage) para evitar el mismo problema.
builder.Services.AddDataProtection()
	.SetApplicationName("MemByte-IM")
	.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));

// Add services to the container.
builder.Services.AddRazorComponents()
	.AddInteractiveServerComponents();

builder.Services.AddMudServices();
builder.Services.AddHealthChecks();

// Application Insights: se registra solo si hay una cadena de conexión
// configurada. El SDK actual (basado en OpenTelemetry + Azure Monitor
// Exporter) falla en el arranque si se le pasa una cadena nula/vacía, así
// que en dev local/tests sin telemetría configurada simplemente se omite el
// registro en vez de dejar que Hosting falle. Se lee de
// "ApplicationInsights:ConnectionString" o de la variable de entorno
// estándar APPLICATIONINSIGHTS_CONNECTION_STRING (esta última tiene
// prioridad automáticamente dentro del SDK).
var appInsightsConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
{
	builder.Services.AddApplicationInsightsTelemetry(options =>
	{
		options.ConnectionString = appInsightsConnectionString;
	});
}

// Publica cada evento de auditoría (login, MFA, bloqueos) como log
// estructurado; cuando Application Insights está configurado, estos logs
// llegan como "traces" consultables por KQL para definir alertas de
// anomalías (ver runbook de despliegue, sección "Telemetría y alertas").
builder.Services.AddSingleton<ISecurityTelemetryPublisher, SecurityTelemetryPublisher>();

builder.Services.AddCascadingAuthenticationState();

// La cadena de conexión se busca en "ConnectionStrings:Default": en local
// puede venir de appsettings.Development.json (réplica local en Docker) o de
// los user-secrets del proyecto (por ejemplo, apuntando al pooler de
// Supabase); en Azure, de las Application Settings/Key Vault configuradas en
// el App Service. Se valida aquí para fallar rápido en el arranque con un
// mensaje claro, en vez de que el error aparezca más tarde y de forma
// confusa en la primera petición que toque la base de datos (ej. login).
var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
{
	throw new InvalidOperationException(
		"No se encontró la cadena de conexión 'ConnectionStrings:Default'. " +
		"Configúrala con 'dotnet user-secrets set \"ConnectionStrings:Default\" \"<cadena>\"' " +
		"(apuntando a Supabase o a la réplica local de docker-compose.yml), " +
		"o en appsettings.Development.json para desarrollo local.");
}

builder.Services.AddDbContext<MemByteDbContext>(options =>
	options.UseNpgsql(connectionString, npgsql =>
		npgsql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null)));

// Identity: modelo de usuario/rol custom, política de contraseñas y lockout
// alineada con el plan de seguridad (mín. 12 caracteres, complejidad, bloqueo
// tras 5 intentos fallidos).
builder.Services
	.AddIdentity<ApplicationUser, ApplicationRole>(options =>
	{
		options.Password.RequiredLength = 12;
		options.Password.RequireUppercase = true;
		options.Password.RequireLowercase = true;
		options.Password.RequireDigit = true;
		options.Password.RequireNonAlphanumeric = true;

		options.Lockout.MaxFailedAccessAttempts = 5;
		options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
		options.Lockout.AllowedForNewUsers = true;

		options.User.RequireUniqueEmail = true;
		options.SignIn.RequireConfirmedEmail = false; // Ajustable cuando se active el flujo de confirmación de email.
	})
	.AddEntityFrameworkStores<MemByteDbContext>()
	.AddPasswordValidator<BreachedPasswordValidator>()
	.AddPasswordValidator<PasswordHistoryValidator>()
	.AddDefaultTokenProviders();

// Reemplaza el UserManager por defecto para que el historial de contraseñas
// se registre automáticamente tras un cambio/restablecimiento correcto.
builder.Services.AddScoped<UserManager<ApplicationUser>, ApplicationUserManager>();

// Cookie de autenticación con configuración segura por defecto.
builder.Services.ConfigureApplicationCookie(options =>
{
	options.Cookie.HttpOnly = true;
	options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
	options.Cookie.SameSite = SameSiteMode.Strict;

	options.ExpireTimeSpan = TimeSpan.FromMinutes(60); // Inactividad: 60 min (configurable).
	options.SlidingExpiration = true;

	options.LoginPath = "/login";
	options.AccessDeniedPath = "/access-denied";

	// Evita redirecciones HTML en llamadas API: se devuelve 401/403 explícito.
	options.Events.OnRedirectToLogin = context =>
	{
		if (context.Request.Path.StartsWithSegments("/api"))
		{
			context.Response.StatusCode = StatusCodes.Status401Unauthorized;
			return Task.CompletedTask;
		}

		context.Response.Redirect(context.RedirectUri);
		return Task.CompletedTask;
	};
});

// Políticas del módulo de mensajería: cualquier usuario autenticado puede
// leer sus propias conversaciones, pero sólo Admin y Support pueden
// responder o cambiar el estado de un hilo. El aislamiento por cliente no se
// resuelve aquí sino en el servicio, porque depende del dato consultado.
builder.Services.AddAuthorization(options =>
{
	options.AddPolicy(MessagingPolicies.AccessMessaging, policy =>
		policy.RequireAuthenticatedUser());

	options.AddPolicy(MessagingPolicies.ReplyMessages, policy =>
		policy.RequireAuthenticatedUser()
			  .RequireRole(ApplicationRoleNames.MessagingResponders));
});

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IMfaService, MfaService>();

// Gestión de empleados (fichas de RRHH), persistidas en la base de datos.
// Independiente de Identity/ApplicationUser: un Employee es el registro del
// trabajador, no la cuenta con la que accede a la intranet.
builder.Services.AddScoped<IEmployeeService, EmployeeService>();

// Gestión de clientes: agrupador base de los usuarios externos que acceden
// a la aplicación frontal de MemByte (ver planes-de-desarrollo/03-plan-gestion-usuarios.md).
// Por ahora es una ficha mínima (nombre + estado), sin proyectos ni facturación.
builder.Services.AddScoped<IClientService, ClientService>();

// Gestión del catálogo de productos y servicios de cada cliente de la
// plantilla (incluido el propio catálogo de soluciones digitales de
// MemByte). Cada cliente adaptará los atributos de Product/Service a su
// propia naturaleza de negocio; el aislamiento se aplica siempre por
// ClientId, recibido explícitamente en cada operación del servicio.
builder.Services.AddScoped<ICatalogService, CatalogService>();

// Gestión de pedidos de cada cliente de la plantilla: alta, líneas
// (referenciando artículos del catálogo), transiciones de estado y pago.
// Vista genérica pensada para extenderse: en el caso de MemByte, sus
// clientes ordenan soluciones digitales y servicios de mantenimiento.
builder.Services.AddScoped<IOrderService, OrderService>();

// Gestión de cuentas de usuario
// activación, roles, vínculo con Employee/Client, activación/desactivación,
// reseteo de contraseña y revocación de sesiones.
builder.Services.AddScoped<IUserService, UserService>();

// Envío de email: implementación "stub" (solo log) mientras no se decide un
// proveedor real (SMTP/SendGrid/Azure Communication Services, ver runbook de
// despliegue). El enlace de invitación/reseteo también se muestra al Admin
// directamente en la UI, así que este stub no bloquea el flujo (PR3 del plan
// de gestión de usuarios).
builder.Services.AddScoped<IEmailSender, LoggingEmailSender>();

// Mensajería interna: opciones de negocio (retención, límites de adjuntos) y
// almacenamiento de adjuntos. Si no hay cuenta de Azure Storage configurada
// se usa la implementación local en disco, válida solo para desarrollo: en
// ningún caso se exponen URLs públicas, el contenido se sirve desde la
// aplicación tras validar los permisos del usuario.
builder.Services.Configure<MessagingOptions>(
	builder.Configuration.GetSection(MessagingOptions.SectionName));
builder.Services.Configure<MessagingAttachmentStorageOptions>(
	builder.Configuration.GetSection(MessagingAttachmentStorageOptions.SectionName));

var attachmentStorageConnectionString =
	builder.Configuration[$"{MessagingAttachmentStorageOptions.SectionName}:ConnectionString"];

if (!string.IsNullOrWhiteSpace(attachmentStorageConnectionString))
{
	builder.Services.AddSingleton<IMessagingAttachmentStorage, AzureBlobMessagingAttachmentStorage>();
}
else
{
	builder.Services.AddSingleton<IMessagingAttachmentStorage, LocalMessagingAttachmentStorage>();
}

builder.Services.AddScoped<IMessagingUserContextProvider, MessagingUserContextProvider>();
builder.Services.AddScoped<IMessagingAuditLogger, MessagingAuditLogger>();
builder.Services.AddScoped<IMessagingService, MessagingService>();

// Avisos en tiempo real de mensajería. Blazor Server ya usa SignalR para el
// circuito, pero el hub es independiente para poder notificar también a
// pestañas en segundo plano sin acoplarlo al ciclo de vida del componente.
builder.Services.AddScoped<IMessagingNotifier, SignalRMessagingNotifier>();

// Purga periódica de conversaciones fuera de la política de retención
// (5 años por defecto, configurable en Messaging:RetentionYears).
builder.Services.AddScoped<MessagingRetentionService>();
builder.Services.AddHostedService<MessagingRetentionBackgroundService>();

// Refuerza el hashing de contraseñas por encima del valor por defecto de
// ASP.NET Core Identity (100.000 iteraciones PBKDF2-HMAC-SHA256), alineado
// con la recomendación OWASP vigente para PBKDF2-SHA256.
builder.Services.Configure<PasswordHasherOptions>(options =>
{
	options.IterationCount = 210_000;
});

// Rate limiting específico para los endpoints de autenticación (login, MFA,
// logout): limita intentos por IP para mitigar fuerza bruta y credential
// stuffing sin penalizar el resto de la aplicación.
builder.Services.AddRateLimiter(options =>
{
	options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

	options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
		PartitionedRateLimiter.Create<HttpContext, string>(context =>
		{
			var isAuthEndpoint = HttpMethods.IsPost(context.Request.Method) &&
				(context.Request.Path.StartsWithSegments("/login") ||
				 context.Request.Path.StartsWithSegments("/mfa") ||
				 context.Request.Path.StartsWithSegments("/logout"));

			if (!isAuthEndpoint)
				return RateLimitPartition.GetNoLimiter("bypass");

			var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
			return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
			{
				PermitLimit = 10,
				Window = TimeSpan.FromMinutes(1),
				QueueLimit = 0
			});
		}));
});

var app = builder.Build();

// Aplica migraciones pendientes y siembra roles/admin de desarrollo. Solo en
// Development: en producción las migraciones se aplican como paso de
// despliegue explícito y las cuentas se crean desde el módulo de Gestión de
// Usuarios, nunca automáticamente al arrancar.
if (app.Environment.IsDevelopment())
{
	using var scope = app.Services.CreateScope();
	var services = scope.ServiceProvider;

	var dbContext = services.GetRequiredService<MemByteDbContext>();
	await dbContext.Database.MigrateAsync();

	var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
	var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
	var configuration = services.GetRequiredService<IConfiguration>();
	var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeder");

	await IdentitySeeder.SeedAsync(userManager, roleManager, configuration, logger);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Error", createScopeForErrors: true);
	// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
	app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// Cabeceras de seguridad de navegador de referencia (Fase 3 del plan de
// hardening). CSP permisiva en style-src por los bloques <style> embebidos
// de los layouts de Blazor (CSS con ámbito); el resto se restringe a 'self'.
app.Use(async (context, next) =>
{
	var headers = context.Response.Headers;
	headers["X-Content-Type-Options"] = "nosniff";
	headers["X-Frame-Options"] = "DENY";
	headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
	headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
	headers["Content-Security-Policy"] =
		"default-src 'self'; " +
		"script-src 'self'; " +
		"style-src 'self' 'unsafe-inline'; " +
		"img-src 'self' data:; " +
		"font-src 'self' data:; " +
		"connect-src 'self' ws: wss:; " +
		"frame-ancestors 'none'; " +
		"base-uri 'self'; " +
		"form-action 'self'";

	await next();
});

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapStaticAssets();
app.MapRazorComponents<App>()
	.AddInteractiveServerRenderMode();

app.MapHub<MessagingHub>(MessagingHub.HubPath);

// Descarga de adjuntos de mensajería. No se sirve el blob directamente: el
// servicio revalida el cliente y la participación del usuario antes de
// devolver el contenido, y registra la descarga en la auditoría.
app.MapGet("/messaging/attachments/{attachmentId:guid}", async (
	Guid attachmentId,
	IMessagingService messagingService,
	CancellationToken cancellationToken) =>
{
	var result = await messagingService.OpenAttachmentAsync(attachmentId, cancellationToken);

	if (!result.Succeeded || result.Value is null)
		return Results.NotFound();

	return Results.File(
		result.Value.Content,
		result.Value.ContentType,
		result.Value.FileName);
}).RequireAuthorization(MessagingPolicies.AccessMessaging);

// Cierre de sesión: se implementa como endpoint dedicado (no como componente
// Blazor interactivo) porque SignInManager necesita escribir la cookie de
// borrado directamente en la respuesta HTTP antes de que se envíen las
// cabeceras.
app.MapPost("/logout", async (SignInManager<ApplicationUser> signInManager) =>
{
	await signInManager.SignOutAsync();
	return Results.LocalRedirect("/login");
});

app.Run();
