using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Test.Net.TestDoubles;

public sealed class ManualTimeProvider : TimeProvider
{
    private readonly object _gate = new();
    private readonly List<ManualTimer> _timers = new();
    private DateTimeOffset _utcNow;

    public ManualTimeProvider()
        : this(DateTimeOffset.UnixEpoch)
    {
    }

    public ManualTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
            return _utcNow;
    }

    public void Advance(TimeSpan delta)
    {
        ManualTimer[] dueTimers;
        lock (_gate)
        {
            _utcNow += delta;
            dueTimers = _timers
                .Where(timer => timer.IsDue(_utcNow))
                .ToArray();
        }

        foreach (var timer in dueTimers)
            timer.Fire();
    }

    public override ITimer CreateTimer(TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state, dueTime, period);
        lock (_gate)
            _timers.Add(timer);
        return timer;
    }

    private void Remove(ManualTimer timer)
    {
        lock (_gate)
            _timers.Remove(timer);
    }

    private sealed class ManualTimer : ITimer
    {
        private readonly ManualTimeProvider _owner;
        private readonly TimerCallback _callback;
        private readonly object _state;
        private TimeSpan _period;
        private DateTimeOffset _dueAt;
        private bool _disposed;

        public ManualTimer(ManualTimeProvider owner, TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period)
        {
            _owner = owner;
            _callback = callback;
            _state = state;
            _period = period;
            _dueAt = dueTime == Timeout.InfiniteTimeSpan
                ? DateTimeOffset.MaxValue
                : owner.GetUtcNow() + dueTime;
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (_disposed)
                return false;

            _period = period;
            _dueAt = dueTime == Timeout.InfiniteTimeSpan
                ? DateTimeOffset.MaxValue
                : _owner.GetUtcNow() + dueTime;
            return true;
        }

        public bool IsDue(DateTimeOffset now) => !_disposed && now >= _dueAt;

        public void Fire()
        {
            if (_disposed)
                return;

            if (_period == Timeout.InfiniteTimeSpan)
                _dueAt = DateTimeOffset.MaxValue;
            else
                _dueAt += _period;

            _callback(_state);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _owner.Remove(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
