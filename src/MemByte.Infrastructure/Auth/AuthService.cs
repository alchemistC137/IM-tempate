using MemByte.Application.Auth;
using MemByte.Infrastructure.Audit;
using MemByte.Infrastructure.Identity;
using MemByte.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MemByte.Infrastructure.Auth;

/// <summary>
/// Implementación del flujo de login sobre ASP.NET Core Identity. Registra
/// auditoría de éxito/fallo y calcula la ruta de redirección por rol.
/// </summary>
public class AuthService : IAuthService
{
	private readonly UserManager<ApplicationUser> _userManager;
	private readonly SignInManager<ApplicationUser> _signInManager;
	private readonly MemByteDbContext _dbContext;
	private readonly ISecurityTelemetryPublisher? _telemetryPublisher;

	public AuthService(
		UserManager<ApplicationUser> userManager,
		SignInManager<ApplicationUser> signInManager,
		MemByteDbContext dbContext,
		ISecurityTelemetryPublisher? telemetryPublisher = null)
	{
		_userManager = userManager;
		_signInManager = signInManager;
		_dbContext = dbContext;
		_telemetryPublisher = telemetryPublisher;
	}

	public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
	{
		var user = await _userManager.FindByEmailAsync(request.Email);

		if (user is null)
		{
			// No revelamos si el email existe o no: registramos el intento sin UserId
			// y devolvemos el mismo resultado que unas credenciales inválidas.
			await LogAsync(null, "LoginFailed", success: false, "UserNotFound", request.IpAddress, request.UserAgent);
			return LoginResult.InvalidCredentials();
		}

		var result = await _signInManager.PasswordSignInAsync(user, request.Password, request.RememberMe, lockoutOnFailure: true);

		if (result.IsLockedOut)
		{
			var remaining = (user.LockoutEnd ?? DateTimeOffset.UtcNow) - DateTimeOffset.UtcNow;
			await LogAsync(user.Id, "AccountLockedOut", success: false, "TooManyFailedAttempts", request.IpAddress, request.UserAgent);
			return LoginResult.LockedOut(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero);
		}

		if (result.IsNotAllowed)
		{
			await LogAsync(user.Id, "LoginNotAllowed", success: false, "EmailNotConfirmedOrDisallowed", request.IpAddress, request.UserAgent);
			return LoginResult.NotAllowed();
		}

		if (result.RequiresTwoFactor)
		{
			// SignInManager ya ha establecido la cookie parcial de dos factores
			// (TwoFactorUserIdScheme); el segundo paso lo resuelve IMfaService.
			await LogAsync(user.Id, "LoginRequiresTwoFactor", success: true, null, request.IpAddress, request.UserAgent);
			return LoginResult.RequiresTwoFactor();
		}

		if (!result.Succeeded)
		{
			await LogAsync(user.Id, "LoginFailed", success: false, "InvalidPassword", request.IpAddress, request.UserAgent);
			return LoginResult.InvalidCredentials();
		}

		user.LastLoginAt = DateTimeOffset.UtcNow;
		await _userManager.UpdateAsync(user);

		await TrackSessionAsync(user.Id, request);
		await LogAsync(user.Id, "LoginSucceeded", success: true, null, request.IpAddress, request.UserAgent);

		var roles = await _userManager.GetRolesAsync(user);
		var redirectUrl = RoleRedirectMap.Resolve(roles);

		return LoginResult.Success(redirectUrl);
	}

	public async Task<SetPasswordResult> SetPasswordAsync(SetPasswordRequest request, CancellationToken cancellationToken = default)
	{
		var user = await _userManager.FindByIdAsync(request.UserId.ToString());
		if (user is null)
		{
			// Mensaje genérico: un token válido ya identifica al usuario, así
			// que un fallo aquí solo puede deberse a un enlace manipulado o a
			// una cuenta borrada, no a enumeración de cuentas por email.
			return SetPasswordResult.Failure("El enlace no es válido o ha caducado.");
		}

		var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
		if (!result.Succeeded)
		{
			var isTokenError = result.Errors.Any(e => e.Code == "InvalidToken");
			await LogAsync(user.Id, "PasswordSetViaToken", success: false, isTokenError ? "InvalidOrExpiredToken" : "PasswordPolicy", request.IpAddress, request.UserAgent);

			return isTokenError
				? SetPasswordResult.Failure("El enlace no es válido o ha caducado.")
				: SetPasswordResult.Failure(result.Errors.Select(e => e.Description).ToArray());
		}

		await LogAsync(user.Id, "PasswordSetViaToken", success: true, null, request.IpAddress, request.UserAgent);

		return SetPasswordResult.Success();
	}

	private async Task TrackSessionAsync(Guid userId, LoginRequest request)
	{
		var session = new UserSession(userId, Guid.NewGuid().ToString("N"), request.IpAddress, request.UserAgent);
		_dbContext.UserSessions.Add(session);
		await _dbContext.SaveChangesAsync();
	}

	private async Task LogAsync(Guid? userId, string eventType, bool success, string? failureReason, string? ipAddress, string? userAgent)
	{
		var entry = new AuthAuditEntry(userId, eventType, success, failureReason, ipAddress, userAgent);
		_dbContext.AuthAuditEntries.Add(entry);
		await _dbContext.SaveChangesAsync();

		_telemetryPublisher?.Publish(entry);
	}
}
