namespace MemByte.Application.Users;

/// <summary>
/// Datos editables de una cuenta de usuario existente: nombre a mostrar,
/// vínculos opcionales a Employee/Client, roles asignados y política de MFA.
/// No incluye email ni contraseña: el email es la clave de acceso (no se
/// permite cambiarlo desde este formulario para no invalidar el histórico de
/// auditoría) y la contraseña nunca se fija en texto plano desde el panel de
/// administración (ver ResetPasswordAsync, pendiente de PR3).
/// </summary>
public sealed record UpdateUserRequest(
	string DisplayName,
	Guid? EmployeeId,
	Guid? ClientId,
	IReadOnlyList<string> Roles,
	bool IsMfaRequired);
