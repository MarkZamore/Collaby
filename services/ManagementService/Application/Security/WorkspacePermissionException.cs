namespace ManagementService.Application.Security;

public sealed class WorkspacePermissionException : UnauthorizedAccessException
{
    public WorkspacePermissionException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
