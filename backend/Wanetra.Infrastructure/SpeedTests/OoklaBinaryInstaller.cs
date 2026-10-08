using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Wanetra.Application.SpeedTests;
using Wanetra.Domain;

namespace Wanetra.Infrastructure.SpeedTests;

/// <summary>Provides the Ookla Speedtest CLI binary that the operator opted into.</summary>
internal interface IOoklaBinary
{
    Task<string> EnsureInstalledAsync(CancellationToken cancellationToken);
}

/// <summary>An official Ookla Speedtest CLI archive, pinned by checksum.</summary>
internal sealed record OoklaRelease(string Version, Uri Url, string Sha256)
{
    private const string Version120 = "1.2.0";

    public static OoklaRelease ForCurrentPlatform() =>
        OperatingSystem.IsLinux()
            ? For(RuntimeInformation.OSArchitecture)
            : throw Unsupported(RuntimeInformation.OSDescription);

    public static OoklaRelease For(Architecture architecture) => architecture switch
    {
        Architecture.X64 => Release("x86_64", "5690596c54ff9bed63fa3732f818a05dbc2db19ad36ed68f21ca5f64d5cfeeb7"),
        Architecture.Arm64 => Release("aarch64", "3953d231da3783e2bf8904b6dd72767c5c6e533e163d3742fd0437affa431bd3"),
        _ => throw Unsupported(architecture.ToString()),
    };

    private static OoklaRelease Release(string platform, string sha256) => new(
        Version120,
        new Uri($"https://install.speedtest.net/app/cli/ookla-speedtest-{Version120}-linux-{platform}.tgz"),
        sha256);

    private static SpeedTestExecutionException Unsupported(string platform) => new(
        SpeedTestFailureKind.LocalExecutionFailure,
        $"The Ookla Speedtest CLI cannot be installed automatically on {platform}; set SpeedTest:Ookla:ExecutablePath.");
}

/// <summary>
/// Downloads the official archive straight from Ookla into the data directory, so Wanetra never
/// redistributes the proprietary binary: each operator fetches it from Ookla after accepting the EULA.
/// </summary>
internal sealed class OoklaBinaryInstaller(
    HttpClient httpClient,
    string installDirectory,
    Func<OoklaRelease> releaseProvider,
    ILogger<OoklaBinaryInstaller> logger) : IOoklaBinary
{
    private const string BinaryName = "speedtest";

    public async Task<string> EnsureInstalledAsync(CancellationToken cancellationToken)
    {
        var release = releaseProvider();
        var directory = Path.Combine(installDirectory, release.Version);
        var target = Path.Combine(directory, BinaryName);
        if (File.Exists(target))
        {
            return target;
        }

        logger.LogInformation("Downloading Ookla Speedtest CLI {Version} from {Url}", release.Version, release.Url);
        var archive = await DownloadAsync(release, cancellationToken);

        var actual = Convert.ToHexString(SHA256.HashData(archive));
        if (!string.Equals(actual, release.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new SpeedTestExecutionException(
                SpeedTestFailureKind.LocalExecutionFailure,
                "The downloaded Ookla Speedtest CLI did not match its pinned checksum and was discarded.");
        }

        try
        {
            Directory.CreateDirectory(directory);
            var temporary = target + ".tmp";
            await ExtractBinaryAsync(archive, temporary, cancellationToken);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(
                    temporary,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                    | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
            File.Move(temporary, target, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            throw new SpeedTestExecutionException(
                SpeedTestFailureKind.LocalExecutionFailure,
                $"Could not install the Ookla Speedtest CLI into {directory}.",
                exception);
        }

        logger.LogInformation("Installed Ookla Speedtest CLI {Version} at {Path}", release.Version, target);
        return target;
    }

    private async Task<byte[]> DownloadAsync(OoklaRelease release, CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.GetByteArrayAsync(release.Url, cancellationToken);
        }
        catch (Exception exception) when (
            exception is HttpRequestException
            || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new SpeedTestExecutionException(
                SpeedTestFailureKind.NetworkFailure,
                "Could not download the Ookla Speedtest CLI.",
                exception);
        }
    }

    private static async Task ExtractBinaryAsync(byte[] archive, string destination, CancellationToken cancellationToken)
    {
        await using var gzip = new GZipStream(new MemoryStream(archive), CompressionMode.Decompress);
        using var tar = new TarReader(gzip);
        while (await tar.GetNextEntryAsync(copyData: false, cancellationToken) is { } entry)
        {
            if (entry.EntryType is TarEntryType.RegularFile or TarEntryType.V7RegularFile
                && string.Equals(Path.GetFileName(entry.Name), BinaryName, StringComparison.Ordinal))
            {
                await entry.ExtractToFileAsync(destination, overwrite: true, cancellationToken);
                return;
            }
        }

        throw new InvalidDataException($"The Ookla archive does not contain {BinaryName}.");
    }
}
