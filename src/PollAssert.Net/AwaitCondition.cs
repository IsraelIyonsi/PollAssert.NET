using System.Runtime.CompilerServices;
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
    /// <remarks>
    /// If <paramref name="delay"/> is greater than the timeout passed to <see cref="Await.AtMost"/>,
    /// the wait still performs exactly one evaluation once the initial delay elapses, so the
    /// wall-clock time spent before throwing or returning can exceed the configured timeout.
    /// This is intentional: "at most one evaluation is always attempted" outweighs the literal
    /// timeout bound. Choose a delay no greater than the timeout if the timeout must be a hard
    /// wall-clock ceiling.
    /// </remarks>
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
            await DelayAsync(_initialDelay, _timeProvider);
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
                await DelayAsync(delay, _timeProvider);
            }
        }
    }

    private static TimerAwaitable DelayAsync(TimeSpan delay, TimeProvider timeProvider) => new(delay, timeProvider);

    /// <summary>
    /// A custom awaitable timer delay that invokes its continuation as a direct, synchronous
    /// delegate call from the firing <see cref="ITimer"/> callback. Unlike awaiting
    /// <see cref="Task.Delay(TimeSpan)"/>-style constructs, this never routes the resumption
    /// through the thread pool or any other Task-scheduling heuristic, so a synchronous
    /// <see cref="TimeProvider"/> (such as a test fake that fires due timers inline) resumes
    /// the poll loop deterministically on the very thread that advanced the clock. With
    /// <see cref="TimeProvider.System"/>, the callback (and therefore the predicate and any
    /// synchronous continuation of the awaited <c>Until</c> task) runs inline on a thread-pool
    /// timer callback thread rather than being posted back to a captured synchronization
    /// context; a long-blocking predicate blocks that callback thread for its duration.
    /// </summary>
    private readonly struct TimerAwaitable
    {
        private readonly TimeSpan _delay;
        private readonly TimeProvider _timeProvider;

        internal TimerAwaitable(TimeSpan delay, TimeProvider timeProvider)
        {
            _delay = delay;
            _timeProvider = timeProvider;
        }

        public TimerAwaiter GetAwaiter() => new(_delay, _timeProvider);
    }

    private sealed class TimerAwaiter : ICriticalNotifyCompletion
    {
        private readonly TimeProvider _timeProvider;
        private readonly TimeSpan _delay;

        internal TimerAwaiter(TimeSpan delay, TimeProvider timeProvider)
        {
            _delay = delay;
            _timeProvider = timeProvider;
        }

        public bool IsCompleted => false;

        /// <summary>
        /// Schedules <paramref name="continuation"/> to run when the delay elapses, flowing the
        /// captured <see cref="ExecutionContext"/> as required by the <see cref="INotifyCompletion"/>
        /// contract. The compiler-generated state machine always calls <see cref="UnsafeOnCompleted"/>
        /// instead (this awaiter implements <see cref="ICriticalNotifyCompletion"/>), so this path is
        /// only exercised by a caller awaiting the awaiter directly.
        /// </summary>
        public void OnCompleted(Action continuation)
        {
            ArgumentNullException.ThrowIfNull(continuation);
            var capturedContext = ExecutionContext.Capture();
            Schedule(capturedContext is null
                ? continuation
                : () => ExecutionContext.Run(capturedContext, static state => ((Action)state!)(), continuation));
        }

        public void UnsafeOnCompleted(Action continuation) => Schedule(continuation);

        public void GetResult()
        {
        }

        private void Schedule(Action continuation)
        {
            var state = new TimerState(continuation);
            var timer = _timeProvider.CreateTimer(
                static timerState => ((TimerState)timerState!).Fire(),
                state,
                dueTime: _delay,
                period: Timeout.InfiniteTimeSpan);
            state.AttachTimer(timer);
        }

        /// <summary>
        /// Carries the continuation and the eventual <see cref="ITimer"/> for a single scheduled
        /// delay, and guarantees the timer is disposed exactly once regardless of whether the
        /// timer callback fires before or after <see cref="AttachTimer"/> observes it. The timer
        /// callback can legitimately run on another thread before <see cref="TimeProvider.CreateTimer"/>
        /// has returned to the caller, so the handoff between "the timer object exists" and "the
        /// callback fired" must not assume either ordering.
        /// </summary>
        private sealed class TimerState
        {
            private readonly Action _continuation;
            private ITimer? _timer;

            internal TimerState(Action continuation)
            {
                _continuation = continuation;
            }

            internal void AttachTimer(ITimer timer)
            {
                // If Fire() already ran, _timer holds the FiredSentinel and this attach is
                // responsible for disposing the timer itself; otherwise Fire() (still to come)
                // will observe the timer we store here and dispose it when it runs.
                if (Interlocked.CompareExchange(ref _timer, timer, null) is not null)
                {
                    timer.Dispose();
                }
            }

            internal void Fire()
            {
                var timer = Interlocked.Exchange(ref _timer, FiredSentinel.Instance);
                timer?.Dispose();
                _continuation();
            }
        }

        /// <summary>
        /// A no-op <see cref="ITimer"/> used purely as a non-null marker so that
        /// <see cref="TimerState"/> can distinguish "no timer attached yet" (<see langword="null"/>)
        /// from "the callback already fired" without a separate synchronization primitive.
        /// </summary>
        private sealed class FiredSentinel : ITimer
        {
            internal static readonly FiredSentinel Instance = new();

            private FiredSentinel()
            {
            }

            public bool Change(TimeSpan dueTime, TimeSpan period) => false;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
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
