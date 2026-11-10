namespace MemByte.Application.Messaging;

/// <summary>
/// Almacenamiento de adjuntos de mensajería. La implementación por defecto
/// usa un contenedor privado de Azure Blob Storage; la abstracción permite
/// sustituirlo por otro proveedor en despliegues de cliente.
/// </summary>
public interface IMessagingAttachmentStorage
{
	/// <summary>
	/// Sube el contenido y devuelve la referencia privada que se persiste en
	/// la base de datos.
	/// </summary>
	Task<string> SaveAsync(
		Guid? clientId,
		Guid conversationId,
		Guid messageId,
		NewAttachment attachment,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Abre el contenido previamente almacenado. El llamante ya ha validado
	/// los permisos del usuario.
	/// </summary>
	Task<Stream> OpenReadAsync(string storageReference, CancellationToken cancellationToken = default);

	Task DeleteAsync(string storageReference, CancellationToken cancellationToken = default);
}
