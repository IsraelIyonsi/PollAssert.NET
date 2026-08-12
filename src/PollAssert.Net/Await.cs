namespace PollAssert;

/// <summary>
/// Entry point for building a polling wait, in the style of the Java Awaitility
/// library: <c>Await.AtMost(timeout).PollInterval(interval).Until(condition)</c>.
/// </summary>
public static class Await
{
    /// <summary>
    /// Starts building a wait that gives up and throws <see cref="ConditionTimeoutException"/>
    /// after <paramref name="timeout"/> has elapsed without the condition becoming true.
    /// </summary>
    /// <param name="timeout">The maximum total time to wait, including any initial delay. Must not be negative.</param>
    /// <returns>A fluent <see cref="AwaitCondition"/> builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
    public static AwaitCondition AtMost(TimeSpan timeout)
    {
        return new AwaitCondition(timeout);
    }
}
