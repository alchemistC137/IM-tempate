using MemByte.Infrastructure.Audit;
using MemByte.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace MemByte.Infrastructure.Messaging;

/// <summary>
/// Registra las acciones del módulo de mensajería en la tabla de auditoría
/// (append-only) y las publica como log estructurado. Los intentos de acceso
/// denegados se registran siempre: son la señal principal de un intento de
/// acceso cruzado entre clientes.
/// </summary>
public class MessagingAuditLogger : IMessagingAuditLogger
{
	private readonly MemByteDbContext _dbContext;
	private readonly ILogger<MessagingAuditLogger> _logger;

	public MessagingAuditLogger(MemByteDbContext dbContext, ILogger<MessagingAuditLogger> logger)
	{
		_dbContext = dbContext;
		_logger = logger;
	}

	public void Record(
		Guid userId,
		Guid? clientId,
		Guid? conversationId,
		Guid? messageId,
		string eventType,
		bool wasAllowed,
		string? details = null)
	{
		_dbContext.MessagingAuditEntries.Add(new MessagingAuditEntry(
			userId,
			clientId,
			conversationId,
			messageId,
			eventType,
			wasAllowed,
			details));

		if (wasAllowed)
		{
			_logger.LogInformation(
				"Mensajería: {EventType} por {UserId} sobre conversación {ConversationId}.",
				eventType,
				userId,
				conversationId);
		}
		else
		{
			_logger.LogWarning(
				"Mensajería: acceso denegado ({EventType}) para {UserId} sobre conversación {ConversationId}. {Details}",
				eventType,
				userId,
				conversationId,
				details);
		}
	}

	public async Task RecordAndSaveAsync(
		Guid userId,
		Guid? clientId,
		Guid? conversationId,
		Guid? messageId,
		string eventType,
		bool wasAllowed,
		string? details = null,
		CancellationToken cancellationToken = default)
	{
		Record(userId, clientId, conversationId, messageId, eventType, wasAllowed, details);
		await _dbContext.SaveChangesAsync(cancellationToken);
	}
}
