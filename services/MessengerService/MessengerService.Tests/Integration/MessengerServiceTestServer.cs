using ManagementService.TestCommon;
using MessengerService.Application.Interfaces;
using MessengerService.Controllers;
using MessengerService.Contracts.Responses;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MessengerService.Tests.Integration;

public sealed class MessengerServiceTestServer : IAsyncLifetime
{
    private WebApplication? _app;

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });

        builder.WebHost.UseTestServer();
        builder.Services
            .AddControllers()
            .AddApplicationPart(typeof(PresenceController).Assembly);
        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(JwtBearerDefaults.AuthenticationScheme, _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IPresenceService, StubPresenceService>();

        _app = builder.Build();
        _app.UsePathBase("/api/messenger");
        _app.UseRouting();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapControllers();

        await _app.StartAsync();
        Client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    private sealed class StubPresenceService : IPresenceService
    {
        public Task ClearTypingAsync(Guid userId, string chatId) => Task.CompletedTask;

        public Task<IReadOnlyList<PresenceResponse>> GetPresenceAsync(IEnumerable<Guid> userIds)
            => Task.FromResult<IReadOnlyList<PresenceResponse>>(userIds
                .Select((userId, index) => new PresenceResponse
                {
                    UserId = userId,
                    IsOnline = index == 0,
                    LastSeenAt = DateTime.UtcNow
                })
                .ToList());

        public Task<IReadOnlyList<Guid>> GetTypingUsersAsync(string chatId)
            => Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>());

        public Task<bool> IsOnlineAsync(Guid userId) => Task.FromResult(true);

        public Task SetOfflineAsync(Guid userId) => Task.CompletedTask;

        public Task SetOnlineAsync(Guid userId) => Task.CompletedTask;

        public Task SetTypingAsync(Guid userId, string chatId) => Task.CompletedTask;
    }
}
