namespace MemByte.Domain.Messaging;

/// <summary>
/// Adjunto de un mensaje. En la base de datos sólo se guarda una referencia
/// privada al blob (nunca una URL pública ni un SAS persistido): el acceso se
/// concede en el momento de la descarga, tras validar que el usuario es
/// participante de la conversación.
/// </summary>
public class MessageAttachment
{
	public const int FileNameMaxLength = 260;

	public Guid Id { get; private set; }

	public Guid MessageId { get; private set; }

	public string FileName { get; private set; } = string.Empty;

	public string ContentType { get; private set; } = string.Empty;

	public long SizeInBytes { get; private set; }

	/// <summary>
	/// Ruta/nombre del blob dentro del contenedor privado. No es navegable por
	/// sí sola: requiere credencial del servicio de almacenamiento.
	/// </summary>
	public string StorageReference { get; private set; } = string.Empty;

	public DateTimeOffset UploadedAt { get; private set; }

	private MessageAttachment()
	{
		// Requerido por EF Core.
	}

	internal MessageAttachment(Guid messageId, string fileName, string contentType, long sizeInBytes, string storageReference)
	{
		if (string.IsNullOrWhiteSpace(fileName))
			throw new ArgumentException("El nombre del archivo no puede estar vacío.", nameof(fileName));

		if (string.IsNullOrWhiteSpace(contentType))
			throw new ArgumentException("El tipo de contenido no puede estar vacío.", nameof(contentType));

		if (sizeInBytes <= 0)
			throw new ArgumentException("El tamaño del archivo debe ser mayor que cero.", nameof(sizeInBytes));

		if (string.IsNullOrWhiteSpace(storageReference))
			throw new ArgumentException("La referencia de almacenamiento no puede estar vacía.", nameof(storageReference));

		Id = Guid.NewGuid();
		MessageId = messageId;
		FileName = SanitizeFileName(fileName);
		ContentType = contentType;
		SizeInBytes = sizeInBytes;
		StorageReference = storageReference;
		UploadedAt = DateTimeOffset.UtcNow;
	}

	/// <summary>
	/// Elimina rutas del nombre original para evitar path traversal al
	/// reconstruir el nombre de descarga.
	/// </summary>
	private static string SanitizeFileName(string fileName)
	{
		var name = Path.GetFileName(fileName.Trim());
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("El nombre del archivo no es válido.", nameof(fileName));

		return name.Length > FileNameMaxLength ? name[^FileNameMaxLength..] : name;
	}
}
