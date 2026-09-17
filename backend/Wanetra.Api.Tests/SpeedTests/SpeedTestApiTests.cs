using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wanetra.Api.Contracts;
using Wanetra.Application.SpeedTests;
using Wanetra.Domain;
using Wanetra.Infrastructure.Persistence;

namespace Wanetra.Api.Tests.SpeedTests;

public class SpeedTestApiTests
{
    private static readonly DateTime FirstRun = new(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SecondRun = new(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ThirdRun = new(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Run_accepts_the_request_and_stores_the_result()
    {
        await using var factory = new SpeedTestApiFactory
        {
            Run = _ => Task.FromResult(new SpeedTestResult { Engine = "ignored", DownloadMbps = 812.3 }),
        };
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/speedtests/run", content: null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("started", (await response.Content.ReadFromJsonAsync<SpeedTestRunResponse>())!.Status);

        await WaitUntilFinishedAsync(factory);

        var latest = await client.GetFromJsonAsync<SpeedTestResultResponse>("/api/speedtests/latest");

        Assert.Equal("stub", latest!.Engine);
        Assert.Equal(812.3, latest.DownloadMbps);
        Assert.True(latest.Success);
    }

    [Fact]
    public async Task Run_is_rejected_while_another_test_is_running()
    {
        var engineGate = new TaskCompletionSource();
        await using var factory = new SpeedTestApiFactory
        {
            Run = async _ =>
            {
                await engineGate.Task;
                return new SpeedTestResult { Engine = "stub" };
            },
        };

        var coordinator = factory.Services.GetRequiredService<SpeedTestCoordinator>();
        var running = coordinator.TryStart(SpeedTestTrigger.Manual);
        Assert.NotNull(running);

        var response = await factory.CreateClient().PostAsync("/api/speedtests/run", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("speedtest_already_running", (await response.Content.ReadFromJsonAsync<ApiError>())!.Code);

        engineGate.SetResult();
        await running;
    }

    [Fact]
    public async Task Status_reports_the_running_trigger_and_then_the_failure()
    {
        var engineGate = new TaskCompletionSource();
        await using var factory = new SpeedTestApiFactory
        {
            Run = async _ =>
            {
                await engineGate.Task;
                throw new InvalidOperationException("librespeed-cli exited with code 1");
            },
        };
        var client = factory.CreateClient();

        var running = factory.Services.GetRequiredService<SpeedTestCoordinator>()
            .TryStart(SpeedTestTrigger.Scheduled);

        var whileRunning = await client.GetFromJsonAsync<SpeedTestStatusResponse>("/api/speedtests/status");

        Assert.Equal("running", whileRunning!.State);
        Assert.Equal("scheduled", whileRunning.Trigger);
        Assert.NotNull(whileRunning.StartedAt);

        engineGate.SetResult();
        await running!;

        var afterFailure = await client.GetFromJsonAsync<SpeedTestStatusResponse>("/api/speedtests/status");

        Assert.Equal("failed", afterFailure!.State);
        Assert.Equal("librespeed-cli exited with code 1", afterFailure.ErrorMessage);
        Assert.Equal("scheduled", afterFailure.Trigger);
        Assert.Equal(whileRunning.StartedAt, afterFailure.StartedAt);
    }

    [Fact]
    public async Task Status_is_idle_before_any_test_has_run()
    {
        await using var factory = new SpeedTestApiFactory();

        var status = await factory.CreateClient()
            .GetFromJsonAsync<SpeedTestStatusResponse>("/api/speedtests/status");

        Assert.Equal("idle", status!.State);
        Assert.Null(status.Trigger);
        Assert.Null(status.StartedAt);
    }

    [Fact]
    public async Task Latest_returns_the_most_recent_result()
    {
        await using var factory = new SpeedTestApiFactory();
        await SeedAsync(factory);

        var latest = await factory.CreateClient()
            .GetFromJsonAsync<SpeedTestResultResponse>("/api/speedtests/latest");

        Assert.Equal(ThirdRun, latest!.Timestamp);
        Assert.Equal("stub", latest.Engine);
    }

    [Fact]
    public async Task Latest_returns_404_when_nothing_has_been_recorded()
    {
        await using var factory = new SpeedTestApiFactory();

        var response = await factory.CreateClient().GetAsync("/api/speedtests/latest");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("speedtest_not_found", (await response.Content.ReadFromJsonAsync<ApiError>())!.Code);
    }

    [Fact]
    public async Task A_single_result_can_be_read_by_id()
    {
        await using var factory = new SpeedTestApiFactory();
        await SeedAsync(factory);
        var client = factory.CreateClient();

        var history = await client.GetFromJsonAsync<SpeedTestHistoryResponse>("/api/speedtests");
        var wanted = history!.Items.Single(item => item.Timestamp == FirstRun);

        var result = await client.GetFromJsonAsync<SpeedTestResultResponse>($"/api/speedtests/{wanted.Id}");

        Assert.Equal(wanted.Id, result!.Id);
        Assert.Equal(100, result.DownloadMbps);
        Assert.Equal("Test Server", result.ServerName);
    }

    [Fact]
    public async Task An_unknown_id_returns_404()
    {
        await using var factory = new SpeedTestApiFactory();

        var response = await factory.CreateClient().GetAsync("/api/speedtests/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("speedtest_not_found", (await response.Content.ReadFromJsonAsync<ApiError>())!.Code);
    }

    [Fact]
    public async Task History_returns_the_newest_results_first()
    {
        await using var factory = new SpeedTestApiFactory();
        await SeedAsync(factory);

        var history = await factory.CreateClient()
            .GetFromJsonAsync<SpeedTestHistoryResponse>("/api/speedtests");

        Assert.Equal([ThirdRun, SecondRun, FirstRun], history!.Items.Select(item => item.Timestamp));
        Assert.Equal(3, history.TotalCount);
        Assert.Equal(1, history.TotalPages);
    }

    [Fact]
    public async Task History_can_be_sorted_oldest_first()
    {
        await using var factory = new SpeedTestApiFactory();
        await SeedAsync(factory);

        var history = await factory.CreateClient()
            .GetFromJsonAsync<SpeedTestHistoryResponse>("/api/speedtests?sort=asc");

        Assert.Equal([FirstRun, SecondRun, ThirdRun], history!.Items.Select(item => item.Timestamp));
    }

    [Fact]
    public async Task History_is_paginated()
    {
        await using var factory = new SpeedTestApiFactory();
        await SeedAsync(factory);

        var history = await factory.CreateClient()
            .GetFromJsonAsync<SpeedTestHistoryResponse>("/api/speedtests?page=2&pageSize=2");

        Assert.Equal(FirstRun, Assert.Single(history!.Items).Timestamp);
        Assert.Equal(2, history.Page);
        Assert.Equal(3, history.TotalCount);
        Assert.Equal(2, history.TotalPages);
    }

    [Theory]
    [InlineData("?success=false", 1)]
    [InlineData("?success=true", 2)]
    [InlineData("?engine=LibreSpeed", 2)]
    [InlineData("?engine=stub", 1)]
    [InlineData("?from=2026-09-02T00:00:00Z&to=2026-09-02T23:59:59Z", 1)]
    [InlineData("?from=2026-09-02T00:00:00Z", 2)]
    public async Task History_applies_filters(string query, int expectedCount)
    {
        await using var factory = new SpeedTestApiFactory();
        await SeedAsync(factory);

        var history = await factory.CreateClient()
            .GetFromJsonAsync<SpeedTestHistoryResponse>($"/api/speedtests{query}");

        Assert.Equal(expectedCount, history!.Items.Count);
        Assert.Equal(expectedCount, history.TotalCount);
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=201")]
    [InlineData("?sort=sideways")]
    [InlineData("?from=2026-09-03T00:00:00Z&to=2026-09-01T00:00:00Z")]
    public async Task History_rejects_invalid_parameters(string query)
    {
        await using var factory = new SpeedTestApiFactory();

        var response = await factory.CreateClient().GetAsync($"/api/speedtests{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_request", (await response.Content.ReadFromJsonAsync<ApiError>())!.Code);
    }

    private static async Task SeedAsync(SpeedTestApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WanetraDbContext>();

        dbContext.SpeedTestResults.AddRange(
            new SpeedTestResult
            {
                Timestamp = FirstRun,
                Engine = "librespeed",
                Success = true,
                DownloadMbps = 100,
                ServerName = "Test Server",
            },
            new SpeedTestResult
            {
                Timestamp = SecondRun,
                Engine = "librespeed",
                Success = false,
                ErrorMessage = "connection refused",
            },
            new SpeedTestResult
            {
                Timestamp = ThirdRun,
                Engine = "stub",
                Success = true,
                DownloadMbps = 300,
            });

        await dbContext.SaveChangesAsync();
    }

    private static async Task WaitUntilFinishedAsync(SpeedTestApiFactory factory)
    {
        var coordinator = factory.Services.GetRequiredService<SpeedTestCoordinator>();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (coordinator.Status.State == SpeedTestState.Running)
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed class SpeedTestApiFactory : WanetraApiFactory
    {
        public Func<CancellationToken, Task<SpeedTestResult>> Run { get; init; } =
            _ => Task.FromResult(new SpeedTestResult { Engine = "stub" });

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISpeedTestEngine>();
                services.AddScoped<ISpeedTestEngine>(_ => new StubSpeedTestEngine(Run));
            });
        }
    }
}
