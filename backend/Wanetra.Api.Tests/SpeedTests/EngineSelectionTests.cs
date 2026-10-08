using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wanetra.Domain;
using Wanetra.Infrastructure;
using Wanetra.Infrastructure.SpeedTests;

namespace Wanetra.Api.Tests.SpeedTests;

public class EngineSelectionTests
{
    [Theory]
    [InlineData(null, "librespeed")]
    [InlineData("librespeed", "librespeed")]
    [InlineData("Cloudflare", "cloudflare")]
    [InlineData(" cloudflare ", "cloudflare")]
    public void Selects_the_configured_engine(string? configured, string expected)
    {
        var settings = new Dictionary<string, string?> { [DependencyInjection.EngineSettingKey] = configured };

        using var provider = BuildProvider(settings);
        using var scope = provider.CreateScope();

        Assert.Equal(expected, scope.ServiceProvider.GetRequiredService<ISpeedTestEngine>().Name);
    }

    [Fact]
    public void Selects_ookla_once_the_license_is_accepted()
    {
        var settings = new Dictionary<string, string?>
        {
            [DependencyInjection.EngineSettingKey] = "ookla",
            ["SpeedTest:Ookla:AcceptLicense"] = "true",
        };

        using var provider = BuildProvider(settings);
        using var scope = provider.CreateScope();

        Assert.Equal("ookla", scope.ServiceProvider.GetRequiredService<ISpeedTestEngine>().Name);
    }

    [Fact]
    public void Refuses_ookla_until_the_license_is_accepted()
    {
        var settings = new Dictionary<string, string?> { [DependencyInjection.EngineSettingKey] = "ookla" };

        using var provider = BuildProvider(settings);
        using var scope = provider.CreateScope();

        var error = Assert.Throws<OptionsValidationException>(
            () => scope.ServiceProvider.GetRequiredService<IOptions<OoklaOptions>>().Value);
        Assert.Contains("AcceptLicense", error.Message);
    }

    [Fact]
    public void Rejects_an_unknown_engine_name_at_startup()
    {
        var settings = new Dictionary<string, string?> { [DependencyInjection.EngineSettingKey] = "fast" };

        var error = Assert.Throws<InvalidOperationException>(() => BuildProvider(settings));

        Assert.Contains("fast", error.Message);
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration, Path.GetTempPath());
        return services.BuildServiceProvider();
    }
}
