using MemByte.Application.Auth;
using MemByte.Infrastructure.Identity;
using AuthServiceImpl = MemByte.Infrastructure.Auth.AuthService;

namespace MemByte.Infrastructure.Tests.Auth;

public class AuthServiceTests
{
	[Fact]
	public async Task LoginAsync_WithValidCredentials_ReturnsSucceededAndRoleRedirect()
	{
		using var host = new AuthTestHost();
		await host.CreateUserAsync("employee@membyte.local", AuthTestHost.ValidPassword, ApplicationRoleNames.Employee);

		var sut = new AuthServiceImpl(host.UserManager, host.SignInManager, host.DbContext);
		var request = new LoginRequest("employee@membyte.local", AuthTestHost.ValidPassword, RememberMe: false, "127.0.0.1", "test-agent");

		var result = await sut.LoginAsync(request);

		Assert.Equal(LoginOutcome.Succeeded, result.Outcome);
		Assert.Equal("/dashboard", result.RedirectUrl);
	}

	[Fact]
	public async Task LoginAsync_WithAdminRole_RedirectsToAdminDashboard()
	{
		using var host = new AuthTestHost();
		await host.CreateUserAsync("admin@membyte.local", AuthTestHost.ValidPassword, ApplicationRoleNames.Admin);

		var sut = new AuthServiceImpl(host.UserManager, host.SignInManager, host.DbContext);
		var request = new LoginRequest("admin@membyte.local", AuthTestHost.ValidPassword, RememberMe: false, "127.0.0.1", "test-agent");

		var result = await sut.LoginAsync(request);

		Assert.Equal(LoginOutcome.Succeeded, result.Outcome);
		Assert.Equal("/admin/dashboard", result.RedirectUrl);
	}

	[Fact]
	public async Task LoginAsync_WithUnknownEmail_ReturnsInvalidCredentialsWithoutRevealingExistence()
	{
		using var host = new AuthTestHost();

		var sut = new AuthServiceImpl(host.UserManager, host.SignInManager, host.DbContext);
		var request = new LoginRequest("nobody@membyte.local", "whatever", RememberMe: false, "127.0.0.1", "test-agent");

		var result = await sut.LoginAsync(request);

		Assert.Equal(LoginOutcome.InvalidCredentials, result.Outcome);
	}

	[Fact]
	public async Task LoginAsync_WithWrongPassword_ReturnsInvalidCredentials()
	{
		using var host = new AuthTestHost();
		await host.CreateUserAsync("employee@membyte.local", AuthTestHost.ValidPassword, ApplicationRoleNames.Employee);

		var sut = new AuthServiceImpl(host.UserManager, host.SignInManager, host.DbContext);
		var request = new LoginRequest("employee@membyte.local", "WrongPassword!123", RememberMe: false, "127.0.0.1", "test-agent");

		var result = await sut.LoginAsync(request);

		Assert.Equal(LoginOutcome.InvalidCredentials, result.Outcome);
	}

	[Fact]
	public async Task LoginAsync_AfterMaxFailedAttempts_LocksAccountOut()
	{
		using var host = new AuthTestHost();
		await host.CreateUserAsync("employee@membyte.local", AuthTestHost.ValidPassword, ApplicationRoleNames.Employee);

		var sut = new AuthServiceImpl(host.UserManager, host.SignInManager, host.DbContext);
		var badRequest = new LoginRequest("employee@membyte.local", "WrongPassword!123", RememberMe: false, "127.0.0.1", "test-agent");

		LoginResult? lastResult = null;
		for (var attempt = 0; attempt < 5; attempt++)
		{
			lastResult = await sut.LoginAsync(badRequest);
		}

		Assert.NotNull(lastResult);
		Assert.Equal(LoginOutcome.LockedOut, lastResult!.Outcome);

		// Incluso con la contraseña correcta, la cuenta bloqueada no debe permitir el acceso.
		var goodRequest = badRequest with { Password = AuthTestHost.ValidPassword };
		var resultWhileLocked = await sut.LoginAsync(goodRequest);
		Assert.Equal(LoginOutcome.LockedOut, resultWhileLocked.Outcome);
	}

	[Fact]
	public async Task LoginAsync_WithTwoFactorEnabled_ReturnsRequiresTwoFactor()
	{
		using var host = new AuthTestHost();
		var user = await host.CreateUserAsync("mfa-user@membyte.local", AuthTestHost.ValidPassword, ApplicationRoleNames.Employee);

		await host.UserManager.SetTwoFactorEnabledAsync(user, true);

		var sut = new AuthServiceImpl(host.UserManager, host.SignInManager, host.DbContext);
		var request = new LoginRequest("mfa-user@membyte.local", AuthTestHost.ValidPassword, RememberMe: false, "127.0.0.1", "test-agent");

		var result = await sut.LoginAsync(request);

		Assert.Equal(LoginOutcome.RequiresTwoFactor, result.Outcome);
	}

