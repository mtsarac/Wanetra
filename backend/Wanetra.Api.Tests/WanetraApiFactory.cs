using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Wanetra.Api.Tests;

public class WanetraApiFactory : WebApplicationFactory<Program>
{
    public string DataPath { get; } = Path.Combine(Path.GetTempPath(), $"wanetra-tests-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("WANETRA_DATA_PATH", DataPath);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        if (Directory.Exists(DataPath))
        {
            Directory.Delete(DataPath, recursive: true);
        }

        GC.SuppressFinalize(this);
    }
}
