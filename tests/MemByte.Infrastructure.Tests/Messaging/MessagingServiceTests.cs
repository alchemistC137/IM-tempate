using MemByte.Application.Messaging;
using MemByte.Domain.Messaging;
using MemByte.Infrastructure.Messaging;
using MemByte.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MemByte.Infrastructure.Tests.Messaging;

/// <summary>
/// Pruebas centradas en las dos garantías críticas del módulo: el aislamiento
/// entre clientes y el control de quién puede responder.
/// </summary>
public class MessagingServiceTests
{
	// Cada prueba usa una base de datos aislada, pero puede abrir varios
	// contextos sobre ella para reproducir el scope por petición de la app.
	private static MemByteDbContext CreateDbContext(string databaseName)
	{
		var options = new DbContextOptionsBuilder<MemByteDbContext>()
			.UseInMemoryDatabase(databaseName)
			.Options;

		return new MemByteDbContext(options);
	}

	private static string NewDatabaseName() => Guid.NewGuid().ToString("N");

	private static MemByteDbContext CreateDbContext() => CreateDbContext(NewDatabaseName());

	private static MessagingService CreateService(
		MemByteDbContext dbContext,
		MessagingUserContext? userContext,
		FakeMessagingNotifier? notifier = null,
		FakeAttachmentStorage? storage = null)
	{
		return new MessagingService(
			dbContext,
			new FakeUserContextProvider(userContext),
			storage ?? new FakeAttachmentStorage(),
			new MessagingAuditLogger(dbContext, NullLogger<MessagingAuditLogger>.Instance),
			notifier ?? new FakeMessagingNotifier(),
			Options.Create(new MessagingOptions()));
	}

	private static async Task<Conversation> SeedConversationAsync(
		MemByteDbContext dbContext,
		Guid? clientId,
		Guid ownerUserId,
		bool ownerCanReply = true)
	{
		var conversation = new Conversation(clientId, "Incidencia de acceso", ownerUserId);
		conversation.AddParticipant(ownerUserId, ConversationParticipantRole.Requester, ownerCanReply);
		conversation.AddMessage(ownerUserId, "No puedo entrar en la intranet");

		dbContext.Conversations.Add(conversation);
		await dbContext.SaveChangesAsync();

		return conversation;
	}

	[Fact]
	public async Task GetInboxAsync_WithoutAuthenticatedUser_ReturnsEmpty()
	{
		await using var dbContext = CreateDbContext();
		var service = CreateService(dbContext, userContext: null);

		var inbox = await service.GetInboxAsync();

		Assert.Empty(inbox);
	}

	[Fact]
	public async Task GetInboxAsync_ReturnsOnlyConversationsOfOwnClient()
	{
		await using var dbContext = CreateDbContext();
		var clientA = Guid.NewGuid();
		var clientB = Guid.NewGuid();
		var userA = Guid.NewGuid();

		var ownConversation = await SeedConversationAsync(dbContext, clientA, userA);

		// Conversación de otro cliente en la que, por error, el mismo usuario
		// figura como participante: el filtro por cliente debe excluirla.
		var foreignConversation = new Conversation(clientB, "Hilo ajeno", Guid.NewGuid());
		foreignConversation.AddParticipant(userA, ConversationParticipantRole.Agent, canReply: true);
		foreignConversation.AddMessage(foreignConversation.CreatedByUserId, "Contenido de otro tenant");
		dbContext.Conversations.Add(foreignConversation);
		await dbContext.SaveChangesAsync();

		var service = CreateService(dbContext, new MessagingUserContext(userA, clientA, IsInternalStaff: false, CanReply: false));

		var inbox = await service.GetInboxAsync();

		Assert.Single(inbox);
		Assert.Equal(ownConversation.Id, inbox[0].Id);
	}

