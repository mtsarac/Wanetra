namespace Wanetra.Domain;

public class AuthAccount
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }
    public bool AuthEnabled { get; set; } = true;
    public string? MetricsTokenHash { get; set; }
    public DateTime? MetricsTokenCreatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
