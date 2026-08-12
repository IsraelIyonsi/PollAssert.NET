using PollAssert.Tests.TestSupport;
using PollAssert;

namespace PollAssert.Tests.Timing;

public class PollIntervalTests
{
    [Fact]
    public async Task Until_DoesNotPollAgainBeforePollIntervalElapses()
    {
        var provider = new ManualTimeProvider();
        var callCount = 0;

        var task = Await.AtMost(TimeSpan.FromSeconds(10))
            .PollInterval(TimeSpan.FromSeconds(2))
            .WithTimeProvider(provider)
            .Until(() =>
            {
                callCount++;
                return callCount >= 3;
            });

        Assert.Equal(1, callCount);

        provider.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(1, callCount);

        provider.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(2, callCount);

        provider.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(3, callCount);

        await task;
    }

    [Fact]
    public async Task Until_HonorsDefaultPollIntervalWhenNoneConfigured()
    {
        var provider = new ManualTimeProvider();
        var callCount = 0;

        var task = Await.AtMost(TimeSpan.FromSeconds(1))
            .WithTimeProvider(provider)
            .Until(() =>
            {
                callCount++;
                return callCount == 2;
            });

        Assert.Equal(1, callCount);

        provider.Advance(TimeSpan.FromMilliseconds(99));
        Assert.Equal(1, callCount);

        provider.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(2, callCount);

        await task;
    }

    [Fact]
    public async Task Until_TruncatesFinalIntervalToAvoidOvershootingTimeout()
    {
        var provider = new ManualTimeProvider();
        var callCount = 0;

        var task = Await.AtMost(TimeSpan.FromSeconds(5))
            .PollInterval(TimeSpan.FromSeconds(2))
            .WithTimeProvider(provider)
            .Until(() =>
            {
                callCount++;
                return false;
            });

        provider.Advance(TimeSpan.FromSeconds(2));
        provider.Advance(TimeSpan.FromSeconds(2));
        provider.Advance(TimeSpan.FromSeconds(1));

        var exception = await Assert.ThrowsAsync<ConditionTimeoutException>(() => task);
        Assert.Equal(4, callCount);
        Assert.Contains("5000 ms", exception.Message);
        Assert.Contains("4 polls.", exception.Message);
    }
}
