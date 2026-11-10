namespace MemByte.Application.Messaging;

/// <summary>
/// Opciones configurables del módulo de mensajería (sección
/// <c>Messaging</c> de la configuración). Los valores por defecto son los
/// acordados para la plantilla: retención de 5 años y adjuntos limitados a
/// tipos ofimáticos habituales.
/// </summary>
public sealed class MessagingOptions
{
	public const string SectionName = "Messaging";

	/// <summary>
	/// Años que se conservan las conversaciones antes de purgarse.
	/// </summary>
	public int RetentionYears { get; set; } = 5;

	/// <summary>
	/// Indica si el proceso de purga por retención está activo.
	/// </summary>
	public bool RetentionEnabled { get; set; } = true;

	/// <summary>
	/// Intervalo entre ejecuciones del proceso de purga.
	/// </summary>
	public TimeSpan RetentionScanInterval { get; set; } = TimeSpan.FromHours(24);

	/// <summary>
	/// Tamaño máximo por adjunto (10 MB por defecto).
	/// </summary>
	public long MaxAttachmentSizeBytes { get; set; } = 10 * 1024 * 1024;

	public int MaxAttachmentsPerMessage { get; set; } = 5;

	/// <summary>
	/// Lista blanca de tipos MIME admitidos. Se usa lista blanca (y no negra)
	/// para evitar la subida de contenido ejecutable.
	/// </summary>
	public string[] AllowedAttachmentContentTypes { get; set; } =
	[
		"application/pdf",
		"image/png",
		"image/jpeg",
		"image/gif",
		"text/plain",
		"text/csv",
		"application/vnd.openxmlformats-officedocument.wordprocessingml.document",
		"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
		"application/vnd.openxmlformats-officedocument.presentationml.presentation"
	];

	/// <summary>
	/// Número máximo de mensajes devueltos en una sola página del hilo.
	/// </summary>
	public int MaxMessagesPerPage { get; set; } = 100;
}
