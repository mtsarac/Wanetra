namespace Wanetra.Domain;

public class AuthSession
{
    public long Id { get; set; }
    public required string TokenHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
