namespace Wanetra.Api.Tests;

internal sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private readonly Lock gate = new();
    private readonly List<Entry> timers = [];
    private DateTimeOffset now = start;

    public override DateTimeOffset GetUtcNow()
    {
        lock (gate)
        {
            return now;
        }
    }

    public void Advance(TimeSpan delta)
    {
        List<Entry> due;
        lock (gate)
        {
            now += delta;
            due = timers.Where(timer => !timer.Disposed && timer.Due <= now).ToList();
        }

        foreach (var timer in due)
        {
            timer.Fire();
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        DateTimeOffset current;
        lock (gate)
        {
            current = now;
        }

        var entry = new Entry(this, callback, state, current + dueTime, period);
        lock (gate)
        {
            timers.Add(entry);
        }

        return entry;
    }

    private void Remove(Entry entry)
    {
        lock (gate)
        {
            timers.Remove(entry);
        }
    }

    private sealed class Entry(
        ManualTimeProvider owner,
        TimerCallback callback,
        object? state,
        DateTimeOffset due,
        TimeSpan period) : ITimer
    {
        public DateTimeOffset Due { get; private set; } = due;

        public bool Disposed { get; private set; }

        public void Fire()
        {
            lock (this)
            {
                if (Disposed)
                {
                    return;
                }

                if (period == Timeout.InfiniteTimeSpan)
                {
                    Disposed = true;
                }
                else
                {
                    Due += period;
                }
            }

            callback(state);
        }

        public bool Change(TimeSpan dueTime, TimeSpan period) => throw new NotSupportedException();

        public void Dispose()
        {
            Disposed = true;
            owner.Remove(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
