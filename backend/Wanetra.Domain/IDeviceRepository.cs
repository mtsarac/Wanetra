namespace Wanetra.Domain;

public interface IDeviceRepository
{
    Task<IReadOnlyList<Device>> ListAsync(CancellationToken cancellationToken);
    Task<Device?> GetAsync(long id, CancellationToken cancellationToken);
    Task<int> CountAsync(CancellationToken cancellationToken);
    void Add(Device device);
    void Remove(Device device);

    void AddEvent(DeviceEvent deviceEvent);
    Task<IReadOnlyList<DeviceEvent>> GetEventsAsync(long? deviceId, int count, CancellationToken cancellationToken);

    void AddScanRun(ScanRun run);
    Task<ScanRun?> GetLatestScanRunAsync(CancellationToken cancellationToken);
    Task<bool> HasSuccessfulScanAsync(CancellationToken cancellationToken);

    void AddAlert(DeviceAlert alert);
    Task<DeviceAlert?> GetOpenOfflineAlertAsync(long deviceId, CancellationToken cancellationToken);

    Task<int> DeleteEventsOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken);
    Task<int> DeleteScanRunsOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
