using Wanetra.Domain;

namespace Wanetra.Api.Contracts;

public sealed record ScheduleResponse(
    bool Enabled,
    string CronExpression,
    string Timezone,
    DateTime UpdatedAt,
    IReadOnlyList<DateTime> NextRuns)
{
    public static ScheduleResponse From(ScheduleSettings settings, IReadOnlyList<DateTime> nextRuns) => new(
        settings.Enabled,
        settings.CronExpression,
        settings.Timezone,
        settings.UpdatedAt,
        nextRuns);
}

public sealed record ScheduleUpdateRequest(
    bool Enabled,
    string CronExpression,
    string Timezone);

public sealed record ScheduleNextRunsResponse(IReadOnlyList<DateTime> NextRuns);
