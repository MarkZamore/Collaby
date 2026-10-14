using CallService.Application.Interfaces;
using CallService.Controllers;
using CallService.Contracts.Requests;
using CallService.Contracts.Responses;
using CallService.Hubs;
using ManagementService.TestCommon;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CallService.Tests.Integration;

public sealed class CallServiceTestServer : IAsyncLifetime
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
            .AddApplicationPart(typeof(CallsController).Assembly);
        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(JwtBearerDefaults.AuthenticationScheme, _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<ICallService, StubCallService>();
        builder.Services.AddSingleton<IHubContext<CallHub>, NoOpHubContext<CallHub>>();

        _app = builder.Build();
        _app.UsePathBase("/api/calls");
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

    private sealed class StubCallService : ICallService
    {
        public Task<CallSessionResponse> AnswerCallAsync(string callId, Guid userId, AnswerCallRequest request, CancellationToken ct = default)
            => Task.FromResult(CreateCall(callId, userId));

        public Task<CallSessionResponse> DeclineCallAsync(string callId, Guid userId, CancellationToken ct = default)
            => Task.FromResult(CreateCall(callId, userId));

        public Task<CallSessionResponse> EndCallAsync(string callId, Guid userId, CancellationToken ct = default)
            => Task.FromResult(CreateCall(callId, userId));

        public Task<CallSessionResponse?> GetActiveCallAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult<CallSessionResponse?>(CreateCall("active-call", userId));

        public Task<CallSessionResponse?> GetCallAsync(string callId, CancellationToken ct = default)
            => Task.FromResult<CallSessionResponse?>(CreateCall(callId, Guid.NewGuid()));

        public Task<IReadOnlyList<CallSessionResponse>> GetCallHistoryAsync(Guid userId, int limit = 50, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CallSessionResponse>>(new[] { CreateCall("history-call", userId) });

        public Task<CallSessionResponse> LeaveCallAsync(string callId, Guid userId, CancellationToken ct = default)
            => Task.FromResult(CreateCall(callId, userId));

        public Task<CallSessionResponse> StartCallAsync(Guid initiatorId, StartCallRequest request, CancellationToken ct = default)
            => Task.FromResult(CreateCall("started-call", initiatorId));

        public Task ToggleAudioAsync(string callId, Guid userId, bool enabled, CancellationToken ct = default) => Task.CompletedTask;

        public Task ToggleScreenShareAsync(string callId, Guid userId, bool enabled, CancellationToken ct = default) => Task.CompletedTask;

        public Task ToggleVideoAsync(string callId, Guid userId, bool enabled, CancellationToken ct = default) => Task.CompletedTask;

        private static CallSessionResponse CreateCall(string callId, Guid userId) => new()
        {
            Id = callId,
            Type = "video",
            Status = "active",
            InitiatorId = userId,
            CreatedAt = DateTime.UtcNow,
            Participants =
            {
                new CallParticipantResponse
                {
                    UserId = userId,
                    Status = "joined",
                    HasAudio = true,
                    HasVideo = true
                }
            }
        };
    }
}
