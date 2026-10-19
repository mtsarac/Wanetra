using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wanetra.Application.Notifications;
using Wanetra.Application.Settings;

namespace Wanetra.Application.Probes;

public sealed class ProbeWorker(
    IServiceScopeFactory scopeFactory,
    SettingsSnapshot settings,
    TimeProvider timeProvider,
    ILogger<ProbeWorker> logger) : BackgroundService
{
    private static readonly TimeSpan MinimumDelay = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            var started = timeProvider.GetTimestamp();
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Probe cycle failed");
            }

            var interval = TimeSpan.FromSeconds(settings.GetInt(SettingKeys.ProbesIntervalSeconds));
            var delay = interval - timeProvider.GetElapsedTime(started);
            try
            {
                await Task.Delay(delay < MinimumDelay ? MinimumDelay : delay, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        if (!settings.GetBool(SettingKeys.ProbesEnabled))
        {
            var closed = await scope.ServiceProvider
                .GetRequiredService<OutageService>()
                .CloseForDisabledMonitoringAsync(timeProvider.GetUtcNow().UtcDateTime, stoppingToken);
            Dispatch(closed);
            return;
        }

        var result = await scope.ServiceProvider.GetRequiredService<ProbeCycleService>().RunAsync(stoppingToken);
        Dispatch(result.Transition);
    }

    // Delivery can take as long as a provider timeout, and while the connection is down it fails by design.
    // The retry worker picks up whatever this pass does not deliver, so the probe loop never waits for it.
    private void Dispatch(OutageTransition? transition)
    {
        if (transition is null)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider
                    .GetRequiredService<NotificationDispatcher>()
                    .DispatchOutageAsync(transition.Trigger, transition.OutageId, CancellationToken.None);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Outage notification dispatch failed; the retry worker will try again");
            }
        });
    }
}

public sealed class ProbeMaintenanceWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<ProbeMaintenanceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), timeProvider);
        var lastRetention = DateTime.MinValue;
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var rollups = scope.ServiceProvider.GetRequiredService<ProbeRollupService>();
                await rollups.RollUpAsync(stoppingToken);

                var now = timeProvider.GetUtcNow().UtcDateTime;
                if (now - lastRetention >= TimeSpan.FromHours(1))
                {
                    await rollups.ApplyRetentionAsync(stoppingToken);
                    lastRetention = now;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Probe rollup pass failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
