namespace UserService.Domain.Entities;

public class UserActivity
{
    public Guid UserId { get; set; }

    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

    public Guid? LastActiveWorkspaceId { get; set; }

    public int TaskCompletedCount { get; set; }

    public int CommentCount { get; set; }

    public int WorkspaceVisitCount { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

