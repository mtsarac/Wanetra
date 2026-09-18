using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Wanetra.Api.Contracts;
using Wanetra.Application.Scheduling;

namespace Wanetra.Api.Tests.SpeedTests;

public class ScheduleApiTests
{
    [Fact]
    public async Task Get_returns_default_disabled_schedule_with_empty_next_runs()
    {
        await using var factory = new WanetraApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/schedule");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var schedule = await response.Content.ReadFromJsonAsync<ScheduleResponse>();

        Assert.NotNull(schedule);
        Assert.False(schedule.Enabled);
        Assert.Equal("*/30 * * * *", schedule.CronExpression);
        Assert.Equal("Europe/Istanbul", schedule.Timezone);
        Assert.Empty(schedule.NextRuns);
    }

    [Fact]
    public async Task Update_persists_and_returns_utc_schedule_with_next_runs()
    {
        await using var factory = new WanetraApiFactory();
        var client = factory.CreateClient();

        var update = await client.PutAsJsonAsync("/api/schedule", new ScheduleUpdateRequest(
            true,
            "*/15 * * * *",
            "Europe/Istanbul"));

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var saved = await update.Content.ReadFromJsonAsync<ScheduleResponse>();

        Assert.NotNull(saved);
        Assert.True(saved.Enabled);
        Assert.Equal("*/15 * * * *", saved.CronExpression);
        Assert.Equal("Europe/Istanbul", saved.Timezone);
        Assert.Equal(DateTimeKind.Utc, saved.UpdatedAt.Kind);
        Assert.Equal(5, saved.NextRuns.Count);
        Assert.All(saved.NextRuns, run => Assert.Equal(DateTimeKind.Utc, run.Kind));

        var reloaded = await client.GetFromJsonAsync<ScheduleResponse>("/api/schedule");

        Assert.NotNull(reloaded);
        Assert.True(reloaded.Enabled);
        Assert.Equal("*/15 * * * *", reloaded.CronExpression);
        Assert.Equal(saved.UpdatedAt, reloaded.UpdatedAt);
    }

    [Fact]
    public async Task Update_wakes_worker_signal()
    {
        await using var factory = new WanetraApiFactory();
        var client = factory.CreateClient();
        var signal = factory.Services.GetRequiredService<ScheduleChangeSignal>();

        var wait = signal.WaitAsync(CancellationToken.None);
        Assert.False(wait.IsCompleted);

        var response = await client.PutAsJsonAsync("/api/schedule", new ScheduleUpdateRequest(
            true,
            "*/15 * * * *",
            "Europe/Istanbul"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(wait.IsCompleted);
    }

    [Fact]
    public async Task Next_runs_are_empty_when_disabled()
    {
        await using var factory = new WanetraApiFactory();
        var client = factory.CreateClient();

        var runs = await client.GetFromJsonAsync<ScheduleNextRunsResponse>("/api/schedule/next-runs?count=5");

        Assert.NotNull(runs);
        Assert.Empty(runs.NextRuns);
    }

    [Fact]
    public async Task Next_runs_return_bounded_utc_occurrences_when_enabled()
    {
        await using var factory = new WanetraApiFactory();
        var client = factory.CreateClient();

        await client.PutAsJsonAsync("/api/schedule", new ScheduleUpdateRequest(
            true,
            "*/30 * * * *",
            "Europe/Istanbul"));

        var runs = await client.GetFromJsonAsync<ScheduleNextRunsResponse>("/api/schedule/next-runs?count=5");

        Assert.NotNull(runs);
        Assert.Equal(5, runs.NextRuns.Count);
        Assert.All(runs.NextRuns, run => Assert.Equal(DateTimeKind.Utc, run.Kind));
    }

    [Theory]
    [InlineData("bogus")]
    [InlineData("")]
    [InlineData("*/15 * *")]
    public async Task Update_rejects_invalid_cron(string cronExpression)
    {
        await using var factory = new WanetraApiFactory();
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/schedule", new ScheduleUpdateRequest(
            true,
            cronExpression,
            "Europe/Istanbul"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_request", (await response.Content.ReadFromJsonAsync<ApiError>())!.Code);
    }

    [Theory]
    [InlineData("Mars/Olympus")]
    [InlineData("")]
    public async Task Update_rejects_invalid_timezone(string timezone)
    {
        await using var factory = new WanetraApiFactory();
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/schedule", new ScheduleUpdateRequest(
            true,
            "*/30 * * * *",
            timezone));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_request", (await response.Content.ReadFromJsonAsync<ApiError>())!.Code);
    }

    [Theory]
    [InlineData("?count=0")]
    [InlineData("?count=-1")]
    [InlineData("?count=101")]
    public async Task Next_runs_reject_invalid_count(string query)
    {
        await using var factory = new WanetraApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/schedule/next-runs{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_request", (await response.Content.ReadFromJsonAsync<ApiError>())!.Code);
    }
}
