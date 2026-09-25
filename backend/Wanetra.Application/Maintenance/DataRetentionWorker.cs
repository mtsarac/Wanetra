using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wanetra.Domain;

namespace Wanetra.Application.Maintenance;

public sealed class DataRetentionWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<DataRetentionOptions> options,
    TimeProvider timeProvider,
    ILogger<DataRetentionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CleanupInterval);
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

    private async Task DeleteExpiredResultsAsync(CancellationToken cancellationToken)
    {
        var cutoff = timeProvider.GetUtcNow().UtcDateTime.AddDays(-options.Value.Days);
        await using var scope = scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<ISpeedTestResultRepository>();
        var deletedCount = await repository.DeleteOlderThanAsync(cutoff, cancellationToken);
        if (deletedCount > 0)
        {
            logger.LogInformation("Removed {DeletedCount} speed test results older than {Cutoff}.", deletedCount, cutoff);
        }
    }
}
