using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Wanetra.Application.SpeedTests;

/// <summary>
/// Guarantees that only one speed test runs at a time and tracks its state.
/// </summary>
public sealed class SpeedTestCoordinator(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<SpeedTestCoordinator> logger) : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private volatile SpeedTestStatus status = SpeedTestStatus.Idle;

    public SpeedTestStatus Status => status;

    /// <summary>
    /// Starts a speed test in the background. Returns the running task, or
    /// <c>null</c> when a test is already running. The task itself never
    /// faults; failures are stored and reported through <see cref="Status"/>.
    /// </summary>
    public Task? TryStart(SpeedTestTrigger trigger, CancellationToken cancellationToken = default)
    {
        if (!gate.Wait(0))
        {
            logger.LogInformation("{Trigger} speed test skipped because another test is running", trigger);
            return null;
        }

        status = new SpeedTestStatus(SpeedTestState.Running, trigger, timeProvider.GetUtcNow());

        return RunAsync(cancellationToken);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var executor = scope.ServiceProvider.GetRequiredService<SpeedTestExecutor>();

            var result = await executor.RunAsync(cancellationToken);

            status = result.Success
                ? SpeedTestStatus.Idle
                : new SpeedTestStatus(SpeedTestState.Failed, ErrorMessage: result.ErrorMessage);
        }
        catch (OperationCanceledException)
        {
            status = SpeedTestStatus.Idle;
        }
        catch (Exception ex)
        {
            // Storing the result can fail on its own, so the run must not bring the process down.
            logger.LogError(ex, "Speed test run failed");
            status = new SpeedTestStatus(SpeedTestState.Failed, ErrorMessage: ex.Message);
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose() => gate.Dispose();
}
