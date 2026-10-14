using System.Net;
using System.Text.Json;
using ManagementService.Application.Interfaces;
using ManagementService.Application.Services;
using ManagementService.Contracts.Requests.Admin;
using ManagementService.Controllers;
using ManagementService.Domain.Entities;
using ManagementService.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ManagementService.Tests.Unit;

public sealed class KubernetesRestartTests
{
    private static ManagementDbContext Database(DbContextOptions<ManagementDbContext>? options = null) =>
        new(options ?? new DbContextOptionsBuilder<ManagementDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Kubernetes:Namespace"] = "collaby" }).Build();
    private static string Deployment(Guid? annotation = null, string uid = "uid-1", long generation = 7, long observed = 7, int replicas = 1, int available = 1, string? reason = null) => JsonSerializer.Serialize(new
    {
        metadata = new { uid, generation, resourceVersion = "12" },
        spec = new { replicas, template = new { metadata = new { annotations = annotation is null ? new Dictionary<string,string>() : new Dictionary<string,string> { ["collaby.io/restart-operation"] = annotation.ToString()! } } } },
        status = new { observedGeneration = observed, updatedReplicas = replicas, availableReplicas = available, replicas, conditions = reason is null ? Array.Empty<object>() : new object[] { new { reason } } }
    });
    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json) };
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => handle(request);
    }
    private static KubernetesAdminRestartClient Client(ManagementDbContext db, Func<HttpRequestMessage, Task<HttpResponseMessage>> handle) =>
        new(Config(), db, () => new HttpClient(new Handler(handle)) { BaseAddress = new Uri("https://kubernetes.default.svc") });

    [Fact]
    public async Task PatchPersistsIdentityBeforeSelfRestartAndTargetsOnlyDeployment()
    {
        var options = new DbContextOptionsBuilder<ManagementDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = Database(options);
        var operation = new ServiceRestartOperation { TargetService = "workspace" };
        db.Add(operation); await db.SaveChangesAsync();
        var patches = 0;
        var client = Client(db, async request =>
        {
            Assert.Equal("/apis/apps/v1/namespaces/collaby/deployments/workspace", request.RequestUri!.AbsolutePath);
            if (request.Method == HttpMethod.Get) return Json(Deployment());
            Assert.Equal(HttpMethod.Patch, request.Method);
            await using var persisted = Database(options);
            var saved = await persisted.ServiceRestartOperations.SingleAsync();
            Assert.Equal("triggered", saved.Status);
            Assert.Equal("uid-1", saved.DeploymentUid);
            Assert.Equal("collaby", saved.DeploymentNamespace);
            using var patch = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal("12", patch.RootElement.GetProperty("metadata").GetProperty("resourceVersion").GetString());
            Assert.Equal(operation.Id.ToString(), patch.RootElement.GetProperty("spec").GetProperty("template").GetProperty("metadata").GetProperty("annotations").GetProperty("collaby.io/restart-operation").GetString());
            patches++;
            return Json(Deployment(operation.Id, generation: 8));
        });
        await client.TrySyncAsync(operation);
        Assert.Equal(1, patches); Assert.Equal("running", operation.Status); Assert.Equal(8, operation.DeploymentGeneration);
    }

    [Fact]
    public async Task ProcessKilledAfterPatchResumesWithoutAnotherRollout()
    {
        await using var db = Database();
        var operation = new ServiceRestartOperation { TargetService = "workspace" };
        db.Add(operation); await db.SaveChangesAsync();
        Guid? annotation = null;
        var patches = 0;
        var client = Client(db, request =>
        {
            if (request.Method == HttpMethod.Get) return Task.FromResult(Json(Deployment(annotation)));
            patches++; annotation = operation.Id;
            throw new HttpRequestException("РЎРѕРµРґРёРЅРµРЅРёРµ РѕР±РѕСЂРІР°Р»РѕСЃСЊ РїРѕСЃР»Рµ РїСЂРёРјРµРЅРµРЅРёСЏ PATCH.");
        });
        await Assert.ThrowsAsync<HttpRequestException>(() => client.TrySyncAsync(operation));
        db.ChangeTracker.Clear();
        var resumed = await db.ServiceRestartOperations.SingleAsync();
        Assert.Equal("triggered", resumed.Status);
        await client.TrySyncAsync(resumed); await db.SaveChangesAsync();
        await client.TrySyncAsync(resumed);
        Assert.Equal(1, patches); Assert.Equal("succeeded", resumed.Status);
    }

    [Theory]
    [InlineData(6, 1, 1)]
    [InlineData(7, 1, 0)]
    [InlineData(7, 0, 0)]
    public async Task ReadinessRequiresObservedGenerationAndAvailableNonzeroReplicas(long observed, int replicas, int available)
    {
        await using var db = Database();
        var operation = new ServiceRestartOperation { TargetService = "access", Status = "running", DeploymentUid = "uid-1", DeploymentGeneration = 7 };
        var client = Client(db, _ => Task.FromResult(Json(Deployment(operation.Id, observed: observed, replicas: replicas, available: available))));
        await client.TrySyncAsync(operation);
        Assert.Equal("running", operation.Status);
    }

    [Theory]
    [InlineData("different", null)]
    [InlineData("uid-1", "ProgressDeadlineExceeded")]
    public async Task RecreatedDeploymentOrFailedRolloutCannotSucceed(string uid, string? reason)
    {
        await using var db = Database();
        var operation = new ServiceRestartOperation { TargetService = "access", Status = "running", DeploymentUid = "uid-1", DeploymentGeneration = 7 };
        var client = Client(db, _ => Task.FromResult(Json(Deployment(operation.Id, uid: uid, reason: reason))));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.TrySyncAsync(operation));
    }

    [Fact]
    public async Task SupersededAnnotationCannotBeReportedAsOurSuccessfulRestart()
    {
        await using var db = Database();
        var operation = new ServiceRestartOperation { TargetService = "access", Status = "running", DeploymentUid = "uid-1", DeploymentGeneration = 7 };
        var client = Client(db, _ => Task.FromResult(Json(Deployment(Guid.NewGuid()))));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.TrySyncAsync(operation));
    }

    [Fact]
    public async Task BoundTargetsAndTimeoutFailBeforePatch()
    {
        await using var db = Database();
        var client = Client(db, _ => throw new Xunit.Sdk.XunitException("РќРµРѕР¶РёРґР°РЅРЅС‹Р№ Р·Р°РїСЂРѕСЃ Рє API"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.TrySyncAsync(new ServiceRestartOperation { TargetService = "postgres" }));
        await Assert.ThrowsAsync<TimeoutException>(() => client.TrySyncAsync(new ServiceRestartOperation { TargetService = "access", RequestedAtUtc = DateTime.UtcNow.AddMinutes(-21) }));
    }

    private sealed class RaceDatabase(DbContextOptions<ManagementDbContext> options) : ManagementDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw new DbUpdateException("Гонка POST", new Npgsql.PostgresException("duplicate", "ERROR", "ERROR", "23505", constraintName: "IX_service_restart_operations_TargetService_active"));
    }
    [Fact]
    public async Task ConcurrentPostUniqueConstraintReturns409()
    {
        await using var db = new RaceDatabase(new DbContextOptionsBuilder<ManagementDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var controller = new AdminOpsController(db, new Catalog(), new NoRestartClient(), null!, Config())
        { ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() } };
        Assert.IsType<ConflictObjectResult>(await controller.CreateRestartOperation(new CreateServiceRestartOperationRequest { TargetService = "workspace" }));
    }

    private sealed class NoRestartClient : IServiceRestartClient
    {
        public Task TriggerRestartAsync(ServiceRestartOperation operation, CancellationToken cancellationToken = default) => throw new Xunit.Sdk.XunitException("РљРѕРЅС‚СЂРѕР»Р»РµСЂ РЅРµ РґРѕР»Р¶РµРЅ РїРµСЂРµР·Р°РїСѓСЃРєР°С‚СЊ Deployment");
        public Task TrySyncAsync(ServiceRestartOperation operation, CancellationToken cancellationToken = default) => throw new Xunit.Sdk.XunitException("GET РЅРµ РґРѕР»Р¶РµРЅ РёР·РјРµРЅСЏС‚СЊ РѕРїРµСЂР°С†РёРё");
    }
    private sealed class Catalog : IServiceCatalogProvider
    {
        private readonly ServiceCatalogEntry entry = new() { ComposeService = "workspace", Kind = "application" };
        public IReadOnlyCollection<ServiceCatalogEntry> GetEntries() => [entry];
        public ServiceCatalogEntry? GetRestartEligibleApplication(string composeService) => composeService == "workspace" ? entry : null;
    }
    [Fact]
    public async Task PostPersistsQueuedOperationReturns202AndDuplicate409WhileGetIsReadOnly()
    {
        await using var db = Database();
        var controller = new AdminOpsController(db, new Catalog(), new NoRestartClient(), null!, Config())
        { ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() } };
        Assert.IsType<AcceptedAtActionResult>(await controller.CreateRestartOperation(new CreateServiceRestartOperationRequest { TargetService = "workspace" }));
        var operation = await db.ServiceRestartOperations.SingleAsync();
        Assert.Equal("queued", operation.Status);
        Assert.IsType<ConflictObjectResult>(await controller.CreateRestartOperation(new CreateServiceRestartOperationRequest { TargetService = "workspace" }));
        Assert.IsType<OkObjectResult>(await controller.GetRestartOperation(operation.Id));
        await controller.GetRestartOperations();
        Assert.Equal("queued", operation.Status); Assert.Single(db.ServiceRestartOperations);
    }
}
