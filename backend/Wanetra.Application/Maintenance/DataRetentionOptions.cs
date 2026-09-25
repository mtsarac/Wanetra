namespace Wanetra.Application.Maintenance;

public sealed class DataRetentionOptions
{
    public const string SectionName = "DataRetention";

    public int Days { get; set; } = 365;
}
