using Wanetra.Application.SpeedTests;
using Wanetra.Domain;

namespace Wanetra.Api.Contracts;

public sealed record SpeedTestRunResponse(string Status);

public sealed record SpeedTestStatusResponse(
    string State,
    string? Trigger,
    DateTimeOffset? StartedAt,
    string? ErrorMessage)
{
    public static SpeedTestStatusResponse From(SpeedTestStatus status) => new(
        status.State.ToString().ToLowerInvariant(),
        status.Trigger?.ToString().ToLowerInvariant(),
        status.StartedAt,
        status.ErrorMessage);
}

public sealed record SpeedTestResultResponse(
    long Id,
    DateTime Timestamp,
    string Engine,
    bool Success,
    double? DownloadMbps,
    double? UploadMbps,
    double? LatencyMs,
    double? JitterMs,
    double? PacketLossPercent,
    string? ServerName,
    string? ServerLocation,
    string? ServerId,
    string? Isp,
    string? ExternalIp,
    long? DurationMs,
    string? ErrorMessage)
{
    public static SpeedTestResultResponse From(SpeedTestResult result) => new(
        result.Id,
        result.Timestamp,
        result.Engine,
        result.Success,
        result.DownloadMbps,
        result.UploadMbps,
        result.LatencyMs,
        result.JitterMs,
        result.PacketLossPercent,
        result.ServerName,
        result.ServerLocation,
        result.ServerId,
        result.Isp,
        result.ExternalIp,
        result.DurationMs,
        result.ErrorMessage);
}

public sealed record SpeedTestHistoryResponse(
    IReadOnlyList<SpeedTestResultResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages)
{
    public static SpeedTestHistoryResponse From(SpeedTestResultPage page) => new(
        [.. page.Items.Select(SpeedTestResultResponse.From)],
        page.Page,
        page.PageSize,
        page.TotalCount,
        (int)Math.Ceiling(page.TotalCount / (double)page.PageSize));
}
