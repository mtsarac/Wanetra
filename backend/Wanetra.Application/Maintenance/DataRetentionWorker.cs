using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wanetra.Application.Settings;
using Wanetra.Domain;

namespace Wanetra.Application.Maintenance;

public sealed class DataRetentionWorker(
    IServiceScopeFactory scopeFactory,
    SettingsSnapshot settingsSnapshot,
    TimeProvider timeProvider,
    ILogger<DataRetentionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CleanupInterval, timeProvider);
        do
        {
            try
            {
                await DeleteExpiredResultsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Speed test retention cleanup failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task DeleteExpiredResultsAsync(CancellationToken cancellationToken)
    {
        var days = settingsSnapshot.GetInt(SettingKeys.RetentionDays);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        DateTime cutoff;
        try
        {
            cutoff = now.AddDays(-days);
        }
        catch (ArgumentOutOfRangeException)
        {
            logger.LogWarning("Retention days {Days} produces an out of range cutoff date; skipping cleanup.", days);
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<ISpeedTestResultRepository>();
        var deletedCount = await repository.DeleteOlderThanAsync(cutoff, cancellationToken);
        if (deletedCount > 0)
        {
            logger.LogInformation("Removed {DeletedCount} speed test results older than {Cutoff}.", deletedCount, cutoff);
        }
    }
}
