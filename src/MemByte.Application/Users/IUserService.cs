namespace MemByte.Application.Users;

/// <summary>
/// Servicio de aplicación para la gestión de cuentas de usuario (internas o
/// de cliente) desde el panel de Administración. Complementa a
/// <c>IEmployeeService</c> e <c>IClientService</c>: estos gestionan la
/// ficha (RRHH/cliente), mientras que este servicio gestiona la cuenta de
/// acceso (Identity) y su vínculo con dichas fichas.
/// </summary>
public interface IUserService
{
	Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken cancellationToken = default);

	Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

	/// <summary>
	/// Da de alta una cuenta sin contraseña utilizable y envía (o, mientras
	/// no haya proveedor de email real, expone en <see cref="UserOperationResult.ActionUrlPath"/>)
	/// un enlace de activación de un solo uso para que el usuario fije su
	/// contraseña.
	/// </summary>
	Task<UserOperationResult> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default);

	Task<UserOperationResult> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default);

	/// <summary>
	/// Activa o desactiva la cuenta. La desactivación no elimina al usuario
	/// (se conserva el histórico de auditoría y sesiones): bloquea el acceso
	/// de forma indefinida y revoca cualquier sesión activa.
	/// </summary>
	Task<UserOperationResult> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);

	/// <summary>
	/// Genera un nuevo token de reseteo de contraseña de un solo uso y lo
	/// envía (o lo expone al Admin, ver <see cref="UserOperationResult.ActionUrlPath"/>),
	/// sin cambiar la contraseña actual hasta que el usuario complete el
	/// flujo. No revoca sesiones activas por sí solo.
	/// </summary>
	Task<UserOperationResult> ResetPasswordAsync(Guid id, CancellationToken cancellationToken = default);

	/// <summary>
	/// Revoca todas las sesiones activas del usuario e invalida su cookie de
	/// autenticación actual, sin afectar al estado de activación de la cuenta.
	/// </summary>
	Task<UserOperationResult> RevokeSessionsAsync(Guid id, CancellationToken cancellationToken = default);

	/// <summary>
	/// Devuelve el historial de auditoría del usuario (altas, cambios de rol,
	/// activación/desactivación, resets de contraseña, sesiones revocadas,
	/// intentos de login, etc.), del más reciente al más antiguo. Devuelve
	/// una lista vacía si el usuario no existe o no tiene eventos registrados.
	/// </summary>
	Task<IReadOnlyList<UserAuditEntryDto>> GetAuditHistoryAsync(Guid id, int maxEntries = 50, CancellationToken cancellationToken = default);
}
