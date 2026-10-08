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

    [Fact]
    public async Task Maps_a_real_speedtest_1_2_0_result()
    {
        // Captured from speedtest 1.2.0 (addresses anonymized); stdout is pure JSON, the license text goes to stderr.
        const string real = """
            {"type":"result","timestamp":"2026-10-08T12:46:46Z",
             "ping":{"jitter":0.964,"latency":10.841,"low":10.537,"high":12.493},
             "download":{"bandwidth":2525417,"bytes":24780264,"elapsed":9915,"latency":{"iqm":200.015,"low":21.473,"high":717.799,"jitter":58.492}},
             "upload":{"bandwidth":1434104,"bytes":13671800,"elapsed":10133,"latency":{"iqm":604.578,"low":38.692,"high":1667.695,"jitter":87.307}},
             "packetLoss":0,"isp":"Netinternet Bilisim Teknolojileri",
             "interface":{"internalIp":"192.0.2.10","name":"wlan0","macAddr":"00:00:5E:00:53:01","isVpn":false,"externalIp":"203.0.113.10"},
             "server":{"id":39901,"host":"st-denizli-1.turksatkablo.com.tr","port":8080,"name":"Turksat Kablonet","location":"Denizli","country":"Turkey","ip":"198.51.100.7"},
             "result":{"id":"17144c0f-6c4c-45df-92a3-9b0c8e06d6d7","url":"https://www.speedtest.net/result/c/17144c0f-6c4c-45df-92a3-9b0c8e06d6d7","persisted":true}}
            """;
        var engine = CreateEngine(new StubProcessRunner(new ProcessResult(0, real, "license notice on stderr")));

        var result = await engine.RunAsync(CancellationToken.None);

        Assert.Equal(2525417 * 8 / 1_000_000.0, result.DownloadMbps);
        Assert.Equal(1434104 * 8 / 1_000_000.0, result.UploadMbps);
        Assert.Equal(10.841, result.LatencyMs);
        Assert.Equal(0.964, result.JitterMs);
        Assert.Equal(0, result.PacketLossPercent);
        Assert.Equal("Turksat Kablonet", result.ServerName);
        Assert.Equal("Denizli, Turkey", result.ServerLocation);
        Assert.Equal("39901", result.ServerId);
        Assert.Equal("203.0.113.10", result.ExternalIp);
    }

    [Fact]
    public async Task Reports_unreachable_ookla_configuration_as_network_failure()
    {
        // Exact stderr observed from speedtest 1.2.0 with no network.
        var runner = new StubProcessRunner(new ProcessResult(2, string.Empty, """
            [2026-10-08 15:46:51.982] [error] Configuration - Cannot retrieve configuration document (0)
            [2026-10-08 15:46:51.986] [error] ConfigurationError - Could not retrieve or read configuration (Configuration)
            """));
        var engine = CreateEngine(runner);

        var error = await Assert.ThrowsAsync<SpeedTestExecutionException>(
            () => engine.RunAsync(CancellationToken.None));

        Assert.Equal(SpeedTestFailureKind.NetworkFailure, error.FailureKind);
    }

    [Fact]
    public async Task Runs_an_explicit_executable_without_installing_anything()
    {
        var binary = new StubOoklaBinary("/data/ookla/1.2.0/speedtest");
        var runner = new StubProcessRunner(new ProcessResult(0, SampleOutput, string.Empty));
        var engine = CreateEngine(runner, new OoklaOptions { ExecutablePath = "/opt/ookla/speedtest" }, binary);

        await engine.RunAsync(CancellationToken.None);

        Assert.Equal("/opt/ookla/speedtest", runner.FileName);
        Assert.Equal(0, binary.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task Runs_the_installed_binary_when_no_executable_is_configured(string? configured)
    {
        var binary = new StubOoklaBinary("/data/ookla/1.2.0/speedtest");
        var runner = new StubProcessRunner(new ProcessResult(0, SampleOutput, string.Empty));
        var engine = CreateEngine(runner, new OoklaOptions { ExecutablePath = configured }, binary);

        await engine.RunAsync(CancellationToken.None);

        Assert.Equal("/data/ookla/1.2.0/speedtest", runner.FileName);
        Assert.Equal(1, binary.Calls);
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

    private static OoklaSpeedEngine CreateEngine(
        IProcessRunner runner,
        OoklaOptions? options = null,
        IOoklaBinary? binary = null) =>
        new(
            runner,
            Options.Create(options ?? new OoklaOptions { ExecutablePath = "speedtest" }),
            binary ?? new StubOoklaBinary("/unused"));

    private sealed class StubOoklaBinary(string path) : IOoklaBinary
    {
        public int Calls { get; private set; }

        public Task<string> EnsureInstalledAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(path);
        }
    }
}
