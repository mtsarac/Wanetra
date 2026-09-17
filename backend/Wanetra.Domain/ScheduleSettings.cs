namespace Wanetra.Domain;

public class ScheduleSettings
{
    public const string DefaultCronExpression = "*/30 * * * *";

    public int Id { get; set; }
    public bool Enabled { get; set; } = true;
    public string CronExpression { get; set; } = DefaultCronExpression;
    public string Timezone { get; set; } = "UTC";
    public DateTime UpdatedAt { get; set; }
}
