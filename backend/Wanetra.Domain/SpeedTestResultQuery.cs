namespace Wanetra.Domain;

public sealed record SpeedTestResultQuery
{
    public DateTime? From { get; init; }

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
