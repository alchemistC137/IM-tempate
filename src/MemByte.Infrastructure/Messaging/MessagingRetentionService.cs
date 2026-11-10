using MemByte.Application.Messaging;
using MemByte.Infrastructure.Audit;
using MemByte.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MemByte.Infrastructure.Messaging;

/// <summary>
/// Purga las conversaciones que superan la política de retención configurada
/// (5 años por defecto, ajustable por despliegue). Elimina primero los blobs
/// de los adjuntos y después las filas, de modo que no queden ficheros
/// huérfanos accesibles en el almacenamiento.
/// </summary>
public class MessagingRetentionService
{
	private readonly MemByteDbContext _dbContext;
	private readonly IMessagingAttachmentStorage _attachmentStorage;
	private readonly MessagingOptions _options;
	private readonly ILogger<MessagingRetentionService> _logger;

	public MessagingRetentionService(
		MemByteDbContext dbContext,
		IMessagingAttachmentStorage attachmentStorage,
		IOptions<MessagingOptions> options,
		ILogger<MessagingRetentionService> logger)
	{
		_dbContext = dbContext;
		_attachmentStorage = attachmentStorage;
		_options = options.Value;
		_logger = logger;
	}

	/// <summary>
	/// Ejecuta una pasada de purga y devuelve el número de conversaciones
	/// eliminadas.
	/// </summary>
	public async Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default)
	{
		if (!_options.RetentionEnabled)
			return 0;

		var retentionYears = Math.Max(1, _options.RetentionYears);
		var threshold = DateTimeOffset.UtcNow.AddYears(-retentionYears);

		var expiredConversationIds = await _dbContext.Conversations
			.AsNoTracking()
			.Where(c => c.LastMessageAt < threshold)
			.Select(c => c.Id)
			.ToListAsync(cancellationToken);

		if (expiredConversationIds.Count == 0)
			return 0;

		var attachmentReferences = await (
			from attachment in _dbContext.MessageAttachments.AsNoTracking()
			join message in _dbContext.Messages.AsNoTracking()
				on attachment.MessageId equals message.Id
			where expiredConversationIds.Contains(message.ConversationId)
			select attachment.StorageReference)
			.ToListAsync(cancellationToken);

		foreach (var reference in attachmentReferences)
		{
			try
			{
				await _attachmentStorage.DeleteAsync(reference, cancellationToken);
			}
			catch (Exception ex)
			{
				// Un fallo al borrar un blob no debe impedir la purga del
				// resto: se registra para revisarlo manualmente.
				_logger.LogWarning(ex, "No se pudo eliminar el adjunto {Reference} durante la purga por retención.", reference);
			}
		}

		// Las filas hijas (participantes, mensajes y adjuntos) caen por
		// cascada al eliminar la conversación.
		var deleted = await _dbContext.Conversations
			.Where(c => expiredConversationIds.Contains(c.Id))
			.ExecuteDeleteAsync(cancellationToken);

		_dbContext.MessagingAuditEntries.Add(new MessagingAuditEntry(
			userId: Guid.Empty,
			clientId: null,
			conversationId: null,
			messageId: null,
			eventType: MessagingAuditEventTypes.RetentionPurge,
			wasAllowed: true,
			details: $"{deleted} conversaciones purgadas (retención de {retentionYears} años)."));

		await _dbContext.SaveChangesAsync(cancellationToken);

		_logger.LogInformation(
			"Purga por retención de mensajería: {Count} conversaciones eliminadas anteriores a {Threshold}.",
			deleted,
			threshold);

		return deleted;
	}
}

/// <summary>
/// Ejecuta <see cref="MessagingRetentionService"/> de forma periódica según
/// <c>Messaging:RetentionScanInterval</c>.
/// </summary>
public class MessagingRetentionBackgroundService : BackgroundService
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly MessagingOptions _options;
	private readonly ILogger<MessagingRetentionBackgroundService> _logger;

	public MessagingRetentionBackgroundService(
		IServiceScopeFactory scopeFactory,
		IOptions<MessagingOptions> options,
		ILogger<MessagingRetentionBackgroundService> logger)
	{
		_scopeFactory = scopeFactory;
		_options = options.Value;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!_options.RetentionEnabled)
		{
			_logger.LogInformation("Purga por retención de mensajería deshabilitada por configuración.");
			return;
		}

		var interval = _options.RetentionScanInterval > TimeSpan.Zero
			? _options.RetentionScanInterval
			: TimeSpan.FromHours(24);

		using var timer = new PeriodicTimer(interval);

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				using var scope = _scopeFactory.CreateScope();
				var retentionService = scope.ServiceProvider.GetRequiredService<MessagingRetentionService>();
				await retentionService.PurgeExpiredAsync(stoppingToken);
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error durante la purga por retención de mensajería.");
			}

			if (!await timer.WaitForNextTickAsync(stoppingToken))
				break;
		}
	}
}
