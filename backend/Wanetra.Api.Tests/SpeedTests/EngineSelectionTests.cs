using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Wanetra.Application.Settings;
using Wanetra.Domain;
using Wanetra.Infrastructure;

namespace Wanetra.Api.Tests.SpeedTests;

public class EngineSelectionTests
{
    [Theory]
    [InlineData(null, "librespeed")]
    [InlineData("librespeed", "librespeed")]
    [InlineData("Cloudflare", "cloudflare")]
    [InlineData(" cloudflare ", "cloudflare")]
    public async Task Selects_the_configured_engine(string? configured, string expected)
    {
        var settings = new Dictionary<string, string?> { ["SpeedTest:Engine"] = configured };

        await using var provider = await BuildProviderAsync(settings);
        using var scope = provider.CreateScope();

        Assert.Equal(expected, scope.ServiceProvider.GetRequiredService<ISpeedTestEngine>().Name);
    }

    [Fact]
    public async Task Selects_ookla_once_the_license_is_accepted()
    {
        var settings = new Dictionary<string, string?>
        {
            ["SpeedTest:Engine"] = "ookla",
            ["SpeedTest:Ookla:AcceptLicense"] = "true",
        };

        await using var provider = await BuildProviderAsync(settings);
        using var scope = provider.CreateScope();

        Assert.Equal("ookla", scope.ServiceProvider.GetRequiredService<ISpeedTestEngine>().Name);
    }

    [Fact]
    public async Task Refuses_ookla_until_the_license_is_accepted()
    {
        var settings = new Dictionary<string, string?> { ["SpeedTest:Engine"] = "ookla" };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => BuildProviderAsync(settings));
        Assert.Contains("AcceptLicense", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejects_an_unknown_engine_name_at_startup()
    {
        var settings = new Dictionary<string, string?> { ["SpeedTest:Engine"] = "fast" };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => BuildProviderAsync(settings));
        Assert.Contains("fast", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<ServiceProvider> BuildProviderAsync(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<SettingsSnapshot>();
        services.AddScoped<SettingsService>();
        services.AddInfrastructure(Path.GetTempPath());
        services.AddSingleton<IAppSettingRepository, StubAppSettingRepository>();

        var provider = services.BuildServiceProvider();
        using (var scope = provider.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<SettingsService>();
            await service.InitializeAsync(CancellationToken.None);
        }

        return provider;
    }

    private sealed class StubAppSettingRepository : IAppSettingRepository
    {
        public Task<IReadOnlyList<AppSetting>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AppSetting>>([]);

        public Task SaveAsync(IReadOnlyDictionary<string, string?> changes, DateTime updatedAt, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
