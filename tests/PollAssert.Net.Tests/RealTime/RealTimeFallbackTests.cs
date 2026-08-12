using System.Diagnostics;
using PollAssert;

namespace PollAssert.Tests.RealTime;

public class RealTimeFallbackTests
{
    private const int TimeoutMilliseconds = 500;
    private const int PollIntervalMilliseconds = 20;
    private const int ConditionTrueAfterMilliseconds = 120;

    [Fact]
    public async Task Until_UsesRealSystemClockByDefault()
    {
        var stopwatch = Stopwatch.StartNew();
        var flipAt = DateTime.UtcNow.AddMilliseconds(ConditionTrueAfterMilliseconds);

        await Await.AtMost(TimeSpan.FromMilliseconds(TimeoutMilliseconds))
            .PollInterval(TimeSpan.FromMilliseconds(PollIntervalMilliseconds))
            .Until(() => DateTime.UtcNow >= flipAt);

        stopwatch.Stop();
        Assert.True(stopwatch.ElapsedMilliseconds < TimeoutMilliseconds);
    }

    [Fact]
    public async Task Until_ThrowsConditionTimeoutException_WhenConditionNeverBecomesTrueUsingRealClock()
    {
        var exception = await Assert.ThrowsAsync<ConditionTimeoutException>(() =>
            Await.AtMost(TimeSpan.FromMilliseconds(150))
                .PollInterval(TimeSpan.FromMilliseconds(20))
                .Until(static () => false));

        Assert.Contains("ms after", exception.Message);
    }
}
