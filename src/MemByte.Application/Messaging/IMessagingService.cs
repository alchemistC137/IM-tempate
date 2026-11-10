using MemByte.Domain.Messaging;

namespace MemByte.Application.Messaging;

/// <summary>
/// Servicio de mensajería interna de la intranet. Todas las operaciones
/// resuelven el usuario a partir de <see cref="IMessagingUserContextProvider"/>
/// y aplican el filtro por cliente antes de tocar datos, de modo que no
/// existe ninguna ruta que devuelva conversaciones de otro tenant.
/// </summary>
public interface IMessagingService
{
	/// <summary>
	/// Devuelve la bandeja del usuario autenticado, ordenada por conversación
	/// anclada y fecha del último mensaje.
	/// </summary>
	Task<IReadOnlyList<ConversationSummaryDto>> GetInboxAsync(
		ConversationStatus? status = null,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Número total de mensajes sin leer del usuario autenticado, para el
	/// badge de la barra de navegación.
	/// </summary>
	Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Devuelve el detalle de una conversación con la página de mensajes más
	/// reciente. Falla si el usuario no es participante activo.
	/// </summary>
	Task<MessagingOperationResult<ConversationDetailDto>> GetConversationAsync(
		Guid conversationId,
		int take = 50,
		CancellationToken cancellationToken = default);

	Task<MessagingOperationResult<ConversationDetailDto>> StartConversationAsync(
		StartConversationRequest request,
		CancellationToken cancellationToken = default);

	Task<MessagingOperationResult<MessageDto>> SendMessageAsync(
		SendMessageRequest request,
		CancellationToken cancellationToken = default);

	Task<MessagingOperationResult<bool>> MarkAsReadAsync(
		Guid conversationId,
		CancellationToken cancellationToken = default);

	Task<MessagingOperationResult<bool>> SetPinnedAsync(
		Guid conversationId,
		bool isPinned,
		CancellationToken cancellationToken = default);

	Task<MessagingOperationResult<bool>> SetMutedAsync(
		Guid conversationId,
		bool isMuted,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Cambia el estado del hilo. Reservado a los usuarios que pueden
	/// responder (administración/soporte).
	/// </summary>
	Task<MessagingOperationResult<bool>> ChangeStatusAsync(
		Guid conversationId,
		ConversationStatus status,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Abre el contenido de un adjunto tras revalidar que el usuario es
	/// participante de la conversación a la que pertenece.
	/// </summary>
	Task<MessagingOperationResult<AttachmentDownload>> OpenAttachmentAsync(
		Guid attachmentId,
		CancellationToken cancellationToken = default);
}
