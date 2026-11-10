namespace MemByte.Application.Messaging;

/// <summary>
/// Resuelve el <see cref="MessagingUserContext"/> del usuario autenticado a
/// partir del estado de autenticación actual. Se abstrae para que la capa de
/// aplicación no dependa de ASP.NET Core.
/// </summary>
public interface IMessagingUserContextProvider
{
	/// <summary>
	/// Devuelve el contexto del usuario autenticado, o <c>null</c> si no hay
	/// sesión válida.
	/// </summary>
	Task<MessagingUserContext?> GetCurrentAsync(CancellationToken cancellationToken = default);
}
