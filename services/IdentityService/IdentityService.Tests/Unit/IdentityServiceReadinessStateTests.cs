using IdentityService.Infrastructure.Health;
using Xunit;

namespace IdentityService.Tests.Unit;

public sealed class IdentityServiceReadinessStateTests
{
    [Fact]
    public void MarkFailed_ThenMarkReady_ShouldTrackCurrentState()
    {
        var state = new IdentityServiceReadinessState();

        state.MarkFailed("dependency offline");

        Assert.False(state.IsReady);
        Assert.Equal("dependency offline", state.FailureReason);

        state.MarkReady();

        Assert.True(state.IsReady);
        Assert.Null(state.FailureReason);
    }
}
