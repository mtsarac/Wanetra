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
        var byId = stored.ToDictionary(configuration => configuration.Id);
        var retainedIds = configurations.Where(configuration => configuration.Id != 0)
            .Select(configuration => configuration.Id)
            .ToHashSet();

        dbContext.NotificationConfigurations.RemoveRange(stored.Where(configuration => !retainedIds.Contains(configuration.Id)));
        foreach (var configuration in configurations)
        {
            if (configuration.Id == 0)
            {
                dbContext.NotificationConfigurations.Add(configuration);
                continue;
            }

            var existing = byId[configuration.Id];
            existing.Provider = configuration.Provider;
            existing.Enabled = configuration.Enabled;
            existing.ConfigurationJson = configuration.ConfigurationJson;
            existing.UpdatedAt = configuration.UpdatedAt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
