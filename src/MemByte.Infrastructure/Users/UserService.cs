using MemByte.Application.Notifications;
using MemByte.Application.Configuration;
using MemByte.Application.Users;
using MemByte.Domain.Clients;
using MemByte.Domain.Employees;
using MemByte.Infrastructure.Audit;
using MemByte.Infrastructure.Identity;
using MemByte.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MemByte.Infrastructure.Users;

/// <summary>
/// Implementación de <see cref="IUserService"/> sobre ASP.NET Core Identity
/// (<see cref="UserManager{TUser}"/>) para la cuenta y sus roles, y
/// <see cref="MemByteDbContext"/> para los vínculos con Employee/Client, las
/// sesiones activas y el registro de auditoría.
/// </summary>
public class UserService : IUserService
{
	/// <summary>
	/// Ruta (sin dominio) de la pantalla donde el usuario fija su contraseña
	/// a partir de un token de invitación/reseteo. Debe coincidir con la ruta
	/// de <c>SetPassword.razor</c>.
	/// </summary>
	private const string SetPasswordPath = "/account/set-password";

	private readonly UserManager<ApplicationUser> _userManager;
	private readonly MemByteDbContext _dbContext;
	private readonly IEmailSender _emailSender;
	private readonly ISecurityTelemetryPublisher? _telemetryPublisher;
	private readonly IntranetOptions _intranet;

	public UserService(
		UserManager<ApplicationUser> userManager,
		MemByteDbContext dbContext,
		IEmailSender emailSender,
		ISecurityTelemetryPublisher? telemetryPublisher = null,
		IOptions<IntranetOptions>? intranetOptions = null)
	{
		_intranet = intranetOptions?.Value ?? new IntranetOptions();
		_userManager = userManager;
		_dbContext = dbContext;
		_emailSender = emailSender;
		_telemetryPublisher = telemetryPublisher;
	}

	public async Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		var users = await _dbContext.Users
			.AsNoTracking()
			.OrderBy(u => u.DisplayName)
			.ToListAsync(cancellationToken);

		var employeeNames = await _dbContext.Employees
			.AsNoTracking()
			.ToDictionaryAsync(e => e.Id, e => e.FullName, cancellationToken);

		var clientNames = await _dbContext.Clients
			.AsNoTracking()
			.ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

		var result = new List<UserDto>(users.Count);
		foreach (var user in users)
		{
			var roles = await _userManager.GetRolesAsync(user);
			var employeeName = user.EmployeeId is not null && employeeNames.TryGetValue(user.EmployeeId.Value, out var eName)
				? eName
				: null;
			var clientName = user.ClientId is not null && clientNames.TryGetValue(user.ClientId.Value, out var cName)
				? cName
				: null;

			result.Add(ToDto(user, employeeName, clientName, roles));
		}

