using Microsoft.EntityFrameworkCore;
using Wanetra.Domain;

namespace Wanetra.Infrastructure.Persistence;

internal sealed class AlertStateRepository(WanetraDbContext dbContext) : IAlertStateRepository
{
    private const int StateId = 1;

    public Task<AlertRule?> GetEnabledRuleAsync(CancellationToken cancellationToken) =>
        dbContext.AlertRules
            .Where(rule => rule.Enabled)
            .OrderBy(rule => rule.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<AlertRule?> GetRuleAsync(CancellationToken cancellationToken) =>
        dbContext.AlertRules.OrderBy(rule => rule.Id).FirstOrDefaultAsync(cancellationToken);

    public async Task<AlertState> GetStateAsync(CancellationToken cancellationToken)
    {
        var state = await dbContext.AlertStates.SingleOrDefaultAsync(alertState => alertState.Id == StateId, cancellationToken);
        if (state is not null)
        {
            return state;
        }

        state = new AlertState { Id = StateId, UpdatedAt = DateTime.UtcNow };
        dbContext.AlertStates.Add(state);
        return state;
    }

    public Task<DegradationEvent?> GetOpenEventAsync(CancellationToken cancellationToken) =>
        dbContext.DegradationEvents
            .Include(degradationEvent => degradationEvent.NotificationDeliveries)
            .Where(degradationEvent => degradationEvent.Status == DegradationStatus.Active
                || degradationEvent.Status == DegradationStatus.Recovering)
            .OrderBy(degradationEvent => degradationEvent.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<DegradationEvent?> GetEventAsync(long id, CancellationToken cancellationToken) =>
        dbContext.DegradationEvents
            .Include(degradationEvent => degradationEvent.NotificationDeliveries)
            .SingleOrDefaultAsync(degradationEvent => degradationEvent.Id == id, cancellationToken);

    public async Task<IReadOnlyList<DegradationEvent>> GetRecentEventsAsync(int count, CancellationToken cancellationToken) =>
        await dbContext.DegradationEvents
            .AsNoTracking()
            .Include(degradationEvent => degradationEvent.NotificationDeliveries)
            .OrderByDescending(degradationEvent => degradationEvent.StartedAt)
            .ThenByDescending(degradationEvent => degradationEvent.Id)
            .Take(count)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DegradationEvent>> GetPendingNotificationEventsAsync(CancellationToken cancellationToken) =>
        await dbContext.DegradationEvents
            .Include(degradationEvent => degradationEvent.NotificationDeliveries)
            .Where(degradationEvent => degradationEvent.NotificationDeliveries
                .Any(delivery => delivery.Status == NotificationDeliveryStatus.Pending))
            .OrderBy(degradationEvent => degradationEvent.StartedAt)
            .ThenBy(degradationEvent => degradationEvent.Id)
            .ToListAsync(cancellationToken);
    public Task<Outage?> GetOutageAsync(long id, CancellationToken cancellationToken) =>
        dbContext.Outages
            .Include(outage => outage.NotificationDeliveries)
            .SingleOrDefaultAsync(outage => outage.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Outage>> GetPendingNotificationOutagesAsync(CancellationToken cancellationToken) =>
        await dbContext.Outages
            .Include(outage => outage.NotificationDeliveries)
            .Where(outage => !outage.OpenedDeliveryInitialized
                || (outage.Status == OutageStatus.Recovered && !outage.RecoveryDeliveryInitialized)
                || outage.NotificationDeliveries.Any(delivery => delivery.Status == NotificationDeliveryStatus.Pending))
            .OrderBy(outage => outage.StartedAt)
            .ThenBy(outage => outage.Id)
            .ToListAsync(cancellationToken);

    public Task<DeviceAlert?> GetDeviceAlertAsync(long id, CancellationToken cancellationToken) =>
        dbContext.DeviceAlerts
            .Include(alert => alert.NotificationDeliveries)
            .SingleOrDefaultAsync(alert => alert.Id == id, cancellationToken);

    public async Task<IReadOnlyList<DeviceAlert>> GetPendingNotificationDeviceAlertsAsync(CancellationToken cancellationToken) =>
        await dbContext.DeviceAlerts
            .Include(alert => alert.NotificationDeliveries)
            .Where(alert => !alert.OpenedDeliveryInitialized
                || (alert.Kind == DeviceAlertKind.Offline && alert.Status == DeviceAlertStatus.Resolved && !alert.RecoveryDeliveryInitialized)
                || alert.NotificationDeliveries.Any(delivery => delivery.Status == NotificationDeliveryStatus.Pending))
            .OrderBy(alert => alert.StartedAt)
            .ThenBy(alert => alert.Id)
            .ToListAsync(cancellationToken);

    public void AddRule(AlertRule rule) => dbContext.AlertRules.Add(rule);
    public void AddEvent(DegradationEvent degradationEvent) => dbContext.DegradationEvents.Add(degradationEvent);
    public void AddNotificationDelivery(NotificationDelivery delivery) => dbContext.NotificationDeliveries.Add(delivery);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
