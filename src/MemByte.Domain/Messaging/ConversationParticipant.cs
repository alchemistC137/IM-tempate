namespace MemByte.Domain.Messaging;

/// <summary>
/// Vínculo entre una cuenta de usuario y una conversación. Es la lista de
/// control de acceso del hilo: si un usuario no es participante activo, no
/// puede leer ni escribir en la conversación, aunque comparta cliente.
/// </summary>
public class ConversationParticipant
{
	public Guid Id { get; private set; }

	public Guid ConversationId { get; private set; }

	public Guid UserId { get; private set; }

	public ConversationParticipantRole Role { get; private set; }

	/// <summary>
	/// Indica si este participante puede responder. Sólo los empleados con rol
	/// de administración/soporte se dan de alta con este permiso; el resto
	/// participa en modo lectura.
	/// </summary>
	public bool CanReply { get; private set; }

	public DateTimeOffset JoinedAt { get; private set; }

	public DateTimeOffset? LeftAt { get; private set; }

	/// <summary>
	/// Momento en que el participante leyó por última vez el hilo.
	/// </summary>
	public DateTimeOffset? LastReadAt { get; private set; }

	/// <summary>
	/// Mensajes sin leer acumulados; permite pintar el badge de la bandeja sin
	/// recorrer todos los mensajes.
	/// </summary>
	public int UnreadCount { get; private set; }

	/// <summary>Conversación anclada en la parte superior de la bandeja.</summary>
	public bool IsPinned { get; private set; }

	/// <summary>Silencia las notificaciones en tiempo real de este hilo.</summary>
	public bool IsMuted { get; private set; }

	public bool HasLeft => LeftAt is not null;

	private ConversationParticipant()
	{
		// Requerido por EF Core.
	}

	internal ConversationParticipant(Guid conversationId, Guid userId, ConversationParticipantRole role, bool canReply)
	{
		if (userId == Guid.Empty)
			throw new ArgumentException("El participante no es válido.", nameof(userId));

		Id = Guid.NewGuid();
		ConversationId = conversationId;
		UserId = userId;
		Role = role;
		CanReply = canReply;
		JoinedAt = DateTimeOffset.UtcNow;
	}

	internal void Rejoin(ConversationParticipantRole role, bool canReply)
	{
		Role = role;
		CanReply = canReply;
		LeftAt = null;
	}

	internal void RegisterUnread()
	{
		UnreadCount++;
	}

	internal void MarkReadAt(DateTimeOffset readAt)
	{
		LastReadAt = readAt;
		UnreadCount = 0;
	}

	public void MarkAsRead() => MarkReadAt(DateTimeOffset.UtcNow);

	public void SetPinned(bool isPinned) => IsPinned = isPinned;

	public void SetMuted(bool isMuted) => IsMuted = isMuted;

	public void Leave() => LeftAt = DateTimeOffset.UtcNow;
}
