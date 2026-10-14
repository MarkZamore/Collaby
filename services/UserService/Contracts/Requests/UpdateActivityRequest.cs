namespace UserService.Contracts.Requests;

public class UpdateActivityRequest
{
    public Guid? LastActiveWorkspaceId { get; set; }
    public int? TaskCompletedCount { get; set; }
    public int? CommentCount { get; set; }
    public int? WorkspaceVisitCount { get; set; }
}

