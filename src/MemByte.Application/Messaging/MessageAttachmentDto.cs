namespace MemByte.Application.Messaging;

/// <summary>
/// Metadatos de un adjunto. No se expone la referencia de almacenamiento: la
/// descarga se solicita por <see cref="Id"/> y el servicio genera un acceso
/// temporal tras revalidar los permisos.
/// </summary>
public sealed record MessageAttachmentDto(
	Guid Id,
	string FileName,
	string ContentType,
	long SizeInBytes,
	DateTimeOffset UploadedAt);
