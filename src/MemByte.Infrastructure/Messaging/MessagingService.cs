using MemByte.Application.Messaging;
using MemByte.Domain.Messaging;
using MemByte.Infrastructure.Audit;
using MemByte.Infrastructure.Identity;
using MemByte.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MemByte.Infrastructure.Messaging;

/// <summary>
/// Implementación de <see cref="IMessagingService"/> sobre
/// <see cref="MemByteDbContext"/>, siguiendo el estilo del resto de servicios
/// (sin repositorio genérico adicional).
/// <para>
/// Reglas de aislamiento aplicadas en todas las operaciones:
/// 1) el usuario se resuelve en servidor, nunca se acepta desde la UI;
/// 2) toda consulta filtra por <c>Conversation.ClientId</c> del usuario;
/// 3) además, el usuario debe ser participante activo del hilo;
/// 4) los rechazos devuelven siempre el mismo mensaje, sin distinguir entre
///    "no existe" y "no tienes acceso", para no confirmar la existencia de
///    conversaciones ajenas.
/// </para>
/// </summary>
public class MessagingService : IMessagingService
{
	private readonly MemByteDbContext _dbContext;
	private readonly IMessagingUserContextProvider _userContextProvider;
	private readonly IMessagingAttachmentStorage _attachmentStorage;
	private readonly IMessagingAuditLogger _auditLogger;
	private readonly IMessagingNotifier _notifier;
	private readonly MessagingOptions _options;

	public MessagingService(
		MemByteDbContext dbContext,
		IMessagingUserContextProvider userContextProvider,
		IMessagingAttachmentStorage attachmentStorage,
		IMessagingAuditLogger auditLogger,
		IMessagingNotifier notifier,
		IOptions<MessagingOptions> options)
	{
		_dbContext = dbContext;
		_userContextProvider = userContextProvider;
		_attachmentStorage = attachmentStorage;
		_auditLogger = auditLogger;
		_notifier = notifier;
		_options = options.Value;
	}

	public async Task<IReadOnlyList<ConversationSummaryDto>> GetInboxAsync(
		ConversationStatus? status = null,
		CancellationToken cancellationToken = default)
	{
		var context = await _userContextProvider.GetCurrentAsync(cancellationToken);
		if (context is null)
			return [];

		var query =
			from participant in _dbContext.ConversationParticipants.AsNoTracking()
			join conversation in _dbContext.Conversations.AsNoTracking()
				on participant.ConversationId equals conversation.Id
			where participant.UserId == context.UserId
				&& participant.LeftAt == null
				&& conversation.ClientId == context.ClientId
			select new { conversation, participant };

		if (status is not null)
			query = query.Where(x => x.conversation.Status == status);

		var rows = await query
			.OrderByDescending(x => x.participant.IsPinned)
			.ThenByDescending(x => x.conversation.LastMessageAt)
			.ToListAsync(cancellationToken);

		var senderIds = rows
			.Where(x => x.conversation.LastMessageSenderUserId is not null)
			.Select(x => x.conversation.LastMessageSenderUserId!.Value)
			.Distinct()
			.ToList();

		var senderNames = await GetDisplayNamesAsync(senderIds, cancellationToken);

		return rows
			.Select(x => new ConversationSummaryDto(
				x.conversation.Id,
				x.conversation.Subject,
				x.conversation.Status,
				x.conversation.Priority,
				x.conversation.LastMessageAt,
				x.conversation.LastMessagePreview,
				x.conversation.LastMessageSenderUserId is not null
					? senderNames.GetValueOrDefault(x.conversation.LastMessageSenderUserId.Value)
					: null,
				x.conversation.MessageCount,
				x.participant.UnreadCount,
				x.participant.LastReadAt,
				x.participant.IsPinned,
				x.participant.IsMuted,
				x.participant.CanReply && x.conversation.Status != ConversationStatus.Closed))
			.ToList();
	}

