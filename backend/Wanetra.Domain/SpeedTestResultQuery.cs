namespace Wanetra.Domain;

/// <summary>
/// Filters and paging for a speed test history lookup. All filters are optional
/// and combine with AND.
/// </summary>
public sealed record SpeedTestResultQuery
{
    /// <summary>Inclusive lower bound on <see cref="SpeedTestResult.Timestamp"/>, in UTC.</summary>
    public DateTime? From { get; init; }

    /// <summary>Inclusive upper bound on <see cref="SpeedTestResult.Timestamp"/>, in UTC.</summary>
    public DateTime? To { get; init; }

    public bool? Success { get; init; }

    public string? Engine { get; init; }

    public bool NewestFirst { get; init; } = true;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 50;
}

public sealed record SpeedTestResultPage(
    IReadOnlyList<SpeedTestResult> Items,
    int Page,
    int PageSize,
    int TotalCount);
