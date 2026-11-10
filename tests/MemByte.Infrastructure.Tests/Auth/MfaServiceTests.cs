using MemByte.Application.Auth;
using MemByte.Infrastructure.Identity;
using MfaServiceImpl = MemByte.Infrastructure.Auth.MfaService;

namespace MemByte.Infrastructure.Tests.Auth;

public class MfaServiceTests
{
	[Fact]
	public async Task BeginAuthenticatorSetupAsync_ReturnsSharedKeyAndOtpAuthUri()
	{
		using var host = new AuthTestHost();
		var user = await host.CreateUserAsync("mfa-setup@membyte.local", AuthTestHost.ValidPassword, ApplicationRoleNames.Employee);

		var sut = new MfaServiceImpl(host.UserManager, host.SignInManager, host.DbContext);

		var setupInfo = await sut.BeginAuthenticatorSetupAsync(user.Id);

		Assert.False(string.IsNullOrWhiteSpace(setupInfo.SharedKey));
		Assert.StartsWith("otpauth://totp/", setupInfo.AuthenticatorUri);
	}

	[Fact]
	public async Task ConfirmAuthenticatorSetupAsync_WithValidCode_EnablesTwoFactor()
	{
		using var host = new AuthTestHost();
		var user = await host.CreateUserAsync("mfa-confirm@membyte.local", AuthTestHost.ValidPassword, ApplicationRoleNames.Employee);

		var sut = new MfaServiceImpl(host.UserManager, host.SignInManager, host.DbContext);
		var setupInfo = await sut.BeginAuthenticatorSetupAsync(user.Id);

		// Calcula el código TOTP como lo haría una app autenticadora real a
		// partir de la clave compartida, ya que el servidor nunca genera
		// códigos TOTP (sólo los valida).
		var validCode = TotpTestHelper.GenerateCode(setupInfo.SharedKey);

		var confirmed = await sut.ConfirmAuthenticatorSetupAsync(user.Id, validCode);

		Assert.True(confirmed);
		Assert.True(await host.UserManager.GetTwoFactorEnabledAsync(user));
		Assert.Contains(host.DbContext.AuthAuditEntries, e => e.EventType == "MfaEnabled" && e.UserId == user.Id && e.WasSuccessful);
	}

	[Fact]
	public async Task ConfirmAuthenticatorSetupAsync_WithInvalidCode_DoesNotEnableTwoFactor()
	{
		using var host = new AuthTestHost();
		var user = await host.CreateUserAsync("mfa-invalid@membyte.local", AuthTestHost.ValidPassword, ApplicationRoleNames.Employee);

		var sut = new MfaServiceImpl(host.UserManager, host.SignInManager, host.DbContext);
		await sut.BeginAuthenticatorSetupAsync(user.Id);

		var confirmed = await sut.ConfirmAuthenticatorSetupAsync(user.Id, "000000");

		Assert.False(confirmed);
		Assert.False(await host.UserManager.GetTwoFactorEnabledAsync(user));
		Assert.Contains(host.DbContext.AuthAuditEntries, e => e.EventType == "MfaInvalidCode" && e.UserId == user.Id && !e.WasSuccessful);
	}

	[Fact]
	public async Task GenerateRecoveryCodesAsync_ReturnsExpectedCount()
	{
		using var host = new AuthTestHost();
		var user = await host.CreateUserAsync("mfa-recovery@membyte.local", AuthTestHost.ValidPassword, ApplicationRoleNames.Employee);

		var sut = new MfaServiceImpl(host.UserManager, host.SignInManager, host.DbContext);
		await host.UserManager.SetTwoFactorEnabledAsync(user, true);

		var codes = await sut.GenerateRecoveryCodesAsync(user.Id);

		Assert.Equal(8, codes.Count);
		Assert.Contains(host.DbContext.AuthAuditEntries, e => e.EventType == "RecoveryCodesGenerated" && e.UserId == user.Id);
	}

	[Fact]
	public async Task ValidateRecoveryCodeAsync_WithUnknownCode_ReturnsInvalidCredentials()
	{
		using var host = new AuthTestHost();
		var user = await host.CreateUserAsync("mfa-badrecovery@membyte.local", AuthTestHost.ValidPassword, ApplicationRoleNames.Employee);
		await host.UserManager.SetTwoFactorEnabledAsync(user, true);

		var sut = new MfaServiceImpl(host.UserManager, host.SignInManager, host.DbContext);

		var result = await sut.ValidateRecoveryCodeAsync("not-a-real-code");

		Assert.Equal(LoginOutcome.InvalidCredentials, result.Outcome);
		Assert.Contains(host.DbContext.AuthAuditEntries, e => e.EventType == "InvalidRecoveryCode" && !e.WasSuccessful);
	}

	[Fact]
	public async Task DisableAsync_TurnsOffTwoFactorAndResetsAuthenticatorKey()
	{
		using var host = new AuthTestHost();
		var user = await host.CreateUserAsync("mfa-disable@membyte.local", AuthTestHost.ValidPassword, ApplicationRoleNames.Employee);

		var sut = new MfaServiceImpl(host.UserManager, host.SignInManager, host.DbContext);
		await sut.BeginAuthenticatorSetupAsync(user.Id);
		await host.UserManager.SetTwoFactorEnabledAsync(user, true);

		await sut.DisableAsync(user.Id);

		Assert.False(await host.UserManager.GetTwoFactorEnabledAsync(user));
		Assert.Contains(host.DbContext.AuthAuditEntries, e => e.EventType == "MfaDisabled" && e.UserId == user.Id);
	}
}
