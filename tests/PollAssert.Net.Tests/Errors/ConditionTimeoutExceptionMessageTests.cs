using PollAssert.Tests.TestSupport;
using PollAssert;

namespace PollAssert.Tests.Errors;

public class ConditionTimeoutExceptionMessageTests
{
    [Fact]
    public async Task Timeout_IncludesLastObservedValueInMessage()
    {
        var provider = new ManualTimeProvider();

        var task = Await.AtMost(TimeSpan.FromSeconds(1))
            .PollInterval(TimeSpan.FromMilliseconds(500))
            .WithTimeProvider(provider)
            .Until(static () => 42, static n => n == 999);

        provider.Advance(TimeSpan.FromMilliseconds(500));
        provider.Advance(TimeSpan.FromMilliseconds(500));

        var exception = await Assert.ThrowsAsync<ConditionTimeoutException>(() => task);
        Assert.Contains("Last observed value: 42", exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public async Task Timeout_IncludesLastExceptionInMessageAndAsInnerException()
    {
        var provider = new ManualTimeProvider();
        var failure = new InvalidOperationException("not ready yet");

        var task = Await.AtMost(TimeSpan.FromSeconds(1))
            .PollInterval(TimeSpan.FromMilliseconds(500))
            .WithTimeProvider(provider)
            .Until(bool () => throw failure);

        provider.Advance(TimeSpan.FromMilliseconds(500));
        provider.Advance(TimeSpan.FromMilliseconds(500));

        var exception = await Assert.ThrowsAsync<ConditionTimeoutException>(() => task);
        Assert.Same(failure, exception.InnerException);
        Assert.Contains("Last poll threw InvalidOperationException: not ready yet", exception.Message);
    }

    [Fact]
    public async Task Timeout_ReportsNoValueObserved_WhenEveryPollThrowsAndThenTimesOutWithoutASuccessfulPoll()
    {
        var provider = new ManualTimeProvider();

        var task = Await.AtMost(TimeSpan.FromMilliseconds(500))
            .PollInterval(TimeSpan.FromMilliseconds(500))
            .WithTimeProvider(provider)
            .Until(int () => throw new InvalidOperationException("still failing"), static n => n == 1);

        provider.Advance(TimeSpan.FromMilliseconds(500));

        var exception = await Assert.ThrowsAsync<ConditionTimeoutException>(() => task);
        Assert.Contains("Last poll threw InvalidOperationException: still failing", exception.Message);
        Assert.DoesNotContain("No value was observed.", exception.Message);
    }

    [Fact]
    public async Task Timeout_MessageStatesWaitedDurationAndPollCount()
    {
        var provider = new ManualTimeProvider();

        var task = Await.AtMost(TimeSpan.FromMilliseconds(300))
            .PollInterval(TimeSpan.FromMilliseconds(100))
            .WithTimeProvider(provider)
            .Until(static () => false);

        provider.Advance(TimeSpan.FromMilliseconds(100));
        provider.Advance(TimeSpan.FromMilliseconds(100));
        provider.Advance(TimeSpan.FromMilliseconds(100));

        var exception = await Assert.ThrowsAsync<ConditionTimeoutException>(() => task);
        Assert.StartsWith("Condition was not met within 300 ms after 4 polls.", exception.Message);
    }
}
