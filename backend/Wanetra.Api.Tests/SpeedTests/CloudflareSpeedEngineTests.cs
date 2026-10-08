using Microsoft.Extensions.Options;
using Wanetra.Application.SpeedTests;
using Wanetra.Domain;
using Wanetra.Infrastructure.Processes;
using Wanetra.Infrastructure.SpeedTests;

namespace Wanetra.Api.Tests.SpeedTests;

public class CloudflareSpeedEngineTests
{
    // Shape of `cfspeedtest -o json` v2.2.2; the 25 MB tier was skipped on this slow line.
    private const string SampleOutput = """
        {
          "metadata": {"country": "TR", "ip": "203.0.113.7", "colo": "IST"},
          "latency_measurement": {
            "avg_latency_ms": 67.67, "min_latency_ms": 41.7, "max_latency_ms": 284.8,
            "latency_measurements": [44.0, 42.0, 46.0, 284.0, 43.0]
          },
          "speed_measurements": [
            {"test_type": "Download", "payload_size": 100000, "median": 5.6, "successes": 3},
            {"test_type": "Download", "payload_size": 10000000, "median": 24.2, "successes": 3},
            {"test_type": "Download", "payload_size": 1000000, "median": 13.5, "successes": 3},
            {"test_type": "Upload", "payload_size": 100000, "median": 9.7, "successes": 3},
            {"test_type": "Upload", "payload_size": 10000000, "median": 13.8, "successes": 3},
            {"test_type": "Upload", "payload_size": 25000000, "median": 0.0, "successes": 0}
          ]
        }
        """;

    [Fact]
    public async Task Maps_cloudflare_output_to_a_result()
    {
        var engine = CreateEngine(new StubProcessRunner(new ProcessResult(0, SampleOutput, string.Empty)));

        var result = await engine.RunAsync(CancellationToken.None);

        Assert.Equal("cloudflare", result.Engine);
        Assert.Equal(24.2, result.DownloadMbps);
        Assert.Equal(13.8, result.UploadMbps);
        Assert.Equal("Cloudflare (IST)", result.ServerName);
        Assert.Equal("TR", result.ServerLocation);
        Assert.Equal("203.0.113.7", result.ExternalIp);
        Assert.Null(result.Isp);
        Assert.Null(result.PacketLossPercent);
    }

    [Fact]
    public async Task Reports_median_latency_and_mean_consecutive_difference_as_jitter()
    {
        var engine = CreateEngine(new StubProcessRunner(new ProcessResult(0, SampleOutput, string.Empty)));

        var result = await engine.RunAsync(CancellationToken.None);

        // Median ignores the 284 ms outlier that would skew an average.
        Assert.Equal(44.0, result.LatencyMs);
        // |42-44| + |46-42| + |284-46| + |43-284| over 4 pairs.
        Assert.Equal((2.0 + 4.0 + 238.0 + 241.0) / 4, result.JitterMs);
    }

    [Fact]
    public async Task Leaves_latency_unavailable_when_no_latency_samples_were_collected()
    {
        var output = """
            {"speed_measurements": [
              {"test_type": "Download", "payload_size": 100000, "median": 5.0, "successes": 1},
              {"test_type": "Upload", "payload_size": 100000, "median": 6.0, "successes": 1}],
             "latency_measurement": {"latency_measurements": []}}
            """;
        var engine = CreateEngine(new StubProcessRunner(new ProcessResult(0, output, string.Empty)));

        var result = await engine.RunAsync(CancellationToken.None);

        Assert.Null(result.LatencyMs);
        Assert.Null(result.JitterMs);
        Assert.Equal("Cloudflare", result.ServerName);
    }

    [Fact]
    public async Task Requests_json_output()
    {
        var runner = new StubProcessRunner(new ProcessResult(0, SampleOutput, string.Empty));
        var engine = CreateEngine(runner, new CloudflareOptions { ExecutablePath = "/usr/local/bin/cfspeedtest" });

        await engine.RunAsync(CancellationToken.None);

        Assert.Equal("/usr/local/bin/cfspeedtest", runner.FileName);
        Assert.Equal(new[] { "--output-format", "json" }, runner.Arguments);
    }

    [Fact]
    public async Task Reports_unreachable_cloudflare_as_network_failure()
    {
        // Exact stderr observed from cfspeedtest 2.2.2 with no network.
        var runner = new StubProcessRunner(new ProcessResult(
            1, string.Empty, "Error fetching metadata: error sending request for url (https://speed.cloudflare.com/cdn-cgi/trace)"));
        var engine = CreateEngine(runner);

        var error = await Assert.ThrowsAsync<SpeedTestExecutionException>(
            () => engine.RunAsync(CancellationToken.None));

        Assert.Equal(SpeedTestFailureKind.NetworkFailure, error.FailureKind);
        Assert.Equal("cfspeedtest could not reach the test service.", error.Message);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("""{"speed_measurements": []}""")]
    [InlineData("""{"speed_measurements": [{"test_type": "Download", "payload_size": 100000, "median": 5.0, "successes": 1}]}""")]
    [InlineData("""{"speed_measurements": [{"test_type": "Download", "payload_size": 100000, "median": 0.0, "successes": 0}, {"test_type": "Upload", "payload_size": 100000, "median": 6.0, "successes": 1}]}""")]
    public async Task Rejects_output_without_both_directions_as_measurement_failure(string standardOutput)
    {
        // cfspeedtest 2.2.2 can exit 0 with an empty measurement list, so exit status alone is not enough.
        var engine = CreateEngine(new StubProcessRunner(new ProcessResult(0, standardOutput, string.Empty)));

        var error = await Assert.ThrowsAsync<SpeedTestExecutionException>(
            () => engine.RunAsync(CancellationToken.None));

        Assert.Equal(SpeedTestFailureKind.MeasurementFailure, error.FailureKind);
    }

    private static CloudflareSpeedEngine CreateEngine(IProcessRunner runner, CloudflareOptions? options = null) =>
        new(runner, Options.Create(options ?? new CloudflareOptions()));
}
