namespace Wanetra.Domain;

public interface ISpeedTestResultRepository
{
    Task AddAsync(SpeedTestResult result, CancellationToken cancellationToken);
}
