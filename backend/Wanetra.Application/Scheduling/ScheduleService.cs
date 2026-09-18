using Wanetra.Domain;

namespace Wanetra.Application.Scheduling;

public sealed class ScheduleService(
    IScheduleSettingsRepository repository,
    TimeProvider timeProvider,
    ScheduleCalculator calculator,
    ScheduleChangeSignal changeSignal)
{
    public Task<ScheduleSettings> GetAsync(CancellationToken cancellationToken) =>
        repository.GetAsync(cancellationToken);

    public async Task<ScheduleSettings> UpdateAsync(
        bool enabled,
        string cronExpression,
        string timezone,
        CancellationToken cancellationToken)
    {
        if (!ScheduleCalculator.IsValidCronExpression(cronExpression))
        {
            throw new ArgumentException($"Invalid cron expression: {cronExpression}", nameof(cronExpression));
        }

        if (!ScheduleCalculator.IsValidTimezone(timezone))
        {
            throw new ArgumentException($"Invalid timezone: {timezone}", nameof(timezone));
        }

        var settings = new ScheduleSettings
        {
            Enabled = enabled,
            CronExpression = cronExpression,
            Timezone = timezone,
            UpdatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };

        await repository.SaveAsync(settings, cancellationToken);
        changeSignal.Signal();

        return settings;
    }

    public async Task<IReadOnlyList<DateTime>> GetNextRunsAsync(int count, CancellationToken cancellationToken)
    {
        var settings = await repository.GetAsync(cancellationToken);
        return calculator.GetNextRuns(settings, timeProvider.GetUtcNow().UtcDateTime, count);
    }
}
