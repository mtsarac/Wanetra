using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wanetra.Infrastructure.Persistence;

namespace Wanetra.Infrastructure;

public static class DependencyInjection
{
    public const string DatabaseFileName = "wanetra.db";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string dataPath)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(dataPath, DatabaseFileName),
        }.ToString();

        services.AddDbContext<WanetraDbContext>(options => options.UseSqlite(connectionString));

        return services;
    }
}
