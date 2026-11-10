using System.Text;
using MemByte.Application.Auth;
using MemByte.Application.Configuration;
using MemByte.Infrastructure.Audit;
using MemByte.Infrastructure.Identity;
using MemByte.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace MemByte.Infrastructure.Auth;

/// <summary>
/// Implementación de <see cref="IMfaService"/> sobre el proveedor TOTP
/// nativo de ASP.NET Core Identity (AuthenticatorTokenProvider) y los
/// códigos de recuperación integrados. Registra auditoría/telemetría de los
/// eventos de ciclo de vida de MFA (alta, baja, código inválido, generación
/// de códigos de recuperación) de forma análoga a <see cref="AuthService"/>.
/// </summary>
public class MfaService : IMfaService
{
	private const int RecoveryCodesCount = 8;

	private readonly IntranetOptions _intranet;
	private readonly UserManager<ApplicationUser> _userManager;
	private readonly SignInManager<ApplicationUser> _signInManager;
	private readonly MemByteDbContext? _dbContext;
	private readonly ISecurityTelemetryPublisher? _telemetryPublisher;

	public MfaService(
		UserManager<ApplicationUser> userManager,
		SignInManager<ApplicationUser> signInManager,
		MemByteDbContext? dbContext = null,
		ISecurityTelemetryPublisher? telemetryPublisher = null,
		IOptions<IntranetOptions>? intranetOptions = null)
	{
		_intranet = intranetOptions?.Value ?? new IntranetOptions();
		_userManager = userManager;
		_signInManager = signInManager;
		_dbContext = dbContext;
		_telemetryPublisher = telemetryPublisher;
	}

	public async Task<MfaSetupInfo> BeginAuthenticatorSetupAsync(Guid userId, CancellationToken cancellationToken = default)
	{
		var user = await GetUserOrThrowAsync(userId);

		// Reiniciar la clave garantiza que un intento de configuración anterior
		// abandonado no deje una clave "a medias" activa.
		await _userManager.ResetAuthenticatorKeyAsync(user);
		var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);

		if (string.IsNullOrEmpty(unformattedKey))
			throw new InvalidOperationException("No se pudo generar la clave del autenticador.");

		var sharedKey = FormatSharedKey(unformattedKey);
		var authenticatorUri = GenerateAuthenticatorUri(user.Email ?? user.UserName ?? userId.ToString(), unformattedKey);

		return new MfaSetupInfo(sharedKey, authenticatorUri);
	}

	public async Task<bool> ConfirmAuthenticatorSetupAsync(Guid userId, string verificationCode, CancellationToken cancellationToken = default)
	{
		var user = await GetUserOrThrowAsync(userId);

		var normalizedCode = verificationCode.Replace(" ", string.Empty).Replace("-", string.Empty);

		var isValid = await _userManager.VerifyTwoFactorTokenAsync(
			user, _userManager.Options.Tokens.AuthenticatorTokenProvider, normalizedCode);

		if (!isValid)
		{
			await LogAsync(userId, "MfaInvalidCode", success: false, "InvalidAuthenticatorCode");
			return false;
		}

		await _userManager.SetTwoFactorEnabledAsync(user, true);
		await LogAsync(userId, "MfaEnabled", success: true, null);
		return true;
	}

	public async Task<IReadOnlyCollection<string>> GenerateRecoveryCodesAsync(Guid userId, CancellationToken cancellationToken = default)
	{
		var user = await GetUserOrThrowAsync(userId);

		var codes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodesCount);
		await LogAsync(userId, "RecoveryCodesGenerated", success: true, null);
		return codes?.ToList() ?? new List<string>();
	}

	public async Task<LoginResult> ValidateAuthenticatorCodeAsync(string code, bool rememberMachine, CancellationToken cancellationToken = default)
	{
		var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(code, isPersistent: false, rememberClient: rememberMachine);
		return await ToLoginResultAsync(result, "MfaInvalidCode");
	}

	public async Task<LoginResult> ValidateRecoveryCodeAsync(string recoveryCode, CancellationToken cancellationToken = default)
	{
		var result = await _signInManager.TwoFactorRecoveryCodeSignInAsync(recoveryCode);
		return await ToLoginResultAsync(result, "InvalidRecoveryCode");
	}

	public async Task DisableAsync(Guid userId, CancellationToken cancellationToken = default)
	{
		var user = await GetUserOrThrowAsync(userId);
		await _userManager.SetTwoFactorEnabledAsync(user, false);
		await _userManager.ResetAuthenticatorKeyAsync(user);
		await LogAsync(userId, "MfaDisabled", success: true, null);
	}

	private async Task<LoginResult> ToLoginResultAsync(SignInResult result, string failureEventType)
	{
		if (result.IsLockedOut)
			return LoginResult.LockedOut(TimeSpan.Zero);

		if (!result.Succeeded)
		{
			await LogAsync(null, failureEventType, success: false, "InvalidTwoFactorCredential");
			return LoginResult.InvalidCredentials();
		}

		var principal = _signInManager.Context.User;
		var user = await _signInManager.UserManager.GetUserAsync(principal);

		if (user is null)
			return LoginResult.InvalidCredentials();

		var roles = await _userManager.GetRolesAsync(user);
		return LoginResult.Success(RoleRedirectMap.Resolve(roles));
	}

	private async Task LogAsync(Guid? userId, string eventType, bool success, string? failureReason)
	{
		var entry = new AuthAuditEntry(userId, eventType, success, failureReason, ipAddress: null, userAgent: null);

		if (_dbContext is not null)
		{
			_dbContext.AuthAuditEntries.Add(entry);
			await _dbContext.SaveChangesAsync();
		}

		_telemetryPublisher?.Publish(entry);
	}

	private async Task<ApplicationUser> GetUserOrThrowAsync(Guid userId)
	{
		var user = await _userManager.FindByIdAsync(userId.ToString());

		if (user is null)
			throw new InvalidOperationException($"No se encontró el usuario {userId}.");

		return user;
	}

	private static string FormatSharedKey(string unformattedKey)
	{
		var result = new StringBuilder();
		var currentPosition = 0;

		while (currentPosition + 4 < unformattedKey.Length)
		{
			result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
			currentPosition += 4;
		}

		if (currentPosition < unformattedKey.Length)
			result.Append(unformattedKey.AsSpan(currentPosition));

		return result.ToString().ToUpperInvariant();
	}

	private string GenerateAuthenticatorUri(string userIdentifier, string unformattedKey)
	{
		const string uriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";

		return string.Format(
			uriFormat,
			Uri.EscapeDataString($"{_intranet.CompanyName} - {_intranet.ApplicationName}"),
			Uri.EscapeDataString(userIdentifier),
			unformattedKey);
	}
}
