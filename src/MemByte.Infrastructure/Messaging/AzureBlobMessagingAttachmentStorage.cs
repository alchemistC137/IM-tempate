using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using MemByte.Application.Messaging;
using Microsoft.Extensions.Options;

namespace MemByte.Infrastructure.Messaging;

/// <summary>
/// Almacena los adjuntos en un contenedor privado de Azure Blob Storage. La
/// ruta del blob se compone con identificadores opacos (cliente,
/// conversación, mensaje y un GUID propio), nunca con el nombre original del
/// archivo, para que la referencia no revele información ni sea adivinable.
/// El contenido sólo se sirve a través de <see cref="OpenReadAsync"/>, así
/// que no se emite ninguna URL pública ni SAS persistido.
/// </summary>
public class AzureBlobMessagingAttachmentStorage : IMessagingAttachmentStorage
{
	private readonly BlobContainerClient _containerClient;

	public AzureBlobMessagingAttachmentStorage(IOptions<MessagingAttachmentStorageOptions> options)
	{
		var settings = options.Value;

		if (string.IsNullOrWhiteSpace(settings.ConnectionString))
			throw new InvalidOperationException(
				"Falta 'Messaging:AttachmentStorage:ConnectionString' para el almacenamiento de adjuntos.");

		_containerClient = new BlobContainerClient(settings.ConnectionString, settings.ContainerName);
	}

	public async Task<string> SaveAsync(
		Guid? clientId,
		Guid conversationId,
		Guid messageId,
		NewAttachment attachment,
		CancellationToken cancellationToken = default)
	{
		// PublicAccessType.None: el contenedor nunca debe ser navegable.
		await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

		var reference = BuildReference(clientId, conversationId, messageId);
		var blobClient = _containerClient.GetBlobClient(reference);

		await blobClient.UploadAsync(
			attachment.Content,
			new BlobUploadOptions
			{
				HttpHeaders = new BlobHttpHeaders { ContentType = attachment.ContentType }
			},
			cancellationToken);

		return reference;
	}

	public async Task<Stream> OpenReadAsync(string storageReference, CancellationToken cancellationToken = default)
	{
		var blobClient = _containerClient.GetBlobClient(storageReference);
		return await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
	}

	public async Task DeleteAsync(string storageReference, CancellationToken cancellationToken = default)
	{
		var blobClient = _containerClient.GetBlobClient(storageReference);
		await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
	}

	internal static string BuildReference(Guid? clientId, Guid conversationId, Guid messageId)
	{
		var tenantSegment = clientId?.ToString() ?? "internal";
		return $"{tenantSegment}/{conversationId}/{messageId}/{Guid.NewGuid():N}";
	}
}
