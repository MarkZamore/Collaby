using ManagementService.TestCommon;
using GamificationService.Application.Interfaces;
using GamificationService.Contracts.Responses;
using GamificationService.Controllers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GamificationService.Tests.Integration;

public sealed class GamificationServiceTestServer : IAsyncLifetime
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
            .AddApplicationPart(typeof(XpController).Assembly);
        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(JwtBearerDefaults.AuthenticationScheme, _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IXpService, StubXpService>();

        _app = builder.Build();
        _app.UsePathBase("/api/gamification");
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

    private sealed class StubXpService : IXpService
    {
        public Task<RewardEventResponse> AwardXpAsync(Guid userId, Guid? projectId, Domain.Enums.XpReason reason, Guid? taskId = null, string? priority = null)
            => Task.FromResult(new RewardEventResponse());

        public Task EnsureUserExistsAsync(Guid userId) => Task.CompletedTask;

        public Task<UserXpResponse> GetUserGlobalXpAsync(Guid userId)
            => Task.FromResult(new UserXpResponse
            {
                UserId = userId,
                TotalXp = 120,
                Level = 2,
                XpToNextLevel = 180,
                Coins = 40
            });

        public Task<IEnumerable<XpHistoryItemResponse>> GetXpHistoryAsync(Guid userId, int limit = 50, int offset = 0)
            => Task.FromResult<IEnumerable<XpHistoryItemResponse>>(Array.Empty<XpHistoryItemResponse>());

        public Task<UserProjectXpResponse> GetUserProjectXpAsync(Guid userId, Guid projectId)
            => Task.FromResult(new UserProjectXpResponse
            {
                UserId = userId,
                ProjectId = projectId,
                Level = 1,
                XpToNextLevel = 200
            });

        public Task<RewardEventResponse> RollbackXpAsync(Guid userId, Guid? projectId, Guid taskId)
            => Task.FromResult(new RewardEventResponse());
    }
}
