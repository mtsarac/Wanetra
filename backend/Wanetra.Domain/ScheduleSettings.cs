namespace Wanetra.Domain;

public class ScheduleSettings
{
    public const string DefaultCronExpression = "*/30 * * * *";

    public const string DefaultTimezone = "Europe/Istanbul";
    public const int SingleScheduleId = 1;

    public int Id { get; set; } = SingleScheduleId;
    public bool Enabled { get; set; }
    public string CronExpression { get; set; } = DefaultCronExpression;
    public string Timezone { get; set; } = DefaultTimezone;
    public DateTime UpdatedAt { get; set; }
}
