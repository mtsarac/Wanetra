using Wanetra.Application.Scheduling;
using Wanetra.Domain;

namespace Wanetra.Api.Tests.SpeedTests;

public class ScheduleCalculatorTests
{
    private readonly ScheduleCalculator calculator = new();

    [Theory]
    [InlineData("*/30 * * * *")]
    [InlineData("0 9 * * *")]
    [InlineData("0 0 * * 1")]
    public void Valid_five_field_cron_passes(string expression)
    {
        Assert.True(ScheduleCalculator.IsValidCronExpression(expression));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a cron")]
    [InlineData("*/30 * * * * *")]
    [InlineData("0 0 0 * * *")]
    public void Invalid_or_non_five_field_cron_fails(string? expression)
    {
        Assert.False(ScheduleCalculator.IsValidCronExpression(expression));
    }

    [Theory]
    [InlineData("Europe/Istanbul")]
    [InlineData("Europe/Berlin")]
    [InlineData("UTC")]
    public void Known_timezone_passes(string timezone)
    {
        Assert.True(ScheduleCalculator.IsValidTimezone(timezone));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Mars/Olympus")]
    public void Unknown_timezone_fails(string? timezone)
    {
        Assert.False(ScheduleCalculator.IsValidTimezone(timezone));
    }

    [Fact]
    public void Disabled_schedule_has_no_runs()
    {
        var settings = new ScheduleSettings { Enabled = false };
        var from = new DateTime(2026, 9, 18, 12, 0, 0, DateTimeKind.Utc);

        Assert.Empty(calculator.GetNextRuns(settings, from, 5));
    }

    [Fact]
    public void Enabled_schedule_returns_requested_utc_runs_in_order()
    {
        var settings = new ScheduleSettings
        {
            Enabled = true,
            CronExpression = "*/30 * * * *",
            Timezone = "Europe/Istanbul",
        };
        var from = new DateTime(2026, 9, 18, 12, 0, 0, DateTimeKind.Utc);

        var runs = calculator.GetNextRuns(settings, from, 5);

        Assert.Equal(5, runs.Count);
        Assert.All(runs, run => Assert.Equal(DateTimeKind.Utc, run.Kind));
        Assert.All(runs, run => Assert.True(run > from));
        Assert.Equal(runs.OrderBy(x => x).ToList(), runs.ToList());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Count_outside_bounds_throws(int count)
    {
        var settings = new ScheduleSettings
        {
            Enabled = true,
            CronExpression = "*/30 * * * *",
            Timezone = "Europe/Istanbul",
        };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => calculator.GetNextRuns(settings, new DateTime(2026, 9, 18, 12, 0, 0, DateTimeKind.Utc), count));
    }

    [Fact]
    public void Invalid_cron_throws()
    {
        var settings = new ScheduleSettings
        {
            Enabled = true,
            CronExpression = "bogus",
            Timezone = "Europe/Istanbul",
        };

        Assert.Throws<ArgumentException>(
            () => calculator.GetNextRuns(settings, new DateTime(2026, 9, 18, 12, 0, 0, DateTimeKind.Utc), 5));
    }

    [Fact]
    public void Invalid_timezone_throws()
    {
        var settings = new ScheduleSettings
        {
            Enabled = true,
            CronExpression = "*/30 * * * *",
            Timezone = "Mars/Olympus",
        };

        Assert.Throws<ArgumentException>(
            () => calculator.GetNextRuns(settings, new DateTime(2026, 9, 18, 12, 0, 0, DateTimeKind.Utc), 5));
    }

    [Fact]
    public void Daily_berlin_run_shifts_utc_offset_across_spring_forward()
    {
        var settings = new ScheduleSettings
        {
            Enabled = true,
            CronExpression = "0 9 * * *",
            Timezone = "Europe/Berlin",
        };
        var from = new DateTime(2026, 3, 28, 0, 0, 0, DateTimeKind.Utc);

        var runs = calculator.GetNextRuns(settings, from, 2);

        Assert.Equal(
            new DateTime(2026, 3, 28, 8, 0, 0, DateTimeKind.Utc),
            runs[0]);
        Assert.Equal(
            new DateTime(2026, 3, 29, 7, 0, 0, DateTimeKind.Utc),
            runs[1]);
    }

    [Fact]
    public void Calculation_starts_from_supplied_time_with_no_backfill()
    {
        var settings = new ScheduleSettings
        {
            Enabled = true,
            CronExpression = "* * * * *",
            Timezone = "UTC",
        };
        var from = new DateTime(2026, 9, 18, 12, 0, 0, DateTimeKind.Utc);

        var runs = calculator.GetNextRuns(settings, from, 3);

        Assert.Equal(3, runs.Count);
        Assert.All(runs, run => Assert.True(run > from));
        Assert.Equal(new DateTime(2026, 9, 18, 12, 1, 0, DateTimeKind.Utc), runs[0]);
    }
}
