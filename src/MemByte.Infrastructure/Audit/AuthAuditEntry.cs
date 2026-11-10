namespace MemByte.Infrastructure.Audit;

/// <summary>
/// Registro inmutable de un evento de autenticación o seguridad (login,
/// fallo de login, bloqueo de cuenta, cambio de contraseña, configuración
/// de MFA, etc.). Sólo se inserta, nunca se actualiza ni elimina desde la
/// aplicación (append-only).
/// </summary>
public class AuthAuditEntry
{
	public Guid Id { get; private set; }

	/// <summary>
	/// Usuario afectado por el evento, si se pudo resolver (puede ser null en
	/// intentos de login con un email que no existe, para no filtrar información).
	/// </summary>
	public Guid? UserId { get; private set; }

	/// <summary>
	/// Tipo de evento: LoginSucceeded, LoginFailed, AccountLockedOut,
	/// PasswordChanged, MfaEnabled, MfaDisabled, RoleAssigned, SessionRevoked, etc.
	/// </summary>
	public string EventType { get; private set; } = string.Empty;

	public bool WasSuccessful { get; private set; }

	/// <summary>
	/// Motivo del fallo (si aplica), en texto seguro para auditoría interna
	/// (no se muestra directamente al usuario final).
	/// </summary>
	public string? FailureReason { get; private set; }

	public string? IpAddress { get; private set; }

	public string? UserAgent { get; private set; }

	public DateTimeOffset OccurredAt { get; private set; }

	private AuthAuditEntry()
	{
		// Requerido por EF Core.
	}

	public AuthAuditEntry(
		Guid? userId,
		string eventType,
		bool wasSuccessful,
		string? failureReason,
		string? ipAddress,
		string? userAgent)
	{
		if (string.IsNullOrWhiteSpace(eventType))
			throw new ArgumentException("El tipo de evento no puede estar vacío.", nameof(eventType));

		Id = Guid.NewGuid();
		UserId = userId;
		EventType = eventType;
		WasSuccessful = wasSuccessful;
		FailureReason = failureReason;
		IpAddress = ipAddress;
		UserAgent = userAgent;
		OccurredAt = DateTimeOffset.UtcNow;
	}
}