	[Fact]
	public async Task GetConversationAsync_FromAnotherClient_ReturnsGenericFailure()
	{
		await using var dbContext = CreateDbContext();
		var clientA = Guid.NewGuid();
		var conversation = await SeedConversationAsync(dbContext, clientA, Guid.NewGuid());

		var intruder = new MessagingUserContext(Guid.NewGuid(), Guid.NewGuid(), IsInternalStaff: false, CanReply: true);
		var service = CreateService(dbContext, intruder);

		var result = await service.GetConversationAsync(conversation.Id);

		Assert.False(result.Succeeded);
		Assert.Null(result.Value);
		// El mensaje no distingue entre "no existe" y "no tienes acceso".
		Assert.Contains(result.Errors, e => e.Contains("no existe o no tienes acceso", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task GetConversationAsync_DeniedAccess_IsAudited()
	{
		await using var dbContext = CreateDbContext();
		var conversation = await SeedConversationAsync(dbContext, Guid.NewGuid(), Guid.NewGuid());

		var intruder = new MessagingUserContext(Guid.NewGuid(), Guid.NewGuid(), IsInternalStaff: false, CanReply: true);
		var service = CreateService(dbContext, intruder);

		await service.GetConversationAsync(conversation.Id);

		var audit = Assert.Single(dbContext.MessagingAuditEntries);
		Assert.False(audit.WasAllowed);
		Assert.Equal(intruder.UserId, audit.UserId);
	}

	[Fact]
	public async Task GetConversationAsync_AsParticipant_ReturnsThread()
	{
		await using var dbContext = CreateDbContext();
		var clientId = Guid.NewGuid();
		var userId = Guid.NewGuid();
		var conversation = await SeedConversationAsync(dbContext, clientId, userId);

		var service = CreateService(dbContext, new MessagingUserContext(userId, clientId, IsInternalStaff: false, CanReply: false));

		var result = await service.GetConversationAsync(conversation.Id);

		Assert.True(result.Succeeded);
		Assert.NotNull(result.Value);
		Assert.Single(result.Value!.Messages);
	}

	[Fact]
	public async Task StartConversationAsync_UsesClientOfAuthenticatedUser()
	{
		await using var dbContext = CreateDbContext();
		var clientId = Guid.NewGuid();
		var userId = Guid.NewGuid();
		var service = CreateService(dbContext, new MessagingUserContext(userId, clientId, IsInternalStaff: false, CanReply: false));

		var result = await service.StartConversationAsync(
			new StartConversationRequest("Solicitud de alta", "Necesito acceso al portal"));

		Assert.True(result.Succeeded);
		var stored = Assert.Single(dbContext.Conversations);
		Assert.Equal(clientId, stored.ClientId);
		Assert.Equal(userId, stored.CreatedByUserId);
	}

	[Fact]
	public async Task StartConversationAsync_WithEmptySubject_ReturnsFailure()
	{
		await using var dbContext = CreateDbContext();
		var service = CreateService(
			dbContext,
			new MessagingUserContext(Guid.NewGuid(), Guid.NewGuid(), IsInternalStaff: false, CanReply: false));

		var result = await service.StartConversationAsync(new StartConversationRequest(" ", "Cuerpo"));

		Assert.False(result.Succeeded);
		Assert.Empty(dbContext.Conversations);
	}

	[Fact]
	public async Task SendMessageAsync_WhenUserCannotReply_ReturnsFailureAndDoesNotPersist()
	{
		await using var dbContext = CreateDbContext();
		var clientId = Guid.NewGuid();
		var ownerId = Guid.NewGuid();
		var conversation = await SeedConversationAsync(dbContext, clientId, ownerId, ownerCanReply: false);

		var service = CreateService(dbContext, new MessagingUserContext(ownerId, clientId, IsInternalStaff: false, CanReply: false));

		var result = await service.SendMessageAsync(new SendMessageRequest(conversation.Id, "Respuesta"));

		Assert.False(result.Succeeded);
		Assert.Equal(1, dbContext.Messages.Count());
	}

	[Fact]
	public async Task SendMessageAsync_WhenUserCanReply_PersistsAndNotifies()
	{
		var databaseName = NewDatabaseName();
		var clientId = Guid.NewGuid();
		var requesterId = Guid.NewGuid();
		var agentId = Guid.NewGuid();
		Guid conversationId;

		await using (var seedContext = CreateDbContext(databaseName))
		{
			var conversation = new Conversation(clientId, "Incidencia", requesterId);
			conversation.AddParticipant(requesterId, ConversationParticipantRole.Requester, canReply: true);
			conversation.AddParticipant(agentId, ConversationParticipantRole.Agent, canReply: true);
			conversation.AddMessage(requesterId, "Hola");
			seedContext.Conversations.Add(conversation);
			await seedContext.SaveChangesAsync();
			conversationId = conversation.Id;
		}

		var notifier = new FakeMessagingNotifier();
		await using var dbContext = CreateDbContext(databaseName);
		var service = CreateService(
			dbContext,
			new MessagingUserContext(agentId, clientId, IsInternalStaff: true, CanReply: true),
			notifier);

		var result = await service.SendMessageAsync(new SendMessageRequest(conversationId, "Lo revisamos"));

		Assert.True(result.Succeeded);
		Assert.Equal(2, dbContext.Messages.Count());
		// El aviso llega al solicitante, nunca al propio emisor.
		Assert.Equal([requesterId], notifier.LastRecipients);
	}

	[Fact]
	public async Task SendMessageAsync_ToConversationOfAnotherClient_ReturnsGenericFailure()
	{
		await using var dbContext = CreateDbContext();
		var conversation = await SeedConversationAsync(dbContext, Guid.NewGuid(), Guid.NewGuid());

		var service = CreateService(
			dbContext,
			new MessagingUserContext(Guid.NewGuid(), Guid.NewGuid(), IsInternalStaff: true, CanReply: true));

		var result = await service.SendMessageAsync(new SendMessageRequest(conversation.Id, "Intruso"));

		Assert.False(result.Succeeded);
		Assert.Equal(1, dbContext.Messages.Count());
	}

	[Fact]
	public async Task ChangeStatusAsync_WhenUserCannotReply_ReturnsFailure()
	{
		await using var dbContext = CreateDbContext();
		var clientId = Guid.NewGuid();
		var userId = Guid.NewGuid();
		var conversation = await SeedConversationAsync(dbContext, clientId, userId, ownerCanReply: false);

		var service = CreateService(dbContext, new MessagingUserContext(userId, clientId, IsInternalStaff: false, CanReply: false));

		var result = await service.ChangeStatusAsync(conversation.Id, ConversationStatus.Closed);

		Assert.False(result.Succeeded);
		Assert.Equal(ConversationStatus.Open, dbContext.Conversations.Single().Status);
	}

	[Fact]
	public async Task MarkAsReadAsync_ResetsUnreadCounter()
	{
		await using var dbContext = CreateDbContext();
		var clientId = Guid.NewGuid();
		var requesterId = Guid.NewGuid();
		var agentId = Guid.NewGuid();

		var conversation = new Conversation(clientId, "Incidencia", requesterId);
		conversation.AddParticipant(requesterId, ConversationParticipantRole.Requester, canReply: true);
		conversation.AddParticipant(agentId, ConversationParticipantRole.Agent, canReply: true);
		conversation.AddMessage(agentId, "Novedad");
		dbContext.Conversations.Add(conversation);
		await dbContext.SaveChangesAsync();

		var service = CreateService(dbContext, new MessagingUserContext(requesterId, clientId, IsInternalStaff: false, CanReply: true));

		var result = await service.MarkAsReadAsync(conversation.Id);

		Assert.True(result.Succeeded);
		var participant = dbContext.ConversationParticipants.Single(p => p.UserId == requesterId);
		Assert.Equal(0, participant.UnreadCount);
	}

	private sealed class FakeUserContextProvider : IMessagingUserContextProvider
	{
		private readonly MessagingUserContext? _context;

		public FakeUserContextProvider(MessagingUserContext? context) => _context = context;

		public Task<MessagingUserContext?> GetCurrentAsync(CancellationToken cancellationToken = default)
			=> Task.FromResult(_context);
	}

	private sealed class FakeMessagingNotifier : IMessagingNotifier
	{
		public IReadOnlyCollection<Guid> LastRecipients { get; private set; } = [];

		public Task NotifyConversationUpdatedAsync(
			IReadOnlyCollection<Guid> recipientUserIds,
			ConversationNotification notification,
			CancellationToken cancellationToken = default)
		{
			LastRecipients = recipientUserIds;
			return Task.CompletedTask;
		}
	}

	private sealed class FakeAttachmentStorage : IMessagingAttachmentStorage
	{
		public List<string> DeletedReferences { get; } = [];

		public Task<string> SaveAsync(
			Guid? clientId,
			Guid conversationId,
			Guid messageId,
			NewAttachment attachment,
			CancellationToken cancellationToken = default)
			=> Task.FromResult($"{conversationId:N}/{messageId:N}/{Guid.NewGuid():N}");

		public Task<Stream> OpenReadAsync(string storageReference, CancellationToken cancellationToken = default)
			=> Task.FromResult<Stream>(new MemoryStream());

		public Task DeleteAsync(string storageReference, CancellationToken cancellationToken = default)
		{
			DeletedReferences.Add(storageReference);
			return Task.CompletedTask;
		}
	}
}
