using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Wanetra.Domain;
using Wanetra.Infrastructure.Processes;

namespace Wanetra.Infrastructure.SpeedTests;

internal sealed class LibreSpeedEngine(
    IProcessRunner processRunner,
    IOptions<LibreSpeedOptions> options) : ProcessSpeedTestEngine(processRunner)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public override string Name => "librespeed";

    protected override string ToolName => "librespeed-cli";

    protected override string ExecutablePath => options.Value.ExecutablePath;

    protected override TimeSpan ProcessTimeout => TimeSpan.FromSeconds(options.Value.TimeoutSeconds);

    protected override IReadOnlyList<string> BuildArguments()
    {
        var arguments = new List<string> { "--json" };
        if (options.Value.ServerId is { } serverId)
        {
            arguments.Add("--server");
            arguments.Add(serverId.ToString(CultureInfo.InvariantCulture));
        }

        return arguments;
    }

    protected override SpeedTestResult Map(string standardOutput)
    {
        var output = Parse(standardOutput);
        return new SpeedTestResult
        {
            Engine = Name,
            DownloadMbps = output.Download,
            UploadMbps = output.Upload,
            LatencyMs = output.Ping,
            JitterMs = output.Jitter,

            PacketLossPercent = null,

            ServerName = Normalize(output.Server?.Name),
            ServerId = options.Value.ServerId?.ToString(CultureInfo.InvariantCulture),
            Isp = Normalize(output.Client?.Org),
            ExternalIp = Normalize(output.Client?.Ip),
        };
    }

    private static LibreSpeedOutput Parse(string standardOutput)
    {
        var entries = JsonSerializer.Deserialize<LibreSpeedOutput[]>(standardOutput, JsonOptions);

        return entries is [var entry, ..]
            ? entry
            : throw new InvalidOperationException("librespeed-cli returned no measurement.");
    }
}
