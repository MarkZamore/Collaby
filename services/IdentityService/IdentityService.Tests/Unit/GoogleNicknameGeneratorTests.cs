using IdentityService.Application.Services;
using Xunit;

namespace IdentityService.Tests.Unit;

public sealed class GoogleNicknameGeneratorTests
{
    private readonly GoogleNicknameGenerator _generator = new();

    [Fact]
    public void GenerateBaseNickname_ShouldNormalizeLatinName()
    {
        var nickname = _generator.GenerateBaseNickname("José García", "jose@example.com", "subject-123");

        Assert.Equal("jose_garcia", nickname);
    }

    [Fact]
    public void GenerateBaseNickname_ShouldFallbackForNonLatinCandidates()
    {
        var nickname = _generator.GenerateBaseNickname("Иван", "почта@example.com", "subject-123");

        Assert.StartsWith("collaby_", nickname);
    }
}
