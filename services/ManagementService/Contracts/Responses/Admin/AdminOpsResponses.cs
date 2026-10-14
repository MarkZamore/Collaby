namespace ManagementService.Contracts.Responses.Admin;

public sealed class AdminOpsOverviewResponse
{
    public IReadOnlyCollection<AdminOpsServiceOverviewResponse> Services { get; set; } = [];
    public IReadOnlyCollection<AdminOpsPanelLinkResponse> PanelLinks { get; set; } = [];
}

public sealed class AdminOpsServiceOverviewResponse
{
    public string ComposeService { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool RestartEligible { get; set; }
    public string? DirectReadinessUrl { get; set; }
    public string? GatewaySmokeUrl { get; set; }
    public AdminOpsHealthProbeResponse? DirectHealth { get; set; }
    public AdminOpsHealthProbeResponse? GatewayHealth { get; set; }
    public IReadOnlyCollection<string> OwnedStateUnits { get; set; } = [];
}

public sealed class AdminOpsHealthProbeResponse
{
    public int? StatusCode { get; set; }
    public bool IsHealthy { get; set; }
    public string? Error { get; set; }
}

public sealed class AdminOpsPanelLinkResponse
{
    public string Key { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public sealed class AdminRestartOperationListResponse
{
    public IReadOnlyCollection<ServiceRestartOperationResponse> Items { get; set; } = [];
}

public sealed class ServiceRestartOperationResponse
{
    public Guid Id { get; set; }
    public string TargetService { get; set; } = string.Empty;
    public IReadOnlyCollection<string> OwnedStateUnits { get; set; } = [];
    public string Status { get; set; } = string.Empty;
    public Guid? RequestedByUserId { get; set; }
    public string? RequestedByEmail { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public long? DeploymentGeneration { get; set; }
    public string? DeploymentUid { get; set; }
    public string? RolloutStatus { get; set; }
    public string? DeploymentNamespace { get; set; }
    public string? FailureMessage { get; set; }
}
