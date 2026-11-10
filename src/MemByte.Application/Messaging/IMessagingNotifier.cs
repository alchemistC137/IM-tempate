namespace MemByte.Application.Messaging;

/// <summary>
/// Aviso mínimo de novedad en una conversación. Se transmite por el canal en
/// tiempo real: deliberadamente no lleva el cuerpo del mensaje ni datos del
/// remitente, sólo lo imprescindible para que el cliente decida si debe
/// recargar el hilo. Así, aunque una suscripción fuese incorrecta, no habría
/// contenido que filtrar.
/// </summary>
public sealed record ConversationNotification(
	Guid ConversationId,
	Guid MessageId,
	DateTimeOffset OccurredAt);

/// <summary>
/// Publicador de avisos de mensajería en tiempo real.
/// </summary>
public interface IMessagingNotifier
{
	/// <summary>
	/// Notifica a los participantes indicados que la conversación tiene una
	/// novedad. El emisor y los participantes silenciados quedan excluidos por
	/// el llamante.
	/// </summary>
	Task NotifyConversationUpdatedAsync(
		IReadOnlyCollection<Guid> recipientUserIds,
		ConversationNotification notification,
		CancellationToken cancellationToken = default);
}
