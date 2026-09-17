using Microsoft.Extensions.Options;
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
    public async Task Reports_the_last_error_line_when_the_process_fails()
    {
        var runner = new StubProcessRunner(new ProcessResult(1, string.Empty, "loading server list\nno server available\n"));
        var engine = CreateEngine(runner, new LibreSpeedOptions());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.RunAsync(CancellationToken.None));

        Assert.Contains("exited with code 1", error.Message);
        Assert.Contains("no server available", error.Message);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("not json")]
    public async Task Rejects_unusable_output(string standardOutput)
    {
        var runner = new StubProcessRunner(new ProcessResult(0, standardOutput, string.Empty));
        var engine = CreateEngine(runner, new LibreSpeedOptions());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.RunAsync(CancellationToken.None));
    }

    private static LibreSpeedEngine CreateEngine(StubProcessRunner runner, LibreSpeedOptions options) =>
        new(runner, Options.Create(options));
}
