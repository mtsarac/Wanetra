using Microsoft.EntityFrameworkCore;
using Wanetra.Domain;

namespace Wanetra.Infrastructure.Persistence;

internal sealed class NotificationConfigurationRepository(WanetraDbContext dbContext) : INotificationConfigurationRepository
{
    public async Task<IReadOnlyList<NotificationConfiguration>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.NotificationConfigurations
            .OrderBy(configuration => configuration.Id)
            .ToListAsync(cancellationToken);
    public async Task SaveAsync(IReadOnlyList<NotificationConfiguration> configurations, CancellationToken cancellationToken)
    {
        var stored = await dbContext.NotificationConfigurations.ToListAsync(cancellationToken);
        dbContext.NotificationConfigurations.RemoveRange(stored);
        dbContext.NotificationConfigurations.AddRange(configurations);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
