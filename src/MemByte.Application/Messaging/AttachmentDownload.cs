namespace MemByte.Application.Messaging;

/// <summary>
/// Contenido de un adjunto autorizado para descarga puntual. El stream lo
/// consume y libera el llamante.
/// </summary>
public sealed record AttachmentDownload(
	string FileName,
	string ContentType,
	Stream Content);
