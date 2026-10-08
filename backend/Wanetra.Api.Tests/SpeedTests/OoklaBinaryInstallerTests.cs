using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Wanetra.Application.SpeedTests;
using Wanetra.Domain;
using Wanetra.Infrastructure.SpeedTests;

namespace Wanetra.Api.Tests.SpeedTests;

public sealed class OoklaBinaryInstallerTests : IDisposable
{
    private static readonly byte[] BinaryContent = "#!/bin/sh\necho fake speedtest\n"u8.ToArray();

    private readonly string installDirectory =
        Path.Combine(Path.GetTempPath(), "wanetra-ookla-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(installDirectory))
        {
            Directory.Delete(installDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Installs_the_binary_from_the_archive_as_executable_and_downloads_only_once()
    {
        var archive = Archive(("speedtest.5", "man page"u8.ToArray()), ("speedtest", BinaryContent));
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(archive) });
        var installer = CreateInstaller(handler, archive);

        var path = await installer.EnsureInstalledAsync(CancellationToken.None);
        var again = await installer.EnsureInstalledAsync(CancellationToken.None);

        Assert.Equal(Path.Combine(installDirectory, "1.2.0", "speedtest"), path);
        Assert.Equal(path, again);
        Assert.Equal(BinaryContent, await File.ReadAllBytesAsync(path));
        if (!OperatingSystem.IsWindows())
        {
            Assert.True(File.GetUnixFileMode(path).HasFlag(UnixFileMode.UserExecute));
        }
        Assert.Equal(1, handler.Requests);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public async Task Discards_an_archive_that_does_not_match_the_pinned_checksum()
    {
        var archive = Archive(("speedtest", BinaryContent));
        var tampered = Archive(("speedtest", "#!/bin/sh\necho malicious\n"u8.ToArray()));
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(tampered) });
        var installer = CreateInstaller(handler, archive);

        var error = await Assert.ThrowsAsync<SpeedTestExecutionException>(
            () => installer.EnsureInstalledAsync(CancellationToken.None));

        Assert.Equal(SpeedTestFailureKind.LocalExecutionFailure, error.FailureKind);
        Assert.Contains("checksum", error.Message);
        Assert.False(Directory.Exists(installDirectory));
    }

    [Fact]
    public async Task Classifies_a_failed_download_as_network_failure()
    {
        var archive = Archive(("speedtest", BinaryContent));
        var handler = new StubHandler(_ => throw new HttpRequestException("no route to host"));
        var installer = CreateInstaller(handler, archive);

        var error = await Assert.ThrowsAsync<SpeedTestExecutionException>(
            () => installer.EnsureInstalledAsync(CancellationToken.None));

        Assert.Equal(SpeedTestFailureKind.NetworkFailure, error.FailureKind);
    }

    [Fact]
    public async Task Classifies_an_http_error_status_as_network_failure()
    {
        var archive = Archive(("speedtest", BinaryContent));
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var installer = CreateInstaller(handler, archive);

        var error = await Assert.ThrowsAsync<SpeedTestExecutionException>(
            () => installer.EnsureInstalledAsync(CancellationToken.None));

        Assert.Equal(SpeedTestFailureKind.NetworkFailure, error.FailureKind);
    }

    [Fact]
    public async Task Rejects_a_verified_archive_that_has_no_speedtest_binary_and_leaves_nothing_behind()
    {
        var archive = Archive(("speedtest.md", "docs"u8.ToArray()));
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(archive) });
        var installer = CreateInstaller(handler, archive);

        var error = await Assert.ThrowsAsync<SpeedTestExecutionException>(
            () => installer.EnsureInstalledAsync(CancellationToken.None));

        Assert.Equal(SpeedTestFailureKind.LocalExecutionFailure, error.FailureKind);
        Assert.False(File.Exists(Path.Combine(installDirectory, "1.2.0", "speedtest")));
    }

    [Fact]
    public async Task Propagates_caller_cancellation_instead_of_reporting_a_network_failure()
    {
        var archive = Archive(("speedtest", BinaryContent));
        using var cancellation = new CancellationTokenSource();
        var handler = new StubHandler(_ =>
        {
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        });
        var installer = CreateInstaller(handler, archive);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => installer.EnsureInstalledAsync(cancellation.Token));
    }

    [Theory]
    [InlineData(Architecture.X64, "linux-x86_64.tgz")]
    [InlineData(Architecture.Arm64, "linux-aarch64.tgz")]
    public void Pins_an_official_ookla_archive_per_supported_architecture(Architecture architecture, string suffix)
    {
        var release = OoklaRelease.For(architecture);

        Assert.Equal("install.speedtest.net", release.Url.Host);
        Assert.EndsWith(suffix, release.Url.AbsolutePath);
        Assert.Matches("^[0-9a-f]{64}$", release.Sha256);
    }

    [Fact]
    public void Refuses_unsupported_architectures_with_guidance()
    {
        var error = Assert.Throws<SpeedTestExecutionException>(() => OoklaRelease.For(Architecture.Arm));

        Assert.Equal(SpeedTestFailureKind.LocalExecutionFailure, error.FailureKind);
        Assert.Contains("ExecutablePath", error.Message);
    }

    private OoklaBinaryInstaller CreateInstaller(HttpMessageHandler handler, byte[] expectedArchive) => new(
        new HttpClient(handler),
        installDirectory,
        () => new OoklaRelease(
            "1.2.0",
            new Uri("https://example.invalid/ookla-speedtest.tgz"),
            Convert.ToHexString(SHA256.HashData(expectedArchive))),
        NullLogger<OoklaBinaryInstaller>.Instance);

    private static byte[] Archive(params (string Name, byte[] Content)[] files)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        using (var tar = new TarWriter(gzip, TarEntryFormat.Pax, leaveOpen: false))
        {
            foreach (var (name, content) in files)
            {
                tar.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, name)
                {
                    DataStream = new MemoryStream(content),
                    Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                });
            }
        }

        return output.ToArray();
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(respond(request));
        }
    }
}
