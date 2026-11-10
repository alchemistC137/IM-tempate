using MemByte.Domain.Messaging;

namespace MemByte.Application.Messaging;

/// <summary>
/// Detalle de una conversación con su página de mensajes. Sólo se construye
/// después de validar que el solicitante es participante activo del hilo.
/// </summary>
public sealed record ConversationDetailDto(
	Guid Id,
	string Subject,
	ConversationStatus Status,
	ConversationPriority Priority,
	DateTimeOffset CreatedAt,
	DateTimeOffset LastMessageAt,
	bool CanReply,
	bool IsPinned,
	bool IsMuted,
	IReadOnlyList<MessageDto> Messages);
