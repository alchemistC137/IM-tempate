using MemByte.Domain.Messaging;

namespace MemByte.Application.Messaging;

/// <summary>
/// Petición de apertura de una conversación. El cliente propietario no se
/// recibe desde la UI: lo impone el servicio a partir del usuario
/// autenticado, para que nadie pueda crear hilos en otro tenant.
/// </summary>
public sealed record StartConversationRequest(
	string Subject,
	string Body,
	ConversationPriority Priority = ConversationPriority.Normal,
	IReadOnlyList<Guid>? AdditionalParticipantUserIds = null,
	IReadOnlyList<NewAttachment>? Attachments = null);
