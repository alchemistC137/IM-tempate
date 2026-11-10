namespace MemByte.Infrastructure.Identity;

/// <summary>
/// Registro histórico de un hash de contraseña usado anteriormente por un
/// usuario. Se conserva un número limitado de entradas (ver
/// <see cref="PasswordHistoryValidator"/>) para impedir la reutilización de
/// las últimas N contraseñas, tal y como exige la política de seguridad.
/// </summary>
public class PasswordHistoryEntry
{
	public Guid Id { get; private set; }

	public Guid UserId { get; private set; }

	public string PasswordHash { get; private set; } = string.Empty;

	public DateTimeOffset CreatedAt { get; private set; }

	private PasswordHistoryEntry()
	{
		// Requerido por EF Core.
	}

	public PasswordHistoryEntry(Guid userId, string passwordHash)
	{
		if (string.IsNullOrWhiteSpace(passwordHash))
			throw new ArgumentException("El hash de contraseña no puede estar vacío.", nameof(passwordHash));

		Id = Guid.NewGuid();
		UserId = userId;
		PasswordHash = passwordHash;
		CreatedAt = DateTimeOffset.UtcNow;
	}
}
