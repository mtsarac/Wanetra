namespace Wanetra.Domain;

public class AppSetting
{
    public required string Key { get; set; }
    public string? Value { get; set; }
    public DateTime UpdatedAt { get; set; }
}
