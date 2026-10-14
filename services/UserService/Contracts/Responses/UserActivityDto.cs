namespace UserService.Contracts.Responses;

public class UserActivityDto
{
    public Guid UserId { get; set; }
    public DateTime LastActiveAt { get; set; }
    public Guid? LastActiveWorkspaceId { get; set; }
    public int TaskCompletedCount { get; set; }
    public int CommentCount { get; set; }
    public int WorkspaceVisitCount { get; set; }
    public DateTime UpdatedAt { get; set; }
}

