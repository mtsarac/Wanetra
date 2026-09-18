using Cronos;
using Wanetra.Domain;

namespace Wanetra.Application.Scheduling;

public sealed class ScheduleCalculator
{
    public const int MaxNextRuns = 100;

    public static bool IsValidCronExpression(string? expression) =>
        !string.IsNullOrWhiteSpace(expression) &&
        CronExpression.TryParse(expression, CronFormat.Standard, out _);

    public static bool IsValidTimezone(string? timezone)
    {
        if (string.IsNullOrWhiteSpace(timezone))
        {
            return false;
        }

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timezone);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    public IReadOnlyList<DateTime> GetNextRuns(ScheduleSettings settings, DateTime fromUtc, int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, MaxNextRuns);

        if (!settings.Enabled)
        {
            return [];
        }

        if (!IsValidCronExpression(settings.CronExpression))
        {
            throw new ArgumentException($"Invalid cron expression: {settings.CronExpression}", nameof(settings));
        }

        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(settings.Timezone);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new ArgumentException($"Invalid timezone: {settings.Timezone}", nameof(settings), ex);
        }

        CronExpression.TryParse(settings.CronExpression, CronFormat.Standard, out var expression);
        var from = new DateTimeOffset(DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc));
        var runs = new List<DateTime>(count);

        for (var i = 0; i < count; i++)
        {
            var next = expression!.GetNextOccurrence(from, zone);
            if (next is null)
            {
                break;
            }

            runs.Add(next.Value.UtcDateTime);
            from = next.Value;
        }

        return runs;
    }
}
