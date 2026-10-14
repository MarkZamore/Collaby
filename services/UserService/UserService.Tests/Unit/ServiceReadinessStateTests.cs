using Collaby.ServiceDefaults.Health;
using Xunit;

namespace UserService.Tests.Unit;

public class ServiceReadinessStateTests
{
    [Fact]
    public void MarkStarting_ShouldKeepServiceUnreadyAndExposeStage()
    {
        var state = new ServiceReadinessState();

        state.MarkStarting("Bootstrapping identity directory projection");

        Assert.False(state.IsReady);
        Assert.Equal("Bootstrapping identity directory projection", state.StatusDetail);
        Assert.Equal(state.StatusDetail, state.FailureReason);
    }

    [Fact]
    public void MarkReady_ShouldClearStatusDetail()
    {
        var state = new ServiceReadinessState();
        state.MarkStarting("Applying UserService migrations");

        state.MarkReady();

        Assert.True(state.IsReady);
        Assert.Null(state.StatusDetail);
        Assert.Null(state.FailureReason);
    }

    [Fact]
    public void MarkFailed_ShouldKeepServiceUnreadyAndExposeFailure()
    {
        var state = new ServiceReadinessState();

        state.MarkFailed("Bootstrapping identity directory projection: timeout");

        Assert.False(state.IsReady);
        Assert.Equal("Bootstrapping identity directory projection: timeout", state.StatusDetail);
        Assert.Equal(state.StatusDetail, state.FailureReason);
    }
}
