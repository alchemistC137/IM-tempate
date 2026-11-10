namespace MemByte.Application.Messaging;

/// <summary>
/// Petición de envío de un mensaje a una conversación existente.
/// </summary>
public sealed record SendMessageRequest(
	Guid ConversationId,
	string Body,
	IReadOnlyList<NewAttachment>? Attachments = null);
