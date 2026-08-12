using PollAssert.Tests.TestSupport;
using PollAssert;

namespace PollAssert.Tests.Timing;

public class TimeoutBoundaryTests
{
    [Fact]
    public async Task Until_WithZeroTimeout_EvaluatesExactlyOnce()
    {
        var provider = new ManualTimeProvider();
        var callCount = 0;

        var exception = await Assert.ThrowsAsync<ConditionTimeoutException>(() =>
            Await.AtMost(TimeSpan.Zero)
                .WithTimeProvider(provider)
                .Until(() =>
                {
                    callCount++;
                    return false;
                }));

        Assert.Equal(1, callCount);
        Assert.Contains("1 poll.", exception.Message);
    }

    [Fact]
    public async Task Until_WithZeroTimeoutAndTruePredicate_SucceedsOnFirstEvaluation()
    {
        var provider = new ManualTimeProvider();

        await Await.AtMost(TimeSpan.Zero)
            .WithTimeProvider(provider)
            .Until(static () => true);
    }

    [Fact]
    public async Task Until_EvaluatesExactlyOnceMoreAfterTimeElapsesPastTheDeadline()
    {
        var provider = new ManualTimeProvider();
        var callCount = 0;

        var task = Await.AtMost(TimeSpan.FromSeconds(1))
            .PollInterval(TimeSpan.FromSeconds(1))
            .WithTimeProvider(provider)
            .Until(() =>
            {
                callCount++;
                return callCount == 2;
            });

        Assert.Equal(1, callCount);

        provider.Advance(TimeSpan.FromSeconds(1));

        await task;
        Assert.Equal(2, callCount);
    }
}
