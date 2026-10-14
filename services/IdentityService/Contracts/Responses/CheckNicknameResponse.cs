namespace IdentityService.Contracts.Responses;

public class CheckNicknameResponse
{
    public string Nickname { get; set; } = string.Empty;
    public bool Available { get; set; }
    public string Message { get; set; } = string.Empty;
}
