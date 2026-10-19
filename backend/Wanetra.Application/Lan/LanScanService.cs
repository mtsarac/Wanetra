using Microsoft.Extensions.Logging;
using Wanetra.Application.Settings;
using Wanetra.Domain;

namespace Wanetra.Application.Lan;

public sealed class LanScanService(
    IDeviceRepository devices,
    ILanScanner scanner,
    ILanNetworkDetector detector,
    IOuiLookup oui,
    DeviceProbeLinker linker,
    SettingsSnapshot settings,
    ILanMetrics metrics,
    TimeProvider timeProvider,
    ILogger<LanScanService> logger)
{
    public async Task<LanScanOutcome> RunAsync(string trigger, CancellationToken cancellationToken)
    {
        var startedAt = timeProvider.GetUtcNow().UtcDateTime;
        var (ranges, problem) = ResolveRanges();
        if (problem is not null)
        {
            return await RecordFailureAsync(trigger, startedAt, ranges, problem, cancellationToken);
        }

        LanScanResult result;
        try
        {
            result = await scanner.ScanAsync(ranges, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "LAN scan failed");
            return await RecordFailureAsync(trigger, startedAt, ranges, "The scan failed. See the Wanetra log.", cancellationToken);
        }

        // Nothing at all answering means the scan, not the whole network, failed (no link, wrong interface).
        // Reading that as "everyone left" would mark every device offline.
        if (result.Error is not null || result.Hosts.Count == 0)
        {
            return await RecordFailureAsync(trigger, startedAt, ranges, result.Error ?? "No device answered the scan.", cancellationToken);
        }

        var transitions = await ReconcileAsync(result.Hosts, startedAt, cancellationToken);
        var run = new ScanRun
        {
            StartedAt = startedAt,
            FinishedAt = timeProvider.GetUtcNow().UtcDateTime,
            Networks = Describe(ranges),
            HostsFound = result.Hosts.Count,
            Success = true,
            Trigger = trigger,
        };
        devices.AddScanRun(run);
        await devices.SaveChangesAsync(cancellationToken);
        await PublishCountsAsync(run, cancellationToken);
        return new LanScanOutcome(run, transitions);
    }

    private (IReadOnlyList<CidrRange> Ranges, string? Problem) ResolveRanges()
    {
        var configured = settings.GetOptionalString(SettingKeys.LanCidr);
        if (configured is not null)
        {
            var parts = configured.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries);
            var ranges = new List<CidrRange>();
            foreach (var part in parts)
            {
                if (!CidrRange.TryParse(part, out var range))
                {
                    return ([], $"'{part}' is not a network in CIDR notation, for example 192.168.1.0/24.");
                }

                if (range.Problem() is { } problem)
                {
                    return ([], problem);
                }

                ranges.Add(range);
            }

            return (ranges, ranges.Count == 0 ? "No network is configured." : null);
        }

        var detection = detector.Detect();
        if (detection.Networks.Count == 0)
        {
            return ([], detection.OnlyContainerNetworks
                ? "Wanetra only sees Docker's own network. Run it with compose.lan.yaml (host networking) to scan your LAN."
                : "No local network was found to scan.");
        }

        return (detection.Networks.Select(network => network.Range).ToList(), null);
    }

    private async Task<LanScanOutcome> RecordFailureAsync(
        string trigger,
        DateTime startedAt,
        IReadOnlyList<CidrRange> ranges,
        string error,
        CancellationToken cancellationToken)
    {
        var run = new ScanRun
        {
            StartedAt = startedAt,
            FinishedAt = timeProvider.GetUtcNow().UtcDateTime,
            Networks = Describe(ranges),
            Success = false,
            Error = error,
            Trigger = trigger,
        };
        devices.AddScanRun(run);
        await devices.SaveChangesAsync(cancellationToken);
        metrics.RecordScan(false, (run.FinishedAt - run.StartedAt).TotalSeconds);
        return new LanScanOutcome(run, []);
    }

    private async Task<IReadOnlyList<DeviceAlertTransition>> ReconcileAsync(
        IReadOnlyList<DiscoveredHost> hosts,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var known = (await devices.ListAsync(cancellationToken)).ToDictionary(device => device.Mac, StringComparer.Ordinal);
        // The first scan only learns what is already there; alerting about every device you own would be noise.
        var baseline = known.Count == 0 && !await devices.HasSuccessfulScanAsync(cancellationToken);
        var offlineAfter = settings.GetInt(SettingKeys.LanOfflineAfterScans);
        var alertOnNew = settings.GetBool(SettingKeys.LanAlertOnNewDevice);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var arrivals = new List<(Device Device, DeviceEvent Event, bool Alert)>();
        var events = new List<(Device Device, DeviceEventType Type, string? Detail)>();
        var alerts = new List<(Device Device, DeviceAlertKind Kind)>();
        var resolutions = new List<Device>();

        foreach (var host in hosts.GroupBy(host => host.Mac).Select(group => group.First()))
        {
            seen.Add(host.Mac);
            var randomized = MacAddress.IsRandomized(host.Mac);
            var vendor = randomized ? null : oui.Lookup(host.Mac);

            if (!known.TryGetValue(host.Mac, out var device))
            {
                device = new Device
                {
                    Mac = host.Mac,
                    CurrentIp = host.Ip,
                    Hostname = host.Hostname,
                    Description = host.Description,
                    Vendor = vendor,
                    Known = baseline,
                    Online = true,
                    RandomizedMac = randomized,
                    FirstSeenAt = now,
                    LastSeenAt = now,
                    LastStateChangeAt = now,
                };
                devices.Add(device);
                if (!baseline)
                {
                    arrivals.Add((device, new DeviceEvent { Timestamp = now, Type = DeviceEventType.Joined, Detail = host.Ip }, alertOnNew));
                }

                continue;
            }

            if (!device.Online)
            {
                device.Online = true;
                device.LastStateChangeAt = now;
                events.Add((device, DeviceEventType.Online, host.Ip));
                resolutions.Add(device);
            }

            if (device.CurrentIp != host.Ip)
            {
                if (device.CurrentIp is not null)
                {
                    events.Add((device, DeviceEventType.IpChanged, $"{device.CurrentIp} → {host.Ip}"));
                }

                device.CurrentIp = host.Ip;
            }

            device.MissedScans = 0;
            device.LastSeenAt = now;
            device.Hostname = host.Hostname ?? device.Hostname;
            device.Description = host.Description ?? device.Description;
            device.Vendor = vendor ?? device.Vendor;
        }

        foreach (var device in known.Values.Where(device => device.Online && !seen.Contains(device.Mac)))
        {
            device.MissedScans++;
            if (device.MissedScans < offlineAfter)
            {
                continue;
            }

            device.Online = false;
            device.LastStateChangeAt = now;
            events.Add((device, DeviceEventType.Offline, null));
            if (device.Watch)
            {
                alerts.Add((device, DeviceAlertKind.Offline));
            }
        }

        // New devices need their ids before events and alerts can point at them.
        await devices.SaveChangesAsync(cancellationToken);

        var created = new List<DeviceAlert>();
        foreach (var (device, deviceEvent, alert) in arrivals)
        {
            deviceEvent.DeviceId = device.Id;
            devices.AddEvent(deviceEvent);
            if (alert)
            {
                created.Add(NewAlert(device, DeviceAlertKind.NewDevice, now));
            }
        }

        foreach (var (device, type, detail) in events)
        {
            devices.AddEvent(new DeviceEvent { DeviceId = device.Id, Timestamp = now, Type = type, Detail = detail });
        }

        foreach (var (device, kind) in alerts)
        {
            created.Add(NewAlert(device, kind, now));
        }

        var transitions = new List<DeviceAlertTransition>();
        foreach (var device in resolutions)
        {
            var open = await devices.GetOpenOfflineAlertAsync(device.Id, cancellationToken);
            if (open is not null)
            {
                open.Status = DeviceAlertStatus.Resolved;
                open.EndedAt = now;
                transitions.Add(new DeviceAlertTransition(NotificationTrigger.Recovered, open.Id));
            }
        }

        foreach (var alert in created)
        {
            devices.AddAlert(alert);
        }

        foreach (var device in known.Values.Concat(arrivals.Select(arrival => arrival.Device)).Where(device => device.Watch))
        {
            await linker.SyncAsync(device, cancellationToken);
        }

        await devices.SaveChangesAsync(cancellationToken);
        transitions.AddRange(created.Select(alert => new DeviceAlertTransition(NotificationTrigger.Opened, alert.Id)));
        return transitions;
    }

    private static DeviceAlert NewAlert(Device device, DeviceAlertKind kind, DateTime now) => new()
    {
        DeviceId = device.Id,
        Kind = kind,
        Status = DeviceAlertStatus.Active,
        StartedAt = now,
        DeviceLabel = DeviceProbeLinker.Label(device),
        Mac = device.Mac,
        Ip = device.CurrentIp,
        Vendor = device.Vendor,
    };

    private async Task PublishCountsAsync(ScanRun run, CancellationToken cancellationToken)
    {
        var all = await devices.ListAsync(cancellationToken);
        metrics.SetDeviceCounts(all.Count, all.Count(device => device.Online), all.Count(device => !device.Known));
        metrics.RecordScan(true, (run.FinishedAt - run.StartedAt).TotalSeconds);
    }

    private static string Describe(IReadOnlyList<CidrRange> ranges) =>
        ranges.Count == 0 ? "—" : string.Join(", ", ranges);
}
