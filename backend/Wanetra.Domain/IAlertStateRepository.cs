namespace Wanetra.Domain;

public interface IAlertStateRepository
{
    Task<AlertRule?> GetEnabledRuleAsync(CancellationToken cancellationToken);
    Task<AlertRule?> GetRuleAsync(CancellationToken cancellationToken);

    Task<AlertState> GetStateAsync(CancellationToken cancellationToken);

    Task<DegradationEvent?> GetOpenEventAsync(CancellationToken cancellationToken);

    Task<DegradationEvent?> GetEventAsync(long id, CancellationToken cancellationToken);
    Task<IReadOnlyList<DegradationEvent>> GetRecentEventsAsync(int count, CancellationToken cancellationToken);
    Task<IReadOnlyList<DegradationEvent>> GetPendingNotificationEventsAsync(CancellationToken cancellationToken);

    void AddRule(AlertRule rule);
    void AddEvent(DegradationEvent degradationEvent);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
