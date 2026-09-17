using Wanetra.Domain;

namespace Wanetra.Infrastructure.Persistence;

internal sealed class SpeedTestResultRepository(WanetraDbContext dbContext) : ISpeedTestResultRepository
{
    public async Task AddAsync(SpeedTestResult result, CancellationToken cancellationToken)
    {
        dbContext.SpeedTestResults.Add(result);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
