using MemByte.Infrastructure.Identity;
using MemByte.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MemByte.Infrastructure.Tests.Auth;

/// <summary>
/// Construye un contenedor de servicios con ASP.NET Core Identity real
/// (UserManager/SignInManager) sobre EF Core InMemory, para probar el flujo
/// de login de extremo a extremo sin necesidad de mocks de tipos sellados ni
/// de una base de datos real. Cada test obtiene una base InMemory aislada.
/// </summary>
public sealed class AuthTestHost : IDisposable
{
	public ServiceProvider Services { get; }
	private readonly IServiceScope _scope;
	public MemByteDbContext DbContext { get; }
	public HttpContext HttpContext { get; }

	public AuthTestHost()
	{
		var serviceCollection = new ServiceCollection();

		serviceCollection.AddLogging();
		serviceCollection.AddHttpContextAccessor();

		serviceCollection.AddDbContext<MemByteDbContext>(options =>
			options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));

		serviceCollection
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
				options.SignIn.RequireConfirmedEmail = false;
			})
			.AddEntityFrameworkStores<MemByteDbContext>()
			.AddDefaultTokenProviders();

		Services = serviceCollection.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

		// Se resuelve todo desde un único scope explícito, igual que ocurriría
		// con una petición HTTP real, para evitar instancias duplicadas de
		// DbContext/UserManager al resolver servicios "Scoped" directamente
		// desde el contenedor raíz.
		_scope = Services.CreateScope();
		var scopedProvider = _scope.ServiceProvider;

		DbContext = scopedProvider.GetRequiredService<MemByteDbContext>();
		DbContext.Database.EnsureCreated();

		HttpContext = new DefaultHttpContext { RequestServices = scopedProvider };

		UserManager = scopedProvider.GetRequiredService<UserManager<ApplicationUser>>();
		RoleManager = scopedProvider.GetRequiredService<RoleManager<ApplicationRole>>();
		SignInManager = scopedProvider.GetRequiredService<SignInManager<ApplicationUser>>();
		SignInManager.Context = HttpContext;
	}

	public UserManager<ApplicationUser> UserManager { get; }

	public RoleManager<ApplicationRole> RoleManager { get; }

	public SignInManager<ApplicationUser> SignInManager { get; }

	public const string ValidPassword = "Str0ng!Passw0rd";

	public async Task<ApplicationUser> CreateUserAsync(string email, string password, params string[] roles)
	{
		var user = new ApplicationUser
		{
			UserName = email,
			Email = email,
			EmailConfirmed = true,
			DisplayName = email
		};

		var result = await UserManager.CreateAsync(user, password);
		if (!result.Succeeded)
			throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

		foreach (var role in roles)
		{
			if (!await RoleManager.RoleExistsAsync(role))
				await RoleManager.CreateAsync(new ApplicationRole { Name = role });

			await UserManager.AddToRoleAsync(user, role);
		}

		return user;
	}

	public void Dispose()
	{
		_scope.Dispose();
		Services.Dispose();
	}
}
