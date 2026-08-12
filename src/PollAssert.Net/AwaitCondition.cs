using System.Text;

namespace PollAssert;

/// <summary>
/// A fluent, repeatable builder that polls a condition until it becomes true or a
/// configured timeout elapses. Instances are created via <see cref="Await.AtMost"/>.
/// </summary>
public sealed class AwaitCondition
{
    private readonly TimeSpan _timeout;
    private TimeSpan _pollInterval = PollDefaults.DefaultPollInterval;
    private TimeSpan _initialDelay = PollDefaults.DefaultInitialDelay;
    private TimeProvider _timeProvider = TimeProvider.System;
    private bool _ignoreExceptions = PollDefaults.DefaultIgnoreExceptions;

    internal AwaitCondition(TimeSpan timeout)
    {
        if (timeout < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout must not be negative.");
        }

        _timeout = timeout;
    }

    /// <summary>
    /// Sets the interval between two consecutive evaluations of the condition.
    /// Defaults to 100 milliseconds.
    /// </summary>
    /// <param name="interval">The poll interval. Must be greater than zero.</param>
    /// <returns>This <see cref="AwaitCondition"/>, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="interval"/> is zero or negative.</exception>
    public AwaitCondition PollInterval(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), interval, "Poll interval must be greater than zero.");
        }

        _pollInterval = interval;
        return this;
    }

    /// <summary>
    /// Sets a delay to wait before the first evaluation of the condition. The
    /// initial delay counts towards the overall <see cref="Await.AtMost"/> timeout,
    /// so the condition is still guaranteed exactly one evaluation even if the
    /// initial delay alone consumes the whole timeout.
    /// </summary>
    /// <param name="delay">The initial delay. Must not be negative.</param>
    /// <returns>This <see cref="AwaitCondition"/>, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="delay"/> is negative.</exception>
    public AwaitCondition WithInitialDelay(TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Initial delay must not be negative.");
        }

        _initialDelay = delay;
        return this;
    }

    /// <summary>
    /// Sets the <see cref="TimeProvider"/> used to measure elapsed time and to
    /// schedule delays. Defaults to <see cref="TimeProvider.System"/>. Inject a
    /// fake provider to drive waits deterministically in tests.
    /// </summary>
    /// <param name="timeProvider">The time provider to use.</param>
    /// <returns>This <see cref="AwaitCondition"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is null.</exception>
    public AwaitCondition WithTimeProvider(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        return this;
    }

    /// <summary>
    /// Controls whether exceptions thrown while evaluating the condition are
    /// swallowed and treated as "not yet ready", so the wait retries until the
    /// timeout instead of failing immediately. Enabled by default.
    /// </summary>
    /// <param name="ignoreExceptions">
    /// <see langword="true"/> to swallow predicate exceptions and retry;
    /// <see langword="false"/> to let the first predicate exception propagate immediately.
    /// </param>
    /// <returns>This <see cref="AwaitCondition"/>, for chaining.</returns>
    public AwaitCondition IgnoreExceptions(bool ignoreExceptions = true)
    {
        _ignoreExceptions = ignoreExceptions;
        return this;
    }

    /// <summary>
    /// Polls the given synchronous predicate until it returns <see langword="true"/>
    /// or the timeout elapses.
    /// </summary>
    /// <param name="condition">The predicate to evaluate on each poll.</param>
    /// <returns>A task that completes once the condition becomes true.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="condition"/> is null.</exception>
    /// <exception cref="ConditionTimeoutException">The condition did not become true within the timeout.</exception>
    public async Task Until(Func<bool> condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        await RunAsync(() => Task.FromResult(condition()), static result => result).ConfigureAwait(false);
    }

    /// <summary>
    /// Polls the given asynchronous predicate until it returns <see langword="true"/>
    /// or the timeout elapses.
    /// </summary>
    /// <param name="condition">The asynchronous predicate to evaluate on each poll.</param>
    /// <returns>A task that completes once the condition becomes true.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="condition"/> is null.</exception>
    /// <exception cref="ConditionTimeoutException">The condition did not become true within the timeout.</exception>
    public async Task Until(Func<Task<bool>> condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        await RunAsync(condition, static result => result).ConfigureAwait(false);
    }

    /// <summary>
    /// Polls the given synchronous value provider until the produced value
    /// satisfies <paramref name="matcher"/>, or the timeout elapses.
    /// </summary>
    /// <typeparam name="T">The type of value being probed.</typeparam>
    /// <param name="valueProvider">A function that produces the current value on each poll.</param>
    /// <param name="matcher">A predicate that decides whether the produced value satisfies the wait.</param>
    /// <returns>A task that completes with the matching value once the wait succeeds.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="valueProvider"/> or <paramref name="matcher"/> is null.</exception>
    /// <exception cref="ConditionTimeoutException">No produced value satisfied <paramref name="matcher"/> within the timeout.</exception>
    public async Task<T> Until<T>(Func<T> valueProvider, Func<T, bool> matcher)
    {
        ArgumentNullException.ThrowIfNull(valueProvider);
        ArgumentNullException.ThrowIfNull(matcher);
        return await RunAsync(() => Task.FromResult(valueProvider()), matcher).ConfigureAwait(false);
    }

    /// <summary>
    /// Polls the given asynchronous value provider until the produced value
    /// satisfies <paramref name="matcher"/>, or the timeout elapses.
    /// </summary>
    /// <typeparam name="T">The type of value being probed.</typeparam>
    /// <param name="valueProvider">An asynchronous function that produces the current value on each poll.</param>
    /// <param name="matcher">A predicate that decides whether the produced value satisfies the wait.</param>
    /// <returns>A task that completes with the matching value once the wait succeeds.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="valueProvider"/> or <paramref name="matcher"/> is null.</exception>
    /// <exception cref="ConditionTimeoutException">No produced value satisfied <paramref name="matcher"/> within the timeout.</exception>
    public async Task<T> Until<T>(Func<Task<T>> valueProvider, Func<T, bool> matcher)
    {
        ArgumentNullException.ThrowIfNull(valueProvider);
        ArgumentNullException.ThrowIfNull(matcher);
        return await RunAsync(valueProvider, matcher).ConfigureAwait(false);
    }

    private async Task<T> RunAsync<T>(Func<Task<T>> probe, Func<T, bool> matcher)
    {
        var startTimestamp = _timeProvider.GetTimestamp();

        if (_initialDelay > TimeSpan.Zero)
        {
            await DelayAsync(_initialDelay, _timeProvider).ConfigureAwait(false);
        }

        var pollCount = 0;
        var hasValue = false;
        T? lastValue = default;
        Exception? lastException = null;

        while (true)
        {
            pollCount++;

            try
            {
                var value = await probe().ConfigureAwait(false);
                lastValue = value;
                hasValue = true;
                lastException = null;

                if (matcher(value))
                {
                    return value;
                }
            }
            catch (Exception ex) when (_ignoreExceptions)
            {
                lastException = ex;
                hasValue = false;
            }

            var elapsed = _timeProvider.GetElapsedTime(startTimestamp);
            if (elapsed >= _timeout)
            {
                throw CreateTimeoutException(elapsed, pollCount, hasValue, lastValue, lastException);
            }

            var remaining = _timeout - elapsed;
            var delay = remaining < _pollInterval ? remaining : _pollInterval;
            if (delay > TimeSpan.Zero)
            {
                await DelayAsync(delay, _timeProvider).ConfigureAwait(false);
            }
        }
    }

    private static Task DelayAsync(TimeSpan delay, TimeProvider timeProvider)
    {
        var completionSource = new TaskCompletionSource();
        ITimer? timer = null;
        timer = timeProvider.CreateTimer(
            _ =>
            {
                completionSource.TrySetResult();
                timer?.Dispose();
            },
            state: null,
            dueTime: delay,
            period: Timeout.InfiniteTimeSpan);

        return completionSource.Task;
    }

    private static ConditionTimeoutException CreateTimeoutException<T>(
        TimeSpan waited,
        int pollCount,
        bool hasValue,
        T? lastValue,
        Exception? lastException)
    {
        var message = BuildTimeoutMessage(waited, pollCount, hasValue, lastValue, lastException);
        return lastException is null
            ? new ConditionTimeoutException(message)
            : new ConditionTimeoutException(message, lastException);
    }

    private static string BuildTimeoutMessage<T>(TimeSpan waited, int pollCount, bool hasValue, T? lastValue, Exception? lastException)
    {
        const string PollSuffixSingular = "poll";
        const string PollSuffixPlural = "polls";

        var builder = new StringBuilder();
        builder.Append("Condition was not met within ")
            .Append(waited.TotalMilliseconds.ToString("F0"))
            .Append(" ms after ")
            .Append(pollCount)
            .Append(' ')
            .Append(pollCount == 1 ? PollSuffixSingular : PollSuffixPlural)
            .Append('.');

        if (lastException is not null)
        {
            builder.Append(" Last poll threw ")
                .Append(lastException.GetType().Name)
                .Append(": ")
                .Append(lastException.Message);
        }
        else if (hasValue)
        {
            builder.Append(" Last observed value: ")
                .Append(FormatValue(lastValue));
        }
        else
        {
            builder.Append(" No value was observed.");
        }

        return builder.ToString();
    }

    private static string FormatValue<T>(T? value)
    {
        const string NullValueText = "null";
        return value is null ? NullValueText : value.ToString() ?? NullValueText;
    }
}
