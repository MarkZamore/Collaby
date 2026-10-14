using System.Security.Claims;
using System.Text.Json;
using ManagementService.Application.Interfaces;
using ManagementService.Contracts.Requests.Admin;
using ManagementService.Contracts.Responses.Admin;
using ManagementService.Domain.Entities;
using ManagementService.Infrastructure.Data;
using Collaby.ServiceDefaults.SystemAdministration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagementService.Controllers;

[ApiController]
[Authorize(Policy = SystemAdminConstants.PolicyName)]
[Route("admin/ops")]
public sealed class AdminOpsController : ControllerBase
{
    private readonly ManagementDbContext _dbContext;
    private readonly IServiceCatalogProvider _serviceCatalogProvider;
    private readonly IServiceRestartClient _serviceRestartClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public AdminOpsController(
        ManagementDbContext dbContext,
        IServiceCatalogProvider serviceCatalogProvider,
        IServiceRestartClient serviceRestartClient,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _serviceCatalogProvider = serviceCatalogProvider;
        _serviceRestartClient = serviceRestartClient;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    [HttpGet("overview")]
    public async Task<ActionResult<AdminOpsOverviewResponse>> GetOverview(CancellationToken cancellationToken = default)
    {
        var entries = _serviceCatalogProvider.GetEntries().OrderBy(entry => entry.ComposeService).ToArray();
        var services = await Task.WhenAll(entries.Select(entry => BuildOverviewAsync(entry, cancellationToken)));

        return Ok(new AdminOpsOverviewResponse
        {
            Services = services,
            PanelLinks = BuildPanelLinks()
        });
    }

    [HttpGet("restart-operations")]
    public async Task<ActionResult<AdminRestartOperationListResponse>> GetRestartOperations(CancellationToken cancellationToken = default)
    {
        var operations = await _dbContext.ServiceRestartOperations
            .OrderByDescending(operation => operation.RequestedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);


        return Ok(new AdminRestartOperationListResponse
        {
            Items = operations.Select(MapOperation).ToArray()
        });
    }

    [HttpGet("restart-operations/{operationId:guid}")]
    public async Task<IActionResult> GetRestartOperation(Guid operationId, CancellationToken cancellationToken = default)
    {
        var operation = await _dbContext.ServiceRestartOperations.FirstOrDefaultAsync(entity => entity.Id == operationId, cancellationToken);
        if (operation is null)
        {
            return NotFound(new { code = "operation_not_found", message = "Restart operation not found." });
        }

        return Ok(MapOperation(operation));
    }

    [HttpPost("restart-operations")]
    public async Task<IActionResult> CreateRestartOperation(
        [FromBody] CreateServiceRestartOperationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.TargetService))
        {
            return BadRequest(new { code = "invalid_target", message = "TargetService is required." });
        }

        var target = _serviceCatalogProvider.GetRestartEligibleApplication(request.TargetService.Trim());
        if (target is null)
        {
            return BadRequest(new { code = "invalid_target", message = "Only active application services from service-catalog.json can be restarted." });
        }

        if (await _dbContext.ServiceRestartOperations.AnyAsync(o => o.TargetService == target.ComposeService && (o.Status == "queued" || o.Status == "triggered" || o.Status == "running"), cancellationToken))
            return Conflict(new { code = "restart_in_progress", message = "Перезапуск этого сервиса уже выполняется." });

        var operation = new ServiceRestartOperation
        {
            TargetService = target.ComposeService,
            OwnedStateUnitsJson = JsonSerializer.Serialize(target.OwnedStateUnits),
            Status = ServiceRestartOperationStatuses.Queued,
            RequestedByUserId = GetCurrentUserId(),
            RequestedByEmail = GetCurrentUserEmail(),
            RequestedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _dbContext.ServiceRestartOperations.Add(operation);
        try { await _dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505", ConstraintName: "IX_service_restart_operations_TargetService_active" })
        {
            return Conflict(new { code = "restart_in_progress", message = "Перезапуск этого сервиса уже выполняется." });
        }

        return AcceptedAtAction(nameof(GetRestartOperation), new { operationId = operation.Id }, MapOperation(operation));
    }

    private async Task<AdminOpsServiceOverviewResponse> BuildOverviewAsync(ServiceCatalogEntry entry, CancellationToken cancellationToken)
    {
        return new AdminOpsServiceOverviewResponse
        {
            ComposeService = entry.ComposeService,
            Kind = entry.Kind,
            IsActive = true,
            RestartEligible = _serviceCatalogProvider.GetRestartEligibleApplication(entry.ComposeService) is not null,
            DirectReadinessUrl = entry.DirectReadinessUrl,
            GatewaySmokeUrl = entry.GatewaySmokeUrl,
            DirectHealth = await ProbeAsync(entry.DirectReadinessUrl, cancellationToken),
            GatewayHealth = await ProbeAsync(entry.GatewaySmokeUrl, cancellationToken),
            OwnedStateUnits = entry.OwnedStateUnits
        };
    }

    private async Task<AdminOpsHealthProbeResponse?> ProbeAsync(string? url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            using var response = await client.GetAsync(url, cancellationToken);
            return new AdminOpsHealthProbeResponse
            {
                StatusCode = (int)response.StatusCode,
                IsHealthy = response.IsSuccessStatusCode
            };
        }
        catch (Exception ex)
        {
            return new AdminOpsHealthProbeResponse
            {
                IsHealthy = false,
                Error = ex.Message
            };
        }
    }

    private IReadOnlyCollection<AdminOpsPanelLinkResponse> BuildPanelLinks()
    {
        var links = new Dictionary<string,string> {
            ["admin"]="http://localhost:8080/admin", ["swagger"]="http://localhost:8080/swagger",
            ["logs"]="http://localhost:3001", ["server"]="http://localhost:19999", ["kafka"]="http://localhost:8082",
            ["pg"]="http://localhost:5050", ["mongo"]="http://localhost:8081", ["redis"]="http://localhost:5540", ["portainer"]="http://localhost:9000", ["mailpit"]="http://localhost:8025" };
        return links.Select(pair => new AdminOpsPanelLinkResponse { Key=pair.Key, Url=pair.Value }).ToArray();
    }

    private static ServiceRestartOperationResponse MapOperation(ServiceRestartOperation operation)
    {
        return new ServiceRestartOperationResponse
        {
            Id = operation.Id,
            TargetService = operation.TargetService,
            OwnedStateUnits = JsonSerializer.Deserialize<string[]>(operation.OwnedStateUnitsJson) ?? [],
            Status = operation.Status,
            RequestedByUserId = operation.RequestedByUserId,
            RequestedByEmail = operation.RequestedByEmail,
            RequestedAtUtc = operation.RequestedAtUtc,
            UpdatedAtUtc = operation.UpdatedAtUtc,
            CompletedAtUtc = operation.CompletedAtUtc,
            DeploymentGeneration = operation.DeploymentGeneration,
            DeploymentUid = operation.DeploymentUid,
            RolloutStatus = operation.RolloutStatus,
            DeploymentNamespace = operation.DeploymentNamespace,
            FailureMessage = operation.FailureMessage
        };
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    private string? GetCurrentUserEmail()
    {
        return User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");
    }
}
