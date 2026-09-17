namespace Wanetra.Application.SpeedTests;

public enum SpeedTestState
{
    Idle,
    Running,
    Failed,
}

public sealed record SpeedTestStatus(
    SpeedTestState State,
    SpeedTestTrigger? Trigger = null,
    DateTimeOffset? StartedAt = null,
    string? ErrorMessage = null)
{
    public static readonly SpeedTestStatus Idle = new(SpeedTestState.Idle);
}
