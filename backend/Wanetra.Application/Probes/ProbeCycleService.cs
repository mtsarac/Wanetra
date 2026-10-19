using Microsoft.Extensions.Logging;
using Wanetra.Application.Settings;
using Wanetra.Application.SpeedTests;
using Wanetra.Domain;

namespace Wanetra.Application.Probes;

public sealed record ProbeCycleResult(IReadOnlyList<ProbeTargetResult> Results, OutageTransition? Transition);

public sealed class ProbeCycleService(
    IProbeRepository probes,
    IProbeExecutor executor,
    IGatewayDetector gatewayDetector,
    OutageService outageService,
    SpeedTestCoordinator speedTests,
    SettingsSnapshot settings,
    IProbeMetrics metrics,
    IAppSettingRepository appSettings,
    TimeProvider timeProvider,
    ILogger<ProbeCycleService> logger)
{
    private const int MaxParallelProbes = 16;

    public async Task<ProbeCycleResult> RunAsync(CancellationToken cancellationToken)
    {
        var targets = await probes.ListTargetsAsync(onlyEnabled: true, cancellationToken);
        if (targets.Count == 0)
        {
            return new ProbeCycleResult([], null);
        }

        var startedAt = timeProvider.GetUtcNow().UtcDateTime;
        var timeout = TimeSpan.FromMilliseconds(settings.GetInt(SettingKeys.ProbesTimeoutMs));
        var gateway = targets.Any(NeedsGateway) ? gatewayDetector.DetectGateway() : null;
        var duringSpeedTest = speedTests.Status.State == SpeedTestState.Running;

        using var limiter = new SemaphoreSlim(MaxParallelProbes);
        var results = (await Task.WhenAll(targets.Select(target => ProbeAsync(target, gateway, timeout, limiter, cancellationToken))))
            .Where(result => result is not null)
            .Select(result => result!)
            .ToList();

        probes.AddSamples(results.Select(result => new ProbeSample
        {
            TargetId = result.Target.Id,
            Timestamp = startedAt,
            Success = result.Outcome.Success,
            RttMs = result.Outcome.RttMs,
            Error = Truncate(result.Outcome.Error),
            DuringSpeedTest = duringSpeedTest,
        }));
        await probes.SaveChangesAsync(cancellationToken);

        foreach (var result in results)
        {
            metrics.RecordProbe(result.Target.Name, RoleName(result.Target.Role), result.Outcome.Success, result.Outcome.RttMs);
        }

        await RememberMonitoringStartAsync(startedAt, cancellationToken);

        // A speed test saturates the link, so probe failures during one do not mean the connection is down.
        OutageTransition? transition = null;
        if (!duringSpeedTest && results.Count > 0)
        {
            var diagnosis = OutageClassifier.Classify(results.Select(result => (result.Target.Role, result.Outcome.Success)));
            transition = await outageService.EvaluateAsync(diagnosis, startedAt, cancellationToken);
        }

        return new ProbeCycleResult(results, transition);
    }

    private async Task<ProbeTargetResult?> ProbeAsync(
        ProbeTarget target,
        string? gateway,
        TimeSpan timeout,
        SemaphoreSlim limiter,
        CancellationToken cancellationToken)
    {
        var host = target.Target ?? (NeedsGateway(target) ? gateway : null);
        if (host is null)
        {
            return null;
        }

        await limiter.WaitAsync(cancellationToken);
        try
        {
            var outcome = await executor.ExecuteAsync(
                new ProbeRequest(target.Kind, host, target.Port, target.Resolver, timeout),
                cancellationToken);
            return new ProbeTargetResult(target, outcome);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Probe {Target} failed unexpectedly", target.Name);
            return new ProbeTargetResult(target, ProbeOutcome.Down("error"));
        }
        finally
        {
            limiter.Release();
        }
    }

    private async Task RememberMonitoringStartAsync(DateTime now, CancellationToken cancellationToken)
    {
        var stored = await appSettings.ListAsync(cancellationToken);
        if (stored.Any(setting => setting.Key == ProbeInternalKeys.MonitoringSince))
        {
            return;
        }

        await appSettings.SaveAsync(
            new Dictionary<string, string?>
            {
                [ProbeInternalKeys.MonitoringSince] = now.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            },
            now,
            cancellationToken);
    }

    private static bool NeedsGateway(ProbeTarget target) => target.Role == ProbeRole.Gateway && target.Target is null;

    private static string RoleName(ProbeRole role) => role.ToString().ToLowerInvariant();

    private static string? Truncate(string? error) =>
        error is { Length: > 64 } ? error[..64] : error;
}
