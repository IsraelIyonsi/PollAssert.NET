namespace PollAssert;

/// <summary>
/// Thrown when a condition passed to <see cref="AwaitCondition"/> does not become
/// true within the configured timeout.
/// </summary>
public sealed class ConditionTimeoutException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConditionTimeoutException"/> class.
    /// </summary>
    /// <param name="message">A message describing how long the wait ran, how many polls occurred, and the last observed value or exception.</param>
    public ConditionTimeoutException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConditionTimeoutException"/> class,
    /// wrapping the exception thrown by the last predicate evaluation.
    /// </summary>
    /// <param name="message">A message describing how long the wait ran, how many polls occurred, and the last observed value or exception.</param>
    /// <param name="innerException">The exception thrown by the last predicate evaluation.</param>
    public ConditionTimeoutException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
