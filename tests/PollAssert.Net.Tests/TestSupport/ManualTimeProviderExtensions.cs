namespace PollAssert.Tests.TestSupport;

/// <summary>
/// Test-only helpers for driving a <see cref="ManualTimeProvider"/> from an async test.
/// The .NET runtime may defer a deeply chained task continuation to the thread pool
/// instead of running it inline (a stack-depth safety valve), so a poll loop's next
/// timer registration is not always visible immediately after <see cref="ManualTimeProvider.Advance"/>
/// returns. <see cref="AdvanceUntilAsync"/> advances the clock and then polls, with short
/// real waits, until the caller-supplied condition is satisfied, so the next assertion
/// or the next <see cref="ManualTimeProvider.Advance"/> call sees an up-to-date timer
/// registration instead of racing a deferred continuation.
/// </summary>
internal static class ManualTimeProviderExtensions
{
    private static readonly TimeSpan SettlePollInterval = TimeSpan.FromMilliseconds(2);
    private static readonly TimeSpan SettleCeiling = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan BriefSettleWindow = TimeSpan.FromMilliseconds(50);

    internal static async Task AdvanceUntilAsync(this ManualTimeProvider provider, TimeSpan delta, Func<bool> settled)
    {
        provider.Advance(delta);
        await WaitUntilAsync(settled);
    }

    /// <summary>
    /// Advances the clock and waits a short, fixed real-time window rather than for a
    /// condition to become true. Use this before asserting that nothing happened, since
    /// there is no target state to poll for.
    /// </summary>
    internal static async Task AdvanceAndSettleBrieflyAsync(this ManualTimeProvider provider, TimeSpan delta)
    {
        provider.Advance(delta);
        await Task.Delay(BriefSettleWindow);
    }

    internal static async Task WaitUntilAsync(Func<bool> settled)
    {
        var deadline = DateTime.UtcNow + SettleCeiling;
        while (!settled() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(SettlePollInterval);
        }
    }
}