	[Fact]
	public async Task LoginAsync_OnSuccess_RecordsAuditEntryAndSession()
	{
		using var host = new AuthTestHost();
		await host.CreateUserAsync("employee@membyte.local", AuthTestHost.ValidPassword, ApplicationRoleNames.Employee);

		var sut = new AuthServiceImpl(host.UserManager, host.SignInManager, host.DbContext);
		var request = new LoginRequest("employee@membyte.local", AuthTestHost.ValidPassword, RememberMe: false, "10.0.0.5", "xunit-agent");

		await sut.LoginAsync(request);

		Assert.Single(host.DbContext.UserSessions);
		Assert.Contains(host.DbContext.AuthAuditEntries, e => e.EventType == "LoginSucceeded" && e.WasSuccessful);
	}

	[Fact]
	public async Task LoginAsync_OnFailure_RecordsAuditEntryWithoutSession()
	{
		using var host = new AuthTestHost();
		await host.CreateUserAsync("employee@membyte.local", AuthTestHost.ValidPassword, ApplicationRoleNames.Employee);

		var sut = new AuthServiceImpl(host.UserManager, host.SignInManager, host.DbContext);
		var request = new LoginRequest("employee@membyte.local", "WrongPassword!123", RememberMe: false, "10.0.0.5", "xunit-agent");

		await sut.LoginAsync(request);

		Assert.Empty(host.DbContext.UserSessions);
		Assert.Contains(host.DbContext.AuthAuditEntries, e => e.EventType == "LoginFailed" && !e.WasSuccessful);
	}

	[Fact]
	public async Task SetPasswordAsync_WithValidToken_UpdatesPasswordAndAllowsLogin()
	{
		using var host = new AuthTestHost();
		var user = await host.CreateUserAsync("invited@membyte.local", AuthTestHost.ValidPassword, ApplicationRoleNames.Employee);
		var token = await host.UserManager.GeneratePasswordResetTokenAsync(user);

		var sut = new AuthServiceImpl(host.UserManager, host.SignInManager, host.DbContext);
		const string newPassword = "N3w!Str0ngPassw0rd";

		var result = await sut.SetPasswordAsync(new SetPasswordRequest(user.Id, token, newPassword, "127.0.0.1", "test-agent"));

		Assert.True(result.Succeeded);
		Assert.Contains(host.DbContext.AuthAuditEntries, e =>
			e.UserId == user.Id && e.EventType == "PasswordSetViaToken" && e.WasSuccessful);

		var loginResult = await sut.LoginAsync(new LoginRequest("invited@membyte.local", newPassword, RememberMe: false, "127.0.0.1", "test-agent"));
		Assert.Equal(LoginOutcome.Succeeded, loginResult.Outcome);
	}

	[Fact]
	public async Task SetPasswordAsync_WithInvalidToken_Fails()
	{
		using var host = new AuthTestHost();
		var user = await host.CreateUserAsync("badtoken@membyte.local", AuthTestHost.ValidPassword, ApplicationRoleNames.Employee);

		var sut = new AuthServiceImpl(host.UserManager, host.SignInManager, host.DbContext);

		var result = await sut.SetPasswordAsync(new SetPasswordRequest(user.Id, "not-a-real-token", "N3w!Str0ngPassw0rd", "127.0.0.1", "test-agent"));

		Assert.False(result.Succeeded);
		Assert.Contains(host.DbContext.AuthAuditEntries, e =>
			e.UserId == user.Id && e.EventType == "PasswordSetViaToken" && !e.WasSuccessful);
	}

	[Fact]
	public async Task SetPasswordAsync_UnknownUser_Fails()
	{
		using var host = new AuthTestHost();
		var sut = new AuthServiceImpl(host.UserManager, host.SignInManager, host.DbContext);

		var result = await sut.SetPasswordAsync(new SetPasswordRequest(Guid.NewGuid(), "token", "N3w!Str0ngPassw0rd", null, null));

		Assert.False(result.Succeeded);
	}

	[Fact]
	public async Task SetPasswordAsync_WeakPassword_ReturnsPolicyErrors()
	{
		using var host = new AuthTestHost();
		var user = await host.CreateUserAsync("weakpwd@membyte.local", AuthTestHost.ValidPassword, ApplicationRoleNames.Employee);
		var token = await host.UserManager.GeneratePasswordResetTokenAsync(user);

		var sut = new AuthServiceImpl(host.UserManager, host.SignInManager, host.DbContext);

		var result = await sut.SetPasswordAsync(new SetPasswordRequest(user.Id, token, "short", "127.0.0.1", "test-agent"));

		Assert.False(result.Succeeded);
		Assert.NotEmpty(result.Errors);
	}
}
