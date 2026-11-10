namespace MemByte.Application.Auth;

/// <summary>
/// Resultado posible de un intento de login, alineado con el contrato
/// documentado en el plan (200 éxito, 202 MFA requerida, 401 fallo).
/// </summary>
public enum LoginOutcome
{
	Succeeded,
	RequiresTwoFactor,
	LockedOut,
	InvalidCredentials,
	NotAllowed
}

/// <summary>
/// Resultado de un intento de login, con la información mínima necesaria
/// para decidir la redirección o el mensaje de error a mostrar. No incluye
/// nunca detalles que permitan enumerar cuentas (p.ej. si el email existe).
/// </summary>
public sealed record LoginResult(
	LoginOutcome Outcome,
	string? RedirectUrl = null,
	TimeSpan? LockoutRemaining = null)
{
	public static LoginResult Success(string redirectUrl) =>
		new(LoginOutcome.Succeeded, RedirectUrl: redirectUrl);

	public static LoginResult RequiresTwoFactor() =>
		new(LoginOutcome.RequiresTwoFactor);

	public static LoginResult LockedOut(TimeSpan remaining) =>
		new(LoginOutcome.LockedOut, LockoutRemaining: remaining);

	public static LoginResult InvalidCredentials() =>
		new(LoginOutcome.InvalidCredentials);

	public static LoginResult NotAllowed() =>
		new(LoginOutcome.NotAllowed);
}
