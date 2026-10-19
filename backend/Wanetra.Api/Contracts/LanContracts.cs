using Wanetra.Application.Lan;
using Wanetra.Domain;

namespace Wanetra.Api.Contracts;

public sealed record DeviceResponse(
    long Id,
    string Mac,
    string? Ip,
    string DisplayName,
    string? Name,
    string? Hostname,
    string? Description,
    string? Vendor,
    bool Known,
    bool Watch,
    bool Online,
    bool RandomizedMac,
    DateTime FirstSeenAt,
    DateTime LastSeenAt,
    DateTime LastStateChangeAt)
{
    public static DeviceResponse From(Device device) => new(
        device.Id,
        device.Mac,
        device.CurrentIp,
        DeviceProbeLinker.Label(device),
        device.Name,
        device.Hostname,
        device.Description,
        device.Vendor,
        device.Known,
        device.Watch,
        device.Online,
        device.RandomizedMac,
        device.FirstSeenAt,
        device.LastSeenAt,
        device.LastStateChangeAt);
}

public sealed record DeviceUpdateRequest(string? Name, bool? Known, bool? Watch);

public sealed record DeviceEventResponse(long Id, long DeviceId, DateTime Timestamp, string Type, string? Detail)
{
    public static DeviceEventResponse From(DeviceEvent deviceEvent) => new(
        deviceEvent.Id,
        deviceEvent.DeviceId,
        deviceEvent.Timestamp,
        WireNames.Of(deviceEvent.Type),
        deviceEvent.Detail);
}

public sealed record ScanRunResponse(
    DateTime StartedAt,
    DateTime FinishedAt,
    string Networks,
    int HostsFound,
    bool Success,
    string? Error,
    string Trigger)
{
    public static ScanRunResponse From(ScanRun run) => new(
        run.StartedAt, run.FinishedAt, run.Networks, run.HostsFound, run.Success, run.Error, run.Trigger);
}

public sealed record LanStatusResponse(
    bool Enabled,
    bool Scanning,
    DateTimeOffset? NextScanAt,
    ScanRunResponse? LastScan,
    int TotalDevices,
    int OnlineDevices,
    int UnknownDevices,
    IReadOnlyList<string> Networks,
    bool OnlyContainerNetworks)
{
    public static LanStatusResponse From(LanStatus status) => new(
        status.Enabled,
        status.Scan.State == LanScanState.Running,
        status.NextScanAt,
        status.LastScan is null ? null : ScanRunResponse.From(status.LastScan),
        status.TotalDevices,
        status.OnlineDevices,
        status.UnknownDevices,
        status.Networks,
        status.OnlyContainerNetworks);
}

public sealed record MarkKnownResponse(int Updated);
