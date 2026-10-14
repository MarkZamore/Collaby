namespace MessengerService.Contracts.Requests;

public class CreatePrivateChatRequest
{
    public Guid ParticipantId { get; set; }
}

public class CreateProjectChatRequest
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public Guid ProjectId { get; set; }
    public Guid WorkspaceId { get; set; }
    public List<Guid>? MemberIds { get; set; }
}

public class CreateChannelRequest
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public Guid WorkspaceId { get; set; }
    public List<Guid>? MemberIds { get; set; }
}

public class UpdateChatRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
}

public class AddChatMembersRequest
{
    public List<Guid> UserIds { get; set; } = new();
}
