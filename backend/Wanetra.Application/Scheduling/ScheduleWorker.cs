using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wanetra.Application.SpeedTests;

namespace Wanetra.Application.Scheduling;

public sealed class ScheduleWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ScheduleChangeSignal changeSignal,
    SpeedTestCoordinator coordinator,
    ILogger<ScheduleWorker> logger) : BackgroundService
{
    private static readonly TimeSpan MaxWait = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var next = await GetNextRunAsync(stoppingToken);
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            if (next is null)
            {
                await WaitForChangeAsync(stoppingToken);
                continue;
            }

            var delay = next.Value - timeProvider.GetUtcNow().UtcDateTime;
            if (delay <= TimeSpan.Zero)
            {
                continue;
            }

            if (!await WaitForDueOrChangeAsync(delay, stoppingToken))
            {
                continue;
            }

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                coordinator.TryStart(SpeedTestTrigger.Scheduled, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduled speed test failed to start");
            }
        }
    }

    private async Task<DateTime?> GetNextRunAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var schedule = scope.ServiceProvider.GetRequiredService<ScheduleService>();
            var settings = await schedule.GetAsync(stoppingToken);
            if (!settings.Enabled)
            {
                return null;
            }

            var calculator = scope.ServiceProvider.GetRequiredService<ScheduleCalculator>();
            var runs = calculator.GetNextRuns(settings, timeProvider.GetUtcNow().UtcDateTime, 1);
            return runs.Count > 0 ? runs[0] : null;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return null;
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Ignoring invalid schedule settings");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Scheduled speed test lookup failed");
            return null;
        }
    }

    private async Task WaitForChangeAsync(CancellationToken stoppingToken)
    {
        try
        {
            await changeSignal.WaitAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task<bool> WaitForDueOrChangeAsync(TimeSpan delay, CancellationToken stoppingToken)
    {
        // ponytail: cap single waits below the platform timer limit; the loop recomputes from now anyway.
        var wait = delay > MaxWait ? MaxWait : delay;
        try
        {
            var signalTask = changeSignal.WaitAsync(stoppingToken);
            var delayTask = Task.Delay(wait, timeProvider, stoppingToken);
            var completed = await Task.WhenAny(signalTask, delayTask);
            if (!ReferenceEquals(completed, delayTask))
            {
                return false;
            }

            await delayTask;
            return delay <= MaxWait;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
