using MessengerService.Application.Interfaces;
using MessengerService.Application.Services;
using MessengerService.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MessengerService.Tests.Unit;

public sealed class ChatServiceDeleteChatTests
{
    [Fact]
    public async Task DeleteChatAsync_PrivateCreator_ShouldDeleteChatAndCascadeMessages()
    {
        var userId = Guid.NewGuid();
        var store = new FakeChatDeletionStore(
            new Chat
            {
                Id = "chat-private",
                Type = "private",
                CreatedBy = userId,
                Members =
                [
                    new ChatMember { UserId = userId, Role = "member" },
                    new ChatMember { UserId = Guid.NewGuid(), Role = "member" },
                ],
            },
            [
                new Message { Id = "message-1", ChatId = "chat-private" },
                new Message { Id = "message-2", ChatId = "chat-private" },
                new Message { Id = "message-3", ChatId = "chat-other" },
            ]);
        var service = new ChatService(new NoOpUserLookupClient(), NullLogger<ChatService>.Instance, store);

        var result = await service.DeleteChatAsync("chat-private", userId);

        Assert.Equal(DeleteChatStatus.Deleted, result.Status);
        Assert.Null(store.Chat);
        Assert.Equal(1, store.DeleteChatCalls);
        Assert.Equal(1, store.DeleteMessagesCalls);
        Assert.Single(store.Messages);
        Assert.Equal("chat-other", store.Messages[0].ChatId);
    }

    [Fact]
    public async Task DeleteChatAsync_PrivateNonCreator_ShouldReturnForbidden()
    {
        var creatorId = Guid.NewGuid();
        var nonCreatorId = Guid.NewGuid();
        var store = new FakeChatDeletionStore(
            new Chat
            {
                Id = "chat-private",
                Type = "private",
                CreatedBy = creatorId,
                Members =
                [
                    new ChatMember { UserId = creatorId, Role = "member" },
                    new ChatMember { UserId = nonCreatorId, Role = "member" },
                ],
            },
            [new Message { Id = "message-1", ChatId = "chat-private" }]);
        var service = new ChatService(new NoOpUserLookupClient(), NullLogger<ChatService>.Instance, store);

        var result = await service.DeleteChatAsync("chat-private", nonCreatorId);

        Assert.Equal(DeleteChatStatus.Forbidden, result.Status);
        Assert.NotNull(store.Chat);
        Assert.Equal(0, store.DeleteChatCalls);
        Assert.Equal(0, store.DeleteMessagesCalls);
        Assert.Single(store.Messages);
    }

    [Fact]
    public async Task DeleteChatAsync_ChannelAdmin_ShouldDeleteChat()
    {
        var userId = Guid.NewGuid();
        var store = new FakeChatDeletionStore(
            new Chat
            {
                Id = "chat-channel",
                Type = "channel",
                Members =
                [
                    new ChatMember { UserId = userId, Role = "admin" },
                    new ChatMember { UserId = Guid.NewGuid(), Role = "member" },
                ],
            },
            []);
        var service = new ChatService(new NoOpUserLookupClient(), NullLogger<ChatService>.Instance, store);

        var result = await service.DeleteChatAsync("chat-channel", userId);

        Assert.Equal(DeleteChatStatus.Deleted, result.Status);
        Assert.Null(store.Chat);
    }

    [Fact]
    public async Task DeleteChatAsync_ProjectMember_ShouldReturnForbidden()
    {
        var userId = Guid.NewGuid();
        var store = new FakeChatDeletionStore(
            new Chat
            {
                Id = "chat-project",
                Type = "project",
                Members =
                [
                    new ChatMember { UserId = userId, Role = "member" },
                    new ChatMember { UserId = Guid.NewGuid(), Role = "admin" },
                ],
            },
            [new Message { Id = "message-1", ChatId = "chat-project" }]);
        var service = new ChatService(new NoOpUserLookupClient(), NullLogger<ChatService>.Instance, store);

        var result = await service.DeleteChatAsync("chat-project", userId);

        Assert.Equal(DeleteChatStatus.Forbidden, result.Status);
        Assert.NotNull(store.Chat);
        Assert.Equal(0, store.DeleteChatCalls);
        Assert.Equal(0, store.DeleteMessagesCalls);
        Assert.Single(store.Messages);
    }

    [Fact]
    public async Task DeleteChatAsync_UnknownChat_ShouldReturnNotFound()
    {
        var service = new ChatService(
            new NoOpUserLookupClient(),
            NullLogger<ChatService>.Instance,
            new FakeChatDeletionStore(null, []));

        var result = await service.DeleteChatAsync("missing-chat", Guid.NewGuid());

        Assert.Equal(DeleteChatStatus.NotFound, result.Status);
    }

    private sealed class FakeChatDeletionStore : IChatDeletionStore
    {
        public FakeChatDeletionStore(Chat? chat, List<Message> messages)
        {
            Chat = chat;
            Messages = messages;
        }

        public Chat? Chat { get; private set; }
        public List<Message> Messages { get; }
        public int DeleteChatCalls { get; private set; }
        public int DeleteMessagesCalls { get; private set; }

        public Task<Chat?> GetChatAsync(string chatId, CancellationToken ct = default)
        {
            return Task.FromResult(Chat?.Id == chatId ? Chat : null);
        }

        public Task DeleteChatAsync(string chatId, CancellationToken ct = default)
        {
            DeleteChatCalls += 1;
            if (Chat?.Id == chatId)
            {
                Chat = null;
            }

            return Task.CompletedTask;
        }

        public Task DeleteMessagesAsync(string chatId, CancellationToken ct = default)
        {
            DeleteMessagesCalls += 1;
            Messages.RemoveAll(message => message.ChatId == chatId);
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpUserLookupClient : IUserLookupClient
    {
        public Task<IReadOnlyDictionary<Guid, UserLookupResult>> GetUsersByIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
        {
            IReadOnlyDictionary<Guid, UserLookupResult> result = new Dictionary<Guid, UserLookupResult>();
            return Task.FromResult(result);
        }
    }
}
