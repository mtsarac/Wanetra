namespace Wanetra.Domain;

public enum ProbeKind
{
    Icmp,
    Tcp,
    Dns,
    Http,
}

public enum ProbeRole
{
    Gateway,
    Internet,
    Dns,
    Custom,
}

public class ProbeTarget
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public ProbeKind Kind { get; set; }
    public ProbeRole Role { get; set; }

    // Host or IP for icmp/tcp, the name to resolve for dns, the full URL for http.
    // Empty on a gateway target means "the default gateway, detected at probe time".
    public string? Target { get; set; }
    public int? Port { get; set; }

    // Dns only: resolver address. Empty means the system resolver.
    public string? Resolver { get; set; }

    // Set when the target follows a watched device; the Devices page owns it.
    public long? DeviceId { get; set; }

    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
