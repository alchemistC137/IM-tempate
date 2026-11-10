namespace MemByte.Infrastructure.Audit;

/// <summary>
/// Tipos de evento auditables del módulo de mensajería. Se usan constantes
/// para evitar strings mágicos y poder filtrar por tipo en el panel interno.
/// </summary>
public static class MessagingAuditEventTypes
{
	public const string ConversationCreated = "ConversationCreated";
	public const string MessageSent = "MessageSent";
	public const string MessageDeleted = "MessageDeleted";
	public const string ConversationRead = "ConversationRead";
	public const string ConversationStatusChanged = "ConversationStatusChanged";
	public const string AttachmentUploaded = "AttachmentUploaded";
	public const string AttachmentDownloaded = "AttachmentDownloaded";
	public const string AccessDenied = "AccessDenied";
	public const string RetentionPurge = "RetentionPurge";
}
