using MemByte.Domain.Messaging;

namespace MemByte.Application.Messaging;

/// <summary>
/// Fila de la bandeja de conversaciones para un usuario concreto. Contiene
/// sólo lo necesario para pintar la lista: nunca incluye el cuerpo completo
/// de los mensajes ni datos de otros clientes.
/// </summary>
public sealed record ConversationSummaryDto(
	Guid Id,
	string Subject,
	ConversationStatus Status,
	ConversationPriority Priority,
	DateTimeOffset LastMessageAt,
	string LastMessagePreview,
	string? LastMessageSenderDisplayName,
	int MessageCount,
	int UnreadCount,
	DateTimeOffset? LastReadAt,
	bool IsPinned,
	bool IsMuted,
	bool CanReply);
