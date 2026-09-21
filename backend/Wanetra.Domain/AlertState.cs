namespace Wanetra.Domain;

public class AlertState
{
    public int Id { get; set; }
    public int? RuleId { get; set; }
    public DateTime? RuleUpdatedAt { get; set; }
    public int ConsecutiveUnhealthyMeasurements { get; set; }
    public int ConsecutiveHealthyMeasurements { get; set; }
    public DateTime UpdatedAt { get; set; }
}
