namespace MemByte.Application.Auth;

/// <summary>
/// Servicio de aplicación para la configuración y verificación de MFA
/// (TOTP + códigos de recuperación). El cumplimiento de la política de MFA
/// (obligatoria/opcional por rol o tenant) se evalúa fuera de este servicio,
/// usando <c>ApplicationUser.IsMfaRequired</c> como bandera de configuración.
/// </summary>
public interface IMfaService
{
	/// <summary>
	/// Inicia (o reinicia) el proceso de configuración de un autenticador TOTP
	/// para el usuario, devolviendo la clave compartida y la URI otpauth://
	/// necesarias para generar el código QR en la UI.
	/// </summary>
	Task<MfaSetupInfo> BeginAuthenticatorSetupAsync(Guid userId, CancellationToken cancellationToken = default);

	/// <summary>
	/// Verifica el código introducido por el usuario durante la configuración
	/// inicial y, si es válido, activa el MFA en la cuenta.
	/// </summary>
	Task<bool> ConfirmAuthenticatorSetupAsync(Guid userId, string verificationCode, CancellationToken cancellationToken = default);

	/// <summary>
	/// Genera un nuevo lote de códigos de recuperación de un solo uso
	/// (invalida los anteriores). Debe llamarse tras activar MFA y mostrarse
	/// una única vez para que el usuario los guarde/descargue.
	/// </summary>
	Task<IReadOnlyCollection<string>> GenerateRecoveryCodesAsync(Guid userId, CancellationToken cancellationToken = default);

	/// <summary>
	/// Valida el código TOTP introducido durante el login (segundo factor) y,
	/// si es correcto, completa el inicio de sesión devolviendo el resultado
	/// con la redirección por rol.
	/// </summary>
	Task<LoginResult> ValidateAuthenticatorCodeAsync(string code, bool rememberMachine, CancellationToken cancellationToken = default);

	/// <summary>
	/// Valida un código de recuperación como alternativa al TOTP cuando el
	/// usuario ha perdido acceso a su autenticador.
	/// </summary>
	Task<LoginResult> ValidateRecoveryCodeAsync(string recoveryCode, CancellationToken cancellationToken = default);

	/// <summary>
	/// Desactiva MFA para el usuario (por ejemplo, tras verificar identidad
	/// mediante otro canal). Requiere que la política de rol no lo tenga como
	/// obligatorio.
	/// </summary>
	Task DisableAsync(Guid userId, CancellationToken cancellationToken = default);
}
