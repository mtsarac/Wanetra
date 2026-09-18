using Microsoft.Extensions.DependencyInjection;
using Wanetra.Application.Scheduling;
using Wanetra.Domain;

namespace Wanetra.Api.Tests.SpeedTests;

public class ScheduleServiceTests
{
    [Fact]
    public async Task Update_validates_saves_with_utc_timestamp_and_signals()
    {
        await using var factory = new WanetraApiFactory();
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ScheduleService>();
        var signal = scope.ServiceProvider.GetRequiredService<ScheduleChangeSignal>();
        var repository = scope.ServiceProvider.GetRequiredService<IScheduleSettingsRepository>();

        var wait = signal.WaitAsync(CancellationToken.None);
        Assert.False(wait.IsCompleted);

        var saved = await service.UpdateAsync(true, "*/15 * * * *", "Europe/Berlin", CancellationToken.None);

        Assert.True(saved.Enabled);
        Assert.Equal("*/15 * * * *", saved.CronExpression);
        Assert.Equal("Europe/Berlin", saved.Timezone);
        Assert.Equal(DateTimeKind.Utc, saved.UpdatedAt.Kind);
        Assert.True(wait.IsCompleted);

        var reloaded = await repository.GetAsync(CancellationToken.None);
        Assert.True(reloaded.Enabled);
        Assert.Equal("*/15 * * * *", reloaded.CronExpression);
    }

    [Fact]
    public async Task Update_rejects_invalid_cron_without_saving_or_signalling()
    {
        await using var factory = new WanetraApiFactory();
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ScheduleService>();
        var signal = scope.ServiceProvider.GetRequiredService<ScheduleChangeSignal>();

        var wait = signal.WaitAsync(CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.UpdateAsync(true, "bogus", "Europe/Istanbul", CancellationToken.None));

        Assert.False(wait.IsCompleted);
    }

    [Fact]
    public async Task Update_rejects_invalid_timezone_without_saving_or_signalling()
    {
        await using var factory = new WanetraApiFactory();
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ScheduleService>();
        var signal = scope.ServiceProvider.GetRequiredService<ScheduleChangeSignal>();

        var wait = signal.WaitAsync(CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.UpdateAsync(true, "*/30 * * * *", "Mars/Olympus", CancellationToken.None));

        Assert.False(wait.IsCompleted);
    }

    [Fact]
    public async Task Next_runs_are_empty_when_disabled()
    {
        await using var factory = new WanetraApiFactory();
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ScheduleService>();

        await service.UpdateAsync(false, "*/30 * * * *", "Europe/Istanbul", CancellationToken.None);
        var runs = await service.GetNextRunsAsync(5, CancellationToken.None);

        Assert.Empty(runs);
    }

    [Fact]
    public async Task Next_runs_return_bounded_utc_occurrences()
    {
        await using var factory = new WanetraApiFactory();
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ScheduleService>();

        await service.UpdateAsync(true, "*/30 * * * *", "Europe/Istanbul", CancellationToken.None);
        var runs = await service.GetNextRunsAsync(5, CancellationToken.None);

        Assert.Equal(5, runs.Count);
        Assert.All(runs, run => Assert.Equal(DateTimeKind.Utc, run.Kind));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.GetNextRunsAsync(0, CancellationToken.None));
    }
}
