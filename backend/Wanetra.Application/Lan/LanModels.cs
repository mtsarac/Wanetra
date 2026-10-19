namespace Wanetra.Application.Lan;

public sealed record DiscoveredHost(string Ip, string Mac, string? Hostname, string? Description);

public sealed record LanScanResult(IReadOnlyList<DiscoveredHost> Hosts, string? Error = null);

public sealed record DetectedNetwork(CidrRange Range, string InterfaceName, string Address);

// OnlyContainerNetworks: every usable interface was one that Docker creates for a container on a bridge network.
public sealed record NetworkDetection(IReadOnlyList<DetectedNetwork> Networks, bool OnlyContainerNetworks);

public interface ILanNetworkDetector
{
    NetworkDetection Detect();
}

public interface ILanScanner
{
    Task<LanScanResult> ScanAsync(IReadOnlyList<CidrRange> ranges, CancellationToken cancellationToken);
}

public interface IOuiLookup
{
    string? Lookup(string mac);
}

public sealed record DeviceAlertTransition(Wanetra.Domain.NotificationTrigger Trigger, long AlertId);

public sealed record LanScanOutcome(Wanetra.Domain.ScanRun Run, IReadOnlyList<DeviceAlertTransition> Transitions);

public enum LanScanState
{
    Idle,
    Running,
}

public sealed record LanScanStatus(LanScanState State, string? Trigger = null, DateTimeOffset? StartedAt = null)
{
    public static readonly LanScanStatus Idle = new(LanScanState.Idle);
}
