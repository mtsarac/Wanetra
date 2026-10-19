using Microsoft.EntityFrameworkCore;
using Wanetra.Domain;

namespace Wanetra.Infrastructure.Persistence;

internal sealed class DeviceRepository(WanetraDbContext dbContext) : IDeviceRepository
{
    public async Task<IReadOnlyList<Device>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.Devices.OrderBy(device => device.Id).ToListAsync(cancellationToken);

    public Task<Device?> GetAsync(long id, CancellationToken cancellationToken) =>
        dbContext.Devices.SingleOrDefaultAsync(device => device.Id == id, cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken) => dbContext.Devices.CountAsync(cancellationToken);

    public void Add(Device device) => dbContext.Devices.Add(device);

    public void Remove(Device device) => dbContext.Devices.Remove(device);

    public void AddEvent(DeviceEvent deviceEvent) => dbContext.DeviceEvents.Add(deviceEvent);

    public async Task<IReadOnlyList<DeviceEvent>> GetEventsAsync(long? deviceId, int count, CancellationToken cancellationToken) =>
        await dbContext.DeviceEvents
            .AsNoTracking()
            .Where(deviceEvent => deviceId == null || deviceEvent.DeviceId == deviceId)
            .OrderByDescending(deviceEvent => deviceEvent.Timestamp)
            .ThenByDescending(deviceEvent => deviceEvent.Id)
            .Take(count)
            .ToListAsync(cancellationToken);

    public void AddScanRun(ScanRun run) => dbContext.ScanRuns.Add(run);

    public Task<ScanRun?> GetLatestScanRunAsync(CancellationToken cancellationToken) =>
        dbContext.ScanRuns
            .AsNoTracking()
            .OrderByDescending(run => run.StartedAt)
            .ThenByDescending(run => run.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> HasSuccessfulScanAsync(CancellationToken cancellationToken) =>
        dbContext.ScanRuns.AnyAsync(run => run.Success, cancellationToken);

    public void AddAlert(DeviceAlert alert) => dbContext.DeviceAlerts.Add(alert);

    public Task<DeviceAlert?> GetOpenOfflineAlertAsync(long deviceId, CancellationToken cancellationToken) =>
        dbContext.DeviceAlerts
            .Where(alert => alert.DeviceId == deviceId
                && alert.Kind == DeviceAlertKind.Offline
                && alert.Status == DeviceAlertStatus.Active)
            .OrderByDescending(alert => alert.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<int> DeleteEventsOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken) =>
        dbContext.DeviceEvents.Where(deviceEvent => deviceEvent.Timestamp < cutoff).ExecuteDeleteAsync(cancellationToken);

    public Task<int> DeleteScanRunsOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken) =>
        dbContext.ScanRuns.Where(run => run.StartedAt < cutoff).ExecuteDeleteAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
