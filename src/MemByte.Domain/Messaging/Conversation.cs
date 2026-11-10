namespace MemByte.Domain.Messaging;

/// <summary>
/// Hilo de mensajería interna de la intranet. Es la unidad de aislamiento:
/// toda consulta de mensajería debe filtrar primero por <see cref="ClientId"/>
/// para impedir que un usuario de un cliente vea conversaciones de otro.
/// <para>
/// <see cref="ClientId"/> nulo identifica una conversación interna de MemByte
/// (la instancia que administra la propia empresa); un valor concreto
/// identifica la conversación de un cliente de la plantilla.
/// </para>
/// </summary>
public class Conversation
{
	public const int SubjectMaxLength = 200;
	public const int PreviewMaxLength = 280;

	private readonly List<ConversationParticipant> _participants = [];
	private readonly List<Message> _messages = [];

	public Guid Id { get; private set; }

	/// <summary>
	/// Cliente propietario de la conversación. Nulo para las conversaciones
	/// internas de MemByte. Nunca debe modificarse tras la creación: cambiarlo
	/// movería el histórico completo a otro tenant.
	/// </summary>
	public Guid? ClientId { get; private set; }

	public string Subject { get; private set; } = string.Empty;

	public ConversationStatus Status { get; private set; } = ConversationStatus.Open;

	public ConversationPriority Priority { get; private set; } = ConversationPriority.Normal;

	/// <summary>
	/// Usuario (cuenta de Identity) que abrió la conversación.
	/// </summary>
	public Guid CreatedByUserId { get; private set; }

	public DateTimeOffset CreatedAt { get; private set; }

	/// <summary>
	/// Marca del último mensaje; es el criterio de orden de la bandeja.
	/// </summary>
	public DateTimeOffset LastMessageAt { get; private set; }

	/// <summary>
	/// Extracto corto del último mensaje para la lista de conversaciones. Se
	/// almacena truncado para no tener que cargar el cuerpo completo (y para
	/// no arrastrar contenido extenso a las notificaciones).
	/// </summary>
	public string LastMessagePreview { get; private set; } = string.Empty;

	public Guid? LastMessageSenderUserId { get; private set; }

	/// <summary>
	/// Número total de mensajes visibles, para paginación y métricas.
	/// </summary>
	public int MessageCount { get; private set; }

	public DateTimeOffset? ClosedAt { get; private set; }

	public IReadOnlyCollection<ConversationParticipant> Participants => _participants;

	public IReadOnlyCollection<Message> Messages => _messages;

	private Conversation()
	{
		// Requerido por EF Core.
	}

	public Conversation(Guid? clientId, string subject, Guid createdByUserId, ConversationPriority priority = ConversationPriority.Normal)
	{
		if (createdByUserId == Guid.Empty)
			throw new ArgumentException("El autor de la conversación no es válido.", nameof(createdByUserId));

		Id = Guid.NewGuid();
		ClientId = clientId;
		CreatedByUserId = createdByUserId;
		Priority = priority;
		CreatedAt = DateTimeOffset.UtcNow;
		LastMessageAt = CreatedAt;
		SetSubject(subject);
	}

	public void UpdateSubject(string subject) => SetSubject(subject);

	public void ChangePriority(ConversationPriority priority) => Priority = priority;

	/// <summary>
	/// Añade un participante. Se ignora si el usuario ya participa, para que la
	/// operación sea idempotente al reabrir o reasignar conversaciones.
	/// </summary>
	public ConversationParticipant AddParticipant(Guid userId, ConversationParticipantRole role, bool canReply)
	{
		var existing = _participants.FirstOrDefault(p => p.UserId == userId);
		if (existing is not null)
		{
			existing.Rejoin(role, canReply);
			return existing;
		}

		var participant = new ConversationParticipant(Id, userId, role, canReply);
		_participants.Add(participant);
		return participant;
	}

	public ConversationParticipant? FindParticipant(Guid userId)
		=> _participants.FirstOrDefault(p => p.UserId == userId && !p.HasLeft);

	/// <summary>
	/// Registra un mensaje nuevo en el hilo, actualizando los datos de
	/// seguimiento (último mensaje, extracto, contadores de no leídos).
	/// </summary>
	public Message AddMessage(Guid senderUserId, string body, bool isSystemMessage = false)
	{
		if (Status == ConversationStatus.Closed)
			throw new InvalidOperationException("La conversación está cerrada y no admite nuevos mensajes.");

		var message = new Message(Id, senderUserId, body, isSystemMessage);
		_messages.Add(message);

		MessageCount++;
		LastMessageAt = message.CreatedAt;
		LastMessageSenderUserId = senderUserId;
		LastMessagePreview = BuildPreview(message.Body);

		if (Status == ConversationStatus.Resolved)
			Status = ConversationStatus.Open;

		foreach (var participant in _participants.Where(p => !p.HasLeft))
		{
			if (participant.UserId == senderUserId)
				participant.MarkReadAt(message.CreatedAt);
			else
				participant.RegisterUnread();
		}

		return message;
	}

	public void MarkPending() => Status = ConversationStatus.Pending;

	public void MarkResolved()
	{
		Status = ConversationStatus.Resolved;
		ClosedAt = null;
	}

	public void Close()
	{
		Status = ConversationStatus.Closed;
		ClosedAt = DateTimeOffset.UtcNow;
	}

	public void Reopen()
	{
		Status = ConversationStatus.Open;
		ClosedAt = null;
	}

	/// <summary>
	/// Indica si la conversación pertenece al ámbito del cliente indicado.
	/// Se usa como última barrera de aislamiento antes de devolver datos.
	/// </summary>
	public bool BelongsToClient(Guid? clientId) => ClientId == clientId;

	private void SetSubject(string subject)
	{
		if (string.IsNullOrWhiteSpace(subject))
			throw new ArgumentException("El asunto de la conversación no puede estar vacío.", nameof(subject));

		subject = subject.Trim();
		Subject = subject.Length > SubjectMaxLength ? subject[..SubjectMaxLength] : subject;
	}

	private static string BuildPreview(string body)
	{
		var normalized = body.Replace('\r', ' ').Replace('\n', ' ').Trim();
		return normalized.Length > PreviewMaxLength ? normalized[..PreviewMaxLength] : normalized;
	}
}
