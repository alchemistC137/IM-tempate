using MemByte.Infrastructure.Identity;
using MemByte.Infrastructure.Identity.Seeding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace MemByte.Infrastructure.Tests.Auth;

public class IdentitySeederTests
{
	[Fact]
	public async Task SeedAsync_DefaultConfiguration_CreatesRolesButNoAccounts()
	{
		using var host = new AuthTestHost();
		var configuration = new ConfigurationBuilder().Build();

		await IdentitySeeder.SeedAsync(host.UserManager, host.RoleManager, configuration, NullLogger.Instance);

		Assert.Empty(host.DbContext.Users);
		Assert.Equal(ApplicationRoleNames.All.Count(), host.DbContext.Roles.Count());
	}

	[Fact]
	public async Task SeedAsync_ExplicitCredentials_CreatesNeutralAdminOnlyOnce()
	{
		using var host = new AuthTestHost();
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
		{
			["SeedAdmin:Enabled"] = "true",
			["SeedAdmin:Email"] = "admin@example.test",
			["SeedAdmin:Password"] = AuthTestHost.ValidPassword
		}).Build();

		await IdentitySeeder.SeedAsync(host.UserManager, host.RoleManager, configuration, NullLogger.Instance);
		await IdentitySeeder.SeedAsync(host.UserManager, host.RoleManager, configuration, NullLogger.Instance);

		var admin = Assert.Single(host.DbContext.Users);
		Assert.Equal("Administrador", admin.DisplayName);
		Assert.Equal("admin@example.test", admin.Email);
		Assert.True(await host.UserManager.IsInRoleAsync(admin, ApplicationRoleNames.Admin));
		Assert.True(await host.UserManager.CheckPasswordAsync(admin, AuthTestHost.ValidPassword));
	}
}
