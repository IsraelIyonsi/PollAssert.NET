namespace PollAssert.Tests.TestSupport;

/// <summary>
/// A minimal, fully synchronous fake <see cref="TimeProvider"/> for deterministic tests.
/// Unlike a thread-pool-backed fake clock, <see cref="Advance"/> invokes every due timer
/// callback inline on the calling thread before returning, so a test can assert on poll
/// counts immediately after advancing without any race against background continuations.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly List<ManualTimer> _timers = new();
    private DateTimeOffset _utcNow;

    internal ManualTimeProvider()
        : this(DateTimeOffset.UtcNow)
    {
    }

    internal ManualTimeProvider(DateTimeOffset start)
    {
        _utcNow = start;
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => _utcNow.Ticks;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        _timers.Add(timer);
        timer.Change(dueTime, period);
        return timer;
    }

    /// <summary>
    /// Moves the clock forward by <paramref name="delta"/>, firing every timer callback
    /// due at or before the new time, in due-time order, synchronously on this thread.
    /// </summary>
    internal void Advance(TimeSpan delta)
    {
        var target = _utcNow + delta;

        while (true)
        {
            var next = _timers
                .Where(timer => timer.DueAt is { } dueAt && dueAt <= target)
                .OrderBy(timer => timer.DueAt)
                .FirstOrDefault();

            if (next is null)
            {
                _utcNow = target;
                return;
            }

            _utcNow = next.DueAt!.Value;
            next.Fire();
        }
    }

    private void Unregister(ManualTimer timer) => _timers.Remove(timer);

    private sealed class ManualTimer : ITimer
    {
        private readonly ManualTimeProvider _owner;
        private readonly TimerCallback _callback;
        private readonly object? _state;
        private TimeSpan _period;
        private bool _disposed;

        internal ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state)
        {
            _owner = owner;
            _callback = callback;
            _state = state;
        }

        internal DateTimeOffset? DueAt { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (_disposed)
            {
                return false;
            }

            _period = period;
            DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : _owner.GetUtcNow() + dueTime;
            return true;
        }

        internal void Fire()
        {
            if (_disposed)
            {
                return;
            }

            DueAt = _period == Timeout.InfiniteTimeSpan || _period == TimeSpan.Zero
                ? null
                : _owner.GetUtcNow() + _period;

            _callback(_state);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            DueAt = null;
            _owner.Unregister(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
