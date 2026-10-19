using System.Net;
using System.Text.RegularExpressions;
using Wanetra.Domain;

namespace Wanetra.Application.Probes;

public sealed record ProbeTargetInput(
    string? Name,
    string? Kind,
    string? Role,
    string? Target,
    int? Port,
    string? Resolver,
    bool Enabled);

public enum ProbeTargetErrorKind
{
    Invalid,
    Duplicate,
    NotFound,
    LimitReached,
}

public sealed class ProbeTargetException(ProbeTargetErrorKind kind, string message) : Exception(message)
{
    public ProbeTargetErrorKind Kind { get; } = kind;
}

public sealed partial class ProbeTargetService(
    IProbeRepository probes,
    IAppSettingRepository appSettings,
    IProbeMetrics metrics,
    TimeProvider timeProvider)
{
    public const int MaxTargets = 32;

    [GeneratedRegex(@"^(?=.{1,253}$)([A-Za-z0-9_]([A-Za-z0-9_-]{0,61}[A-Za-z0-9_])?)(\.[A-Za-z0-9_]([A-Za-z0-9_-]{0,61}[A-Za-z0-9_])?)*\.?$")]
    private static partial Regex HostnamePattern();

    public Task<IReadOnlyList<ProbeTarget>> ListAsync(CancellationToken cancellationToken) =>
        probes.ListTargetsAsync(onlyEnabled: false, cancellationToken);

    public async Task EnsureDefaultsAsync(CancellationToken cancellationToken)
    {
        var settings = await appSettings.ListAsync(cancellationToken);
        if (settings.Any(setting => setting.Key == ProbeInternalKeys.DefaultsSeeded))
        {
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (await probes.CountTargetsAsync(cancellationToken) == 0)
        {
            foreach (var target in DefaultTargets(now))
            {
                probes.AddTarget(target);
            }

            await probes.SaveChangesAsync(cancellationToken);
        }

        await appSettings.SaveAsync(
            new Dictionary<string, string?> { [ProbeInternalKeys.DefaultsSeeded] = "true" },
            now,
            cancellationToken);
    }

    public async Task<ProbeTarget> CreateAsync(ProbeTargetInput input, CancellationToken cancellationToken)
    {
        if (await probes.CountTargetsAsync(cancellationToken) >= MaxTargets)
        {
            throw new ProbeTargetException(ProbeTargetErrorKind.LimitReached, $"At most {MaxTargets} probe targets are allowed.");
        }

        var target = new ProbeTarget
        {
            Name = string.Empty,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };
        await ApplyAsync(target, input, cancellationToken);
        probes.AddTarget(target);
        await probes.SaveChangesAsync(cancellationToken);
        return target;
    }

    public async Task<ProbeTarget> UpdateAsync(int id, ProbeTargetInput input, CancellationToken cancellationToken)
    {
        var target = await probes.GetTargetAsync(id, cancellationToken)
            ?? throw new ProbeTargetException(ProbeTargetErrorKind.NotFound, "Probe target not found.");
        var previousName = target.Name;
        if (target.DeviceId is not null)
        {
            // The Devices page owns what is probed; the target can only be paused here.
            target.Enabled = input.Enabled;
        }
        else
        {
            await ApplyAsync(target, input, cancellationToken);
        }

        await probes.SaveChangesAsync(cancellationToken);
        if (!string.Equals(previousName, target.Name, StringComparison.Ordinal) || !target.Enabled)
        {
            metrics.RemoveProbeTarget(previousName);
        }

        return target;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var target = await probes.GetTargetAsync(id, cancellationToken)
            ?? throw new ProbeTargetException(ProbeTargetErrorKind.NotFound, "Probe target not found.");
        if (target.DeviceId is not null)
        {
            throw Invalid("This target follows a watched device. Stop watching the device to remove it.");
        }

        probes.RemoveTarget(target);
        await probes.SaveChangesAsync(cancellationToken);
        metrics.RemoveProbeTarget(target.Name);
    }

    private async Task ApplyAsync(ProbeTarget target, ProbeTargetInput input, CancellationToken cancellationToken)
    {
        var name = input.Name?.Trim() ?? string.Empty;
        if (name.Length is < 1 or > 64)
        {
            throw Invalid("Name must be 1 to 64 characters.");
        }

        if (!Enum.TryParse<ProbeKind>(input.Kind, ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
        {
            throw Invalid("Kind must be icmp, tcp, dns or http.");
        }

        if (!Enum.TryParse<ProbeRole>(input.Role, ignoreCase: true, out var role) || !Enum.IsDefined(role))
        {
            throw Invalid("Role must be gateway, internet, dns or custom.");
        }

        if (role == ProbeRole.Dns && kind != ProbeKind.Dns)
        {
            throw Invalid("The dns role needs a dns probe.");
        }

        if (kind == ProbeKind.Dns && role is not (ProbeRole.Dns or ProbeRole.Custom))
        {
            throw Invalid("A dns probe must use the dns or custom role.");
        }

        if (role == ProbeRole.Gateway && kind is not (ProbeKind.Icmp or ProbeKind.Tcp))
        {
            throw Invalid("The gateway role needs an icmp or tcp probe.");
        }

        var host = string.IsNullOrWhiteSpace(input.Target) ? null : input.Target.Trim();
        var resolver = string.IsNullOrWhiteSpace(input.Resolver) ? null : input.Resolver.Trim();
        int? port = input.Port;

        switch (kind)
        {
            case ProbeKind.Icmp:
            case ProbeKind.Tcp:
                if (host is null && role != ProbeRole.Gateway)
                {
                    throw Invalid("A host or IP address is required.");
                }

                if (host is not null)
                {
                    ValidateHost(host);
                }

                if (kind == ProbeKind.Tcp && port is not (>= 1 and <= 65535))
                {
                    throw Invalid("A TCP probe needs a port from 1 to 65535.");
                }

                if (kind == ProbeKind.Icmp)
                {
                    port = null;
                }

                resolver = null;
                break;

            case ProbeKind.Dns:
                if (host is null || !HostnamePattern().IsMatch(host) || IPAddress.TryParse(host, out _))
                {
                    throw Invalid("A DNS probe needs a domain name to look up.");
                }

                if (resolver is not null && !IPAddress.TryParse(resolver, out _))
                {
                    throw Invalid("The resolver must be an IP address, or empty to use the system resolver.");
                }

                port = null;
                break;

            case ProbeKind.Http:
                if (host is null
                    || !Uri.TryCreate(host, UriKind.Absolute, out var uri)
                    || uri.Scheme is not ("http" or "https")
                    || !string.IsNullOrEmpty(uri.UserInfo)
                    || host.Length > 2048)
                {
                    throw Invalid("An HTTP probe needs an http:// or https:// URL without credentials.");
                }

                port = null;
                resolver = null;
                break;
        }

        var existing = await probes.ListTargetsAsync(onlyEnabled: false, cancellationToken);
        if (existing.Any(other => other.Id != target.Id && string.Equals(other.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ProbeTargetException(ProbeTargetErrorKind.Duplicate, "Another probe target already has this name.");
        }

        target.Name = name;
        target.Kind = kind;
        target.Role = role;
        target.Target = host;
        target.Port = port;
        target.Resolver = resolver;
        target.Enabled = input.Enabled;
    }

    private static void ValidateHost(string host)
    {
        if (IPAddress.TryParse(host, out _) || HostnamePattern().IsMatch(host))
        {
            return;
        }

        throw Invalid("The host must be an IP address or a valid hostname.");
    }

    private static ProbeTargetException Invalid(string message) => new(ProbeTargetErrorKind.Invalid, message);

    private static IEnumerable<ProbeTarget> DefaultTargets(DateTime now)
    {
        yield return new ProbeTarget { Name = "Gateway", Kind = ProbeKind.Icmp, Role = ProbeRole.Gateway, CreatedAt = now };
        yield return new ProbeTarget { Name = "Cloudflare 1.1.1.1", Kind = ProbeKind.Icmp, Role = ProbeRole.Internet, Target = "1.1.1.1", CreatedAt = now };
        yield return new ProbeTarget { Name = "Google 8.8.8.8", Kind = ProbeKind.Icmp, Role = ProbeRole.Internet, Target = "8.8.8.8", CreatedAt = now };
        yield return new ProbeTarget { Name = "Quad9 9.9.9.9", Kind = ProbeKind.Icmp, Role = ProbeRole.Internet, Target = "9.9.9.9", CreatedAt = now };
        yield return new ProbeTarget { Name = "DNS lookup", Kind = ProbeKind.Dns, Role = ProbeRole.Dns, Target = "cloudflare.com", CreatedAt = now };
    }
}
