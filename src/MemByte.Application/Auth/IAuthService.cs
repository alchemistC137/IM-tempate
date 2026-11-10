namespace MemByte.Application.Auth;

/// <summary>
/// Servicio de aplicación para el flujo de login, desacoplado de ASP.NET
/// Core Identity para poder testear la lógica de negocio de forma aislada.
/// </summary>
public interface IAuthService
{
	Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

	/// <summary>
	/// Fija una nueva contraseña a partir de un token de invitación (alta de
	/// usuario) o de reseteo (solicitado por un Admin), ambos generados por
	/// <c>IUserService</c>. No requiere sesión iniciada.
	/// </summary>
	Task<SetPasswordResult> SetPasswordAsync(SetPasswordRequest request, CancellationToken cancellationToken = default);
}
