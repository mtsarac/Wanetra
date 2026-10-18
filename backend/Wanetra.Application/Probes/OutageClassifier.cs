using Wanetra.Domain;

namespace Wanetra.Application.Probes;

public enum LayerState
{
    Unknown,
    Up,
    Down,
}

public sealed record Diagnosis(
    LayerState Gateway,
    LayerState Internet,
    LayerState Dns,
    OutageScope? Scope,
    string Reason)
{
    public static readonly Diagnosis Healthy = new(LayerState.Unknown, LayerState.Unknown, LayerState.Unknown, null, string.Empty);
}

public static class OutageClassifier
{
    // A layer is up when any of its targets answered and down when every target failed.
    // Several independent internet targets must all fail, so one rate-limited resolver cannot raise an outage.
    public static Diagnosis Classify(IEnumerable<(ProbeRole Role, bool Success)> results)
    {
        var list = results.ToList();
        var gateway = Layer(list, ProbeRole.Gateway);
        var internet = Layer(list, ProbeRole.Internet);
        var dns = Layer(list, ProbeRole.Dns);

        if (internet == LayerState.Down)
        {
            return gateway == LayerState.Down
                ? new Diagnosis(gateway, internet, dns, OutageScope.LocalNetwork,
                    "The gateway and every internet target are unreachable")
                : new Diagnosis(gateway, internet, dns, OutageScope.Wan,
                    gateway == LayerState.Up
                        ? "Every internet target is unreachable while the gateway responds"
                        : "Every internet target is unreachable");
        }

        if (dns == LayerState.Down)
        {
            return new Diagnosis(gateway, internet, dns, OutageScope.Dns,
                "DNS lookups fail while the internet targets respond");
        }

        return new Diagnosis(gateway, internet, dns, null, string.Empty);
    }

    private static LayerState Layer(List<(ProbeRole Role, bool Success)> results, ProbeRole role)
    {
        var layer = results.Where(result => result.Role == role).ToList();
        if (layer.Count == 0)
        {
            return LayerState.Unknown;
        }

        return layer.Any(result => result.Success) ? LayerState.Up : LayerState.Down;
    }
}
