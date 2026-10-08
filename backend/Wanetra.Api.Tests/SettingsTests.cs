using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Wanetra.Api.Contracts;
using Wanetra.Api.Tests.SpeedTests;
using Wanetra.Application.Maintenance;
using Wanetra.Application.Settings;
using Wanetra.Domain;
using Wanetra.Infrastructure.Persistence;

namespace Wanetra.Api.Tests;

public class SettingsTests(WanetraApiFactory factory) : IClassFixture<WanetraApiFactory>
{
    private static readonly DateTimeOffset InitialTime = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Get_returns_all_settings_in_defined_order_with_defaults()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/settings");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var listResponse = await response.Content.ReadFromJsonAsync<SettingsListResponse>();
        Assert.NotNull(listResponse);

        var expectedKeys = SettingDefinitions.All.Select(d => d.Key).ToList();
        var actualKeys = listResponse.Settings.Select(s => s.Key).ToList();
        Assert.Equal(expectedKeys, actualKeys);

        var engine = listResponse.Settings.First(s => s.Key == "speedtest.engine");
        Assert.Equal("choice", engine.Type);
        Assert.Equal("speedtest", engine.Group);
        Assert.Equal("librespeed", engine.DefaultValue?.ToString());
        Assert.False(engine.Locked);

        var restrictedPath = listResponse.Settings.First(s => s.Key == "speedtest.librespeed.executablePath");
        Assert.True(restrictedPath.Locked);
        Assert.Equal("restricted", restrictedPath.LockReason);
    }

    [Fact]
    public async Task Put_updates_setting_and_reflected_in_get()
    {
        var client = factory.CreateClient();

        var updateBody = new Dictionary<string, object?>
        {
            ["speedtest.librespeed.timeoutSeconds"] = 45,
        };

        var putRes = await client.PutAsJsonAsync("/api/settings", new { values = updateBody });
        Assert.Equal(HttpStatusCode.OK, putRes.StatusCode);

        var putList = await putRes.Content.ReadFromJsonAsync<SettingsListResponse>();
        Assert.NotNull(putList);

        var timeoutSetting = putList.Settings.First(s => s.Key == "speedtest.librespeed.timeoutSeconds");
        Assert.Equal(45, ((JsonElement)timeoutSetting.Value!).GetInt32());
        Assert.Equal("stored", timeoutSetting.Source);
        Assert.NotNull(timeoutSetting.UpdatedAt);

        // Verify GET also reflects it
        var getRes = await client.GetFromJsonAsync<SettingsListResponse>("/api/settings");
        Assert.NotNull(getRes);
        var getSetting = getRes.Settings.First(s => s.Key == "speedtest.librespeed.timeoutSeconds");
        Assert.Equal(45, ((JsonElement)getSetting.Value!).GetInt32());

        // Reset to default with null
        var resetBody = new Dictionary<string, object?>
        {
            ["speedtest.librespeed.timeoutSeconds"] = null,
        };
        var resetRes = await client.PutAsJsonAsync("/api/settings", new { values = resetBody });
        Assert.Equal(HttpStatusCode.OK, resetRes.StatusCode);

        var resetList = await resetRes.Content.ReadFromJsonAsync<SettingsListResponse>();
        Assert.NotNull(resetList);
        var resetSetting = resetList.Settings.First(s => s.Key == "speedtest.librespeed.timeoutSeconds");
        Assert.Equal(120, ((JsonElement)resetSetting.Value!).GetInt32());
        Assert.Equal("default", resetSetting.Source);
    }

    [Fact]
    public async Task Put_rejects_unknown_setting_with_400()
    {
        var client = factory.CreateClient();
        var updateBody = new Dictionary<string, object?>
        {
            ["unknown.key"] = "value",
        };

        var response = await client.PutAsJsonAsync("/api/settings", new { values = updateBody });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var err = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.NotNull(err);
        Assert.Equal("unknown_setting", err.Code);
        Assert.Contains("unknown.key", err.Message);
    }

    [Fact]
    public async Task Put_rejects_restricted_setting_with_409()
    {
        var client = factory.CreateClient();
        var updateBody = new Dictionary<string, object?>
        {
            ["speedtest.librespeed.executablePath"] = "/usr/bin/my-bin",
        };

        var response = await client.PutAsJsonAsync("/api/settings", new { values = updateBody });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var err = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.NotNull(err);
        Assert.Equal("setting_locked", err.Code);
        Assert.Contains("speedtest.librespeed.executablePath", err.Message);
        Assert.Contains("SpeedTest__LibreSpeed__ExecutablePath", err.Message);
    }

    [Fact]
    public async Task Put_is_atomic_on_failure()
    {
        var client = factory.CreateClient();

        // Attempt batch with one valid and one unknown setting
        var updateBody = new Dictionary<string, object?>
        {
            ["speedtest.librespeed.timeoutSeconds"] = 99,
            ["invalid.unknown.key"] = "val",
        };

        var response = await client.PutAsJsonAsync("/api/settings", new { values = updateBody });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Check timeoutSeconds was NOT updated
        var getRes = await client.GetFromJsonAsync<SettingsListResponse>("/api/settings");
        Assert.NotNull(getRes);
        var getSetting = getRes.Settings.First(s => s.Key == "speedtest.librespeed.timeoutSeconds");
        Assert.NotEqual(99, ((JsonElement)getSetting.Value!).GetInt32());
    }

    [Fact]
    public async Task Put_validates_bounds_and_types()
    {
        var client = factory.CreateClient();

        // Below min (timeout min is 10)
        var lowRes = await client.PutAsJsonAsync("/api/settings", new
        {
            values = new Dictionary<string, object?> { ["speedtest.librespeed.timeoutSeconds"] = 5 },
        });
        Assert.Equal(HttpStatusCode.BadRequest, lowRes.StatusCode);
        var lowErr = await lowRes.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal("invalid_setting", lowErr?.Code);

        // Above max (timeout max is 1800)
        var highRes = await client.PutAsJsonAsync("/api/settings", new
        {
            values = new Dictionary<string, object?> { ["speedtest.librespeed.timeoutSeconds"] = 2000 },
        });
        Assert.Equal(HttpStatusCode.BadRequest, highRes.StatusCode);

        // Wrong type (string where integer expected)
        var typeRes = await client.PutAsJsonAsync("/api/settings", new
        {
            values = new Dictionary<string, object?> { ["speedtest.librespeed.timeoutSeconds"] = "invalid" },
        });
        Assert.Equal(HttpStatusCode.BadRequest, typeRes.StatusCode);

        // Invalid choice
        var choiceRes = await client.PutAsJsonAsync("/api/settings", new
        {
            values = new Dictionary<string, object?> { ["speedtest.engine"] = "invalid-engine" },
        });
        Assert.Equal(HttpStatusCode.BadRequest, choiceRes.StatusCode);
    }

    [Fact]
    public async Task Put_enforces_cross_key_ookla_rules()
    {
        var client = factory.CreateClient();

        // 1. Setting engine to ookla without acceptLicense true -> fails
        var setOoklaRes = await client.PutAsJsonAsync("/api/settings", new
        {
            values = new Dictionary<string, object?> { ["speedtest.engine"] = "ookla" },
        });
        Assert.Equal(HttpStatusCode.BadRequest, setOoklaRes.StatusCode);
        var setOoklaErr = await setOoklaRes.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal("invalid_setting", setOoklaErr?.Code);
        Assert.Contains("speedtest.engine", setOoklaErr?.Message);

        // 2. Setting both engine=ookla AND acceptLicense=true -> succeeds
        var bothRes = await client.PutAsJsonAsync("/api/settings", new
        {
            values = new Dictionary<string, object?>
            {
                ["speedtest.engine"] = "ookla",
                ["speedtest.ookla.acceptLicense"] = true,
            },
        });
        Assert.Equal(HttpStatusCode.OK, bothRes.StatusCode);

        // 3. Setting acceptLicense to false while engine is ookla -> fails naming acceptLicense
        var rejectLicenseRes = await client.PutAsJsonAsync("/api/settings", new
        {
            values = new Dictionary<string, object?> { ["speedtest.ookla.acceptLicense"] = false },
        });
        Assert.Equal(HttpStatusCode.BadRequest, rejectLicenseRes.StatusCode);
        var rejectErr = await rejectLicenseRes.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal("invalid_setting", rejectErr?.Code);
        Assert.Contains("speedtest.ookla.acceptLicense", rejectErr?.Message);

        // Clean up: reset engine back to librespeed
        await client.PutAsJsonAsync("/api/settings", new
        {
            values = new Dictionary<string, object?>
            {
                ["speedtest.engine"] = "librespeed",
                ["speedtest.ookla.acceptLicense"] = false,
            },
        });
    }

    [Fact]
    public async Task Next_resolved_engine_actually_switches_after_PUT()
    {
        var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var engine = scope.ServiceProvider.GetRequiredService<ISpeedTestEngine>();
            Assert.Equal("librespeed", engine.Name);
        }

        // Switch to cloudflare via PUT
        var putRes = await client.PutAsJsonAsync("/api/settings", new
        {
            values = new Dictionary<string, object?> { ["speedtest.engine"] = "cloudflare" },
        });
        Assert.Equal(HttpStatusCode.OK, putRes.StatusCode);

        // Next scope resolves cloudflare
        using (var scope = factory.Services.CreateScope())
        {
            var engine = scope.ServiceProvider.GetRequiredService<ISpeedTestEngine>();
            Assert.Equal("cloudflare", engine.Name);
        }

        // Clean up back to librespeed
        await client.PutAsJsonAsync("/api/settings", new
        {
            values = new Dictionary<string, object?> { ["speedtest.engine"] = "librespeed" },
        });
    }

    [Fact]
    public void Precedence_env_over_stored_over_default_and_empty_env_treated_as_unset()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SpeedTest:Engine"] = "cloudflare",
            ["SpeedTest:LibreSpeed:TimeoutSeconds"] = "", // Empty env counts as unset
            ["SpeedTest:Cloudflare:TimeoutSeconds"] = "  ", // Whitespace counts as unset
        }).Build();

        var stored = new Dictionary<string, (string? Value, DateTime? UpdatedAt)>
        {
            ["speedtest.engine"] = ("librespeed", DateTime.UtcNow), // Should be overridden by env "cloudflare"
            ["speedtest.librespeed.timeoutSeconds"] = ("60", DateTime.UtcNow), // Stored used because env is empty
        };

        var resolved = SettingsService.ResolveEffectiveSettings(stored, config, changedKeys: null);

        // 1. Env beats stored
        var engine = resolved["speedtest.engine"];
        Assert.Equal("cloudflare", engine.Value);
        Assert.Equal(SettingSource.Environment, engine.Source);
        Assert.True(engine.Locked);
        Assert.Equal("environment", engine.LockReason);

        // 2. Empty env treated as unset -> stored wins
        var timeout = resolved["speedtest.librespeed.timeoutSeconds"];
        Assert.Equal(60, timeout.Value);
        Assert.Equal(SettingSource.Stored, timeout.Source);
        Assert.False(timeout.Locked);

        // 3. Whitespace env treated as unset, no stored -> default wins
        var cfTimeout = resolved["speedtest.cloudflare.timeoutSeconds"];
        Assert.Equal(300, cfTimeout.Value);
        Assert.Equal(SettingSource.Default, cfTimeout.Source);
        Assert.False(cfTimeout.Locked);
    }

    [Fact]
    public async Task Put_rejects_env_locked_setting_with_409()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SpeedTest:Engine"] = "librespeed",
        }).Build();

        var repo = new InMemoryAppSettingRepository();
        var snapshot = new SettingsSnapshot();
        var service = new SettingsService(repo, config, snapshot, TimeProvider.System);
        await service.InitializeAsync(CancellationToken.None);

        var ex = await Assert.ThrowsAsync<SettingsException>(() => service.UpdateAsync(
            new Dictionary<string, JsonElement>
            {
                ["speedtest.engine"] = JsonDocument.Parse("\"cloudflare\"").RootElement,
            },
            CancellationToken.None));

        Assert.Equal(SettingsErrorKind.SettingLocked, ex.Kind);
        Assert.Contains("speedtest.engine", ex.Message);
        Assert.Contains("SpeedTest__Engine", ex.Message);
    }

    [Fact]
    public void Startup_refusal_for_invalid_effective_config_names_env_var()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SpeedTest:Engine"] = "ookla",
            ["SpeedTest:Ookla:AcceptLicense"] = "false",
        }).Build();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            SettingsService.ResolveEffectiveSettings(
                new Dictionary<string, (string? Value, DateTime? UpdatedAt)>(),
                config,
                changedKeys: null));

        Assert.Contains("SpeedTest__Ookla__AcceptLicense", ex.Message);
        Assert.Contains("SpeedTest__Engine", ex.Message);
    }

    [Fact]
    public async Task Retention_worker_uses_updated_days_from_snapshot_on_next_pass()
    {
        var time = new ManualTimeProvider(InitialTime);
        var snapshot = new SettingsSnapshot();
        var repo = new InMemoryAppSettingRepository();
        var config = new ConfigurationBuilder().Build();
        var settingsService = new SettingsService(repo, config, snapshot, time);
        await settingsService.InitializeAsync(CancellationToken.None);

        var resultRepo = new RecordingResultRepository();
        var services = new ServiceCollection();
        services.AddSingleton<ISpeedTestResultRepository>(resultRepo);
        services.AddLogging();
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var worker = new DataRetentionWorker(
            scopeFactory,
            snapshot,
            time,
            NullLogger<DataRetentionWorker>.Instance);

        // Pass 1: retention is default 365 days
        var now1 = time.GetUtcNow().UtcDateTime;
        resultRepo.Results.Add(new SpeedTestResult
        {
            Engine = "test",
            Timestamp = now1.AddDays(-400),
            Success = true,
        });
        resultRepo.Results.Add(new SpeedTestResult
        {
            Engine = "test",
            Timestamp = now1.AddDays(-100),
            Success = true,
        });

        await worker.DeleteExpiredResultsAsync(CancellationToken.None);

        // Only the 400-day-old result is deleted
        Assert.Single(resultRepo.Results);
        Assert.Equal(now1.AddDays(-100), resultRepo.Results[0].Timestamp);

        // Update retention to 30 days via SettingsService
        await settingsService.UpdateAsync(
            new Dictionary<string, JsonElement>
            {
                ["retention.days"] = JsonDocument.Parse("30").RootElement,
            },
            CancellationToken.None);

        Assert.Equal(30, snapshot.GetInt(SettingKeys.RetentionDays));

        // Pass 2: retention is now 30 days, so the 100-day-old result is deleted
        await worker.DeleteExpiredResultsAsync(CancellationToken.None);
        Assert.Empty(resultRepo.Results);
    }

    private sealed class InMemoryAppSettingRepository : IAppSettingRepository
    {
        private readonly Dictionary<string, AppSetting> settings = [];

        public Task<IReadOnlyList<AppSetting>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AppSetting>>(settings.Values.ToList());

        public Task SaveAsync(IReadOnlyDictionary<string, string?> changes, DateTime updatedAt, CancellationToken cancellationToken)
        {
            foreach (var (k, v) in changes)
            {
                if (v is null)
                {
                    settings.Remove(k);
                }
                else
                {
                    settings[k] = new AppSetting { Key = k, Value = v, UpdatedAt = updatedAt };
                }
            }
            return Task.CompletedTask;
        }
    }
}
