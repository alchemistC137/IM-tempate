namespace MemByte.Domain.Messaging;

/// <summary>
/// Mensaje individual de una conversación. El borrado es lógico para no
/// romper la trazabilidad del hilo: el contenido se vacía pero se conserva la
/// marca de quién y cuándo escribió.
/// </summary>
public class Message
{
	public const int BodyMaxLength = 8000;

	private readonly List<MessageAttachment> _attachments = [];

	public Guid Id { get; private set; }

	public Guid ConversationId { get; private set; }

	/// <summary>
	/// Cuenta de Identity que envía el mensaje. Para mensajes de sistema se
	/// conserva el usuario que originó la acción.
	/// </summary>
	public Guid SenderUserId { get; private set; }

	public string Body { get; private set; } = string.Empty;

	/// <summary>
	/// Mensaje generado por la aplicación (cambio de estado, asignación, etc.)
	/// en lugar de escrito por una persona.
	/// </summary>
	public bool IsSystemMessage { get; private set; }

	public DateTimeOffset CreatedAt { get; private set; }

	public DateTimeOffset? EditedAt { get; private set; }

	public bool IsDeleted { get; private set; }

	public DateTimeOffset? DeletedAt { get; private set; }

	public IReadOnlyCollection<MessageAttachment> Attachments => _attachments;

	private Message()
	{
		// Requerido por EF Core.
	}

	internal Message(Guid conversationId, Guid senderUserId, string body, bool isSystemMessage)
	{
		if (senderUserId == Guid.Empty)
			throw new ArgumentException("El remitente del mensaje no es válido.", nameof(senderUserId));

		Id = Guid.NewGuid();
		ConversationId = conversationId;
		SenderUserId = senderUserId;
		IsSystemMessage = isSystemMessage;
		CreatedAt = DateTimeOffset.UtcNow;
		SetBody(body);
	}

	public void Edit(string body)
	{
		if (IsDeleted)
			throw new InvalidOperationException("No se puede editar un mensaje eliminado.");

		SetBody(body);
		EditedAt = DateTimeOffset.UtcNow;
	}

	/// <summary>
	/// Borrado lógico: elimina el contenido visible y los adjuntos asociados,
	/// pero mantiene el mensaje en el hilo para auditoría.
	/// </summary>
	public void SoftDelete()
	{
		IsDeleted = true;
		DeletedAt = DateTimeOffset.UtcNow;
		Body = string.Empty;
	}

	public MessageAttachment AddAttachment(string fileName, string contentType, long sizeInBytes, string storageReference)
	{
		if (IsDeleted)
			throw new InvalidOperationException("No se pueden añadir adjuntos a un mensaje eliminado.");

		var attachment = new MessageAttachment(Id, fileName, contentType, sizeInBytes, storageReference);
		_attachments.Add(attachment);
		return attachment;
	}

	private void SetBody(string body)
	{
		if (string.IsNullOrWhiteSpace(body))
			throw new ArgumentException("El mensaje no puede estar vacío.", nameof(body));

		body = body.Trim();
		if (body.Length > BodyMaxLength)
			throw new ArgumentException($"El mensaje no puede superar {BodyMaxLength} caracteres.", nameof(body));

		Body = body;
	}
}