	public async Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default)
	{
		var context = await _userContextProvider.GetCurrentAsync(cancellationToken);
		if (context is null)
			return 0;

		return await (
			from participant in _dbContext.ConversationParticipants.AsNoTracking()
			join conversation in _dbContext.Conversations.AsNoTracking()
				on participant.ConversationId equals conversation.Id
			where participant.UserId == context.UserId
				&& participant.LeftAt == null
				&& conversation.ClientId == context.ClientId
			select participant.UnreadCount)
			.SumAsync(cancellationToken);
	}

	public async Task<MessagingOperationResult<ConversationDetailDto>> GetConversationAsync(
		Guid conversationId,
		int take = 50,
		CancellationToken cancellationToken = default)
	{
		var context = await _userContextProvider.GetCurrentAsync(cancellationToken);
		if (context is null)
			return MessagingOperationResult<ConversationDetailDto>.NotAccessible();

		var access = await ResolveAccessAsync(context, conversationId, tracking: false, cancellationToken);
		if (access is null)
		{
			await AuditDeniedAsync(context, conversationId, cancellationToken);
			return MessagingOperationResult<ConversationDetailDto>.NotAccessible();
		}

		take = Math.Clamp(take, 1, _options.MaxMessagesPerPage);

		var messages = await _dbContext.Messages
			.AsNoTracking()
			.Where(m => m.ConversationId == conversationId)
			.OrderByDescending(m => m.CreatedAt)
			.Take(take)
			.ToListAsync(cancellationToken);

		messages.Reverse();

		var messageIds = messages.Select(m => m.Id).ToList();

		var attachments = await _dbContext.MessageAttachments
			.AsNoTracking()
			.Where(a => messageIds.Contains(a.MessageId))
			.ToListAsync(cancellationToken);

		var senderNames = await GetDisplayNamesAsync(
			messages.Select(m => m.SenderUserId).Distinct().ToList(),
			cancellationToken);

		var messageDtos = messages
			.Select(m => ToDto(m, context.UserId, senderNames, attachments))
			.ToList();

		var detail = new ConversationDetailDto(
			access.Conversation.Id,
			access.Conversation.Subject,
			access.Conversation.Status,
			access.Conversation.Priority,
			access.Conversation.CreatedAt,
			access.Conversation.LastMessageAt,
			access.Participant.CanReply && access.Conversation.Status != ConversationStatus.Closed,
			access.Participant.IsPinned,
			access.Participant.IsMuted,
			messageDtos);

		return MessagingOperationResult<ConversationDetailDto>.Success(detail);
	}

	public async Task<MessagingOperationResult<ConversationDetailDto>> StartConversationAsync(
		StartConversationRequest request,
		CancellationToken cancellationToken = default)
	{
		var context = await _userContextProvider.GetCurrentAsync(cancellationToken);
		if (context is null)
			return MessagingOperationResult<ConversationDetailDto>.NotAccessible();

		var attachmentError = ValidateAttachments(request.Attachments);
		if (attachmentError is not null)
			return MessagingOperationResult<ConversationDetailDto>.Failure(attachmentError);

		Conversation conversation;
		Message message;
		try
		{
			// El cliente se toma del usuario autenticado: la UI no puede
			// elegir en qué tenant se crea la conversación.
			conversation = new Conversation(context.ClientId, request.Subject, context.UserId, request.Priority);
			conversation.AddParticipant(context.UserId, ConversationParticipantRole.Requester, context.CanReply);

			foreach (var participantUserId in await ResolveAdditionalParticipantsAsync(context, request.AdditionalParticipantUserIds, cancellationToken))
				conversation.AddParticipant(participantUserId.UserId, ConversationParticipantRole.Agent, participantUserId.CanReply);

			message = conversation.AddMessage(context.UserId, request.Body);
		}
		catch (ArgumentException ex)
		{
			return MessagingOperationResult<ConversationDetailDto>.Failure(ex.Message);
		}

		_dbContext.Conversations.Add(conversation);

		await StoreAttachmentsAsync(context, conversation, message, request.Attachments, cancellationToken);

		_auditLogger.Record(
			context.UserId,
			context.ClientId,
			conversation.Id,
			message.Id,
			MessagingAuditEventTypes.ConversationCreated,
			wasAllowed: true);

		await _dbContext.SaveChangesAsync(cancellationToken);

		await NotifyAsync(conversation, message, context.UserId, cancellationToken);

		return await GetConversationAsync(conversation.Id, cancellationToken: cancellationToken);
	}

	public async Task<MessagingOperationResult<MessageDto>> SendMessageAsync(
		SendMessageRequest request,
		CancellationToken cancellationToken = default)
	{
		var context = await _userContextProvider.GetCurrentAsync(cancellationToken);
		if (context is null)
			return MessagingOperationResult<MessageDto>.NotAccessible();

		var access = await ResolveAccessAsync(context, request.ConversationId, tracking: true, cancellationToken);
		if (access is null)
		{
			await AuditDeniedAsync(context, request.ConversationId, cancellationToken);
			return MessagingOperationResult<MessageDto>.NotAccessible();
		}

		// Doble comprobación: el permiso general del rol y el permiso concreto
		// del participante dentro del hilo.
		if (!access.Participant.CanReply || !context.CanReply)
		{
			await _auditLogger.RecordAndSaveAsync(
				context.UserId,
				context.ClientId,
				request.ConversationId,
				null,
				MessagingAuditEventTypes.AccessDenied,
				wasAllowed: false,
				"Intento de respuesta sin permiso.",
				cancellationToken);

			return MessagingOperationResult<MessageDto>.Failure("No tienes permiso para responder en esta conversación.");
		}

		var attachmentError = ValidateAttachments(request.Attachments);
		if (attachmentError is not null)
			return MessagingOperationResult<MessageDto>.Failure(attachmentError);

		Message message;
		try
		{
			message = access.Conversation.AddMessage(context.UserId, request.Body);
		}
		catch (ArgumentException ex)
		{
			return MessagingOperationResult<MessageDto>.Failure(ex.Message);
		}
		catch (InvalidOperationException ex)
		{
			return MessagingOperationResult<MessageDto>.Failure(ex.Message);
		}

		await StoreAttachmentsAsync(context, access.Conversation, message, request.Attachments, cancellationToken);

		_auditLogger.Record(
			context.UserId,
			context.ClientId,
			access.Conversation.Id,
			message.Id,
			MessagingAuditEventTypes.MessageSent,
			wasAllowed: true);

		await _dbContext.SaveChangesAsync(cancellationToken);

		await NotifyAsync(access.Conversation, message, context.UserId, cancellationToken);

		var senderNames = await GetDisplayNamesAsync([context.UserId], cancellationToken);
		var attachments = await _dbContext.MessageAttachments
			.AsNoTracking()
			.Where(a => a.MessageId == message.Id)
			.ToListAsync(cancellationToken);

		return MessagingOperationResult<MessageDto>.Success(
			ToDto(message, context.UserId, senderNames, attachments));
	}

	public async Task<MessagingOperationResult<bool>> MarkAsReadAsync(
		Guid conversationId,
		CancellationToken cancellationToken = default)
	{
		var context = await _userContextProvider.GetCurrentAsync(cancellationToken);
		if (context is null)
			return MessagingOperationResult<bool>.NotAccessible();

		var access = await ResolveAccessAsync(context, conversationId, tracking: true, cancellationToken);
		if (access is null)
		{
			await AuditDeniedAsync(context, conversationId, cancellationToken);
			return MessagingOperationResult<bool>.NotAccessible();
		}

		access.Participant.MarkAsRead();

		_auditLogger.Record(
			context.UserId,
			context.ClientId,
			conversationId,
			null,
			MessagingAuditEventTypes.ConversationRead,
			wasAllowed: true);

		await _dbContext.SaveChangesAsync(cancellationToken);
		return MessagingOperationResult<bool>.Success(true);
	}

	public Task<MessagingOperationResult<bool>> SetPinnedAsync(
		Guid conversationId,
		bool isPinned,
		CancellationToken cancellationToken = default)
		=> UpdateParticipantPreferenceAsync(
			conversationId,
			participant => participant.SetPinned(isPinned),
			cancellationToken);

	public Task<MessagingOperationResult<bool>> SetMutedAsync(
		Guid conversationId,
		bool isMuted,
		CancellationToken cancellationToken = default)
		=> UpdateParticipantPreferenceAsync(
			conversationId,
			participant => participant.SetMuted(isMuted),
			cancellationToken);

	public async Task<MessagingOperationResult<bool>> ChangeStatusAsync(
		Guid conversationId,
		ConversationStatus status,
		CancellationToken cancellationToken = default)
	{
		var context = await _userContextProvider.GetCurrentAsync(cancellationToken);
		if (context is null)
			return MessagingOperationResult<bool>.NotAccessible();

		var access = await ResolveAccessAsync(context, conversationId, tracking: true, cancellationToken);
		if (access is null)
		{
			await AuditDeniedAsync(context, conversationId, cancellationToken);
			return MessagingOperationResult<bool>.NotAccessible();
		}

		if (!context.CanReply || !access.Participant.CanReply)
		{
			await _auditLogger.RecordAndSaveAsync(
				context.UserId,
				context.ClientId,
				conversationId,
				null,
				MessagingAuditEventTypes.AccessDenied,
				wasAllowed: false,
				"Intento de cambio de estado sin permiso.",
				cancellationToken);

			return MessagingOperationResult<bool>.Failure("No tienes permiso para cambiar el estado de esta conversación.");
		}

		switch (status)
		{
			case ConversationStatus.Open:
				access.Conversation.Reopen();
				break;
			case ConversationStatus.Pending:
				access.Conversation.MarkPending();
				break;
			case ConversationStatus.Resolved:
				access.Conversation.MarkResolved();
				break;
			case ConversationStatus.Closed:
				access.Conversation.Close();
				break;
		}

		_auditLogger.Record(
			context.UserId,
			context.ClientId,
			conversationId,
			null,
			MessagingAuditEventTypes.ConversationStatusChanged,
			wasAllowed: true,
			status.ToString());

		await _dbContext.SaveChangesAsync(cancellationToken);
		return MessagingOperationResult<bool>.Success(true);
	}

	public async Task<MessagingOperationResult<AttachmentDownload>> OpenAttachmentAsync(
		Guid attachmentId,
		CancellationToken cancellationToken = default)
	{
		var context = await _userContextProvider.GetCurrentAsync(cancellationToken);
		if (context is null)
			return MessagingOperationResult<AttachmentDownload>.NotAccessible();

		// Se resuelve el adjunto junto con su conversación para revalidar el
		// cliente y la participación antes de tocar el almacenamiento.
		var row = await (
			from attachment in _dbContext.MessageAttachments.AsNoTracking()
			join message in _dbContext.Messages.AsNoTracking()
				on attachment.MessageId equals message.Id
			join conversation in _dbContext.Conversations.AsNoTracking()
				on message.ConversationId equals conversation.Id
			where attachment.Id == attachmentId
				&& conversation.ClientId == context.ClientId
			select new { attachment, conversation })
			.FirstOrDefaultAsync(cancellationToken);

		if (row is null)
		{
			await AuditDeniedAsync(context, null, cancellationToken);
			return MessagingOperationResult<AttachmentDownload>.NotAccessible();
		}

		var isParticipant = await _dbContext.ConversationParticipants
			.AsNoTracking()
			.AnyAsync(
				p => p.ConversationId == row.conversation.Id
					&& p.UserId == context.UserId
					&& p.LeftAt == null,
				cancellationToken);

		if (!isParticipant)
		{
			await AuditDeniedAsync(context, row.conversation.Id, cancellationToken);
			return MessagingOperationResult<AttachmentDownload>.NotAccessible();
		}

		var content = await _attachmentStorage.OpenReadAsync(row.attachment.StorageReference, cancellationToken);

		await _auditLogger.RecordAndSaveAsync(
			context.UserId,
			context.ClientId,
			row.conversation.Id,
			row.attachment.MessageId,
			MessagingAuditEventTypes.AttachmentDownloaded,
			wasAllowed: true,
			cancellationToken: cancellationToken);

		return MessagingOperationResult<AttachmentDownload>.Success(
			new AttachmentDownload(row.attachment.FileName, row.attachment.ContentType, content));
	}

	private async Task<MessagingOperationResult<bool>> UpdateParticipantPreferenceAsync(
		Guid conversationId,
		Action<ConversationParticipant> apply,
		CancellationToken cancellationToken)
	{
		var context = await _userContextProvider.GetCurrentAsync(cancellationToken);
		if (context is null)
			return MessagingOperationResult<bool>.NotAccessible();

		var access = await ResolveAccessAsync(context, conversationId, tracking: true, cancellationToken);
		if (access is null)
		{
			await AuditDeniedAsync(context, conversationId, cancellationToken);
			return MessagingOperationResult<bool>.NotAccessible();
		}

		apply(access.Participant);
		await _dbContext.SaveChangesAsync(cancellationToken);
		return MessagingOperationResult<bool>.Success(true);
	}

	/// <summary>
	/// Resuelve conversación y participación del usuario aplicando el filtro
	/// por cliente. Devuelve <c>null</c> tanto si la conversación no existe
	/// como si el usuario no tiene acceso: el llamante no debe distinguirlos.
	/// </summary>
	private async Task<ConversationAccess?> ResolveAccessAsync(
		MessagingUserContext context,
		Guid conversationId,
		bool tracking,
		CancellationToken cancellationToken)
	{
		var conversations = tracking
			// Con seguimiento se cargan participantes y mensajes porque las
			// reglas de dominio (contadores de no leídos y alta de mensajes)
			// operan sobre esas colecciones; sin cargarlas, EF no reconocería
			// correctamente las entidades nuevas del agregado.
			? _dbContext.Conversations
				.Include(c => c.Participants)
				.Include(c => c.Messages)
			: _dbContext.Conversations.AsNoTracking();

		var conversation = await conversations
			.FirstOrDefaultAsync(
				c => c.Id == conversationId && c.ClientId == context.ClientId,
				cancellationToken);

		if (conversation is null)
			return null;

		var participants = tracking
			? _dbContext.ConversationParticipants
			: _dbContext.ConversationParticipants.AsNoTracking();

		var participant = await participants
			.FirstOrDefaultAsync(
				p => p.ConversationId == conversationId
					&& p.UserId == context.UserId
					&& p.LeftAt == null,
				cancellationToken);

		return participant is null ? null : new ConversationAccess(conversation, participant);
	}

	/// <summary>
	/// Filtra los participantes propuestos: sólo se admiten cuentas del mismo
	/// cliente, para que no se pueda "invitar" a un usuario de otro tenant a
	/// una conversación y filtrarle así el contenido.
	/// </summary>
	private async Task<IReadOnlyList<(Guid UserId, bool CanReply)>> ResolveAdditionalParticipantsAsync(
		MessagingUserContext context,
		IReadOnlyList<Guid>? candidateUserIds,
		CancellationToken cancellationToken)
	{
		if (candidateUserIds is null || candidateUserIds.Count == 0)
			return [];

		var ids = candidateUserIds.Where(id => id != context.UserId).Distinct().ToList();
		if (ids.Count == 0)
			return [];

		var users = await _dbContext.Users
			.AsNoTracking()
			.Where(u => ids.Contains(u.Id) && u.ClientId == context.ClientId)
			.Select(u => u.Id)
			.ToListAsync(cancellationToken);

		if (users.Count == 0)
			return [];

		var responderRoleIds = await _dbContext.Roles
			.AsNoTracking()
			.Where(r => r.Name != null && ApplicationRoleNames.MessagingResponders.Contains(r.Name))
			.Select(r => r.Id)
			.ToListAsync(cancellationToken);

		var responderUserIds = await _dbContext.UserRoles
			.AsNoTracking()
			.Where(ur => users.Contains(ur.UserId) && responderRoleIds.Contains(ur.RoleId))
			.Select(ur => ur.UserId)
			.ToListAsync(cancellationToken);

		return users
			.Select(id => (UserId: id, CanReply: responderUserIds.Contains(id)))
			.ToList();
	}

	private string? ValidateAttachments(IReadOnlyList<NewAttachment>? attachments)
	{
		if (attachments is null || attachments.Count == 0)
			return null;

		if (attachments.Count > _options.MaxAttachmentsPerMessage)
			return $"No se pueden adjuntar más de {_options.MaxAttachmentsPerMessage} archivos por mensaje.";

		foreach (var attachment in attachments)
		{
			if (attachment.SizeInBytes > _options.MaxAttachmentSizeBytes)
				return $"El archivo '{attachment.FileName}' supera el tamaño máximo permitido.";

			// Lista blanca de tipos: evita la subida de contenido ejecutable.
			if (!_options.AllowedAttachmentContentTypes.Contains(attachment.ContentType, StringComparer.OrdinalIgnoreCase))
				return $"El tipo de archivo '{attachment.ContentType}' no está permitido.";
		}

		return null;
	}

	private async Task StoreAttachmentsAsync(
		MessagingUserContext context,
		Conversation conversation,
		Message message,
		IReadOnlyList<NewAttachment>? attachments,
		CancellationToken cancellationToken)
	{
		if (attachments is null || attachments.Count == 0)
			return;

		foreach (var attachment in attachments)
		{
			var reference = await _attachmentStorage.SaveAsync(
				conversation.ClientId,
				conversation.Id,
				message.Id,
				attachment,
				cancellationToken);

			message.AddAttachment(
				attachment.FileName,
				attachment.ContentType,
				attachment.SizeInBytes,
				reference);

			_auditLogger.Record(
				context.UserId,
				context.ClientId,
				conversation.Id,
				message.Id,
				MessagingAuditEventTypes.AttachmentUploaded,
				wasAllowed: true);
		}
	}

	/// <summary>
	/// Notifica a los participantes activos, excluyendo al emisor y a quienes
	/// hayan silenciado el hilo. El aviso no lleva contenido del mensaje.
	/// </summary>
	private async Task NotifyAsync(
		Conversation conversation,
		Message message,
		Guid senderUserId,
		CancellationToken cancellationToken)
	{
		var recipients = await _dbContext.ConversationParticipants
			.AsNoTracking()
			.Where(p => p.ConversationId == conversation.Id
				&& p.UserId != senderUserId
				&& p.LeftAt == null
				&& !p.IsMuted)
			.Select(p => p.UserId)
			.ToListAsync(cancellationToken);

		if (recipients.Count == 0)
			return;

		await _notifier.NotifyConversationUpdatedAsync(
			recipients,
			new ConversationNotification(conversation.Id, message.Id, message.CreatedAt),
			cancellationToken);
	}

	private Task AuditDeniedAsync(
		MessagingUserContext context,
		Guid? conversationId,
		CancellationToken cancellationToken)
		=> _auditLogger.RecordAndSaveAsync(
			context.UserId,
			context.ClientId,
			conversationId,
			null,
			MessagingAuditEventTypes.AccessDenied,
			wasAllowed: false,
			"Conversación inexistente o fuera del ámbito del usuario.",
			cancellationToken);

	private async Task<Dictionary<Guid, string>> GetDisplayNamesAsync(
		IReadOnlyCollection<Guid> userIds,
		CancellationToken cancellationToken)
	{
		if (userIds.Count == 0)
			return [];

		// Sólo se expone el nombre para mostrar: nunca el email ni otros datos
		// de la cuenta.
		return await _dbContext.Users
			.AsNoTracking()
			.Where(u => userIds.Contains(u.Id))
			.ToDictionaryAsync(
				u => u.Id,
				u => string.IsNullOrWhiteSpace(u.DisplayName) ? "Usuario" : u.DisplayName,
				cancellationToken);
	}

	private static MessageDto ToDto(
		Message message,
		Guid currentUserId,
		IReadOnlyDictionary<Guid, string> senderNames,
		IReadOnlyList<MessageAttachment> attachments) => new(
			message.Id,
			message.ConversationId,
			message.SenderUserId,
			senderNames.TryGetValue(message.SenderUserId, out var name) ? name : "Usuario",
			message.IsDeleted ? "Mensaje eliminado" : message.Body,
			message.IsSystemMessage,
			message.SenderUserId == currentUserId,
			message.IsDeleted,
			message.CreatedAt,
			message.EditedAt,
			attachments
				.Where(a => a.MessageId == message.Id)
				.Select(a => new MessageAttachmentDto(a.Id, a.FileName, a.ContentType, a.SizeInBytes, a.UploadedAt))
				.ToList());

	private sealed record ConversationAccess(Conversation Conversation, ConversationParticipant Participant);
}
