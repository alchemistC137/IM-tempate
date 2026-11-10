namespace MemByte.Application.Messaging;

/// <summary>
/// Adjunto entrante todavía no persistido. El contenido se entrega como
/// stream para no cargar el archivo completo en memoria.
/// </summary>
public sealed record NewAttachment(
	string FileName,
	string ContentType,
	long SizeInBytes,
	Stream Content);
