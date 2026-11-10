namespace MemByte.Infrastructure.Messaging;

/// <summary>
/// Configuración del almacenamiento de adjuntos de mensajería (sección
/// <c>Messaging:AttachmentStorage</c>). El contenedor debe ser privado: la
/// aplicación nunca publica URLs directas, sirve el contenido tras validar
/// permisos.
/// </summary>
public sealed class MessagingAttachmentStorageOptions
{
	public const string SectionName = "Messaging:AttachmentStorage";

	/// <summary>
	/// Cadena de conexión de la cuenta de Azure Storage. Si está vacía se usa
	/// el almacenamiento local en disco (solo desarrollo).
	/// </summary>
	public string? ConnectionString { get; set; }

	public string ContainerName { get; set; } = "messaging-attachments";

	/// <summary>
	/// Carpeta usada por la implementación local de desarrollo.
	/// </summary>
	public string LocalRootPath { get; set; } = ".messaging-attachments";
}
