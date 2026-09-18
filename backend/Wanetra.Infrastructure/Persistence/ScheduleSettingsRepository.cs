using Wanetra.Domain;

namespace Wanetra.Infrastructure.Persistence;

internal sealed class ScheduleSettingsRepository(WanetraDbContext dbContext) : IScheduleSettingsRepository
{
    public async Task<ScheduleSettings> GetAsync(CancellationToken cancellationToken)
    {
        var stored = await dbContext.ScheduleSettings.FindAsync(
            [ScheduleSettings.SingleScheduleId], cancellationToken);

        return stored ?? new ScheduleSettings();
    }

    public async Task SaveAsync(ScheduleSettings settings, CancellationToken cancellationToken)
    {
        settings.Id = ScheduleSettings.SingleScheduleId;
        var stored = await dbContext.ScheduleSettings.FindAsync(
            [ScheduleSettings.SingleScheduleId], cancellationToken);

        if (stored is null)
        {
            dbContext.ScheduleSettings.Add(settings);
        }
        else
        {
            stored.Enabled = settings.Enabled;
            stored.CronExpression = settings.CronExpression;
            stored.Timezone = settings.Timezone;
            stored.UpdatedAt = settings.UpdatedAt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
