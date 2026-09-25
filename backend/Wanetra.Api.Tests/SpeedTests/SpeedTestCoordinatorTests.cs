using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wanetra.Application;
using Wanetra.Application.Notifications;
using Wanetra.Application.SpeedTests;
using Wanetra.Domain;
namespace Wanetra.Api.Tests.SpeedTests;

public class SpeedTestCoordinatorTests
{
    [Fact]
    public async Task Only_one_speed_test_runs_at_a_time()
    {
        var engineGate = new TaskCompletionSource();
        var (coordinator, repository) = Build(async _ =>
        {
            await engineGate.Task;
            return new SpeedTestResult { Engine = "stub", DownloadMbps = 500 };
        });

        var running = coordinator.TryStart(SpeedTestTrigger.Manual);

        Assert.NotNull(running);
        Assert.Equal(SpeedTestState.Running, coordinator.Status.State);
        Assert.Equal(SpeedTestTrigger.Manual, coordinator.Status.Trigger);

        Assert.Null(coordinator.TryStart(SpeedTestTrigger.Scheduled));

        engineGate.SetResult();
        await running;

        Assert.Equal(SpeedTestState.Idle, coordinator.Status.State);
        Assert.Single(repository.Results);
    }

    [Fact]
    public async Task A_new_test_can_start_once_the_previous_one_finished()
    {
        var (coordinator, repository) = Build(_ => Task.FromResult(new SpeedTestResult { Engine = "stub" }));

        await coordinator.TryStart(SpeedTestTrigger.Manual)!;
        await coordinator.TryStart(SpeedTestTrigger.Scheduled)!;

        Assert.Equal(2, repository.Results.Count);
    }

    [Fact]
    public async Task Successful_test_is_stored_with_engine_timestamp_and_duration()
    {
        var (coordinator, repository) = Build(_ => Task.FromResult(new SpeedTestResult
        {
            Engine = "ignored",
            DownloadMbps = 812.3,
            UploadMbps = 42.5,
            LatencyMs = 11.2,
        }));

        await coordinator.TryStart(SpeedTestTrigger.Manual)!;

        var stored = Assert.Single(repository.Results);
        Assert.True(stored.Success);
        Assert.Null(stored.ErrorMessage);
        Assert.Equal("stub", stored.Engine);
        Assert.Equal(812.3, stored.DownloadMbps);
        Assert.Equal(DateTimeKind.Utc, stored.Timestamp.Kind);
        Assert.NotNull(stored.DurationMs);
    }

    [Fact]
    public async Task Failed_test_is_stored_and_surfaced_in_the_status()
    {
        var (coordinator, repository) = Build(_ =>
            throw new InvalidOperationException("librespeed-cli exited with code 1: connection refused"));

        await coordinator.TryStart(SpeedTestTrigger.Manual)!;
        var stored = Assert.Single(repository.Results);

        Assert.False(stored.Success);
        Assert.Equal(SpeedTestFailureKind.LocalExecutionFailure, stored.FailureKind);
        Assert.Equal("Speed test could not be executed.", stored.ErrorMessage);
        Assert.Equal("stub", stored.Engine);

        Assert.Equal(SpeedTestState.Failed, coordinator.Status.State);
        Assert.Equal(stored.ErrorMessage, coordinator.Status.ErrorMessage);
        Assert.Equal(SpeedTestTrigger.Manual, coordinator.Status.Trigger);
        Assert.NotNull(coordinator.Status.StartedAt);
    }

    [Fact]
    public async Task Cancelled_test_is_not_stored_and_releases_the_lock()
    {
        using var cancellation = new CancellationTokenSource();
        var (coordinator, repository) = Build(async token =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return new SpeedTestResult { Engine = "stub" };
        });

        var running = coordinator.TryStart(SpeedTestTrigger.Scheduled, cancellation.Token);
        await cancellation.CancelAsync();
        await running!;

        Assert.Empty(repository.Results);
        Assert.Equal(SpeedTestState.Idle, coordinator.Status.State);
        Assert.NotNull(coordinator.TryStart(SpeedTestTrigger.Manual));
    }

    private static (SpeedTestCoordinator Coordinator, RecordingResultRepository Repository) Build(
        Func<CancellationToken, Task<SpeedTestResult>> run)
    {
        var repository = new RecordingResultRepository();

        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<ISpeedTestResultRepository>(repository);
        services.AddSingleton<IAlertStateRepository, RecordingAlertStateRepository>();
        services.AddSingleton<INotificationConfigurationRepository, RecordingNotificationConfigurationRepository>();
        services.AddSingleton(new HttpClient());
        services.AddSingleton<ISpeedTestEngine>(new StubSpeedTestEngine(run));
        services.AddApplication();

        var provider = services.BuildServiceProvider();

        return (provider.GetRequiredService<SpeedTestCoordinator>(), repository);
    }
}
