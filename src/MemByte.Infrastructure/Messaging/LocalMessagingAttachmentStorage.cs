using MemByte.Application.Messaging;
using Microsoft.Extensions.Options;

namespace MemByte.Infrastructure.Messaging;

/// <summary>
/// Implementación de desarrollo que guarda los adjuntos en disco, usada
/// cuando no hay cuenta de Azure Storage configurada. Mantiene la misma
/// convención de referencias opacas y valida que la ruta resultante no se
/// salga de la carpeta raíz (protección frente a path traversal).
/// </summary>
public class LocalMessagingAttachmentStorage : IMessagingAttachmentStorage
{
	private readonly string _rootPath;

	public LocalMessagingAttachmentStorage(IOptions<MessagingAttachmentStorageOptions> options)
	{
		_rootPath = Path.GetFullPath(options.Value.LocalRootPath);
		Directory.CreateDirectory(_rootPath);
	}

	public async Task<string> SaveAsync(
		Guid? clientId,
		Guid conversationId,
		Guid messageId,
		NewAttachment attachment,
		CancellationToken cancellationToken = default)
	{
		var reference = AzureBlobMessagingAttachmentStorage.BuildReference(clientId, conversationId, messageId);
		var fullPath = ResolvePath(reference);

		Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

		await using var target = File.Create(fullPath);
		await attachment.Content.CopyToAsync(target, cancellationToken);

		return reference;
	}

	public Task<Stream> OpenReadAsync(string storageReference, CancellationToken cancellationToken = default)
	{
		Stream stream = File.OpenRead(ResolvePath(storageReference));
		return Task.FromResult(stream);
	}

	public Task DeleteAsync(string storageReference, CancellationToken cancellationToken = default)
	{
		var fullPath = ResolvePath(storageReference);
		if (File.Exists(fullPath))
			File.Delete(fullPath);

		return Task.CompletedTask;
	}

	private string ResolvePath(string storageReference)
	{
		var fullPath = Path.GetFullPath(Path.Combine(_rootPath, storageReference));

		if (!fullPath.StartsWith(_rootPath, StringComparison.Ordinal))
			throw new InvalidOperationException("La referencia de almacenamiento no es válida.");

		return fullPath;
	}
}
