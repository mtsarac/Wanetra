using Microsoft.Extensions.Options;
using Wanetra.Application.SpeedTests;
using Wanetra.Domain;
using Wanetra.Infrastructure.Processes;
using Wanetra.Infrastructure.SpeedTests;

namespace Wanetra.Api.Tests.SpeedTests;

public class OoklaSpeedEngineTests
{
    private const string SampleOutput = """
        {"type":"result","timestamp":"2026-10-08T10:00:00Z",
         "ping":{"jitter":0.7,"latency":10.3,"low":9.8,"high":11.2},
         "download":{"bandwidth":118000000,"bytes":900000000,"elapsed":7600},
         "upload":{"bandwidth":5312500,"bytes":40000000,"elapsed":7500},
         "packetLoss":0.5,
         "isp":"Example Telekom",
         "interface":{"internalIp":"192.168.1.20","name":"eth0","externalIp":"203.0.113.10","isVpn":false},
         "server":{"id":1234,"host":"speed.example.net","port":8080,"name":"Example Net","location":"Frankfurt","country":"Germany"},
         "result":{"id":"abc","url":"https://www.speedtest.net/result/c/abc"}}
        """;

    [Fact]
    public async Task Maps_ookla_output_to_a_result_converting_bytes_per_second_to_mbps()
    {
        var engine = CreateEngine(new StubProcessRunner(new ProcessResult(0, SampleOutput, string.Empty)));

        var result = await engine.RunAsync(CancellationToken.None);

        Assert.Equal("ookla", result.Engine);
        Assert.Equal(944.0, result.DownloadMbps);
        Assert.Equal(42.5, result.UploadMbps);
        Assert.Equal(10.3, result.LatencyMs);
        Assert.Equal(0.7, result.JitterMs);
        Assert.Equal(0.5, result.PacketLossPercent);
        Assert.Equal("Example Net", result.ServerName);
        Assert.Equal("Frankfurt, Germany", result.ServerLocation);
        Assert.Equal("1234", result.ServerId);
        Assert.Equal("Example Telekom", result.Isp);
        Assert.Equal("203.0.113.10", result.ExternalIp);
    }

    [Fact]
    public async Task Leaves_packet_loss_unavailable_when_the_cli_omits_it()
    {
        var output = """
            {"ping":{"jitter":1,"latency":2},"download":{"bandwidth":1250000},"upload":{"bandwidth":625000}}
            """;
        var engine = CreateEngine(new StubProcessRunner(new ProcessResult(0, output, string.Empty)));

        var result = await engine.RunAsync(CancellationToken.None);

        Assert.Null(result.PacketLossPercent);
        Assert.Null(result.ServerLocation);
        Assert.Equal(10.0, result.DownloadMbps);
    }

    [Fact]
    public async Task Runs_non_interactively_with_the_configured_server()
    {
        var runner = new StubProcessRunner(new ProcessResult(0, SampleOutput, string.Empty));
        var engine = CreateEngine(runner, new OoklaOptions { ExecutablePath = "/opt/ookla/speedtest", ServerId = 42 });

        await engine.RunAsync(CancellationToken.None);

        Assert.Equal("/opt/ookla/speedtest", runner.FileName);
        Assert.Equal(
            new[] { "--format=json", "--progress=no", "--accept-license", "--accept-gdpr", "--server-id=42" },
            runner.Arguments);
    }

    [Fact]
    public async Task Reports_cli_resolution_failure_as_network_failure()
    {
        var runner = new StubProcessRunner(new ProcessResult(1, string.Empty, "[error] Failed to resolve host name. Cancelling test suite."));
        var engine = CreateEngine(runner);

        var error = await Assert.ThrowsAsync<SpeedTestExecutionException>(
            () => engine.RunAsync(CancellationToken.None));

        Assert.Equal(SpeedTestFailureKind.NetworkFailure, error.FailureKind);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("""{"download":{"bandwidth":1000},"upload":{}}""")]
    [InlineData("""{"upload":{"bandwidth":1000}}""")]
    public async Task Rejects_output_without_both_bandwidths_as_measurement_failure(string standardOutput)
    {
        var engine = CreateEngine(new StubProcessRunner(new ProcessResult(0, standardOutput, string.Empty)));

        var error = await Assert.ThrowsAsync<SpeedTestExecutionException>(
            () => engine.RunAsync(CancellationToken.None));

        Assert.Equal(SpeedTestFailureKind.MeasurementFailure, error.FailureKind);
    }

    private static OoklaSpeedEngine CreateEngine(IProcessRunner runner, OoklaOptions? options = null) =>
        new(runner, Options.Create(options ?? new OoklaOptions()));
}
