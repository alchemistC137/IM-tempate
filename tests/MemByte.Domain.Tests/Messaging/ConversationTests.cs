using MemByte.Domain.Messaging;

namespace MemByte.Domain.Tests.Messaging;

public class ConversationTests
{
	private static Conversation CreateConversation(Guid? clientId = null, Guid? creatorId = null)
		=> new(clientId, "Incidencia de acceso", creatorId ?? Guid.NewGuid());

	[Fact]
	public void Constructor_WithValidData_CreatesOpenConversation()
	{
		var creatorId = Guid.NewGuid();
		var clientId = Guid.NewGuid();

		var conversation = new Conversation(clientId, "Incidencia de acceso", creatorId);

		Assert.NotEqual(Guid.Empty, conversation.Id);
		Assert.Equal(clientId, conversation.ClientId);
		Assert.Equal("Incidencia de acceso", conversation.Subject);
		Assert.Equal(ConversationStatus.Open, conversation.Status);
		Assert.Equal(creatorId, conversation.CreatedByUserId);
		Assert.Equal(0, conversation.MessageCount);
	}

	[Theory]
	[InlineData("")]
	[InlineData(" ")]
	public void Constructor_WithInvalidSubject_Throws(string subject)
	{
		Assert.Throws<ArgumentException>(() => new Conversation(Guid.NewGuid(), subject, Guid.NewGuid()));
	}

	[Fact]
	public void BelongsToClient_WithDifferentClient_ReturnsFalse()
	{
		var conversation = CreateConversation(Guid.NewGuid());

		// Regla clave de aislamiento: una conversación nunca pertenece al
		// ámbito de otro cliente.
		Assert.False(conversation.BelongsToClient(Guid.NewGuid()));
	}

	[Fact]
	public void BelongsToClient_WithSameClient_ReturnsTrue()
	{
		var clientId = Guid.NewGuid();
		var conversation = CreateConversation(clientId);

		Assert.True(conversation.BelongsToClient(clientId));
	}

	[Fact]
	public void AddMessage_UpdatesTrackingFields()
	{
		var senderId = Guid.NewGuid();
		var conversation = CreateConversation(creatorId: senderId);
		conversation.AddParticipant(senderId, ConversationParticipantRole.Requester, canReply: true);

		var message = conversation.AddMessage(senderId, "No puedo entrar en la intranet");

		Assert.Equal(1, conversation.MessageCount);
		Assert.Equal(senderId, conversation.LastMessageSenderUserId);
		Assert.Equal(message.CreatedAt, conversation.LastMessageAt);
		Assert.False(string.IsNullOrWhiteSpace(conversation.LastMessagePreview));
	}

	[Fact]
	public void AddMessage_IncrementsUnreadCountOnlyForOtherParticipants()
	{
		var senderId = Guid.NewGuid();
		var recipientId = Guid.NewGuid();
		var conversation = CreateConversation(creatorId: senderId);
		conversation.AddParticipant(senderId, ConversationParticipantRole.Requester, canReply: true);
		conversation.AddParticipant(recipientId, ConversationParticipantRole.Agent, canReply: true);

		conversation.AddMessage(senderId, "Hola");

		var sender = conversation.Participants.Single(p => p.UserId == senderId);
		var recipient = conversation.Participants.Single(p => p.UserId == recipientId);

		Assert.Equal(0, sender.UnreadCount);
		Assert.Equal(1, recipient.UnreadCount);
	}

	[Fact]
	public void AddMessage_WhenClosed_Throws()
	{
		var senderId = Guid.NewGuid();
		var conversation = CreateConversation(creatorId: senderId);
		conversation.AddParticipant(senderId, ConversationParticipantRole.Requester, canReply: true);
		conversation.Close();

		Assert.Throws<InvalidOperationException>(() => conversation.AddMessage(senderId, "Hola"));
	}

	[Fact]
	public void AddParticipant_WithDuplicateUser_DoesNotDuplicate()
	{
		var userId = Guid.NewGuid();
		var conversation = CreateConversation(creatorId: userId);

		conversation.AddParticipant(userId, ConversationParticipantRole.Requester, canReply: true);
		conversation.AddParticipant(userId, ConversationParticipantRole.Agent, canReply: true);

		Assert.Single(conversation.Participants);
	}

	[Fact]
	public void Reopen_AfterClose_SetsStatusToOpen()
	{
		var conversation = CreateConversation();

		conversation.Close();
		conversation.Reopen();

		Assert.Equal(ConversationStatus.Open, conversation.Status);
	}
}
