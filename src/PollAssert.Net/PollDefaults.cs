namespace PollAssert;

/// <summary>
/// Default values used by <see cref="AwaitCondition"/> when a caller does not
/// explicitly configure a setting.
/// </summary>
internal static class PollDefaults
{
    /// <summary>
    /// The default interval, in milliseconds, between two consecutive evaluations
    /// of a condition when no explicit poll interval is configured.
    /// </summary>
    internal const long DefaultPollIntervalMilliseconds = 100;

    /// <summary>
    /// The default interval between two consecutive evaluations of a condition.
    /// </summary>
    internal static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(DefaultPollIntervalMilliseconds);

    /// <summary>
    /// The default delay applied before the first evaluation of a condition.
    /// </summary>
    internal static readonly TimeSpan DefaultInitialDelay = TimeSpan.Zero;

    /// <summary>
    /// Whether predicate exceptions are swallowed and retried by default.
    /// </summary>
    internal const bool DefaultIgnoreExceptions = true;
}
