using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wanetra.Application.Notifications;
using Wanetra.Domain;

namespace Wanetra.Application.Lan;

public sealed class LanScanCoordinator(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<LanScanCoordinator> logger) : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private volatile LanScanStatus status = LanScanStatus.Idle;
    private long nextScanTicks;

    public LanScanStatus Status => status;

    public DateTimeOffset? NextScanAt
    {
        get
        {
            var ticks = Interlocked.Read(ref nextScanTicks);
            return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
        }
        set => Interlocked.Exchange(ref nextScanTicks, value?.UtcTicks ?? 0);
    }

    // Null when a scan is already running.
    public Task? TryStart(string trigger, CancellationToken cancellationToken = default)
    {
        if (!gate.Wait(0))
        {
            return null;
        }

        status = new LanScanStatus(LanScanState.Running, trigger, timeProvider.GetUtcNow());
        return RunAsync(trigger, cancellationToken);
    }

    private async Task RunAsync(string trigger, CancellationToken cancellationToken)
    {
        LanScanOutcome? outcome = null;
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            outcome = await scope.ServiceProvider.GetRequiredService<LanScanService>().RunAsync(trigger, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "LAN scan run failed");
        }
        finally
        {
            status = LanScanStatus.Idle;
            gate.Release();
        }

        if (outcome is { Transitions.Count: > 0 })
        {
            Dispatch(outcome.Transitions);
        }
    }

    // Provider calls can be slow; the scan is already over, so they run on their own.
    private void Dispatch(IReadOnlyList<DeviceAlertTransition> transitions)
    {
        _ = Task.Run(async () =>
        {
            foreach (var transition in transitions)
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    await scope.ServiceProvider
                        .GetRequiredService<NotificationDispatcher>()
                        .DispatchDeviceAlertAsync(transition.Trigger, transition.AlertId, CancellationToken.None);
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Device notification dispatch failed; the retry worker will try again");
                }
            }
        });
    }

    public void Dispose() => gate.Dispose();
}
