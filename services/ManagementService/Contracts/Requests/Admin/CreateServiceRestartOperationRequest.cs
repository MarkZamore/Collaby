namespace ManagementService.Contracts.Requests.Admin;

public sealed class CreateServiceRestartOperationRequest
{
    public string TargetService { get; set; } = string.Empty;
}
