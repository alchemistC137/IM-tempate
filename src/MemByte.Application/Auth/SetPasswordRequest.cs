namespace MemByte.Application.Auth;

/// <summary>
/// Datos para fijar la contraseña de una cuenta a partir de un token de
/// invitación (alta de usuario) o de reseteo (solicitado por un Admin). Es
/// un flujo público (sin sesión iniciada): <see cref="Token"/> es el que
/// autoriza la operación, no una contraseña actual.
/// </summary>
public sealed record SetPasswordRequest(
	Guid UserId,
	string Token,
	string NewPassword,
	string? IpAddress,
	string? UserAgent);
