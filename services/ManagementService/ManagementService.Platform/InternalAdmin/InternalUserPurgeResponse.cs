namespace ManagementService.Platform.InternalAdmin;

public static class InternalUserPurgeStatuses
{
    public const string Completed = "completed";
    public const string Noop = "noop";
}

public sealed class InternalUserPurgeResponse
{
    public string Status { get; set; } = InternalUserPurgeStatuses.Completed;
    public int DeletedRecords { get; set; }
}
