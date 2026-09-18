namespace Wanetra.Domain;

public interface IScheduleSettingsRepository
{
    Task<ScheduleSettings> GetAsync(CancellationToken cancellationToken);

    Task SaveAsync(ScheduleSettings settings, CancellationToken cancellationToken);
}
