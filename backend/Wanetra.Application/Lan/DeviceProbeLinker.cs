using Wanetra.Application.Probes;
using Wanetra.Domain;

namespace Wanetra.Application.Lan;

// A watched device gets a ping target so its latency shows up next to the other probes.
// The target follows the device's IP address when DHCP hands out a new one.
public sealed class DeviceProbeLinker(IProbeRepository probes, TimeProvider timeProvider)
{
    public const string NamePrefix = "Device: ";

    public async Task SyncAsync(Device device, CancellationToken cancellationToken)
    {
        var existing = await FindAsync(device.Id, cancellationToken);
        if (!device.Watch || device.CurrentIp is null)
        {
            if (existing is not null)
            {
                probes.RemoveTarget(existing);
            }

            return;
        }

        if (existing is not null)
        {
            existing.Target = device.CurrentIp;
            return;
        }

        var all = await probes.ListTargetsAsync(onlyEnabled: false, cancellationToken);
        if (all.Count >= ProbeTargetService.MaxTargets)
        {
            return;
        }

        probes.AddTarget(new ProbeTarget
        {
            Name = UniqueName(all, NamePrefix + Label(device)),
            Kind = ProbeKind.Icmp,
            Role = ProbeRole.Custom,
            Target = device.CurrentIp,
            DeviceId = device.Id,
            Enabled = true,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        });
    }

    public async Task RemoveAsync(long deviceId, CancellationToken cancellationToken)
    {
        var existing = await FindAsync(deviceId, cancellationToken);
        if (existing is not null)
        {
            probes.RemoveTarget(existing);
        }
    }

    public static string Label(Device device) =>
        device.Name ?? device.Hostname ?? device.Description ?? device.Vendor ?? device.Mac;

    private async Task<ProbeTarget?> FindAsync(long deviceId, CancellationToken cancellationToken) =>
        (await probes.ListTargetsAsync(onlyEnabled: false, cancellationToken)).FirstOrDefault(target => target.DeviceId == deviceId);

    private static string UniqueName(IReadOnlyList<ProbeTarget> existing, string wanted)
    {
        var name = wanted.Length > 64 ? wanted[..64] : wanted;
        var candidate = name;
        for (var i = 2; existing.Any(target => string.Equals(target.Name, candidate, StringComparison.OrdinalIgnoreCase)); i++)
        {
            var suffix = $" ({i})";
            candidate = (name.Length + suffix.Length > 64 ? name[..(64 - suffix.Length)] : name) + suffix;
        }

        return candidate;
    }
}
