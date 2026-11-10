using MemByte.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace MemByte.Infrastructure.Tests.Auth;

public class PasswordHistoryValidatorTests
{
	[Fact]
	public async Task ValidateAsync_WithNewPassword_Succeeds()
	{
		using var host = new AuthTestHost();
		var user = await host.CreateUserAsync("history-new@membyte.local", AuthTestHost.ValidPassword, ApplicationRoleNames.Employee);

		var sut = new PasswordHistoryValidator(host.DbContext);

		var result = await sut.ValidateAsync(host.UserManager, user, "Br4nd!NewPassw0rd");

		Assert.True(result.Succeeded);
	}

	[Fact]
	public async Task ValidateAsync_WithNullOrEmptyPassword_Succeeds()
	{
		using var host = new AuthTestHost();
		var user = await host.CreateUserAsync("history-empty@membyte.local", AuthTestHost.ValidPassword, ApplicationRoleNames.Employee);

		var sut = new PasswordHistoryValidator(host.DbContext);

		var result = await sut.ValidateAsync(host.UserManager, user, null);

		Assert.True(result.Succeeded);
	}

	[Fact]
	public async Task ValidateAsync_WithPasswordMatchingRecordedHistory_Fails()
	{
		using var host = new AuthTestHost();
		var user = await host.CreateUserAsync("history-reuse@membyte.local", AuthTestHost.ValidPassword, ApplicationRoleNames.Employee);

		// Simula que la contraseña actual del usuario ya quedó registrada en el
		// historial (lo que hace ApplicationUserManager tras un cambio exitoso).
		var currentHash = host.UserManager.PasswordHasher.HashPassword(user, AuthTestHost.ValidPassword);
		host.DbContext.Set<PasswordHistoryEntry>().Add(new PasswordHistoryEntry(user.Id, currentHash));
		await host.DbContext.SaveChangesAsync();

		var sut = new PasswordHistoryValidator(host.DbContext);

		var result = await sut.ValidateAsync(host.UserManager, user, AuthTestHost.ValidPassword);

		Assert.False(result.Succeeded);
		Assert.Contains(result.Errors, e => e.Code == "PasswordReused");
	}

	[Fact]
	public async Task ValidateAsync_OnlyChecksLastFiveEntries()
	{
		using var host = new AuthTestHost();
		var user = await host.CreateUserAsync("history-limit@membyte.local", AuthTestHost.ValidPassword, ApplicationRoleNames.Employee);

		// Contraseña antigua, fuera de la ventana de las últimas 5 (se añaden 5
		// entradas más recientes después), no debería bloquear su reutilización.
		var oldPassword = "Old3rPassw0rd!";
		var oldHash = host.UserManager.PasswordHasher.HashPassword(user, oldPassword);
		var oldEntry = new PasswordHistoryEntry(user.Id, oldHash);
		SetCreatedAt(oldEntry, DateTimeOffset.UtcNow.AddDays(-10));
		host.DbContext.Set<PasswordHistoryEntry>().Add(oldEntry);

		for (var i = 0; i < PasswordHistoryValidator.MaxHistoryEntries; i++)
		{
			var recentHash = host.UserManager.PasswordHasher.HashPassword(user, $"Recent{i}Passw0rd!");
			var recentEntry = new PasswordHistoryEntry(user.Id, recentHash);
			SetCreatedAt(recentEntry, DateTimeOffset.UtcNow.AddDays(-i));
			host.DbContext.Set<PasswordHistoryEntry>().Add(recentEntry);
		}

		await host.DbContext.SaveChangesAsync();

		var sut = new PasswordHistoryValidator(host.DbContext);

		var result = await sut.ValidateAsync(host.UserManager, user, oldPassword);

		Assert.True(result.Succeeded);
	}

	private static void SetCreatedAt(PasswordHistoryEntry entry, DateTimeOffset createdAt)
	{
		var property = typeof(PasswordHistoryEntry).GetProperty(nameof(PasswordHistoryEntry.CreatedAt))!;
		property.SetValue(entry, createdAt);
	}
}