		return result;
	}

	public async Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
		return user is null ? null : await BuildDtoAsync(user, cancellationToken);
	}

	public async Task<UserOperationResult> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(request.Email))
			return UserOperationResult.Failure("El email es obligatorio.");

		if (string.IsNullOrWhiteSpace(request.DisplayName))
			return UserOperationResult.Failure("El nombre a mostrar es obligatorio.");

		if (await _userManager.FindByEmailAsync(request.Email) is not null)
			return UserOperationResult.Failure("Ya existe un usuario con ese email.");

		if (request.EmployeeId is not null &&
			!await _dbContext.Employees.AsNoTracking().AnyAsync(e => e.Id == request.EmployeeId, cancellationToken))
			return UserOperationResult.Failure("El empleado seleccionado no existe.");

		if (request.ClientId is not null &&
			!await _dbContext.Clients.AsNoTracking().AnyAsync(c => c.Id == request.ClientId, cancellationToken))
			return UserOperationResult.Failure("El cliente seleccionado no existe.");

		var invalidRoles = request.Roles.Where(r => !ApplicationRoleNames.All.Contains(r)).ToList();
		if (invalidRoles.Count > 0)
			return UserOperationResult.Failure($"Rol(es) no válido(s): {string.Join(", ", invalidRoles)}.");

		var user = new ApplicationUser
		{
			UserName = request.Email,
			Email = request.Email,
			// El alta la realiza un Admin ya autenticado: se confía en el
			// email introducido sin exigir una confirmación adicional.
			EmailConfirmed = true,
			DisplayName = request.DisplayName,
			EmployeeId = request.EmployeeId,
			ClientId = request.ClientId
		};

		// Se crea sin contraseña utilizable: el propio usuario la fija a
		// través del enlace de activación (token de reseteo).
		var createResult = await _userManager.CreateAsync(user);
		if (!createResult.Succeeded)
			return UserOperationResult.Failure(createResult.Errors.Select(e => e.Description).ToArray());

		if (request.Roles.Count > 0)
			await _userManager.AddToRolesAsync(user, request.Roles);

		await LogAsync(user.Id, "UserCreatedByAdmin", success: true, null);

		var actionUrlPath = await SendActivationLinkAsync(
			user,
			$"Activa tu cuenta en {_intranet.ApplicationName}",
			$"Se ha creado una cuenta para ti en {_intranet.ApplicationName}. Completa el siguiente enlace para fijar tu " +
			"contraseña y activar el acceso:",
			cancellationToken);

		return UserOperationResult.Success(await BuildDtoAsync(user, cancellationToken), actionUrlPath);
	}

	public async Task<UserOperationResult> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default)
	{
		var user = await _userManager.FindByIdAsync(id.ToString());
		if (user is null)
			return UserOperationResult.Failure("El usuario no existe.");

		if (string.IsNullOrWhiteSpace(request.DisplayName))
			return UserOperationResult.Failure("El nombre a mostrar es obligatorio.");

		if (request.EmployeeId is not null &&
			!await _dbContext.Employees.AsNoTracking().AnyAsync(e => e.Id == request.EmployeeId, cancellationToken))
			return UserOperationResult.Failure("El empleado seleccionado no existe.");

		if (request.ClientId is not null &&
			!await _dbContext.Clients.AsNoTracking().AnyAsync(c => c.Id == request.ClientId, cancellationToken))
			return UserOperationResult.Failure("El cliente seleccionado no existe.");

		var invalidRoles = request.Roles.Where(r => !ApplicationRoleNames.All.Contains(r)).ToList();
		if (invalidRoles.Count > 0)
			return UserOperationResult.Failure($"Rol(es) no válido(s): {string.Join(", ", invalidRoles)}.");

		user.DisplayName = request.DisplayName;
		user.EmployeeId = request.EmployeeId;
		user.ClientId = request.ClientId;
		user.IsMfaRequired = request.IsMfaRequired;

		var updateResult = await _userManager.UpdateAsync(user);
		if (!updateResult.Succeeded)
			return UserOperationResult.Failure(updateResult.Errors.Select(e => e.Description).ToArray());

		var currentRoles = await _userManager.GetRolesAsync(user);
		var rolesToAdd = request.Roles.Except(currentRoles, StringComparer.OrdinalIgnoreCase).ToList();
		var rolesToRemove = currentRoles.Except(request.Roles, StringComparer.OrdinalIgnoreCase).ToList();

		if (rolesToRemove.Count > 0)
			await _userManager.RemoveFromRolesAsync(user, rolesToRemove);

		if (rolesToAdd.Count > 0)
			await _userManager.AddToRolesAsync(user, rolesToAdd);

		if (rolesToAdd.Count > 0 || rolesToRemove.Count > 0)
			await LogAsync(user.Id, "UserRoleChanged", success: true, null);

		return UserOperationResult.Success(await BuildDtoAsync(user, cancellationToken));
	}

	public async Task<UserOperationResult> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
	{
		var user = await _userManager.FindByIdAsync(id.ToString());
		if (user is null)
			return UserOperationResult.Failure("El usuario no existe.");

		user.LockoutEnabled = true;
		// Sentinela de "desactivado por Admin": distinto de un bloqueo
		// temporal por intentos fallidos (que expira en minutos).
		user.LockoutEnd = isActive ? null : DateTimeOffset.MaxValue;

		var updateResult = await _userManager.UpdateAsync(user);
		if (!updateResult.Succeeded)
			return UserOperationResult.Failure(updateResult.Errors.Select(e => e.Description).ToArray());

		if (isActive)
		{
			await LogAsync(user.Id, "UserReactivated", success: true, null);
		}
		else
		{
			// Al desactivar, se revocan también las sesiones activas: no
			// tendría sentido bloquear el acceso futuro pero dejar una
			// sesión ya iniciada funcionando.
			await RevokeSessionsInternalAsync(user, cancellationToken);
			await LogAsync(user.Id, "UserDeactivated", success: true, null);
		}

		return UserOperationResult.Success(await BuildDtoAsync(user, cancellationToken));
	}

	public async Task<UserOperationResult> RevokeSessionsAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var user = await _userManager.FindByIdAsync(id.ToString());
		if (user is null)
			return UserOperationResult.Failure("El usuario no existe.");

		await RevokeSessionsInternalAsync(user, cancellationToken);
		await LogAsync(user.Id, "SessionsRevokedByAdmin", success: true, null);

		return UserOperationResult.Success(await BuildDtoAsync(user, cancellationToken));
	}

	public async Task<UserOperationResult> ResetPasswordAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var user = await _userManager.FindByIdAsync(id.ToString());
		if (user is null)
			return UserOperationResult.Failure("El usuario no existe.");

		await LogAsync(user.Id, "PasswordResetRequestedByAdmin", success: true, null);

		var actionUrlPath = await SendActivationLinkAsync(
			user,
			$"Restablece tu contraseña en {_intranet.ApplicationName}",
			$"Un administrador ha solicitado el restablecimiento de tu contraseña en {_intranet.ApplicationName}. Completa el " +
			"siguiente enlace para fijar una nueva contraseña:",
			cancellationToken);

		return UserOperationResult.Success(await BuildDtoAsync(user, cancellationToken), actionUrlPath);
	}

	public async Task<IReadOnlyList<UserAuditEntryDto>> GetAuditHistoryAsync(Guid id, int maxEntries = 50, CancellationToken cancellationToken = default)
	{
		return await _dbContext.AuthAuditEntries
			.AsNoTracking()
			.Where(e => e.UserId == id)
			.OrderByDescending(e => e.OccurredAt)
			.Take(maxEntries)
			.Select(e => new UserAuditEntryDto(e.Id, e.EventType, e.WasSuccessful, e.FailureReason, e.OccurredAt))
			.ToListAsync(cancellationToken);
	}

	/// <summary>
	/// Genera un token de reseteo de contraseña, envía (o registra, vía el
	/// stub de email) el enlace de activación y devuelve su ruta relativa
	/// para que la UI pueda mostrarla también directamente al Admin.
	/// </summary>
	private async Task<string> SendActivationLinkAsync(ApplicationUser user, string subject, string introBody, CancellationToken cancellationToken)
	{
		var token = await _userManager.GeneratePasswordResetTokenAsync(user);
		var actionUrlPath = $"{SetPasswordPath}?userId={user.Id}&token={Uri.EscapeDataString(token)}";

		await _emailSender.SendAsync(new EmailMessage(
			user.Email ?? string.Empty,
			subject,
			$"{introBody}\n\n{actionUrlPath}\n\n(Añade el dominio de tu intranet delante de la ruta anterior. " +
			"El enlace caduca en 24 horas y solo puede usarse una vez.)"),
			cancellationToken);

		return actionUrlPath;
	}

	private async Task RevokeSessionsInternalAsync(ApplicationUser user, CancellationToken cancellationToken)
	{
		var sessions = await _dbContext.UserSessions
			.Where(s => s.UserId == user.Id && !s.IsRevoked)
			.ToListAsync(cancellationToken);

		foreach (var session in sessions)
			session.Revoke();

		// Invalida de inmediato cualquier cookie de autenticación ya
		// emitida: SignInManager valida el SecurityStamp en cada petición.
		await _userManager.UpdateSecurityStampAsync(user);

		await _dbContext.SaveChangesAsync(cancellationToken);
	}

	private async Task<UserDto> BuildDtoAsync(ApplicationUser user, CancellationToken cancellationToken)
	{
		Employee? employee = null;
		if (user.EmployeeId is not null)
			employee = await _dbContext.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == user.EmployeeId, cancellationToken);

		Client? client = null;
		if (user.ClientId is not null)
			client = await _dbContext.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == user.ClientId, cancellationToken);

		var roles = await _userManager.GetRolesAsync(user);

		return ToDto(user, employee?.FullName, client?.Name, roles);
	}

	private async Task LogAsync(Guid userId, string eventType, bool success, string? failureReason)
	{
		var entry = new AuthAuditEntry(userId, eventType, success, failureReason, ipAddress: null, userAgent: null);
		_dbContext.AuthAuditEntries.Add(entry);
		await _dbContext.SaveChangesAsync();

		_telemetryPublisher?.Publish(entry);
	}

	private static bool IsPermanentlyDeactivated(ApplicationUser user) =>
		user.LockoutEnd.HasValue && user.LockoutEnd.Value == DateTimeOffset.MaxValue;

	private static bool IsCurrentlyLockedOut(ApplicationUser user) =>
		user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;

	private static UserDto ToDto(ApplicationUser user, string? employeeName, string? clientName, IList<string> roles) => new(
		user.Id,
		user.Email ?? string.Empty,
		user.DisplayName,
		user.EmployeeId,
		employeeName,
		user.ClientId,
		clientName,
		roles.ToArray(),
		!IsPermanentlyDeactivated(user),
		IsCurrentlyLockedOut(user),
		user.LastLoginAt,
		user.IsMfaRequired,
		user.TwoFactorEnabled);
}
