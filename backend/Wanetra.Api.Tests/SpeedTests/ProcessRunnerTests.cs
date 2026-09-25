using Microsoft.Extensions.Logging.Abstractions;
using Wanetra.Infrastructure.Processes;

namespace Wanetra.Api.Tests.SpeedTests;

public class ProcessRunnerTests
{
    private readonly ProcessRunner runner = new(NullLogger<ProcessRunner>.Instance);

    [Fact]
    public async Task Captures_standard_output_and_exit_code()
    {
        var result = await runner.RunAsync(
            "/bin/echo",
            ["speedtest"],
            TimeSpan.FromSeconds(10),
            CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("speedtest", result.StandardOutput.Trim());
        Assert.Empty(result.StandardError);
    }

    [Fact]
    public async Task Reports_a_failing_exit_code_without_throwing()
    {
        var result = await runner.RunAsync(
            "/bin/false",
            [],
            TimeSpan.FromSeconds(10),
            CancellationToken.None);

        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public async Task Terminates_a_process_that_outlives_its_timeout()
    {
        var timedOut = await Assert.ThrowsAsync<TimeoutException>(() => runner.RunAsync(
            "/bin/sleep",
            ["30"],
            TimeSpan.FromMilliseconds(200),
            CancellationToken.None));

        Assert.Contains("did not finish", timedOut.Message);
    }

    [Fact]
    public async Task Cancellation_stops_the_process()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.RunAsync(
            "/bin/sleep",
            ["30"],
            TimeSpan.FromSeconds(30),
            cancellation.Token));
    }

    [Fact]
    public async Task Missing_executable_fails_with_a_process_error()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => runner.RunAsync(
            "/usr/bin/wanetra-does-not-exist",
            [],
            TimeSpan.FromSeconds(10),
            CancellationToken.None));
    }
}
