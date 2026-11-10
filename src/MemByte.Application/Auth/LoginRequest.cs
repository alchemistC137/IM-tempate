namespace MemByte.Application.Auth;

/// <summary>
/// Datos de entrada para intentar un inicio de sesión.
/// </summary>
/// <param name="Email">Email o nombre de usuario introducido.</param>
/// <param name="Password">Contraseña en texto plano (sólo en memoria, nunca se persiste).</param>
/// <param name="RememberMe">Si se debe usar cookie persistente (sliding expiration extendida).</param>
/// <param name="IpAddress">Dirección IP del cliente, para auditoría y rate limiting.</param>
/// <param name="UserAgent">User-Agent del cliente, para auditoría y detección de nuevo dispositivo.</param>
public sealed record LoginRequest(
	string Email,
	string Password,
	bool RememberMe,
	string? IpAddress,
	string? UserAgent);
