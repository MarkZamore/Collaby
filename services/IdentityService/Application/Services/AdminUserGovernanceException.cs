namespace IdentityService.Application.Services;

public sealed class AdminUserGovernanceException : Exception
{
    public AdminUserGovernanceException(int statusCode, string code, string message)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    public int StatusCode { get; }
    public string Code { get; }
}
