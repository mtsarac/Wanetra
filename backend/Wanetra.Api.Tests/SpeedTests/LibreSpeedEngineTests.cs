using System.ComponentModel;
using Microsoft.Extensions.Options;
using Wanetra.Application.SpeedTests;
using Wanetra.Domain;
using Wanetra.Infrastructure.Processes;
using Wanetra.Infrastructure.SpeedTests;
namespace Wanetra.Api.Tests.SpeedTests;

public class LibreSpeedEngineTests
{
    private const string SampleOutput = """
        [{"timestamp":"2026-09-18T10:00:00Z",
          "server":{"name":"Frankfurt, Germany (Clouvider)","url":"https://example.invalid/speedtest"},
          "client":{"ip":"203.0.113.10","hostname":"","region":"","country":"","org":"AS3320 Example Telekom"},
          "bytes_sent":52428800,"bytes_received":1048576000,
          "ping":11.2,"jitter":1.4,"upload":42.5,"download":812.3,"share":""}]
        """;

    [Fact]
    public async Task Maps_librespeed_output_to_a_result()
    {
        var runner = new StubProcessRunner(new ProcessResult(0, SampleOutput, string.Empty));
        var engine = CreateEngine(runner, new LibreSpeedOptions());

        var result = await engine.RunAsync(CancellationToken.None);

        Assert.Equal("librespeed", result.Engine);
        Assert.Equal(812.3, result.DownloadMbps);
        Assert.Equal(42.5, result.UploadMbps);
        Assert.Equal(11.2, result.LatencyMs);
        Assert.Equal(1.4, result.JitterMs);
        Assert.Equal("Frankfurt, Germany (Clouvider)", result.ServerName);
        Assert.Equal("203.0.113.10", result.ExternalIp);
        Assert.Equal("AS3320 Example Telekom", result.Isp);

        // LibreSpeed reports no packet loss and no server location.
        Assert.Null(result.PacketLossPercent);
        Assert.Null(result.ServerLocation);
    }

    [Fact]
    public async Task Requests_json_output_and_the_configured_server()
    {
        var runner = new StubProcessRunner(new ProcessResult(0, SampleOutput, string.Empty));
        var engine = CreateEngine(runner, new LibreSpeedOptions { ExecutablePath = "/usr/bin/librespeed-cli", ServerId = 42 });

        var result = await engine.RunAsync(CancellationToken.None);

        Assert.Equal("/usr/bin/librespeed-cli", runner.FileName);
        Assert.Equal(new[] { "--json", "--server", "42" }, runner.Arguments);
        Assert.Equal("42", result.ServerId);
    }

    [Fact]
    public async Task Reports_process_exit_failure_as_network_failure_when_cli_reports_dns_error()
    {
        var runner = new StubProcessRunner(new ProcessResult(1, string.Empty, "lookup speed.example: no such host"));
        var engine = CreateEngine(runner, new LibreSpeedOptions());

        var error = await Assert.ThrowsAsync<SpeedTestExecutionException>(
            () => engine.RunAsync(CancellationToken.None));

        Assert.Equal(SpeedTestFailureKind.NetworkFailure, error.FailureKind);
        Assert.Equal("librespeed-cli could not reach the test service.", error.Message);
    }

    [Fact]
    public async Task Classifies_process_timeout_as_local_execution_failure()
    {
        var engine = CreateEngine(new ThrowingProcessRunner(new TimeoutException()), new LibreSpeedOptions());

        var error = await Assert.ThrowsAsync<SpeedTestExecutionException>(
            () => engine.RunAsync(CancellationToken.None));

        Assert.Equal(SpeedTestFailureKind.LocalExecutionFailure, error.FailureKind);
    }

    [Fact]
    public async Task Classifies_cli_reported_network_timeout_as_network_failure()
    {
        var runner = new StubProcessRunner(new ProcessResult(1, string.Empty, "connection timed out"));
        var engine = CreateEngine(runner, new LibreSpeedOptions());

        var error = await Assert.ThrowsAsync<SpeedTestExecutionException>(
            () => engine.RunAsync(CancellationToken.None));

        Assert.Equal(SpeedTestFailureKind.NetworkFailure, error.FailureKind);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("not json")]
    public async Task Rejects_unusable_output_as_measurement_failure(string standardOutput)
    {
        var runner = new StubProcessRunner(new ProcessResult(0, standardOutput, string.Empty));
        var engine = CreateEngine(runner, new LibreSpeedOptions());

        var error = await Assert.ThrowsAsync<SpeedTestExecutionException>(
            () => engine.RunAsync(CancellationToken.None));

        Assert.Equal(SpeedTestFailureKind.MeasurementFailure, error.FailureKind);
    }

    [Theory]
    [InlineData(typeof(Win32Exception))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public async Task Classifies_process_start_errors_as_local_execution_failure(Type exceptionType)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;
        var engine = CreateEngine(new ThrowingProcessRunner(exception), new LibreSpeedOptions());

        var error = await Assert.ThrowsAsync<SpeedTestExecutionException>(
            () => engine.RunAsync(CancellationToken.None));

        Assert.Equal(SpeedTestFailureKind.LocalExecutionFailure, error.FailureKind);
    }

    private static LibreSpeedEngine CreateEngine(IProcessRunner runner, LibreSpeedOptions options) =>
        new(runner, Options.Create(options));

    private sealed class ThrowingProcessRunner(Exception exception) : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken) =>
            Task.FromException<ProcessResult>(exception);
    }
}

