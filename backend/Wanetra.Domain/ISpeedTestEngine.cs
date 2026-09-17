namespace Wanetra.Domain;

public interface ISpeedTestEngine
{
    string Name { get; }

    Task<SpeedTestResult> RunAsync(CancellationToken cancellationToken);
}
