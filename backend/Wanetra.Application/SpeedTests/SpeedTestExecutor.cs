using Microsoft.Extensions.Logging;
using Wanetra.Application.Alerts;
using Wanetra.Application.Notifications;
using Wanetra.Domain;

namespace Wanetra.Application.SpeedTests;

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
            result.FailureKind = null;
            result.ErrorMessage = null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Speed test cancelled using engine {Engine}", engine.Name);
            throw;
        }
        catch (SpeedTestExecutionException exception)
        {
            logger.LogError(exception, "Speed test failed using engine {Engine} ({FailureKind})", engine.Name, exception.FailureKind);
            result = FailedResult(exception.FailureKind, exception.Message);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Speed test failed using engine {Engine}", engine.Name);
            result = FailedResult(SpeedTestFailureKind.LocalExecutionFailure, "Speed test could not be executed.");
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

    private SpeedTestResult FailedResult(SpeedTestFailureKind failureKind, string message) => new()
    {
        Engine = engine.Name,
        Success = false,
        FailureKind = failureKind,
        ErrorMessage = message.Length <= MaxErrorMessageLength ? message : message[..MaxErrorMessageLength],
    };
}
