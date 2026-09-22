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
            .Where(degradationEvent => degradationEvent.Status == DegradationStatus.Active
                || degradationEvent.Status == DegradationStatus.Recovering)
            .OrderBy(degradationEvent => degradationEvent.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<DegradationEvent?> GetEventAsync(long id, CancellationToken cancellationToken) =>
        dbContext.DegradationEvents.SingleOrDefaultAsync(degradationEvent => degradationEvent.Id == id, cancellationToken);
    public void AddEvent(DegradationEvent degradationEvent) => dbContext.DegradationEvents.Add(degradationEvent);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
