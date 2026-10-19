using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wanetra.Application.Settings;

namespace Wanetra.Application.Lan;

public sealed class LanScanWorker(
    LanScanCoordinator coordinator,
    SettingsSnapshot settings,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<LanScanWorker> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        var next = timeProvider.GetUtcNow() + StartupDelay;
        var lastCleanup = DateTimeOffset.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = timeProvider.GetUtcNow();
                if (!settings.GetBool(SettingKeys.LanEnabled))
                {
                    coordinator.NextScanAt = null;
                    next = now + StartupDelay;
                }
                else
                {
                    coordinator.NextScanAt = next;
                    if (now >= next)
                    {
                        if (coordinator.TryStart("scheduled", stoppingToken) is { } scan)
                        {
                            await scan.WaitAsync(stoppingToken);
                        }

                        next = timeProvider.GetUtcNow() + TimeSpan.FromMinutes(settings.GetInt(SettingKeys.LanScanIntervalMinutes));
                        coordinator.NextScanAt = next;
                    }
                }

                if (now - lastCleanup >= CleanupInterval)
                {
                    lastCleanup = now;
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var cutoff = now.UtcDateTime.AddDays(-settings.GetInt(SettingKeys.RetentionDays));
                    await scope.ServiceProvider.GetRequiredService<DeviceService>().DeleteHistoryOlderThanAsync(cutoff, stoppingToken);
                }

                await Task.Delay(PollInterval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "LAN scan loop failed");
                try
                {
                    await Task.Delay(PollInterval, timeProvider, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
