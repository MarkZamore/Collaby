namespace IdentityService.Application.Interfaces;

public interface IGoogleNicknameGenerator
{
    string GenerateBaseNickname(string? name, string email, string subject);
    string GenerateSuffix(string subject, string email, int length = 6);
}
