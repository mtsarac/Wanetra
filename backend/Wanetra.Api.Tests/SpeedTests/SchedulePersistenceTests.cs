using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wanetra.Domain;
using Wanetra.Infrastructure.Persistence;

namespace Wanetra.Api.Tests.SpeedTests;

public class SchedulePersistenceTests
{
    [Fact]
    public void Defaults_are_disabled_with_istanbul_timezone()
    {
        var settings = new ScheduleSettings();

        Assert.Equal(1, settings.Id);
        Assert.False(settings.Enabled);
        Assert.Equal("*/30 * * * *", settings.CronExpression);
        Assert.Equal("Europe/Istanbul", settings.Timezone);
    }

    [Fact]
    public async Task Get_returns_defaults_when_no_row_exists()
    {
        await using var factory = new WanetraApiFactory();
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IScheduleSettingsRepository>();

        var settings = await repository.GetAsync(CancellationToken.None);

        Assert.Equal(1, settings.Id);
        Assert.False(settings.Enabled);
        Assert.Equal("*/30 * * * *", settings.CronExpression);
        Assert.Equal("Europe/Istanbul", settings.Timezone);
    }

    [Fact]
    public async Task Save_and_reload_roundtrips_row_one_with_utc_timestamp()
    {
        await using var factory = new WanetraApiFactory();
        var updatedAt = new DateTime(2026, 9, 18, 12, 0, 0, DateTimeKind.Utc);

        using (var scope = factory.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IScheduleSettingsRepository>();
            await repository.SaveAsync(new ScheduleSettings
            {
                Enabled = true,
                CronExpression = "*/15 * * * *",
                Timezone = "Europe/Istanbul",
                UpdatedAt = updatedAt,
            }, CancellationToken.None);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IScheduleSettingsRepository>();
            var reloaded = await repository.GetAsync(CancellationToken.None);

            Assert.Equal(1, reloaded.Id);
            Assert.True(reloaded.Enabled);
            Assert.Equal("*/15 * * * *", reloaded.CronExpression);
            Assert.Equal("Europe/Istanbul", reloaded.Timezone);
            Assert.Equal(updatedAt, reloaded.UpdatedAt);
            Assert.Equal(DateTimeKind.Utc, reloaded.UpdatedAt.Kind);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<WanetraDbContext>();
            var stored = await dbContext.ScheduleSettings.FindAsync(
                [1], CancellationToken.None);

            Assert.NotNull(stored);
            Assert.Equal(DateTimeKind.Utc, stored.UpdatedAt.Kind);
        }
    }

    [Fact]
    public async Task Save_twice_keeps_single_row()
    {
        await using var factory = new WanetraApiFactory();
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IScheduleSettingsRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<WanetraDbContext>();
        var cancellationToken = CancellationToken.None;

        await repository.SaveAsync(new ScheduleSettings
        {
            Enabled = true,
            CronExpression = "*/15 * * * *",
            Timezone = "Europe/Istanbul",
            UpdatedAt = new DateTime(2026, 9, 18, 12, 0, 0, DateTimeKind.Utc),
        }, cancellationToken);

        await repository.SaveAsync(new ScheduleSettings
        {
            Id = 99,
            Enabled = false,
            CronExpression = "0 * * * *",
            Timezone = "Europe/Istanbul",
            UpdatedAt = new DateTime(2026, 9, 18, 13, 0, 0, DateTimeKind.Utc),
        }, cancellationToken);

        Assert.Equal(1, await dbContext.ScheduleSettings.CountAsync(cancellationToken));
        var stored = await dbContext.ScheduleSettings.SingleAsync(
            settings => settings.Id == 1, cancellationToken);

        Assert.False(stored.Enabled);
        Assert.Equal("0 * * * *", stored.CronExpression);
    }
}
