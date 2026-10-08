using Microsoft.EntityFrameworkCore;
using Wanetra.Domain;

namespace Wanetra.Infrastructure.Persistence;

internal sealed class AppSettingRepository(WanetraDbContext dbContext) : IAppSettingRepository
{
    public async Task<IReadOnlyList<AppSetting>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.AppSettings
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task SaveAsync(
        IReadOnlyDictionary<string, string?> changes,
        DateTime updatedAt,
        CancellationToken cancellationToken)
    {
        var keys = changes.Keys.ToList();
        var existing = await dbContext.AppSettings
            .Where(setting => keys.Contains(setting.Key))
            .ToDictionaryAsync(setting => setting.Key, cancellationToken);

        foreach (var (key, value) in changes)
        {
            if (value is null)
            {
                if (existing.TryGetValue(key, out var toRemove))
                {
                    dbContext.AppSettings.Remove(toRemove);
                }
            }
            else if (existing.TryGetValue(key, out var toUpdate))
            {
                toUpdate.Value = value;
                toUpdate.UpdatedAt = updatedAt;
            }
            else
            {
                dbContext.AppSettings.Add(new AppSetting
                {
                    Key = key,
                    Value = value,
                    UpdatedAt = updatedAt,
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
