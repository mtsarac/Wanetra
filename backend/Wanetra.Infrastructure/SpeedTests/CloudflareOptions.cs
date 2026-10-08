namespace Wanetra.Infrastructure.SpeedTests;

public sealed class CloudflareOptions
{
    public const string SectionName = "SpeedTest:Cloudflare";

    public string ExecutablePath { get; set; } = "cfspeedtest";

    // cfspeedtest 2.2.x has no run deadline of its own, so this is the only upper bound.
    public int TimeoutSeconds { get; set; } = 300;
}
