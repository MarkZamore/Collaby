using System.Text;
using System.Text.Json;
using ManagementService.Application.Interfaces;
using ManagementService.Domain.Entities;
using ManagementService.Infrastructure.Data;

namespace ManagementService.Application.Services;

public sealed class KubernetesAdminRestartClient : IServiceRestartClient
{
    private readonly IConfiguration _configuration;
    private readonly ManagementDbContext _db;
    private readonly Func<HttpClient>? _clientFactory;
    private static readonly string[] Allowed = ["access", "workspace", "realtime", "engagement", "assistant"];
    public KubernetesAdminRestartClient(IConfiguration configuration, ManagementDbContext db, Func<HttpClient>? clientFactory = null)
    { _configuration = configuration; _db = db; _clientFactory = clientFactory; }

    private HttpClient CreateClient() => _clientFactory?.Invoke()
        ?? InClusterKubernetesHttpClient.Create("/var/run/secrets/kubernetes.io/serviceaccount");

    private string PathFor(ServiceRestartOperation operation)
    {
        if (!Allowed.Contains(operation.TargetService)) throw new InvalidOperationException("Сервис запрещён для перезапуска.");
        var ns = operation.DeploymentNamespace ?? _configuration["Kubernetes:Namespace"] ?? "collaby";
        return $"/apis/apps/v1/namespaces/{Uri.EscapeDataString(ns)}/deployments/{operation.TargetService}";
    }

    public async Task TriggerRestartAsync(ServiceRestartOperation operation, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var path = PathFor(operation);
        using var deployment = JsonDocument.Parse(await client.GetStringAsync(path, cancellationToken));
        var root = deployment.RootElement;
        var uid = root.GetProperty("metadata").GetProperty("uid").GetString();
        if (operation.DeploymentUid is not null && operation.DeploymentUid != uid) throw new InvalidOperationException("Deployment был пересоздан; операция остановлена.");
        operation.DeploymentUid = uid;
        operation.DeploymentNamespace ??= _configuration["Kubernetes:Namespace"] ?? "collaby";
        operation.UpdatedAtUtc = DateTime.UtcNow;
        operation.Status = ServiceRestartOperationStatuses.Triggered;
        await _db.SaveChangesAsync(cancellationToken);
        var metadata = root.GetProperty("spec").GetProperty("template").GetProperty("metadata");
        if (!metadata.TryGetProperty("annotations", out var annotations) || !annotations.TryGetProperty("collaby.io/restart-operation", out var annotation) || annotation.GetString() != operation.Id.ToString())
        {
            var patch = new { metadata = new { resourceVersion = root.GetProperty("metadata").GetProperty("resourceVersion").GetString() }, spec = new { template = new { metadata = new { annotations = new Dictionary<string,string> { ["collaby.io/restart-operation"] = operation.Id.ToString() } } } } };
            using var request = new HttpRequestMessage(HttpMethod.Patch, path) { Content = new StringContent(JsonSerializer.Serialize(patch), Encoding.UTF8, "application/merge-patch+json") };
            using var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var updated = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            operation.DeploymentGeneration = updated.RootElement.GetProperty("metadata").GetProperty("generation").GetInt64();
        }
        else operation.DeploymentGeneration = root.GetProperty("metadata").GetProperty("generation").GetInt64();
        operation.Status = ServiceRestartOperationStatuses.Running;
        operation.RolloutStatus = "waiting";
        operation.UpdatedAtUtc = DateTime.UtcNow;
    }

    public async Task TrySyncAsync(ServiceRestartOperation operation, CancellationToken cancellationToken = default)
    {
        if (operation.Status is ServiceRestartOperationStatuses.Succeeded or ServiceRestartOperationStatuses.Failed) return;
        if (DateTime.UtcNow - operation.RequestedAtUtc > TimeSpan.FromMinutes(20)) throw new TimeoutException("Время ожидания готовности Deployment истекло.");
        if (operation.Status is ServiceRestartOperationStatuses.Queued or ServiceRestartOperationStatuses.Triggered) { await TriggerRestartAsync(operation, cancellationToken); return; }
        using var client = CreateClient();
        using var deployment = JsonDocument.Parse(await client.GetStringAsync(PathFor(operation), cancellationToken));
        var root = deployment.RootElement;
        if (root.GetProperty("metadata").GetProperty("uid").GetString() != operation.DeploymentUid) throw new InvalidOperationException("Deployment UID изменился.");
        var annotations = root.GetProperty("spec").GetProperty("template").GetProperty("metadata");
        if (!annotations.TryGetProperty("annotations", out var values) || !values.TryGetProperty("collaby.io/restart-operation", out var value) || value.GetString() != operation.Id.ToString())
            throw new InvalidOperationException("Перезапуск был заменён другой операцией.");
        if (!root.TryGetProperty("status", out var status)) return;
        if (status.TryGetProperty("conditions", out var conditions) && conditions.EnumerateArray().Any(c => c.TryGetProperty("reason", out var reason) && reason.GetString() == "ProgressDeadlineExceeded")) throw new InvalidOperationException("Deployment сообщил ProgressDeadlineExceeded.");
        var desired = root.GetProperty("spec").GetProperty("replicas").GetInt32();
        if (desired > 0 && operation.DeploymentGeneration is not null && status.TryGetProperty("observedGeneration", out var generation) && generation.GetInt64() >= operation.DeploymentGeneration &&
            status.TryGetProperty("updatedReplicas", out var updated) && updated.GetInt32() == desired &&
            status.TryGetProperty("availableReplicas", out var available) && available.GetInt32() == desired &&
            status.TryGetProperty("replicas", out var replicas) && replicas.GetInt32() == desired)
        {
            operation.Status = ServiceRestartOperationStatuses.Succeeded;
            operation.RolloutStatus = "ready";
            operation.CompletedAtUtc = DateTime.UtcNow;
        }
        operation.UpdatedAtUtc = DateTime.UtcNow;
    }
}
