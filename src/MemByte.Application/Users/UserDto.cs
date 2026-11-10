namespace MemByte.Application.Users;

/// <summary>
/// Modelo de lectura de una cuenta de usuario (interno o de cliente),
/// expuesto a la capa de presentación. Combina datos de la cuenta de
/// Identity con los vínculos opcionales a Employee/Client, sin exponer
/// detalles de implementación (hash de contraseña, security stamp, etc.).
/// </summary>
public sealed record UserDto(
	Guid Id,
	string Email,
	string DisplayName,
	Guid? EmployeeId,
	string? EmployeeFullName,
	Guid? ClientId,
	string? ClientName,
	IReadOnlyList<string> Roles,
	bool IsActive,
	bool IsLockedOut,
	DateTimeOffset? LastLoginAt,
	bool IsMfaRequired,
	bool TwoFactorEnabled);
