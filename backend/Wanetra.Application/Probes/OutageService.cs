using Wanetra.Application.Settings;
using Wanetra.Domain;

namespace Wanetra.Application.Probes;

// Counts consecutive bad and good probe cycles across scopes. One instance for the whole process.
public sealed class OutageTracker
{
    private readonly Lock gate = new();

    public int Bad { get; private set; }
    public int Good { get; private set; }
    public DateTime? FirstBadAt { get; private set; }
    public DateTime? FirstGoodAt { get; private set; }

    public (int Bad, DateTime FirstBadAt) RecordBad(DateTime now)
    {
        lock (gate)
        {
            Good = 0;
            FirstGoodAt = null;
            FirstBadAt ??= now;
            Bad++;
            return (Bad, FirstBadAt.Value);
        }
    }

    public (int Good, DateTime FirstGoodAt) RecordGood(DateTime now)
    {
        lock (gate)
        {
            Bad = 0;
            FirstBadAt = null;
            FirstGoodAt ??= now;
            Good++;
            return (Good, FirstGoodAt.Value);
        }
    }

    public void Reset()
    {
        lock (gate)
        {
            Bad = 0;
            Good = 0;
            FirstBadAt = null;
            FirstGoodAt = null;
        }
    }
}

public sealed class OutageService(
    IOutageRepository outages,
    OutageTracker tracker,
    ConnectivityState state,
    SettingsSnapshot settings,
    IProbeMetrics metrics)
{
    public async Task<OutageTransition?> EvaluateAsync(Diagnosis diagnosis, DateTime now, CancellationToken cancellationToken)
    {
        var open = await outages.GetOpenOutageAsync(cancellationToken);

        if (diagnosis.Scope is { } scope)
        {
            var (bad, firstBadAt) = tracker.RecordBad(now);
            if (open is not null)
            {
                open.LastObservedAt = now;
                if (open.Scope != scope)
                {
                    open.Scope = scope;
                    open.Reason = diagnosis.Reason;
                }

                await outages.SaveChangesAsync(cancellationToken);
                return null;
            }

            if (bad < settings.GetInt(SettingKeys.ProbesOutageFailuresRequired))
            {
                return null;
            }

            var outage = new Outage
            {
                StartedAt = firstBadAt,
                Status = OutageStatus.Active,
                Scope = scope,
                Reason = diagnosis.Reason,
                LastObservedAt = now,
            };
            outages.AddOutage(outage);
            await outages.SaveChangesAsync(cancellationToken);
            Publish(true, opened: true);
            return new OutageTransition(NotificationTrigger.Opened, outage.Id);
        }

        var (good, firstGoodAt) = tracker.RecordGood(now);
        if (open is null)
        {
            tracker.Reset();
            return null;
        }

        open.LastObservedAt = now;
        if (good < settings.GetInt(SettingKeys.ProbesOutageRecoveriesRequired))
        {
            await outages.SaveChangesAsync(cancellationToken);
            return null;
        }

        Close(open, firstGoodAt, closureReason: null);
        await outages.SaveChangesAsync(cancellationToken);
        tracker.Reset();
        Publish(false, opened: false);
        return new OutageTransition(NotificationTrigger.Recovered, open.Id);
    }

    // Wanetra itself was not running, so nothing is known about the time in between.
    public async Task<OutageTransition?> CloseInterruptedAsync(CancellationToken cancellationToken)
    {
        var open = await outages.GetOpenOutageAsync(cancellationToken);
        if (open is null)
        {
            Publish(false, opened: false);
            return null;
        }

        Close(open, open.LastObservedAt, OutageClosureReasons.Interrupted);
        await outages.SaveChangesAsync(cancellationToken);
        tracker.Reset();
        Publish(false, opened: false);
        return new OutageTransition(NotificationTrigger.Recovered, open.Id);
    }

    public async Task<OutageTransition?> CloseForDisabledMonitoringAsync(DateTime now, CancellationToken cancellationToken)
    {
        var open = await outages.GetOpenOutageAsync(cancellationToken);
        if (open is null)
        {
            return null;
        }

        Close(open, now, OutageClosureReasons.MonitoringDisabled);
        await outages.SaveChangesAsync(cancellationToken);
        tracker.Reset();
        Publish(false, opened: false);
        return new OutageTransition(NotificationTrigger.Recovered, open.Id);
    }

    private static void Close(Outage outage, DateTime endedAt, string? closureReason)
    {
        outage.EndedAt = endedAt < outage.StartedAt ? outage.StartedAt : endedAt;
        outage.Status = OutageStatus.Recovered;
        outage.ClosureReason = closureReason;
    }

    private void Publish(bool open, bool opened)
    {
        state.SetOutageOpen(open);
        metrics.SetOutageActive(open);
        if (opened)
        {
            metrics.RecordOutageOpened();
        }
    }
}
