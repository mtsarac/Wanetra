using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Wanetra.Application.SpeedTests;

public sealed class SpeedTestCoordinator(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<SpeedTestCoordinator> logger) : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private volatile SpeedTestStatus status = SpeedTestStatus.Idle;

    public SpeedTestStatus Status => status;

    public Task? TryStart(SpeedTestTrigger trigger, CancellationToken cancellationToken = default)
    {
        if (!gate.Wait(0))
        {
            logger.LogInformation("{Trigger} speed test skipped because another test is running", trigger);
            return null;
        }

        var running = new SpeedTestStatus(SpeedTestState.Running, trigger, timeProvider.GetUtcNow());
        status = running;

        return RunAsync(running, cancellationToken);
    }

    private async Task RunAsync(SpeedTestStatus running, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var executor = scope.ServiceProvider.GetRequiredService<SpeedTestExecutor>();

            var result = await executor.RunAsync(cancellationToken);

            status = result.Success ? SpeedTestStatus.Idle : Failed(running, result.ErrorMessage);
        }
        catch (OperationCanceledException)
        {
            status = SpeedTestStatus.Idle;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Speed test run failed");
            status = Failed(running, ex.Message);
        }
        finally
        {
            gate.Release();
        }
    }

    private static SpeedTestStatus Failed(SpeedTestStatus running, string? errorMessage) =>
        running with { State = SpeedTestState.Failed, ErrorMessage = errorMessage };

    public void Dispose() => gate.Dispose();
}
