namespace Wanetra.Domain;

public interface IAlertStateRepository
{
    Task<AlertRule?> GetEnabledRuleAsync(CancellationToken cancellationToken);

    Task<AlertState> GetStateAsync(CancellationToken cancellationToken);

    Task<DegradationEvent?> GetOpenEventAsync(CancellationToken cancellationToken);

    void AddEvent(DegradationEvent degradationEvent);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
