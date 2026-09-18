using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wanetra.Application;
using Wanetra.Application.Scheduling;
using Wanetra.Application.SpeedTests;
using Wanetra.Domain;

namespace Wanetra.Api.Tests.SpeedTests;

public class ScheduleWorkerTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Fires_once_at_next_occurrence_with_scheduled_trigger()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var harness = StartHarness(
            EveryMinute(enabled: true),
            async token =>
            {
                await gate.Task.WaitAsync(token);
                return new SpeedTestResult { Engine = "stub" };
            });

        harness.Time.Advance(TimeSpan.FromSeconds(61));
        await harness.AdvanceUntilAsync(() =>
            harness.Coordinator.Status is { State: SpeedTestState.Running, Trigger: SpeedTestTrigger.Scheduled });

        gate.SetResult();
        await harness.AdvanceUntilAsync(() => harness.Repository.Results.Count == 1);

        Assert.Equal(1, harness.Invocations);
        Assert.Equal(SpeedTestState.Idle, harness.Coordinator.Status.State);
    }

    [Fact]
    public async Task Jumping_past_many_occurrences_fires_only_once_with_no_backfill()
    {
        using var harness = StartHarness(EveryMinute(enabled: true));

        harness.Time.Advance(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(1)));
        await harness.AdvanceUntilAsync(() => harness.Invocations >= 1);
        await Task.Delay(Settle);

        Assert.Equal(1, harness.Invocations);
    }

    [Fact]
    public async Task Schedule_update_wakes_worker_and_uses_new_schedule()
    {
        using var harness = StartHarness(Yearly(enabled: true));
        await harness.Started();

        harness.Time.Advance(TimeSpan.FromMinutes(2));
        await Task.Delay(Settle);
        Assert.Equal(0, harness.Invocations);

        harness.Schedules.Current = EveryMinute(enabled: true);
        harness.Signal.Signal();
        await harness.AdvanceUntilAsync(() => harness.Invocations >= 1);

        Assert.Equal(1, harness.Invocations);
    }

    [Fact]
    public async Task Disabled_schedule_never_fires()
    {
        using var harness = StartHarness(EveryMinute(enabled: false));
        await harness.Started();

        harness.Time.Advance(TimeSpan.FromDays(1));
        await Task.Delay(Settle);

        Assert.Equal(0, harness.Invocations);
    }

    [Fact]
    public async Task Invalid_persistence_does_not_crash_and_recovers_after_update()
    {
        using var harness = StartHarness(new ScheduleSettings
        {
            Enabled = true,
            CronExpression = "bogus",
            Timezone = "UTC",
            UpdatedAt = Start.UtcDateTime,
        });
        await harness.Started();

        harness.Time.Advance(TimeSpan.FromHours(1));
        await Task.Delay(Settle);
        Assert.Equal(0, harness.Invocations);
        Assert.True(harness.WorkerRunning);

        harness.Schedules.Current = EveryMinute(enabled: true);
        harness.Signal.Signal();
        await harness.AdvanceUntilAsync(() => harness.Invocations >= 1);

        Assert.Equal(1, harness.Invocations);
    }

    [Fact]
    public async Task Busy_coordinator_skips_scheduled_run_and_worker_survives()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var harness = StartHarness(
            EveryMinute(enabled: true),
            async token =>
            {
                await gate.Task.WaitAsync(token);
                return new SpeedTestResult { Engine = "stub" };
            });
        await harness.Started();

        var manual = harness.Coordinator.TryStart(SpeedTestTrigger.Manual);
        Assert.NotNull(manual);
        Assert.Equal(1, harness.Invocations);

        await harness.AdvancePastAsync(TimeSpan.FromSeconds(70));
        await Task.Delay(Settle);
        Assert.Equal(1, harness.Invocations);
        Assert.Empty(harness.Repository.Results);

        gate.SetResult();
        await manual.WaitAsync(WaitTimeout);

        await harness.AdvanceUntilAsync(() => harness.Invocations >= 2);
    }

    [Fact]
    public async Task Cancellation_stops_worker_while_waiting()
    {
        using var harness = StartHarness(EveryMinute(enabled: true));
        await harness.Started();

        await harness.StopAsync();

        Assert.False(harness.WorkerRunning);
    }

    private static ScheduleSettings EveryMinute(bool enabled) => new()
    {
        Enabled = enabled,
        CronExpression = "* * * * *",
        Timezone = "UTC",
        UpdatedAt = Start.UtcDateTime,
    };

    private static ScheduleSettings Yearly(bool enabled) => new()
    {
        Enabled = enabled,
        CronExpression = "0 0 1 1 *",
        Timezone = "UTC",
        UpdatedAt = Start.UtcDateTime,
    };

    private static WorkerHarness StartHarness(
        ScheduleSettings initial,
        Func<CancellationToken, Task<SpeedTestResult>>? run = null)
    {
        var harness = new WorkerHarness(initial, run ?? (token => Task.FromResult(new SpeedTestResult { Engine = "stub" })));
        harness.Start();
        return harness;
    }

    private sealed class WorkerHarness : IDisposable
    {
        private readonly ServiceProvider provider;
        private readonly CancellationTokenSource cts = new();
        private int invocations;

        public WorkerHarness(ScheduleSettings initial, Func<CancellationToken, Task<SpeedTestResult>> run)
        {
            Time = new ManualTimeProvider(ScheduleWorkerTests.Start);
            Schedules = new StubScheduleRepository(initial);
            Repository = new RecordingResultRepository();
            var counter = this;

            var services = new ServiceCollection();
            services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
            services.AddSingleton<TimeProvider>(Time);
            services.AddSingleton<IScheduleSettingsRepository>(Schedules);
            services.AddSingleton<ISpeedTestResultRepository>(Repository);
            services.AddSingleton<ISpeedTestEngine>(new StubSpeedTestEngine(async token =>
            {
                Interlocked.Increment(ref counter.invocations);
                return await run(token);
            }));
            services.AddApplication();
            provider = services.BuildServiceProvider();

            Signal = provider.GetRequiredService<ScheduleChangeSignal>();
            Coordinator = provider.GetRequiredService<SpeedTestCoordinator>();
            Worker = provider.GetServices<IHostedService>().OfType<ScheduleWorker>().Single();
        }

        public ManualTimeProvider Time { get; }

        public StubScheduleRepository Schedules { get; }

        public RecordingResultRepository Repository { get; }

        public ScheduleChangeSignal Signal { get; }

        public SpeedTestCoordinator Coordinator { get; }

        public ScheduleWorker Worker { get; }

        public int Invocations => Volatile.Read(ref invocations);

        public bool WorkerRunning => Worker.ExecuteTask is { IsCompleted: false };

        public void Start()
        {
            _ = Worker.StartAsync(cts.Token);
        }

        public async Task Started()
        {
            await Task.Delay(Settle);
        }

        public async Task AdvanceUntilAsync(Func<bool> condition)
        {
            for (var step = 0; step < 500 && !condition(); step++)
            {
                Time.Advance(TimeSpan.FromSeconds(5));
                await Task.Delay(10);
            }

            if (!condition())
            {
                throw new TimeoutException("Timed out waiting for the schedule worker.");
            }
        }

        public async Task AdvancePastAsync(TimeSpan duration)
        {
            var target = Time.GetUtcNow() + duration;
            while (Time.GetUtcNow() < target)
            {
                Time.Advance(TimeSpan.FromSeconds(5));
                await Task.Delay(10);
            }
        }

        public async Task StopAsync()
        {
            await cts.CancelAsync();
            await Worker.StopAsync(CancellationToken.None).WaitAsync(WaitTimeout);
        }

        public void Dispose()
        {
            cts.Cancel();
            provider.Dispose();
            cts.Dispose();
        }
    }

    private sealed class StubScheduleRepository(ScheduleSettings initial) : IScheduleSettingsRepository
    {
        public ScheduleSettings Current = initial;

        public Task<ScheduleSettings> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Current);

        public Task SaveAsync(ScheduleSettings settings, CancellationToken cancellationToken)
        {
            Current = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private readonly Lock gate = new();
        private readonly List<Entry> timers = [];
        private DateTimeOffset now = start;

        public override DateTimeOffset GetUtcNow()
        {
            lock (gate)
            {
                return now;
            }
        }

        public void Advance(TimeSpan delta)
        {
            List<Entry> due;
            lock (gate)
            {
                now += delta;
                due = timers.Where(timer => !timer.Disposed && timer.Due <= now).ToList();
            }

            foreach (var timer in due)
            {
                timer.Fire();
            }
        }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            DateTimeOffset current;
            lock (gate)
            {
                current = now;
            }

            var entry = new Entry(this, callback, state, current + dueTime, period);
            lock (gate)
            {
                timers.Add(entry);
            }

            return entry;
        }

        private void Remove(Entry entry)
        {
            lock (gate)
            {
                timers.Remove(entry);
            }
        }

        private sealed class Entry(
            ManualTimeProvider owner,
            TimerCallback callback,
            object? state,
            DateTimeOffset due,
            TimeSpan period) : ITimer
        {
            public DateTimeOffset Due { get; private set; } = due;

            public bool Disposed { get; private set; }

            public void Fire()
            {
                lock (this)
                {
                    if (Disposed)
                    {
                        return;
                    }

                    if (period == Timeout.InfiniteTimeSpan)
                    {
                        Disposed = true;
                    }
                    else
                    {
                        Due += period;
                    }
                }

                callback(state);
            }

            public bool Change(TimeSpan dueTime, TimeSpan period) => throw new NotSupportedException();

            public void Dispose()
            {
                Disposed = true;
                owner.Remove(this);
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
