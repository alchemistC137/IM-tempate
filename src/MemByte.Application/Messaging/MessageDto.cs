namespace MemByte.Application.Messaging;

/// <summary>
/// Mensaje visible dentro de un hilo. El remitente se expone con su nombre
/// para mostrar, no con su email ni con datos de su ficha, para minimizar la
/// información difundida por la UI y por las notificaciones en tiempo real.
/// </summary>
public sealed record MessageDto(
	Guid Id,
	Guid ConversationId,
	Guid SenderUserId,
	string SenderDisplayName,
	string Body,
	bool IsSystemMessage,
	bool IsMine,
	bool IsDeleted,
	DateTimeOffset CreatedAt,
	DateTimeOffset? EditedAt,
	IReadOnlyList<MessageAttachmentDto> Attachments);
