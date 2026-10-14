using System.Security.Claims;
using MessengerService.Application.Interfaces;
using MessengerService.Controllers;
using MessengerService.Contracts.Requests;
using MessengerService.Contracts.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace MessengerService.Tests.Unit;

public sealed class ChatsControllerDeleteTests
{
    [Fact]
    public async Task DeleteChat_ShouldReturnNoContent_WhenServiceDeletesChat()
    {
        var controller = CreateController(DeleteChatResult.Deleted);

        var result = await controller.DeleteChat("chat-1", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteChat_ShouldReturnNotFound_WhenChatIsMissing()
    {
        var controller = CreateController(DeleteChatResult.NotFound);

        var result = await controller.DeleteChat("chat-1", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeleteChat_ShouldReturnForbid_WhenUserCannotDeleteChat()
    {
        var controller = CreateController(DeleteChatResult.Forbidden);

        var result = await controller.DeleteChat("chat-1", CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    private static ChatsController CreateController(DeleteChatResult deleteChatResult)
    {
        var controller = new ChatsController(new StubChatService(deleteChatResult), null!);
        var userId = Guid.NewGuid();
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("sub", userId.ToString()),
        ], "Test");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity),
            },
        };

        return controller;
    }

    private sealed class StubChatService : IChatService
    {
        private readonly DeleteChatResult _deleteChatResult;

        public StubChatService(DeleteChatResult deleteChatResult)
        {
            _deleteChatResult = deleteChatResult;
        }

        public Task<DeleteChatResult> DeleteChatAsync(string chatId, Guid userId, CancellationToken ct = default)
        {
            return Task.FromResult(_deleteChatResult);
        }

        public Task<ClearChatResult> ClearChatAsync(string chatId, Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddMembersAsync(string chatId, Guid userId, AddChatMembersRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task LeaveChatAsync(string chatId, Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task MarkAsReadAsync(string chatId, Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RemoveMemberAsync(string chatId, Guid userId, Guid memberUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ChatResponse> CreateChannelAsync(Guid userId, CreateChannelRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ChatResponse> CreatePrivateChatAsync(Guid userId, CreatePrivateChatRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ChatResponse> CreateProjectChatAsync(Guid userId, CreateProjectChatRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ChatResponse?> GetChatByIdAsync(string chatId, Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ChatResponse>> GetUserChatsAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ChatResponse> UpdateChatAsync(string chatId, Guid userId, UpdateChatRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
