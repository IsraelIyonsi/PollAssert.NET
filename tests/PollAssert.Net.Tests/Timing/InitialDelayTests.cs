using PollAssert.Tests.TestSupport;
using PollAssert;

namespace PollAssert.Tests.Timing;

public class InitialDelayTests
{
    [Fact]
    public async Task Until_DoesNotEvaluateBeforeInitialDelayElapses()
    {
        var provider = new ManualTimeProvider();
        var callCount = 0;

        var task = Await.AtMost(TimeSpan.FromSeconds(10))
            .WithInitialDelay(TimeSpan.FromSeconds(3))
            .WithTimeProvider(provider)
            .Until(() =>
            {
                callCount++;
                return true;
            });

        Assert.Equal(0, callCount);

        provider.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(0, callCount);

        provider.Advance(TimeSpan.FromSeconds(1));
        await task;
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task Until_StillEvaluatesOnceWhenInitialDelayConsumesEntireTimeoutAndConditionIsTrue()
    {
        var provider = new ManualTimeProvider();
        var callCount = 0;

        var task = Await.AtMost(TimeSpan.FromSeconds(2))
            .WithInitialDelay(TimeSpan.FromSeconds(5))
            .WithTimeProvider(provider)
            .Until(() =>
            {
                callCount++;
                return true;
            });

        provider.Advance(TimeSpan.FromSeconds(5));

        await task;
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task Until_ThrowsAfterSingleEvaluation_WhenInitialDelayConsumesEntireTimeoutAndConditionIsFalse()
    {
        var provider = new ManualTimeProvider();
        var callCount = 0;

        var task = Await.AtMost(TimeSpan.FromSeconds(2))
            .WithInitialDelay(TimeSpan.FromSeconds(5))
            .WithTimeProvider(provider)
            .Until(() =>
            {
                callCount++;
                return false;
            });

        provider.Advance(TimeSpan.FromSeconds(5));

        var exception = await Assert.ThrowsAsync<ConditionTimeoutException>(() => task);
        Assert.Equal(1, callCount);
        Assert.Contains("1 poll.", exception.Message);
    }
}
