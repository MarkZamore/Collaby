namespace ManagementService.Contracts.Responses;

public class NotificationResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? ReferenceId { get; set; }
    public Guid? SecondaryReferenceId { get; set; }
    public Guid? ActorId { get; set; }
    public bool IsRead { get; set; }
    public string? ActionStatus { get; set; }
    public DateTime CreatedAt { get; set; }
}
