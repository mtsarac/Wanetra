using Wanetra.Domain;

namespace Wanetra.Api.Contracts;

public sealed record ActiveAlertResponse(
    long Id,
    DateTime StartedAt,
    string Status,
    string Reason,
    double? BaselineDownloadMbps,
    double? WorstDownloadMbps,
    double? BaselineUploadMbps,
    double? WorstUploadMbps)
{
    public static ActiveAlertResponse From(DegradationEvent degradationEvent) => new(
        degradationEvent.Id,
        degradationEvent.StartedAt,
        degradationEvent.Status.ToString().ToLowerInvariant(),
        degradationEvent.Reason,
        degradationEvent.BaselineDownloadMbps,
        degradationEvent.WorstDownloadMbps,
        degradationEvent.BaselineUploadMbps,
        degradationEvent.WorstUploadMbps);
}
