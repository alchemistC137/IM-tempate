using System.Security.Claims;
using MemByte.Application.Messaging;
using MemByte.Infrastructure.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;

namespace MemByte.Infrastructure.Messaging;

/// <summary>
/// Construye el <see cref="MessagingUserContext"/> a partir del estado de
/// autenticación de Blazor. El cliente y el permiso de respuesta se derivan
/// siempre del servidor (cuenta de Identity + roles), nunca de parámetros
/// enviados por el componente, para que la UI no pueda escalar privilegios.
/// </summary>
public class MessagingUserContextProvider : IMessagingUserContextProvider
{
	private readonly AuthenticationStateProvider _authenticationStateProvider;
	private readonly UserManager<ApplicationUser> _userManager;

	public MessagingUserContextProvider(
		AuthenticationStateProvider authenticationStateProvider,
		UserManager<ApplicationUser> userManager)
	{
		_authenticationStateProvider = authenticationStateProvider;
		_userManager = userManager;
	}

	public async Task<MessagingUserContext?> GetCurrentAsync(CancellationToken cancellationToken = default)
	{
		var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
		var principal = authState.User;

		if (principal?.Identity?.IsAuthenticated != true)
			return null;

		var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
		if (!Guid.TryParse(userIdValue, out var userId))
			return null;

		// Se relee el usuario de la base de datos en lugar de confiar en una
		// claim: si un administrador cambia el cliente o desactiva la cuenta,
		// el cambio surte efecto sin esperar a que caduque la cookie.
		var user = await _userManager.FindByIdAsync(userId.ToString());
		if (user is null || user.LockoutEnd > DateTimeOffset.UtcNow)
			return null;

		var roles = await _userManager.GetRolesAsync(user);
		var canReply = roles.Any(r => ApplicationRoleNames.MessagingResponders.Contains(r, StringComparer.OrdinalIgnoreCase));

		return new MessagingUserContext(
			user.Id,
			user.ClientId,
			IsInternalStaff: user.EmployeeId is not null,
			CanReply: canReply);
	}
}
