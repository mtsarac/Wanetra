namespace Wanetra.Application.Auth;

public sealed class LoginThrottle(TimeProvider timeProvider)
{
    public const int PerKeyLimit = 5;
    public const int GlobalLimit = 30;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private readonly Lock gate = new();
    private readonly Dictionary<string, List<DateTimeOffset>> failuresByKey = new(StringComparer.Ordinal);
    private readonly List<DateTimeOffset> globalFailures = [];

    public bool IsBlocked(string key, out TimeSpan retryAfter)
    {
        lock (gate)
        {
            var now = timeProvider.GetUtcNow();
            Purge(now);

            retryAfter = TimeSpan.Zero;
            if (failuresByKey.TryGetValue(key, out var keyed) && keyed.Count >= PerKeyLimit)
            {
                retryAfter = keyed[0] + Window - now;
            }

            if (globalFailures.Count >= GlobalLimit)
            {
                var globalRetry = globalFailures[0] + Window - now;
                if (globalRetry > retryAfter)
                {
                    retryAfter = globalRetry;
                }
            }

            if (retryAfter <= TimeSpan.Zero)
            {
                retryAfter = TimeSpan.Zero;
                return false;
            }

            return true;
        }
    }

    public void RecordFailure(string key)
    {
        lock (gate)
        {
            var now = timeProvider.GetUtcNow();
            Purge(now);
            if (!failuresByKey.TryGetValue(key, out var keyed))
            {
                keyed = [];
                failuresByKey[key] = keyed;
            }

            keyed.Add(now);
            globalFailures.Add(now);
        }
    }

    public void Reset(string key)
    {
        lock (gate)
        {
            failuresByKey.Remove(key);
        }
    }

    private void Purge(DateTimeOffset now)
    {
        var cutoff = now - Window;
        globalFailures.RemoveAll(time => time <= cutoff);
        foreach (var key in failuresByKey.Keys.ToList())
        {
            var keyed = failuresByKey[key];
            keyed.RemoveAll(time => time <= cutoff);
            if (keyed.Count == 0)
            {
                failuresByKey.Remove(key);
            }
        }
    }
}
