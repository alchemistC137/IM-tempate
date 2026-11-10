using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MemByte.Infrastructure.Identity.Seeding;

public static class IdentitySeeder
{
	public static async Task SeedAsync(
		UserManager<ApplicationUser> userManager,
		RoleManager<ApplicationRole> roleManager,
		IConfiguration configuration,
		ILogger logger,
		CancellationToken cancellationToken = default)
	{
		foreach (var roleName in ApplicationRoleNames.All)
		{
			if (await roleManager.RoleExistsAsync(roleName))
				continue;

			var roleResult = await roleManager.CreateAsync(new ApplicationRole { Name = roleName });
			if (!roleResult.Succeeded)
			{
				logger.LogWarning(
					"No se pudo crear el rol {RoleName} durante el seeding: {Errors}",
					roleName,
					string.Join("; ", roleResult.Errors.Select(e => e.Description)));
			}
		}

		var seedAdminEnabled = configuration.GetValue<bool>("SeedAdmin:Enabled");
		if (!seedAdminEnabled)
		{
			logger.LogInformation("SeedAdmin deshabilitado. No se crea usuario administrador por seeding.");
			return;
		}

		var adminEmail = configuration["SeedAdmin:Email"];
		var adminPassword = configuration["SeedAdmin:Password"];

		if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
		{
			logger.LogWarning(
				"SeedAdmin habilitado pero faltan SeedAdmin:Email o SeedAdmin:Password. Se omite creación de admin.");
			return;
		}

		var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
		if (existingAdmin is not null)
			return;

		var admin = new ApplicationUser
		{
			UserName = adminEmail,
			Email = adminEmail,
			EmailConfirmed = true,
			DisplayName = "Administrador MemByte-IM"
		};

		var createResult = await userManager.CreateAsync(admin, adminPassword);
		if (!createResult.Succeeded)
		{
			logger.LogWarning(
				"No se pudo crear la cuenta de administrador de seeding: {Errors}",
				string.Join("; ", createResult.Errors.Select(e => e.Description)));
			return;
		}

		await userManager.AddToRoleAsync(admin, ApplicationRoleNames.Admin);

		logger.LogInformation("Cuenta de administrador creada por seeding: {Email}", adminEmail);
	}
}
