using Wanetra.Application.Settings;
using Wanetra.Domain;

namespace Wanetra.Application.Lan;

public sealed record DeviceUpdate(string? Name, bool Known, bool Watch);

public enum DeviceErrorKind
{
    NotFound,
    Invalid,
}

public sealed class DeviceException(DeviceErrorKind kind, string message) : Exception(message)
{
    public DeviceErrorKind Kind { get; } = kind;
}

public sealed record LanStatus(
    bool Enabled,
    LanScanStatus Scan,
    DateTimeOffset? NextScanAt,
    ScanRun? LastScan,
    int TotalDevices,
    int OnlineDevices,
    int UnknownDevices,
    IReadOnlyList<string> Networks,
    bool OnlyContainerNetworks);

public sealed class DeviceService(
    IDeviceRepository devices,
    DeviceProbeLinker linker,
    ILanNetworkDetector detector,
    LanScanCoordinator coordinator,
    SettingsSnapshot settings,
    TimeProvider timeProvider)
{
    public const int MaxNameLength = 64;

    public Task<IReadOnlyList<Device>> ListAsync(CancellationToken cancellationToken) =>
        devices.ListAsync(cancellationToken);

    public async Task<Device> GetAsync(long id, CancellationToken cancellationToken) =>
        await devices.GetAsync(id, cancellationToken) ?? throw NotFound();

    public async Task<Device> UpdateAsync(long id, DeviceUpdate update, CancellationToken cancellationToken)
    {
        var device = await GetAsync(id, cancellationToken);
        var name = string.IsNullOrWhiteSpace(update.Name) ? null : update.Name.Trim();
        if (name is { Length: > MaxNameLength })
        {
            throw new DeviceException(DeviceErrorKind.Invalid, $"The name can be at most {MaxNameLength} characters.");
        }

        device.Name = name;
        device.Known = update.Known;
        device.Watch = update.Watch;
        await linker.SyncAsync(device, cancellationToken);
        await devices.SaveChangesAsync(cancellationToken);
        return device;
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken)
    {
        var device = await GetAsync(id, cancellationToken);
        await linker.RemoveAsync(device.Id, cancellationToken);
        devices.Remove(device);
        await devices.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> MarkAllKnownAsync(CancellationToken cancellationToken)
    {
        var unknown = (await devices.ListAsync(cancellationToken)).Where(device => !device.Known).ToList();
        foreach (var device in unknown)
        {
            device.Known = true;
        }

        await devices.SaveChangesAsync(cancellationToken);
        return unknown.Count;
    }

    public async Task<IReadOnlyList<DeviceEvent>> GetEventsAsync(long? deviceId, int count, CancellationToken cancellationToken)
    {
        if (deviceId is { } id && await devices.GetAsync(id, cancellationToken) is null)
        {
            throw NotFound();
        }

        return await devices.GetEventsAsync(deviceId, count, cancellationToken);
    }

    public async Task<LanStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        var all = await devices.ListAsync(cancellationToken);
        var detection = detector.Detect();
        var configured = settings.GetOptionalString(SettingKeys.LanCidr);
        var networks = configured is not null
            ? configured.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries).ToList()
            : detection.Networks.Select(network => network.Range.ToString()).ToList();

        return new LanStatus(
            settings.GetBool(SettingKeys.LanEnabled),
            coordinator.Status,
            coordinator.NextScanAt,
            await devices.GetLatestScanRunAsync(cancellationToken),
            all.Count,
            all.Count(device => device.Online),
            all.Count(device => !device.Known),
            networks,
            configured is null && detection.OnlyContainerNetworks);
    }

    public async Task<int> DeleteHistoryOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken)
    {
        var events = await devices.DeleteEventsOlderThanAsync(cutoff, cancellationToken);
        var runs = await devices.DeleteScanRunsOlderThanAsync(timeProvider.GetUtcNow().UtcDateTime.AddDays(-30), cancellationToken);
        return events + runs;
    }

    private static DeviceException NotFound() => new(DeviceErrorKind.NotFound, "Device not found.");
}
