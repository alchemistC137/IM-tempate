using MemByte.Application.Messaging;
using Microsoft.AspNetCore.SignalR;

namespace MemByte.Web.Messaging;

/// <summary>
/// Publica los avisos de mensajería a través de <see cref="MessagingHub"/>.
/// El envío se dirige a los usuarios concretos autorizados, nunca por
/// difusión, y el payload contiene sólo identificadores.
/// </summary>
public class SignalRMessagingNotifier : IMessagingNotifier
{
	private readonly IHubContext<MessagingHub> _hubContext;

	public SignalRMessagingNotifier(IHubContext<MessagingHub> hubContext)
	{
		_hubContext = hubContext;
	}

	public async Task NotifyConversationUpdatedAsync(
		IReadOnlyCollection<Guid> recipientUserIds,
		ConversationNotification notification,
		CancellationToken cancellationToken = default)
	{
		if (recipientUserIds.Count == 0)
			return;

		var userIds = recipientUserIds
			.Select(id => id.ToString())
			.ToList();

		await _hubContext.Clients
			.Users(userIds)
			.SendAsync(MessagingHub.ConversationUpdatedMethod, notification, cancellationToken);
	}
}
