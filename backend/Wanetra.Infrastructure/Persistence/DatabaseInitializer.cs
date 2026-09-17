using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Wanetra.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WanetraDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));
        var dataSource = dbContext.Database.GetDbConnection().DataSource;

        try
        {
            var directory = Path.GetDirectoryName(dataSource);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            await dbContext.Database.MigrateAsync(cancellationToken);

            logger.LogInformation("Database migration completed at {DatabasePath} ({MigrationCount} applied)", dataSource, pending.Count);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Database initialization failed at {DatabasePath}", dataSource);
            throw;
        }
    }
}
