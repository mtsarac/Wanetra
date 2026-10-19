namespace Wanetra.Domain;

public class Device
{
    public long Id { get; set; }

    // Lower-case, colon separated. The MAC is what identifies a device; its IP may change.
    public required string Mac { get; set; }
    public string? CurrentIp { get; set; }
    public string? Hostname { get; set; }
    public string? Vendor { get; set; }
    public string? Description { get; set; }

    // Chosen by the owner. Shown instead of the hostname when set.
    public string? Name { get; set; }

    // Unknown devices raise an alert when they first show up; marking one as known means "this is mine".
    public bool Known { get; set; }

    // A watched device raises an alert when it goes offline and gets its own latency probe.
    public bool Watch { get; set; }

    public bool Online { get; set; }

    // Phones and laptops rotate private MACs, so the same device can show up under several identities.
    public bool RandomizedMac { get; set; }

    public int MissedScans { get; set; }
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime LastStateChangeAt { get; set; }
}

public enum DeviceEventType
{
    Joined,
    Online,
    Offline,
    IpChanged,
}

public class DeviceEvent
{
    public long Id { get; set; }
    public long DeviceId { get; set; }
    public DateTime Timestamp { get; set; }
    public DeviceEventType Type { get; set; }
    public string? Detail { get; set; }
}

public class ScanRun
{
    public long Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime FinishedAt { get; set; }
    public required string Networks { get; set; }
    public int HostsFound { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public required string Trigger { get; set; }
}
