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

    [Fact]
    public async Task Until_ManyConcurrentShortRealClockWaits_AllCompleteWithoutFaulting()
    {
        // Regression test for a disposal race in the ITimer-backed delay awaiter: with
        // TimeProvider.System and a very short due time, the timer callback can fire on a
        // thread-pool thread before the scheduling call has finished handing back the ITimer
        // reference on the calling thread. Running many of these concurrently with a
        // near-zero poll interval reliably exercises that race; a regression here would
        // surface as a hang, a faulted task, or (under a debugger/analyzer) a
        // use-after-dispose on the timer.
        const int ConcurrentWaits = 200;

        Task BuildWait()
        {
            var attempts = 0;
            return Await.AtMost(TimeSpan.FromMilliseconds(500))
                .PollInterval(TimeSpan.FromMilliseconds(1))
                .Until(() => ++attempts >= 2);
        }

        var tasks = Enumerable.Range(0, ConcurrentWaits)
            .Select(_ => BuildWait())
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.All(tasks, task => Assert.True(task.IsCompletedSuccessfully));
    }
}
