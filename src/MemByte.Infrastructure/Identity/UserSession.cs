namespace MemByte.Infrastructure.Identity;

/// <summary>
/// Metadatos de una sesión activa de usuario, usados para el panel de
/// "sesiones activas", la revocación forzada por un Admin y el límite de
/// sesiones concurrentes por usuario.
/// </summary>
public class UserSession
{
	public Guid Id { get; private set; }

	public Guid UserId { get; private set; }

	/// <summary>
	/// Identificador estable de la sesión de autenticación (claim de sesión),
	/// usado para invalidar una sesión concreta sin afectar a las demás.
	/// </summary>
	public string SessionToken { get; private set; } = string.Empty;

	public string? IpAddress { get; private set; }

	public string? UserAgent { get; private set; }

	public DateTimeOffset CreatedAt { get; private set; }

	public DateTimeOffset LastActivityAt { get; private set; }

	public bool IsRevoked { get; private set; }

	private UserSession()
	{
		// Requerido por EF Core.
	}

	public UserSession(Guid userId, string sessionToken, string? ipAddress, string? userAgent)
	{
		if (string.IsNullOrWhiteSpace(sessionToken))
			throw new ArgumentException("El token de sesión no puede estar vacío.", nameof(sessionToken));

		Id = Guid.NewGuid();
		UserId = userId;
		SessionToken = sessionToken;
		IpAddress = ipAddress;
		UserAgent = userAgent;
		CreatedAt = DateTimeOffset.UtcNow;
		LastActivityAt = CreatedAt;
		IsRevoked = false;
	}

	public void TouchActivity()
	{
		LastActivityAt = DateTimeOffset.UtcNow;
	}

	public void Revoke()
	{
		IsRevoked = true;
	}
}
