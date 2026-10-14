using System.Net;
using System.Text;
using Collaby.ServiceDefaults.SystemAdministration;
using Collaby.ServiceDefaults.InternalAuth;
using ManagementService.Application.Interfaces;
using ManagementService.Application.Services;
using ManagementService.Contracts.Responses;
using ManagementService.Controllers;
using ManagementService.Infrastructure.Data;
using ManagementService.TestCommon;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace ManagementService.Tests.Integration;

public sealed class ManagementServiceTestServer : IAsyncLifetime
{
    private readonly InMemoryDatabaseRoot _databaseRoot = new();
    private readonly string _databaseName = $"management-tests-{Guid.NewGuid():N}";
    private WebApplication? _app;

    public HttpClient Client { get; private set; } = null!;
    public IServiceProvider Services => _app?.Services ?? throw new InvalidOperationException("Test server is not initialized.");

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });

        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [SystemAdminConstants.EmailsConfigurationKey] = "admin@collaby.test"
        });
        builder.Services
            .AddControllers()
            .AddApplicationPart(typeof(HealthController).Assembly);
        builder.Services.AddSystemAdministration(builder.Configuration);
        builder.Services.AddDbContext<ManagementDbContext>(options =>
            options.UseInMemoryDatabase(_databaseName, _databaseRoot));
        builder.Services.AddSingleton<ILandingDefaultsProvider, LandingDefaultsProvider>();
        builder.Services.AddScoped<ILandingContentService, LandingContentService>();
        builder.Services.AddSingleton<IProjectProgressSnapshotBuilder, FakeProjectProgressSnapshotBuilder>();
        builder.Services.AddOptions<InternalServiceAuthOptions>()
            .Configure(options =>
            {
                options.AllowedCallers["analytics-service"] = "analytics-secret";
            });
        builder.Services.AddSingleton<InternalServiceRequestAuthenticator>();
        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(JwtBearerDefaults.AuthenticationScheme, _ => { });

        _app = builder.Build();
        _app.UsePathBase("/api/management");
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

    private sealed class FakeProjectProgressSnapshotBuilder : IProjectProgressSnapshotBuilder
    {
        public Task<ProjectProgressResponse?> BuildAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ProjectProgressResponse?>(null);
        }
    }
}
