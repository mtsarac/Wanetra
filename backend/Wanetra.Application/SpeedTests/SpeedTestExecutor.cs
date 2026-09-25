using Microsoft.Extensions.Logging;
using Wanetra.Application.Alerts;
using Wanetra.Application.Notifications;
using Wanetra.Domain;

namespace Wanetra.Application.SpeedTests;

/// <summary>
/// Runs a single speed test and stores the outcome, including failures.
/// </summary>
public sealed class SpeedTestExecutor(
    ISpeedTestEngine engine,
    ISpeedTestResultRepository repository,
    AlertEvaluationService alertEvaluationService,
    NotificationDispatcher notificationDispatcher,
    IPrometheusMetrics metrics,
    TimeProvider timeProvider,
    ILogger<SpeedTestExecutor> logger)
{
    private const int MaxErrorMessageLength = 2048;

    public async Task<SpeedTestResult> RunAsync(CancellationToken cancellationToken)
    {
        var startedAt = timeProvider.GetUtcNow();
        var startedTimestamp = timeProvider.GetTimestamp();

        logger.LogInformation("Speed test started using engine {Engine}", engine.Name);

        SpeedTestResult result;
        try
        {
            result = await engine.RunAsync(cancellationToken);
            result.Success = true;
            result.ErrorMessage = null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Speed test cancelled using engine {Engine}", engine.Name);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Speed test failed using engine {Engine}", engine.Name);

            result = new SpeedTestResult
            {
                Engine = engine.Name,
                Success = false,
                ErrorMessage = Truncate(ex.Message),
            };
        }

        result.Engine = engine.Name;
        result.Timestamp = startedAt.UtcDateTime;
        result.DurationMs = (long)timeProvider.GetElapsedTime(startedTimestamp).TotalMilliseconds;

        await repository.AddAsync(result, cancellationToken);
        metrics.RecordSpeedTest(result);

        var (trigger, eventId) = await alertEvaluationService.EvaluateAsync(result, cancellationToken);
        if (trigger.HasValue && eventId.HasValue)
        {
            await notificationDispatcher.DispatchAsync(trigger.Value, eventId.Value, cancellationToken);
        }

        if (result.Success)
        {
            logger.LogInformation(
                "Speed test completed in {DurationMs} ms: {DownloadMbps} Mbps down, {UploadMbps} Mbps up, {LatencyMs} ms latency",
                result.DurationMs,
                result.DownloadMbps,
                result.UploadMbps,
                result.LatencyMs);
        }

        return result;
    }

    private static string Truncate(string message) =>
        message.Length <= MaxErrorMessageLength ? message : message[..MaxErrorMessageLength];
}
