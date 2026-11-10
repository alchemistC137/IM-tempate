namespace MemByte.Application.Users;

/// <summary>
/// Datos para dar de alta una cuenta de usuario (interna o de cliente). No
/// incluye contraseña: se crea sin contraseña utilizable y se envía un
/// enlace de activación de un solo uso (token de reseteo de Identity) para
/// que el propio usuario la fije (ver flujo de invitación, PR3 del plan de
/// gestión de usuarios).
/// </summary>
public sealed record CreateUserRequest(
	string Email,
	string DisplayName,
	Guid? EmployeeId,
	Guid? ClientId,
	IReadOnlyList<string> Roles);
