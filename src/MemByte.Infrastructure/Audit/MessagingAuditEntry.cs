namespace MemByte.Infrastructure.Audit;

/// <summary>
/// Registro inmutable (append-only) de una acción sobre el módulo de
/// mensajería: creación de conversaciones, envíos, lecturas, descargas de
/// adjuntos e intentos de acceso denegados. Permite investigar cualquier
/// sospecha de fuga de información sin almacenar el contenido de los mensajes.
/// </summary>
public class MessagingAuditEntry
{
	public Guid Id { get; private set; }

	/// <summary>
	/// Usuario que ejecutó la acción.
	/// </summary>
	public Guid UserId { get; private set; }

	/// <summary>
	/// Cliente al que pertenece la conversación afectada (nulo para las
	/// conversaciones internas de MemByte).
	/// </summary>
	public Guid? ClientId { get; private set; }

	public Guid? ConversationId { get; private set; }

	public Guid? MessageId { get; private set; }

	public string EventType { get; private set; } = string.Empty;

	public bool WasAllowed { get; private set; }

	/// <summary>
	/// Detalle técnico corto (motivo del rechazo, estado destino, etc.). Nunca
	/// debe contener el cuerpo del mensaje ni datos personales del adjunto.
	/// </summary>
	public string? Details { get; private set; }

	public DateTimeOffset OccurredAt { get; private set; }

	private MessagingAuditEntry()
	{
		// Requerido por EF Core.
	}

	public MessagingAuditEntry(
		Guid userId,
		Guid? clientId,
		Guid? conversationId,
		Guid? messageId,
		string eventType,
		bool wasAllowed,
		string? details = null)
	{
		if (string.IsNullOrWhiteSpace(eventType))
			throw new ArgumentException("El tipo de evento no puede estar vacío.", nameof(eventType));

		Id = Guid.NewGuid();
		UserId = userId;
		ClientId = clientId;
		ConversationId = conversationId;
		MessageId = messageId;
		EventType = eventType;
		WasAllowed = wasAllowed;
		Details = details;
		OccurredAt = DateTimeOffset.UtcNow;
	}
}
