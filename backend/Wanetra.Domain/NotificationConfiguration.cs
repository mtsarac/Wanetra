namespace Wanetra.Domain;

public class NotificationConfiguration
{
    public int Id { get; set; }
    public required string Provider { get; set; }
    public bool Enabled { get; set; }

    // Provider-specific settings, including credentials. Never return this to clients as-is.
    public string ConfigurationJson { get; set; } = "{}";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
